namespace Infinity.Platform.Abstractions;

public interface IWindowStack
{
    event EventHandler? WindowStackChanged;

    void Start();

    void Stop();

    void BringToFront(nint windowHandle);

    void Refresh();
}
