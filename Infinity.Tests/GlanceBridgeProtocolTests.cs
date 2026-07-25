using Infinity.Application.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

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
}
