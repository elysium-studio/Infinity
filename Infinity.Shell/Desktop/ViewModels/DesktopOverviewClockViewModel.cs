using CommunityToolkit.Mvvm.ComponentModel;

namespace Infinity.Shell;

public sealed partial class DesktopOverviewClockViewModel :
    ObservableObject
{
    [ObservableProperty]
    private string timeText = string.Empty;

    [ObservableProperty]
    private string dateText = string.Empty;

    public void Update(string time, string date)
    {
        TimeText = time;
        DateText = date;
    }
}
