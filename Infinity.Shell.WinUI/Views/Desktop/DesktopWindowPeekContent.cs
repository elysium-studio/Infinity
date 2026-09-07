using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Infinity.Shell.WinUI;

internal sealed class DesktopWindowPeekContent : IDisposable
{
    private readonly Border source;
    private readonly Viewbox destination;
    private readonly double originalWidth;
    private readonly double originalHeight;
    private bool disposed;

    public FrameworkElement Element { get; }

    public double Width { get; }

    public double Height { get; }

    public DesktopWindowPeekContent(
        Border source,
        Viewbox destination)
    {
        this.source = source;
        this.destination = destination;
        Element = source.Child as FrameworkElement ?? throw new InvalidOperationException("The thumbnail has no content to present");
        Width = double.IsFinite(source.Width) && source.Width > 0 ? source.Width : source.ActualWidth;
        Height = double.IsFinite(source.Height) && source.Height > 0 ? source.Height : source.ActualHeight;
        if (!double.IsFinite(Width) || !double.IsFinite(Height) || Width <= 0 || Height <= 0)
        {
            throw new InvalidOperationException("The thumbnail has no valid presentation bounds");
        }

        originalWidth = Element.Width;
        originalHeight = Element.Height;
        source.Child = null;
        try
        {
            Element.Width = Width;
            Element.Height = Height;
            destination.Child = Element;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (ReferenceEquals(destination.Child, Element))
        {
            destination.Child = null;
        }

        Element.Width = originalWidth;
        Element.Height = originalHeight;
        source.Child = Element;
    }
}
