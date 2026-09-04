using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using System;

namespace Infinity.Shell.WinUI;

public sealed class DesktopDragCursorConfinement :
    IDisposable
{
    private readonly IPointerConfinement pointerConfinement;
    private readonly IPanState panState;
    private readonly DesktopDragBoundaryCalculator boundaryCalculator;
    private nint ownerWindowHandle;
    private double viewportWidth;
    private double viewportHeight;
    private double overviewScale;
    private double rasterizationScale;
    private bool constrainVertically;
    private bool constrainToCenteredPage;
    private bool active;
    private bool disposed;

    public bool IsConstrainedToCenteredPage => constrainToCenteredPage;

    public DesktopDragCursorConfinement(IPointerConfinement pointerConfinement, IPanState panState, DesktopDragBoundaryCalculator boundaryCalculator)
    {
        this.pointerConfinement = pointerConfinement;
        this.panState = panState;
        this.boundaryCalculator = boundaryCalculator;

        panState.OffsetChanged += HandleOffsetChanged;
    }

    public void SetOwner(nint windowHandle) => ownerWindowHandle = windowHandle;

    public void SetWorkAreaOffsetY(double value)
    {
        boundaryCalculator.SetWorkAreaOffsetY(value);

        if (active)
        {
            Apply();
        }
    }

    public void Begin(double width, double height, double scale, double rasterScale, bool constrainVertical, bool constrainToCenteredPage = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        viewportWidth = width;
        viewportHeight = height;
        overviewScale = scale;
        rasterizationScale = rasterScale;
        constrainVertically = constrainVertical;
        this.constrainToCenteredPage = constrainToCenteredPage;
        active = true;

        Apply();
    }

    public void Update(double width, double height, double scale, double rasterScale)
    {
        if (!active)
        {
            return;
        }

        viewportWidth = width;
        viewportHeight = height;
        overviewScale = scale;
        rasterizationScale = rasterScale;

        Apply();
    }

    public void UseCenteredPageBounds()
    {
        if (!active || constrainToCenteredPage)
        {
            return;
        }

        constrainToCenteredPage = true;
        Apply();
    }

    public void Release()
    {
        if (!active)
        {
            return;
        }

        active = false;
        pointerConfinement.Release();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        panState.OffsetChanged -= HandleOffsetChanged;
        Release();
        GC.SuppressFinalize(this);
    }

    private void HandleOffsetChanged() => Apply();

    private void Apply()
    {
        if (!active)
        {
            return;
        }

        DesktopDragBounds bounds = constrainToCenteredPage
            ? boundaryCalculator.GetCenteredPageBounds(viewportWidth, viewportHeight, overviewScale)
            : boundaryCalculator.GetBounds(viewportWidth, viewportHeight, overviewScale);

        if (!bounds.IsValid)
        {
            pointerConfinement.Release();
            return;
        }

        double minimumY = constrainVertically ? bounds.MinimumY : 0;
        double maximumY = constrainVertically ? bounds.MaximumY : viewportHeight;
        pointerConfinement.Confine(ownerWindowHandle, rasterizationScale, bounds.MinimumX, minimumY, bounds.MaximumX, maximumY);
    }
}
