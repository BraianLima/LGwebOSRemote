namespace LGwebOSRemote.Models;

public record TVDevice(string Host, string Name, string Model, string? Mac = null);

public record TVApp(string Id, string Title, string? Icon = null);

public record TVInput(string Id, string Label, bool Connected, string AppId);

public record PictureSettings(int Contrast = 80, int Backlight = 80, int Brightness = 50, int Color = 50);

public record RemoteEvent(string Title, string Detail, DateTime Date);
