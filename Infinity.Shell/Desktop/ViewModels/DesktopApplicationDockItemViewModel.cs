using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopApplicationDockItemViewModel(LaunchableApplication application, DesktopApplicationDockSource source) : DesktopApplicationItemViewModel(application)
{
    public DesktopApplicationDockSource Source { get; } = source;

    public bool CanUnpin => Source == DesktopApplicationDockSource.Infinity;
}
