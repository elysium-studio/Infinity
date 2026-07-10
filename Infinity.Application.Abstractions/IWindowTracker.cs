namespace Infinity.Application.Abstractions;

public interface IWindowTracker
{
    void Start();

    void Stop();

    void TryRegister(IntPtr windowHandle);
}
