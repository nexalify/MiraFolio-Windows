namespace MiraFolio.Core.Models;

public record MonitorInfo(
    int Index,
    string DevicePath,
    int Left,
    int Top,
    int Width,
    int Height
)
{
    public ImageOrientation Orientation =>
        Width >= Height ? ImageOrientation.Landscape : ImageOrientation.Portrait;

    public string FriendlyName => $"Display {Index + 1} ({Width}x{Height})";
}
