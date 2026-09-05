using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace Infinity.Shell.WinUI;

public sealed class DesktopOverviewBackdropAnimator
{
    private static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(300);

    public void AnimateIn(FrameworkElement element) => Animate(element, 1, EnterDuration, new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1));

    public void Reset(FrameworkElement element)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation(nameof(Visual.Opacity));
        visual.Opacity = 0;
    }


    private static void Animate(FrameworkElement element, float target, TimeSpan duration, Vector2 firstControlPoint, Vector2 secondControlPoint)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(firstControlPoint, secondControlPoint);
        animation.Duration = duration;
        animation.InsertExpressionKeyFrame(0, "this.StartingValue");
        animation.InsertKeyFrame(1, target, easing);
        visual.StartAnimation(nameof(Visual.Opacity), animation);
    }
}
