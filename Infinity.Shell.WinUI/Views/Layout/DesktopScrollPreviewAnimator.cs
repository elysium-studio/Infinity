using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Infinity.Shell.WinUI;

public sealed class DesktopScrollPreviewAnimator
{
    private const float OverviewScale = 0.94f;

    private static readonly TimeSpan EnterAnimationDuration = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan ExitAnimationDuration = TimeSpan.FromMilliseconds(220);

    private int animationGeneration;

    public void AnimateInward(FrameworkElement element, double width, double height)
    {
        Visual visual = GetVisual(element, width, height);
        visual.StopAnimation(nameof(Visual.Scale));
        visual.Scale = Vector3.One;
        StartScaleAnimation(visual,
            Vector3.One,
            new Vector3(OverviewScale, OverviewScale, 1),
            EnterAnimationDuration,
            null);
    }

    public void AnimateOutward(FrameworkElement element, double width, double height, Action completed)
    {
        Visual visual = GetVisual(element, width, height);
        visual.StopAnimation(nameof(Visual.Scale));
        visual.Scale = new Vector3(OverviewScale, OverviewScale, 1);
        StartScaleAnimation(visual,
            visual.Scale,
            Vector3.One,
            ExitAnimationDuration,
            completed);
    }

    public void Reset(FrameworkElement element, double width, double height)
    {
        animationGeneration++;
        Visual visual = GetVisual(element, width, height);
        visual.StopAnimation(nameof(Visual.Scale));
        visual.Scale = Vector3.One;
    }

    private static Visual GetVisual(FrameworkElement element, double width, double height)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3(ToFloat(width / 2), ToFloat(height / 2), 0);
        return visual;
    }

    private void StartScaleAnimation(Visual visual,
        Vector3 from,
        Vector3 to,
        TimeSpan duration,
        Action? completed)
    {
        int generation = ++animationGeneration;
        Compositor compositor = visual.Compositor;
        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f),
            new Vector2(0.2f, 1));
        animation.Duration = duration;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        visual.Scale = to;

        if (completed is null)
        {
            visual.StartAnimation(nameof(Visual.Scale), animation);
            return;
        }

        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        batch.Completed += (sender, args) =>
        {
            batch.Dispose();

            if (generation == animationGeneration)
            {
                completed();
            }
        };
        visual.StartAnimation(nameof(Visual.Scale), animation);
        batch.End();
    }

    private static float ToFloat(double value) =>
        (float)Math.Clamp(value, -float.MaxValue, float.MaxValue);
}
