namespace Infinity.Platform.Abstractions;

public sealed record WindowCommandState(bool CanMinimize, bool CanMaximize, bool CanRestore)
{
    public static WindowCommandState Unavailable { get; } = new(false, false, false);
}
