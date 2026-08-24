using Elysium.Application.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Options;

namespace Infinity.Shell;

public sealed class WindowPlacementRules(IWindowApplicationIdentityProvider identityProvider,
    IOptionsMonitor<Settings> options,
    IWritableOptions<Settings> writer) :
    IWindowPlacementRules
{
    private readonly IWindowApplicationIdentityProvider identityProvider = identityProvider;
    private readonly IWritableOptions<Settings> writer = writer;
    private readonly Lock syncRoot = new();

    private readonly Dictionary<string, int> rules = CopyRules(options.CurrentValue.ApplicationPageRules);

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
        if (!identityProvider.TryGetApplicationId(windowHandle, out string applicationId))
        {
            return false;
        }

        lock (syncRoot)
        {
            if (!rules.ContainsKey(applicationId))
            {
                return false;
            }
        }

        await writer.WriteAsync(settings => settings.ApplicationPageRules?.Remove(applicationId));

        lock (syncRoot)
        {
            rules.Remove(applicationId);
        }

        return true;
    }

    private static Dictionary<string, int> CopyRules(Dictionary<string, int>? source) =>
        source is null
            ? new(StringComparer.OrdinalIgnoreCase)
            : new(source, StringComparer.OrdinalIgnoreCase);
}