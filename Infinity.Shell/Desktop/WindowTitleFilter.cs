namespace Infinity.Shell;

public static class WindowTitleFilter
{
    public static bool Matches(string title, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        string[] filterWords = filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return filterWords.All(word => MatchesWord(title, word));
    }


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
