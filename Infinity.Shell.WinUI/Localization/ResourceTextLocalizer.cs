using Elysium.UI.WinUI;
using Infinity.Application.Abstractions;

namespace Infinity.Shell.WinUI;

public sealed class ResourceTextLocalizer(IStringLocalizer localizer) :
    ITextLocalizer
{
    public string GetText(string key, params object[] arguments) => localizer.GetString(key, arguments);
}
