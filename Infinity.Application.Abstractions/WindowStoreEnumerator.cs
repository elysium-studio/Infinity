using System.Collections;

namespace Infinity.Application.Abstractions;

public struct WindowStoreEnumerator(TrackedWindow[] items) : IEnumerator<TrackedWindow>
{
    private readonly TrackedWindow[] items = items;
    private int index = -1;

    public TrackedWindow Current => items[index];

    object IEnumerator.Current => Current;

    public bool MoveNext()
    {
        index++;
        return index < items.Length;
    }


    public void Reset() => index = -1;

    public void Dispose()
    {
    }
}
