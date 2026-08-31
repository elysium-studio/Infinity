using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Infinity.Shell.WinUI;

public sealed class DesktopScrollPreviewAnimator
{
    private const float OverviewScale = 0.38f;

    private static readonly TimeSpan EnterAnimationDuration = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan ExitAnimationDuration = TimeSpan.FromMilliseconds(250);

    private int animationGeneration;

    public double Scale => OverviewScale;

    public TimeSpan EnterDuration => EnterAnimationDuration;

    public TimeSpan ExitDuration => ExitAnimationDuration;

    public void AnimateInward(FrameworkElement element, double width, double height, Action? completed = null)
    {
        Visual visual = GetVisual(element, width, height);
        StartScaleAnimation(visual, new Vector3(OverviewScale, OverviewScale, 1), EnterAnimationDuration, CreateEntranceEasing(visual.Compositor), completed);
    }

    public void AnimateOutward(FrameworkElement element, double width, double height, Action completed)
    {
        Visual visual = GetVisual(element, width, height);
        StartScaleAnimation(visual, Vector3.One, ExitAnimationDuration, CreateExistingElementEasing(visual.Compositor), completed);
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

    private void StartScaleAnimation(Visual visual, Vector3 target, TimeSpan duration, CubicBezierEasingFunction easing, Action? completed)
    {
        int generation = ++animationGeneration;
        Compositor compositor = visual.Compositor;
        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertExpressionKeyFrame(0, "this.StartingValue");
        animation.InsertKeyFrame(1, target, easing);

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

    private static CubicBezierEasingFunction CreateEntranceEasing(Compositor compositor) => compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1));

    private static CubicBezierEasingFunction CreateExistingElementEasing(Compositor compositor) => compositor.CreateCubicBezierEasingFunction(new Vector2(0.55f, 0.55f), new Vector2(0, 1));

    private static float ToFloat(double value) => (float)Math.Clamp(value, -float.MaxValue, float.MaxValue);
}
