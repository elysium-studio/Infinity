using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;

namespace Infinity.Shell.WinUI;

public sealed partial class TourKeyChord : UserControl
{
    private const float CycleDuration = 4400;
    public static readonly DependencyProperty FirstModifierProperty = DependencyProperty.Register(nameof(FirstModifier), typeof(string), typeof(TourKeyChord), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty SecondModifierProperty = DependencyProperty.Register(nameof(SecondModifier), typeof(string), typeof(TourKeyChord), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty ActionLabelProperty = DependencyProperty.Register(nameof(ActionLabel), typeof(string), typeof(TourKeyChord), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string), typeof(TourKeyChord), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty ShowShiftProperty = DependencyProperty.Register(nameof(ShowShift), typeof(bool), typeof(TourKeyChord), new PropertyMetadata(false));
    public static readonly DependencyProperty UseMouseWheelProperty = DependencyProperty.Register(nameof(UseMouseWheel), typeof(bool), typeof(TourKeyChord), new PropertyMetadata(false));

    public TourKeyChord()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }


    public string FirstModifier { get => (string)GetValue(FirstModifierProperty); set => SetValue(FirstModifierProperty, value); }

    public string SecondModifier { get => (string)GetValue(SecondModifierProperty); set => SetValue(SecondModifierProperty, value); }

    public string ActionLabel { get => (string)GetValue(ActionLabelProperty); set => SetValue(ActionLabelProperty, value); }

    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    public bool ShowShift { get => (bool)GetValue(ShowShiftProperty); set => SetValue(ShowShiftProperty, value); }

    public bool UseMouseWheel { get => (bool)GetValue(UseMouseWheelProperty); set => SetValue(UseMouseWheelProperty, value); }


    public Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ToInverseVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;


    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        StartKeyAnimation(FirstKey, FirstAccent, 0.03f, 0.72f);
        StartKeyAnimation(SecondKey, SecondAccent, 0.08f, 0.72f);
        if (ShowShift)
        {
            StartKeyAnimation(ShiftKey, ShiftAccent, 0.13f, 0.72f);
        }

        if (UseMouseWheel)
        {
            StartKeyAnimation(MouseKey, MouseAccent, 0.23f, 0.6f);
            StartWheelAnimation();
        }
        else
        {
            StartKeyAnimation(ActionKey, ActionAccent, 0.2f, 0.42f);
        }
    }


    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        StopAnimations(FirstKey, FirstAccent, SecondKey, SecondAccent, ShiftKey, ShiftAccent, ActionKey, ActionAccent, MouseKey, MouseAccent, MouseWheel);
        if (UseMouseWheel)
        {
            ElementCompositionPreview.GetElementVisual(MouseWheel).Properties.StopAnimation("Translation");
        }
    }


    private static void StartKeyAnimation(UIElement key, UIElement accent, float pressAt, float releaseAt)
    {
        Visual keyVisual = ElementCompositionPreview.GetElementVisual(key);
        keyVisual.CenterPoint = new((float)(key.RenderSize.Width / 2), (float)(key.RenderSize.Height / 2), 0);
        Compositor compositor = keyVisual.Compositor;
        CubicBezierEasingFunction easing = TourAnimation.CreateEasing(compositor);
        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, Vector3.One);
        scale.InsertKeyFrame(pressAt, Vector3.One);
        scale.InsertKeyFrame(pressAt + 0.04f, new Vector3(0.92f, 0.92f, 1), easing);
        scale.InsertKeyFrame(releaseAt, new Vector3(0.92f, 0.92f, 1));
        scale.InsertKeyFrame(releaseAt + 0.06f, Vector3.One, easing);
        scale.InsertKeyFrame(1, Vector3.One);
        scale.Duration = TimeSpan.FromMilliseconds(CycleDuration);
        scale.IterationBehavior = AnimationIterationBehavior.Forever;
        keyVisual.StartAnimation(nameof(Visual.Scale), scale);
        Visual accentVisual = ElementCompositionPreview.GetElementVisual(accent);
        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0);
        opacity.InsertKeyFrame(pressAt, 0);
        opacity.InsertKeyFrame(pressAt + 0.04f, 0.72f, easing);
        opacity.InsertKeyFrame(releaseAt, 0.72f);
        opacity.InsertKeyFrame(releaseAt + 0.06f, 0, easing);
        opacity.InsertKeyFrame(1, 0);
        opacity.Duration = scale.Duration;
        opacity.IterationBehavior = AnimationIterationBehavior.Forever;
        accentVisual.StartAnimation(nameof(Visual.Opacity), opacity);
    }


    private void StartWheelAnimation()
    {
        ElementCompositionPreview.SetIsTranslationEnabled(MouseWheel, true);
        Visual visual = ElementCompositionPreview.GetElementVisual(MouseWheel);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = TourAnimation.CreateEasing(compositor);
        Vector3KeyFrameAnimation movement = compositor.CreateVector3KeyFrameAnimation();
        movement.InsertKeyFrame(0, Vector3.Zero);
        movement.InsertKeyFrame(0.24f, Vector3.Zero);
        movement.InsertKeyFrame(0.3f, new Vector3(0, 8, 0), easing);
        movement.InsertKeyFrame(0.36f, Vector3.Zero, easing);
        movement.InsertKeyFrame(0.42f, new Vector3(0, 8, 0), easing);
        movement.InsertKeyFrame(0.48f, Vector3.Zero, easing);
        movement.InsertKeyFrame(1, Vector3.Zero);
        movement.Duration = TimeSpan.FromMilliseconds(CycleDuration);
        movement.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.Properties.StartAnimation("Translation", movement);
    }


    private static void StopAnimations(params UIElement[] elements)
    {
        foreach (UIElement element in elements)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(element);
            visual.StopAnimation(nameof(Visual.Scale));
            visual.StopAnimation(nameof(Visual.Opacity));
        }
    }
}
