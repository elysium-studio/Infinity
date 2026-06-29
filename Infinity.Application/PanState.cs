using Infinity.Application.Abstractions;

namespace Infinity.Application;

public class PanState :
    IPanState
{
    public event Action? OffsetChanged;

    public double Offset { get; private set; }

    public double MinOffset => 0;

    public double MaxOffset { get; private set; } = double.MaxValue;

    public void SetMaxOffset(double value) => MaxOffset = value;

    public void SetOffset(double value)
    {
        Offset = value;
        OffsetChanged?.Invoke();
    }

    public void ApplyDelta(double delta)
    {
        Offset = Math.Clamp(Offset + delta, MinOffset, MaxOffset);
        OffsetChanged?.Invoke();
    }
}