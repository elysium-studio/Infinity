using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Graphics;

namespace Infinity.UI.WinUI;

public partial class DesktopOverlay :
    ContentControl,
    IDisposable
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(object),
            typeof(DesktopOverlay), new PropertyMetadata(null, OnHeaderPropertyChanged));

    public static readonly DependencyProperty HeaderPlacementProperty =
        DependencyProperty.Register(nameof(HeaderPlacement), typeof(DesktopOverlayHeaderPlacement),
            typeof(DesktopOverlay), new PropertyMetadata(DesktopOverlayHeaderPlacement.Top, OnHeaderPlacementPropertyChanged));

    public static readonly DependencyProperty IsBlurEnabledProperty =
        DependencyProperty.Register(nameof(IsBlurEnabled), typeof(bool),
            typeof(DesktopOverlay), new PropertyMetadata(true, OnIsBlurEnabledPropertyChanged));

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool),
            typeof(DesktopOverlay), new PropertyMetadata(false, OnIsOpenPropertyChanged));

    public static readonly DependencyProperty IsInputEnabledProperty =
        DependencyProperty.Register(nameof(IsInputEnabled), typeof(bool),
            typeof(DesktopOverlay), new PropertyMetadata(false, OnIsInputEnabledPropertyChanged));

    public static readonly DependencyProperty IsMonitorSpanningEnabledProperty =
        DependencyProperty.Register(nameof(IsMonitorSpanningEnabled), typeof(bool),
            typeof(DesktopOverlay), new PropertyMetadata(false, OnIsMonitorSpanningEnabledPropertyChanged));

    public static readonly DependencyProperty StaysOpenProperty =
        DependencyProperty.Register(nameof(StaysOpen), typeof(bool),
            typeof(DesktopOverlay), new PropertyMetadata(false, OnStaysOpenPropertyChanged));

    private readonly DesktopOverlayHeader header;
    private readonly DesktopOverlayHost host;
    private bool disposed;

    // Safe for native input hooks: no XAML property or dispatcher access.
    protected bool IsEmergencyHidden => host.IsEmergencyHidden;

    public DesktopOverlay()
    {
        DefaultStyleKey = typeof(DesktopOverlay);
        host = new DesktopOverlayHost(this);
        host.Dismissed += HandleDismissed;
        header = new DesktopOverlayHeader();
    }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public DesktopOverlayHeaderPlacement HeaderPlacement
    {
        get => (DesktopOverlayHeaderPlacement)GetValue(HeaderPlacementProperty);
        set => SetValue(HeaderPlacementProperty, value);
    }

    public bool IsBlurEnabled
    {
        get => (bool)GetValue(IsBlurEnabledProperty);
        set => SetValue(IsBlurEnabledProperty, value);
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool IsInputEnabled
    {
        get => (bool)GetValue(IsInputEnabledProperty);
        set => SetValue(IsInputEnabledProperty, value);
    }

    public bool IsMonitorSpanningEnabled
    {
        get => (bool)GetValue(IsMonitorSpanningEnabledProperty);
        set => SetValue(IsMonitorSpanningEnabledProperty, value);
    }

    public bool StaysOpen
    {
        get => (bool)GetValue(StaysOpenProperty);
        set => SetValue(StaysOpenProperty, value);
    }

    public nint Handle => host.Handle;

    public RectInt32 ScreenBounds => host.ScreenBounds;

    public RectInt32 MonitorBounds => host.CurrentMonitorBounds;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        host.Dismissed -= HandleDismissed;
        host.Close();
        header.Close();
        GC.SuppressFinalize(this);
    }

    protected virtual void OnOpened()
    { }

    protected virtual void OnClosed()
    { }

    protected void PromoteTopMost()
    {
        host.SetTopMost(true);
        header.PromoteTopMost();
    }

    protected void SetTopMost(bool enabled) => host.SetTopMost(enabled);

    private static void OnHeaderPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is DesktopOverlay overlay)
        {
            overlay.OnHeaderPropertyChanged(args.NewValue);
        }
    }

    private static void OnHeaderPlacementPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is DesktopOverlay overlay)
        {
            overlay.header.SetPlacement((DesktopOverlayHeaderPlacement)args.NewValue);
        }
    }

    private static void OnIsBlurEnabledPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is DesktopOverlay overlay)
        {
            overlay.OnIsBlurEnabledPropertyChanged((bool)args.NewValue);
        }
    }

    private static void OnIsOpenPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is DesktopOverlay overlay)
        {
            overlay.OnIsOpenPropertyChanged((bool)args.NewValue);
        }
    }

    private static void OnIsInputEnabledPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is DesktopOverlay overlay)
        {
            overlay.host.SetInputEnabled((bool)args.NewValue);
        }
    }

    private static void OnIsMonitorSpanningEnabledPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is DesktopOverlay overlay)
        {
            overlay.host.SetMonitorSpanningEnabled((bool)args.NewValue);
        }
    }

    private static void OnStaysOpenPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is DesktopOverlay overlay)
        {
            overlay.host.SetStaysOpen((bool)args.NewValue);
        }
    }

    private void HandleDismissed(object? sender, EventArgs args)
    {
        if (IsOpen)
        {
            IsOpen = false;
        }
    }

    private void OnHeaderPropertyChanged(object? content)
    {
        header.SetContent(content);
    }

    private void OnIsBlurEnabledPropertyChanged(bool isBlurEnabled)
    {
        host.SetBlurEnabled(isBlurEnabled);
    }

    private void OnIsOpenPropertyChanged(bool isOpen)
    {
        if (isOpen)
        {
            host.Show();
            header.SetPlacement(HeaderPlacement);
            header.Show(host.CurrentMonitor);
            OnOpened();
        }
        else
        {
            host.Hide();
            header.Hide();
            OnClosed();
        }
    }
}
