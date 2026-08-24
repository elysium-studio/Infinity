using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopPagePreview :
    Button,
    IDisposable
{
    private const float VisibleCornerRadius = 8;
    private const float ShadowDepth = 128;

    private readonly Border wallpaperHost;
    private readonly Border interactionLayer;
    private readonly CompositionRoundedRectangleGeometry clipGeometry;
    private readonly CompositionGeometricClip clip;
    private readonly ExpressionAnimation cornerRadiusExpression;
    private readonly Visual shadowVisual;
    private readonly Visual visual;
    private Vector3 translation;
    private bool disposed;

    public DesktopPagePreview(Visual scaleVisual, double overviewScale)
    {
        SolidColorBrush transparentBrush = new(Color.FromArgb(0, 0, 0, 0));
        ShadowHost = new Border
        {
            Background = FluentVisualResources.GetBrush("CardBackgroundFillColorDefaultBrush",
                Color.FromArgb(255, 32, 32, 32)),
            CornerRadius = new CornerRadius(VisibleCornerRadius / overviewScale),
            IsHitTestVisible = false,
            Shadow = new ThemeShadow()
        };
        Padding = new Thickness(0);
        Background = transparentBrush;
        BorderThickness = new Thickness(0);
        CornerRadius = new CornerRadius(0);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Resources["ButtonBackgroundPointerOver"] = transparentBrush;
        Resources["ButtonBackgroundPressed"] = transparentBrush;
        Resources["ButtonBackgroundDisabled"] = transparentBrush;
        Resources["ButtonBorderBrushPointerOver"] = transparentBrush;
        Resources["ButtonBorderBrushPressed"] = transparentBrush;
        Resources["ButtonBorderBrushDisabled"] = transparentBrush;

        wallpaperHost = new Border();
        interactionLayer = new Border
        {
            IsHitTestVisible = false
        };

        Grid content = new();
        content.Children.Add(wallpaperHost);
        content.Children.Add(interactionLayer);
        Content = content;

        PointerEntered += HandlePointerEntered;
        PointerExited += HandlePointerExited;
        PointerPressed += HandlePointerPressed;
        PointerReleased += HandlePointerReleased;
        ActualThemeChanged += HandleActualThemeChanged;
        ApplyInteractionState(false, false);

        ElementCompositionPreview.SetIsTranslationEnabled(this, true);
        visual = ElementCompositionPreview.GetElementVisual(this);
        Compositor compositor = visual.Compositor;
        visual.Properties.InsertVector3("Translation", Vector3.Zero);
        ElementCompositionPreview.SetIsTranslationEnabled(ShadowHost, true);
        ShadowHost.Translation = new Vector3(0, 0, ShadowDepth);
        shadowVisual = ElementCompositionPreview.GetElementVisual(ShadowHost);
        clipGeometry = compositor.CreateRoundedRectangleGeometry();
        clip = compositor.CreateGeometricClip(clipGeometry);
        cornerRadiusExpression = compositor.CreateExpressionAnimation(
            "Vector2(radius / scaleVisual.Scale.X, radius / scaleVisual.Scale.Y)");
        cornerRadiusExpression.SetScalarParameter("radius", VisibleCornerRadius);
        cornerRadiusExpression.SetReferenceParameter("scaleVisual", scaleVisual);
        clipGeometry.StartAnimation(nameof(CompositionRoundedRectangleGeometry.CornerRadius),
            cornerRadiusExpression);
        visual.Clip = clip;
    }

    public Border ShadowHost { get; }

    public int Page { get; private set; }

    public void Bind(int page,
        double width,
        double height,
        Brush background)
    {
        Page = page;
        Width = width;
        Height = height;
        ShadowHost.Width = width;
        ShadowHost.Height = height;
        wallpaperHost.Background = background;
        clipGeometry.Size = new Vector2(ToFloat(width), ToFloat(height));
        ShadowHost.Opacity = 1;
        IsHitTestVisible = true;
        Opacity = 1;
    }

    public void Hide()
    {
        ShadowHost.Opacity = 0;
        IsHitTestVisible = false;
        Opacity = 0;
        ApplyInteractionState(false, false);
    }

    public void Update(double translationX,
        TimeSpan? transitionDuration = null)
    {
        translation = new Vector3(ToFloat(translationX), 0, 0);

        if (!transitionDuration.HasValue)
        {
            ClearTranslationTransition();
            return;
        }

        Compositor compositor = visual.Compositor;
        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.1f, 0.9f),
            new Vector2(0.2f, 1));
        animation.Duration = transitionDuration.Value;
        animation.InsertExpressionKeyFrame(0, "this.StartingValue");
        animation.InsertKeyFrame(1, translation, easing);
        Vector3KeyFrameAnimation shadowAnimation = compositor.CreateVector3KeyFrameAnimation();
        shadowAnimation.Duration = transitionDuration.Value;
        shadowAnimation.InsertExpressionKeyFrame(0, "this.StartingValue");
        shadowAnimation.InsertKeyFrame(1,
            new Vector3(ToFloat(translationX), 0, ShadowDepth),
            easing);
        visual.Properties.StartAnimation("Translation", animation);
        shadowVisual.Properties.StartAnimation("Translation", shadowAnimation);
    }

    public void ClearTranslationTransition()
    {
        visual.Properties.StopAnimation("Translation");
        visual.Properties.InsertVector3("Translation", translation);
        shadowVisual.Properties.StopAnimation("Translation");
        shadowVisual.Properties.InsertVector3("Translation",
            new Vector3(translation.X, translation.Y, ShadowDepth));
    }

    public void SetInteractionEnabled(bool value)
    {
        IsHitTestVisible = value;

        if (!value)
        {
            ApplyInteractionState(false, false);
        }
    }

    public void Reset()
    {
        Page = 0;
        translation = Vector3.Zero;
        ClearTranslationTransition();
        Hide();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        visual.Properties.StopAnimation("Translation");
        shadowVisual.Properties.StopAnimation("Translation");
        clipGeometry.StopAnimation(nameof(CompositionRoundedRectangleGeometry.CornerRadius));
        visual.Clip = null;
        cornerRadiusExpression.Dispose();
        clip.Dispose();
        clipGeometry.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ApplyInteractionState(bool isHovered, bool isPressed)
    {
        interactionLayer.Background = FluentVisualResources.GetBrush(isPressed
            ? "SubtleFillColorTertiaryBrush"
            : isHovered
                ? "SubtleFillColorSecondaryBrush"
                : "SubtleFillColorTransparentBrush",
            isPressed
                ? Color.FromArgb(72, 255, 255, 255)
                : isHovered
                    ? Color.FromArgb(48, 255, 255, 255)
                    : Color.FromArgb(0, 255, 255, 255));
    }

    private void HandlePointerEntered(object sender, PointerRoutedEventArgs args) =>
        ApplyInteractionState(true, false);

    private void HandlePointerExited(object sender, PointerRoutedEventArgs args) =>
        ApplyInteractionState(false, false);

    private void HandlePointerPressed(object sender, PointerRoutedEventArgs args) =>
        ApplyInteractionState(true, true);

    private void HandlePointerReleased(object sender, PointerRoutedEventArgs args) =>
        ApplyInteractionState(true, false);

    private void HandleActualThemeChanged(FrameworkElement sender, object args) =>
        ApplyInteractionState(false, false);

    private static float ToFloat(double value) =>
        (float)Math.Clamp(value, -float.MaxValue, float.MaxValue);
}
