using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Infinity.Shell.WinUI;

public sealed class DesktopApplicationDockPressAnimator
{
    private static readonly TimeSpan PressDuration = TimeSpan.FromMilliseconds(70);
    private static readonly TimeSpan ReleaseDuration = TimeSpan.FromMilliseconds(140);

    public void Press(FrameworkElement element) => Animate(element, 0.84f, PressDuration);

    public void Release(FrameworkElement element) => Animate(element, 1, ReleaseDuration);

    private static void Animate(FrameworkElement element, float targetScale, TimeSpan duration)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3(
            (float)(element.ActualWidth / 2),
            (float)(element.ActualHeight / 2),
            0);

        Compositor compositor = visual.Compositor;
        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(
            1,
            new Vector3(targetScale, targetScale, 1),
            compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.2f, 0),
                new Vector2(0, 1)));
        visual.StartAnimation(nameof(visual.Scale), animation);
    }
}
