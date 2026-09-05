using Infinity.Platform.Abstractions;

namespace Infinity.Platform.Windows;

internal static class UserAssistApplicationMatcher
{
    private const string AppsFolderPrefix = "shell:AppsFolder\\";

    public static IReadOnlyList<LaunchableApplication> Match(IReadOnlyList<LaunchableApplication> applications, IEnumerable<UserAssistApplicationUsageEntry> entries, int maximumCount)
    {
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(entries);
        if (maximumCount <= 0)
        {
            return[];
        }

        Dictionary<string, LaunchableApplication> applicationsByIdentifier = applications.Select(application => new KeyValuePair<string, LaunchableApplication>(NormalizeIdentifier(application.Id), application)).Where(pair => pair.Key.Length > 0).GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);
        return entries.Select(entry => new { Application = FindApplication(entry.Identifier, applicationsByIdentifier), Entry = entry }).Where(candidate => candidate.Application is not null).GroupBy(candidate => candidate.Application!.Id, StringComparer.OrdinalIgnoreCase).Select(group => group.OrderByDescending(candidate => candidate.Entry.UseCount).ThenByDescending(candidate => candidate.Entry.LastUsedUtc).First()).OrderByDescending(candidate => candidate.Entry.UseCount).ThenByDescending(candidate => candidate.Entry.LastUsedUtc).Take(maximumCount).Select(candidate => candidate.Application!).ToArray();
    }


    private static LaunchableApplication? FindApplication(string usageIdentifier, IReadOnlyDictionary<string, LaunchableApplication> applicationsByIdentifier)
    {
        foreach ((string identifier, LaunchableApplication application)in applicationsByIdentifier)
        {
            if (usageIdentifier.Contains(identifier, StringComparison.OrdinalIgnoreCase))
            {
                return application;
            }
        }

        return null;
    }


    private static string NormalizeIdentifier(string identifier) => identifier.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase) ? identifier[AppsFolderPrefix.Length..] : identifier;
}
