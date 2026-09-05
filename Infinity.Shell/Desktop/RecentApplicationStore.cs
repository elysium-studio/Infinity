using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infinity.Shell;

public sealed class RecentApplicationStore : IRecentApplicationStore
{
    private const int MaximumApplications = 6;
    private readonly Lock syncRoot = new();
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly List<LaunchableApplication> applications;
    private readonly IWritableOptions<Settings> writer;
    private readonly ILogger<RecentApplicationStore> logger;

    public RecentApplicationStore(IOptionsMonitor<Settings> settings, IWritableOptions<Settings> writer, ILogger<RecentApplicationStore> logger)
    {
        applications = [..(settings.CurrentValue.RecentApplications ?? []).Take(MaximumApplications)];
        this.writer = writer;
        this.logger = logger;
    }


    public event Action<LaunchableApplication>? ApplicationRecorded;

    public IReadOnlyList<LaunchableApplication> Applications
    {
        get
        {
            lock (syncRoot)
            {
                return[..applications];
            }
        }
    }


    public async Task RecordAsync(LaunchableApplication application, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LaunchableApplication> snapshot = RecordCore(application);
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            Settings updated = await writer.ReadAsync(cancellationToken) ?? new Settings();
            updated.RecentApplications = [..snapshot];
            await writer.WriteAsync(updated, cancellationToken);
        }
        catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not save the recent application list");
        }
        finally
        {
            writeGate.Release();
        }
    }


    public void RecordForSession(LaunchableApplication application) => RecordCore(application);

    private IReadOnlyList<LaunchableApplication> RecordCore(LaunchableApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        IReadOnlyList<LaunchableApplication> snapshot;
        lock (syncRoot)
        {
            applications.RemoveAll(candidate => string.Equals(candidate.Id, application.Id, StringComparison.Ordinal));
            applications.Insert(0, application);
            if (applications.Count > MaximumApplications)
            {
                applications.RemoveRange(MaximumApplications, applications.Count - MaximumApplications);
            }

            snapshot = [..applications];
        }

        ApplicationRecorded?.Invoke(application);
        return snapshot;
    }
}
