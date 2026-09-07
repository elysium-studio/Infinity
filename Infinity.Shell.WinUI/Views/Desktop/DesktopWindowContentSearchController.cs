using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWindowContentSearchController(
    WindowCapturePreviewSurface capture,
    DesktopWindowContentIndex index,
    IWindowTextRecognizer recognizer,
    IWindowCollection windows,
    IPager pager,
    IWorkspace workspace,
    DesktopWindowPreviewCollection previews,
    DesktopOverviewConfiguration configuration,
    ILogger<DesktopWindowContentSearchController> logger)
{
    private readonly Dictionary<nint, long> nextScan = [];
    private DispatcherQueueTimer? timer;
    private DispatcherQueue? dispatcher;
    private CancellationTokenSource? cancellation;
    private long generation;
    private long readyAt;
    private int busy;

    public event Action? Updated;

    public bool IsInteractionEnabled { get; set; }

    public void Start()
    {
        if (cancellation is not null)
        {
            return;
        }

        dispatcher ??= DispatcherQueue.GetForCurrentThread();
        if (timer is null)
        {
            timer = dispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += HandleTick;
        }

        cancellation = new();
        readyAt = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
        timer.Start();
    }

    public void Stop()
    {
        generation++;
        timer?.Stop();
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
        nextScan.Clear();
        index.Clear();
        previews.ClearSearchSnippets();
    }

    private void HandleTick(DispatcherQueueTimer sender, object args)
    {
        if (cancellation is null || !IsInteractionEnabled || !configuration.SearchWindowContents || !configuration.ShowSearchBox || !pager.IsPageCentered(pager.CurrentPage) || previews.HasActiveInteraction || Stopwatch.GetTimestamp() < readyAt)
        {
            return;
        }

        TrackedWindow[] current = [..windows.AllTrackedWindows];
        HashSet<nint> handles = [..current.Select(window => window.Handle)];
        foreach (nint handle in nextScan.Keys.Where(handle => !handles.Contains(handle)).ToArray())
        {
            nextScan.Remove(handle);
            index.Remove(handle);
        }

        if (Volatile.Read(ref busy) != 0)
        {
            return;
        }

        long now = Stopwatch.GetTimestamp();
        TrackedWindow? candidate = current.Where(window => index.CanRefresh(window.Handle) && nextScan.GetValueOrDefault(window.Handle) <= now).OrderBy(window => nextScan.GetValueOrDefault(window.Handle)).ThenBy(window => Math.Abs(window.CanvasX - workspace.WorkAreaX - pager.CurrentPage * (double)workspace.Width)).FirstOrDefault();
        if (candidate is null)
        {
            return;
        }

        nint target = candidate.Handle;
        nextScan[target] = now + 5 * Stopwatch.Frequency;
        CancellationToken token = cancellation.Token;
        long requestGeneration = generation;
        Interlocked.Exchange(ref busy, 1);
        _ = Task.Run(() => ScanAsync(candidate, requestGeneration, token));
    }

    private async Task ScanAsync(TrackedWindow window, long requestGeneration, CancellationToken token)
    {
        nint handle = window.Handle;
        try
        {
            WindowContentSnapshot? snapshot = await capture.TakeSnapshotAsync(handle, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (snapshot is null)
            {
                Publish(window, requestGeneration, token, null, null, null);
                return;
            }

            string fingerprint = Convert.ToHexString(SHA256.HashData(snapshot.Pixels));
            if (index.IsUnchanged(handle, fingerprint, snapshot.Width, snapshot.Height))
            {
                if (index.GetSnapshot(handle) is not null)
                {
                    return;
                }

                if (index.GetRecognition(handle) is { } cached)
                {
                    Publish(window, requestGeneration, token, fingerprint, snapshot, cached);
                    return;
                }
            }

            WindowTextRecognition recognition = await recognizer.RecognizeAsync(snapshot, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            Publish(window, requestGeneration, token, fingerprint, snapshot, recognition);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not index visible text for window {WindowHandle}", handle);
            Publish(window, requestGeneration, token, null, null, null);
        }
        finally
        {
            Interlocked.Exchange(ref busy, 0);
        }
    }

    private void Publish(TrackedWindow window, long requestGeneration, CancellationToken token, string? fingerprint, WindowContentSnapshot? snapshot, WindowTextRecognition? recognition)
    {
        dispatcher?.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (token.IsCancellationRequested || requestGeneration != generation || !configuration.SearchWindowContents || !configuration.ShowSearchBox || !windows.AllTrackedWindows.Any(current => ReferenceEquals(current, window)))
            {
                return;
            }

            if (index.TryUpdateSnapshot(window.Handle, fingerprint, snapshot, recognition))
            {
                Updated?.Invoke();
            }
        });
    }
}
