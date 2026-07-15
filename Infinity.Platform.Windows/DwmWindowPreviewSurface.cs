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

    public nint OwnerWindowHandle
    {
        get
        {
            lock (syncLock)
            {
                return isDisposed ? 0 : ownerWindowHandle;
            }
        }
    }

    public void Apply(DwmWindowPreview preview,
        nint sharedTargetHandle,
        double width,
        double height,
        bool isVisible)
    {
        lock (syncLock)
        {
            if (isDisposed || !previews.TryGetValue(preview.Id, out PreviewState? state) ||
                !ReferenceEquals(state.Preview, preview))
            {
                return;
            }

            int normalizedWidth = NormalizeLength(width);
            int normalizedHeight = NormalizeLength(height);
            bool normalizedVisibility = isVisible && sharedTargetHandle != 0 &&
                normalizedWidth > 0 && normalizedHeight > 0;

            if (state.SharedTargetHandle == sharedTargetHandle &&
                state.Width == normalizedWidth &&
                state.Height == normalizedHeight &&
                state.IsVisible == normalizedVisibility)
            {
                return;
            }

            state.SharedTargetHandle = sharedTargetHandle;
            state.Width = normalizedWidth;
            state.Height = normalizedHeight;
            state.IsVisible = normalizedVisibility;
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

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern void DwmThumbnailVisual_Clear();

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int DwmThumbnailVisual_IsAvailable();

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int DwmThumbnailVisual_RenderBatch(nint ownerWindowHandle,
        DwmThumbnailVisualItem[] items,
        int count);

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
        DwmThumbnailVisualItem[] items,
        int count)
    {
        try
        {
            return DwmThumbnailVisual_RenderBatch(ownerWindowHandle, items, count);
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
        if (ownerWindowHandle == 0 || (bridgeAvailable ??= TryIsAvailable()) is false)
        {
            return false;
        }

        EnsureRenderCapacity(previews.Count);
        int itemCount = 0;

        foreach (PreviewState state in previews.Values)
        {
            if (state.SharedTargetHandle == 0 || state.Width <= 0 || state.Height <= 0)
            {
                continue;
            }

            renderItems[itemCount++] = new DwmThumbnailVisualItem
            {
                PreviewId = (ulong)state.Preview.Id,
                SourceWindowHandle = state.Preview.WindowHandle,
                SharedTargetHandle = state.SharedTargetHandle,
                Width = state.Width,
                Height = state.Height,
                IsVisible = state.IsVisible ? 1 : 0
            };
        }

        int result = TryRenderBatch(ownerWindowHandle, renderItems, itemCount);
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

        public nint SharedTargetHandle { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public bool IsVisible { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DwmThumbnailVisualItem
    {
        public ulong PreviewId;

        public nint SourceWindowHandle;

        public nint SharedTargetHandle;

        public int Width;

        public int Height;

        public int IsVisible;
    }
}
