using Infinity.Application.Abstractions;
using System.Text.Json.Serialization;

namespace Infinity.Shell;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(InfinityPageTitleUpdate))]
internal sealed partial class InfinityGlanceJsonContext :
    JsonSerializerContext
{
}
