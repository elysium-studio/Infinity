using System;
using Microsoft.UI.Dispatching;

namespace Infinity.Shell.WinUI;

public sealed class DesktopOverviewClockController(DesktopOverviewClockViewModel viewModel, DesktopOverviewClockFormatter formatter)
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
    private readonly DesktopOverviewClockViewModel viewModel = viewModel;
    private readonly DesktopOverviewClockFormatter formatter = formatter;
    private DispatcherQueueTimer? timer;

    public DesktopOverviewClockViewModel ViewModel => viewModel;

    public void Start(DispatcherQueue dispatcherQueue)
    {
        Update();
        if (timer is not null)
        {
            return;
        }

        timer = dispatcherQueue.CreateTimer();
        timer.Interval = UpdateInterval;
        timer.Tick += HandleTick;
        timer.Start();
    }


    public void Stop()
    {
        if (timer is null)
        {
            return;
        }

        timer.Stop();
        timer.Tick -= HandleTick;
        timer = null;
    }


    private void HandleTick(DispatcherQueueTimer sender, object args) => Update();

    private void Update()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        viewModel.Update(formatter.FormatTime(now), formatter.FormatDate(now));
    }
}
