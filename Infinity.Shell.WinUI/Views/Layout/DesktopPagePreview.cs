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

    private readonly Border shadowHost;
    private readonly ThemeShadow pageShadow;
    private readonly Border wallpaperHost;
    private readonly Border interactionLayer;
    private readonly CompositionRoundedRectangleGeometry clipGeometry;
    private readonly CompositionGeometricClip clip;
    private readonly ExpressionAnimation cornerRadiusExpression;
    private readonly Visual pageHostVisual;
    private readonly Visual pageVisual;
    private readonly Visual titleVisual;
    private Vector3 pageTranslation;
    private Vector3 titleTranslation;
    private bool interactionEnabled;
    private bool disposed;

    public DesktopPagePreview(Visual scaleVisual,
        double overviewScale,
        string editLabel,
        string saveLabel,
        string cancelLabel)
    {
        double visualScale = double.IsFinite(overviewScale) && overviewScale > 0 ? overviewScale : 1;
        SolidColorBrush transparentBrush = new(Color.FromArgb(0, 0, 0, 0));
        pageShadow = new ThemeShadow();
        shadowHost = new Border
        {
            Background = FluentVisualResources.GetBrush("CardBackgroundFillColorDefaultBrush",
                Color.FromArgb(255, 32, 32, 32)),
            CornerRadius = new CornerRadius(VisibleCornerRadius / visualScale),
            IsHitTestVisible = false
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
        TitleEditor = new DesktopPageTitleEditor(editLabel,
            saveLabel,
            cancelLabel);

        Grid content = new();
        content.Children.Add(wallpaperHost);
        content.Children.Add(interactionLayer);
        Content = content;

        PageHost = new Grid();
        PageHost.Children.Add(shadowHost);
        PageHost.Children.Add(this);

        PointerEntered += HandlePointerEntered;
        PointerExited += HandlePointerExited;
        PointerPressed += HandlePointerPressed;
        PointerReleased += HandlePointerReleased;
        ActualThemeChanged += HandleActualThemeChanged;
        ApplyInteractionState(false, false);

        ElementCompositionPreview.SetIsTranslationEnabled(PageHost, true);
        pageHostVisual = ElementCompositionPreview.GetElementVisual(PageHost);
        Compositor compositor = pageHostVisual.Compositor;
        pageHostVisual.Properties.InsertVector3("Translation", Vector3.Zero);
        ElementCompositionPreview.SetIsTranslationEnabled(TitleEditor, true);
        titleVisual = ElementCompositionPreview.GetElementVisual(TitleEditor);
        titleVisual.Properties.InsertVector3("Translation", Vector3.Zero);
        ElementCompositionPreview.SetIsTranslationEnabled(shadowHost, true);
        shadowHost.Translation = new Vector3(0, 0, ShadowDepth);
        pageVisual = ElementCompositionPreview.GetElementVisual(this);
        clipGeometry = compositor.CreateRoundedRectangleGeometry();
        clip = compositor.CreateGeometricClip(clipGeometry);
        cornerRadiusExpression = compositor.CreateExpressionAnimation(
            "Vector2(radius / scaleVisual.Scale.X, radius / scaleVisual.Scale.Y)");
        cornerRadiusExpression.SetScalarParameter("radius", VisibleCornerRadius);
        cornerRadiusExpression.SetReferenceParameter("scaleVisual", scaleVisual);
        clipGeometry.StartAnimation(nameof(CompositionRoundedRectangleGeometry.CornerRadius),
            cornerRadiusExpression);
        pageVisual.Clip = clip;
    }

    public Grid PageHost { get; }

    public DesktopPageTitleEditor TitleEditor { get; }

    public int Page { get; private set; }

    public void Bind(int page,
        double width,
        double height,
        Brush background,
        string title)
    {
        Page = page;
        PageHost.Width = width;
        PageHost.Height = height;
        Width = width;
        Height = height;
        shadowHost.Width = width;
        shadowHost.Height = height;
        wallpaperHost.Background = background;
        TitleEditor.ViewModel.Bind(page, title);
        clipGeometry.Size = new Vector2(ToFloat(width), ToFloat(height));
        PageHost.Opacity = 1;
        shadowHost.Opacity = 1;
        PageHost.IsHitTestVisible = interactionEnabled;
        IsHitTestVisible = interactionEnabled;
        Opacity = 1;
    }

    public void Hide()
    {
        PageHost.Opacity = 0;
        shadowHost.Opacity = 0;
        shadowHost.Shadow = null;
        TitleEditor.Hide();
        PageHost.IsHitTestVisible = false;
        IsHitTestVisible = false;
        Opacity = 0;
        ApplyInteractionState(false, false);
    }

    public void Update(double pageTranslationX,
        double titleTranslationX,
        TimeSpan? transitionDuration = null)
    {
        pageTranslation = new Vector3(ToFloat(pageTranslationX), 0, 0);
        titleTranslation = new Vector3(ToFloat(titleTranslationX), 0, 0);

        if (!transitionDuration.HasValue)
        {
            ClearTranslationTransition();
            return;
        }

        StartTranslationAnimation(pageHostVisual, pageTranslation, transitionDuration.Value);
        StartTranslationAnimation(titleVisual, titleTranslation, transitionDuration.Value);
    }

    public void ClearTranslationTransition()
    {
        pageHostVisual.Properties.StopAnimation("Translation");
        pageHostVisual.Properties.InsertVector3("Translation", pageTranslation);
        titleVisual.Properties.StopAnimation("Translation");
        titleVisual.Properties.InsertVector3("Translation", titleTranslation);
    }

    public void SetInteractionEnabled(bool value)
    {
        interactionEnabled = value;
        PageHost.IsHitTestVisible = value;
        IsHitTestVisible = value;

        if (value)
        {
            shadowHost.Shadow = pageShadow;
            TitleEditor.Show();
        }
        else
        {
            shadowHost.Shadow = null;
            TitleEditor.Hide();
        }

        if (!value)
        {
            ApplyInteractionState(false, false);
        }
    }

    public void Reset()
    {
        Page = 0;
        pageTranslation = Vector3.Zero;
        titleTranslation = Vector3.Zero;
        TitleEditor.ViewModel.Reset();
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
        pageHostVisual.Properties.StopAnimation("Translation");
        titleVisual.Properties.StopAnimation("Translation");
        clipGeometry.StopAnimation(nameof(CompositionRoundedRectangleGeometry.CornerRadius));
        pageVisual.Clip = null;
        shadowHost.Shadow = null;
        TitleEditor.Dispose();
        cornerRadiusExpression.Dispose();
        clip.Dispose();
        clipGeometry.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void StartTranslationAnimation(Visual visual,
        Vector3 target,
        TimeSpan duration)
    {
        Compositor compositor = visual.Compositor;
        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.1f, 0.9f),
            new Vector2(0.2f, 1));
        animation.Duration = duration;
        animation.InsertExpressionKeyFrame(0, "this.StartingValue");
        animation.InsertKeyFrame(1, target, easing);
        visual.Properties.StartAnimation("Translation", animation);
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
