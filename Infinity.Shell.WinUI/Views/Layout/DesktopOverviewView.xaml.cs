using Elysium.UI.Controls.WinUI;
using Infinity.Platform.Windows;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using WindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopOverviewView :
    DesktopOverlay
{
    private static readonly TimeSpan PreviewCleanupDelay = TimeSpan.FromMilliseconds(220);

    private readonly DesktopScrollPreviewView desktopScrollPreview;
    private readonly DesktopOverviewBackdropAnimator backdropAnimator;
    private readonly WindowInputTransparencyController inputController;
    private DesktopOverviewViewModel? subscribedViewModel;
    private DispatcherQueueTimer? previewCleanupTimer;
    private bool isCompletingDesktopPreview;
    private bool isDesktopPreviewAnimationStarted;

    public DesktopOverviewView(DesktopScrollPreviewView desktopScrollPreview,
        DesktopOverviewBackdropAnimator backdropAnimator,
        WindowInputTransparencyController inputController)
    {
        InitializeComponent();

        this.desktopScrollPreview = desktopScrollPreview;
        this.backdropAnimator = backdropAnimator;
        this.inputController = inputController;
        DesktopPreviewContent.Content = desktopScrollPreview;
        desktopScrollPreview.BackgroundInvoked += HandleBackgroundInvoked;
        desktopScrollPreview.PageInvoked += HandlePageInvoked;
        desktopScrollPreview.WindowInvoked += HandleWindowInvoked;

        DataContextChanged += HandleDataContextChanged;
        Loaded += HandleLoaded;
    }

    public DesktopOverviewViewModel ViewModel => (DesktopOverviewViewModel)DataContext;

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        EnsureSubscribed();
        UpdateBindings();
    }

    protected override void OnOpened()
    {
        CancelPreviewCleanup();
        UpdateBindings();
        backdropAnimator.AnimateIn(DesktopBackdrop);
        DispatcherQueue.TryEnqueue(() => inputController.SetInputEnabled(Handle, true));

        if (ViewModel.IsDesktopPreviewActive)
        {
            BeginDesktopPreview();
        }
        else
        {
            ClearDesktopPreview();
        }
    }

    protected override void OnClosed() => SchedulePreviewCleanup();

    private void HandleDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        EnsureSubscribed();
        UpdateBindings();
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
                DispatcherQueue.TryEnqueue(() =>
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
                backdropAnimator.AnimateIn(DesktopBackdrop);
                desktopScrollPreview.AnimateInward();
            }
        }
    }

    private void UpdateBindings()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(UpdateBindings);
            return;
        }

        Bindings.Update();
    }

    private void BeginDesktopPreview()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(BeginDesktopPreview);
            return;
        }

        desktopScrollPreview.Prepare(Handle);
        isCompletingDesktopPreview = false;

        if (!IsOpen || isDesktopPreviewAnimationStarted)
        {
            return;
        }

        isDesktopPreviewAnimationStarted = true;

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (desktopScrollPreview.IsRunning && IsOpen && ViewModel.IsDesktopPreviewActive &&
                !ViewModel.IsDesktopPreviewCompletionRequested)
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
        backdropAnimator.AnimateOut(DesktopBackdrop);

        desktopScrollPreview.AnimateOutward(() =>
        {
            if (!isCompletingDesktopPreview)
            {
                return;
            }

            isCompletingDesktopPreview = false;

            if (ViewModel.IsDesktopPreviewCompletionRequested)
            {
                isDesktopPreviewAnimationStarted = false;
                ViewModel.CompleteDesktopPreview();
            }
        });
    }

    private void SchedulePreviewCleanup()
    {
        previewCleanupTimer ??= CreatePreviewCleanupTimer();
        previewCleanupTimer.Stop();
        previewCleanupTimer.Start();
    }

    private DispatcherQueueTimer CreatePreviewCleanupTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
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
        backdropAnimator.Reset(DesktopBackdrop);
        desktopScrollPreview.Clear();
        WindowExtensions.SetTopMost(Handle, false);
    }

    private void HandleBackgroundInvoked(object? sender, EventArgs args) =>
        ViewModel.DismissDesktopPreview();

    private void HandlePageInvoked(int page) => ViewModel.SelectPage(page);

    private void HandleWindowInvoked(nint handle) => ViewModel.ActivateWindow(handle);
}
