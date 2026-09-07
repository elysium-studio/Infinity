namespace Infinity.Platform.Abstractions;

public sealed record WindowContentSnapshot(
    int Width,
    int Height,
    byte[] Pixels);
