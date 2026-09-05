using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopScrollPreviewView : UserControl
{
    private const string ApplicationPickerDragProperty = "Infinity.ApplicationPin.Identifier";
    private const double TopCommandMargin = 32;
    private const double ClockTopMargin = 32;
    private const double SearchWithClockTopMargin = 144;
    private const double SettingsTopMargin = 24;
    private readonly IWindowPreviewSurface windowPreviewSurface;
    private readonly IWindowCollection windowCollection;
    private readonly IPanState panState;
    private readonly IPager pager;
    private readonly IScroller scroller;
    private readonly IWorkspace workspace;
    private readonly IScrollInputSuppression scrollInputSuppression;
    private readonly IDesktopBackgroundSource backgroundSource;
    private readonly DesktopOverviewConfiguration overviewConfiguration;
    private readonly DesktopOverviewForegroundThemeResolver foregroundThemeResolver;
    private readonly DesktopScrollPreviewAnimator animator;
    private readonly DesktopOverviewChromeAnimator chromeAnimator;
    private readonly DesktopOverviewClockController clockController;
    private readonly DesktopOverviewLayoutPresenter layoutPresenter;
    private readonly DesktopPageStrip pageStrip;
    private readonly DesktopWindowPreviewCollection previews;
    private readonly DesktopDragCursorConfinement cursorConfinement;
    private readonly DesktopApplicationDockContextMenuBuilder applicationDockContextMenuBuilder;
    private readonly DesktopApplicationDockPressAnimator applicationDockPressAnimator;
    private readonly DesktopApplicationLaunchCoordinator applicationLaunchCoordinator;
    private readonly DesktopOverviewInputController inputController;
    private readonly DesktopWindowSnapInteractionCoordinator snapInteractionCoordinator;
    private readonly ILogger<DesktopScrollPreviewView> logger;
    private CancellationTokenSource? applicationLaunchCancellation;
    private IDisposable? applicationPickerScrollSuppression;
    private bool eventsSubscribed;
    private bool isRunning;
    private double spacingProgress = 1;
    private int monitorOriginX;
    private int scrollRefreshQueued;
    private int monitorOriginY;
    private int overlayScreenOriginX;
    private int overlayScreenOriginY;
    private int workAreaOffsetX;
    private int workAreaOffsetY;
    private int monitorWidth;
    private int monitorHeight;
    private int foregroundGeneration;
    private (int X, int Y, int OffsetX, int OffsetY, int ScreenWidth, int ScreenHeight, double Width, double Height)? appliedViewport;

    public DesktopScrollPreviewView(IWindowPreviewSurface windowPreviewSurface, IWindowCollection windowCollection, IPanState panState, IPager pager, IScroller scroller, IWorkspace workspace, IScrollInputSuppression scrollInputSuppression, IDesktopBackgroundSource backgroundSource, DesktopOverviewConfiguration overviewConfiguration, DesktopOverviewForegroundThemeResolver foregroundThemeResolver, DesktopScrollPreviewAnimator animator, DesktopOverviewChromeAnimator chromeAnimator, DesktopOverviewClockController clockController, DesktopOverviewLayoutPresenter layoutPresenter, DesktopPageStrip pageStrip, DesktopWindowPreviewCollection previews, DesktopDragCursorConfinement cursorConfinement, DesktopShortcutHintsViewModel shortcutHints, DesktopApplicationPickerViewModel applicationPicker, DesktopApplicationDockViewModel applicationDock, DesktopApplicationDockContextMenuBuilder applicationDockContextMenuBuilder, DesktopApplicationDockPressAnimator applicationDockPressAnimator, DesktopApplicationLaunchCoordinator applicationLaunchCoordinator, DesktopOverviewInputController inputController, DesktopWindowSnapInteractionCoordinator snapInteractionCoordinator, ILogger<DesktopScrollPreviewView> logger)
    {
        InitializeComponent();
        this.windowPreviewSurface = windowPreviewSurface;
        this.windowCollection = windowCollection;
        this.panState = panState;
        this.pager = pager;
        this.scroller = scroller;
        this.workspace = workspace;
        this.scrollInputSuppression = scrollInputSuppression;
        this.backgroundSource = backgroundSource;
        this.overviewConfiguration = overviewConfiguration;
        this.foregroundThemeResolver = foregroundThemeResolver;
        this.animator = animator;
        this.chromeAnimator = chromeAnimator;
        this.clockController = clockController;
        this.layoutPresenter = layoutPresenter;
        this.pageStrip = pageStrip;
        this.previews = previews;
        this.cursorConfinement = cursorConfinement;
        this.applicationLaunchCoordinator = applicationLaunchCoordinator;
        this.inputController = inputController;
        this.snapInteractionCoordinator = snapInteractionCoordinator;
        this.logger = logger;
        ShortcutHints = shortcutHints;
        Clock = clockController.ViewModel;
        ApplicationPicker = applicationPicker;
        ApplicationDock = applicationDock;
        this.applicationDockContextMenuBuilder = applicationDockContextMenuBuilder;
        this.applicationDockPressAnimator = applicationDockPressAnimator;
        this.pageStrip.PageInvoked += HandlePageInvoked;
        this.pageStrip.ReorderPreviewChanged += HandlePageReorderPreviewChanged;
        this.previews.WindowInvoked += HandleWindowInvoked;
        this.previews.WindowPositionChanged += HandleWindowPositionChanged;
        this.inputController.WindowInvoked += HandleWindowInvoked;
        ApplicationPickerFlyout.Opened += HandleApplicationPickerOpened;
        ApplicationPickerFlyout.Closed += HandleApplicationPickerClosed;
        ApplicationResultsList.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(HandleApplicationResultsPointerWheelChanged), true);
        AttachApplicationDockPressHandlers(AllApplicationsButton);
        ElementCompositionPreview.SetIsTranslationEnabled(PreviewSurface, true);
        ElementCompositionPreview.SetIsTranslationEnabled(ApplicationDockSurface, true);
        ApplicationDockSurface.Shadow = new ThemeShadow();
        ApplicationDockSurface.Translation = new(0, 0, 64);
    }


    public event EventHandler? BackgroundInvoked;

    public event EventHandler? InputFocusRequested;

    public event Action<int>? PageInvoked;

    public event EventHandler? SettingsInvoked;

    public event Action<nint>? WindowInvoked;

    public bool IsRunning => isRunning;

    public DesktopShortcutHintsViewModel ShortcutHints { get; }

    public DesktopOverviewClockViewModel Clock { get; }

    public DesktopApplicationPickerViewModel ApplicationPicker { get; }

    public DesktopApplicationDockViewModel ApplicationDock { get; }


    public Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ToStaticVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ToApplicationIconVisibility(ApplicationIcon? icon) => icon is null ? Visibility.Collapsed : Visibility.Visible;


    public static ImageSource? CreateApplicationIconSource(ApplicationIcon? icon)
    {
        if (icon is null || icon.Width <= 0 || icon.Height <= 0 || icon.Pixels.Length != icon.Width * icon.Height * 4)
        {
            return null;
        }

        WriteableBitmap bitmap = new(icon.Width, icon.Height);
        using Stream stream = bitmap.PixelBuffer.AsStream();
        stream.Write(icon.Pixels);
        return bitmap;
    }


