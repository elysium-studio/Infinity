namespace Infinity.Platform.Abstractions;

public interface IWindowController
{
    void Close(IntPtr handle);

    void Minimize(IntPtr handle);

    void Restore(IntPtr handle);
}