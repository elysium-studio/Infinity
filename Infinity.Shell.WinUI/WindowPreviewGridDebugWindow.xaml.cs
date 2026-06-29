using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Graphics;
using WinRT.Interop;

namespace Infinity.Shell.WinUI;

public partial class WindowPreviewGridDebugWindow :
    Window
{
    private const int MaxPreviewCount = 80;
    private const double Gap = 10.0;
    private const int DwmwaCloaked = 14;

    private readonly DwmWindowPreviewSurface previewSurface = new();
    private readonly List<WindowPreviewItem> items = [];

    private bool isInitialized;
    private bool isRendering;
    private int candidateCount;

    public WindowPreviewGridDebugWindow()
    {
        InitializeComponent();

        Root.Loaded += HandleRootLoaded;
        Root.SizeChanged += HandleRootSizeChanged;
        Closed += HandleClosed;

        AppWindow.Resize(new SizeInt32(1200, 800));
    }

    private void HandleRootLoaded(object sender, RoutedEventArgs args)
    {
        if (isInitialized)
        {
            return;
        }

        isInitialized = true;

        RenderWindows();
    }

    private void HandleRootSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (!isInitialized || isRendering)
        {
            return;
        }

        RenderWindows();
    }

    private void RenderWindows()
    {
        isRendering = true;

        try
        {
            ClearCurrentWindows();

            nint ownerWindowHandle = WindowNative.GetWindowHandle(this);

            previewSurface.Initialize(ownerWindowHandle);

            if (!previewSurface.IsAvailable)
            {
                UpdateStatus("Not available");
                return;
            }

            double availableWidth = ItemsHost.ActualWidth;
            double availableHeight = ItemsHost.ActualHeight;

            if (availableWidth <= 0.0 || availableHeight <= 0.0)
            {
                UpdateStatus("No size");
                return;
            }

            List<WindowCandidate> candidates = GetWindowCandidates(ownerWindowHandle);
            candidateCount = candidates.Count;

            if (candidates.Count == 0)
            {
                UpdateStatus("No windows");
                return;
            }

            int columns = (int)Math.Ceiling(Math.Sqrt(candidates.Count));
            int rows = (int)Math.Ceiling((double)candidates.Count / columns);

            double cellWidth = Math.Max(1.0, (availableWidth - (Gap * (columns + 1))) / columns);
            double cellHeight = Math.Max(1.0, (availableHeight - (Gap * (rows + 1))) / rows);

            for (int index = 0; index < candidates.Count; index++)
            {
                WindowCandidate candidate = candidates[index];

                int column = index % columns;
                int row = index / columns;

                double cellX = Gap + (column * (cellWidth + Gap));
                double cellY = Gap + (row * (cellHeight + Gap));

                PreviewPlacement placement = GetPlacement(candidate, cellX, cellY, cellWidth, cellHeight);

                WindowPreviewItem? item = CreateWindowPreviewItem(ownerWindowHandle, candidate, placement, index);

                if (item is null)
                {
                    continue;
                }

                items.Add(item);
            }

            previewSurface.Render();
            UpdateStatus("Rendered");
        }
        finally
        {
            isRendering = false;
        }
    }

    private WindowPreviewItem? CreateWindowPreviewItem(nint ownerWindowHandle, WindowCandidate candidate, PreviewPlacement placement, int index)
    {
        IWindowPreview? preview = previewSurface.CreatePreview(candidate.WindowHandle);

        if (preview is null)
        {
            return null;
        }

        Border itemBorder = new()
        {
            Width = placement.Width,
            Height = placement.Height,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.DeepSkyBlue),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(30, 0, 0, 0)),
            IsHitTestVisible = false
        };

        Grid itemGrid = new();

        Border thumbnailHost = new()
        {
            Width = placement.Width,
            Height = placement.Height,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            IsHitTestVisible = false
        };

        Border labelBackground = new()
        {
            Padding = new Thickness(6, 3, 6, 3),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(210, 0, 0, 0)),
            IsHitTestVisible = false
        };

        TextBlock titleText = new()
        {
            Text = $"{index}: {candidate.Title}",
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 12
        };

        labelBackground.Child = titleText;

        itemGrid.Children.Add(thumbnailHost);
        itemGrid.Children.Add(labelBackground);

        itemBorder.Child = itemGrid;

        Canvas.SetLeft(itemBorder, placement.X);
        Canvas.SetTop(itemBorder, placement.Y);

        ItemsHost.Children.Add(itemBorder);

        SystemVisualProxyVisualPrivate? thumbnailProxy = CreateThumbnailProxy(thumbnailHost, placement.Width, placement.Height);

        if (thumbnailProxy is null)
        {
            preview.Dispose();
            ItemsHost.Children.Remove(itemBorder);
            return null;
        }

        preview.SetTarget(thumbnailProxy.Handle, placement.Width, placement.Height, true);

        return new WindowPreviewItem(candidate, preview, itemBorder, thumbnailHost, thumbnailProxy);
    }

    private static SystemVisualProxyVisualPrivate? CreateThumbnailProxy(Border thumbnailHost, double width, double height)
    {
        try
        {
            Visual elementVisual = ElementCompositionPreview.GetElementVisual(thumbnailHost);
            Compositor compositor = elementVisual.Compositor;

            SystemVisualProxyVisualPrivate thumbnailProxy = SystemVisualProxyVisualPrivate.Create(compositor);
            Visual thumbnailProxyVisual = thumbnailProxy.Visual;

            thumbnailProxyVisual.Offset = new Vector3(0.0f, 0.0f, 0.0f);
            thumbnailProxyVisual.Size = new Vector2((float)Math.Max(1.0, width), (float)Math.Max(1.0, height));
            thumbnailProxyVisual.Scale = new Vector3(1.0f, 1.0f, 1.0f);
            thumbnailProxyVisual.Clip = compositor.CreateInsetClip();

            ElementCompositionPreview.SetElementChildVisual(thumbnailHost, thumbnailProxyVisual);

            return thumbnailProxy;
        }
        catch
        {
            return null;
        }
    }

    private static PreviewPlacement GetPlacement(WindowCandidate candidate, double cellX, double cellY, double cellWidth, double cellHeight)
    {
        double scale = Math.Min(cellWidth / candidate.Width, cellHeight / candidate.Height);
        double width = Math.Max(1.0, candidate.Width * scale);
        double height = Math.Max(1.0, candidate.Height * scale);
        double x = cellX + ((cellWidth - width) / 2.0);
        double y = cellY + ((cellHeight - height) / 2.0);

        return new PreviewPlacement(x, y, width, height);
    }

    private void UpdateStatus(string stage)
    {
        StatusText.Text = $"{stage} | Available: {previewSurface.IsAvailable} | Candidates: {candidateCount} | Rendered: {items.Count} | HRESULT: 0x{previewSurface.LastHResult:X8} | Bridge: 0x{previewSurface.LastBridgeHResult:X8}";
    }

    private void ClearCurrentWindows()
    {
        foreach (WindowPreviewItem item in items)
        {
            item.Dispose();
        }

        items.Clear();
        ItemsHost.Children.Clear();

        previewSurface.Clear();
        candidateCount = 0;
    }

    private void HandleClosed(object sender, WindowEventArgs args)
    {
        ClearCurrentWindows();

        previewSurface.Dispose();
    }

    private static List<WindowCandidate> GetWindowCandidates(nint ownerWindowHandle)
    {
        List<WindowCandidate> candidates = [];

        EnumWindows((windowHandle, parameter) =>
        {
            if (candidates.Count >= MaxPreviewCount)
            {
                return false;
            }

            if (!TryCreateWindowCandidate(windowHandle, ownerWindowHandle, out WindowCandidate? candidate))
            {
                return true;
            }

            candidates.Add(candidate);
            return true;
        }, 0);

        return candidates;
    }

    private static bool TryCreateWindowCandidate(nint windowHandle, nint ownerWindowHandle, out WindowCandidate? candidate)
    {
        candidate = null;

        if (windowHandle == 0 || windowHandle == ownerWindowHandle)
        {
            return false;
        }

        if (windowHandle == GetShellWindow())
        {
            return false;
        }

        if (!IsWindowVisible(windowHandle))
        {
            return false;
        }

        if (IsWindowCloaked(windowHandle))
        {
            return false;
        }

        if (!GetWindowRect(windowHandle, out RECT bounds))
        {
            return false;
        }

        int width = bounds.Right - bounds.Left;
        int height = bounds.Bottom - bounds.Top;

        if (width <= 1 || height <= 1)
        {
            return false;
        }

        string title = GetWindowTitle(windowHandle);

        if (string.IsNullOrWhiteSpace(title))
        {
            title = $"HWND 0x{windowHandle:X}";
        }

        candidate = new WindowCandidate(windowHandle, title, width, height);
        return true;
    }

    private static string GetWindowTitle(nint windowHandle)
    {
        int length = GetWindowTextLength(windowHandle);

        if (length <= 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new(length + 1);
        _ = GetWindowText(windowHandle, builder, builder.Capacity);

        return builder.ToString();
    }

    private static bool IsWindowCloaked(nint windowHandle)
    {
        int cloaked = 0;
        int result = DwmGetWindowAttribute(windowHandle, DwmwaCloaked, ref cloaked, Marshal.SizeOf<int>());

        return result == 0 && cloaked != 0;
    }

    private sealed class WindowCandidate
    {
        public WindowCandidate(nint windowHandle, string title, int width, int height)
        {
            WindowHandle = windowHandle;
            Title = title;
            Width = width;
            Height = height;
        }

        public nint WindowHandle { get; }

        public string Title { get; }

        public int Width { get; }

        public int Height { get; }
    }

    private sealed class WindowPreviewItem :
        IDisposable
    {
        public WindowPreviewItem(WindowCandidate candidate, IWindowPreview preview, Border itemBorder, Border thumbnailHost, SystemVisualProxyVisualPrivate thumbnailProxy)
        {
            Candidate = candidate;
            Preview = preview;
            ItemBorder = itemBorder;
            ThumbnailHost = thumbnailHost;
            ThumbnailProxy = thumbnailProxy;
        }

        public WindowCandidate Candidate { get; }

        public IWindowPreview Preview { get; }

        public Border ItemBorder { get; }

        public Border ThumbnailHost { get; }

        public SystemVisualProxyVisualPrivate ThumbnailProxy { get; }

        public void Dispose()
        {
            Preview.Dispose();

            ElementCompositionPreview.SetElementChildVisual(ThumbnailHost, null);

            ThumbnailProxy.Dispose();
        }
    }

    private readonly record struct PreviewPlacement(double X, double Y, double Width, double Height);

    private delegate bool EnumWindowsProc(nint windowHandle, nint parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(nint windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint windowHandle, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint windowHandle, out RECT rect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint windowHandle, int attribute, ref int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}