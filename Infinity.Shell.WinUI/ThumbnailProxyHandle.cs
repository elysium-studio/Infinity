using Microsoft.UI.Composition;
using System;

namespace Infinity.Shell.WinUI;

public partial class ThumbnailProxyHandle(SystemVisualProxyVisualPrivate proxy, Visual visual) :
    IDisposable
{
    public SystemVisualProxyVisualPrivate Proxy { get; } = proxy;

    public Visual Visual { get; } = visual;

    public void Dispose() => Proxy.Dispose();
}
