namespace Infinity.Application.Abstractions;

public class NavigationStartedEventArgs(int page) :
    EventArgs
{
    public int Page { get; } = page;
}