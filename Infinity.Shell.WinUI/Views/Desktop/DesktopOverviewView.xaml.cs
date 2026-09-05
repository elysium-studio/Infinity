using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Elysium.Platform.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Infinity.UI.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopOverviewView : DesktopOverlay
{
    private const int EscapeVirtualKey = 0x1B;
    private const int ControlVirtualKey = 0x11;
    private const int LeftControlVirtualKey = 0xA2;
    private const int RightControlVirtualKey = 0xA3;
    private const int ShiftVirtualKey = 0x10;
    private const int LeftShiftVirtualKey = 0xA0;
    private const int RightShiftVirtualKey = 0xA1;
    private const int MenuVirtualKey = 0x12;
    private const int SKeyboardVirtualKey = 0x53;
    private const int LeftWindowsVirtualKey = 0x5B;
    private const int RightWindowsVirtualKey = 0x5C;
    private const int LeftMenuVirtualKey = 0xA4;
    private const int RightMenuVirtualKey = 0xA5;
    private static readonly TimeSpan PreviewCleanupDelay = TimeSpan.FromMilliseconds(220);
    private readonly DesktopScrollPreviewView desktopScrollPreview;
    private readonly DesktopOverviewBackdropAnimator backdropAnimator;
    private readonly DesktopOverviewWallpaperPresenter wallpaperPresenter;
    private readonly WindowInputTransparencyController inputController;
    private readonly IDesktopBackgroundSource backgroundSource;
    private readonly IKeyboardInputSource keyboardInputSource;
    private readonly DesktopOverviewConfiguration overviewConfiguration;
    private readonly DesktopOverlayTopMostCoordinator topMostCoordinator;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly HashSet<int> consumedKeyUps = [];
    private readonly Lock consumedKeyUpsLock = new();
    private DesktopOverviewViewModel? subscribedViewModel;
    private DispatcherQueueTimer? previewCleanupTimer;
    private bool isCompletingDesktopPreview;
    private bool isDesktopPreviewAnimationStarted;
    private bool isScreenshotCapturePending;
    private volatile bool isOverlayOpen;
    private int globalDismissQueued;
    private int openingGeneration;

    public DesktopOverviewView(DesktopScrollPreviewView desktopScrollPreview, DesktopOverviewBackdropAnimator backdropAnimator, DesktopOverviewWallpaperPresenter wallpaperPresenter, WindowInputTransparencyController inputController, IDesktopBackgroundSource backgroundSource, IKeyboardInputSource keyboardInputSource, IWindowEventListener windowEventListener, DesktopOverviewConfiguration overviewConfiguration)
    {
        InitializeComponent();
        IsBlurEnabled = false;
        this.desktopScrollPreview = desktopScrollPreview;
        this.backdropAnimator = backdropAnimator;
        this.wallpaperPresenter = wallpaperPresenter;
        this.inputController = inputController;
        this.backgroundSource = backgroundSource;
        this.keyboardInputSource = keyboardInputSource;
        this.overviewConfiguration = overviewConfiguration;
        dispatcherQueue = DispatcherQueue;
        topMostCoordinator = new(windowEventListener, dispatcherQueue, () => isOverlayOpen && IsOpen, PromoteTopMost);
        backdropAnimator.Reset(BackgroundSurface);
        backdropAnimator.Reset(ThemeBackgroundSurface);
        backdropAnimator.Reset(BackgroundTint);
        DesktopPreviewContent.Content = desktopScrollPreview;
        desktopScrollPreview.BackgroundInvoked += HandleBackgroundInvoked;
        desktopScrollPreview.InputFocusRequested += HandleInputFocusRequested;
        desktopScrollPreview.PageInvoked += HandlePageInvoked;
        desktopScrollPreview.SettingsInvoked += HandleSettingsInvoked;
        desktopScrollPreview.WindowInvoked += HandleWindowInvoked;
        DataContextChanged += HandleDataContextChanged;
        Loaded += HandleLoaded;
        backgroundSource.BackgroundChanged += HandleBackgroundChanged;
        this.keyboardInputSource.KeyDown += HandleGlobalKeyDown;
        this.keyboardInputSource.KeyUp += HandleGlobalKeyUp;
        windowEventListener.ForegroundChanged += HandleForegroundChanged;
        topMostCoordinator.Start();
    }


    public DesktopOverviewViewModel ViewModel => (DesktopOverviewViewModel)DataContext;

#if DEBUG
    internal async Task OpenApplicationPickerForDebugAsync()
    {
        await Task.Delay(750);
        dispatcherQueue.TryEnqueue(desktopScrollPreview.OpenApplicationPickerForDebug);
    }


#endif
    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        EnsureSubscribed();
        UpdateBindings();
        PrepareBackground();
    }


    protected override void OnOpened()
    {
        IsMonitorSpanningEnabled = overviewConfiguration.IsMonitorSpanningEnabled;
        isOverlayOpen = true;
        int generation = ++openingGeneration;
        CancelPreviewCleanup();
        topMostCoordinator.PromoteNow();
        UpdateBindings();
        OpenOverview(generation);
    }


    protected override void OnClosed()
    {
        isOverlayOpen = false;
        isScreenshotCapturePending = false;
        Interlocked.Exchange(ref globalDismissQueued, 0);
        openingGeneration++;
        inputController.SetInputEnabled(Handle, false);
        SetTopMost(false);
        topMostCoordinator.Reset();
        SchedulePreviewCleanup();
    }


    private void HandleDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        EnsureSubscribed();
        UpdateBindings();
    }


    private void HandleGlobalKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Handled || !isOverlayOpen || IsEmergencyHidden)
        {
            return;
        }

        if (args.VirtualKeyCode != EscapeVirtualKey)
        {
            bool controlDown = IsAnyKeyDown(ControlVirtualKey, LeftControlVirtualKey, RightControlVirtualKey);
            bool shiftDown = IsAnyKeyDown(ShiftVirtualKey, LeftShiftVirtualKey, RightShiftVirtualKey);
            bool menuDown = IsAnyKeyDown(MenuVirtualKey, LeftMenuVirtualKey, RightMenuVirtualKey);
            bool windowsDown = IsAnyKeyDown(LeftWindowsVirtualKey, RightWindowsVirtualKey);
            if (args.VirtualKeyCode == SKeyboardVirtualKey && shiftDown && windowsDown)
            {
                YieldForScreenshotCapture();
                return;
            }

            args.Handled = desktopScrollPreview.TryHandleGlobalKeyDown(args.VirtualKeyCode, controlDown, shiftDown, menuDown, windowsDown);
            if (args.Handled)
            {
                TrackConsumedKeyUp(args.VirtualKeyCode);
            }

            return;
        }

        args.Handled = true;
        TrackConsumedKeyUp(args.VirtualKeyCode);
        if (desktopScrollPreview.TryCancelEditor())
        {
            return;
        }

        if (desktopScrollPreview.TryClearWindowSelection())
        {
            return;
        }

        if (Interlocked.Exchange(ref globalDismissQueued, 1) != 0)
        {
            return;
        }

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            Interlocked.Exchange(ref globalDismissQueued, 0);
            if (isOverlayOpen)
            {
                desktopScrollPreview.Dismiss();
            }
        }))
        {
            Interlocked.Exchange(ref globalDismissQueued, 0);
        }
    }


    private void HandleGlobalKeyUp(object? sender, KeyEventArgs args)
    {
        lock (consumedKeyUpsLock)
        {
            if (IsEmergencyHidden)
            {
                consumedKeyUps.Clear();
                return;
            }

            args.Handled = consumedKeyUps.Remove(args.VirtualKeyCode);
        }
    }


    private void TrackConsumedKeyUp(int virtualKeyCode)
    {
        lock (consumedKeyUpsLock)
        {
            consumedKeyUps.Add(virtualKeyCode);
        }
    }


    private bool IsAnyKeyDown(int key, int leftKey, int rightKey) => keyboardInputSource.IsKeyDown(key) || keyboardInputSource.IsKeyDown(leftKey) || keyboardInputSource.IsKeyDown(rightKey);

    private bool IsAnyKeyDown(int firstKey, int secondKey) => keyboardInputSource.IsKeyDown(firstKey) || keyboardInputSource.IsKeyDown(secondKey);

    private void YieldForScreenshotCapture()
    {
        isScreenshotCapturePending = true;
        topMostCoordinator.Suspend();
    }


    private void HandleForegroundChanged(nint handle)
    {
        if (!isScreenshotCapturePending || handle != Handle)
        {
            return;
        }

        isScreenshotCapturePending = false;
        dispatcherQueue.TryEnqueue(() =>
        {
            if (isOverlayOpen && IsOpen)
            {
                topMostCoordinator.Resume();
            }
        });
    }


    private void EnsureSubscribed()
    {
        DesktopOverviewViewModel? current = DataContext as DesktopOverviewViewModel;
        if (subscribedViewModel == current)
        {
            return;
        }

        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        }

        subscribedViewModel = current;
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged += HandleViewModelPropertyChanged;
        }
    }


    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DesktopOverviewViewModel.IsDesktopPreviewActive))
        {
            if (ViewModel.IsDesktopPreviewActive)
            {
                BeginDesktopPreview();
            }
            else
            {
                dispatcherQueue.TryEnqueue(() =>
                {
                    if (!ViewModel.IsDesktopPreviewActive && IsOpen)
                    {
                        ClearDesktopPreview();
                    }
                });
            }
        }

        if (args.PropertyName == nameof(DesktopOverviewViewModel.IsDesktopPreviewCompletionRequested))
        {
            if (ViewModel.IsDesktopPreviewCompletionRequested)
            {
                CompleteDesktopPreview();
            }
            else if (isCompletingDesktopPreview)
            {
                isCompletingDesktopPreview = false;
                desktopScrollPreview.AnimateInward();
            }
        }

        if (args.PropertyName == nameof(DesktopOverviewViewModel.IsDesktopPreviewReadyToClose) && ViewModel.IsDesktopPreviewReadyToClose)
        {
            FinishDesktopPreview();
        }
    }


    private void UpdateBindings()
    {
        if (!dispatcherQueue.HasThreadAccess)
        {
            dispatcherQueue.TryEnqueue(UpdateBindings);
            return;
        }

        Bindings.Update();
    }


    private void HandleBackgroundChanged(object? sender, EventArgs args)
    {
        if (overviewConfiguration.Backdrop != DesktopOverviewBackdrop.Wallpaper)
        {
            return;
        }

        if (dispatcherQueue.HasThreadAccess)
        {
            PrepareBackground();
        }
        else
        {
            dispatcherQueue.TryEnqueue(PrepareBackground);
        }
    }


    private async void PrepareBackground()
    {
        if (overviewConfiguration.Backdrop != DesktopOverviewBackdrop.Wallpaper)
        {
            return;
        }

        DesktopBackground background = backgroundSource.GetBackground();
        bool prepared = await wallpaperPresenter.PrepareAsync(BackgroundSurface, background);
        if (prepared)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                if (IsOpen)
                {
                    wallpaperPresenter.Attach(BackgroundSurface);
                }
            });
        }
    }


    private async void OpenOverview(int generation)
    {
        DesktopOverviewBackdrop backdrop = overviewConfiguration.Backdrop;
        if (backdrop == DesktopOverviewBackdrop.Wallpaper)
        {
            DesktopBackground background = backgroundSource.GetBackground();
            bool prepared = await wallpaperPresenter.PrepareAsync(BackgroundSurface, background);
            backdrop = prepared ? DesktopOverviewBackdrop.Wallpaper : DesktopOverviewBackdrop.Dark;
        }

        dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => PresentOverview(generation, backdrop));
    }


    private void PresentOverview(int generation, DesktopOverviewBackdrop backdrop)
    {
        if (generation != openingGeneration || !IsOpen)
        {
            return;
        }

        if (backdrop == DesktopOverviewBackdrop.Wallpaper && !wallpaperPresenter.Attach(BackgroundSurface))
        {
            backdrop = DesktopOverviewBackdrop.Dark;
        }

        if (backdrop == DesktopOverviewBackdrop.Wallpaper)
        {
            backdropAnimator.Reset(ThemeBackgroundSurface);
            backdropAnimator.AnimateIn(BackgroundSurface);
            backdropAnimator.AnimateIn(BackgroundTint);
        }
        else
        {
            wallpaperPresenter.Detach();
            ThemeBackgroundSurface.RequestedTheme = backdrop == DesktopOverviewBackdrop.Light ? ElementTheme.Light : ElementTheme.Dark;
            backdropAnimator.Reset(BackgroundSurface);
            backdropAnimator.Reset(BackgroundTint);
            backdropAnimator.AnimateIn(ThemeBackgroundSurface);
        }

        topMostCoordinator.PromoteNow();
        inputController.SetInputEnabled(Handle, true);
        if (ViewModel.IsDesktopPreviewActive)
        {
            BeginDesktopPreview();
        }
    }


    private void BeginDesktopPreview()
    {
        if (!dispatcherQueue.HasThreadAccess)
        {
            dispatcherQueue.TryEnqueue(BeginDesktopPreview);
            return;
        }

        desktopScrollPreview.Prepare(Handle, ScreenBounds, MonitorBounds);
        isCompletingDesktopPreview = false;
        if (!IsOpen || isDesktopPreviewAnimationStarted)
        {
            return;
        }

        isDesktopPreviewAnimationStarted = true;
        dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (desktopScrollPreview.IsRunning && IsOpen && ViewModel.IsDesktopPreviewActive && !ViewModel.IsDesktopPreviewCompletionRequested)
            {
                desktopScrollPreview.AnimateInward();
                topMostCoordinator.PromoteNow();
            }
            else
            {
                isDesktopPreviewAnimationStarted = false;
            }
        });
    }


    private void CompleteDesktopPreview()
    {
        if (!ViewModel.IsDesktopPreviewCompletionRequested || isCompletingDesktopPreview)
        {
            return;
        }

        isCompletingDesktopPreview = true;
        desktopScrollPreview.AnimateOutward(() =>
        {
            if (!isCompletingDesktopPreview)
            {
                return;
            }

            if (ViewModel.IsDesktopPreviewCompletionRequested)
            {
                ViewModel.NotifyDesktopPreviewExitAnimationCompleted();
            }
            else
            {
                isCompletingDesktopPreview = false;
            }
        });
    }


    private void FinishDesktopPreview()
    {
        if (!isCompletingDesktopPreview || !ViewModel.IsDesktopPreviewReadyToClose)
        {
            return;
        }

        isCompletingDesktopPreview = false;
        isDesktopPreviewAnimationStarted = false;
        ViewModel.CompleteDesktopPreview();
    }


    private void SchedulePreviewCleanup()
    {
        previewCleanupTimer ??= CreatePreviewCleanupTimer();
        previewCleanupTimer.Stop();
        previewCleanupTimer.Start();
    }


    private DispatcherQueueTimer CreatePreviewCleanupTimer()
    {
        DispatcherQueueTimer timer = dispatcherQueue.CreateTimer();
        timer.Interval = PreviewCleanupDelay;
        timer.IsRepeating = false;
        timer.Tick += HandlePreviewCleanupTimerTick;
        return timer;
    }


    private void HandlePreviewCleanupTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (!IsOpen)
        {
            ClearDesktopPreview();
        }
    }


    private void CancelPreviewCleanup() => previewCleanupTimer?.Stop();

    private void ClearDesktopPreview()
    {
        isCompletingDesktopPreview = false;
        isDesktopPreviewAnimationStarted = false;
        wallpaperPresenter.Detach();
        backdropAnimator.Reset(BackgroundSurface);
        backdropAnimator.Reset(ThemeBackgroundSurface);
        backdropAnimator.Reset(BackgroundTint);
        desktopScrollPreview.Deactivate();
        SetTopMost(false);
    }


    private void HandleBackgroundInvoked(object? sender, EventArgs args) => ViewModel.DismissDesktopPreview();

    private void HandleInputFocusRequested(object? sender, EventArgs args) => inputController.SetInputEnabled(Handle, true);

    private void HandlePageInvoked(int page) => ViewModel.SelectPage(page);

    private void HandleSettingsInvoked(object? sender, EventArgs args) => ViewModel.NavigateToSettings();

    private void HandleWindowInvoked(nint handle) => ViewModel.ActivateWindow(handle);
}
