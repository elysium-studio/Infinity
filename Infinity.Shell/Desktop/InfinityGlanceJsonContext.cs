using System.Text.Json.Serialization;
using Infinity.Application.Abstractions;

namespace Infinity.Shell;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(InfinityPageTitleUpdate))]
internal sealed partial class InfinityGlanceJsonContext : JsonSerializerContext
{
}
