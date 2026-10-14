using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using Android.Content;
using Android.Net.Wifi;
using LGwebOSRemote.Models;

namespace LGwebOSRemote.Services;

public static class TVDiscovery
{
    public static async Task<List<TVDevice>> SearchAsync(Context context, int timeoutMs = 4000)
    {
        var devices = new Dictionary<string, TVDevice>();
        WifiManager? wifi = context.ApplicationContext?.GetSystemService(Context.WifiService) as WifiManager;
        WifiManager.MulticastLock? mLock = wifi?.CreateMulticastLock("LGwebOS_SSDP");
        mLock?.SetReferenceCounted(true);
        mLock?.Acquire();

        try
        {
            using var udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.ReceiveTimeout = 1000;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

            string query = "M-SEARCH * HTTP/1.1\r\n" +
                           "HOST: 239.255.255.250:1900\r\n" +
                           "MAN: \"ssdp:discover\"\r\n" +
                           "MX: 2\r\n" +
                           "ST: urn:lge-com:service:webos-second-screen:1\r\n\r\n";

            byte[] bytes = Encoding.UTF8.GetBytes(query);
            var endpoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
            await udp.SendAsync(bytes, bytes.Length, endpoint);

            var start = DateTime.UtcNow;
            var locations = new HashSet<string>();

            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                try
                {
                    var receiveTask = udp.ReceiveAsync();
                    var completedTask = await Task.WhenAny(receiveTask, Task.Delay(1000));
                    if (completedTask == receiveTask)
                    {
                        var result = await receiveTask;
                        string text = Encoding.UTF8.GetString(result.Buffer);
                        foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (line.StartsWith("location:", StringComparison.OrdinalIgnoreCase))
                            {
                                string loc = line.Substring(9).Trim();
                                if (loc.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                                {
                                    locations.Add(loc);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    break;
                }
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            foreach (var loc in locations)
            {
                try
                {
                    var uri = new Uri(loc);
                    string xml = await http.GetStringAsync(uri);
                    var doc = XDocument.Parse(xml);
                    XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                    var device = doc.Descendants(ns + "device").FirstOrDefault();
                    if (device != null)
                    {
                        string name = device.Element(ns + "friendlyName")?.Value ?? "LG TV";
                        string model = device.Element(ns + "modelNumber")?.Value ?? "webOS";
                        string mfg = device.Element(ns + "manufacturer")?.Value ?? "";

                        // Do not use arbitrary UDN UUID endings as MAC, as LG webOS TV UDN does not match the Wi-Fi MAC.
                        // The actual Wi-Fi MAC is retrieved reliably via the webOS connection manager API once connected.
                        if (name.Contains("LG", StringComparison.OrdinalIgnoreCase) || 
                            mfg.Contains("LG", StringComparison.OrdinalIgnoreCase) ||
                            model.Contains("LG", StringComparison.OrdinalIgnoreCase))
                        {
                            devices[uri.Host] = new TVDevice(uri.Host, name, model, null);
                        }
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
        finally
        {
            try { mLock?.Release(); } catch { }
        }

        return devices.Values.OrderBy(d => d.Name).ToList();
    }

    public static async Task<bool> WakeAsync(string mac, string? host = null, Context? context = null)
    {
        return await Task.Run(() =>
        {
            WifiManager.MulticastLock? mLock = null;
            try
            {
                if (context != null)
                {
                    var wifi = context.ApplicationContext?.GetSystemService(Context.WifiService) as WifiManager;
                    mLock = wifi?.CreateMulticastLock("LGwebOS_WakeLock");
                    mLock?.SetReferenceCounted(false);
                    mLock?.Acquire();
                }

                // Sanitize MAC: remove colons, hyphens, dots, and whitespace
                string cleanMac = mac.Replace(":", "").Replace("-", "").Replace(".", "").Replace(" ", "").Trim();
                if (cleanMac.Length != 12) return false;

                byte[] macBytes = new byte[6];
                for (int i = 0; i < 6; i++)
                {
                    macBytes[i] = Convert.ToByte(cleanMac.Substring(i * 2, 2), 16);
                }

                byte[] packet = new byte[102];
                for (int i = 0; i < 6; i++) packet[i] = 0xFF;
                for (int i = 0; i < 16; i++)
                {
                    Buffer.BlockCopy(macBytes, 0, packet, 6 + i * 6, 6);
                }

                using var udp = new UdpClient();
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                udp.EnableBroadcast = true;

                var endpoints = new List<IPEndPoint>();

                // 1. Calculate subnet broadcast from host IP first (most reliable for Wi-Fi routers)
                if (!string.IsNullOrWhiteSpace(host) && IPAddress.TryParse(host, out var hostIp))
                {
                    byte[] ipBytes = hostIp.GetAddressBytes();
                    if (ipBytes.Length == 4)
                    {
                        var subnetBcast = new IPAddress(new byte[] { ipBytes[0], ipBytes[1], ipBytes[2], 255 });
                        endpoints.Add(new IPEndPoint(subnetBcast, 9));
                        endpoints.Add(new IPEndPoint(subnetBcast, 7));
                        endpoints.Add(new IPEndPoint(subnetBcast, 12287));
                    }
                    endpoints.Add(new IPEndPoint(hostIp, 9));
                    endpoints.Add(new IPEndPoint(hostIp, 7));
                }

                // 2. Also try dynamic Wi-Fi broadcast from Android DhcpInfo if available
                if (context != null)
                {
                    try
                    {
                        var wifiMgr = context.ApplicationContext?.GetSystemService(Context.WifiService) as WifiManager;
                        var dhcp = wifiMgr?.DhcpInfo;
                        if (dhcp != null && dhcp.IpAddress != 0)
                        {
                            int bcastInt = (dhcp.IpAddress & dhcp.Netmask) | ~dhcp.Netmask;
                            byte[] bcastQuad = new byte[4];
                            for (int k = 0; k < 4; k++)
                                bcastQuad[k] = (byte)((bcastInt >> (k * 8)) & 0xFF);
                            var directBcast = new IPAddress(bcastQuad);
                            endpoints.Add(new IPEndPoint(directBcast, 9));
                            endpoints.Add(new IPEndPoint(directBcast, 7));
                        }
                    }
                    catch { }
                }

                // 3. Fallback to standard global broadcast
                endpoints.Add(new IPEndPoint(IPAddress.Parse("255.255.255.255"), 9));
                endpoints.Add(new IPEndPoint(IPAddress.Parse("255.255.255.255"), 7));

                Android.Util.Log.Info("LGWoL", $"Sending WoL to MAC {cleanMac} via {endpoints.Count} endpoints");

                // Send 5 bursts with small delays
                for (int r = 0; r < 5; r++)
                {
                    foreach (var ep in endpoints)
                    {
                        try { udp.Send(packet, packet.Length, ep); } catch (Exception ex) {
                            Android.Util.Log.Warn("LGWoL", $"Failed to send to {ep}: {ex.Message}");
                        }
                    }
                    Thread.Sleep(30);
                }

                Android.Util.Log.Info("LGWoL", "WoL packets successfully sent!");
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                try { mLock?.Release(); } catch { }
            }
        });
    }
}
