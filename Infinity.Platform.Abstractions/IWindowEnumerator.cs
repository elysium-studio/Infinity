namespace Infinity.Platform.Abstractions;

public interface IWindowEnumerator
{
    void EnumerateVisible(Action<IntPtr> onWindowFound);
}