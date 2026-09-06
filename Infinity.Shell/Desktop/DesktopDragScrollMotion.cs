using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public sealed class DesktopDragScrollMotion
{
    private double pointerX;
    private double targetVelocity;
    private double edgeVelocity;
    private double velocity;
    private TimeSpan lastPointerTime;
    private TimeSpan lastTickTime;
    private bool hasPointer;

    public bool IsMoving => Math.Abs(velocity) >= 1 || Math.Abs(targetVelocity) >= 1 || Math.Abs(edgeVelocity) >= 1;

    public void Update(double x, double viewportWidth, TimeSpan now)
    {
        bool wasMoving = IsMoving;
        if (!double.IsFinite(x) || !double.IsFinite(viewportWidth) || viewportWidth <= 0)
        {
            Reset();
            return;
        }

        double threshold = Math.Min(64, viewportWidth / 4);
        int direction = x <= threshold ? -1 : x >= viewportWidth - threshold ? 1 : 0;
        double distanceFromEdge = direction < 0 ? x : viewportWidth - x;
        double depth = direction == 0 ? 0 : 1 - Math.Clamp(distanceFromEdge / threshold, 0, 1);
        edgeVelocity = direction * 1000 * depth;
        if (direction == 0)
        {
            targetVelocity = 0;
            velocity = 0;
        }

        if (hasPointer && now > lastPointerTime)
        {
            double delta = x - pointerX;
            double elapsed = (now - lastPointerTime).TotalSeconds;
            double measured = Math.Clamp(delta / Math.Max(0.001, elapsed), -4000, 4000);
            if (direction == 0 || measured * direction < 0)
            {
                targetVelocity = 0;
                velocity = direction * Math.Min(Math.Abs(velocity), Math.Abs(edgeVelocity));
            }
            else
            {
                if (velocity * direction < 0)
                {
                    velocity = 0;
                }

                targetVelocity = measured;
            }
        }
        if (!hasPointer || !wasMoving)
        {
            lastTickTime = now;
        }

        pointerX = x;
        lastPointerTime = now;
        hasPointer = true;
    }

    public double Advance(TimeSpan now, DragScrollSpeed speed, double overviewScale)
    {
        if (!hasPointer || !double.IsFinite(overviewScale) || overviewScale <= 0 || now <= lastTickTime)
        {
            return 0;
        }

        double elapsed = Math.Min(0.032, (now - lastTickTime).TotalSeconds);
        lastTickTime = now;
        double idle = Math.Max(0, (now - lastPointerTime).TotalSeconds - 0.04);
        if (idle >= 0.5)
        {
            targetVelocity = 0;
        }

        double multiplier = speed switch
        {
            DragScrollSpeed.Slow => 0.5,
            DragScrollSpeed.Normal => 1,
            DragScrollSpeed.Fast => 2,
            DragScrollSpeed.Turbo => 3.5,
            _ => 1
        };
        double maximumVelocity = 4000 / multiplier;
        double target = Math.Sign(edgeVelocity) * Math.Min(maximumVelocity, Math.Max(Math.Abs(edgeVelocity), Math.Abs(targetVelocity) * Math.Exp(-idle / 0.08)));
        double initialVelocity = Math.Clamp(velocity, -maximumVelocity, maximumVelocity);
        double response = 1 - Math.Exp(-elapsed / 0.06);
        double distance = target * elapsed + (initialVelocity - target) * 0.06 * response;
        velocity = initialVelocity + (target - initialVelocity) * response;
        if (Math.Abs(velocity) < 1 && Math.Abs(target) < 1)
        {
            velocity = 0;
            targetVelocity = 0;
        }

        return distance * multiplier / overviewScale;
    }

    public void Reset()
    {
        hasPointer = false;
        targetVelocity = 0;
        edgeVelocity = 0;
        velocity = 0;
        lastPointerTime = TimeSpan.Zero;
        lastTickTime = TimeSpan.Zero;
    }
}
