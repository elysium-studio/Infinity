using Elysium.Application.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Options;

namespace Infinity.Shell;

public class WindowPlacementRules :
    IWindowPlacementRules,
    IDisposable
{
    private readonly IWindowApplicationIdentityProvider identityProvider;
    private readonly IWritableOptions<Settings> writer;
    private readonly IDisposable? optionsSubscription;
    private readonly Lock syncRoot = new();

    private Dictionary<string, int> rules;
    private bool disposed;

    public WindowPlacementRules(IWindowApplicationIdentityProvider identityProvider,
        IOptionsMonitor<Settings> options,
        IWritableOptions<Settings> writer)
    {
        this.identityProvider = identityProvider;
        this.writer = writer;
        rules = CopyRules(options.CurrentValue.ApplicationPageRules);
        optionsSubscription = options.OnChange((settings, _) => ReplaceRules(settings.ApplicationPageRules));
    }

    public bool TryGetTargetPage(IntPtr windowHandle, out int targetPage)
    {
        if (!identityProvider.TryGetApplicationId(windowHandle, out string applicationId))
        {
            targetPage = 0;
            return false;
        }

        lock (syncRoot)
        {
            return rules.TryGetValue(applicationId, out targetPage);
        }
    }

    public bool CanCreateRule(IntPtr windowHandle) =>
        identityProvider.TryGetApplicationId(windowHandle, out _);

    public async Task<bool> SetTargetPageAsync(IntPtr windowHandle, int targetPage)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (targetPage < 0 || !identityProvider.TryGetApplicationId(windowHandle, out string applicationId))
        {
            return false;
        }

        await writer.WriteAsync(settings =>
        {
            settings.ApplicationPageRules ??= [];
            settings.ApplicationPageRules[applicationId] = targetPage;
        });

        lock (syncRoot)
        {
            rules[applicationId] = targetPage;
        }

        return true;
    }

    public async Task<bool> RemoveAsync(IntPtr windowHandle)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (!identityProvider.TryGetApplicationId(windowHandle, out string applicationId))
        {
            return false;
        }

        bool exists;

        lock (syncRoot)
        {
            exists = rules.ContainsKey(applicationId);
        }

        if (!exists)
        {
            return false;
        }

        await writer.WriteAsync(settings => settings.ApplicationPageRules?.Remove(applicationId));

        lock (syncRoot)
        {
            rules.Remove(applicationId);
        }

        return true;
    }

    public void Dispose()
    {
        lock (syncRoot)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
        }

        optionsSubscription?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ReplaceRules(Dictionary<string, int>? updatedRules)
    {
        lock (syncRoot)
        {
            if (!disposed)
            {
                rules = CopyRules(updatedRules);
            }
        }
    }

    private static Dictionary<string, int> CopyRules(Dictionary<string, int>? source) =>
        source is null
            ? new(StringComparer.OrdinalIgnoreCase)
            : new(source, StringComparer.OrdinalIgnoreCase);
}
