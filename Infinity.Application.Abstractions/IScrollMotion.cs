namespace Infinity.Application.Abstractions;

public interface IScrollMotion
{
    bool IsActive { get; }

    double Drain();

    void Reset();
}
