namespace Infinity.Platform.Abstractions;

public interface IWindowTracker
{
    event Action<IntPtr>? WindowRestored;

    void TryRegister(IntPtr windowHandle);

    void Start();

    void Stop();
}