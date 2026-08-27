namespace Infinity.Platform.Windows;

internal interface IWindowArrangingSystem
{
    bool TryRead(out bool enabled, out int error);

    bool TryWrite(bool enabled, out int error);
}
