using System.Text.Json.Serialization;

namespace Infinity.Shell;

[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
public sealed partial class InfinityJsonContext : JsonSerializerContext;
