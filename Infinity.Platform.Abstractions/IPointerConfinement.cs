namespace Infinity.Platform.Abstractions;

public interface IPointerConfinement
{
    bool Confine(nint windowHandle, double rasterizationScale, double left, double top, double right, double bottom);

    void Release();
}
