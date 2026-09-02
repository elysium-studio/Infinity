using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Infinity.Shell.WinUI;

public sealed class DesktopThumbnailCompositionLayer :
    IDisposable
{
    private readonly Dictionary<ThumbnailCompositionPreview, ThumbnailEntry> entries = [];
    private FrameworkElement? host;
    private ContainerVisual? root;
    private long nextSequence;
    private bool disposed;

    internal Compositor? Compositor => root?.Compositor;

    public void Attach(FrameworkElement element)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (ReferenceEquals(host, element))
        {
            return;
        }

        if (entries.Count != 0)
        {
            throw new InvalidOperationException("The thumbnail composition layer cannot change hosts while previews are attached.");
        }

        Detach();

        Visual elementVisual = ElementCompositionPreview.GetElementVisual(element);
        root = elementVisual.Compositor.CreateContainerVisual();
        root.RelativeSizeAdjustment = System.Numerics.Vector2.One;
        ElementCompositionPreview.SetElementChildVisual(element, root);
        host = element;
    }

    internal void Add(ThumbnailCompositionPreview preview)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (root is null)
        {
            throw new InvalidOperationException("The thumbnail composition layer must be attached before previews are created.");
        }

        if (entries.ContainsKey(preview))
        {
            return;
        }

        entries.Add(preview, new ThumbnailEntry(nextSequence++));
        root.Children.InsertAtTop(preview.RootVisual);
    }

    internal void Remove(ThumbnailCompositionPreview preview)
    {
        if (!entries.Remove(preview) || root is null)
        {
            return;
        }

        if (ReferenceEquals(preview.RootVisual.Parent, root))
        {
            root.Children.Remove(preview.RootVisual);
        }
    }

    internal void SetZIndex(ThumbnailCompositionPreview preview, int zIndex)
    {
        if (!entries.TryGetValue(preview, out ThumbnailEntry? entry) || entry.ZIndex == zIndex)
        {
            return;
        }

        entry.ZIndex = zIndex;
        RebuildOrder();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        foreach (ThumbnailCompositionPreview preview in entries.Keys.ToArray())
        {
            preview.Dispose();
        }

        entries.Clear();
        Detach();
        GC.SuppressFinalize(this);
    }

    private void RebuildOrder()
    {
        if (root is null)
        {
            return;
        }

        ThumbnailCompositionPreview[] ordered = [.. entries
            .OrderBy(entry => entry.Value.ZIndex)
            .ThenBy(entry => entry.Value.Sequence)
            .Select(entry => entry.Key)];

        root.Children.RemoveAll();

        foreach (ThumbnailCompositionPreview preview in ordered)
        {
            root.Children.InsertAtTop(preview.RootVisual);
        }
    }

    private void Detach()
    {
        if (host is not null && root is not null && ReferenceEquals(ElementCompositionPreview.GetElementChildVisual(host), root))
        {
            ElementCompositionPreview.SetElementChildVisual(host, null);
        }

        root?.Dispose();
        root = null;
        host = null;
    }

    private sealed class ThumbnailEntry(long sequence)
    {
        public long Sequence { get; } = sequence;

        public int ZIndex { get; set; }
    }
}
