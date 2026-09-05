using System.Text.Json;
using Infinity.Application.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class GlanceBridgeProtocolTests
{
    [Fact]
    public void HelloMessageOmitsMissingPayload()
    {
        GlanceBridgeWireMessage message = new()
        {
            Kind = "hello",
            ProtocolVersion = GlanceBridgeProtocol.Version,
            ApplicationId = GlanceBridgeProtocol.ApplicationId
        };
        string json = JsonSerializer.Serialize(message, GlanceBridgeJsonContext.Default.GlanceBridgeWireMessage);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("hello", document.RootElement.GetProperty("kind").GetString());
        Assert.False(document.RootElement.TryGetProperty("payload", out _));
    }


    [Fact]
    public void DuplicatePageStateIsDiscarded()
    {
        using InfinityGlanceBridge bridge = new(NullLogger<InfinityGlanceBridge>.Instance);
        InfinityPageNavigationState state = new(2, 3, "Page 3");
        Assert.True(bridge.TrySetLatestState(state));
        Assert.False(bridge.TrySetLatestState(state));
        Assert.True(bridge.TrySetLatestState(state with { PageIndex = 3, PageNumber = 4, PageTitle = "Page 4" }));
    }


    [Fact]
    public void NavigationVisibilityTracksDesktopOverviewSurface()
    {
        using InfinityGlanceBridge bridge = new(NullLogger<InfinityGlanceBridge>.Instance);
        Assert.True(bridge.TrySetPageNavigationSurfaceVisibility(InfinityPageNavigationSurface.DesktopOverview, true));
        Assert.True(bridge.TrySetPageNavigationSurfaceVisibility(InfinityPageNavigationSurface.DesktopOverview, false));
        Assert.False(bridge.TrySetPageNavigationSurfaceVisibility(InfinityPageNavigationSurface.DesktopOverview, false));
    }


    [Fact]
    public void OpeningFirstNavigationSurfaceRepublishesCurrentPage()
    {
        using InfinityGlanceBridge bridge = new(NullLogger<InfinityGlanceBridge>.Instance);
        InfinityPageNavigationState state = new(0, 1, "Page 1");
        Assert.True(bridge.TrySetLatestState(state));
        bridge.TakePendingUpdates();
        Assert.True(bridge.TrySetPageNavigationSurfaceVisibility(InfinityPageNavigationSurface.DesktopOverview, true));
        (InfinityPageNavigationState? page, bool? visibility) = bridge.TakePendingUpdates();
        Assert.Equal(state, page);
        Assert.True(visibility);
    }


    [Fact]
    public void ConcurrentUpdatesAreSafelyCoalesced()
    {
        using InfinityGlanceBridge bridge = new(NullLogger<InfinityGlanceBridge>.Instance);
        Parallel.For(0, 1000, index =>  {  bridge.PublishPageNavigation(new InfinityPageNavigationState(index, index + 1, $"Page {index + 1}"));  bridge.SetPageNavigationSurfaceVisible(InfinityPageNavigationSurface.DesktopOverview, index % 2 == 0);  });
        (InfinityPageNavigationState? page, bool? visibility) = bridge.TakePendingUpdates();
        Assert.NotNull(page);
        Assert.NotNull(visibility);
    }
}
