using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public class TrackedWindowFilter :
    ITrackedWindowFilter
{
    public string Filter { get; set; } = string.Empty;

    public bool IsActive => !string.IsNullOrWhiteSpace(Filter);

    public bool IsMatch(string title)
    {
        if (!IsActive)
        {
            return true;
        }

        string[] filterWords = Filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return filterWords.All(word => MatchesWord(title, word));
    }

    public bool IsMatch(ITrackedWindow window) => IsMatch(window.Title);

    private static bool MatchesWord(string title, string word)
    {
        if (title.Contains(word, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return MatchesAcronym(title, word);
    }

    private static bool MatchesAcronym(string title, string acronym)
    {
        string[] titleWords = title.Split([' ', '-', '_', '|', '·', '—'], StringSplitOptions.RemoveEmptyEntries);

        if (titleWords.Length < acronym.Length)
        {
            return false;
        }

        int acronymIndex = 0;

        foreach (string titleWord in titleWords)
        {
            if (acronymIndex >= acronym.Length)
            {
                break;
            }

            if (titleWord.StartsWith(acronym[acronymIndex].ToString(), StringComparison.OrdinalIgnoreCase))
            {
                acronymIndex++;
            }
        }

        return acronymIndex == acronym.Length;
    }
}