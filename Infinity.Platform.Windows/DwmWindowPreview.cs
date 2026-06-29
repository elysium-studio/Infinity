using Infinity.Platform.Abstractions;

namespace Infinity.Platform.Windows;

public class DwmWindowPreview(IDwmWindowPreviewSurface surface,
    nint windowHandle,
    uint ownerProcessId) :
    IWindowPreview
{
    private const int FailureThreshold = 3;

    private bool isDisposed;
    private int consecutiveFailureCount;

    public nint WindowHandle { get; } = windowHandle;

    public object? KeepAlive { get; set; }

    internal uint OwnerProcessId { get; } = ownerProcessId;

    internal nint SharedTargetHandle { get; private set; }

    internal double Width { get; private set; }

    internal double Height { get; private set; }

    internal bool IsVisible { get; private set; }

    internal bool HasTarget { get; private set; }

    public event Action? PreviewInvalidated;

    public void SetTarget(nint sharedTargetHandle, double width, double height, bool isVisible)
    {
        if (isDisposed)
        {
            return;
        }

        SharedTargetHandle = sharedTargetHandle;
        Width = width;
        Height = height;
        IsVisible = isVisible;
        HasTarget = sharedTargetHandle != 0 && width > 0.0 && height > 0.0;

        surface.Apply(this);
    }

    public void SetPlacement(double x, double y, double width, double height, bool isVisible) =>
        SetTarget(SharedTargetHandle, width, height, isVisible);

    public void ClearTarget()
    {
        if (isDisposed)
        {
            return;
        }

        SharedTargetHandle = 0;
        Width = 0.0;
        Height = 0.0;
        IsVisible = false;
        HasTarget = false;

        surface.Apply(this);
    }

    internal void ReportRenderResult(bool succeeded, int hResult)
    {
        if (isDisposed)
        {
            return;
        }

        if (succeeded)
        {
            consecutiveFailureCount = 0;
            return;
        }

        consecutiveFailureCount++;

        if (consecutiveFailureCount < FailureThreshold)
        {
            return;
        }

        consecutiveFailureCount = 0;

        InvalidateKeepAlive();

        SharedTargetHandle = 0;
        Width = 0.0;
        Height = 0.0;
        HasTarget = false;

        PreviewInvalidated?.Invoke();
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        InvalidateKeepAlive();

        surface.Remove(this);
    }

    internal void MarkDisposed()
    {
        isDisposed = true;

        InvalidateKeepAlive();
    }

    private void InvalidateKeepAlive()
    {
        (KeepAlive as IDisposable)?.Dispose();
        KeepAlive = null;
    }
}