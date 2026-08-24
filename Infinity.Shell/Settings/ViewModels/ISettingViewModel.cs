using System.Collections;
using System.ComponentModel;

namespace Infinity.Shell;

public interface ISettingViewModel :
    IEnumerable,
    IDisposable,
    INotifyPropertyChanged
{
    IReadOnlyList<ISettingViewModel> Children => [];

    string Glyph => string.Empty;

    string RouteSegment => GetType().Name;

    string Title => string.Empty;
}
