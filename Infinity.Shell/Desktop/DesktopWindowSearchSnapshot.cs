using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed record DesktopWindowSearchSnapshot(
    WindowContentSnapshot Image,
    WindowTextRecognition Recognition)
{
    public WindowTextWord[] GetMatches(string query)
    {
        string[] terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return terms.Length == 0 ? [] : [..Recognition.Words.Where(word => terms.Any(term => word.Text.Contains(term, StringComparison.OrdinalIgnoreCase)))];
    }
}
