using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Infinity.Shell.WinUI;

public sealed partial class PageSwitchTriggerView :
    UserControl
{
    private const float CtrlPressEnd = 500f;
    private const float CtrlPressStart = 300f;
    private const float CycleDuration = 5400f;

    private const float EntranceDuration = 500f;
    private const float EntranceOffset = 16f;
    private const float EntranceStaggerStep = 100f;

    private const float KeyAccentOpacity = 0.6f;
    private const float KeyPressedScale = 0.92f;
    private const float KeysHoldEnd = 3700f;
    private const float KeysReleaseEnd = 4000f;

    private const float LeftFlashEnd = 3400f;
    private const float LeftPressEnd = 3120f;
    private const float LeftPressStart = 3000f;
    private const float LeftSettleEnd = 3600f;

    private const float PageDotRevertEnd = 3500f;
    private const float PageDotRevertStart = 3200f;
    private const float PageDotSwapEnd = 1400f;
    private const float PageDotSwapStart = 1100f;

    private const float RightFlashEnd = 1300f;
    private const float RightPressEnd = 1020f;
    private const float RightPressStart = 900f;
    private const float RightSettleEnd = 1500f;

    private const float ViewportDeltaX = 98f;
    private const float ViewportSnapScale = 0.96f;

    private const float WinPressEnd = 400f;
    private const float WinPressStart = 200f;

    public PageSwitchTriggerView()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
    }

    public PageSwitchTriggerViewModel? ViewModel => DataContext as PageSwitchTriggerViewModel;

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

    private void AnimateArrowTap(UIElement keyCard, UIElement accentOverlay, float pressStart, float pressEnd, float flashEnd)
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
        scaleAnimation.InsertKeyFrame(flashEnd / duration, Vector3.One, releaseEasing);
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
        overlayOpacityAnimation.InsertKeyFrame(flashEnd / duration, 0f, releaseEasing);
        overlayOpacityAnimation.InsertKeyFrame(1.00f, 0f, releaseEasing);
        overlayOpacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        overlayOpacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        overlayOpacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        overlayVisual.StartAnimation("Opacity", overlayOpacityAnimation);
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

    private void AnimateKeyPress(UIElement keyCard, UIElement accentOverlay, float pressStart, float pressEnd)
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
        scaleAnimation.InsertKeyFrame(KeysHoldEnd / duration, new Vector3(KeyPressedScale, KeyPressedScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(KeysReleaseEnd / duration, Vector3.One, releaseEasing);
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
        overlayOpacityAnimation.InsertKeyFrame(KeysHoldEnd / duration, KeyAccentOpacity, pressEasing);
        overlayOpacityAnimation.InsertKeyFrame(KeysReleaseEnd / duration, 0f, releaseEasing);
        overlayOpacityAnimation.InsertKeyFrame(1.00f, 0f, releaseEasing);
        overlayOpacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        overlayOpacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        overlayOpacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        overlayVisual.StartAnimation("Opacity", overlayOpacityAnimation);
    }

    private void AnimatePageDots()
    {
        Visual dotOneAccentVisual = ElementCompositionPreview.GetElementVisual(PageDotOneAccent);
        Visual dotTwoAccentVisual = ElementCompositionPreview.GetElementVisual(PageDotTwoAccent);
        Compositor compositor = dotOneAccentVisual.Compositor;
        CubicBezierEasingFunction pressEasing = CreatePressEasing(compositor);
        CubicBezierEasingFunction releaseEasing = CreateReleaseEasing(compositor);
        float duration = CycleDuration;

        dotOneAccentVisual.Opacity = 1f;
        dotTwoAccentVisual.Opacity = 0f;

        ScalarKeyFrameAnimation dotOneAccentOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        dotOneAccentOpacityAnimation.InsertKeyFrame(0f, 1f, releaseEasing);
        dotOneAccentOpacityAnimation.InsertKeyFrame(PageDotSwapStart / duration, 1f, pressEasing);
        dotOneAccentOpacityAnimation.InsertKeyFrame(PageDotSwapEnd / duration, 0f, pressEasing);
        dotOneAccentOpacityAnimation.InsertKeyFrame(PageDotRevertStart / duration, 0f, pressEasing);
        dotOneAccentOpacityAnimation.InsertKeyFrame(PageDotRevertEnd / duration, 1f, releaseEasing);
        dotOneAccentOpacityAnimation.InsertKeyFrame(1.00f, 1f, releaseEasing);
        dotOneAccentOpacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        dotOneAccentOpacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        dotOneAccentOpacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        ScalarKeyFrameAnimation dotTwoAccentOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        dotTwoAccentOpacityAnimation.InsertKeyFrame(0f, 0f, releaseEasing);
        dotTwoAccentOpacityAnimation.InsertKeyFrame(PageDotSwapStart / duration, 0f, pressEasing);
        dotTwoAccentOpacityAnimation.InsertKeyFrame(PageDotSwapEnd / duration, 1f, pressEasing);
        dotTwoAccentOpacityAnimation.InsertKeyFrame(PageDotRevertStart / duration, 1f, pressEasing);
        dotTwoAccentOpacityAnimation.InsertKeyFrame(PageDotRevertEnd / duration, 0f, releaseEasing);
        dotTwoAccentOpacityAnimation.InsertKeyFrame(1.00f, 0f, releaseEasing);
        dotTwoAccentOpacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        dotTwoAccentOpacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        dotTwoAccentOpacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        dotOneAccentVisual.StartAnimation("Opacity", dotOneAccentOpacityAnimation);
        dotTwoAccentVisual.StartAnimation("Opacity", dotTwoAccentOpacityAnimation);
    }

    private void AnimateViewportFrame()
    {
        Visual visual = SetupVisual(ViewportFrame);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction pressEasing = CreatePressEasing(compositor);
        CubicBezierEasingFunction releaseEasing = CreateReleaseEasing(compositor);
        float duration = CycleDuration;

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.InsertKeyFrame(0f, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(RightPressStart / duration, Vector3.One, pressEasing);
        scaleAnimation.InsertKeyFrame(RightPressEnd / duration, new Vector3(ViewportSnapScale, ViewportSnapScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(RightSettleEnd / duration, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(LeftPressStart / duration, Vector3.One, pressEasing);
        scaleAnimation.InsertKeyFrame(LeftPressEnd / duration, new Vector3(ViewportSnapScale, ViewportSnapScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(LeftSettleEnd / duration, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(1.00f, Vector3.One, releaseEasing);
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        scaleAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        scaleAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        ElementCompositionPreview.SetIsTranslationEnabled(ViewportFrame, true);
        ScalarKeyFrameAnimation translateAnimation = compositor.CreateScalarKeyFrameAnimation();
        translateAnimation.InsertKeyFrame(0f, 0f, releaseEasing);
        translateAnimation.InsertKeyFrame(RightPressStart / duration, 0f, pressEasing);
        translateAnimation.InsertKeyFrame(RightPressEnd / duration, 0f, pressEasing);
        translateAnimation.InsertKeyFrame(RightSettleEnd / duration, ViewportDeltaX, pressEasing);
        translateAnimation.InsertKeyFrame(LeftPressStart / duration, ViewportDeltaX, pressEasing);
        translateAnimation.InsertKeyFrame(LeftPressEnd / duration, ViewportDeltaX, pressEasing);
        translateAnimation.InsertKeyFrame(LeftSettleEnd / duration, 0f, releaseEasing);
        translateAnimation.InsertKeyFrame(1.00f, 0f, releaseEasing);
        translateAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        translateAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        translateAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Scale", scaleAnimation);
        visual.StartAnimation("Translation.X", translateAnimation);
    }
    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        AnimateEntrance(HeroCard, 0f);
        AnimateEntrance(HeadingGroup, EntranceStaggerStep);

        AnimateKeyPress(WinKeyCard, WinKeyAccentOverlay, WinPressStart, WinPressEnd);
        AnimateKeyPress(CtrlKeyCard, CtrlKeyAccentOverlay, CtrlPressStart, CtrlPressEnd);
        AnimateArrowTap(LeftArrowKeyCard, LeftArrowAccentOverlay, LeftPressStart, LeftPressEnd, LeftFlashEnd);
        AnimateArrowTap(RightArrowKeyCard, RightArrowAccentOverlay, RightPressStart, RightPressEnd, RightFlashEnd);
        AnimateViewportFrame();
        AnimatePageDots();
    }
}