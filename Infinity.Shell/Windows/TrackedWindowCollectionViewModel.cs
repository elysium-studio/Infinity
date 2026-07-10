using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Options;
using NavigationCompletedEventArgs = Infinity.Application.Abstractions.NavigationCompletedEventArgs;

namespace Infinity.Shell;

public partial class TrackedWindowCollectionViewModel :
    ObservableCollectionViewModel<ITrackedWindow>,
    IRecipient<OptionsChangedEventArgs<Settings>>,
    IRecipient<WindowPeekChangedEventArgs>,
    IRecipient<WindowNavigationRequestedEventArgs>,
    IRecipient<DesktopFlyoutClosedEventArgs>,
    IRecipient<TrackedWindowAddedEventArgs>,
    IRecipient<TrackedWindowRemovedEventArgs>,
    IRecipient<TrackedWindowChangedEventArgs>,
    IRecipient<WindowStackRefreshedEventArgs>,
    IRecipient<WindowCollectionWorkspaceLayoutChangedEventArgs>,
    IRecipient<WindowCollectionRefreshRequestedEventArgs>,
    IRecipient<WindowDragMovedEventArgs>,
    IRecipient<WindowDragScrolledEventArgs>,
    IRecipient<DesktopBackgroundChangedEventArgs>
{
    private readonly IDispatcher dispatcher;
    private readonly IWorkspace workspace;
    private readonly IShellLayoutCalculator calculator;
    private readonly IPager pager;
    private readonly IPanState state;
    private readonly IScroller scroller;
    private readonly IWindowCollection windowCollection;
    private readonly ITrackedWindowCollection trackedWindowCollection;
    private readonly IWindowSelector selector;
    private readonly IWindowFilterState filterState;
    private readonly IWindowPeekController peekController;
    private readonly IWindowPeekSource peekSource;
    private readonly IDesktopBackgroundController backgroundController;
    private readonly IWindowNavigationCoordinator coordinator;
    private readonly INavigator navigator;
    private readonly IOptionsMonitor<Settings> settings;
    private readonly IApplicationLifetime lifetime;

    private bool preservePageOnFilterClear;
    private bool filterSelectionResolved;
    private string lastActivatedFilterText = string.Empty;
    private IntPtr lastActivatedHandle;
    private long activationPeekSuppressUntilTicks;

    private const int ActivationPeekSuppressionMilliseconds = 300;

    [ObservableProperty]
    private string? backgroundColour;

    [ObservableProperty]
    private string? backgroundPath;

    [ObservableProperty]
    private double canvasWidth;

    [ObservableProperty]
    private double contentHeight;

    [ObservableProperty]
    private int currentPage;

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private int pageCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewSizeIndex))]
    private PreviewSize previewSize;

    [ObservableProperty]
    private bool showDesktopBackground;

    [ObservableProperty]
    private double viewportIndicatorWidth;

    public TrackedWindowCollectionViewModel(IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDispatcher dispatcher,
        IDisposer disposer,
        IWorkspace workspace,
        IShellLayoutCalculator calculator,
        IPager pager,
        IPanState state,
        IScroller scroller,
        IWindowCollection windowCollection,
        ITrackedWindowCollection trackedWindowCollection,
        IWindowSelector selector,
        IWindowFilterState filterState,
        IWindowPeekController peekController,
        IWindowPeekSource peekSource,
        IDesktopBackgroundController backgroundController,
        IWindowNavigationCoordinator coordinator,
        INavigator navigator,
        IOptionsMonitor<Settings> settings,
        IApplicationLifetime lifetime) : base(provider, factory, messenger, disposer)
    {
        this.dispatcher = dispatcher;
        this.workspace = workspace;
        this.calculator = calculator;
        this.pager = pager;
        this.state = state;
        this.scroller = scroller;
        this.windowCollection = windowCollection;
        this.trackedWindowCollection = trackedWindowCollection;
        this.selector = selector;
        this.filterState = filterState;
        this.peekController = peekController;
        this.peekSource = peekSource;
        this.backgroundController = backgroundController;
        this.coordinator = coordinator;
        this.navigator = navigator;
        this.settings = settings;
        this.lifetime = lifetime;

        IsActive = true;
    }

    public int PreviewSizeIndex => (int)PreviewSize;

    public int ScreenHeight => workspace.Height;

    public int ScreenWidth => workspace.Width;

    private double ScaleFactor => ContentHeight > 0 ? ContentHeight / ScreenHeight : 0;

    public override void Activated()
    {
        Settings current = settings.CurrentValue;
        PreviewSize = current.PreviewSize;
        ShowDesktopBackground = current.ShowDesktopBackground;

        ResetFilterState();
        Refresh();

        if (ShowDesktopBackground)
        {
            backgroundController.Subscribe();
            ApplyBackground();
        }
    }

    public override void Deactivated() => ResetFilterState();

    public void ExitApplication() => _ = lifetime.ExitAsync();

    public async void NavigateToAbout() => await navigator.NavigateAsync("AboutWindow");

    public async void NavigateToTour() => await navigator.NavigateAsync("TourWindow");

    public async void NavigateToSettings() => await navigator.NavigateAsync("SettingsWindow");

    public void NavigateToPage(int page)
    {
        if (page == CurrentPage)
        {
            return;
        }

        coordinator.NavigationTargetPage = page;
        pager.NavigateToPage(page);
    }

    public void Receive(OptionsChangedEventArgs<Settings> message)
    {
        dispatcher.Dispatch(() =>
        {
            PreviewSize = message.Options.PreviewSize;
            ShowDesktopBackground = message.Options.ShowDesktopBackground;

            if (filterState.IsActive)
            {
                peekController.Apply();
            }
            else
            {
                peekController.Clear();
            }

            windowCollection.Queue(false, false);
        });
    }

    public void Receive(WindowPeekChangedEventArgs message)
    {
        dispatcher.Dispatch(() =>
        {
            if (coordinator.NavigationTargetPage >= 0 || Environment.TickCount64 < activationPeekSuppressUntilTicks)
            {
                return;
            }

            if (message.IsPeeking)
            {
                peekSource.Handle = message.Handle;
                peekController.Apply();
            }
            else if (peekSource.Handle == message.Handle)
            {
                peekSource.Handle = default;

                if (filterState.IsActive)
                {
                    peekController.Apply();
                }
                else
                {
                    peekController.Clear();
                }
            }
        });
    }

    public void Receive(WindowNavigationRequestedEventArgs message) =>
        dispatcher.Dispatch(() => NavigateToWindowHandle(message.Handle));

    public void Receive(DesktopFlyoutClosedEventArgs message) =>
        dispatcher.Dispatch(() => FilterText = string.Empty);

    public void Receive(TrackedWindowAddedEventArgs message) =>
        dispatcher.Dispatch(Refresh);

    public void Receive(TrackedWindowRemovedEventArgs message) =>
        dispatcher.Dispatch(Refresh);

    public void Receive(TrackedWindowChangedEventArgs message) =>
        dispatcher.Dispatch(Refresh);

    public void Receive(WindowStackRefreshedEventArgs message) =>
        dispatcher.Dispatch(RefreshWindowZIndexes);

    public void Receive(WindowCollectionWorkspaceLayoutChangedEventArgs message) =>
        dispatcher.Dispatch(Refresh);

    public void Receive(WindowCollectionRefreshRequestedEventArgs message) =>
        dispatcher.Dispatch(Refresh);

    public void Receive(WindowDragMovedEventArgs message) =>
        dispatcher.Dispatch(Refresh);

    public void Receive(WindowDragScrolledEventArgs message) =>
        dispatcher.Dispatch(Refresh);

    public void Receive(DesktopBackgroundChangedEventArgs message) =>
        dispatcher.Dispatch(ApplyBackground);

    [RelayCommand]
    private void ActivateSelected()
    {
        IntPtr handle = selector.Resolve(trackedWindowCollection);

        if (handle == default)
        {
            return;
        }

        if (filterState.IsActive)
        {
            lastActivatedFilterText = FilterText;
            lastActivatedHandle = handle;
        }
        else
        {
            lastActivatedFilterText = string.Empty;
            lastActivatedHandle = default;
        }

        selector.Clear(trackedWindowCollection);
        NavigateToWindowHandle(handle);
    }

    private void ApplyBackground()
    {
        BackgroundColour = backgroundController.BackgroundColour;
        BackgroundPath = backgroundController.BackgroundPath;
    }

    private void ClearWindowFilterStates()
    {
        foreach (ITrackedWindow window in trackedWindowCollection)
        {
            window.IsFiltered = false;
        }
    }

    private void NavigateToWindowHandle(IntPtr handle)
    {
        peekSource.Handle = default;
        peekController.Apply();

        activationPeekSuppressUntilTicks = Environment.TickCount64 + ActivationPeekSuppressionMilliseconds;
        preservePageOnFilterClear = filterState.IsActive;

        coordinator.NavigateTo(handle);
    }

    partial void OnContentHeightChanged(double value)
    {
        if (value <= 0)
        {
            return;
        }

        Refresh();
    }

    partial void OnFilterTextChanged(string value)
    {
        peekSource.Handle = default;

        bool wasFilterActive = filterState.IsActive;

        filterState.Filter = value;

        if (!wasFilterActive && filterState.IsActive)
        {
            preservePageOnFilterClear = false;
            coordinator.PageBeforeFilter = (int)Math.Round(state.Offset / ScreenWidth);
            filterSelectionResolved = false;
            Messenger.Send(new FilterChangedEventArgs(true));
        }

        ApplyWindowFilter();
        selector.Clear(trackedWindowCollection);

        if (filterState.IsActive)
        {
            peekController.Apply();
            ScrollToMatch();
        }
        else
        {
            bool shouldPreservePage = preservePageOnFilterClear;
            preservePageOnFilterClear = false;

            peekController.Clear();
            ClearWindowFilterStates();

            if (coordinator.PageBeforeFilter >= 0 && !shouldPreservePage)
            {
                double targetOffset = coordinator.PageBeforeFilter * (double)ScreenWidth;
                coordinator.NavigationTargetPage = coordinator.PageBeforeFilter;
                coordinator.NavigationTargetOffset = targetOffset;
                pager.NavigateToPage(coordinator.PageBeforeFilter);
            }

            coordinator.PageBeforeFilter = -1;
            filterSelectionResolved = false;
            Messenger.Send(new FilterChangedEventArgs(false));
        }
    }

    partial void OnShowDesktopBackgroundChanged(bool value)
    {
        if (value)
        {
            backgroundController.Subscribe();
            ApplyBackground();
        }
        else
        {
            backgroundController.Unsubscribe();
            backgroundController.Clear();
        }
    }

    private void Refresh()
    {
        SynchroniseWindows();

        CanvasWidth = ScreenWidth * ScaleFactor;
        ViewportIndicatorWidth = ScreenWidth * ScaleFactor;

        int newCurrentPage = pager.CurrentPage;
        int newPageCount = pager.PageCount;

        PageCount = newPageCount;
        CurrentPage = newCurrentPage;

        if (coordinator.NavigationTargetPage >= 0)
        {
            if (Math.Abs(state.Offset - coordinator.NavigationTargetOffset) < 2)
            {
                coordinator.NavigationTargetPage = -1;
                coordinator.NavigationTargetOffset = -1;
                Messenger.Send(new NavigationCompletedEventArgs());

                if (coordinator.PendingActivation != default)
                {
                    IntPtr handle = coordinator.PendingActivation;
                    coordinator.PendingActivation = default;
                    Messenger.Send(new WindowActivationRequestedEventArgs());
                    coordinator.Activate(handle);
                }
            }
        }

        foreach (TrackedWindow trackedWindow in windowCollection.AllTrackedWindows.OrderByDescending(window => window.ZIndex))
        {
            if (!trackedWindowCollection.TryGet(trackedWindow.Handle, out ITrackedWindow? windowViewModel))
            {
                AddOrUpdateWindow(trackedWindow);

                if (!trackedWindowCollection.TryGet(trackedWindow.Handle, out windowViewModel))
                {
                    continue;
                }
            }

            ShellWindowLayout layout = calculator.Calculate(trackedWindow,
                scroller.VisualOffset,
                workspace.WorkAreaX,
                ScaleFactor,
                ScreenWidth,
                ScreenHeight);

            windowViewModel!.X = layout.X;
            windowViewModel.Y = layout.Y;
            windowViewModel.Width = layout.Width;
            windowViewModel.Height = layout.Height;
            windowViewModel.IsVisible = layout.Width > 0 && layout.Height > 0;
            windowViewModel.ZIndex = trackedWindow.ZIndex;
            windowViewModel.Title = trackedWindow.Title;
            windowViewModel.IsFiltered = !filterState.IsMatch(windowViewModel.Title);
        }
    }

    private void RefreshWindowZIndexes()
    {
        foreach (TrackedWindow trackedWindow in windowCollection.AllTrackedWindows)
        {
            if (!trackedWindowCollection.TryGet(trackedWindow.Handle, out ITrackedWindow? windowViewModel))
            {
                continue;
            }

            windowViewModel!.ZIndex = trackedWindow.ZIndex;
        }
    }

    private void SynchroniseWindows()
    {
        List<TrackedWindow> trackedWindows = [.. windowCollection.AllTrackedWindows];
        HashSet<IntPtr> current = [.. trackedWindows.Select(window => window.Handle)];

        foreach (IntPtr handle in trackedWindowCollection.Select(window => window.Handle).Where(handle => !current.Contains(handle)).ToList())
        {
            RemoveWindow(handle);
        }

        foreach (ITrackedWindow window in this.Where(window => !current.Contains(window.Handle)).ToList())
        {
            Remove(window);
        }

        foreach (TrackedWindow trackedWindow in trackedWindows)
        {
            AddOrUpdateWindow(trackedWindow);
        }
    }

    private void AddOrUpdateWindow(TrackedWindow trackedWindow)
    {
        if (!trackedWindowCollection.TryGet(trackedWindow.Handle, out ITrackedWindow? windowViewModel))
        {
            foreach (ITrackedWindow orphanedWindow in this.Where(window => window.Handle == trackedWindow.Handle).ToList())
            {
                Remove(orphanedWindow);
            }

            windowViewModel = Factory!.Create<TrackedWindowViewModel>(trackedWindow.Handle);

            trackedWindowCollection.Add(trackedWindow.Handle, windowViewModel);
            Add(windowViewModel);
        }
        else
        {
            foreach (ITrackedWindow duplicateWindow in this.Where(window =>
                         window.Handle == trackedWindow.Handle &&
                         !ReferenceEquals(window, windowViewModel)).ToList())
            {
                Remove(duplicateWindow);
            }

            if (!this.Contains(windowViewModel!))
            {
                Add(windowViewModel!);
            }
        }

        windowViewModel!.Title = trackedWindow.Title;
        windowViewModel.IsFiltered = !filterState.IsMatch(windowViewModel.Title);
    }

    private void RemoveWindow(IntPtr handle)
    {
        if (!trackedWindowCollection.TryGet(handle, out ITrackedWindow? windowViewModel))
        {
            foreach (ITrackedWindow orphanedWindow in this.Where(window => window.Handle == handle).ToList())
            {
                Remove(orphanedWindow);
            }

            return;
        }

        if (handle == selector.SelectedHandle)
        {
            selector.Clear(trackedWindowCollection);
        }

        if (handle == peekSource.Handle)
        {
            peekSource.Handle = default;

            if (filterState.IsActive)
            {
                peekController.Apply();
            }
            else
            {
                peekController.Clear();
            }
        }

        Remove(windowViewModel!);
        trackedWindowCollection.Remove(handle);

        foreach (ITrackedWindow orphanedWindow in this.Where(window => window.Handle == handle).ToList())
        {
            Remove(orphanedWindow);
        }
    }

    private void ResetFilterState()
    {
        preservePageOnFilterClear = false;
        peekSource.Handle = default;
        filterState.Filter = string.Empty;
        ApplyWindowFilter();
        coordinator.PageBeforeFilter = -1;
        coordinator.NavigationTargetPage = -1;
        filterSelectionResolved = false;
        ClearWindowFilterStates();
        peekController.Clear();

        if (FilterText != string.Empty)
        {
            FilterText = string.Empty;
            OnPropertyChanged(nameof(FilterText));
        }
    }

    private void ScrollToMatch()
    {
        ITrackedWindow? match = null;

        if (!filterSelectionResolved &&
            string.Equals(FilterText, lastActivatedFilterText, StringComparison.OrdinalIgnoreCase) &&
            lastActivatedHandle != default &&
            trackedWindowCollection.TryGet(lastActivatedHandle, out ITrackedWindow? lastActivatedWindow) &&
            !lastActivatedWindow!.IsFiltered)
        {
            match = lastActivatedWindow;
        }

        match ??= trackedWindowCollection
            .Where(window => !window.IsFiltered)
            .OrderBy(window => window.X)
            .FirstOrDefault();

        if (match is null)
        {
            return;
        }

        filterSelectionResolved = true;
        selector.Select(match);
        coordinator.NavigateToPage(match.Handle);
    }

    private void ApplyWindowFilter()
    {
        foreach (ITrackedWindow window in trackedWindowCollection)
        {
            window.IsFiltered = !filterState.IsMatch(window.Title);
        }
    }

    [RelayCommand]
    private void SelectNext() => selector.Step(true, trackedWindowCollection);

    [RelayCommand]
    private void SelectPrevious() => selector.Step(false, trackedWindowCollection);
}
