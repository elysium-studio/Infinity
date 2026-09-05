using Elysium.Application.Abstractions;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Infinity.Shell;

public sealed class PageTitleStore(IOptionsMonitor<Settings> settings, IWritableOptions<Settings> writer, ITextLocalizer localizer)
{
    public event Action<int, string>? TitleChanged;

    public string GetTitle(int page) => settings.CurrentValue.PageTitles?.TryGetValue(page, out string? configuredTitle) == true ? configuredTitle : localizer.GetText("PageTitle", page + 1);

    public async Task<string> UpdateAsync(int page, string title)
    {
        string trimmed = title.Trim();
        if (trimmed.Length > 80)
        {
            trimmed = trimmed[..80];
        }

        Settings updated = await writer.ReadAsync() ?? new Settings();
        updated.PageTitles ??= [];
        if (string.IsNullOrEmpty(trimmed) || trimmed == localizer.GetText("PageTitle", page + 1))
        {
            updated.PageTitles.Remove(page);
        }
        else
        {
            updated.PageTitles[page] = trimmed;
        }

        await writer.WriteAsync(updated);
        string updatedTitle = string.IsNullOrEmpty(trimmed) || trimmed == localizer.GetText("PageTitle", page + 1) ? localizer.GetText("PageTitle", page + 1) : trimmed;
        TitleChanged?.Invoke(page, updatedTitle);
        return updatedTitle;
    }


    public async Task<IReadOnlyDictionary<int, string>> ReorderAsync(int sourcePage, int targetPage)
    {
        if (sourcePage == targetPage)
        {
            return new Dictionary<int, string>();
        }

        Settings updated = await writer.ReadAsync() ?? new Settings();
        Dictionary<int, string> configuredTitles = updated.PageTitles is null ? [] : new Dictionary<int, string>(updated.PageTitles);
        Dictionary<int, string> reorderedTitles = [];
        int firstPage = Math.Min(sourcePage, targetPage);
        int lastPage = Math.Max(sourcePage, targetPage);
        HashSet<string> generatedTitles = [];
        foreach (int page in configuredTitles.Keys)
        {
            generatedTitles.Add(localizer.GetText("PageTitle", page + 1));
        }

        for (int page = firstPage; page <= lastPage; page++)
        {
            generatedTitles.Add(localizer.GetText("PageTitle", page + 1));
        }

        foreach ((int page, string title)in configuredTitles)
        {
            if (!generatedTitles.Contains(title))
            {
                reorderedTitles[PageReorderMapping.Map(page, sourcePage, targetPage)] = title;
            }
        }

        updated.PageTitles = reorderedTitles;
        await writer.WriteAsync(updated);
        Dictionary<int, string> resolvedTitles = [];
        for (int page = firstPage; page <= lastPage; page++)
        {
            string title = reorderedTitles.TryGetValue(page, out string? configuredTitle) ? configuredTitle : localizer.GetText("PageTitle", page + 1);
            resolvedTitles[page] = title;
            TitleChanged?.Invoke(page, title);
        }

        return resolvedTitles;
    }
}
