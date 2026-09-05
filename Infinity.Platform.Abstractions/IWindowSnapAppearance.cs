namespace Infinity.Platform.Abstractions;

public interface IWindowSnapAppearance
{
    bool TryApply(nint windowHandle);

    void Restore(nint windowHandle);
}
