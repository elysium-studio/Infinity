namespace Infinity.Application.Abstractions;

public interface IWindowPageCoordinator
{
    event EventHandler<NavigationStartedEventArgs>? NavigationStarted;

    event EventHandler? WindowActivationRequested;

    double NavigationTargetOffset { get; set; }

    int NavigationTargetPage { get; set; }

    int PageBeforeFilter { get; set; }

    IntPtr PendingActivation { get; set; }

    void Activate(IntPtr handle);

    void ExpectProgrammaticActivation(IntPtr handle);

    void HandleFocusChanged(IntPtr handle);

    void NavigateTo(IntPtr handle);

    void NavigateToPage(IntPtr handle);

    void NotifyWindowClosed(IntPtr handle);
}