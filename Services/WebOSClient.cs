using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Android.Content;
using Android.Content.Res;
using LGwebOSRemote.Models;

namespace LGwebOSRemote.Services;

public class WebOSClient : IDisposable
{
    public interface IListener
    {
        void OnConnected();
        void OnPairingRequested();
        void OnPaired(string clientKey);
        void OnDisconnected(string? reason);
        void OnVolumeChanged(int volume, bool muted);
        void OnForegroundAppChanged(string appId, string title);
        void OnPointerReady(bool ready);
        void OnError(string message);
    }

    private readonly Context _context;
    public string Host { get; }
    private readonly string? _storedClientKey;
    private readonly IListener _listener;

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private PointerChannel? _pointerChannel;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonNode>> _pendingRequests = new();
    private readonly ConcurrentDictionary<string, Action<JsonNode>> _subscriptions = new();

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public WebOSClient(Context context, string host, string? storedClientKey, IListener listener)
    {
        _context = context;
        Host = host;
        _storedClientKey = storedClientKey;
        _listener = listener;
    }

    public async Task ConnectAsync()
    {
        try
        {
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();
            _ws.Options.RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            var uri = new Uri($"wss://{Host}:3001/");
            await _ws.ConnectAsync(uri, _cts.Token);

            _ = ReceiveLoopAsync();
            await SendRegistrationAsync();
        }
        catch (Exception ex)
        {
            _listener.OnDisconnected(ex.Message);
        }
    }

    private async Task SendRegistrationAsync()
    {
        try
        {
            using var stream = _context.Assets?.Open("registration.json");
            if (stream == null) throw new InvalidOperationException("registration.json not found in assets");
            using var reader = new StreamReader(stream);
            string jsonText = await reader.ReadToEndAsync();

            var regObj = JsonNode.Parse(jsonText)?.AsObject() ?? new JsonObject();
            if (!string.IsNullOrEmpty(_storedClientKey))
            {
                regObj["client-key"] = _storedClientKey;
            }

            var registerMsg = new JsonObject
            {
                ["type"] = "register",
                ["id"] = "register",
                ["payload"] = regObj
            };

            await SendRawAsync(registerMsg.ToJsonString());
        }
        catch (Exception ex)
        {
            _listener.OnError($"Failed to send registration: {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[16384];
        var ms = new MemoryStream();

        try
        {
            while (_ws != null && _ws.State == WebSocketState.Open && _cts?.IsCancellationRequested == false)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close) break;

                string text = Encoding.UTF8.GetString(ms.ToArray());
                HandleMessage(text);
            }
        }
        catch
        {
        }
        finally
        {
            Cleanup();
            _listener.OnDisconnected("Connection closed");
        }
    }

    private void HandleMessage(string message)
    {
        try
        {
            var node = JsonNode.Parse(message);
            if (node == null) return;

            string id = node["id"]?.GetValue<string>() ?? "";
            string type = node["type"]?.GetValue<string>() ?? "";
            var payload = node["payload"] ?? new JsonObject();

            if (id == "register")
            {
                if (type == "response")
                {
                    _listener.OnPairingRequested();
                }
                else if (type == "registered")
                {
                    string newKey = payload["client-key"]?.GetValue<string>() ?? "";
                    if (!string.IsNullOrEmpty(newKey))
                    {
                        _listener.OnPaired(newKey);
                    }
                    _listener.OnConnected();
                    _ = InitSubscriptionsAndPointerAsync();
                }
                return;
            }

            if (type == "error")
            {
                string errorMsg = node["error"]?.GetValue<string>() ?? "TV rejected command";
                if (_pendingRequests.TryRemove(id, out var tcs))
                {
                    tcs.TrySetException(new Exception(errorMsg));
                }
                return;
            }

            if (type == "response" || type == "registered")
            {
                if (_pendingRequests.TryRemove(id, out var tcs))
                {
                    tcs.TrySetResult(payload);
                }

                if (_subscriptions.TryGetValue(id, out var subAction))
                {
                    subAction(payload);
                }
            }
        }
        catch (Exception ex)
        {
            _listener.OnError($"Error parsing TV response: {ex.Message}");
        }
    }

