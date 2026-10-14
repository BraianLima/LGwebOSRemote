using System.Net.WebSockets;
using System.Text;

namespace LGwebOSRemote.Services;

public class PointerChannel : IDisposable
{
    private readonly string _socketUrl;
    private readonly Action<bool> _onStateChange;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public PointerChannel(string socketUrl, Action<bool> onStateChange)
    {
        _socketUrl = socketUrl;
        _onStateChange = onStateChange;
    }

    public async Task ConnectAsync()
    {
        try
        {
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();
            _ws.Options.RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            await _ws.ConnectAsync(new Uri(_socketUrl), _cts.Token);
            _onStateChange(true);
            _ = ReceiveLoopAsync();
        }
        catch
        {
            _onStateChange(false);
        }
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[1024];
        try
        {
            while (_ws != null && _ws.State == WebSocketState.Open && _cts?.IsCancellationRequested == false)
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch
        {
        }
        finally
        {
            _onStateChange(false);
        }
    }

    public void SendButton(string name)
    {
        SendMessage($"type:button\nname:{name}\n\n");
    }

    public void SendMove(int dx, int dy)
    {
        int clampedX = Math.Clamp(dx, -100, 100);
        int clampedY = Math.Clamp(dy, -100, 100);
        SendMessage($"type:move\ndx:{clampedX}\ndy:{clampedY}\ndown:0\n\n");
    }

    public void SendClick()
    {
        SendMessage("type:click\n\n");
    }

    private void SendMessage(string message)
    {
        if (_ws?.State == WebSocketState.Open)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            _ = _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _ws?.Dispose(); } catch { }
        _ws = null;
        _onStateChange(false);
    }
}
