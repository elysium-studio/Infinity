using Infinity.Application.Abstractions;

namespace Infinity.Application;


public class MomentumScrollMotion :
    IVelocityScrollMotion
{
    private const double FrictionFactor = 0.88;
    private const double StopThreshold = 0.15;

    private readonly Lock syncLock = new();

    private double velocity;
    private double subPixelRemainder;

    public bool IsActive
    {
        get
        {
            lock (syncLock)
            {
                return Math.Abs(velocity) >= StopThreshold;
            }
        }
    }

    public void AddVelocity(double pixelsPerTick)
    {
        lock (syncLock)
        {
            velocity += pixelsPerTick;
        }
    }

    public double Drain()
    {
        lock (syncLock)
        {
            if (Math.Abs(velocity) < StopThreshold)
            {
                velocity = 0;
                subPixelRemainder = 0;
                return 0;
            }

            velocity *= FrictionFactor;

            double exactStep = velocity + subPixelRemainder;
            int intStep = (int)Math.Truncate(exactStep);
            subPixelRemainder = exactStep - intStep;

            return intStep;
        }
    }

    public void Reset()
    {
        lock (syncLock)
        {
            velocity = 0;
            subPixelRemainder = 0;
        }
    }
}