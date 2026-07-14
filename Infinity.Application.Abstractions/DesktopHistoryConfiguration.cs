namespace Infinity.Application.Abstractions;

public class DesktopHistoryConfiguration
{
    private readonly Lock syncRoot = new();
    private DesktopHistoryConfigurationSnapshot current = DesktopHistoryDefaults.CreateConfiguration();

    public event Action<DesktopHistoryConfigurationSnapshot>? Changed;

    public DesktopHistoryConfigurationSnapshot Current
    {
        get
        {
            lock (syncRoot)
            {
                return current;
            }
        }
    }

    public void Update(bool enabled,
        int capacity,
        bool mouseButtonsEnabled,
        IEnumerable<IEnumerable<int>>? backShortcut,
        IEnumerable<IEnumerable<int>>? forwardShortcut)
    {
        DesktopHistoryConfigurationSnapshot updated = new(enabled,
            Math.Clamp(capacity, DesktopHistoryDefaults.MinimumCapacity, DesktopHistoryDefaults.MaximumCapacity),
            mouseButtonsEnabled,
            CopyShortcut(backShortcut, DesktopHistoryDefaults.CreateBackShortcut()),
            CopyShortcut(forwardShortcut, DesktopHistoryDefaults.CreateForwardShortcut()));

        lock (syncRoot)
        {
            current = updated;
        }

        Changed?.Invoke(updated);
    }

    private static IReadOnlyList<IReadOnlyList<int>> CopyShortcut(IEnumerable<IEnumerable<int>>? shortcut,
        List<List<int>> fallback)
    {
        List<IReadOnlyList<int>> copy = shortcut?
            .Select(group => (IReadOnlyList<int>)group.Distinct().ToArray())
            .Where(group => group.Count > 0)
            .ToList() ?? [];

        return copy.Count > 0
            ? copy
            : fallback.Select(group => (IReadOnlyList<int>)group.ToArray()).ToArray();
    }
}

public record DesktopHistoryConfigurationSnapshot(bool Enabled,
    int Capacity,
    bool MouseButtonsEnabled,
    IReadOnlyList<IReadOnlyList<int>> BackShortcut,
    IReadOnlyList<IReadOnlyList<int>> ForwardShortcut);

public static class DesktopHistoryDefaults
{
    public const int Capacity = 100;
    public const int MinimumCapacity = 10;
    public const int MaximumCapacity = 500;

    public static DesktopHistoryConfigurationSnapshot CreateConfiguration() => new(true,
        Capacity,
        true,
        CreateBackShortcut(),
        CreateForwardShortcut());

    public static List<List<int>> CreateBackShortcut() =>
        [[0x5B, 0x5C], [0xA2, 0xA3], [0xDB]];

    public static List<List<int>> CreateForwardShortcut() =>
        [[0x5B, 0x5C], [0xA2, 0xA3], [0xDD]];
}
