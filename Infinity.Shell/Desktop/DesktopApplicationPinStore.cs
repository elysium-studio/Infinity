using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infinity.Shell;

public sealed class DesktopApplicationPinStore : IDesktopApplicationPinStore
{
    private const int MaximumApplications = 12;

    private readonly Lock syncRoot = new();
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly List<LaunchableApplication> applications;
    private readonly IWritableOptions<Settings> writer;
    private readonly ILogger<DesktopApplicationPinStore> logger;

    public DesktopApplicationPinStore(
        IOptionsMonitor<Settings> settings,
        IWritableOptions<Settings> writer,
        ILogger<DesktopApplicationPinStore> logger)
    {
        applications = [.. (settings.CurrentValue.PinnedApplications ?? [])
            .DistinctBy(application => application.Id, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumApplications)];
        this.writer = writer;
        this.logger = logger;
    }

    public event Action? PinsChanged;

    public IReadOnlyList<LaunchableApplication> Applications
    {
        get
        {
            lock (syncRoot)
            {
                return [.. applications];
            }
        }
    }

    public Task PinAsync(LaunchableApplication application, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        return UpdateAsync(application, pin: true, cancellationToken);
    }

    public Task UnpinAsync(LaunchableApplication application, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        return UpdateAsync(application, pin: false, cancellationToken);
    }

    private async Task UpdateAsync(
        LaunchableApplication application,
        bool pin,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LaunchableApplication>? snapshot = UpdateCore(application, pin);

        if (snapshot is null)
        {
            return;
        }

        await writeGate.WaitAsync(cancellationToken);

        try
        {
            Settings updated = await writer.ReadAsync(cancellationToken) ?? new Settings();
            updated.PinnedApplications = [.. snapshot];
            await writer.WriteAsync(updated, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not save the Infinity application pins");
        }
        finally
        {
            writeGate.Release();
        }
    }

    private IReadOnlyList<LaunchableApplication>? UpdateCore(LaunchableApplication application, bool pin)
    {
        IReadOnlyList<LaunchableApplication> snapshot;

        lock (syncRoot)
        {
            int existingIndex = applications.FindIndex(candidate =>
                string.Equals(candidate.Id, application.Id, StringComparison.OrdinalIgnoreCase));

            if ((pin && existingIndex >= 0) || (!pin && existingIndex < 0))
            {
                return null;
            }

            if (pin)
            {
                applications.Add(application);

                if (applications.Count > MaximumApplications)
                {
                    applications.RemoveAt(0);
                }
            }
            else
            {
                applications.RemoveAt(existingIndex);
            }

            snapshot = [.. applications];
        }

        PinsChanged?.Invoke();
        return snapshot;
    }
}
