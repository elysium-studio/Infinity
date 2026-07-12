using Infinity.Platform.Abstractions;
using System.Runtime.InteropServices;

namespace Infinity.Platform.Windows;

public class DwmWindowPreviewSurface :
    IWindowPreviewSurface,
    IDwmWindowPreviewSurface,
    IDisposable
{
    private const string LibraryName = "Infinity.Platform.Windows.Native.dll";

    private readonly Dictionary<nint, DwmWindowPreview> previews = [];
    private readonly Lock syncLock = new();
    private DwmThumbnailVisualItem[] renderItems = [];
    private DwmWindowPreview?[] renderedPreviews = [];

    private bool isDisposed;
    private bool? bridgeAvailable;
    private nint ownerWindowHandle;
    public bool IsAvailable
    {
        get
        {
            lock (syncLock)
            {
                return TryIsAvailable();
            }
        }
    }

    public int LastBridgeHResult
    {
        get
        {
            lock (syncLock)
            {
                return GetLastBridgeHResult();
            }
        }
    }

    public int LastHResult
    {
        get
        {
            lock (syncLock)
            {
                return GetLastHResult();
            }
        }
    }
    public void Apply(DwmWindowPreview preview)
    {
        lock (syncLock)
        {
            if (isDisposed)
            {
                return;
            }

            if (!previews.TryGetValue(preview.WindowHandle, out DwmWindowPreview? currentPreview))
            {
                return;
            }

            if (!ReferenceEquals(currentPreview, preview))
            {
                return;
            }

            RenderCore();
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

            foreach (DwmWindowPreview preview in previews.Values)
            {
                preview.ClearTarget();
            }

            TryClear();
        }
    }

    public void Commit() => Render();

    public IWindowPreview? CreatePreview(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return null;
        }

        uint currentProcessId = QueryOwnerProcessId(windowHandle);

        lock (syncLock)
        {
            if (isDisposed)
            {
                return null;
            }

            if (previews.TryGetValue(windowHandle, out DwmWindowPreview? existingPreview))
            {
                if (existingPreview.OwnerProcessId == currentProcessId)
                {
                    return existingPreview;
                }

                previews.Remove(windowHandle);
                existingPreview.MarkDisposed();
            }

            DwmWindowPreview preview = new(this, windowHandle, currentProcessId);
            previews[windowHandle] = preview;

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

            foreach (DwmWindowPreview preview in previews.Values)
            {
                preview.MarkDisposed();
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
        lock (syncLock)
        {
            if (isDisposed)
            {
                return;
            }

            if (ownerWindowHandle == 0)
            {
                return;
            }

            if (this.ownerWindowHandle == ownerWindowHandle)
            {
                RenderCore();
                return;
            }

            if (this.ownerWindowHandle != 0)
            {
                return;
            }

            this.ownerWindowHandle = ownerWindowHandle;

            RenderCore();
        }
    }
    public void Remove(DwmWindowPreview preview)
    {
        lock (syncLock)
        {
            if (isDisposed)
            {
                return;
            }

            if (previews.TryGetValue(preview.WindowHandle, out DwmWindowPreview? currentPreview) &&
                ReferenceEquals(currentPreview, preview))
            {
                previews.Remove(preview.WindowHandle);
            }

            RenderCore();
        }
    }

    public void Render()
    {
        lock (syncLock)
        {
            if (isDisposed)
            {
                return;
            }

            RenderCore();
        }
    }
    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern void DwmThumbnailVisual_Clear();

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int DwmThumbnailVisual_GetLastBridgeHResult();

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int DwmThumbnailVisual_GetLastHResult();

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int DwmThumbnailVisual_IsAvailable();

    [DllImport(LibraryName, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int DwmThumbnailVisual_RenderBatch(nint ownerWindowHandle, DwmThumbnailVisualItem[] items, int count);

    private static int GetLastBridgeHResult()
    {
        try
        {
            return DwmThumbnailVisual_GetLastBridgeHResult();
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

    private static int GetLastHResult()
    {
        try
        {
            return DwmThumbnailVisual_GetLastHResult();
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

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

    private static uint QueryOwnerProcessId(nint windowHandle)
    {
        try
        {
            _ = GetWindowThreadProcessId(windowHandle, out uint processId);
            return processId;
        }
        catch (DllNotFoundException)
        {
            return 0;
        }
        catch (EntryPointNotFoundException)
        {
            return 0;
        }
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

    private static bool TryRenderBatch(nint ownerWindowHandle, DwmThumbnailVisualItem[] items, int count)
    {
        try
        {
            return DwmThumbnailVisual_RenderBatch(ownerWindowHandle, items, count) == 0;
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

    private bool RenderCore()
    {
        if (ownerWindowHandle == 0)
        {
            return false;
        }

        if ((bridgeAvailable ??= TryIsAvailable()) is false)
        {
            return false;
        }

        EnsureRenderCapacity(previews.Count);
        int itemCount = 0;

        foreach (DwmWindowPreview preview in previews.Values)
        {
            if (!preview.HasTarget || !preview.IsVisible || preview.WindowHandle == 0 || preview.SharedTargetHandle == 0 || preview.Width <= 0.0 || preview.Height <= 0.0)
            {
                continue;
            }

            renderItems[itemCount] = new DwmThumbnailVisualItem
            {
                SourceWindowHandle = preview.WindowHandle,
                SharedTargetHandle = preview.SharedTargetHandle,
                Width = Math.Max(1, (int)Math.Round(preview.Width)),
                Height = Math.Max(1, (int)Math.Round(preview.Height))
            };
            renderedPreviews[itemCount] = preview;
            itemCount++;
        }

        bool result = TryRenderBatch(ownerWindowHandle, renderItems, itemCount);

        for (int index = 0; index < itemCount; index++)
        {
            bool itemSucceeded = renderItems[index].ResultHResult == 0;
            renderedPreviews[index]!.ReportRenderResult(itemSucceeded, renderItems[index].ResultHResult);
            renderedPreviews[index] = null;
            renderItems[index] = default;
        }

        return result;
    }

    private void EnsureRenderCapacity(int count)
    {
        if (renderItems.Length >= count)
        {
            return;
        }

        int capacity = Math.Max(count, Math.Max(4, renderItems.Length * 2));
        Array.Resize(ref renderItems, capacity);
        Array.Resize(ref renderedPreviews, capacity);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct DwmThumbnailVisualItem
    {
        public nint SourceWindowHandle;

        public nint SharedTargetHandle;

        public int Width;

        public int Height;

        public int ResultHResult;
    }
}
