using Infinity.Application.Abstractions;

namespace Infinity.Tests;

internal sealed class TestForegroundWindowCoordinator(List<string>? operations = null) : IForegroundWindowCoordinator
{
    public void HandleForegroundWindowChanged(IntPtr handle)
    {
    }


    public void HandleWindowMinimizeStarted(IntPtr handle)
    {
    }


    public void HandleWindowMinimizeEnded(IntPtr handle)
    {
    }


    public void NotifyWindowClosed(IntPtr handle)
    {
    }


    public void SuppressForegroundFollow() => operations?.Add("SuppressForegroundFollow");
}
