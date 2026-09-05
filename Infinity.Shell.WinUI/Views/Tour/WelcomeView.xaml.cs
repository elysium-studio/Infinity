using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;

namespace Infinity.Shell.WinUI;

public sealed partial class WelcomeView : UserControl
{
    public WelcomeView()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }


    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        AnimateEntrance(HeroCard, 0);
        AnimateEntrance(HeadingGroup, 120);
        AnimateGlow(GlowOne, 0);
        AnimateGlow(GlowTwo, 1800);
    }


    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        foreach (UIElement element in new UIElement[]
        {
            HeroCard,
            HeadingGroup
        }

        )
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(element);
            visual.StopAnimation(nameof(Visual.Scale));
            visual.StopAnimation(nameof(Visual.Opacity));
            visual.Properties.StopAnimation("Translation");
        }

        ElementCompositionPreview.GetElementVisual(GlowOne).StopAnimation(nameof(Visual.Scale));
        ElementCompositionPreview.GetElementVisual(GlowTwo).StopAnimation(nameof(Visual.Scale));
    }


    private static void AnimateEntrance(UIElement element, int delay)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = TourAnimation.CreateEasing(compositor);
        visual.Properties.InsertVector3("Translation", new Vector3(0, 18, 0));
        visual.Opacity = 0;
        Vector3KeyFrameAnimation movement = compositor.CreateVector3KeyFrameAnimation();
        movement.InsertKeyFrame(0, new Vector3(0, 18, 0));
        movement.InsertKeyFrame(1, Vector3.Zero, easing);
        movement.Duration = TimeSpan.FromMilliseconds(520);
        movement.DelayTime = TimeSpan.FromMilliseconds(delay);
        movement.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        ScalarKeyFrameAnimation opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0);
        opacity.InsertKeyFrame(1, 1, easing);
        opacity.Duration = movement.Duration;
        opacity.DelayTime = movement.DelayTime;
        opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.Properties.StartAnimation("Translation", movement);
        visual.StartAnimation(nameof(Visual.Opacity), opacity);
    }


    private static void AnimateGlow(UIElement element, int delay)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new((float)(element.RenderSize.Width / 2), (float)(element.RenderSize.Height / 2), 0);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = TourAnimation.CreateEasing(compositor);
        Vector3KeyFrameAnimation scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0, Vector3.One);
        scale.InsertKeyFrame(0.5f, new Vector3(1.12f, 1.12f, 1), easing);
        scale.InsertKeyFrame(1, Vector3.One, easing);
        scale.Duration = TimeSpan.FromMilliseconds(8200);
        scale.DelayTime = TimeSpan.FromMilliseconds(delay);
        scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        scale.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation(nameof(Visual.Scale), scale);
    }
}
