namespace Infinity.Application.Abstractions;

public sealed record WindowPeekChangedEventArgs(IntPtr Handle, bool IsPeeking);