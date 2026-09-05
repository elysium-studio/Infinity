namespace Infinity.Platform.Abstractions;

public interface IWindowConcealer
{
    bool Conceal(nint windowHandle);

    void Reveal(nint windowHandle);

    bool IsConcealed(nint windowHandle);

    IReadOnlySet<nint> ConcealedHandles();
}
