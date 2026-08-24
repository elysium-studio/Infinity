using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Options;

namespace Infinity.Shell;

public sealed class PageTitleStore(IOptionsMonitor<Settings> settings,
    IWritableOptions<Settings> writer,
    ITextLocalizer localizer)
{
    public string GetTitle(int page) =>
        settings.CurrentValue.PageTitles?.TryGetValue(page, out string? configuredTitle) == true
            ? configuredTitle
            : localizer.GetText("PageTitle", page + 1);

    public async Task<string> UpdateAsync(int page, string title)
    {
        string trimmed = title.Trim();

        if (trimmed.Length > 80)
        {
            trimmed = trimmed[..80];
        }

        Settings updated = await writer.ReadAsync() ?? new Settings();
        updated.PageTitles ??= [];

        if (string.IsNullOrEmpty(trimmed))
        {
            updated.PageTitles.Remove(page);
        }
        else
        {
            updated.PageTitles[page] = trimmed;
        }

        await writer.WriteAsync(updated);
        return string.IsNullOrEmpty(trimmed) ? localizer.GetText("PageTitle", page + 1) : trimmed;
    }
}
