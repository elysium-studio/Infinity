using System;
using Windows.Globalization.DateTimeFormatting;

namespace Infinity.Shell.WinUI;

public sealed class DesktopOverviewClockFormatter
{
    private readonly DateTimeFormatter timeFormatter = DateTimeFormatter.ShortTime;
    private readonly DateTimeFormatter dateFormatter = DateTimeFormatter.LongDate;

    public string FormatTime(DateTimeOffset value) => timeFormatter.Format(value);

    public string FormatDate(DateTimeOffset value) => dateFormatter.Format(value);
}
