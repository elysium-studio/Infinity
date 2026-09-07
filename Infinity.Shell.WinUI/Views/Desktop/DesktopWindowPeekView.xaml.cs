using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.ViewManagement;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopWindowPeekView : UserControl
{
    private readonly DesktopWindowPlacementAnimator animator;
    private readonly UISettings settings = new();
    private DesktopWindowPeekContent? content;
    private DesktopWindowPeekBounds sourceBounds;
    private DesktopWindowPeekBounds currentBounds;
    private CornerRadius sourceCornerRadius;
    private bool closing;
    private bool isAltClick;

    public DesktopWindowPeekView()
    {
        InitializeComponent();
        animator = new(PreviewFrame) { EasingControlPoint1 = new(0.55f, 0.55f), EasingControlPoint2 = new(0, 1) };
        SizeChanged += HandleSizeChanged;
    }

    public event Action? Dismissed;
    public event Action? Invoked;

    public void Begin(Border thumbnail, DesktopWindowPeekBounds source)
    {
        Hide();
        if (!source.IsValid)
        {
            throw new ArgumentException("The thumbnail must have visible bounds", nameof(source));
        }

        sourceBounds = source;
        sourceCornerRadius = thumbnail.CornerRadius;
        content = new(thumbnail, PeekContent);
        Visibility = Visibility.Visible;
        UpdateSize();
        animator.AnimationDuration = TimeSpan.FromMilliseconds(250);
        animator.Start(ToAnimationBounds(sourceBounds.Fit(currentBounds)), ToAnimationBounds(currentBounds), 0);
        AnimateDimmer(0, 1);
    }

    public void Hide()
    {
        animator.Stop();
        ElementCompositionPreview.GetElementVisual(Dimmer).StopAnimation(nameof(Visual.Opacity));
        Visibility = Visibility.Collapsed;
        content?.Dispose();
        content = null;
        currentBounds = default;
        closing = false;
        isAltClick = false;
        PreviewFrame.Scale = Vector3.One;
        PreviewFrame.Translation = Vector3.Zero;
    }

    public bool MatchesSourceSize(Border thumbnail) => content is not null && content.Width == thumbnail.Width && content.Height == thumbnail.Height;

    public void AnimateOut(DesktopWindowPeekBounds destination, Action completed)
    {
        if (closing)
        {
            return;
        }

        if (content is null || Visibility != Visibility.Visible || !currentBounds.IsValid)
        {
            completed();
            return;
        }

        closing = true;
        DesktopWindowPlacementAnimator.Bounds from = animator.Capture(ToAnimationBounds(currentBounds));
        DesktopWindowPeekBounds target = (destination.IsValid ? destination : sourceBounds).Fit(currentBounds);
        ApplyBounds(target);
        animator.AnimationDuration = TimeSpan.FromMilliseconds(167);
        animator.Start(from, ToAnimationBounds(target), 0, completed);
        AnimateDimmer(null, 0);
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args) => UpdateSize();

    private void UpdateSize()
    {
        if (closing || content is null)
        {
            return;
        }

        double availableWidth = ActualWidth > 0 ? ActualWidth : XamlRoot?.Size.Width ?? (double.IsFinite(Width) ? Width : 640);
        double availableHeight = ActualHeight > 0 ? ActualHeight : XamlRoot?.Size.Height ?? (double.IsFinite(Height) ? Height : 400);
        DesktopWindowPeekBounds target = DesktopWindowPeekBounds.Center(content.Width, content.Height, availableWidth, availableHeight);
        if (target.IsValid && currentBounds != target)
        {
            animator.Stop();
            ApplyBounds(target);
        }
    }

    private void ApplyBounds(DesktopWindowPeekBounds bounds)
    {
        currentBounds = bounds;
        PreviewFrame.Width = bounds.Width;
        PreviewFrame.Height = bounds.Height;
        PreviewFrame.CenterPoint = new((float)bounds.Width / 2, (float)bounds.Height / 2, 0);
        PreviewFrame.Scale = Vector3.One;
        PreviewFrame.Translation = new((float)bounds.X, (float)bounds.Y, 0);
        double scale = content is not null ? bounds.Width / content.Width : 1;
        PreviewFrame.CornerRadius = new(sourceCornerRadius.TopLeft * scale, sourceCornerRadius.TopRight * scale, sourceCornerRadius.BottomRight * scale, sourceCornerRadius.BottomLeft * scale);
    }

    private void AnimateDimmer(float? from, float to)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(Dimmer);
        if (!settings.AnimationsEnabled || Visibility != Visibility.Visible)
        {
            visual.StopAnimation(nameof(Visual.Opacity));
            visual.Opacity = to;
            return;
        }

        using ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        using LinearEasingFunction easing = visual.Compositor.CreateLinearEasingFunction();
        animation.Duration = TimeSpan.FromMilliseconds(83);
        if (from.HasValue)
        {
            animation.InsertKeyFrame(0, from.Value);
        }
        else
        {
            animation.InsertExpressionKeyFrame(0, "this.StartingValue");
        }
        animation.InsertKeyFrame(1, to, easing);
        visual.StartAnimation(nameof(Visual.Opacity), animation);
    }

    private static DesktopWindowPlacementAnimator.Bounds ToAnimationBounds(DesktopWindowPeekBounds bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private void HandlePointerWheelChanged(object sender, PointerRoutedEventArgs args) => args.Handled = true;

    private void HandleBackgroundTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;
        Dismissed?.Invoke();
    }

    private void HandlePreviewTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;
        if (isAltClick)
        {
            isAltClick = false;
            Dismissed?.Invoke();
        }
        else
        {
            Invoked?.Invoke();
        }
    }

    private void HandlePreviewPointerPressed(object sender, PointerRoutedEventArgs args) => isAltClick = args.KeyModifiers == VirtualKeyModifiers.Menu;
}
