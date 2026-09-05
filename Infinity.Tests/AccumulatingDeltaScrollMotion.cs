using Infinity.Application.Abstractions;

namespace Infinity.Tests;

internal sealed class AccumulatingDeltaScrollMotion : IDeltaScrollMotion
{
    private double pending;

    public int ResetCount { get; private set; }

    public bool IsActive => pending != 0;

    public void AddDelta(double pixels) => pending += pixels;

    public double Drain()
    {
        double delta = pending;
        pending = 0;
        return delta;
    }


    public void Reset()
    {
        ResetCount++;
        pending = 0;
    }
}
