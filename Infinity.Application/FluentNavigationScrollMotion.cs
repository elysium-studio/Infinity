using Infinity.Application.Abstractions;

namespace Infinity.Application;

public sealed class FluentNavigationScrollMotion(TimeProvider timeProvider) : IDeltaScrollMotion
{
    private const double FirstControlPointX = 0.55;
    private const double FirstControlPointY = 0.55;
    private const double SecondControlPointX = 0;
    private const double SecondControlPointY = 1;
    private const int SolverIterations = 8;
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(250);
    private readonly Lock syncLock = new();
    private long startedAt;
    private double distance;
    private double emittedDistance;
    private bool isActive;

    public bool IsActive
    {
        get
        {
            lock (syncLock)
            {
                return isActive;
            }
        }
    }


    public void AddDelta(double pixels)
    {
        if (!double.IsFinite(pixels) || pixels == 0)
        {
            return;
        }

        lock (syncLock)
        {
            if (!isActive)
            {
                startedAt = timeProvider.GetTimestamp();
                distance = 0;
                emittedDistance = 0;
                isActive = true;
            }

            distance += pixels;
        }
    }


    public double Drain()
    {
        lock (syncLock)
        {
            if (!isActive)
            {
                return 0;
            }

            double progress = Math.Clamp(timeProvider.GetElapsedTime(startedAt).TotalMilliseconds / Duration.TotalMilliseconds, 0, 1);
            double easedProgress = EvaluateCubicBezier(progress);
            double nextDistance = distance * easedProgress;
            double delta = nextDistance - emittedDistance;
            emittedDistance = nextDistance;
            if (progress >= 1)
            {
                delta += distance - emittedDistance;
                ResetCore();
            }

            return delta;
        }
    }


    public void Reset()
    {
        lock (syncLock)
        {
            ResetCore();
        }
    }


    private void ResetCore()
    {
        startedAt = 0;
        distance = 0;
        emittedDistance = 0;
        isActive = false;
    }


    private static double EvaluateCubicBezier(double progress)
    {
        if (progress is <= 0 or >= 1)
        {
            return progress;
        }

        double parameter = progress;
        for (int iteration = 0; iteration < SolverIterations; iteration++)
        {
            double error = EvaluateCurve(parameter, FirstControlPointX, SecondControlPointX) - progress;
            double slope = EvaluateCurveDerivative(parameter, FirstControlPointX, SecondControlPointX);
            if (Math.Abs(slope) < double.Epsilon)
            {
                break;
            }

            parameter = Math.Clamp(parameter - (error / slope), 0, 1);
        }

        return EvaluateCurve(parameter, FirstControlPointY, SecondControlPointY);
    }


    private static double EvaluateCurve(double parameter, double firstControlPoint, double secondControlPoint)
    {
        double inverse = 1 - parameter;
        return (3 * inverse * inverse * parameter * firstControlPoint) + (3 * inverse * parameter * parameter * secondControlPoint) + (parameter * parameter * parameter);
    }


    private static double EvaluateCurveDerivative(double parameter, double firstControlPoint, double secondControlPoint)
    {
        double inverse = 1 - parameter;
        return (3 * inverse * inverse * firstControlPoint) + (6 * inverse * parameter * (secondControlPoint - firstControlPoint)) + (3 * parameter * parameter * (1 - secondControlPoint));
    }
}
