using System.Text;

namespace LGwebOSRemote.Services;

public class DLNACaster
{
    private readonly string _host;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private readonly List<string> _candidateUrls;
    private string? _activeControlUrl;

    public DLNACaster(string host)
    {
        _host = host;
        _candidateUrls = new List<string>
        {
            $"http://{_host}:1400/MediaRenderer/AVTransport/control",
            $"http://{_host}:1780/AVTransport/control",
            $"http://{_host}:8080/MediaRenderer/AVTransport/control",
            $"http://{_host}:1400/xml/AVTransport/control"
        };
    }

    public async Task<bool> SetUriAsync(string mediaUrl, string title, string mimeType)
    {
        string metadata = $@"
<DIDL-Lite xmlns=""urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/"" xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:upnp=""urn:schemas-upnp-org:metadata-1-0/upnp/"">
    <item id=""1"" parentID=""0"" restricted=""1"">
        <dc:title>{title}</dc:title>
        <upnp:class>object.item.videoItem</upnp:class>
        <res protocolInfo=""http-get:*:{mimeType}:*"">{mediaUrl}</res>
    </item>
</DIDL-Lite>".Trim().Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        string body = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
    <s:Body>
        <u:SetAVTransportURI xmlns:u=""urn:schemas-upnp-org:service:AVTransport:1"">
            <InstanceID>0</InstanceID>
            <CurrentURI>{mediaUrl}</CurrentURI>
            <CurrentURIMetaData>{metadata}</CurrentURIMetaData>
        </u:SetAVTransportURI>
    </s:Body>
</s:Envelope>";

        foreach (var url in _candidateUrls)
        {
            if (await SendSoapAsync(url, "SetAVTransportURI", body))
            {
                _activeControlUrl = url;
                return true;
            }
        }
        return false;
    }

    public async Task<bool> PlayAsync()
    {
        string url = _activeControlUrl ?? _candidateUrls[0];
        string body = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
    <s:Body>
        <u:Play xmlns:u=""urn:schemas-upnp-org:service:AVTransport:1"">
            <InstanceID>0</InstanceID>
            <Speed>1</Speed>
        </u:Play>
    </s:Body>
</s:Envelope>";
        return await SendSoapAsync(url, "Play", body);
    }

    public async Task<bool> PauseAsync()
    {
        string url = _activeControlUrl ?? _candidateUrls[0];
        string body = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
    <s:Body>
        <u:Pause xmlns:u=""urn:schemas-upnp-org:service:AVTransport:1"">
            <InstanceID>0</InstanceID>
        </u:Pause>
    </s:Body>
</s:Envelope>";
        return await SendSoapAsync(url, "Pause", body);
    }

    public async Task<bool> StopAsync()
    {
        string url = _activeControlUrl ?? _candidateUrls[0];
        string body = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
    <s:Body>
        <u:Stop xmlns:u=""urn:schemas-upnp-org:service:AVTransport:1"">
            <InstanceID>0</InstanceID>
        </u:Stop>
    </s:Body>
</s:Envelope>";
        return await SendSoapAsync(url, "Stop", body);
    }

    private async Task<bool> SendSoapAsync(string url, string action, string body)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("SOAPAction", $"\"urn:schemas-upnp-org:service:AVTransport:1#{action}\"");
            request.Content = new StringContent(body, Encoding.UTF8, "text/xml");

            using var response = await _http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
