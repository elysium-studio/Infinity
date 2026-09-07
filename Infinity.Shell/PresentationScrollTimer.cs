using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed class PresentationScrollTimer : IScrollTimer, IDisposable
{
    private readonly IScrollTimer desktopTimer;
    private readonly IScrollTimer presentationTimer;
    private readonly IScrollPresentationSession presentationSession;
    private readonly Lock syncLock = new();
    private IScrollTimer? activeTimer;
    private bool disposed;

    public PresentationScrollTimer(
        IScrollTimer desktopTimer,
        IScrollTimer presentationTimer,
        IScrollPresentationSession presentationSession)
    {
        this.desktopTimer = desktopTimer;
        this.presentationTimer = presentationTimer;
        this.presentationSession = presentationSession;
        desktopTimer.Tick += HandleDesktopTick;
        presentationTimer.Tick += HandlePresentationTick;
    }

    public event EventHandler? Tick;

    public void Start()
    {
        lock (syncLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            SelectTimer();
        }
    }

    public void Stop()
    {
        lock (syncLock)
        {
            IScrollTimer? previous = activeTimer;
            activeTimer = null;
            previous?.Stop();
        }
    }

    public void Dispose()
    {
        lock (syncLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Stop();
            desktopTimer.Tick -= HandleDesktopTick;
            presentationTimer.Tick -= HandlePresentationTick;
        }

        GC.SuppressFinalize(this);
    }

    private void HandleDesktopTick(object? sender, EventArgs args) => HandleTick(desktopTimer);

    private void HandlePresentationTick(object? sender, EventArgs args) => HandleTick(presentationTimer);

    private void HandleTick(IScrollTimer source)
    {
        lock (syncLock)
        {
            if (disposed || !ReferenceEquals(activeTimer, source))
            {
                return;
            }

            SelectTimer();
            if (ReferenceEquals(activeTimer, source))
            {
                Tick?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void SelectTimer()
    {
        IScrollTimer selected = presentationSession.IsActive ? presentationTimer : desktopTimer;
        if (ReferenceEquals(activeTimer, selected))
        {
            return;
        }

        activeTimer?.Stop();
        activeTimer = selected;
        selected.Start();
    }
}
