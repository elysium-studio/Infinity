namespace Infinity.Application.Abstractions;

public sealed class NavigationStartedEventArgs(int page) : EventArgs
{
    public int Page { get; } = page;
}
