using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Composition;
using Microsoft.Graphics.DirectX;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public partial class WelcomeView :
    UserControl
{
    private const float BlobCycleDuration = 9000f;
    private const float IconEntranceDuration = 600f;
    private const float ShimmerCycleDuration = 6000f;
    private const float ShimmerSweepFraction = 0.15f;
    private const float TextEntranceDuration = 500f;
    private const float TextEntranceOffset = 16f;
    private const float TextStaggerStep = 120f;

    public WelcomeView()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
    }

    private static CubicBezierEasingFunction CreateEntranceEasing(Compositor compositor) =>
        compositor.CreateCubicBezierEasingFunction(new Vector2(0.17f, 0.67f), new Vector2(0.32f, 1.4f));

    private static CubicBezierEasingFunction CreateSettleEasing(Compositor compositor) =>
        compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));

    private static Visual SetupVisual(UIElement element)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3((float)(element.RenderSize.Width / 2), (float)(element.RenderSize.Height / 2), 0f);
        return visual;
    }

    private void AnimateGlowBlob(UIElement element, float phaseOffset)
    {
        Visual visual = SetupVisual(element);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = CreateSettleEasing(compositor);
        float duration = BlobCycleDuration;

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.InsertKeyFrame(0f, Vector3.One, easing);
        scaleAnimation.InsertKeyFrame(0.5f, new Vector3(1.12f, 1.12f, 1f), easing);
        scaleAnimation.InsertKeyFrame(1f, Vector3.One, easing);
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        scaleAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        scaleAnimation.DelayTime = TimeSpan.FromMilliseconds(phaseOffset);
        scaleAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.InsertKeyFrame(0f, 0.3f, easing);
        opacityAnimation.InsertKeyFrame(0.5f, 0.55f, easing);
        opacityAnimation.InsertKeyFrame(1f, 0.3f, easing);
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        opacityAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        opacityAnimation.DelayTime = TimeSpan.FromMilliseconds(phaseOffset);
        opacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Scale", scaleAnimation);
        visual.StartAnimation("Opacity", opacityAnimation);
    }

    private void AnimateIconEntrance()
    {
        Visual visual = SetupVisual(LogoImage);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = CreateEntranceEasing(compositor);

        visual.Scale = new Vector3(0.4f, 0.4f, 1f);
        visual.Opacity = 0f;

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.InsertKeyFrame(0f, new Vector3(0.4f, 0.4f, 1f));
        scaleAnimation.InsertKeyFrame(1f, Vector3.One, easing);
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(IconEntranceDuration);

        ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.InsertKeyFrame(0f, 0f);
        opacityAnimation.InsertKeyFrame(1f, 1f, easing);
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(IconEntranceDuration);

        visual.StartAnimation("Scale", scaleAnimation);
        visual.StartAnimation("Opacity", opacityAnimation);
    }

    private void AnimateShimmerLoop()
    {
        Visual hostVisual = ElementCompositionPreview.GetElementVisual(ShimmerHost);
        Compositor compositor = hostVisual.Compositor;

        Size surfaceSize = new Size(ShimmerHost.ActualWidth, ShimmerHost.ActualHeight);

        LoadedImageSurface maskSurface = LoadedImageSurface.StartLoadFromUri(new Uri("ms-appx:///Assets/Infinity.png"));
        CompositionSurfaceBrush maskBrush = compositor.CreateSurfaceBrush(maskSurface);

        CanvasDevice canvasDevice = CanvasDevice.GetSharedDevice();
        CompositionGraphicsDevice graphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(compositor, canvasDevice);
        CompositionDrawingSurface gradientSurface = graphicsDevice.CreateDrawingSurface(
            surfaceSize,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            DirectXAlphaMode.Premultiplied);

        using (CanvasDrawingSession drawingSession = CanvasComposition.CreateDrawingSession(gradientSurface))
        {
            drawingSession.Clear(Colors.Transparent);

            CanvasLinearGradientBrush gradientCanvasBrush = new(canvasDevice,
                [
                    new CanvasGradientStop { Position = 0.0f, Color = Colors.Transparent },
                    new CanvasGradientStop { Position = 0.42f, Color = Colors.Transparent },
                    new CanvasGradientStop { Position = 0.5f, Color = Color.FromArgb(120, 255, 255, 255) },
                    new CanvasGradientStop { Position = 0.58f, Color = Colors.Transparent },
                    new CanvasGradientStop { Position = 1.0f, Color = Colors.Transparent }
                ])
            {
                StartPoint = new Vector2(0f, 0f),
                EndPoint = new Vector2((float)surfaceSize.Width, (float)surfaceSize.Height)
            };

            drawingSession.FillRectangle(new Rect(0, 0, surfaceSize.Width, surfaceSize.Height), gradientCanvasBrush);
        }

        CompositionSurfaceBrush gradientSurfaceBrush = compositor.CreateSurfaceBrush(gradientSurface);
        gradientSurfaceBrush.Stretch = CompositionStretch.None;

        AlphaMaskEffect maskEffect = new AlphaMaskEffect
        {
            Source = new CompositionEffectSourceParameter("Gradient"),
            AlphaMask = new CompositionEffectSourceParameter("Mask")
        };

        CompositionEffectFactory effectFactory = compositor.CreateEffectFactory(maskEffect);
        CompositionEffectBrush effectBrush = effectFactory.CreateBrush();
        effectBrush.SetSourceParameter("Gradient", gradientSurfaceBrush);
        effectBrush.SetSourceParameter("Mask", maskBrush);

        SpriteVisual shimmerVisual = compositor.CreateSpriteVisual();
        shimmerVisual.Brush = effectBrush;
        shimmerVisual.Size = new Vector2((float)surfaceSize.Width, (float)surfaceSize.Height);

        ElementCompositionPreview.SetElementChildVisual(ShimmerHost, shimmerVisual);

        CubicBezierEasingFunction easing = CreateSettleEasing(compositor);
        float duration = ShimmerCycleDuration;
        float startDelay = IconEntranceDuration + 300f;
        float sweepRange = (float)(surfaceSize.Width + surfaceSize.Height) / 2f;

        Vector2KeyFrameAnimation offsetAnimation = compositor.CreateVector2KeyFrameAnimation();
        offsetAnimation.InsertKeyFrame(0f, new Vector2(-sweepRange, -sweepRange));
        offsetAnimation.InsertKeyFrame(ShimmerSweepFraction, new Vector2(sweepRange, sweepRange), easing);
        offsetAnimation.InsertKeyFrame(1f, new Vector2(sweepRange, sweepRange));
        offsetAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        offsetAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
        offsetAnimation.DelayTime = TimeSpan.FromMilliseconds(startDelay);
        offsetAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        gradientSurfaceBrush.StartAnimation("Offset", offsetAnimation);
    }

    private void AnimateTextEntrance(UIElement element, float delay)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);

        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction easing = CreateSettleEasing(compositor);

        visual.Properties.InsertVector3("Translation", new Vector3(0f, TextEntranceOffset, 0f));
        visual.Opacity = 0f;

        Vector3KeyFrameAnimation translateAnimation = compositor.CreateVector3KeyFrameAnimation();
        translateAnimation.InsertKeyFrame(0f, new Vector3(0f, TextEntranceOffset, 0f));
        translateAnimation.InsertKeyFrame(1f, Vector3.Zero, easing);
        translateAnimation.Duration = TimeSpan.FromMilliseconds(TextEntranceDuration);
        translateAnimation.DelayTime = TimeSpan.FromMilliseconds(delay);
        translateAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.InsertKeyFrame(0f, 0f);
        opacityAnimation.InsertKeyFrame(1f, 1f, easing);
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(TextEntranceDuration);
        opacityAnimation.DelayTime = TimeSpan.FromMilliseconds(delay);
        opacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Translation", translateAnimation);
        visual.StartAnimation("Opacity", opacityAnimation);
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        AnimateGlowBlob(GlowBlobOne, 0f);
        AnimateGlowBlob(GlowBlobTwo, 2400f);
        AnimateIconEntrance();
        AnimateShimmerLoop();
        AnimateTextEntrance(HeadingText, IconEntranceDuration * 0.6f);
        AnimateTextEntrance(BodyText, (IconEntranceDuration * 0.6f) + TextStaggerStep);
    }
}