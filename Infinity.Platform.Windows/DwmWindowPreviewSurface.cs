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
    private bool isDisposed;
    private bool? bridgeAvailable;
    private int lastCreateFailure;
    private int lastUpdateFailure;
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

    public void Apply(DwmWindowPreview preview,
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

            int normalizedWidth = Math.Max(1, NormalizeLength(width));
            int normalizedHeight = Math.Max(1, NormalizeLength(height));
            bool normalizedVisibility = isVisible && width > 0.0 && height > 0.0;

            if (state.Width == normalizedWidth &&
                state.Height == normalizedHeight &&
                state.IsVisible == normalizedVisibility)
            {
                return;
            }

            int result = TryUpdate(state.ThumbnailHandle,
                preview.WindowHandle,
                normalizedWidth,
                normalizedHeight,
                normalizedVisibility);

            if (result < 0)
            {
                LogUpdateFailure(result);
                return;
            }

            state.Width = normalizedWidth;
            state.Height = normalizedHeight;
            state.IsVisible = normalizedVisibility;
            lastUpdateFailure = 0;
        }
    }

    public void Clear()
    {
        lock (syncLock)
        {
            if (isDisposed)
            {
                return;
            }

            DestroyPreviews();
            ownerWindowHandle = 0;
        }
    }

    public IWindowPreview? CreatePreview(nint windowHandle, nint compositor)
    {
        if (windowHandle == 0 || compositor == 0)
        {
            return null;
        }

        lock (syncLock)
        {
            if (isDisposed || ownerWindowHandle == 0 || (bridgeAvailable ??= TryIsAvailable()) is false)
            {
                return null;
            }

            int result = TryCreate(ownerWindowHandle,
                windowHandle,
                compositor,
                out nint visual,
                out nint thumbnailHandle);

            if (result < 0 || visual == 0 || thumbnailHandle == 0)
            {
                if (thumbnailHandle != 0)
                {
                    TryDestroy(thumbnailHandle);
                }

                if (visual != 0)
                {
                    Marshal.Release(visual);
                }

                LogCreateFailure(result < 0 ? result : unchecked((int)0x80004005));
                return null;
            }

            long previewId = ++nextPreviewId;
            DwmWindowPreview preview = new(this, windowHandle, previewId, visual);
            previews.Add(previewId, new PreviewState(preview, visual, thumbnailHandle));
            lastCreateFailure = 0;
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

            DestroyPreviews();
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
            if (isDisposed || this.ownerWindowHandle == ownerWindowHandle)
            {
                return;
            }

            DestroyPreviews();
            this.ownerWindowHandle = ownerWindowHandle;
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
            DestroyPreview(state, false);
        }
    }

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int DwmThumbnailVisual_Create(nint ownerWindowHandle,
        nint sourceWindowHandle,
        nint compositor,
        out nint visual,
        out nint thumbnailHandle);

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern void DwmThumbnailVisual_Destroy(nint thumbnailHandle);

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int DwmThumbnailVisual_IsAvailable();

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int DwmThumbnailVisual_Update(nint thumbnailHandle,
        nint sourceWindowHandle,
        int width,
        int height,
        int isVisible);

    private static int NormalizeLength(double value)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            return 0;
        }

        return (int)Math.Clamp(Math.Round(value), 1, int.MaxValue);
    }

    private static int TryCreate(nint ownerWindowHandle,
        nint sourceWindowHandle,
        nint compositor,
        out nint visual,
        out nint thumbnailHandle)
    {
        try
        {
            return DwmThumbnailVisual_Create(ownerWindowHandle,
                sourceWindowHandle,
                compositor,
                out visual,
                out thumbnailHandle);
        }
        catch (DllNotFoundException)
        {
            visual = 0;
            thumbnailHandle = 0;
            return unchecked((int)0x8007007E);
        }
        catch (EntryPointNotFoundException)
        {
            visual = 0;
            thumbnailHandle = 0;
            return unchecked((int)0x8007007F);
        }
    }

    private static void TryDestroy(nint thumbnailHandle)
    {
        try
        {
            DwmThumbnailVisual_Destroy(thumbnailHandle);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
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

    private static int TryUpdate(nint thumbnailHandle,
        nint sourceWindowHandle,
        int width,
        int height,
        bool isVisible)
    {
        try
        {
            return DwmThumbnailVisual_Update(thumbnailHandle,
                sourceWindowHandle,
                width,
                height,
                isVisible ? 1 : 0);
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

    private void DestroyPreviews()
    {
        foreach (PreviewState state in previews.Values)
        {
            DestroyPreview(state, true);
        }

        previews.Clear();
    }

    private static void DestroyPreview(PreviewState state, bool markDisposed)
    {
        if (markDisposed)
        {
            state.Preview.MarkDisposed();
        }

        TryDestroy(state.ThumbnailHandle);
        Marshal.Release(state.Visual);
    }

    private void LogCreateFailure(int result)
    {
        if (result == lastCreateFailure)
        {
            return;
        }

        lastCreateFailure = result;
        logger.LogWarning("DWM thumbnail visual creation failed with HRESULT 0x{HResult:X8}", result);
    }

    private void LogUpdateFailure(int result)
    {
        if (result == lastUpdateFailure)
        {
            return;
        }

        lastUpdateFailure = result;
        logger.LogWarning("DWM thumbnail update failed with HRESULT 0x{HResult:X8}", result);
    }

    private class PreviewState(DwmWindowPreview preview,
        nint visual,
        nint thumbnailHandle)
    {
        public DwmWindowPreview Preview { get; } = preview;

        public nint Visual { get; } = visual;

        public nint ThumbnailHandle { get; } = thumbnailHandle;

        public int Width { get; set; }

        public int Height { get; set; }

        public bool IsVisible { get; set; }
    }
}
