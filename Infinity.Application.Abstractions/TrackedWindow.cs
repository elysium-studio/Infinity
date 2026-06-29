namespace Infinity.Application.Abstractions;

public record TrackedWindow
{
    public required IntPtr Handle { get; init; }

    public required int CanvasX { get; set; }

    public required int CanvasY { get; set; }

    public required int Width { get; set; }

    public required int Height { get; set; }

    public int LastPlacedX { get; set; }

    public int LastPlacedY { get; set; }

    public int ZIndex { get; set; }

    public string Title { get; set; } = string.Empty;
}