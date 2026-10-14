using System.Net;
using System.Net.Sockets;
using Android.Content;
using Android.Net;

namespace LGwebOSRemote.Services;

public class LocalMediaServer : IDisposable
{
    private readonly Context _context;
    private readonly Android.Net.Uri _mediaUri;
    private readonly string _mimeType;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public int Port { get; private set; }

    public LocalMediaServer(Context context, Android.Net.Uri mediaUri, string mimeType)
    {
        _context = context;
        _mediaUri = mediaUri;
        _mimeType = mimeType;
    }

    public static string GetLocalIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
        }
        catch { }
        return "127.0.0.1";
    }

    public string Start()
    {
        _listener = new TcpListener(IPAddress.Any, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _cts = new CancellationTokenSource();

        _ = Task.Run(() => AcceptClientsAsync(_cts.Token));

        string ip = GetLocalIpAddress();
        return $"http://{ip}:{Port}/media";
    }

    private async Task AcceptClientsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleClientAsync(client));
            }
            catch
            {
                break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream))
            {
                string? requestLine = await reader.ReadLineAsync();
                if (requestLine != null && requestLine.StartsWith("GET"))
                {
                    var cr = _context.ContentResolver;
                    if (cr == null) return;

                    using var inputStream = cr.OpenInputStream(_mediaUri);
                    if (inputStream == null) return;

                    long length = -1;
                    try
                    {
                        var pfd = cr.OpenAssetFileDescriptor(_mediaUri, "r");
                        length = pfd?.Length ?? -1;
                        pfd?.Close();
                    }
                    catch { }

                    var header = $"HTTP/1.1 200 OK\r\nContent-Type: {_mimeType}\r\n";
                    if (length > 0) header += $"Content-Length: {length}\r\n";
                    header += "Connection: close\r\nAccept-Ranges: none\r\n\r\n";

                    byte[] headerBytes = System.Text.Encoding.UTF8.GetBytes(header);
                    await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                    await inputStream.CopyToAsync(stream);
                    await stream.FlushAsync();
                }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        _listener = null;
    }
}
