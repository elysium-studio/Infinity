namespace Infinity.Shell;

public sealed class DesktopOverviewKeySuppression
{
    private readonly Lock gate = new();
    private readonly HashSet<int> consumed = [];

    public static bool IsModifier(int key) => key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5;

    public void Track(int key)
    {
        if (IsModifier(key))
        {
            return;
        }

        lock (gate)
        {
            consumed.Add(key);
        }
    }

    public bool Release(int key)
    {
        lock (gate)
        {
            return consumed.Remove(key);
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            consumed.Clear();
        }
    }
}
