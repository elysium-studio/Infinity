namespace Infinity.Platform.Abstractions;

public interface IWindowFilter
{
    bool ShouldTrack(IntPtr windowHandle, IntPtr ownerHandle);
}