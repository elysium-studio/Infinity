namespace Infinity.Application.Abstractions;

public interface IInfinityGlanceBridge
{
    bool IsPageNavigationAvailable { get; }

    event EventHandler<InfinityGlanceAvailabilityChangedEventArgs>? AvailabilityChanged;

    event EventHandler<InfinityGlanceMessageReceivedEventArgs>? MessageReceived;

    void PublishPageNavigation(InfinityPageNavigationState state);
}

public sealed record InfinityGlanceAvailabilityChangedEventArgs(
    bool IsPageNavigationAvailable);

public sealed record InfinityGlanceMessageReceivedEventArgs(
    string Capability,
    string Topic,
    string Payload);

public sealed record InfinityPageNavigationState(
    bool IsActive,
    int PageIndex,
    int PageNumber,
    string PageTitle);
