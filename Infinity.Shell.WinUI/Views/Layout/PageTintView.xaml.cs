using Elysium.UI.Controls.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using WindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Infinity.Shell.WinUI;

public sealed partial class PageTintView :
    DesktopOverlay
{
    private static readonly TimeSpan PreviewCleanupDelay = TimeSpan.FromMilliseconds(220);

    private readonly DesktopScrollPreviewView desktopScrollPreview;
    private PageTintViewModel? subscribedViewModel;
    private DispatcherQueueTimer? previewCleanupTimer;
    private bool isCompletingDesktopPreview;
    private bool isDesktopPreviewAnimationStarted;

    public PageTintView(DesktopScrollPreviewView desktopScrollPreview)
    {
        InitializeComponent();

        this.desktopScrollPreview = desktopScrollPreview;
        DesktopPreviewContent.Content = desktopScrollPreview;

        DataContextChanged += HandleDataContextChanged;
        Loaded += HandleLoaded;
    }

    public PageTintViewModel ViewModel => (PageTintViewModel)DataContext;

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        EnsureSubscribed();
        UpdateBindings();
    }

    protected override void OnOpened()
    {
        CancelPreviewCleanup();
        UpdateBindings();

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
        PageTintViewModel? current = DataContext as PageTintViewModel;

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
        if (args.PropertyName == nameof(PageTintViewModel.IsDesktopPreviewActive))
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

        if (args.PropertyName == nameof(PageTintViewModel.IsDesktopPreviewCompletionRequested))
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

        if (!IsOpen)
        {
            return;
        }

        if (isDesktopPreviewAnimationStarted)
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
        desktopScrollPreview.Clear();
        WindowExtensions.SetTopMost(Handle, false);
    }
}
