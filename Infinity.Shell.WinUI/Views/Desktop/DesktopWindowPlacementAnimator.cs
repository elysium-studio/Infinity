using System;
using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace Infinity.Shell.WinUI;

internal sealed class DesktopWindowPlacementAnimator(
    params UIElement[] elements) : IDisposable
{
    internal static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(220);
    private readonly UISettings settings = new();
    private Vector3KeyFrameAnimation? translationAnimation;
    private Vector3KeyFrameAnimation? scaleAnimation;
    private CompositionScopedBatch? batch;
    private Bounds source;
    private Bounds target;
    private long started;
    private TimeSpan activeDuration;
    private Vector2 activeControlPoint1;
    private Vector2 activeControlPoint2;

    public TimeSpan AnimationDuration { get; set; } = Duration;

    public Vector2 EasingControlPoint1 { get; set; } = new(0.2f, 0);

    public Vector2 EasingControlPoint2 { get; set; } = new(0, 1);

    internal readonly record struct Bounds(
        double X,
        double Y,
        double Width,
        double Height)
    {
        public bool IsValid => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;
    }


    public Bounds Capture(Bounds fallback)
    {
        if (batch is null)
        {
            return fallback;
        }

        double time = Math.Clamp(Stopwatch.GetElapsedTime(started).TotalMilliseconds / activeDuration.TotalMilliseconds, 0, 1);
        double low = 0, high = 1;
        for (int iteration = 0; iteration < 16; iteration++)
        {
            double t = (low + high) / 2;
            double curveX = 3 * activeControlPoint1.X * (1 - t) * (1 - t) * t + 3 * activeControlPoint2.X * (1 - t) * t * t + t * t * t;
            if (curveX < time)
            {
                low = t;
            }
            else
            {
                high = t;
            }
        }

        double parameter = (low + high) / 2;
        double progress = 3 * activeControlPoint1.Y * (1 - parameter) * (1 - parameter) * parameter + 3 * activeControlPoint2.Y * (1 - parameter) * parameter * parameter + parameter * parameter * parameter;
        return new(source.X + (target.X - source.X) * progress, source.Y + (target.Y - source.Y) * progress, source.Width + (target.Width - source.Width) * progress, source.Height + (target.Height - source.Height) * progress);
    }


    public void Start(Bounds from, Bounds to, float depth, Action? completed = null)
    {
        Stop();
        if (!from.IsValid || !to.IsValid || from == to || AnimationDuration <= TimeSpan.Zero || !settings.AnimationsEnabled)
        {
            completed?.Invoke();
            return;
        }

        source = from;
        target = to;
        activeDuration = AnimationDuration;
        activeControlPoint1 = EasingControlPoint1;
        activeControlPoint2 = EasingControlPoint2;
        Compositor compositor = CompositionTarget.GetCompositorForCurrentThread();
        using CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(activeControlPoint1, activeControlPoint2);
        Vector3 scale = new((float)(from.Width / to.Width), (float)(from.Height / to.Height), 1);
        Vector3 start = new((float)(from.X - to.Width / 2 * (1 - scale.X)), (float)(from.Y - to.Height / 2 * (1 - scale.Y)), depth);
        translationAnimation = compositor.CreateVector3KeyFrameAnimation();
        translationAnimation.Target = nameof(UIElement.Translation);
        translationAnimation.Duration = activeDuration;
        translationAnimation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        translationAnimation.InsertKeyFrame(0, start);
        translationAnimation.InsertKeyFrame(1, new Vector3((float)to.X, (float)to.Y, depth), easing);
        scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.Target = nameof(UIElement.Scale);
        scaleAnimation.Duration = activeDuration;
        scaleAnimation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        scaleAnimation.InsertKeyFrame(0, scale);
        scaleAnimation.InsertKeyFrame(1, Vector3.One, easing);
        CompositionScopedBatch current = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        batch = current;
        current.Completed += (_, _) =>
        {
            if (ReferenceEquals(batch, current))
            {
                Stop();
                completed?.Invoke();
            }
        };
        started = Stopwatch.GetTimestamp();
        foreach (UIElement element in elements)
        {
            element.StartAnimation(translationAnimation);
            element.StartAnimation(scaleAnimation);
        }

        current.End();
    }


    public void Stop()
    {
        if (batch is null && translationAnimation is null && scaleAnimation is null)
        {
            return;
        }

        CompositionScopedBatch? previous = batch;
        batch = null;
        foreach (UIElement element in elements)
        {
            if (translationAnimation is not null)
            {
                element.StopAnimation(translationAnimation);
            }

            if (scaleAnimation is not null)
            {
                element.StopAnimation(scaleAnimation);
            }
        }

        previous?.Dispose();
        translationAnimation?.Dispose();
        scaleAnimation?.Dispose();
        translationAnimation = null;
        scaleAnimation = null;
    }


    public void Dispose() => Stop();
}
