namespace Infinity.Platform.Abstractions;

public interface IWindowActivator
{
    void Activate(IntPtr handle);
}