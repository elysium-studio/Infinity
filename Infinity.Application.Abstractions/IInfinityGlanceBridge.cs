namespace Infinity.Application.Abstractions;

public interface IInfinityGlanceBridge
{
    bool IsPageNavigationAvailable { get; }

    event EventHandler<InfinityGlanceAvailabilityChangedEventArgs>? AvailabilityChanged;

    event EventHandler<InfinityGlanceMessageReceivedEventArgs>? MessageReceived;

    void PublishPageNavigation(InfinityPageNavigationState state);

    void SetPageNavigationSurfaceVisible(InfinityPageNavigationSurface surface, bool isVisible);
}
