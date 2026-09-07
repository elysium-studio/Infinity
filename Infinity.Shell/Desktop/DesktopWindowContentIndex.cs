using System.Collections.Concurrent;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopWindowContentIndex
{
    private readonly ConcurrentDictionary<nint, Entry> entries = new();
    private readonly Dictionary<nint, DesktopWindowSearchSnapshot> snapshots = [];
    private readonly Queue<nint> snapshotOrder = new();
    private readonly Lock snapshotGate = new();
    private const long SnapshotBudget = 64 * 1024 * 1024;
    private long snapshotBytes;
    private volatile bool isSearchActive;

    public void SetSearchQuery(string query) => isSearchActive = !string.IsNullOrWhiteSpace(query);

    public bool CanRefresh(nint handle) => !isSearchActive || !entries.ContainsKey(handle);

    public bool TryUpdateSnapshot(nint handle, string? fingerprint, WindowContentSnapshot? image, WindowTextRecognition? recognition)
    {
        if (fingerprint is null || image is null || recognition is null || !CanRefresh(handle))
        {
            return false;
        }

        Update(handle, fingerprint, image, recognition);
        return true;
    }

    public void Clear()
    {
        isSearchActive = false;
        entries.Clear();
        lock (snapshotGate)
        {
            snapshots.Clear();
            snapshotOrder.Clear();
            snapshotBytes = 0;
        }
    }

    public void Remove(nint handle)
    {
        entries.TryRemove(handle, out _);
        lock (snapshotGate)
        {
            if (snapshots.Remove(handle, out DesktopWindowSearchSnapshot? snapshot))
            {
                snapshotBytes -= snapshot.Image.Pixels.Length;
            }
        }
    }

    public DesktopWindowSearchSnapshot? GetSnapshot(nint handle)
    {
        lock (snapshotGate)
        {
            return snapshots.GetValueOrDefault(handle);
        }
    }

    public WindowTextRecognition? GetRecognition(nint handle) => entries.TryGetValue(handle, out Entry? entry) ? entry.Recognition : null;

    public void Update(nint handle, string fingerprint, WindowContentSnapshot image, WindowTextRecognition recognition)
    {
        entries[handle] = new(fingerprint, image.Width, image.Height, recognition.Text.Length <= 65536 ? recognition.Text : recognition.Text[..65536], recognition);
        lock (snapshotGate)
        {
            if (snapshots.Remove(handle, out DesktopWindowSearchSnapshot? previous))
            {
                snapshotBytes -= previous.Image.Pixels.Length;
            }

            nint[] retained = [..snapshotOrder.Where(value => value != handle && snapshots.ContainsKey(value))];
            snapshotOrder.Clear();
            foreach (nint value in retained)
            {
                snapshotOrder.Enqueue(value);
            }

            if (image.Pixels.LongLength > SnapshotBudget)
            {
                return;
            }

            while (snapshotBytes + image.Pixels.LongLength > SnapshotBudget && snapshotOrder.TryDequeue(out nint oldest))
            {
                if (snapshots.Remove(oldest, out DesktopWindowSearchSnapshot? evicted))
                {
                    snapshotBytes -= evicted.Image.Pixels.Length;
                }
            }

            snapshots[handle] = new(image, recognition);
            snapshotOrder.Enqueue(handle);
            snapshotBytes += image.Pixels.Length;
        }
    }

    public bool IsUnchanged(nint handle, string fingerprint, int width, int height) => entries.TryGetValue(handle, out Entry? entry) && entry.Fingerprint == fingerprint && entry.Width == width && entry.Height == height;

    public void Update(nint handle, string fingerprint, int width, int height, string text) => entries[handle] = new(fingerprint, width, height, text.Length <= 65536 ? text : text[..65536]);

    public bool Matches(nint handle, string title, string query)
    {
        if (WindowTitleFilter.Matches(title, query))
        {
            return true;
        }

        return entries.TryGetValue(handle, out Entry? entry) && query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).All(word => WindowTitleFilter.Matches(title, word) || entry.Text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    public bool ShouldDisplay(nint handle, string title, string query, bool contentSearchEnabled) => Matches(handle, title, query) || contentSearchEnabled && !entries.ContainsKey(handle);

    public string? GetSnippet(nint handle, string query)
    {
        if (string.IsNullOrWhiteSpace(query) || !entries.TryGetValue(handle, out Entry? entry))
        {
            return null;
        }

        int match = -1;
        foreach (string word in query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            int index = entry.Text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && (match < 0 || index < match))
            {
                match = index;
            }
        }

        if (match < 0)
        {
            return null;
        }

        int start = Math.Max(0, match - 50);
        int length = Math.Min(180, entry.Text.Length - start);
        return (start > 0 ? "…" : string.Empty) + entry.Text.Substring(start, length).Replace('\r', ' ').Replace('\n', ' ') + (start + length < entry.Text.Length ? "…" : string.Empty);
    }

    private sealed record Entry(
        string Fingerprint,
        int Width,
        int Height,
        string Text,
        WindowTextRecognition? Recognition = null);
}
