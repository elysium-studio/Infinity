using Elysium.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infinity.Shell;

public sealed class DesktopApplicationDockOrderStore : IDesktopApplicationDockOrderStore
{
    private const int MaximumApplications = 32;

    private readonly Lock syncRoot = new();
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly IWritableOptions<Settings> writer;
    private readonly ILogger<DesktopApplicationDockOrderStore> logger;
    private List<string> applicationIdentifiers;

    public DesktopApplicationDockOrderStore(
        IOptionsMonitor<Settings> settings,
        IWritableOptions<Settings> writer,
        ILogger<DesktopApplicationDockOrderStore> logger)
    {
        applicationIdentifiers = Normalize(settings.CurrentValue.DockApplicationOrder ?? []);
        this.writer = writer;
        this.logger = logger;
    }

    public IReadOnlyList<string> ApplicationIdentifiers
    {
        get
        {
            lock (syncRoot)
            {
                return [.. applicationIdentifiers];
            }
        }
    }

    public async Task SaveAsync(
        IEnumerable<string> identifiers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identifiers);
        List<string> snapshot = Normalize(identifiers);

        lock (syncRoot)
        {
            applicationIdentifiers = snapshot;
        }

        await writeGate.WaitAsync(cancellationToken);

        try
        {
            Settings updated = await writer.ReadAsync(cancellationToken) ?? new Settings();
            updated.DockApplicationOrder = [.. snapshot];
            await writer.WriteAsync(updated, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not save the application dock order");
        }
        finally
        {
            writeGate.Release();
        }
    }

    private static List<string> Normalize(IEnumerable<string> identifiers) => [.. identifiers
        .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(MaximumApplications)];
}
