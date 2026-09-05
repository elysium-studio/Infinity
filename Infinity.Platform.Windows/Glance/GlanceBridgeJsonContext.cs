using System.Text.Json.Serialization;
using Infinity.Application.Abstractions;

namespace Infinity.Platform.Windows;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GlanceBridgeWireMessage))]
[JsonSerializable(typeof(InfinityPageNavigationState))]
[JsonSerializable(typeof(InfinityPageNavigationVisibility))]
[JsonSerializable(typeof(InfinityPageTitleUpdate))]
internal sealed partial class GlanceBridgeJsonContext : JsonSerializerContext
{
}
