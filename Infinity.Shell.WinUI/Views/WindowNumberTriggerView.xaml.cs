using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Diagnostics;
using System.Numerics;

namespace Infinity.Shell.WinUI;

public sealed partial class WindowNumberTriggerView :
    UserControl
{
    private const float CtrlPressEnd = 500f;
    private const float CtrlPressStart = 300f;
    private const float CycleDuration = 6000f;

    private const float EntranceDuration = 500f;
    private const float EntranceOffset = 16f;
    private const float EntranceStaggerStep = 100f;

    private const float JumpDeltaX = 78f;

    private const float KeyAccentOpacity = 0.6f;
    private const float KeyPressedScale = 0.92f;
    private const float KeysHoldEnd = 4600f;
    private const float KeysReleaseEnd = 4900f;

    private const float Press1End = 1020f;
    private const float Press1FlashEnd = 1300f;
    private const float Press1SettleEnd = 1500f;
    private const float Press1Start = 900f;

    private const float Press2End = 2520f;
    private const float Press2FlashEnd = 2800f;
    private const float Press2SettleEnd = 3000f;
    private const float Press2Start = 2400f;

    private const float Press3End = 4020f;
    private const float Press3FlashEnd = 4300f;
    private const float Press3SettleEnd = 4500f;
    private const float Press3Start = 3900f;

    private const float ShiftPressEnd = 700f;
    private const float ShiftPressStart = 500f;

    private const float WindowSnapScale = 0.94f;
    private const float WinPressEnd = 400f;
    private const float WinPressStart = 200f;

    private readonly Stopwatch digitCycleStopwatch = new();

    private bool animationsStarted;
    private string currentDigitText = "1";
    private DispatcherTimer? digitCycleTimer;

    public WindowNumberTriggerView()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    public WindowNumberTriggerViewModel? ViewModel => DataContext as WindowNumberTriggerViewModel;
    
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

    private void AnimateJumpWindow()
    {
        Visual visual = SetupVisual(JumpWindow);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction pressEasing = CreatePressEasing(compositor);
        CubicBezierEasingFunction releaseEasing = CreateReleaseEasing(compositor);
        float duration = CycleDuration;

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.InsertKeyFrame(0f, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(Press1Start / duration, Vector3.One, pressEasing);
        scaleAnimation.InsertKeyFrame(Press1End / duration, new Vector3(WindowSnapScale, WindowSnapScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(Press1SettleEnd / duration, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(Press2Start / duration, Vector3.One, pressEasing);
        scaleAnimation.InsertKeyFrame(Press2End / duration, new Vector3(WindowSnapScale, WindowSnapScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(Press2SettleEnd / duration, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(Press3Start / duration, Vector3.One, pressEasing);
        scaleAnimation.InsertKeyFrame(Press3End / duration, new Vector3(WindowSnapScale, WindowSnapScale, 1f), pressEasing);
        scaleAnimation.InsertKeyFrame(Press3SettleEnd / duration, Vector3.One, releaseEasing);
        scaleAnimation.InsertKeyFrame(1.00f, Vector3.One, releaseEasing);
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        scaleAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        scaleAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        ElementCompositionPreview.SetIsTranslationEnabled(JumpWindow, true);
        ScalarKeyFrameAnimation translateAnimation = compositor.CreateScalarKeyFrameAnimation();
        translateAnimation.InsertKeyFrame(0f, 0f, releaseEasing);
        translateAnimation.InsertKeyFrame(Press1Start / duration, 0f, pressEasing);
        translateAnimation.InsertKeyFrame(Press1End / duration, 0f, pressEasing);
        translateAnimation.InsertKeyFrame(Press1SettleEnd / duration, JumpDeltaX, pressEasing);
        translateAnimation.InsertKeyFrame(Press2Start / duration, JumpDeltaX, pressEasing);
        translateAnimation.InsertKeyFrame(Press2End / duration, JumpDeltaX, pressEasing);
        translateAnimation.InsertKeyFrame(Press2SettleEnd / duration, JumpDeltaX * 2f, pressEasing);
        translateAnimation.InsertKeyFrame(Press3Start / duration, JumpDeltaX * 2f, pressEasing);
        translateAnimation.InsertKeyFrame(Press3End / duration, JumpDeltaX * 2f, pressEasing);
        translateAnimation.InsertKeyFrame(Press3SettleEnd / duration, 0f, releaseEasing);
        translateAnimation.InsertKeyFrame(1.00f, 0f, releaseEasing);
        translateAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        translateAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        translateAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Scale", scaleAnimation);
        visual.StartAnimation("Translation.X", translateAnimation);

        Visual titlebarVisual = ElementCompositionPreview.GetElementVisual(JumpWindowTitlebar);
        ScalarKeyFrameAnimation titlebarOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        titlebarOpacityAnimation.InsertKeyFrame(0f, 0.55f, releaseEasing);
        titlebarOpacityAnimation.InsertKeyFrame(Press1Start / duration, 0.55f, pressEasing);
        titlebarOpacityAnimation.InsertKeyFrame(Press1End / duration, 1f, pressEasing);
        titlebarOpacityAnimation.InsertKeyFrame(Press1SettleEnd / duration, 1f, pressEasing);
        titlebarOpacityAnimation.InsertKeyFrame((Press1SettleEnd + 200f) / duration, 0.55f, releaseEasing);
        titlebarOpacityAnimation.InsertKeyFrame(Press2Start / duration, 0.55f, pressEasing);
        titlebarOpacityAnimation.InsertKeyFrame(Press2End / duration, 1f, pressEasing);
        titlebarOpacityAnimation.InsertKeyFrame(Press2SettleEnd / duration, 1f, pressEasing);
        titlebarOpacityAnimation.InsertKeyFrame((Press2SettleEnd + 200f) / duration, 0.55f, releaseEasing);
        titlebarOpacityAnimation.InsertKeyFrame(Press3Start / duration, 0.55f, pressEasing);
        titlebarOpacityAnimation.InsertKeyFrame(Press3End / duration, 1f, pressEasing);
        titlebarOpacityAnimation.InsertKeyFrame(Press3SettleEnd / duration, 1f, pressEasing);
        titlebarOpacityAnimation.InsertKeyFrame((Press3SettleEnd + 200f) / duration, 0.55f, releaseEasing);
        titlebarOpacityAnimation.InsertKeyFrame(1.00f, 0.55f, releaseEasing);
        titlebarOpacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        titlebarOpacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        titlebarOpacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        titlebarVisual.StartAnimation("Opacity", titlebarOpacityAnimation);
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

    private void AnimateNumberKeyCard()
    {
        Visual cardVisual = SetupVisual(NumberKeyCard);
        Compositor compositor = cardVisual.Compositor;
        CubicBezierEasingFunction pressEasing = CreatePressEasing(compositor);
        CubicBezierEasingFunction releaseEasing = CreateReleaseEasing(compositor);
        float duration = CycleDuration;

        Vector3KeyFrameAnimation cardScaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        cardScaleAnimation.InsertKeyFrame(0f, Vector3.One, releaseEasing);
        cardScaleAnimation.InsertKeyFrame(Press1Start / duration, Vector3.One, pressEasing);
        cardScaleAnimation.InsertKeyFrame(Press1End / duration, new Vector3(KeyPressedScale, KeyPressedScale, 1f), pressEasing);
        cardScaleAnimation.InsertKeyFrame(Press1FlashEnd / duration, Vector3.One, releaseEasing);
        cardScaleAnimation.InsertKeyFrame(Press2Start / duration, Vector3.One, pressEasing);
        cardScaleAnimation.InsertKeyFrame(Press2End / duration, new Vector3(KeyPressedScale, KeyPressedScale, 1f), pressEasing);
        cardScaleAnimation.InsertKeyFrame(Press2FlashEnd / duration, Vector3.One, releaseEasing);
        cardScaleAnimation.InsertKeyFrame(Press3Start / duration, Vector3.One, pressEasing);
        cardScaleAnimation.InsertKeyFrame(Press3End / duration, new Vector3(KeyPressedScale, KeyPressedScale, 1f), pressEasing);
        cardScaleAnimation.InsertKeyFrame(Press3FlashEnd / duration, Vector3.One, releaseEasing);
        cardScaleAnimation.InsertKeyFrame(1.00f, Vector3.One, releaseEasing);
        cardScaleAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        cardScaleAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        cardScaleAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        cardVisual.StartAnimation("Scale", cardScaleAnimation);

        Visual overlayVisual = ElementCompositionPreview.GetElementVisual(NumberKeyAccentOverlay);
        ScalarKeyFrameAnimation overlayOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        overlayOpacityAnimation.InsertKeyFrame(0f, 0f, releaseEasing);
        overlayOpacityAnimation.InsertKeyFrame(Press1Start / duration, 0f, pressEasing);
        overlayOpacityAnimation.InsertKeyFrame(Press1End / duration, KeyAccentOpacity, pressEasing);
        overlayOpacityAnimation.InsertKeyFrame(Press1FlashEnd / duration, 0f, releaseEasing);
        overlayOpacityAnimation.InsertKeyFrame(Press2Start / duration, 0f, pressEasing);
        overlayOpacityAnimation.InsertKeyFrame(Press2End / duration, KeyAccentOpacity, pressEasing);
        overlayOpacityAnimation.InsertKeyFrame(Press2FlashEnd / duration, 0f, releaseEasing);
        overlayOpacityAnimation.InsertKeyFrame(Press3Start / duration, 0f, pressEasing);
        overlayOpacityAnimation.InsertKeyFrame(Press3End / duration, KeyAccentOpacity, pressEasing);
        overlayOpacityAnimation.InsertKeyFrame(Press3FlashEnd / duration, 0f, releaseEasing);
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
        Visual dotThreeAccentVisual = ElementCompositionPreview.GetElementVisual(PageDotThreeAccent);
        Compositor compositor = dotOneAccentVisual.Compositor;
        CubicBezierEasingFunction pressEasing = CreatePressEasing(compositor);
        CubicBezierEasingFunction releaseEasing = CreateReleaseEasing(compositor);
        float duration = CycleDuration;

        dotOneAccentVisual.Opacity = 1f;
        dotTwoAccentVisual.Opacity = 0f;
        dotThreeAccentVisual.Opacity = 0f;

        ScalarKeyFrameAnimation dotOneAccentOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        dotOneAccentOpacityAnimation.InsertKeyFrame(0f, 1f, releaseEasing);
        dotOneAccentOpacityAnimation.InsertKeyFrame(Press1End / duration, 1f, pressEasing);
        dotOneAccentOpacityAnimation.InsertKeyFrame(Press1SettleEnd / duration, 0f, releaseEasing);
        dotOneAccentOpacityAnimation.InsertKeyFrame(Press3End / duration, 0f, pressEasing);
        dotOneAccentOpacityAnimation.InsertKeyFrame(Press3SettleEnd / duration, 1f, releaseEasing);
        dotOneAccentOpacityAnimation.InsertKeyFrame(1.00f, 1f, releaseEasing);
        dotOneAccentOpacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        dotOneAccentOpacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        dotOneAccentOpacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        ScalarKeyFrameAnimation dotTwoAccentOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        dotTwoAccentOpacityAnimation.InsertKeyFrame(0f, 0f, releaseEasing);
        dotTwoAccentOpacityAnimation.InsertKeyFrame(Press1End / duration, 0f, pressEasing);
        dotTwoAccentOpacityAnimation.InsertKeyFrame(Press1SettleEnd / duration, 1f, releaseEasing);
        dotTwoAccentOpacityAnimation.InsertKeyFrame(Press2End / duration, 1f, pressEasing);
        dotTwoAccentOpacityAnimation.InsertKeyFrame(Press2SettleEnd / duration, 0f, releaseEasing);
        dotTwoAccentOpacityAnimation.InsertKeyFrame(1.00f, 0f, releaseEasing);
        dotTwoAccentOpacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        dotTwoAccentOpacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        dotTwoAccentOpacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        ScalarKeyFrameAnimation dotThreeAccentOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        dotThreeAccentOpacityAnimation.InsertKeyFrame(0f, 0f, releaseEasing);
        dotThreeAccentOpacityAnimation.InsertKeyFrame(Press2End / duration, 0f, pressEasing);
        dotThreeAccentOpacityAnimation.InsertKeyFrame(Press2SettleEnd / duration, 1f, releaseEasing);
        dotThreeAccentOpacityAnimation.InsertKeyFrame(Press3End / duration, 1f, pressEasing);
        dotThreeAccentOpacityAnimation.InsertKeyFrame(Press3SettleEnd / duration, 0f, releaseEasing);
        dotThreeAccentOpacityAnimation.InsertKeyFrame(1.00f, 0f, releaseEasing);
        dotThreeAccentOpacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        dotThreeAccentOpacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        dotThreeAccentOpacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        dotOneAccentVisual.StartAnimation("Opacity", dotOneAccentOpacityAnimation);
        dotTwoAccentVisual.StartAnimation("Opacity", dotTwoAccentOpacityAnimation);
        dotThreeAccentVisual.StartAnimation("Opacity", dotThreeAccentOpacityAnimation);
    }

    private void HandleDigitCycleTick(object? sender, object e)
    {
        double elapsed = digitCycleStopwatch.Elapsed.TotalMilliseconds % CycleDuration;
        string digit = elapsed < Press1SettleEnd
            ? "1"
            : elapsed < Press2SettleEnd
                ? "2"
                : elapsed < Press3SettleEnd
                    ? "3"
                    : "1";

        if (digit != currentDigitText)
        {
            currentDigitText = digit;
            NumberDigitText.Text = digit;
        }
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        if (animationsStarted)
        {
            return;
        }

        animationsStarted = true;

        AnimateEntrance(HeroCard, 0f);
        AnimateEntrance(HeadingGroup, EntranceStaggerStep);

        AnimateKeyPress(WinKeyCard, WinKeyAccentOverlay, WinPressStart, WinPressEnd);
        AnimateKeyPress(CtrlKeyCard, CtrlKeyAccentOverlay, CtrlPressStart, CtrlPressEnd);
        AnimateKeyPress(ShiftKeyCard, ShiftKeyAccentOverlay, ShiftPressStart, ShiftPressEnd);
        AnimateNumberKeyCard();
        AnimateJumpWindow();
        AnimatePageDots();

        StartDigitCycle();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        digitCycleTimer?.Stop();
        digitCycleTimer = null;
        digitCycleStopwatch.Reset();
    }

    private void StartDigitCycle()
    {
        currentDigitText = "1";
        NumberDigitText.Text = "1";

        digitCycleStopwatch.Restart();

        digitCycleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        digitCycleTimer.Tick += HandleDigitCycleTick;
        digitCycleTimer.Start();
    }
}