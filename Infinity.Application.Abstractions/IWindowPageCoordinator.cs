namespace Infinity.Application.Abstractions;

public interface IWindowPageCoordinator
{
    event EventHandler<NavigationStartedEventArgs>? NavigationStarted;

    event EventHandler? WindowActivationRequested;

    int NavigationTargetPage { get; set; }

    double NavigationTargetOffset { get; set; }

    int PageBeforeFilter { get; set; }

    IntPtr PendingActivation { get; set; }

    void NavigateTo(IntPtr handle);

    void NavigateToPage(IntPtr handle);

    void HandleForegroundWindowChanged(IntPtr handle);

    void HandleWindowMinimizeStarted(IntPtr handle);

    void HandleWindowMinimizeEnded(IntPtr handle);

    void NotifyWindowClosed(IntPtr handle);

    void ExpectProgrammaticActivation(IntPtr handle);

    void Activate(IntPtr handle);
}