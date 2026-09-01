using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public interface IDesktopApplicationPinStore
{
    event Action? PinsChanged;

    IReadOnlyList<LaunchableApplication> Applications { get; }

    Task PinAsync(LaunchableApplication application, CancellationToken cancellationToken = default);

    Task UnpinAsync(LaunchableApplication application, CancellationToken cancellationToken = default);
}
