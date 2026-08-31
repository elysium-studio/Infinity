using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infinity.Platform.Windows;

internal sealed class GlanceBridgeWireMessage
{
    public string Kind { get; set; } = string.Empty;

    public int ProtocolVersion { get; set; }

    public string? ApplicationId { get; set; }

    public string? ApplicationVersion { get; set; }

    public string[]? Capabilities { get; set; }

    public string? Capability { get; set; }

    public string? Topic { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Payload { get; set; }
}
