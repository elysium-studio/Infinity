using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public interface IRecentApplicationStore
{
    event Action<LaunchableApplication>? ApplicationRecorded;

    IReadOnlyList<LaunchableApplication> Applications { get; }

    void RecordForSession(LaunchableApplication application);

    Task RecordAsync(LaunchableApplication application, CancellationToken cancellationToken = default);
}
