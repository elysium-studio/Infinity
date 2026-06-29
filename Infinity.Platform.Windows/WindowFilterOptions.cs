namespace Infinity.Platform.Windows;

public class WindowFilterOptions
{
    public IReadOnlySet<string> BlockedProcessNames { get; init; } = new HashSet<string>
    {
        "TextInputHost",
        "SystemSettings",
        "StartMenuExperienceHost",
        "SearchHost",
        "ShellExperienceHost"
    };

    public IReadOnlySet<string> BlockedClassNames { get; init; } = new HashSet<string>
    {
        "Windows.UI.Core.CoreWindow"
    };
}