    private async Task InitSubscriptionsAndPointerAsync()
    {
        // Volume subscription
        await SubscribeAsync("audio/getVolume", payload =>
        {
            var status = payload["volumeStatus"] ?? payload;
            int vol = status["volume"]?.GetValue<int>() ?? -1;
            bool muted = status["mute"]?.GetValue<bool>() ?? status["muted"]?.GetValue<bool>() ?? false;
            if (vol >= 0)
            {
                _listener.OnVolumeChanged(vol, muted);
            }
        });

        // Foreground app subscription
        await SubscribeAsync("com.webos.applicationManager/getForegroundAppInfo", payload =>
        {
            string appId = payload["appId"]?.GetValue<string>() ?? "";
            string title = payload["title"]?.GetValue<string>() ?? "";
            _listener.OnForegroundAppChanged(appId, title);
        });

        // Initialize Pointer Channel
        try
        {
            var resp = await SendRequestAsync("com.webos.service.networkinput/getPointerInputSocket");
            string socketPath = resp["socketPath"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrEmpty(socketPath))
            {
                _pointerChannel?.Dispose();
                _pointerChannel = new PointerChannel(socketPath, ready => _listener.OnPointerReady(ready));
                await _pointerChannel.ConnectAsync();
            }
        }
        catch
        {
            _listener.OnPointerReady(false);
        }
    }

    public async Task<JsonNode> SendRequestAsync(string uri, JsonNode? payload = null)
    {
        string reqId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[reqId] = tcs;

        var msg = new JsonObject
        {
            ["type"] = "request",
            ["id"] = reqId,
            ["uri"] = $"ssap://{uri}",
            ["payload"] = payload ?? new JsonObject()
        };

        await SendRawAsync(msg.ToJsonString());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        cts.Token.Register(() => tcs.TrySetCanceled());

        return await tcs.Task;
    }

    public async Task SubscribeAsync(string uri, Action<JsonNode> callback)
    {
        string subId = Guid.NewGuid().ToString();
        _subscriptions[subId] = callback;

        var msg = new JsonObject
        {
            ["type"] = "subscribe",
            ["id"] = subId,
            ["uri"] = $"ssap://{uri}"
        };

        await SendRawAsync(msg.ToJsonString());
    }

    private async Task SendRawAsync(string json)
    {
        if (_ws?.State == WebSocketState.Open)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        else
        {
            throw new InvalidOperationException("WebSocket is not connected");
        }
    }

    // High level TV APIs
    public async Task<List<TVApp>> ListAppsAsync()
    {
        var resp = await SendRequestAsync("com.webos.applicationManager/listLaunchPoints");
        var list = new List<TVApp>();
        var launchPoints = resp["launchPoints"]?.AsArray();
        if (launchPoints != null)
        {
            foreach (var item in launchPoints)
            {
                string id = item?["id"]?.GetValue<string>() ?? "";
                string title = item?["title"]?.GetValue<string>() ?? "";
                string? icon = item?["icon"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(id))
                {
                    list.Add(new TVApp(id, title, icon));
                }
            }
        }
        return list;
    }

    public async Task LaunchAppAsync(string appId)
    {
        await SendRequestAsync("system.launcher/launch", new JsonObject { ["id"] = appId });
    }

    public async Task CloseAppAsync(string appId)
    {
        await SendRequestAsync("system.launcher/close", new JsonObject { ["id"] = appId });
    }

    public async Task<List<TVInput>> GetInputsAsync()
    {
        var resp = await SendRequestAsync("tv/getExternalInputList");
        var list = new List<TVInput>();
        var devices = resp["devices"]?.AsArray();
        if (devices != null)
        {
            foreach (var item in devices)
            {
                string id = item?["id"]?.GetValue<string>() ?? "";
                string label = item?["label"]?.GetValue<string>() ?? id;
                var subList = item?["subList"]?.AsArray();
                if (subList != null && subList.Count > 0)
                {
                    string cec = subList[0]?["osdName"]?.GetValue<string>() ?? "";
                    if (!string.IsNullOrEmpty(cec)) label = cec;
                }
                bool connected = item?["connected"]?.GetValue<bool>() ?? false;
                string appId = item?["appId"]?.GetValue<string>() ?? "";
                list.Add(new TVInput(id, label, connected, appId));
            }
        }
        return list;
    }

    public async Task SwitchInputAsync(string inputId)
    {
        await SendRequestAsync("tv/switchInput", new JsonObject { ["inputId"] = inputId });
    }

    public async Task SetVolumeAsync(int volume)
    {
        await SendRequestAsync("audio/setVolume", new JsonObject { ["volume"] = Math.Clamp(volume, 0, 100) });
    }

    public async Task VolumeUpAsync() => await SendRequestAsync("audio/volumeUp");
    public async Task VolumeDownAsync() => await SendRequestAsync("audio/volumeDown");

    public async Task SetMuteAsync(bool mute)
    {
        await SendRequestAsync("audio/setMute", new JsonObject { ["mute"] = mute });
    }

    public async Task TurnOffAsync() => await SendRequestAsync("system/turnOff");

