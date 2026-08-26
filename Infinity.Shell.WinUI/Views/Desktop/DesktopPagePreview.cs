using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopPagePreview :
    Button,
    IDisposable
{
    private const float VisibleCornerRadius = 8;
    private const float ShadowDepth = 64;
    private const double DragThreshold = 4;
    private const int DraggedZIndex = 1_000_000;

    private static readonly TimeSpan HoverAnimationDuration = TimeSpan.FromMilliseconds(167);

    private readonly Border shadowHost;
    private readonly ThemeShadow pageShadow;
    private readonly Border wallpaperHost;
    private readonly Border interactionLayer;
    private readonly double visualScale;
    private readonly CompositionRoundedRectangleGeometry clipGeometry;
    private readonly CompositionGeometricClip clip;
    private readonly ExpressionAnimation cornerRadiusExpression;
    private readonly Visual pageHostVisual;
    private readonly Visual pageVisual;
    private readonly Visual interactionVisual;
    private readonly Visual shadowVisual;
    private readonly Visual titleVisual;
    private Vector3 pageTranslation;
    private Vector3 shadowTranslation;
    private Vector3 titleTranslation;
    private UIElement? dragCaptureElement;
    private UIElement? dragCoordinateRoot;
    private uint? dragPointerId;
    private Point dragStartPoint;
    private bool isHovered;
    private bool isPressed;
    private bool isDragging;
    private bool interactionEnabled;
    private bool disposed;

    public DesktopPagePreview(Visual scaleVisual, double overviewScale, string editLabel, string saveLabel, string cancelLabel)
    {
        visualScale = double.IsFinite(overviewScale) && overviewScale > 0 ? overviewScale : 1;
        SolidColorBrush transparentBrush = new(Color.FromArgb(0, 0, 0, 0));
        pageShadow = new ThemeShadow();
        shadowHost = new Border
        {
            Background = FluentVisualResources.GetBrush("CardBackgroundFillColorDefaultBrush", Color.FromArgb(255, 32, 32, 32)),
            CornerRadius = new CornerRadius(VisibleCornerRadius),
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
            IsHitTestVisible = false,
            Opacity = 0
        };
        TitleEditor = new DesktopPageTitleEditor(editLabel, saveLabel, cancelLabel);

        Grid content = new();
        content.Children.Add(wallpaperHost);
        content.Children.Add(interactionLayer);
        Content = content;

        PageHost = new Grid();
        PageHost.Children.Add(this);

        PointerEntered += HandlePointerEntered;
        PointerExited += HandlePointerExited;
        PageHost.AddHandler(PointerPressedEvent, new PointerEventHandler(HandlePointerPressed), true);
        PageHost.AddHandler(PointerMovedEvent, new PointerEventHandler(HandlePointerMoved), true);
        PageHost.AddHandler(PointerReleasedEvent, new PointerEventHandler(HandlePointerReleased), true);
        PageHost.AddHandler(PointerCanceledEvent, new PointerEventHandler(HandlePointerCanceled), true);
        PageHost.AddHandler(PointerCaptureLostEvent, new PointerEventHandler(HandlePointerCaptureLost), true);

        TitleEditor.PointerPressed += HandlePointerPressed;
        TitleEditor.PointerMoved += HandlePointerMoved;
        TitleEditor.PointerReleased += HandlePointerReleased;
        TitleEditor.PointerCanceled += HandlePointerCanceled;
        TitleEditor.PointerCaptureLost += HandlePointerCaptureLost;

        ElementCompositionPreview.SetIsTranslationEnabled(PageHost, true);
        pageHostVisual = ElementCompositionPreview.GetElementVisual(PageHost);
        Compositor compositor = pageHostVisual.Compositor;
        pageHostVisual.Properties.InsertVector3("Translation", Vector3.Zero);

        ElementCompositionPreview.SetIsTranslationEnabled(shadowHost, true);
        shadowVisual = ElementCompositionPreview.GetElementVisual(shadowHost);
        shadowVisual.Properties.InsertVector3("Translation", Vector3.Zero);

        ElementCompositionPreview.SetIsTranslationEnabled(TitleEditor, true);
        titleVisual = ElementCompositionPreview.GetElementVisual(TitleEditor);
        titleVisual.Properties.InsertVector3("Translation", Vector3.Zero);

        shadowHost.Translation = new Vector3(0, 0, ShadowDepth);
        pageVisual = ElementCompositionPreview.GetElementVisual(this);
        interactionVisual = ElementCompositionPreview.GetElementVisual(interactionLayer);

        ActualThemeChanged += HandleActualThemeChanged;
        ApplyInteractionState(false, false);

        clipGeometry = compositor.CreateRoundedRectangleGeometry();
        clip = compositor.CreateGeometricClip(clipGeometry);
        cornerRadiusExpression = compositor.CreateExpressionAnimation("Vector2(radius / scaleVisual.Scale.X, radius / scaleVisual.Scale.Y)");
        cornerRadiusExpression.SetScalarParameter("radius", VisibleCornerRadius);
        cornerRadiusExpression.SetReferenceParameter("scaleVisual", scaleVisual);
        clipGeometry.StartAnimation(nameof(CompositionRoundedRectangleGeometry.CornerRadius), cornerRadiusExpression);
        pageVisual.Clip = clip;
    }

    public Grid PageHost { get; }

    public Border ShadowHost => shadowHost;

    public DesktopPageTitleEditor TitleEditor { get; }

    public int Page { get; private set; }

    public bool IsDragging => isDragging;

    public event Action<DesktopPagePreview>? DragStarted;

    public event Action<DesktopPagePreview, double, double>? DragMoved;

    public event Action<DesktopPagePreview>? DragCompleted;

    public event Action<DesktopPagePreview>? DragCanceled;

    public void Bind(int page, double width, double height, Brush background, string title)
    {
        Page = page;
        PageHost.Width = width;
        PageHost.Height = height;
        Width = width;
        Height = height;

        shadowHost.Width = width * visualScale;
        shadowHost.Height = height * visualScale;

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

    public void Update(double pageTranslationX, double titleTranslationX, TimeSpan? transitionDuration = null)
    {
        pageTranslation = new Vector3(ToFloat(pageTranslationX), 0, 0);
        shadowTranslation = new Vector3(ToFloat(titleTranslationX), 0, ShadowDepth);
        titleTranslation = new Vector3(ToFloat(titleTranslationX), 0, 0);

        if (!transitionDuration.HasValue)
        {
            ClearTranslationTransition();
            return;
        }

        StartTranslationAnimation(pageHostVisual, pageTranslation, transitionDuration.Value);
        StartTranslationAnimation(shadowVisual, shadowTranslation, transitionDuration.Value);
        StartTranslationAnimation(titleVisual, titleTranslation, transitionDuration.Value);
    }

    public void ClearTranslationTransition()
    {
        pageHostVisual.Properties.StopAnimation("Translation");
        pageHostVisual.Properties.InsertVector3("Translation", pageTranslation);

        shadowVisual.Properties.StopAnimation("Translation");
        shadowVisual.Properties.InsertVector3("Translation", shadowTranslation);

        titleVisual.Properties.StopAnimation("Translation");
        titleVisual.Properties.InsertVector3("Translation", titleTranslation);
    }

    public void SetInteractionEnabled(bool value)
    {
        interactionEnabled = value;

        if (!value)
        {
            CancelPointerOperation(true);
        }

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
        shadowTranslation = new Vector3(0, 0, ShadowDepth);
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
        shadowVisual.Properties.StopAnimation("Translation");
        titleVisual.Properties.StopAnimation("Translation");
        interactionVisual.StopAnimation(nameof(Visual.Opacity));

        CancelPointerOperation(false);

        PageHost.RemoveHandler(PointerPressedEvent, new PointerEventHandler(HandlePointerPressed));
        PageHost.RemoveHandler(PointerMovedEvent, new PointerEventHandler(HandlePointerMoved));
        PageHost.RemoveHandler(PointerReleasedEvent, new PointerEventHandler(HandlePointerReleased));
        PageHost.RemoveHandler(PointerCanceledEvent, new PointerEventHandler(HandlePointerCanceled));
        PageHost.RemoveHandler(PointerCaptureLostEvent, new PointerEventHandler(HandlePointerCaptureLost));

        TitleEditor.PointerPressed -= HandlePointerPressed;
        TitleEditor.PointerMoved -= HandlePointerMoved;
        TitleEditor.PointerReleased -= HandlePointerReleased;
        TitleEditor.PointerCanceled -= HandlePointerCanceled;
        TitleEditor.PointerCaptureLost -= HandlePointerCaptureLost;

        clipGeometry.StopAnimation(nameof(CompositionRoundedRectangleGeometry.CornerRadius));
        pageVisual.Clip = null;
        shadowHost.Shadow = null;

        TitleEditor.Dispose();
        cornerRadiusExpression.Dispose();
        clip.Dispose();
        clipGeometry.Dispose();

        GC.SuppressFinalize(this);
    }

    private static void StartTranslationAnimation(Visual visual, Vector3 target, TimeSpan duration)
    {
        Compositor compositor = visual.Compositor;
        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        CubicBezierEasingFunction easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1));

        animation.Duration = duration;
        animation.InsertExpressionKeyFrame(0, "this.StartingValue");
        animation.InsertKeyFrame(1, target, easing);

        visual.Properties.StartAnimation("Translation", animation);
    }

    private void ApplyInteractionState(bool hovered, bool pressed, bool animate = false)
    {
        isHovered = hovered;
        isPressed = pressed;
        float targetOpacity = hovered || pressed ? 1 : 0;

        if (targetOpacity > 0)
        {
            string resourceKey = pressed ? "SubtleFillColorTertiaryBrush" : "SubtleFillColorSecondaryBrush";
            Color fallbackColor = pressed ? Color.FromArgb(72, 255, 255, 255) : Color.FromArgb(48, 255, 255, 255);
            interactionLayer.Background = FluentVisualResources.GetBrush(resourceKey, fallbackColor);
        }

        if (!animate)
        {
            interactionVisual.StopAnimation(nameof(Visual.Opacity));
            interactionVisual.Opacity = targetOpacity;
            return;
        }

        Compositor compositor = interactionVisual.Compositor;
        ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
        CubicBezierEasingFunction easing = targetOpacity > 0 ? compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1)) : compositor.CreateCubicBezierEasingFunction(new Vector2(0.55f, 0.55f), new Vector2(0, 1));

        animation.Duration = HoverAnimationDuration;
        animation.InsertExpressionKeyFrame(0, "this.StartingValue");
        animation.InsertKeyFrame(1, targetOpacity, easing);
        interactionVisual.StartAnimation(nameof(Visual.Opacity), animation);
    }

    private void HandlePointerEntered(object sender, PointerRoutedEventArgs args) => ApplyInteractionState(true, false, true);

    private void HandlePointerExited(object sender, PointerRoutedEventArgs args) => ApplyInteractionState(false, false, true);

    private void HandlePointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (sender is not UIElement inputSource)
        {
            return;
        }

        if (ReferenceEquals(inputSource, TitleEditor) && IsTitleEditorControl(args.OriginalSource as DependencyObject))
        {
            return;
        }

        var point = args.GetCurrentPoint(inputSource);

        if (!interactionEnabled || !point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        dragCaptureElement = ReferenceEquals(inputSource, PageHost) ? this : inputSource;
        dragCoordinateRoot = inputSource.XamlRoot?.Content as UIElement ?? inputSource;
        dragPointerId = args.Pointer.PointerId;
        dragStartPoint = args.GetCurrentPoint(dragCoordinateRoot).Position;
        ApplyInteractionState(true, true, true);
    }

    private void HandlePointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId != args.Pointer.PointerId || dragCoordinateRoot is null)
        {
            return;
        }

        Point currentPoint = args.GetCurrentPoint(dragCoordinateRoot).Position;
        double horizontalDelta = currentPoint.X - dragStartPoint.X;

        if (!isDragging)
        {
            double verticalDelta = currentPoint.Y - dragStartPoint.Y;
            double distance = Math.Sqrt(horizontalDelta * horizontalDelta + verticalDelta * verticalDelta);

            if (distance < DragThreshold)
            {
                return;
            }

            bool alreadyCaptured = dragCaptureElement?.PointerCaptures.Any(pointer => pointer.PointerId == args.Pointer.PointerId) == true;

            if (dragCaptureElement is null || (!alreadyCaptured && !dragCaptureElement.CapturePointer(args.Pointer)))
            {
                ResetPointerOperation();
                return;
            }

            isDragging = true;

            Canvas.SetZIndex(PageHost, DraggedZIndex);
            Canvas.SetZIndex(ShadowHost, DraggedZIndex);
            Canvas.SetZIndex(TitleEditor, DraggedZIndex);

            DragStarted?.Invoke(this);
        }

        DragMoved?.Invoke(this, horizontalDelta, currentPoint.X);
        args.Handled = true;
    }

    private void HandlePointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId != args.Pointer.PointerId)
        {
            return;
        }

        bool completedDrag = isDragging;
        UIElement? captureElement = dragCaptureElement;

        if (completedDrag)
        {
            DragCompleted?.Invoke(this);
        }

        ResetPointerOperation();
        captureElement?.ReleasePointerCapture(args.Pointer);
        ApplyInteractionState(true, false, true);

        if (completedDrag)
        {
            args.Handled = true;
        }
    }

    private void HandlePointerCanceled(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId != args.Pointer.PointerId)
        {
            return;
        }

        UIElement? captureElement = dragCaptureElement;

        CancelPointerOperation(true);
        captureElement?.ReleasePointerCapture(args.Pointer);

        args.Handled = true;
    }

    private void HandlePointerCaptureLost(object sender, PointerRoutedEventArgs args)
    {
        if (dragPointerId == args.Pointer.PointerId && ReferenceEquals(args.OriginalSource, dragCaptureElement))
        {
            bool completedDrag = isDragging && !args.GetCurrentPoint(dragCoordinateRoot ?? this).Properties.IsLeftButtonPressed;

            if (completedDrag)
            {
                DragCompleted?.Invoke(this);

                ResetPointerOperation();
                ApplyInteractionState(false, false, true);
            }
            else
            {
                CancelPointerOperation(true);
            }
        }
    }

    private void CancelPointerOperation(bool notify)
    {
        bool canceledDrag = isDragging;
        UIElement? captureElement = dragCaptureElement;

        ResetPointerOperation();
        captureElement?.ReleasePointerCaptures();

        if (notify && canceledDrag)
        {
            DragCanceled?.Invoke(this);
        }
    }

    private void ResetPointerOperation()
    {
        dragPointerId = null;
        dragCaptureElement = null;
        dragCoordinateRoot = null;
        isDragging = false;

        Canvas.SetZIndex(PageHost, 0);
        Canvas.SetZIndex(ShadowHost, 0);
        Canvas.SetZIndex(TitleEditor, 0);
    }

    private bool IsTitleEditorControl(DependencyObject? source)
    {
        for (DependencyObject? element = source; element is not null && !ReferenceEquals(element, TitleEditor); element = VisualTreeHelper.GetParent(element))
        {
            if (element is Button or TextBox)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleActualThemeChanged(FrameworkElement sender, object args) => ApplyInteractionState(isHovered, isPressed);

    private static float ToFloat(double value) => (float)Math.Clamp(value, -float.MaxValue, float.MaxValue);
}
