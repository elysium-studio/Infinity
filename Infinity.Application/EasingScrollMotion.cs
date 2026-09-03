using Infinity.Application.Abstractions;

namespace Infinity.Application;

public sealed class EasingScrollMotion :
    IDeltaScrollMotion
{
    private const double NaturalFrequency = 30.0;
    private const double InitialIntervalSeconds = 1.0 / 144.0;
    private const double MaxElapsedSeconds = 1.0 / 30.0;
    private const double StopDistanceThreshold = 0.02;
    private const double StopVelocityThreshold = 0.02;
    private const double ReversalDamping = 0.20;

    private readonly Lock syncLock = new();
    private readonly TimeProvider timeProvider;

    private double current;
    private double target;
    private double velocity;
    private long lastDrainTimestamp;
    private bool isTracking;

    public EasingScrollMotion() : this(TimeProvider.System)
    {
    }

    public EasingScrollMotion(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public bool IsActive
    {
        get
        {
            lock (syncLock)
            {
                return Math.Abs(target - current) >= StopDistanceThreshold ||
                    Math.Abs(velocity) >= StopVelocityThreshold;
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
            bool incomingOpposesVelocity = velocity != 0 && Math.Sign(pixels) != Math.Sign(velocity);

            if (incomingOpposesVelocity)
            {
                velocity *= ReversalDamping;
            }

            target += pixels;
        }
    }

    public double Drain()
    {
        lock (syncLock)
        {
            long now = timeProvider.GetTimestamp();

            if (!isTracking)
            {
                isTracking = true;
                lastDrainTimestamp = now - (long)(InitialIntervalSeconds * timeProvider.TimestampFrequency);
            }

            double elapsedSeconds = timeProvider.GetElapsedTime(lastDrainTimestamp, now).TotalSeconds;
            lastDrainTimestamp = now;

            if (elapsedSeconds <= 0)
            {
                elapsedSeconds = InitialIntervalSeconds;
            }

            if (elapsedSeconds > MaxElapsedSeconds)
            {
                elapsedSeconds = MaxElapsedSeconds;
            }

            double distance = target - current;

            if (Math.Abs(distance) < StopDistanceThreshold &&
                Math.Abs(velocity) < StopVelocityThreshold)
            {
                double finalStep = distance;

                current = 0;
                target = 0;
                velocity = 0;
                isTracking = false;

                return finalStep;
            }

            double previous = current;
            double displacement = current - target;
            double velocityTerm = velocity + NaturalFrequency * displacement;
            double decay = Math.Exp(-NaturalFrequency * elapsedSeconds);

            current = target + (displacement + velocityTerm * elapsedSeconds) * decay;
            velocity = (velocity - NaturalFrequency * velocityTerm * elapsedSeconds) * decay;

            distance = target - current;

            if (Math.Abs(distance) < StopDistanceThreshold &&
                Math.Abs(velocity) < StopVelocityThreshold)
            {
                double step = target - previous;

                current = 0;
                target = 0;
                velocity = 0;
                isTracking = false;

                return step;
            }

            return current - previous;
        }
    }

    public void Reset()
    {
        lock (syncLock)
        {
            current = 0;
            target = 0;
            velocity = 0;
            isTracking = false;
        }
    }
}
