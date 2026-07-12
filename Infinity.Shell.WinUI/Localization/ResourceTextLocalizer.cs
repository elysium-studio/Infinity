using Elysium.UI.WinUI;

namespace Infinity.Shell.WinUI;

public class ResourceTextLocalizer(IStringLocalizer localizer) :
    ITextLocalizer
{
    public string GetText(string key, params object[] arguments) => localizer.GetString(key, arguments);
}
