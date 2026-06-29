namespace Infinity.Platform.Abstractions;

public interface IWindowTitleReader
{
    string GetTitle(IntPtr windowHandle);
}