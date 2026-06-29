using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Infinity.Shell.WinUI;

public sealed partial class ScrollTriggerView :
    UserControl
{
    private const float CtrlHoldEnd = 3200f;
    private const float CtrlPressEnd = 650f;
    private const float CtrlPressStart = 500f;
    private const float CtrlReleaseEnd = 3400f;
    private const float CycleDuration = 5000f;

    private const float EntranceDuration = 500f;
    private const float EntranceOffset = 16f;
    private const float EntranceStaggerStep = 100f;

    private const float KeyAccentOpacity = 0.6f;
    private const float KeyPressedScale = 0.92f;

    private const float MiddleFlashOpacity = 0.5f;
    private const float MiddleHoldEnd = 2900f;
    private const float MiddlePressEnd = 2650f;
    private const float MiddlePressStart = 2500f;
    private const float MiddleReleaseEnd = 3100f;
    private const float MouseActiveEnd = 1150f;
    private const float MouseActiveStart = 1000f;
    private const float MouseDimEnd = 3400f;
    private const float MouseDimOpacity = 0.4f;
    private const float MouseDimStart = 3200f;
    private const float Scroll1Down = 1350f;
    private const float Scroll1Up = 1500f;
    private const float Scroll2Down = 1650f;
    private const float Scroll2Up = 1800f;
    private const float Scroll3Down = 1950f;
    private const float Scroll3Up = 2100f;
    private const float ScrollStart = 1200f;
    private const float WheelOffset = -3f;
    private const float WheelPressedScale = 0.75f;
    private const float WinHoldEnd = 3200f;
    private const float WinPressEnd = 150f;
    private const float WinPressStart = 0f;
    private const float WinReleaseEnd = 3400f;

    public ScrollTriggerView()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
    }

    public ScrollTriggerViewModel ViewModel => (ScrollTriggerViewModel)DataContext;

    private static CubicBezierEasingFunction CreateEntranceEasing(Compositor compositor) =>
        compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));

    private static CubicBezierEasingFunction CreatePressEasing(Compositor compositor) =>
        compositor.CreateCubicBezierEasingFunction(new Vector2(0.25f, 0.1f), new Vector2(0.25f, 1.0f));

    private static CubicBezierEasingFunction CreateReleaseEasing(Compositor compositor) =>
        compositor.CreateCubicBezierEasingFunction(new Vector2(0.0f, 0.0f), new Vector2(0.58f, 1.0f));

    private static Visual SetupVisual(UIElement element)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3((float)(element.RenderSize.Width / 2), (float)(element.RenderSize.Height / 2), 0f);
        return visual;
    }

    private void AnimateChevronPulse(UIElement chevron)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(chevron);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction pressEasing = CreatePressEasing(compositor);
        CubicBezierEasingFunction releaseEasing = CreateReleaseEasing(compositor);
        float duration = CycleDuration;

        ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.InsertKeyFrame(0f, 0f, releaseEasing);
        opacityAnimation.InsertKeyFrame(ScrollStart / duration, 0f, pressEasing);
        opacityAnimation.InsertKeyFrame(Scroll1Down / duration, 1f, pressEasing);
        opacityAnimation.InsertKeyFrame(Scroll1Up / duration, 0f, releaseEasing);
        opacityAnimation.InsertKeyFrame(Scroll2Down / duration, 1f, pressEasing);
        opacityAnimation.InsertKeyFrame(Scroll2Up / duration, 0f, releaseEasing);
        opacityAnimation.InsertKeyFrame(Scroll3Down / duration, 1f, pressEasing);
        opacityAnimation.InsertKeyFrame(Scroll3Up / duration, 0f, releaseEasing);
        opacityAnimation.InsertKeyFrame(1.00f, 0f, releaseEasing);
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        opacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        opacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Opacity", opacityAnimation);
    }

    private void AnimateEntrance(UIElement element, float delay)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);

        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = CreateEntranceEasing(compositor);

        visual.Properties.InsertVector3("Translation", new Vector3(0f, EntranceOffset, 0f));
        visual.Opacity = 0f;

        Vector3KeyFrameAnimation translateAnimation = compositor.CreateVector3KeyFrameAnimation();
        translateAnimation.InsertKeyFrame(0f, new Vector3(0f, EntranceOffset, 0f));
        translateAnimation.InsertKeyFrame(1f, Vector3.Zero, easing);
        translateAnimation.Duration = TimeSpan.FromMilliseconds(EntranceDuration);
        translateAnimation.DelayTime = TimeSpan.FromMilliseconds(delay);
        translateAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.InsertKeyFrame(0f, 0f);
        opacityAnimation.InsertKeyFrame(1f, 1f, easing);
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(EntranceDuration);
        opacityAnimation.DelayTime = TimeSpan.FromMilliseconds(delay);
        opacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Translation", translateAnimation);
        visual.StartAnimation("Opacity", opacityAnimation);
    }

    private void AnimateKeyPress(UIElement keyCard, UIElement accentOverlay, float pressStart, float pressEnd, float holdEnd, float releaseEnd)
    {
        Visual keyVisual = SetupVisual(keyCard);
        Compositor compositor = keyVisual.Compositor;
        CubicBezierEasingFunction pressEasing = CreatePressEasing(compositor);
        CubicBezierEasingFunction releaseEasing = CreateReleaseEasing(compositor);
        float duration = CycleDuration;

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.InsertKeyFrame(0f, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(pressStart / duration, Vector3.One, pressEasing);
        scaleAnimation.InsertKeyFrame(pressEnd / duration, new Vector3(KeyPressedScale, KeyPressedScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(holdEnd / duration, new Vector3(KeyPressedScale, KeyPressedScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(releaseEnd / duration, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(1.00f, Vector3.One, releaseEasing);
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        scaleAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        scaleAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        keyVisual.StartAnimation("Scale", scaleAnimation);

        Visual overlayVisual = ElementCompositionPreview.GetElementVisual(accentOverlay);
        ScalarKeyFrameAnimation overlayOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        overlayOpacityAnimation.InsertKeyFrame(0f, 0f, releaseEasing);
        overlayOpacityAnimation.InsertKeyFrame(pressStart / duration, 0f, pressEasing);
        overlayOpacityAnimation.InsertKeyFrame(pressEnd / duration, KeyAccentOpacity, pressEasing);
        overlayOpacityAnimation.InsertKeyFrame(holdEnd / duration, KeyAccentOpacity, pressEasing);
        overlayOpacityAnimation.InsertKeyFrame(releaseEnd / duration, 0f, releaseEasing);
        overlayOpacityAnimation.InsertKeyFrame(1.00f, 0f, releaseEasing);
        overlayOpacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        overlayOpacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        overlayOpacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        overlayVisual.StartAnimation("Opacity", overlayOpacityAnimation);
    }

    private void AnimateMouseGroup()
    {
        Visual visual = SetupVisual(MouseGroup);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction pressEasing = CreatePressEasing(compositor);
        CubicBezierEasingFunction releaseEasing = CreateReleaseEasing(compositor);
        float duration = CycleDuration;

        ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.InsertKeyFrame(0f, MouseDimOpacity, releaseEasing);
        opacityAnimation.InsertKeyFrame(MouseActiveStart / duration, MouseDimOpacity, pressEasing);
        opacityAnimation.InsertKeyFrame(MouseActiveEnd / duration, 1f, pressEasing);
        opacityAnimation.InsertKeyFrame(MouseDimStart / duration, 1f, pressEasing);
        opacityAnimation.InsertKeyFrame(MouseDimEnd / duration, MouseDimOpacity, releaseEasing);
        opacityAnimation.InsertKeyFrame(1.00f, MouseDimOpacity, releaseEasing);
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        opacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        opacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Opacity", opacityAnimation);
    }

    private void AnimateMouseMiddlePress()
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(MouseMiddleFlash);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction pressEasing = CreatePressEasing(compositor);
        CubicBezierEasingFunction releaseEasing = CreateReleaseEasing(compositor);
        float duration = CycleDuration;

        ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.InsertKeyFrame(0f, 0f, releaseEasing);
        opacityAnimation.InsertKeyFrame(MiddlePressStart / duration, 0f, pressEasing);
        opacityAnimation.InsertKeyFrame(MiddlePressEnd / duration, MiddleFlashOpacity, pressEasing);
        opacityAnimation.InsertKeyFrame(MiddleHoldEnd / duration, MiddleFlashOpacity, pressEasing);
        opacityAnimation.InsertKeyFrame(MiddleReleaseEnd / duration, 0f, releaseEasing);
        opacityAnimation.InsertKeyFrame(1.00f, 0f, releaseEasing);
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        opacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        opacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Opacity", opacityAnimation);
    }

    private void AnimateMouseWheel()
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(MouseWheel);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction pressEasing = CreatePressEasing(compositor);
        CubicBezierEasingFunction releaseEasing = CreateReleaseEasing(compositor);
        float duration = CycleDuration;

        visual.CenterPoint = new Vector3(
            (float)(MouseWheel.RenderSize.Width / 2),
            (float)(MouseWheel.RenderSize.Height / 2),
            0f);

        ScalarKeyFrameAnimation offsetAnimation = compositor.CreateScalarKeyFrameAnimation();
        offsetAnimation.InsertKeyFrame(0f, 0f, releaseEasing);
        offsetAnimation.InsertKeyFrame(ScrollStart / duration, 0f, pressEasing);
        offsetAnimation.InsertKeyFrame(Scroll1Down / duration, WheelOffset, pressEasing);
        offsetAnimation.InsertKeyFrame(Scroll1Up / duration, 0f, releaseEasing);
        offsetAnimation.InsertKeyFrame(Scroll2Down / duration, WheelOffset, pressEasing);
        offsetAnimation.InsertKeyFrame(Scroll2Up / duration, 0f, releaseEasing);
        offsetAnimation.InsertKeyFrame(Scroll3Down / duration, WheelOffset, pressEasing);
        offsetAnimation.InsertKeyFrame(Scroll3Up / duration, 0f, releaseEasing);
        offsetAnimation.InsertKeyFrame(1.00f, 0f, releaseEasing);
        offsetAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        offsetAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        offsetAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.InsertKeyFrame(0f, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(ScrollStart / duration, Vector3.One, pressEasing);
        scaleAnimation.InsertKeyFrame(Scroll1Down / duration, new Vector3(WheelPressedScale, WheelPressedScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(Scroll1Up / duration, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(Scroll2Down / duration, new Vector3(WheelPressedScale, WheelPressedScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(Scroll2Up / duration, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(Scroll3Down / duration, new Vector3(WheelPressedScale, WheelPressedScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(Scroll3Up / duration, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(1.00f, Vector3.One, releaseEasing);
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        scaleAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        scaleAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Offset.Y", offsetAnimation);
        visual.StartAnimation("Scale", scaleAnimation);
    }

    private void AnimateScrollChevrons()
    {
        AnimateChevronPulse(ScrollChevronUp);
        AnimateChevronPulse(ScrollChevronDown);
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        AnimateEntrance(HeroCard, 0f);
        AnimateEntrance(HeadingGroup, EntranceStaggerStep);

        AnimateKeyPress(WinKeyCard, WinKeyAccentOverlay, WinPressStart, WinPressEnd, WinHoldEnd, WinReleaseEnd);
        AnimateKeyPress(CtrlKeyCard, CtrlKeyAccentOverlay, CtrlPressStart, CtrlPressEnd, CtrlHoldEnd, CtrlReleaseEnd);
        AnimateMouseGroup();
        AnimateMouseWheel();
        AnimateMouseMiddlePress();
        AnimateScrollChevrons();
    }
}