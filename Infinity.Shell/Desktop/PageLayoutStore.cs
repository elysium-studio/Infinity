using Elysium.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Infinity.Shell;

public sealed class PageLayoutStore(IOptionsMonitor<Settings> settings, IWritableOptions<Settings> writer)
{
    public event Action<int, DesktopSnapLayoutKind>? LayoutChanged;

    public DesktopSnapLayoutKind GetLayout(int page) => settings.CurrentValue.PageLayouts?.GetValueOrDefault(page) ?? DesktopSnapLayoutKind.None;

    public async Task<DesktopSnapLayoutKind> UpdateAsync(int page, DesktopSnapLayoutKind layout)
    {
        Settings updated = await writer.ReadAsync() ?? new Settings();
        updated.PageLayouts ??= [];
        if (layout == DesktopSnapLayoutKind.None)
        {
            updated.PageLayouts.Remove(page);
        }
        else
        {
            updated.PageLayouts[page] = layout;
        }

        await writer.WriteAsync(updated);
        LayoutChanged?.Invoke(page, layout);
        return layout;
    }


    public async Task ReorderAsync(int sourcePage, int targetPage)
    {
        if (sourcePage == targetPage)
        {
            return;
        }

        Settings updated = await writer.ReadAsync() ?? new Settings();
        Dictionary<int, DesktopSnapLayoutKind> reorderedLayouts = [];
        foreach ((int page, DesktopSnapLayoutKind layout)in updated.PageLayouts ?? [])
        {
            reorderedLayouts[PageReorderMapping.Map(page, sourcePage, targetPage)] = layout;
        }

        updated.PageLayouts = reorderedLayouts;
        await writer.WriteAsync(updated);
        int firstPage = Math.Min(sourcePage, targetPage);
        int lastPage = Math.Max(sourcePage, targetPage);
        for (int page = firstPage; page <= lastPage; page++)
        {
            LayoutChanged?.Invoke(page, reorderedLayouts.GetValueOrDefault(page));
        }
    }
}
