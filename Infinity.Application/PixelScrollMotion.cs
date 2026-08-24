using Infinity.Application.Abstractions;

namespace Infinity.Application;

public sealed class PixelScrollMotion :
    IDeltaScrollMotion
{
    private const double StopThreshold = 0.02;

    private readonly Lock syncLock = new();

    private double pending;

    public bool IsActive
    {
        get
        {
            lock (syncLock)
            {
                return Math.Abs(pending) >= StopThreshold;
            }
        }
    }

    public void AddDelta(double pixels)
    {
        if (pixels == 0)
        {
            return;
        }

        lock (syncLock)
        {
            pending += pixels;
        }
    }

    public double Drain()
    {
        lock (syncLock)
        {
            double step = pending;
            pending = 0;

            return step;
        }
    }

    public void Reset()
    {
        lock (syncLock)
        {
            pending = 0;
        }
    }
}