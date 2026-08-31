using Infinity.Application.Abstractions;
using System.Text.Json.Serialization;

namespace Infinity.Platform.Windows;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GlanceBridgeWireMessage))]
[JsonSerializable(typeof(InfinityPageNavigationState))]
[JsonSerializable(typeof(InfinityPageNavigationVisibility))]
[JsonSerializable(typeof(InfinityPageTitleUpdate))]
internal sealed partial class GlanceBridgeJsonContext :
    JsonSerializerContext
{
}
