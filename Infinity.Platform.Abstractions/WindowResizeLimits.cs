namespace Infinity.Platform.Abstractions;

public readonly record struct WindowResizeLimits(int MinimumWidth, int MinimumHeight, int MaximumWidth, int MaximumHeight);
