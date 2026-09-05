using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public interface IRecentApplicationStore
{
    event Action<LaunchableApplication>? ApplicationRecorded;

    IReadOnlyList<LaunchableApplication> Applications { get; }


    Task RecordAsync(LaunchableApplication application, CancellationToken cancellationToken = default);
}
