using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Infinity.Platform.Windows;

public class DwmWindowPreviewSurface(ILogger<DwmWindowPreviewSurface> logger) :
    IWindowPreviewSurface,
    IDwmWindowPreviewSurface,
    IDisposable
{
    private const string LibraryName = "Infinity.Platform.Windows.Native.dll";

    private readonly Dictionary<long, PreviewState> previews = [];
    private readonly Lock syncLock = new();
    private DwmThumbnailVisualItem[] renderItems = [];
    private bool isDisposed;
    private bool? bridgeAvailable;
    private int lastRenderFailure;
    private long nextPreviewId;
    private nint ownerWindowHandle;
    private nint sharedTargetHandle;

    public bool IsAvailable
    {
        get
        {
            lock (syncLock)
            {
                return !isDisposed && (bridgeAvailable ??= TryIsAvailable());
            }
        }
    }

    public void Apply(DwmWindowPreview preview,
        double x,
        double y,
        double width,
        double height,
        int zIndex,
        bool isVisible,
        bool isElevated)
    {
        lock (syncLock)
        {
            if (isDisposed || !previews.TryGetValue(preview.Id, out PreviewState? state) ||
                !ReferenceEquals(state.Preview, preview))
            {
                return;
            }

            int normalizedX = NormalizeCoordinate(x);
            int normalizedY = NormalizeCoordinate(y);
            int normalizedWidth = NormalizeLength(width);
            int normalizedHeight = NormalizeLength(height);
            bool normalizedVisibility = isVisible && normalizedWidth > 0 && normalizedHeight > 0;

            if (state.X == normalizedX &&
                state.Y == normalizedY &&
                state.Width == normalizedWidth &&
                state.Height == normalizedHeight &&
                state.ZIndex == zIndex &&
                state.IsVisible == normalizedVisibility &&
                state.IsElevated == isElevated)
            {
                return;
            }

            state.X = normalizedX;
            state.Y = normalizedY;
            state.Width = normalizedWidth;
            state.Height = normalizedHeight;
            state.ZIndex = zIndex;
            state.IsVisible = normalizedVisibility;
            state.IsElevated = isElevated;
            RenderCore();
        }
    }

    public void Clear()
    {
        lock (syncLock)
        {
            if (!isDisposed)
            {
                TryClear();
                ownerWindowHandle = 0;
                sharedTargetHandle = 0;
            }
        }
    }

    public IWindowPreview? CreatePreview(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return null;
        }

        lock (syncLock)
        {
            if (isDisposed)
            {
                return null;
            }

            long previewId = ++nextPreviewId;
            DwmWindowPreview preview = new(this, windowHandle, previewId);
            previews.Add(previewId, new PreviewState(preview));
            return preview;
        }
    }

    public void Dispose()
    {
        lock (syncLock)
        {
            if (isDisposed)
            {
                return;
            }

            foreach (PreviewState state in previews.Values)
            {
                state.Preview.MarkDisposed();
            }

            previews.Clear();
            TryClear();
            ownerWindowHandle = 0;
            sharedTargetHandle = 0;
            isDisposed = true;
        }

        GC.SuppressFinalize(this);
    }

    public void Initialize(nint ownerWindowHandle)
    {
        if (ownerWindowHandle == 0)
        {
            return;
        }

        lock (syncLock)
        {
            if (isDisposed)
            {
                return;
            }

            if (this.ownerWindowHandle != ownerWindowHandle)
            {
                TryClear();
                this.ownerWindowHandle = ownerWindowHandle;
            }

            RenderCore();
        }
    }

    public void Remove(DwmWindowPreview preview)
    {
        lock (syncLock)
        {
            if (isDisposed || !previews.TryGetValue(preview.Id, out PreviewState? state) ||
                !ReferenceEquals(state.Preview, preview))
            {
                return;
            }

            previews.Remove(preview.Id);
            RenderCore();
        }
    }

    public void SetTarget(nint sharedTargetHandle)
    {
        lock (syncLock)
        {
            if (isDisposed || this.sharedTargetHandle == sharedTargetHandle)
            {
                return;
            }

            TryClear();
            this.sharedTargetHandle = sharedTargetHandle;
            RenderCore();
        }
    }

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern void DwmThumbnailVisual_Clear();

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int DwmThumbnailVisual_IsAvailable();

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int DwmThumbnailVisual_RenderBatch(nint ownerWindowHandle,
        nint sharedTargetHandle,
        DwmThumbnailVisualItem[] items,
        int count);

    private static int NormalizeCoordinate(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue);
    }

    private static int NormalizeLength(double value)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            return 0;
        }

        return (int)Math.Clamp(Math.Round(value), 1, int.MaxValue);
    }

    private static bool TryClear()
    {
        try
        {
            DwmThumbnailVisual_Clear();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static bool TryIsAvailable()
    {
        try
        {
            return DwmThumbnailVisual_IsAvailable() != 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static int TryRenderBatch(nint ownerWindowHandle,
        nint sharedTargetHandle,
        DwmThumbnailVisualItem[] items,
        int count)
    {
        try
        {
            return DwmThumbnailVisual_RenderBatch(ownerWindowHandle, sharedTargetHandle, items, count);
        }
        catch (DllNotFoundException)
        {
            return unchecked((int)0x8007007E);
        }
        catch (EntryPointNotFoundException)
        {
            return unchecked((int)0x8007007F);
        }
    }

    private bool RenderCore()
    {
        if (ownerWindowHandle == 0 || sharedTargetHandle == 0 ||
            (bridgeAvailable ??= TryIsAvailable()) is false)
        {
            return false;
        }

        EnsureRenderCapacity(previews.Count);
        int itemCount = 0;

        foreach (PreviewState state in previews.Values)
        {
            if (state.Width <= 0 || state.Height <= 0)
            {
                continue;
            }

            renderItems[itemCount++] = new DwmThumbnailVisualItem
            {
                PreviewId = (ulong)state.Preview.Id,
                SourceWindowHandle = state.Preview.WindowHandle,
                X = state.X,
                Y = state.Y,
                Width = state.Width,
                Height = state.Height,
                ZIndex = state.ZIndex,
                IsVisible = state.IsVisible ? 1 : 0,
                IsElevated = state.IsElevated ? 1 : 0
            };
        }

        int result = TryRenderBatch(ownerWindowHandle, sharedTargetHandle, renderItems, itemCount);
        Array.Clear(renderItems, 0, itemCount);

        if (result < 0 && result != lastRenderFailure)
        {
            lastRenderFailure = result;
            logger.LogWarning("DWM thumbnail composition failed with HRESULT 0x{HResult:X8}", result);
        }
        else if (result >= 0)
        {
            lastRenderFailure = 0;
        }

        return result >= 0;
    }

    private void EnsureRenderCapacity(int count)
    {
        if (renderItems.Length >= count)
        {
            return;
        }

        int capacity = Math.Max(count, Math.Max(4, renderItems.Length * 2));
        Array.Resize(ref renderItems, capacity);
    }

    private sealed class PreviewState(DwmWindowPreview preview)
    {
        public DwmWindowPreview Preview { get; } = preview;

        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int ZIndex { get; set; }

        public bool IsVisible { get; set; }

        public bool IsElevated { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DwmThumbnailVisualItem
    {
        public ulong PreviewId;

        public nint SourceWindowHandle;

        public int X;

        public int Y;

        public int Width;

        public int Height;

        public int ZIndex;

        public int IsVisible;

        public int IsElevated;
    }
}
