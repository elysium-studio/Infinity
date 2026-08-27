namespace Infinity.Platform.Abstractions;

public sealed record ApplicationIcon(int Width, int Height, byte[] Pixels);

public sealed record LaunchableApplication(string Id, string DisplayName);
