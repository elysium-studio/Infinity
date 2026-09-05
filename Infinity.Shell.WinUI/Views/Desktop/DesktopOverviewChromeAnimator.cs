using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace Infinity.Shell.WinUI;

public sealed class DesktopOverviewChromeAnimator
{
    private static readonly TimeSpan PaneDuration = TimeSpan.FromMilliseconds(333);
    private static readonly TimeSpan ItemDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan TopChromeExitDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PaneExitDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ItemExitDuration = TimeSpan.FromMilliseconds(167);
    private static readonly TimeSpan IconInitialDelay = TimeSpan.FromMilliseconds(72);
    private static readonly TimeSpan IconStagger = TimeSpan.FromMilliseconds(20);
    private const float DockEntranceDistance = 96;
    private readonly Dictionary<FrameworkElement, Vector3> exitRestingOffsets = [];
    private int exitAnimationGeneration;
    private int topAnimationGeneration;
    private int bottomAnimationGeneration;

    public void AnimateDock(FrameworkElement surface, IReadOnlyList<FrameworkElement> icons)
    {
        exitAnimationGeneration++;
        AnimateEntrance(surface, new Vector3(0, DockEntranceDistance, 0), 1, PaneDuration, TimeSpan.Zero);
        for (int index = 0; index < icons.Count; index++)
        {
            TimeSpan delay = IconInitialDelay + (IconStagger * index);
            AnimateEntrance(icons[index], new Vector3(0, 28, 0), 0.72f, ItemDuration, delay);
        }
    }


    public void AnimateBottomChrome(FrameworkElement surface, Action completed)
    {
        int generation = ++bottomAnimationGeneration;
        Visual visual = ElementCompositionPreview.GetElementVisual(surface);
        CompositionScopedBatch batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        batch.Completed += (sender, args) =>  {  batch.Dispose();  if (generation == bottomAnimationGeneration)  {  completed();  }  };
        AnimateEntrance(surface, new Vector3(0, DockEntranceDistance, 0), 1, PaneDuration, TimeSpan.Zero);
        batch.End();
    }


    public void ResetBottomChrome(FrameworkElement surface)
    {
        bottomAnimationGeneration++;
        Reset(surface);
    }


    public void AnimateTopChromeOutward(FrameworkElement surface, Action completed)
    {
        int generation = ++topAnimationGeneration;
        Visual visual = ElementCompositionPreview.GetElementVisual(surface);
        CompositionScopedBatch batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        batch.Completed += (sender, args) =>  {  batch.Dispose();  if (generation == topAnimationGeneration)  {  completed();  }  };
        AnimateExit(surface, new Vector3(0, -16, 0), 1, TopChromeExitDuration, TimeSpan.Zero, animateScale: false, animateOpacity: true);
        batch.End();
    }


    public void ResetTopChrome(FrameworkElement surface)
    {
        topAnimationGeneration++;
        Reset(surface);
    }


    public void AnimateOutward(FrameworkElement dockSurface, IReadOnlyList<FrameworkElement> icons, FrameworkElement shortcutSurface, Action completed)
    {
        bottomAnimationGeneration++;
        int generation = ++exitAnimationGeneration;
        Visual dockVisual = ElementCompositionPreview.GetElementVisual(dockSurface);
        CompositionScopedBatch batch = dockVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        batch.Completed += (sender, args) =>  {  batch.Dispose();  if (generation == exitAnimationGeneration)  {  completed();  }  };
        AnimateExit(dockSurface, new Vector3(0, DockEntranceDistance, 0), 1, PaneExitDuration, TimeSpan.Zero);
        AnimateExit(shortcutSurface, new Vector3(0, DockEntranceDistance, 0), 1, PaneExitDuration, TimeSpan.Zero);
        for (int index = icons.Count - 1; index >= 0; index--)
        {
            int reverseIndex = icons.Count - 1 - index;
            AnimateExit(icons[index], new Vector3(0, 28, 0), 0.72f, ItemExitDuration, TimeSpan.FromMilliseconds(6 * reverseIndex));
        }

        batch.End();
    }


    public void Reset(FrameworkElement element)
    {
        exitAnimationGeneration++;
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation(nameof(Visual.Offset));
        visual.StopAnimation(nameof(Visual.Scale));
        visual.StopAnimation(nameof(Visual.Opacity));
        if (exitRestingOffsets.Remove(element, out Vector3 restingOffset))
        {
            visual.Offset = restingOffset;
        }

        visual.Scale = Vector3.One;
        visual.Opacity = 1;
    }


