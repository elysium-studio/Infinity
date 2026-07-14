using Elysium.Platform.Abstractions;

namespace Infinity.Shell;

public static class DesktopHistoryShortcutValidator
{
    private static readonly HashSet<int> ModifierKeys =
    [
        0x10, 0x11, 0x12, 0x5B, 0x5C, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5
    ];

    private static readonly HashSet<int> PageTriggerKeys =
    [
        0x25, 0x27, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39
    ];

    public static bool IsValid(HotKeysBuilderSnapshot snapshot) =>
        snapshot.Keys.Count == 3 &&
        snapshot.Keys[0].IsModifier &&
        snapshot.Keys[1].IsModifier &&
        !snapshot.Keys[2].IsModifier &&
        IsValid(snapshot.Combinations);

    public static bool IsValid(IEnumerable<IEnumerable<int>> shortcut)
    {
        int[][] groups = Copy(shortcut);

        return groups.Length == 3 &&
            groups[0].All(ModifierKeys.Contains) &&
            groups[1].All(ModifierKeys.Contains) &&
            groups[2].All(key => !ModifierKeys.Contains(key));
    }

    public static bool AreEquivalent(IEnumerable<IEnumerable<int>> left, IEnumerable<IEnumerable<int>> right)
    {
        int[][] leftGroups = Copy(left);
        int[][] rightGroups = Copy(right);

        if (leftGroups.Length != rightGroups.Length)
        {
            return false;
        }

        bool[] matched = new bool[rightGroups.Length];

        foreach (int[] leftGroup in leftGroups)
        {
            int match = -1;

            for (int index = 0; index < rightGroups.Length; index++)
            {
                if (!matched[index] && GroupsOverlap(leftGroup, rightGroups[index]))
                {
                    match = index;
                    break;
                }
            }

            if (match < 0)
            {
                return false;
            }

            matched[match] = true;
        }

        return true;
    }

    public static bool ConflictsWithPageNavigation(IEnumerable<IEnumerable<int>> shortcut,
        IEnumerable<IEnumerable<int>> scrollModifierKeys)
    {
        int[][] shortcutGroups = Copy(shortcut);
        int[][] modifierGroups = Copy(scrollModifierKeys);

        if (shortcutGroups.Length != 3 || modifierGroups.Length != 2 ||
            !shortcutGroups[^1].Any(PageTriggerKeys.Contains))
        {
            return false;
        }

        return AreEquivalent(shortcutGroups.Take(2), modifierGroups);
    }

    private static int[][] Copy(IEnumerable<IEnumerable<int>> groups) =>
        groups.Select(group => group.Distinct().Order().ToArray()).Where(group => group.Length > 0).ToArray();

    private static bool GroupsOverlap(IReadOnlyCollection<int> left, IReadOnlyCollection<int> right) =>
        left.Any(right.Contains);
}
