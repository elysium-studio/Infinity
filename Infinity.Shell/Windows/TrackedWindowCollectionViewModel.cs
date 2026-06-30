using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NavigationCompletedEventArgs = Infinity.Application.Abstractions.NavigationCompletedEventArgs;

namespace Infinity.Shell;

public partial class TrackedWindowCollectionViewModel :
    ObservableCollectionViewModel<ITrackedWindow>,
    IRecipient<OptionsChangedEventArgs<Settings>>
{
    private readonly IDispatcher dispatcher;
    private readonly IWorkspace workspace;
    private readonly IShellLayoutCalculator calculator;
    private readonly IPager pager;
    private readonly IPanState state;
    private readonly IScroller scroller;
    private readonly IWindowDragScroller dragScroller;
    private readonly IWindowCollection windowCollection;
    private readonly ITrackedWindowCollection trackedWindowCollection;
    private readonly IWindowSelector selector;
    private readonly IWindowFilterState filterState;
    private readonly IWindowFilterEffectController filterEffects;
    private readonly IDesktopBackgroundController backgroundController;
    private readonly IWindowPageCoordinator coordinator;
    private readonly INavigator navigator;
    private readonly IOptionsMonitor<Settings> settings;
    private readonly IApplicationLifetime lifetime;
    private readonly ILogger<TrackedWindowCollectionViewModel> logger;
    private bool activatingSelection;
    private bool subscribed;

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
        IWindowDragScroller dragScroller,
        IWindowCollection windowCollection,
        ITrackedWindowCollection trackedWindowCollection,
        IWindowSelector selector,
        IWindowFilterState filterState,
        IWindowFilterEffectController filterEffects,
        IDesktopBackgroundController backgroundController,
        IWindowPageCoordinator coordinator,
        INavigator navigator,
        IOptionsMonitor<Settings> settings,
        IApplicationLifetime lifetime,
        ILogger<TrackedWindowCollectionViewModel> logger) : base(provider, factory, messenger, disposer)
    {
        this.dispatcher = dispatcher;
        this.workspace = workspace;
        this.calculator = calculator;
        this.pager = pager;
        this.state = state;
        this.scroller = scroller;
        this.dragScroller = dragScroller;
        this.windowCollection = windowCollection;
        this.trackedWindowCollection = trackedWindowCollection;
        this.selector = selector;
        this.filterState = filterState;
        this.filterEffects = filterEffects;
        this.backgroundController = backgroundController;
        this.coordinator = coordinator;
        this.navigator = navigator;
        this.settings = settings;
        this.lifetime = lifetime;
        this.logger = logger;

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

        if (!subscribed)
        {
            subscribed = true;
            windowCollection.WindowAdded += HandleWindowAdded;
            windowCollection.WindowRemoved += HandleWindowRemoved;
            windowCollection.WindowChanged += HandleWindowChanged;
            windowCollection.ZOrderRefreshed += HandleZOrderRefreshed;
            windowCollection.WorkspaceLayoutChanged += HandleWorkspaceLayoutChanged;
            windowCollection.RefreshRequested += HandleRefreshRequested;
            dragScroller.DragMoved += HandleDragMoved;
            dragScroller.DragScrolled += HandleDragScrolled;
            backgroundController.BackgroundChanged += HandleBackgroundChanged;
            logger.LogInformation("Window collection subscribed");
        }

        ResetFilterState();
        Refresh();

        if (ShowDesktopBackground)
        {
            backgroundController.Subscribe();
            ApplyBackground();
        }
    }

    public override void Deactivated()
    {
        logger.LogInformation("Window collection deactivated");
        ResetFilterState();
    }

    public void ExitApplication()
    {
        logger.LogInformation("Exiting application");
        _ = lifetime.ExitAsync();
    }

    public async void NavigateToAbout()
    {
        logger.LogInformation("Navigating to About");
        await navigator.NavigateAsync("AboutWindow");
    }

    public async void NavigateToTour()
    {
        logger.LogInformation("Navigating to Tour");
        await navigator.NavigateAsync("TourWindow");
    }

    public async void NavigateToSettings()
    {
        logger.LogInformation("Navigating to Settings");
        await navigator.NavigateAsync("SettingsWindow");
    }

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
                filterEffects.Apply();
            }
            else
            {
                filterEffects.Clear();
            }

            windowCollection.Queue(false, false);
        });
    }

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
            filterState.RecordActivation(FilterText, handle);
        }
        else
        {
            filterState.ClearActivation();
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

    private void HandleBackgroundChanged(object? sender, EventArgs args) =>
        dispatcher.Dispatch(ApplyBackground);

    private void HandleDragMoved() =>
        dispatcher.Dispatch(Refresh);

    private void HandleDragScrolled() =>
        dispatcher.Dispatch(Refresh);

    private void HandleRefreshRequested(object? sender, EventArgs args) =>
        dispatcher.Dispatch(Refresh);

    private void HandleWindowAdded(object? sender, TrackedWindow trackedWindow) =>
        dispatcher.Dispatch(Refresh);

    private void HandleWindowChanged(object? sender, TrackedWindow trackedWindow) =>
        dispatcher.Dispatch(Refresh);

    private void HandleWindowRemoved(object? sender, IntPtr handle) =>
        dispatcher.Dispatch(Refresh);

    private void HandleWorkspaceLayoutChanged(object? sender, EventArgs args) =>
        dispatcher.Dispatch(Refresh);

    private void HandleZOrderRefreshed(object? sender, EventArgs args) =>
        dispatcher.Dispatch(ReorderWindows);

    private void NavigateToWindowHandle(IntPtr handle)
    {
        logger.LogInformation("Navigating to window: {Handle}", handle);
        activatingSelection = true;
        coordinator.NavigateTo(handle);
        activatingSelection = false;
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
        bool wasFilterActive = filterState.IsActive;

        filterState.Filter = value;

        if (!wasFilterActive && filterState.IsActive)
        {
            coordinator.PageBeforeFilter = (int)Math.Round(state.Offset / ScreenWidth);
            filterState.ResetSelectionResolved();
            Messenger.Send(new FilterChangedEventArgs(true));
        }

        filterState.Apply(trackedWindowCollection);
        selector.Clear(trackedWindowCollection);

        if (filterState.IsActive)
        {
            filterEffects.Apply();
            ScrollToMatch();
        }
        else if (coordinator.PageBeforeFilter >= 0 && !activatingSelection)
        {
            filterEffects.Clear();
            ClearWindowFilterStates();
            double targetOffset = coordinator.PageBeforeFilter * (double)ScreenWidth;
            coordinator.NavigationTargetPage = coordinator.PageBeforeFilter;
            coordinator.NavigationTargetOffset = targetOffset;
            pager.NavigateToPage(coordinator.PageBeforeFilter);
            coordinator.PageBeforeFilter = -1;
            filterState.ResetSelectionResolved();
            Messenger.Send(new FilterChangedEventArgs(false));
        }
        else
        {
            filterEffects.Clear();
            ClearWindowFilterStates();
            coordinator.PageBeforeFilter = -1;
            filterState.ResetSelectionResolved();
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

            ShellWindowLayout layout = calculator.Calculate(trackedWindow, scroller.VisualOffset, workspace.WorkAreaX, ScaleFactor, ScreenWidth, ScreenHeight);

            windowViewModel!.X = layout.X;
            windowViewModel.Y = layout.Y;
            windowViewModel.Width = layout.Width;
            windowViewModel.Height = layout.Height;
            windowViewModel.IsVisible = layout.Width > 0 && layout.Height > 0;
            windowViewModel.ZIndex = trackedWindow.ZIndex;
            windowViewModel.Title = trackedWindow.Title;
            windowViewModel.IsFiltered = !filterState.IsMatch(windowViewModel.Title);
        }

        ReorderWindows();
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

            logger.LogInformation("Window added: {Title} ({Handle})", trackedWindow.Title, trackedWindow.Handle);

            windowViewModel = Factory!.Create<TrackedWindowViewModel>(trackedWindow.Handle, (Action<IntPtr>)NavigateToWindowHandle);

            trackedWindowCollection.Add(trackedWindow.Handle, windowViewModel);
            Add(windowViewModel);
        }
        else
        {
            foreach (ITrackedWindow duplicateWindow in this.Where(window => window.Handle == trackedWindow.Handle && !ReferenceEquals(window, windowViewModel)).ToList())
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

        logger.LogInformation("Window removed: {Handle}", handle);

        if (handle == selector.SelectedHandle)
        {
            selector.Clear(trackedWindowCollection);
        }

        Remove(windowViewModel!);
        trackedWindowCollection.Remove(handle);

        foreach (ITrackedWindow orphanedWindow in this.Where(window => window.Handle == handle).ToList())
        {
            Remove(orphanedWindow);
        }
    }

    private void ReorderWindows()
    {
        List<ITrackedWindow> sorted = [.. this.OrderByDescending(window => window.ZIndex)];

        for (int index = 0; index < sorted.Count; index++)
        {
            ITrackedWindow item = sorted[index];

            if (!ReferenceEquals(this[index], item))
            {
                int currentIndex = IndexOf(item);

                if (currentIndex >= 0)
                {
                    Receive(new MoveTo<ITrackedWindow>(currentIndex, index));
                }
            }
        }
    }

    private void ResetFilterState()
    {
        filterState.Filter = string.Empty;
        filterState.Apply(trackedWindowCollection);
        coordinator.PageBeforeFilter = -1;
        coordinator.NavigationTargetPage = -1;
        filterState.ResetSelectionResolved();
        ClearWindowFilterStates();
        filterEffects.Clear();

        if (FilterText != string.Empty)
        {
            FilterText = string.Empty;
            OnPropertyChanged(nameof(FilterText));
        }
    }

    private void ScrollToMatch()
    {
        ITrackedWindow? match = null;

        if (!filterState.FilterSelectionResolved && 
                string.Equals(FilterText, filterState.LastActivatedFilterText, StringComparison.OrdinalIgnoreCase) &&
                filterState.LastActivatedHandle != default && trackedWindowCollection.TryGet(filterState.LastActivatedHandle, 
                    out ITrackedWindow? lastActivatedWindow) && !lastActivatedWindow!.IsFiltered)
        {
            match = lastActivatedWindow;
        }

        match ??= trackedWindowCollection.Where(window => !window.IsFiltered).OrderBy(window => window.X).FirstOrDefault();

        if (match is null)
        {
            return;
        }

        filterState.MarkSelectionResolved();
        selector.Select(match);
        coordinator.NavigateToPage(match.Handle);
    }

    [RelayCommand]
    private void SelectNext() => selector.Step(forward: true, trackedWindowCollection);

    [RelayCommand]
    private void SelectPrevious() => selector.Step(forward: false, trackedWindowCollection);
}