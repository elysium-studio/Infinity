namespace Infinity.Application.Abstractions;

public interface IPanState
{
    event Action? OffsetChanged;

    double Offset { get; }

    double MinOffset { get; }

    double MaxOffset { get; }

    void SetMaxOffset(double value);

    void SetOffset(double value);

    void ApplyDelta(double delta);
}