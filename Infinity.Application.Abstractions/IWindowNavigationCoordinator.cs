namespace Infinity.Application.Abstractions;

public interface IWindowNavigationCoordinator
{
    event EventHandler<NavigationStartedEventArgs>? NavigationStarted;

    event EventHandler? NavigationCompleted;

    event EventHandler? WindowActivationRequested;

    int NavigationTargetPage { get; set; }


    double NavigationTargetOffset { get; set; }


    IntPtr PendingActivation { get; set; }


    void NavigateTo(IntPtr handle);

    void NavigateToPage(IntPtr handle);

    void Activate(IntPtr handle);

    void CancelNavigation();

    void CompleteNavigation();
}
