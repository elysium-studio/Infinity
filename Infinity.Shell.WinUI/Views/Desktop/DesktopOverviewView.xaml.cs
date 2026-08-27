using Elysium.Platform.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;
using Infinity.UI.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Threading;
using WindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopOverviewView :
    DesktopOverlay
{
    private const int EscapeVirtualKey = 0x1B;

    private static readonly TimeSpan PreviewCleanupDelay = TimeSpan.FromMilliseconds(220);

    private readonly DesktopScrollPreviewView desktopScrollPreview;
    private readonly DesktopOverviewBackdropAnimator backdropAnimator;
    private readonly DesktopOverviewWallpaperPresenter wallpaperPresenter;
    private readonly WindowInputTransparencyController inputController;
    private readonly IDesktopBackgroundSource backgroundSource;
    private readonly IKeyboardInputSource keyboardInputSource;
    private readonly DesktopOverviewConfiguration overviewConfiguration;
    private readonly DispatcherQueue dispatcherQueue;
    private DesktopOverviewViewModel? subscribedViewModel;
    private DispatcherQueueTimer? previewCleanupTimer;
    private bool isCompletingDesktopPreview;
    private bool isDesktopPreviewAnimationStarted;
    private volatile bool isOverlayOpen;
    private int globalDismissQueued;
    private int openingGeneration;

    public DesktopOverviewView(DesktopScrollPreviewView desktopScrollPreview, DesktopOverviewBackdropAnimator backdropAnimator, DesktopOverviewWallpaperPresenter wallpaperPresenter, WindowInputTransparencyController inputController, IDesktopBackgroundSource backgroundSource, IKeyboardInputSource keyboardInputSource, DesktopOverviewConfiguration overviewConfiguration)
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
    }

    public DesktopOverviewViewModel ViewModel => (DesktopOverviewViewModel)DataContext;

#if DEBUG
    internal async System.Threading.Tasks.Task OpenApplicationPickerForDebugAsync()
    {
        await System.Threading.Tasks.Task.Delay(750);
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
        UpdateBindings();
        OpenOverview(generation);
    }

    protected override void OnClosed()
    {
        isOverlayOpen = false;
        Interlocked.Exchange(ref globalDismissQueued, 0);
        openingGeneration++;
        inputController.SetInputEnabled(Handle, false);
        WindowExtensions.SetTopMost(Handle, false);
        SchedulePreviewCleanup();
    }

    private void HandleDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        EnsureSubscribed();
        UpdateBindings();
    }

    private void HandleGlobalKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.VirtualKeyCode != EscapeVirtualKey || !isOverlayOpen)
        {
            return;
        }

        args.Handled = true;

        if (desktopScrollPreview.TryCancelEditor())
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

    private void HandleViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
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
        DesktopBackground background = backgroundSource.GetBackground();
        bool prepared = await wallpaperPresenter.PrepareAsync(BackgroundSurface, background);

        if (!prepared)
        {
            return;
        }

        dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => PresentOverview(generation));
    }

    private void PresentOverview(int generation)
    {
        if (generation != openingGeneration || !IsOpen || !wallpaperPresenter.Attach(BackgroundSurface))
        {
            return;
        }

        WindowExtensions.SetTopMost(Handle, true);
        inputController.SetInputEnabled(Handle, true);
        backdropAnimator.AnimateIn(BackgroundSurface);
        backdropAnimator.AnimateIn(BackgroundTint);

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

        desktopScrollPreview.Prepare(Handle, ScreenBounds.Y);
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
                WindowExtensions.SetTopMost(Handle, true);
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
        backdropAnimator.Reset(BackgroundTint);
        desktopScrollPreview.Deactivate();
        WindowExtensions.SetTopMost(Handle, false);
    }

    private void HandleBackgroundInvoked(object? sender, EventArgs args) => ViewModel.DismissDesktopPreview();

    private void HandleInputFocusRequested(object? sender, EventArgs args) => inputController.SetInputEnabled(Handle, true);

    private void HandlePageInvoked(int page) => ViewModel.SelectPage(page);

    private void HandleSettingsInvoked(object? sender, EventArgs args) => ViewModel.NavigateToSettings();

    private void HandleWindowInvoked(nint handle) => ViewModel.ActivateWindow(handle);
}
