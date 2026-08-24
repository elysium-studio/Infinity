using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Infinity.Shell.WinUI;

public sealed class DesktopOverviewBackdropAnimator
{
    private static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(180);

    public void AnimateIn(FrameworkElement element) => Animate(element, 0, 1, EnterDuration);

    public void AnimateOut(FrameworkElement element) => Animate(element, 1, 0, ExitDuration);

    public void Reset(FrameworkElement element)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation(nameof(Visual.Opacity));
        visual.Opacity = 0;
    }

    private static void Animate(FrameworkElement element, float from, float to, TimeSpan duration)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f),
            new Vector2(0.2f, 1));

        visual.StopAnimation(nameof(Visual.Opacity));
        visual.Opacity = from;
        animation.Duration = duration;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        visual.Opacity = to;
        visual.StartAnimation(nameof(Visual.Opacity), animation);
    }
}
