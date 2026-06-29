using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace Infinity.Shell.WinUI;

public sealed partial class WindowDragTriggerView :
    UserControl
{
    private const float ClickPressEnd = 900f;
    private const float ClickPressStart = 750f;
    private const float CtrlPressEnd = 500f;
    private const float CtrlPressStart = 300f;
    private const float CursorEntranceScale = 1.6f;
    private const float CursorFadeInEnd = 900f;
    private const float CursorFadeInStart = 400f;
    private const float CursorFadeOutEnd = 4100f;
    private const float CursorFadeOutStart = 3700f;
    private const float CursorScaleInEnd = 900f;
    private const float CursorScaleInStart = 400f;
    private const float CycleDuration = 5400f;
    private const float DragDeltaX = 98f;
    private const float DragEnd = 2700f;
    private const float DragStart = 900f;

    private const float EntranceDuration = 500f;
    private const float EntranceOffset = 16f;
    private const float EntranceStaggerStep = 100f;

    private const float KeyAccentOpacity = 0.6f;
    private const float KeyPressedScale = 0.92f;
    private const float KeysHoldEnd = 3100f;
    private const float KeysReleaseEnd = 3400f;
    private const float KeysReleaseStart = 3100f;

    private const float PageDotRevertEnd = 4500f;
    private const float PageDotRevertStart = 4200f;
    private const float PageDotSwapEnd = 2000f;
    private const float PageDotSwapStart = 1700f;
    private const float ReleaseEnd = 2900f;
    private const float ReleaseStart = 2700f;

    private const float WindowGrabScale = 0.96f;
    private const float WindowReturnEnd = 4900f;
    private const float WindowReturnStart = 4200f;

    private const float WinPressEnd = 400f;
    private const float WinPressStart = 200f;

    public WindowDragTriggerView()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
    }

    public WindowDragTriggerViewModel? ViewModel => DataContext as WindowDragTriggerViewModel;

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

    private void AnimateCursor()
    {
        Visual visual = SetupVisual(DragCursor);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction pressEasing = CreatePressEasing(compositor);
        CubicBezierEasingFunction releaseEasing = CreateReleaseEasing(compositor);
        float duration = CycleDuration;

        visual.Opacity = 0f;
        visual.Scale = new Vector3(CursorEntranceScale, CursorEntranceScale, 1f);

        ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.InsertKeyFrame(0f, 0f, releaseEasing);
        opacityAnimation.InsertKeyFrame(CursorFadeInStart / duration, 0f, pressEasing);
        opacityAnimation.InsertKeyFrame(CursorFadeInEnd / duration, 1f, pressEasing);
        opacityAnimation.InsertKeyFrame(CursorFadeOutStart / duration, 1f, pressEasing);
        opacityAnimation.InsertKeyFrame(CursorFadeOutEnd / duration, 0f, releaseEasing);
        opacityAnimation.InsertKeyFrame(1.00f, 0f, releaseEasing);
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        opacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        opacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Opacity", opacityAnimation);

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.InsertKeyFrame(0f, new Vector3(CursorEntranceScale, CursorEntranceScale, 1f), releaseEasing);
        scaleAnimation.InsertKeyFrame(CursorScaleInStart / duration, new Vector3(CursorEntranceScale, CursorEntranceScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(CursorScaleInEnd / duration, Vector3.One, pressEasing);
        scaleAnimation.InsertKeyFrame(ClickPressStart / duration, Vector3.One, pressEasing);
        scaleAnimation.InsertKeyFrame(ClickPressEnd / duration, new Vector3(WindowGrabScale, WindowGrabScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(ReleaseStart / duration, new Vector3(WindowGrabScale, WindowGrabScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(ReleaseEnd / duration, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(CursorFadeOutStart / duration, Vector3.One, pressEasing);
        scaleAnimation.InsertKeyFrame(CursorFadeOutEnd / duration, new Vector3(CursorEntranceScale, CursorEntranceScale, 1f), releaseEasing);
        scaleAnimation.InsertKeyFrame(1.00f, new Vector3(CursorEntranceScale, CursorEntranceScale, 1f), releaseEasing);
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        scaleAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        scaleAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Scale", scaleAnimation);

        ElementCompositionPreview.SetIsTranslationEnabled(DragCursor, true);
        ScalarKeyFrameAnimation translateAnimation = compositor.CreateScalarKeyFrameAnimation();
        translateAnimation.InsertKeyFrame(0f, 0f, releaseEasing);
        translateAnimation.InsertKeyFrame(DragStart / duration, 0f, pressEasing);
        translateAnimation.InsertKeyFrame(DragEnd / duration, DragDeltaX, pressEasing);
        translateAnimation.InsertKeyFrame(WindowReturnStart / duration, DragDeltaX, pressEasing);
        translateAnimation.InsertKeyFrame(WindowReturnEnd / duration, 0f, releaseEasing);
        translateAnimation.InsertKeyFrame(1.00f, 0f, releaseEasing);
        translateAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        translateAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        translateAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Translation.X", translateAnimation);
    }

    private void AnimateDragWindow()
    {
        Visual visual = SetupVisual(DragWindow);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction pressEasing = CreatePressEasing(compositor);
        CubicBezierEasingFunction releaseEasing = CreateReleaseEasing(compositor);
        float duration = CycleDuration;

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.InsertKeyFrame(0f, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(ClickPressStart / duration, Vector3.One, pressEasing);
        scaleAnimation.InsertKeyFrame(ClickPressEnd / duration, new Vector3(WindowGrabScale, WindowGrabScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(ReleaseStart / duration, new Vector3(WindowGrabScale, WindowGrabScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(ReleaseEnd / duration, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(1.00f, Vector3.One, releaseEasing);
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        scaleAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        scaleAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        ElementCompositionPreview.SetIsTranslationEnabled(DragWindow, true);
        ScalarKeyFrameAnimation translateAnimation = compositor.CreateScalarKeyFrameAnimation();
        translateAnimation.InsertKeyFrame(0f, 0f, releaseEasing);
        translateAnimation.InsertKeyFrame(DragStart / duration, 0f, pressEasing);
        translateAnimation.InsertKeyFrame(DragEnd / duration, DragDeltaX, pressEasing);
        translateAnimation.InsertKeyFrame(WindowReturnStart / duration, DragDeltaX, pressEasing);
        translateAnimation.InsertKeyFrame(WindowReturnEnd / duration, 0f, releaseEasing);
        translateAnimation.InsertKeyFrame(1.00f, 0f, releaseEasing);
        translateAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        translateAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        translateAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Scale", scaleAnimation);
        visual.StartAnimation("Translation.X", translateAnimation);

        Visual titlebarVisual = ElementCompositionPreview.GetElementVisual(DragWindowTitlebar);
        ScalarKeyFrameAnimation titlebarOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        titlebarOpacityAnimation.InsertKeyFrame(0f, 0.55f, releaseEasing);
        titlebarOpacityAnimation.InsertKeyFrame(ClickPressStart / duration, 0.55f, pressEasing);
        titlebarOpacityAnimation.InsertKeyFrame(ClickPressEnd / duration, 1f, pressEasing);
        titlebarOpacityAnimation.InsertKeyFrame(ReleaseStart / duration, 1f, pressEasing);
        titlebarOpacityAnimation.InsertKeyFrame(ReleaseEnd / duration, 0.55f, releaseEasing);
        titlebarOpacityAnimation.InsertKeyFrame(1.00f, 0.55f, releaseEasing);
        titlebarOpacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        titlebarOpacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        titlebarOpacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        titlebarVisual.StartAnimation("Opacity", titlebarOpacityAnimation);
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

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        AnimateEntrance(HeroCard, 0f);
        AnimateEntrance(HeadingGroup, EntranceStaggerStep);

        AnimateKeyPress(WinKeyCard, WinKeyAccentOverlay, WinPressStart, WinPressEnd);
        AnimateKeyPress(CtrlKeyCard, CtrlKeyAccentOverlay, CtrlPressStart, CtrlPressEnd);
        AnimateCursor();
        AnimateDragWindow();
        AnimatePageDots();
    }
}