#if DEBUG
    internal void OpenApplicationPickerForDebug() => _ = OpenApplicationPickerAsync(ApplicationDockSurface, new DesktopApplicationTarget(pager.CurrentPage));

#endif
    public void Prepare(nint ownerWindowHandle, RectInt32 overlayBounds, RectInt32 monitorBounds)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => Prepare(ownerWindowHandle, overlayBounds, monitorBounds));
            return;
        }

        overlayScreenOriginX = overlayBounds.X;
        overlayScreenOriginY = overlayBounds.Y;
        monitorWidth = monitorBounds.Width;
        monitorHeight = monitorBounds.Height;
        pageStrip.SetMonitorBounds(monitorBounds.X, monitorBounds.Y, monitorBounds.Width, monitorBounds.Height);
        bool originChanged = RefreshMonitorOrigin();
        ApplyChromeSettings();
        if (overviewConfiguration.ShowClock)
        {
            clockController.Start(DispatcherQueue);
        }
        else
        {
            clockController.Stop();
        }

        cursorConfinement.SetOwner(ownerWindowHandle);
        TopChromeSurface.UpdateLayout();
        chromeAnimator.ResetTopChrome(TopChromeSurface);
        if (!isRunning)
        {
            isRunning = true;
            spacingProgress = 1;
            SubscribeEvents();
            pageStrip.Start(PageCanvas, PageShadowCanvas, PageTitleCanvas, PreviewSurface, animator.Scale);
            snapInteractionCoordinator.Start(monitorOriginX, monitorOriginY);
            Synchronise();
            QueueAdaptiveForegroundRefresh();
            SetInteractionEnabled(false);
            Opacity = 1;
        }
        else if (originChanged)
        {
            Synchronise();
            QueueAdaptiveForegroundRefresh();
        }

        windowPreviewSurface.Initialize(ownerWindowHandle);
    }


    public void AnimateInward()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(AnimateInward);
            return;
        }

        if (!isRunning)
        {
            return;
        }

        bool restoreSpacing = spacingProgress != 1;
        spacingProgress = 1;
        SetInteractionEnabled(false);
        pageStrip.SetHeadersVisible(false);
        TopChromeSurface.UpdateLayout();
        chromeAnimator.ResetTopChrome(TopChromeSurface);
        RefreshLayout(restoreSpacing ? animator.EnterDuration : null);
        ApplicationDockChrome.UpdateLayout();
        ApplicationDockList.UpdateLayout();
        chromeAnimator.AnimateDock(ApplicationDockChrome, GetApplicationDockEntranceElements());
        int pendingEntranceAnimations = 2;
        void CompleteEntranceAnimation()
        {
            pendingEntranceAnimations--;
            if (pendingEntranceAnimations == 0 && isRunning)
            {
                DispatcherQueue.TryEnqueue(() =>  {  if (isRunning)  {  ApplicationDockChrome.UpdateLayout();  ApplicationDockList.UpdateLayout();  SetInteractionEnabled(true);  }  });
            }
        }

        chromeAnimator.AnimateBottomChrome(ShortcutHintSurface, CompleteEntranceAnimation);
        animator.AnimateInward(PreviewSurface, GetAnimationWidth(), GetAnimationHeight(), () =>  {  ClearLayoutTransitions();  if (isRunning && spacingProgress == 1)  {  pageStrip.SetHeadersVisible(overviewConfiguration.ShowPageHeaders);  }   CompleteEntranceAnimation();  });
    }


    public void AnimateOutward(Action completed)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => AnimateOutward(completed));
            return;
        }

        if (!isRunning)
        {
            completed();
            return;
        }

        spacingProgress = 0;
        SetInteractionEnabled(false);
        pageStrip.SetHeadersVisible(false);
        chromeAnimator.AnimateTopChromeOutward(TopChromeSurface, () => BeginAnimateOutward(completed));
    }


    public void Deactivate()
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;
        clockController.Stop();
        foregroundGeneration++;
        cursorConfinement.Release();
        ShortcutHintsFlyout.Hide();
        ApplicationPickerFlyout.Hide();
        ReleaseApplicationPickerScrollSuppression();
        applicationLaunchCancellation?.Cancel();
        applicationLaunchCancellation?.Dispose();
        applicationLaunchCancellation = null;
        SetInteractionEnabled(false);
        WindowSearchBox.Text = string.Empty;
        previews.ClearSelection();
        inputController.ResetModifiers();
        SettingsButton.RequestedTheme = ElementTheme.Default;
        ClockSurface.RequestedTheme = ElementTheme.Default;
        ShortcutHintSurface.RequestedTheme = ElementTheme.Default;
        snapInteractionCoordinator.Stop();
        animator.Reset(PreviewSurface, GetAnimationWidth(), GetAnimationHeight());
        chromeAnimator.Reset(ApplicationDockChrome);
        chromeAnimator.Reset(GetApplicationDockEntranceElements());
        chromeAnimator.ResetBottomChrome(ShortcutHintSurface);
        chromeAnimator.ResetTopChrome(TopChromeSurface);
        UnsubscribeEvents();
        pageStrip.Stop();
        windowPreviewSurface.Clear();
        Opacity = 0;
    }


    private void Synchronise()
    {
        if (!isRunning)
        {
            return;
        }

        RefreshMonitorOrigin();
        layoutPresenter.Synchronise(PreviewBackgroundCanvas, PreviewCanvas, FocusCanvas, animator.Scale, monitorOriginX, monitorOriginY, spacingProgress);
    }


    private void RefreshLayout(TimeSpan? transitionDuration = null)
    {
        if (!isRunning)
        {
            return;
        }

        RefreshMonitorOrigin();
        layoutPresenter.Refresh(monitorOriginX, monitorOriginY, spacingProgress, transitionDuration);
        snapInteractionCoordinator.Refresh();
    }


    private void ClearLayoutTransitions()
    {
        pageStrip.ClearTranslationTransitions();
        previews.ClearTranslationTransitions();
    }


    private void SetInteractionEnabled(bool value)
    {
        DismissSurface.IsHitTestVisible = value;
        bool searchEnabled = value && overviewConfiguration.ShowSearchBox;
        bool shortcutButtonEnabled = value && overviewConfiguration.ShowKeyboardShortcutButton;
        bool dockEnabled = value && overviewConfiguration.ShowApplicationDock;
        WindowSearchBox.IsHitTestVisible = searchEnabled;
        WindowSearchBox.IsTabStop = searchEnabled;
        ShortcutHintSurface.IsHitTestVisible = shortcutButtonEnabled;
        ShortcutHintSurface.IsTabStop = shortcutButtonEnabled;
        ApplicationDockChrome.IsHitTestVisible = dockEnabled;
        ApplicationDockSurface.IsHitTestVisible = dockEnabled;
        ApplicationDockList.IsHitTestVisible = dockEnabled;
        ApplicationDockList.IsItemClickEnabled = dockEnabled;
        AllApplicationsButton.IsHitTestVisible = dockEnabled;
        AllApplicationsButton.IsTabStop = dockEnabled;
        for (int index = 0; index < ApplicationDockList.Items.Count; index++)
        {
            if (ApplicationDockList.ContainerFromIndex(index)is Control container)
            {
                container.IsHitTestVisible = dockEnabled;
            }
        }

        SettingsButton.IsHitTestVisible = value;
        SettingsButton.IsTabStop = value;
        pageStrip.SetInteractionEnabled(value);
        previews.SetInteractionEnabled(value);
        if (searchEnabled)
        {
            _ = WindowSearchBox.Focus(FocusState.Programmatic);
        }
    }


    private void BeginAnimateOutward(Action completed)
    {
        if (!isRunning)
        {
            completed();
            return;
        }

        RefreshLayout(animator.ExitDuration);
        int pendingAnimations = 2;
        void CompleteAnimation()
        {
            pendingAnimations--;
            if (pendingAnimations == 0)
            {
                ClearLayoutTransitions();
                completed();
            }
        }

        chromeAnimator.AnimateOutward(ApplicationDockChrome, GetApplicationDockEntranceElements(), ShortcutHintSurface, CompleteAnimation);
        animator.AnimateOutward(PreviewSurface, GetAnimationWidth(), GetAnimationHeight(), () =>  {  CompleteAnimation();  });
    }


    private double GetAnimationWidth() => workspace.Width > 0 ? workspace.Width : ActualWidth > 0 ? ActualWidth : XamlRoot?.Size.Width ?? 0;

    private double GetAnimationHeight() => workspace.Height > 0 ? workspace.Height : ActualHeight > 0 ? ActualHeight : XamlRoot?.Size.Height ?? 0;

    private bool RefreshMonitorOrigin()
    {
        int x = workspace.WorkAreaX;
        int y = workspace.WorkAreaY;
        int offsetX = Math.Max(0, x - overlayScreenOriginX);
        int offsetY = Math.Max(0, y - overlayScreenOriginY);
        (int X, int Y, int OffsetX, int OffsetY, int MonitorWidth, int MonitorHeight, double Width, double Height) viewport = (x, y, offsetX, offsetY, monitorWidth, monitorHeight, GetAnimationWidth(), GetAnimationHeight());
        if (appliedViewport == viewport)
        {
            return false;
        }

        appliedViewport = viewport;
        bool changed = monitorOriginX != x || monitorOriginY != y || workAreaOffsetX != offsetX || workAreaOffsetY != offsetY;
        monitorOriginX = x;
        monitorOriginY = y;
        workAreaOffsetX = offsetX;
        workAreaOffsetY = offsetY;
        PreviewSurface.Translation = new(workAreaOffsetX, workAreaOffsetY, 0);
        previews.SetCaptureViewport(DesktopCaptureViewport.Create(monitorWidth, monitorHeight, GetAnimationWidth(), GetAnimationHeight(), workAreaOffsetX, workAreaOffsetY, animator.Scale));
        UpdateTopCommandSurfaceLayout();
        ShortcutHintSurface.Margin = new(0, Math.Max(0, workAreaOffsetY + workspace.Height - 60), 24, 0);
        ApplicationDockChrome.Margin = new(0, Math.Max(0, workAreaOffsetY + workspace.Height - 88), 0, 0);
        pageStrip.SetWorkAreaOffset(workAreaOffsetX, workAreaOffsetY);
        cursorConfinement.SetWorkAreaOffsetY(workAreaOffsetY);
        snapInteractionCoordinator.UpdateMonitorOrigin(monitorOriginX, monitorOriginY);
        return changed;
    }


    private void ApplyChromeSettings()
    {
        ApplicationDockChrome.Visibility = overviewConfiguration.ShowApplicationDock ? Visibility.Visible : Visibility.Collapsed;
        ShortcutHintSurface.Visibility = overviewConfiguration.ShowKeyboardShortcutButton ? Visibility.Visible : Visibility.Collapsed;
        ClockSurface.Visibility = overviewConfiguration.ShowClock ? Visibility.Visible : Visibility.Collapsed;
        SearchSurface.Visibility = overviewConfiguration.ShowSearchBox ? Visibility.Visible : Visibility.Collapsed;
        if (!overviewConfiguration.ShowSearchBox && WindowSearchBox.Text.Length > 0)
        {
            WindowSearchBox.Text = string.Empty;
        }

        UpdateTopCommandSurfaceLayout();
    }


    private void UpdateTopCommandSurfaceLayout()
    {
        ClockSurface.Margin = new(0, workAreaOffsetY + ClockTopMargin, 0, 0);
        double searchTopMargin = overviewConfiguration.ShowClock ? SearchWithClockTopMargin : TopCommandMargin;
        TopCommandSurface.Margin = new(0, workAreaOffsetY + searchTopMargin, 0, 0);
        SettingsButton.Margin = new(0, workAreaOffsetY + SettingsTopMargin, 24, 0);
    }


    private void SubscribeEvents()
    {
        if (eventsSubscribed)
        {
            return;
        }

        eventsSubscribed = true;
        panState.OffsetChanged += HandleOffsetChanged;
        windowCollection.WindowAdded += HandleCollectionChanged;
        windowCollection.WindowRemoved += HandleRemoved;
        windowCollection.WindowChanged += HandleChanged;
        windowCollection.WindowStackRefreshed += HandleSynchroniseRequested;
        windowCollection.WorkspaceLayoutChanged += HandleSynchroniseRequested;
        windowCollection.RefreshRequested += HandleLayoutRefreshRequested;
        backgroundSource.BackgroundChanged += HandleDesktopBackgroundChanged;
    }


    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed)
        {
            return;
        }

        eventsSubscribed = false;
        panState.OffsetChanged -= HandleOffsetChanged;
        windowCollection.WindowAdded -= HandleCollectionChanged;
        windowCollection.WindowRemoved -= HandleRemoved;
        windowCollection.WindowChanged -= HandleChanged;
        windowCollection.WindowStackRefreshed -= HandleSynchroniseRequested;
        windowCollection.WorkspaceLayoutChanged -= HandleSynchroniseRequested;
        windowCollection.RefreshRequested -= HandleLayoutRefreshRequested;
        backgroundSource.BackgroundChanged -= HandleDesktopBackgroundChanged;
    }


    private void HandleCollectionChanged(object? sender, TrackedWindow trackedWindow) => QueueSynchronise();

    private void HandleRemoved(object? sender, nint handle) => QueueSynchronise();

    private void HandleChanged(object? sender, TrackedWindow trackedWindow) => QueueWindowRefresh(trackedWindow);

    private void HandleSynchroniseRequested(object? sender, EventArgs args) => QueueSynchronise();

    private void HandleLayoutRefreshRequested(object? sender, EventArgs args) => QueueLayoutRefresh();

    private void HandleDesktopBackgroundChanged(object? sender, EventArgs args) => QueueAdaptiveForegroundRefresh();

    private void HandleActualThemeChanged(FrameworkElement sender, object args) => QueueAdaptiveForegroundRefresh();

    private void QueueAdaptiveForegroundRefresh()
    {
        if (!isRunning)
        {
            return;
        }

        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(QueueAdaptiveForegroundRefresh);
            return;
        }

        int generation = ++foregroundGeneration;
        _ = ResolveAdaptiveForegroundsAsync(generation);
    }


    private async Task ResolveAdaptiveForegroundsAsync(int generation)
    {
        ElementTheme settingsTheme;
        ElementTheme clockTheme;
        ElementTheme shortcutTheme;
        try
        {
            double rasterizationScale = XamlRoot?.RasterizationScale ?? 1;
            DesktopBackground background = backgroundSource.GetBackground();
            Point settingsPoint = GetMonitorPoint(SettingsButton, rasterizationScale);
            Task<ElementTheme> settingsThemeTask = foregroundThemeResolver.ResolveAsync(overviewConfiguration.Backdrop, background, monitorWidth, monitorHeight, settingsPoint, WallpaperContrastTint.Background, ActualTheme);
            Task<ElementTheme> shortcutThemeTask;
            Task<ElementTheme> clockThemeTask;
            if (overviewConfiguration.ShowClock)
            {
                Point clockPoint = GetMonitorPoint(ClockSurface, rasterizationScale);
                clockThemeTask = foregroundThemeResolver.ResolveAsync(overviewConfiguration.Backdrop, background, monitorWidth, monitorHeight, clockPoint, WallpaperContrastTint.Background, ActualTheme);
            }
            else
            {
                clockThemeTask = Task.FromResult(ActualTheme);
            }

            if (overviewConfiguration.ShowKeyboardShortcutButton)
            {
                Point shortcutPoint = GetMonitorPoint(ShortcutHintSurface, rasterizationScale);
                shortcutThemeTask = foregroundThemeResolver.ResolveAsync(overviewConfiguration.Backdrop, background, monitorWidth, monitorHeight, shortcutPoint, WallpaperContrastTint.Background, ActualTheme);
            }
            else
            {
                shortcutThemeTask = Task.FromResult(ActualTheme);
            }

            settingsTheme = await settingsThemeTask;
            clockTheme = await clockThemeTask;
            shortcutTheme = await shortcutThemeTask;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => ApplyAdaptiveForegrounds(generation, settingsTheme, clockTheme, shortcutTheme));
            return;
        }

        ApplyAdaptiveForegrounds(generation, settingsTheme, clockTheme, shortcutTheme);
    }


    private Point GetMonitorPoint(FrameworkElement element, double rasterizationScale)
    {
        Point center = element.TransformToVisual(this).TransformPoint(new Point(element.ActualWidth / 2, element.ActualHeight / 2));
        return new(center.X * rasterizationScale, center.Y * rasterizationScale);
    }


    private void ApplyAdaptiveForegrounds(int generation, ElementTheme settingsTheme, ElementTheme clockTheme, ElementTheme shortcutTheme)
    {
        if (isRunning && generation == foregroundGeneration)
        {
            SettingsButton.RequestedTheme = settingsTheme;
            ClockSurface.RequestedTheme = clockTheme;
            ShortcutHintSurface.RequestedTheme = shortcutTheme;
        }
    }


    private void HandleOffsetChanged()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            RefreshScrollLayout();
            return;
        }

        if (Interlocked.Exchange(ref scrollRefreshQueued, 1) != 0)
        {
            return;
        }

        if (!DispatcherQueue.TryEnqueue(() =>  {  Interlocked.Exchange(ref scrollRefreshQueued, 0);  RefreshScrollLayout();  }))
        {
            Interlocked.Exchange(ref scrollRefreshQueued, 0);
        }
    }


    private void RefreshScrollLayout()
    {
        if (!isRunning)
        {
            return;
        }

        ApplicationPickerFlyout.Hide();
        RefreshLayout();
    }


    private void HandleDismissSurfaceTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;
        Dismiss();
    }


    private void HandleSettingsButtonClicked(object sender, RoutedEventArgs args)
    {
        ResetFilter();
        SetInteractionEnabled(false);
        SettingsInvoked?.Invoke(this, EventArgs.Empty);
    }


    private void HandlePageInvoked(int page)
    {
        ResetFilter();
        SetInteractionEnabled(false);
        PageInvoked?.Invoke(page);
    }


    private void HandlePageReorderPreviewChanged(DesktopPageReorderPreviewState? state, TimeSpan? transitionDuration) => layoutPresenter.SetPageReorderState(state, monitorOriginX, monitorOriginY, spacingProgress, transitionDuration);

    private void HandleWindowInvoked(nint handle)
    {
        ResetFilter();
        previews.ClearSelection();
        SetInteractionEnabled(false);
        WindowInvoked?.Invoke(handle);
    }


    private void HandleWindowPositionChanged(nint handle)
    {
        if (isRunning)
        {
            layoutPresenter.RefreshWindow(handle, monitorOriginX, monitorOriginY, spacingProgress);
        }
    }


    private async void HandleAllApplicationsClicked(object sender, RoutedEventArgs args) => await OpenApplicationPickerAsync(ApplicationDockSurface, new DesktopApplicationTarget(pager.CurrentPage));

    private async Task OpenApplicationPickerAsync(FrameworkElement anchor, DesktopApplicationTarget target)
    {
        if (!isRunning)
        {
            return;
        }

        try
        {
            await ApplicationPicker.LoadAsync(target);
            DispatcherQueue.TryEnqueue(() =>  {  if (!isRunning)  {  return;  }   ApplicationPickerFlyout.ShowAt(anchor);  _ = ApplicationSearchBox.Focus(FocusState.Programmatic);  });
        }
        catch (OperationCanceledException)
        {
        }
    }


    private async void HandleDockApplicationClicked(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is DesktopApplicationDockItemViewModel item)
        {
            await LaunchApplicationAsync(item.Application, new DesktopApplicationTarget(pager.CurrentPage), hidePicker: false);
        }
    }


    private void HandleApplicationDragItemsStarting(object sender, DragItemsStartingEventArgs args)
    {
        if (args.Items.Count == 0 || args.Items[0] is not DesktopApplicationPickerItemViewModel item)
        {
            args.Cancel = true;
            return;
        }

        args.Data.RequestedOperation = DataPackageOperation.Copy;
        args.Data.Properties[ApplicationPickerDragProperty] = item.Application.Id;
    }


    private void HandleApplicationDockDragOver(object sender, DragEventArgs args)
    {
        if (!args.DataView.Properties.ContainsKey(ApplicationPickerDragProperty))
        {
            return;
        }

        args.AcceptedOperation = DataPackageOperation.Copy;
        args.Handled = true;
    }


    private void HandleApplicationDockDrop(object sender, DragEventArgs args)
    {
        if (!args.DataView.Properties.TryGetValue(ApplicationPickerDragProperty, out object? value) || value is not string applicationIdentifier)
        {
            return;
        }

        args.AcceptedOperation = DataPackageOperation.Copy;
        args.Handled = true;
        _ = PinDroppedApplicationAsync(applicationIdentifier);
    }


    private async Task PinDroppedApplicationAsync(string applicationIdentifier)
    {
        try
        {
            if (ApplicationPicker.TryGetApplication(applicationIdentifier, out LaunchableApplication? application) && application is not null)
            {
                await ApplicationDock.PinAsync(application);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not apply the dropped application to the dock");
        }
    }


    private async void HandleApplicationDockDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) => await ApplicationDock.SaveOrderAsync();

    private void HandleApplicationDockButtonPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (GetApplicationDockIcon(sender)is FrameworkElement icon && args.GetCurrentPoint(icon).Properties.IsLeftButtonPressed)
        {
            applicationDockPressAnimator.Press(icon);
        }
    }


    private void HandleApplicationDockButtonPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (GetApplicationDockIcon(sender)is FrameworkElement icon)
        {
            applicationDockPressAnimator.Release(icon);
        }
    }


    private void HandleApplicationDockContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue)
        {
            args.ItemContainer.ContextFlyout = null;
            DetachApplicationDockPressHandlers(args.ItemContainer);
            return;
        }

        DetachApplicationDockPressHandlers(args.ItemContainer);
        AttachApplicationDockPressHandlers(args.ItemContainer);
        if (args.Item is DesktopApplicationDockItemViewModel item)
        {
            args.ItemContainer.ContextFlyout = applicationDockContextMenuBuilder.CreateUnpin(item);
        }
    }


    private void AttachApplicationDockPressHandlers(Control control)
    {
        control.AddHandler(PointerPressedEvent, new PointerEventHandler(HandleApplicationDockButtonPointerPressed), true);
        control.AddHandler(PointerReleasedEvent, new PointerEventHandler(HandleApplicationDockButtonPointerReleased), true);
        control.AddHandler(PointerCanceledEvent, new PointerEventHandler(HandleApplicationDockButtonPointerReleased), true);
        control.AddHandler(PointerCaptureLostEvent, new PointerEventHandler(HandleApplicationDockButtonPointerReleased), true);
    }


    private void DetachApplicationDockPressHandlers(Control control)
    {
        control.RemoveHandler(PointerPressedEvent, new PointerEventHandler(HandleApplicationDockButtonPointerPressed));
        control.RemoveHandler(PointerReleasedEvent, new PointerEventHandler(HandleApplicationDockButtonPointerReleased));
        control.RemoveHandler(PointerCanceledEvent, new PointerEventHandler(HandleApplicationDockButtonPointerReleased));
        control.RemoveHandler(PointerCaptureLostEvent, new PointerEventHandler(HandleApplicationDockButtonPointerReleased));
    }


    private static FrameworkElement? GetApplicationDockIcon(object sender) => sender switch
    {
        ContentControl { ContentTemplateRoot: FrameworkElement icon } => icon,
        ContentControl { Content: FrameworkElement icon } => icon,
        _ => null
    };

    private FrameworkElement[] GetApplicationDockEntranceElements()
    {
        List<FrameworkElement> elements = [];
        for (int index = 0; index < ApplicationDockList.Items.Count; index++)
        {
            if (ApplicationDockList.ContainerFromIndex(index)is FrameworkElement container && GetApplicationDockIcon(container)is FrameworkElement icon)
            {
                elements.Add(icon);
            }
        }

        if (GetApplicationDockIcon(AllApplicationsButton)is FrameworkElement allApplicationsIcon)
        {
            elements.Add(allApplicationsIcon);
        }

        return[..elements];
    }


    private void HandleApplicationPickerOpened(object? sender, object args) => applicationPickerScrollSuppression ??= scrollInputSuppression.Suppress();

    private void HandleApplicationPickerClosed(object? sender, object args) => ReleaseApplicationPickerScrollSuppression();

    private static void HandleApplicationResultsPointerWheelChanged(object sender, PointerRoutedEventArgs args) => args.Handled = true;

    private void ReleaseApplicationPickerScrollSuppression()
    {
        applicationPickerScrollSuppression?.Dispose();
        applicationPickerScrollSuppression = null;
    }


    private async void HandleApplicationClicked(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is not DesktopApplicationPickerItemViewModel item || !isRunning)
        {
            return;
        }

        await LaunchApplicationAsync(item.Application, ApplicationPicker.Target, hidePicker: true);
    }


    private async Task LaunchApplicationAsync(LaunchableApplication application, DesktopApplicationTarget target, bool hidePicker)
    {
        if (!isRunning)
        {
            return;
        }

        if (hidePicker)
        {
            ApplicationPickerFlyout.Hide();
        }

        applicationLaunchCancellation?.Cancel();
        applicationLaunchCancellation?.Dispose();
        applicationLaunchCancellation = new();
        SetInteractionEnabled(false);
        try
        {
            await applicationLaunchCoordinator.LaunchAsync(application, target, monitorOriginX, monitorOriginY, applicationLaunchCancellation.Token);
            DispatcherQueue.TryEnqueue(() =>  {  if (isRunning)  {  SetInteractionEnabled(true);  }  });
        }
        catch (OperationCanceledException)
        {
            DispatcherQueue.TryEnqueue(() =>  {  if (isRunning)  {  SetInteractionEnabled(true);  }  });
        }
    }


    private async void HandleApplicationContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue)
        {
            args.ItemContainer.ContextFlyout = null;
            return;
        }

        if (args.Item is DesktopApplicationPickerItemViewModel item)
        {
            args.ItemContainer.ContextFlyout = applicationDockContextMenuBuilder.CreatePin(item.Application);
            await ApplicationPicker.LoadIconAsync(item);
        }
    }


    private void HandleWindowSearchBoxTextChanged(object sender, TextChangedEventArgs args) => inputController.ApplyFilter(WindowSearchBox.Text, isRunning);

    private void HandleWindowSearchBoxKeyDown(object sender, KeyRoutedEventArgs args) => args.Handled = inputController.HandleKeyDown(args.Key);

    private void HandleWindowSearchBoxKeyUp(object sender, KeyRoutedEventArgs args) => inputController.HandleKeyUp(args.Key);

    private void HandleWindowSearchBoxLostFocus(object sender, RoutedEventArgs args) => inputController.ResetModifiers();

    private void HandleCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        if (!isRunning || !overviewConfiguration.ShowSearchBox || WindowSearchBox.FocusState != FocusState.Unfocused || ApplicationPickerFlyout.IsOpen || pageStrip.IsEditorActive || args.Character < 0x20 || args.Character == 0x7F)
        {
            return;
        }

        string character = char.ConvertFromUtf32((int)args.Character);
        WindowSearchBox.Text += character;
        WindowSearchBox.SelectionStart = WindowSearchBox.Text.Length;
        _ = WindowSearchBox.Focus(FocusState.Programmatic);
        args.Handled = true;
    }


    internal bool TryHandleGlobalKeyDown(int virtualKeyCode, bool controlDown, bool shiftDown, bool menuDown, bool windowsDown)
    {
        if (!isRunning || ApplicationPickerFlyout.IsOpen || pageStrip.IsEditorActive)
        {
            return false;
        }

        return inputController.TryHandleGlobalKeyDown(virtualKeyCode, controlDown, shiftDown, menuDown, windowsDown, RemoveLastFilterCharacter, AppendFilterText, FocusWindowSearchBox);
    }


    private void FocusWindowSearchBox()
    {
        if (!overviewConfiguration.ShowSearchBox)
        {
            return;
        }

        InputFocusRequested?.Invoke(this, EventArgs.Empty);
        _ = WindowSearchBox.Focus(FocusState.Programmatic);
    }


    private void RemoveLastFilterCharacter()
    {
        if (!overviewConfiguration.ShowSearchBox || WindowSearchBox.Text.Length == 0)
        {
            return;
        }

        int[] textElementOffsets = StringInfo.ParseCombiningCharacters(WindowSearchBox.Text);
        WindowSearchBox.Text = WindowSearchBox.Text[..textElementOffsets[^1]];
        WindowSearchBox.SelectionStart = WindowSearchBox.Text.Length;
    }


    private void AppendFilterText(string text)
    {
        if (!overviewConfiguration.ShowSearchBox)
        {
            return;
        }

        WindowSearchBox.Text += text;
        WindowSearchBox.SelectionStart = WindowSearchBox.Text.Length;
    }


    public void Dismiss()
    {
        ResetFilter();
        SetInteractionEnabled(false);
        BackgroundInvoked?.Invoke(this, EventArgs.Empty);
    }


    private void HandleShortcutHintsInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!isRunning || !ShortcutHintSurface.IsHitTestVisible)
        {
            return;
        }

        ShortcutHintsFlyout.ShowAt(ShortcutHintSurface);
        args.Handled = true;
    }


    public bool TryCancelEditor() => pageStrip.TryCancelEditor();

    public bool TryClearWindowSelection() => previews.TryClearMultiSelection();

    private void ResetFilter()
    {
        inputController.Reset();
        if (WindowSearchBox.Text.Length > 0)
        {
            WindowSearchBox.Text = string.Empty;
        }
    }


    private void QueueSynchronise()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            Synchronise();
        }
        else
        {
            DispatcherQueue.TryEnqueue(Synchronise);
        }
    }


    private void QueueLayoutRefresh()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            RefreshLayout();
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => RefreshLayout());
        }
    }


    private void QueueWindowRefresh(TrackedWindow trackedWindow)
    {
        void RefreshWindow()
        {
            if (isRunning && previews.TryGet(trackedWindow.Handle, out _))
            {
                previews.Refresh(trackedWindow);
                previews.RefreshSelection(windowCollection.AllTrackedWindows);
                RefreshLayout();
            }
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            RefreshWindow();
        }
        else
        {
            DispatcherQueue.TryEnqueue(RefreshWindow);
        }
    }
}
