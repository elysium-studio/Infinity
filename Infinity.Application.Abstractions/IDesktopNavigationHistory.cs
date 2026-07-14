namespace Infinity.Application.Abstractions;

public interface IDesktopNavigationHistory
{
    event EventHandler? Changed;

    bool IsEnabled { get; }

    bool CanGoBack { get; }

    bool CanGoForward { get; }

    IReadOnlyList<DesktopHistoryEntry> BackEntries { get; }

    IReadOnlyList<DesktopHistoryEntry> ForwardEntries { get; }

    bool GoBack();

    bool GoForward();

    bool NavigateTo(long entryId);

    void Clear();
}

public interface IDesktopNavigationHistoryLifetime
{
    void Start();

    void Stop();
}
