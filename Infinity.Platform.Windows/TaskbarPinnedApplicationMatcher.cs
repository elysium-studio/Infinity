using Infinity.Platform.Abstractions;

namespace Infinity.Platform.Windows;

internal static class TaskbarPinnedApplicationMatcher
{
    public static IReadOnlyList<LaunchableApplication> Match(IEnumerable<string> shortcutNames, IReadOnlyList<LaunchableApplication> availableApplications)
    {
        Dictionary<string, LaunchableApplication> applicationsByName = availableApplications.GroupBy(application => Normalize(application.DisplayName), StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        List<LaunchableApplication> result = [];
        HashSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase);
        foreach (string shortcutName in shortcutNames)
        {
            string name = Normalize(Path.GetFileNameWithoutExtension(shortcutName));
            if (applicationsByName.TryGetValue(name, out LaunchableApplication? application) && identifiers.Add(application.Id))
            {
                result.Add(application);
            }
        }

        return result;
    }


    private static string Normalize(string value) => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
