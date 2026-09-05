using System.Collections;

namespace Infinity.Shell;

public interface IDesktopSettingsSectionViewModel : IDesktopViewModel, IEnumerable
{
    string Title { get; }
}
