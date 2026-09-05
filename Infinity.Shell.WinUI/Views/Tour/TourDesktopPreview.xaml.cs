using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;

namespace Infinity.Shell.WinUI;

public sealed partial class TourDesktopPreview : UserControl
{
    private const float CycleDuration = 4800;
    private const float PageDistance = 212;
    private bool animationsStarted;
    public static readonly DependencyProperty ScenarioProperty = DependencyProperty.Register(nameof(Scenario), typeof(TourDesktopScenario), typeof(TourDesktopPreview), new PropertyMetadata(TourDesktopScenario.Overview, HandleScenarioChanged));

    public TourDesktopPreview()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }


    public TourDesktopScenario Scenario { get => (TourDesktopScenario)GetValue(ScenarioProperty); set => SetValue(ScenarioProperty, value); }


    private static void HandleScenarioChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is TourDesktopPreview preview && preview.IsLoaded)
        {
            preview.StartScenario();
        }
    }


    private static CubicBezierEasingFunction CreateEasing(Compositor compositor) => compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        ApplySceneClip();
        StartScenario();
    }


    private void HandleUnloaded(object sender, RoutedEventArgs args) => StopScenario();

    private void ApplySceneClip()
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(Scene);
        Compositor compositor = visual.Compositor;
        CompositionRoundedRectangleGeometry geometry = compositor.CreateRoundedRectangleGeometry();
        geometry.Size = new(520, 200);
        geometry.CornerRadius = new(12);
        visual.Clip = compositor.CreateGeometricClip(geometry);
    }


    private void StartScenario()
    {
        StopScenario();
        ResetVisuals();
        switch (Scenario)
        {
            case TourDesktopScenario.Overview:
                StartOverviewAnimation();
                break;
            case TourDesktopScenario.DragWindow:
                StartWindowMovement(PageDistance, true);
                break;
            case TourDesktopScenario.JumpWindow:
                StartWindowMovement(PageDistance, false);
                break;
            case TourDesktopScenario.SwitchPage:
                StartPageMovement(PageDistance, PageTwoAccent);
                break;
            case TourDesktopScenario.JumpWindowToNumber:
                StartWindowMovement(PageDistance, false);
                break;
            case TourDesktopScenario.SwitchToNumber:
                StartPageMovement(PageDistance, PageTwoAccent);
                break;
        }

        animationsStarted = true;
    }


    private void ResetVisuals()
    {
        SetTranslation(PageWorld, Vector3.Zero);
        SetTranslation(MovingWindow, Vector3.Zero);
        SetTranslation(PointerGlyph, Vector3.Zero);
        SetTranslation(DestinationGlow, Vector3.Zero);
        SetScale(PageWorld, Vector3.One);
        SetOpacity(OverviewChrome, 1);
        SetOpacity(PageOneAccent, 1);
        SetOpacity(PageTwoAccent, 0);
        SetOpacity(PageThreeAccent, 0);
        SetOpacity(DestinationGlow, 0);
        RightPageTitle.Text = Scenario is TourDesktopScenario.JumpWindowToNumber or TourDesktopScenario.SwitchToNumber ? "Page 3" : "Page 2";
        PointerGlyph.Visibility = Visibility.Collapsed;
    }


    private void StartOverviewAnimation()
    {
        Visual pageVisual = GetVisual(PageWorld);
        pageVisual.CenterPoint = new(260, 112, 0);
        Compositor compositor = pageVisual.Compositor;
        CubicBezierEasingFunction easing = CreateEasing(compositor);
        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, new Vector3(0.96f, 0.96f, 1));
        scale.InsertKeyFrame(0.12f, new Vector3(0.96f, 0.96f, 1));
        scale.InsertKeyFrame(0.32f, Vector3.One, easing);
        scale.InsertKeyFrame(0.78f, Vector3.One);
        scale.InsertKeyFrame(0.94f, new Vector3(0.96f, 0.96f, 1), easing);
        scale.InsertKeyFrame(1, new Vector3(0.96f, 0.96f, 1));
        ConfigureLoop(scale);
        pageVisual.StartAnimation(nameof(Visual.Scale), scale);
        StartOpacityAnimation(OverviewChrome, 0, 0.12f, 0.78f, 0.94f, 0);
    }


    private void StartWindowMovement(float distance, bool showPointer)
    {
        StartTranslationAnimation(MovingWindow, new Vector3(distance, 0, 0));
        StartDestinationAnimation(distance);
        if (showPointer)
        {
            PointerGlyph.Visibility = Visibility.Visible;
            StartTranslationAnimation(PointerGlyph, new Vector3(distance, 0, 0));
            StartOpacityAnimation(PointerGlyph, 0, 0.12f, 0.76f, 0.9f, 0);
        }
    }


    private void StartPageMovement(float distance, UIElement destinationAccent)
    {
        StartTranslationAnimation(PageWorld, new Vector3(-distance, 0, 0));
        StartOpacityAnimation(PageOneAccent, 1, 0.24f, 0.68f, 0.88f, 1, true);
        StartOpacityAnimation(destinationAccent, 0, 0.24f, 0.68f, 0.88f, 0);
    }


    private void StartDestinationAnimation(float distance)
    {
        SetTranslation(DestinationGlow, new Vector3(distance - PageDistance, 0, 0));
        StartOpacityAnimation(DestinationGlow, 0, 0.2f, 0.7f, 0.88f, 0);
    }


    private void StartTranslationAnimation(UIElement element, Vector3 destination)
    {
        Visual visual = GetVisual(element);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = CreateEasing(compositor);
        Vector3KeyFrameAnimation movement = compositor.CreateVector3KeyFrameAnimation();
        movement.InsertKeyFrame(0, Vector3.Zero);
        movement.InsertKeyFrame(0.2f, Vector3.Zero);
        movement.InsertKeyFrame(0.42f, destination, easing);
        movement.InsertKeyFrame(0.72f, destination);
        movement.InsertKeyFrame(0.9f, Vector3.Zero, easing);
        movement.InsertKeyFrame(1, Vector3.Zero);
        ConfigureLoop(movement);
        visual.Properties.StartAnimation("Translation", movement);
    }


    private void StartOpacityAnimation(UIElement element, float initial, float revealAt, float holdUntil, float resetAt, float final, bool invert = false)
    {
        Visual visual = GetVisual(element);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = CreateEasing(compositor);
        float active = invert ? 0 : 1;
        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, initial);
        opacity.InsertKeyFrame(revealAt, initial);
        opacity.InsertKeyFrame(revealAt + 0.08f, active, easing);
        opacity.InsertKeyFrame(holdUntil, active);
        opacity.InsertKeyFrame(resetAt, final, easing);
        opacity.InsertKeyFrame(1, final);
        ConfigureLoop(opacity);
        visual.StartAnimation(nameof(Visual.Opacity), opacity);
    }


    private static Visual GetVisual(UIElement element)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        return ElementCompositionPreview.GetElementVisual(element);
    }


    private static void SetTranslation(UIElement element, Vector3 value)
    {
        Visual visual = GetVisual(element);
        visual.Properties.InsertVector3("Translation", value);
    }


    private static void SetScale(UIElement element, Vector3 value) => GetVisual(element).Scale = value;

    private static void SetOpacity(UIElement element, float value) => GetVisual(element).Opacity = value;

    private static void ConfigureLoop(KeyFrameAnimation animation)
    {
        animation.Duration = TimeSpan.FromMilliseconds(CycleDuration);
        animation.IterationBehavior = AnimationIterationBehavior.Forever;
        animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
    }


    private void StopScenario()
    {
        if (!animationsStarted)
        {
            return;
        }

        foreach (UIElement element in new UIElement[]
        {
            Scene,
            OverviewChrome,
            PageWorld,
            MovingWindow,
            PointerGlyph,
            PageOneAccent,
            PageTwoAccent,
            PageThreeAccent,
            DestinationGlow
        }

        )
        {
            Visual visual = GetVisual(element);
            visual.StopAnimation(nameof(Visual.Scale));
            visual.StopAnimation(nameof(Visual.Opacity));
        }

        GetVisual(PageWorld).Properties.StopAnimation("Translation");
        GetVisual(MovingWindow).Properties.StopAnimation("Translation");
        GetVisual(PointerGlyph).Properties.StopAnimation("Translation");
        GetVisual(DestinationGlow).Properties.StopAnimation("Translation");
        animationsStarted = false;
    }
}
