using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Shell;

public sealed class DesktopApplicationLaunchCoordinator(IApplicationLauncher launcher, IRecentApplicationStore recentApplicationStore, IWindowCollection windowCollection, IWindowStore windowStore, IForegroundWindowTracker foregroundWindowTracker, IWindowResizeSynchronizer resizeSynchronizer, IScroller scroller, DesktopApplicationPlacementResolver placementResolver, ILogger<DesktopApplicationLaunchCoordinator> logger)
{
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(12);
    private readonly SemaphoreSlim launchGate = new(1, 1);

    public async Task<nint> LaunchAsync(LaunchableApplication application, DesktopApplicationTarget target, int screenOriginX, int screenOriginY, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        await launchGate.WaitAsync(cancellationToken);
        try
        {
            TrackedWindow? window = await WaitForApplicationWindowAsync(application, cancellationToken);
            if (window is null || !placementResolver.TryResolve(window, target, screenOriginX, screenOriginY, out DesktopApplicationPlacement placement))
            {
                return 0;
            }

            if (placement.Resize)
            {
                resizeSynchronizer.TrySynchronize(window.Handle, RoundPositive(placement.Width), RoundPositive(placement.Height));
                window.Width = RoundPositive(placement.Width);
                window.Height = RoundPositive(placement.Height);
            }

            window.CanvasX = Round(placement.CanvasX);
            window.CanvasY = Round(placement.CanvasY);
            window.InvalidatePlacement();
            windowStore.NotifyChanged(window.Handle);
            scroller.Reposition();
            logger.LogInformation("Opened {ApplicationName} on desktop page {Page}{Slot}", application.DisplayName, target.Page + 1, target.IsSnapSlot ? $" in slot {target.Slot + 1}" : string.Empty);
            return window.Handle;
        }
        finally
        {
            launchGate.Release();
        }
    }


    private async Task<TrackedWindow?> WaitForApplicationWindowAsync(LaunchableApplication application, CancellationToken cancellationToken)
    {
        HashSet<nint> existingWindows = [..windowCollection.AllTrackedWindows.Select(window => window.Handle)];
        TaskCompletionSource<TrackedWindow> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void HandleWindowAdded(object? sender, TrackedWindow window)
        {
            if (!existingWindows.Contains(window.Handle))
            {
                completion.TrySetResult(window);
            }
        }

        void HandleForegroundWindowChanged(object? sender, nint handle)
        {
            if (existingWindows.Contains(handle) && windowCollection.TryGetTrackedWindow(handle, out TrackedWindow? window) && window is not null)
            {
                completion.TrySetResult(window);
            }
        }

        windowCollection.WindowAdded += HandleWindowAdded;
        foregroundWindowTracker.ForegroundWindowChanged += HandleForegroundWindowChanged;
        try
        {
            if (!launcher.TryLaunch(application))
            {
                return null;
            }

            await recentApplicationStore.RecordAsync(application, cancellationToken);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(LaunchTimeout);
            try
            {
                return await completion.Task.WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException)when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Timed out waiting for {ApplicationName} to create or activate a window", application.DisplayName);
                return null;
            }
        }
        finally
        {
            windowCollection.WindowAdded -= HandleWindowAdded;
            foregroundWindowTracker.ForegroundWindowChanged -= HandleForegroundWindowChanged;
        }
    }


    private static int Round(double value) => (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue);

    private static int RoundPositive(double value) => Math.Max(1, Round(value));
}
