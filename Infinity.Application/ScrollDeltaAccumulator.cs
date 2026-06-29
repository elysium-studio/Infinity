using Infinity.Application.Abstractions;

namespace Infinity.Application;

public class ScrollDeltaAccumulator :
    IScrollDeltaAccumulator
{
    private double pendingPixels;
    private readonly Lock syncLock = new();

    public bool IsEmpty
    {
        get
        {
            lock (syncLock)
            {
                return pendingPixels == 0;
            }
        }
    }

    public void Add(double pixels)
    {
        lock (syncLock)
        {
            pendingPixels += pixels;
        }
    }

    public double DrainAndReset()
    {
        lock (syncLock)
        {
            double value = pendingPixels;
            pendingPixels = 0;
            return value;
        }
    }
}