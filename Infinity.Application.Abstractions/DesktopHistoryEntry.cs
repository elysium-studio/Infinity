namespace Infinity.Application.Abstractions;

public record DesktopHistoryEntry(long Id,
    IntPtr WindowHandle,
    int Page,
    string WindowTitle,
    DateTimeOffset VisitedAt)
{
    public bool HasWindow => WindowHandle != default;
}