    public async Task MediaControlAsync(string action) => await SendRequestAsync($"media.controls/{action}");

    public async Task SendTextAsync(string text)
    {
        await SendRequestAsync("com.webos.service.ime/insertText", new JsonObject
        {
            ["text"] = text,
            ["replace"] = false
        });
    }

    public async Task SendEnterKeyAsync() => await SendRequestAsync("com.webos.service.ime/sendEnterKey", new JsonObject());

    public async Task SendDeleteAsync(int count = 1)
    {
        await SendRequestAsync("com.webos.service.ime/deleteCharacters", new JsonObject
        {
            ["count"] = count
        });
    }

    public async Task<(string? wifiMac, string? wiredMac)> GetMacAddressAsync()
    {
        try
        {
            var resp = await SendRequestAsync("com.webos.service.connectionmanager/getinfo");
            string? wifi = resp["wifiInfo"]?["macAddress"]?.GetValue<string>();
            string? wired = resp["wiredInfo"]?["macAddress"]?.GetValue<string>();
            return (wifi, wired);
        }
        catch
        {
            return (null, null);
        }
    }

    public async Task<PictureSettings> GetPictureSettingsAsync()
    {
        try
        {
            var keys = new JsonArray { (JsonNode)"contrast", (JsonNode)"backlight", (JsonNode)"brightness", (JsonNode)"color" };
            var resp = await SendRequestAsync("settings/getSystemSettings", new JsonObject
            {
                ["category"] = "picture",
                ["keys"] = keys
            });
            var s = resp["settings"];
            return new PictureSettings(
                Contrast: s?["contrast"]?.GetValue<int>() ?? 80,
                Backlight: s?["backlight"]?.GetValue<int>() ?? 80,
                Brightness: s?["brightness"]?.GetValue<int>() ?? 50,
                Color: s?["color"]?.GetValue<int>() ?? 50
            );
        }
        catch
        {
            return new PictureSettings();
        }
    }

    public async Task SetPictureSettingsAsync(PictureSettings settings)
    {
        await SendRequestAsync("settings/setSystemSettings", new JsonObject
        {
            ["category"] = "picture",
            ["settings"] = new JsonObject
            {
                ["contrast"] = Math.Clamp(settings.Contrast, 0, 100),
                ["backlight"] = Math.Clamp(settings.Backlight, 0, 100),
                ["brightness"] = Math.Clamp(settings.Brightness, 0, 100),
                ["color"] = Math.Clamp(settings.Color, 0, 100)
            }
        });
    }

    // Pointer & Button input
    public void SendButton(string name)
    {
        if (_pointerChannel?.IsConnected == true)
        {
            _pointerChannel.SendButton(name);
        }
        else
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (name == "HOME") await SendRequestAsync("system.launcher/open");
                    else if (name == "BACK") await SendRequestAsync("system/launcher/close");
                    else if (name == "ENTER")
                    {
                        try { await SendEnterKeyAsync(); } catch { }
                    }

                    await ReconnectPointerAsync();
                    if (_pointerChannel?.IsConnected == true)
                    {
                        _pointerChannel.SendButton(name);
                    }
                }
                catch { }
            });
        }
    }

    public void SendOk()
    {
        SendButton("ENTER");
    }

    public async Task ReconnectPointerAsync()
    {
        if (_pointerChannel?.IsConnected == true) return;
        try
        {
            var resp = await SendRequestAsync("com.webos.service.networkinput/getPointerInputSocket");
            string socketPath = resp["socketPath"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrEmpty(socketPath))
            {
                _pointerChannel?.Dispose();
                _pointerChannel = new PointerChannel(socketPath, ready => _listener.OnPointerReady(ready));
                await _pointerChannel.ConnectAsync();
            }
        }
        catch
        {
            _listener.OnPointerReady(false);
        }
    }

    public void MovePointer(int dx, int dy)
    {
        _pointerChannel?.SendMove(dx, dy);
    }

    public void ClickPointer()
    {
        _pointerChannel?.SendClick();
    }

    public void Disconnect()
    {
        Cleanup();
        try { _ws?.CloseAsync(WebSocketCloseStatus.NormalClosure, "User disconnected", CancellationToken.None); } catch { }
    }

    private void Cleanup()
    {
        _pointerChannel?.Dispose();
        _pointerChannel = null;
        foreach (var tcs in _pendingRequests.Values)
        {
            tcs.TrySetCanceled();
        }
        _pendingRequests.Clear();
        _subscriptions.Clear();
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        Disconnect();
        _ws?.Dispose();
        _ws = null;
    }
}
