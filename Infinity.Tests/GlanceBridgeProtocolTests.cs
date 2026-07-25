using Infinity.Platform.Windows;
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
}
