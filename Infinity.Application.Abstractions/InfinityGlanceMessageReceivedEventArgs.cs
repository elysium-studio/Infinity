namespace Infinity.Application.Abstractions;

public sealed record InfinityGlanceMessageReceivedEventArgs(string Capability, string Topic, string Payload);
