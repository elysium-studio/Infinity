using Infinity.Application.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infinity.Platform.Windows;

internal static class GlanceBridgeProtocol
{
    public const string PipeName = "ElysiumStudio.Glance.Bridge.v1";
    public const int Version = 1;
    public const string ApplicationId = "ElysiumStudio.Infinity";
    public const string PagesCapability = "infinity.pages.v1";
    public const string PageNavigationTopic = "page-navigation";
}

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

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GlanceBridgeWireMessage))]
[JsonSerializable(typeof(InfinityPageNavigationState))]
internal sealed partial class GlanceBridgeJsonContext :
    JsonSerializerContext
{
}
