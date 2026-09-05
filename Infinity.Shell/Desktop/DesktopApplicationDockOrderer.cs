namespace Infinity.Shell;

public static class DesktopApplicationDockOrderer
{
    public static IReadOnlyList<DesktopApplicationDockEntry> Apply(IReadOnlyList<DesktopApplicationDockEntry> applications, IReadOnlyList<string> orderedIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(orderedIdentifiers);
        Dictionary<string, int> ranks = orderedIdentifiers.Select((identifier, index) => (identifier, index)).DistinctBy(item => item.identifier, StringComparer.OrdinalIgnoreCase).ToDictionary(item => item.identifier, item => item.index, StringComparer.OrdinalIgnoreCase);
        return[..applications.Select((application, index) => new { Application = application, SourceIndex = index, Rank = ranks.GetValueOrDefault(application.Application.Id, int.MaxValue) }).OrderBy(item => item.Rank).ThenBy(item => item.SourceIndex).Select(item => item.Application)];
    }
}
