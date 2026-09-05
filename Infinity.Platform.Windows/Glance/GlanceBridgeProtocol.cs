using Infinity.Application.Abstractions;

namespace Infinity.Platform.Windows;

internal static class GlanceBridgeProtocol
{
    public const string PipeName = "ElysiumStudio.Glance.Bridge.v1";
    public const int Version = 1;
    public const string ApplicationId = "ElysiumStudio.Infinity";
    public const string PagesCapability = InfinityGlanceTopics.PagesCapability;
    public const string PageNavigationTopic = "page-navigation";
    public const string PageNavigationVisibilityTopic = "page-navigation-visibility";
    public const string PageTitleUpdateTopic = InfinityGlanceTopics.PageTitleUpdate;
}
