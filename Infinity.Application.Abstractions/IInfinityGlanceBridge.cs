namespace Infinity.Application.Abstractions;

public interface IInfinityGlanceBridge
{
    bool IsPageNavigationAvailable { get; }

    event EventHandler<InfinityGlanceAvailabilityChangedEventArgs>? AvailabilityChanged;

    event EventHandler<InfinityGlanceMessageReceivedEventArgs>? MessageReceived;

    void PublishPageNavigation(InfinityPageNavigationState state);

    void SetPageNavigationSurfaceVisible(InfinityPageNavigationSurface surface, bool isVisible);
}

public sealed record InfinityGlanceAvailabilityChangedEventArgs(bool IsPageNavigationAvailable);

public sealed record InfinityGlanceMessageReceivedEventArgs(string Capability, string Topic, string Payload);

public sealed record InfinityPageNavigationState(int PageIndex, int PageNumber, string PageTitle);

public sealed record InfinityPageNavigationVisibility(bool IsVisible);

public sealed record InfinityPageTitleUpdate(int PageIndex, string PageTitle);

public static class InfinityGlanceTopics
{
    public const string PagesCapability = "infinity.pages.v1";

    public const string PageTitleUpdate = "page-title-update";
}

[Flags]
public enum InfinityPageNavigationSurface
{
    None = 0,
    PageTint = 1
}