    public void Reset(IEnumerable<FrameworkElement> elements)
    {
        foreach (FrameworkElement element in elements)
        {
            Reset(element);
        }
    }


    private void AnimateEntrance(FrameworkElement element, Vector3 initialOffset, float initialScale, TimeSpan duration, TimeSpan delay, bool animateScale = true, bool animateOpacity = true)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation(nameof(Visual.Offset));
        if (animateScale)
        {
            visual.StopAnimation(nameof(Visual.Scale));
        }

        if (animateOpacity)
        {
            visual.StopAnimation(nameof(Visual.Opacity));
            visual.Opacity = 1;
        }

        if (exitRestingOffsets.Remove(element, out Vector3 restoredOffset))
        {
            visual.Offset = restoredOffset;
        }

        float width = ToFloat(element.ActualWidth > 0 ? element.ActualWidth : element.Width);
        float height = ToFloat(element.ActualHeight > 0 ? element.ActualHeight : element.Height);
        visual.CenterPoint = new(width / 2, height / 2, 0);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(Vector2.Zero, new Vector2(0, 1));
        Vector3KeyFrameAnimation offset = compositor.CreateVector3KeyFrameAnimation();
        offset.Duration = duration;
        offset.DelayTime = delay;
        offset.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        Vector3 restingOffset = visual.Offset;
        offset.InsertKeyFrame(0, restingOffset + initialOffset);
        offset.InsertKeyFrame(1, restingOffset, easing);
        visual.StartAnimation(nameof(Visual.Offset), offset);
        if (animateScale)
        {
            Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
            scale.Duration = duration;
            scale.DelayTime = delay;
            scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            scale.InsertKeyFrame(0, new Vector3(initialScale, initialScale, 1));
            scale.InsertKeyFrame(1, Vector3.One, easing);
            visual.StartAnimation(nameof(Visual.Scale), scale);
            scale.Dispose();
        }

        if (animateOpacity)
        {
            ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.Duration = duration;
            opacity.DelayTime = delay;
            opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 1, easing);
            visual.StartAnimation(nameof(Visual.Opacity), opacity);
            opacity.Dispose();
        }

        offset.Dispose();
        easing.Dispose();
    }


    private void AnimateExit(FrameworkElement element, Vector3 targetOffset, float targetScale, TimeSpan duration, TimeSpan delay, bool animateScale = true, bool animateOpacity = true)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation(nameof(Visual.Offset));
        if (animateScale)
        {
            visual.StopAnimation(nameof(Visual.Scale));
        }

        if (animateOpacity)
        {
            visual.StopAnimation(nameof(Visual.Opacity));
        }

        Vector3 restingOffset = visual.Offset;
        exitRestingOffsets[element] = restingOffset;
        float width = ToFloat(element.ActualWidth > 0 ? element.ActualWidth : element.Width);
        float height = ToFloat(element.ActualHeight > 0 ? element.ActualHeight : element.Height);
        visual.CenterPoint = new(width / 2, height / 2, 0);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.55f, 0.55f), new Vector2(0, 1));
        Vector3KeyFrameAnimation offset = compositor.CreateVector3KeyFrameAnimation();
        offset.Duration = duration;
        offset.DelayTime = delay;
        offset.InsertExpressionKeyFrame(0, "this.StartingValue");
        offset.InsertKeyFrame(1, restingOffset + targetOffset, easing);
        visual.StartAnimation(nameof(Visual.Offset), offset);
        if (animateScale)
        {
            Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
            scale.Duration = duration;
            scale.DelayTime = delay;
            scale.InsertExpressionKeyFrame(0, "this.StartingValue");
            scale.InsertKeyFrame(1, new Vector3(targetScale, targetScale, 1), easing);
            visual.StartAnimation(nameof(Visual.Scale), scale);
            scale.Dispose();
        }

        if (animateOpacity)
        {
            ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.Duration = duration;
            opacity.DelayTime = delay;
            opacity.InsertExpressionKeyFrame(0, "this.StartingValue");
            opacity.InsertKeyFrame(1, 0, easing);
            visual.StartAnimation(nameof(Visual.Opacity), opacity);
            opacity.Dispose();
        }

        offset.Dispose();
        easing.Dispose();
    }


    private static float ToFloat(double value) => double.IsFinite(value) ? (float)Math.Clamp(value, 0, float.MaxValue) : 0;
}
