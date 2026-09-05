using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI;

namespace Infinity.Shell.WinUI;

internal sealed class DesktopSnapZonePresenter : IDisposable
{
    private static readonly TimeSpan RevealDuration = TimeSpan.FromMilliseconds(167);
    private readonly List<Border> zones = [];
    private readonly DesktopSnapLayoutCatalog catalog;
    private readonly double visualScale;
    private readonly Visual visual;
    private DesktopSnapLayoutKind layout;
    private int highlightedSlot = -1;
    private double width;
    private double height;
    private bool isVisible;
    private bool disposed;

    public Canvas Host { get; } = new()
    {
        IsHitTestVisible = false
    };

    public DesktopSnapZonePresenter(DesktopSnapLayoutCatalog catalog, double scale)
    {
        this.catalog = catalog;
        visualScale = double.IsFinite(scale) && scale > 0 ? scale : 1;
        visual = ElementCompositionPreview.GetElementVisual(Host);
        visual.Opacity = 0;
    }


    public void Bind(double requestedWidth, double requestedHeight)
    {
        if (width == requestedWidth && height == requestedHeight)
        {
            return;
        }

        width = requestedWidth;
        height = requestedHeight;
        Host.Width = width;
        Host.Height = height;
        ArrangeZones();
    }


    public void Show(DesktopSnapLayoutKind requestedLayout, int highlightedSlot = -1, bool animate = true)
    {
        bool layoutChanged = layout != requestedLayout;
        if (layoutChanged)
        {
            layout = requestedLayout;
            CreateZones();
        }

        if (layoutChanged || this.highlightedSlot != highlightedSlot)
        {
            this.highlightedSlot = highlightedSlot;
            UpdateHighlight(highlightedSlot);
        }

        if (layout == DesktopSnapLayoutKind.None)
        {
            Hide(animate);
            return;
        }

        if (isVisible && animate)
        {
            return;
        }

        isVisible = true;
        AnimateOpacity(1, animate);
    }


    public void Hide(bool animate = true)
    {
        if (!isVisible && animate)
        {
            return;
        }

        isVisible = false;
        AnimateOpacity(0, animate);
    }


    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        visual.StopAnimation(nameof(Visual.Opacity));
        Host.Children.Clear();
        zones.Clear();
        GC.SuppressFinalize(this);
    }


    private void CreateZones()
    {
        Host.Children.Clear();
        zones.Clear();
        DesktopSnapLayoutDefinition? definition = catalog.Get(layout);
        if (definition is null)
        {
            return;
        }

        foreach (DesktopSnapSlot slot in definition.Slots)
        {
            Border zone = new()
            {
                BorderThickness = new(2 / visualScale),
                CornerRadius = new(8 / visualScale),
                IsHitTestVisible = false,
                Tag = slot
            };
            zones.Add(zone);
            Host.Children.Add(zone);
        }

        ArrangeZones();
    }


    private void ArrangeZones()
    {
        const double gap = DesktopSnapPlacementResolver.SlotGap;
        double halfGap = gap / 2;
        foreach (Border zone in zones)
        {
            if (zone.Tag is not DesktopSnapSlot slot)
            {
                continue;
            }

            zone.Width = Math.Max(0, (slot.Width * width) - gap);
            zone.Height = Math.Max(0, (slot.Height * height) - gap);
            Canvas.SetLeft(zone, (slot.X * width) + halfGap);
            Canvas.SetTop(zone, (slot.Y * height) + halfGap);
        }
    }


    private void UpdateHighlight(int highlightedSlot)
    {
        for (int index = 0; index < zones.Count; index++)
        {
            bool highlighted = index == highlightedSlot;
            zones[index].Background = highlighted ? FluentVisualResources.GetBrush("AccentFillColorTertiaryBrush", Color.FromArgb(96, 0, 120, 212)) : FluentVisualResources.GetBrush("SubtleFillColorSecondaryBrush", Color.FromArgb(40, 255, 255, 255));
            zones[index].BorderBrush = highlighted ? FluentVisualResources.GetBrush("AccentFillColorDefaultBrush", Color.FromArgb(255, 0, 120, 212)) : FluentVisualResources.GetBrush("SurfaceStrokeColorDefaultBrush", Color.FromArgb(96, 255, 255, 255));
        }
    }


    private void AnimateOpacity(float target, bool animate)
    {
        visual.StopAnimation(nameof(Visual.Opacity));
        if (!animate)
        {
            visual.Opacity = target;
            return;
        }

        Compositor compositor = visual.Compositor;
        ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
        CubicBezierEasingFunction easing = target > 0 ? compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1)) : compositor.CreateCubicBezierEasingFunction(new Vector2(0.55f, 0.55f), new Vector2(0, 1));
        animation.Duration = RevealDuration;
        animation.InsertExpressionKeyFrame(0, "this.StartingValue");
        animation.InsertKeyFrame(1, target, easing);
        visual.StartAnimation(nameof(Visual.Opacity), animation);
    }
}
