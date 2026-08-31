namespace Infinity.Platform.Abstractions;

public interface IApplicationLauncher
{
    bool TryLaunch(LaunchableApplication application);
}
