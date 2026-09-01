using Elysium.Platform.Abstractions;
using Microsoft.UI.Dispatching;
using System;
using System.Threading;

namespace Infinity.Shell.WinUI;

internal sealed class DesktopOverlayTopMostCoordinator(
    IWindowEventListener windowEvents,
    DispatcherQueue dispatcherQueue,
    Func<bool> isActive,
    Action promote)
{
    private const long PromotionCooldownMilliseconds = 75;

    private int isStarted;
    private int promotionQueued;
    private long lastPromotionTick;

    public void Start()
    {
        if (Interlocked.Exchange(ref isStarted, 1) != 0)
        {
            return;
        }

        windowEvents.ForegroundChanged += HandleForegroundChanged;
        windowEvents.WindowStackChanged += HandleWindowStackChanged;
    }

    public void PromoteNow()
    {
        if (!isActive())
        {
            return;
        }

        Volatile.Write(ref lastPromotionTick, Environment.TickCount64);
        promote();
    }

    public void Reset() => Interlocked.Exchange(ref promotionQueued, 0);

    private void HandleForegroundChanged(nint handle) => QueuePromotion(ignoreCooldown: true);

    private void HandleWindowStackChanged() => QueuePromotion(ignoreCooldown: false);

    private void QueuePromotion(bool ignoreCooldown)
    {
        if (!isActive())
        {
            return;
        }

        if (!ignoreCooldown &&
            Environment.TickCount64 - Volatile.Read(ref lastPromotionTick) < PromotionCooldownMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref promotionQueued, 1) != 0)
        {
            return;
        }

        if (dispatcherQueue.HasThreadAccess)
        {
            PromoteQueued();
            return;
        }

        if (!dispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, PromoteQueued))
        {
            Interlocked.Exchange(ref promotionQueued, 0);
        }
    }

    private void PromoteQueued()
    {
        Interlocked.Exchange(ref promotionQueued, 0);
        PromoteNow();
    }
}
