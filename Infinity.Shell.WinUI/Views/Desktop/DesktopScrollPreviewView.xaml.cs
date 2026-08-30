using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopScrollPreviewView :
    UserControl
{
    private readonly IWindowPreviewSurface windowPreviewSurface;
    private readonly IWindowCollection windowCollection;
    private readonly IPanState panState;
    private readonly IPager pager;
    private readonly IScroller scroller;
    private readonly IWorkspace workspace;
    private readonly DesktopScrollPreviewAnimator animator;
    private readonly DesktopOverviewLayoutPresenter layoutPresenter;
    private readonly DesktopPageStrip pageStrip;
    private readonly DesktopWindowPreviewCollection previews;
    private readonly DesktopDragCursorConfinement cursorConfinement;
    private readonly DesktopApplicationLaunchCoordinator applicationLaunchCoordinator;
    private readonly DesktopOverviewInputController inputController;
    private readonly DesktopWindowSnapInteractionCoordinator snapInteractionCoordinator;
    private CancellationTokenSource? applicationLaunchCancellation;
    private bool eventsSubscribed;
    private bool isRunning;
    private double spacingProgress = 1;
    private int monitorOriginX;
    private int monitorOriginY;
    private int overlayScreenOriginY;
    private int workAreaOffsetY;

    public DesktopScrollPreviewView(IWindowPreviewSurface windowPreviewSurface, IWindowCollection windowCollection, IPanState panState, IPager pager, IScroller scroller, IWorkspace workspace, DesktopScrollPreviewAnimator animator, DesktopOverviewLayoutPresenter layoutPresenter, DesktopPageStrip pageStrip, DesktopWindowPreviewCollection previews, DesktopDragCursorConfinement cursorConfinement, DesktopShortcutHintsViewModel shortcutHints, DesktopApplicationPickerViewModel applicationPicker, DesktopApplicationDockViewModel applicationDock, DesktopApplicationLaunchCoordinator applicationLaunchCoordinator, DesktopOverviewInputController inputController, DesktopWindowSnapInteractionCoordinator snapInteractionCoordinator)
    {
        InitializeComponent();

        this.windowPreviewSurface = windowPreviewSurface;
        this.windowCollection = windowCollection;
        this.panState = panState;
        this.pager = pager;
        this.scroller = scroller;
        this.workspace = workspace;
        this.animator = animator;
        this.layoutPresenter = layoutPresenter;
        this.pageStrip = pageStrip;
        this.previews = previews;
        this.cursorConfinement = cursorConfinement;
        this.applicationLaunchCoordinator = applicationLaunchCoordinator;
        this.inputController = inputController;
        this.snapInteractionCoordinator = snapInteractionCoordinator;
        ShortcutHints = shortcutHints;
        ApplicationPicker = applicationPicker;
        ApplicationDock = applicationDock;

        this.pageStrip.PageInvoked += HandlePageInvoked;
        this.pageStrip.ReorderPreviewChanged += HandlePageReorderPreviewChanged;
        this.previews.WindowInvoked += HandleWindowInvoked;
        this.previews.WindowPositionChanged += HandleWindowPositionChanged;
        this.inputController.WindowInvoked += HandleWindowInvoked;

        ElementCompositionPreview.SetIsTranslationEnabled(PreviewSurface, true);
        ElementCompositionPreview.SetIsTranslationEnabled(ApplicationDockSurface, true);
        ApplicationDockSurface.Shadow = new ThemeShadow();
        ApplicationDockSurface.Translation = new Vector3(0, 0, 64);
    }

    public event EventHandler? BackgroundInvoked;

    public event EventHandler? InputFocusRequested;

    public event Action<int>? PageInvoked;

    public event EventHandler? SettingsInvoked;

    public event Action<nint>? WindowInvoked;

    public bool IsRunning => isRunning;

    public DesktopShortcutHintsViewModel ShortcutHints { get; }

    public DesktopApplicationPickerViewModel ApplicationPicker { get; }

    public DesktopApplicationDockViewModel ApplicationDock { get; }

    public Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ToStaticVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ToApplicationIconVisibility(ApplicationIcon? icon) => icon is null ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility ToFallbackIconVisibility(ApplicationIcon? icon) => icon is null ? Visibility.Visible : Visibility.Collapsed;

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

    public void Prepare(nint ownerWindowHandle, int screenOriginY)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => Prepare(ownerWindowHandle, screenOriginY));
            return;
        }

        overlayScreenOriginY = screenOriginY;
        bool originChanged = RefreshMonitorOrigin();
        cursorConfinement.SetOwner(ownerWindowHandle);
        windowPreviewSurface.Initialize(ownerWindowHandle);

        if (!isRunning)
        {
            isRunning = true;
            spacingProgress = 1;

            SubscribeEvents();
            pageStrip.Start(PageCanvas, PageShadowCanvas, PageTitleCanvas, PreviewSurface, animator.Scale);
            snapInteractionCoordinator.Start(monitorOriginX, monitorOriginY);
            Synchronise();

            SetInteractionEnabled(false);
            Opacity = 1;
        }
        else if (originChanged)
        {
            Synchronise();
        }
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
        RefreshLayout(restoreSpacing ? animator.EnterDuration : null);

        animator.AnimateInward(PreviewSurface, GetAnimationWidth(), GetAnimationHeight(), () =>
        {
            ClearLayoutTransitions();

            if (isRunning)
            {
                SetInteractionEnabled(true);
            }
        });
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
        BeginAnimateOutward(completed);
    }

    public void Deactivate()
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;
        cursorConfinement.Release();
        ShortcutHintsFlyout.Hide();
        ApplicationPickerFlyout.Hide();
        applicationLaunchCancellation?.Cancel();
        applicationLaunchCancellation?.Dispose();
        applicationLaunchCancellation = null;
        SetInteractionEnabled(false);
        WindowSearchBox.Text = string.Empty;
        previews.ClearSelection();
        inputController.ResetModifiers();
        snapInteractionCoordinator.Stop();

        animator.Reset(PreviewSurface, GetAnimationWidth(), GetAnimationHeight());

        UnsubscribeEvents();
        pageStrip.Stop();

        Opacity = 0;
    }

    private void Synchronise()
    {
        if (!isRunning)
        {
            return;
        }

        RefreshMonitorOrigin();
        layoutPresenter.Synchronise(PreviewCanvas, FocusCanvas, animator.Scale, monitorOriginX, monitorOriginY, spacingProgress);
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
        WindowSearchBox.IsHitTestVisible = value;
        WindowSearchBox.IsTabStop = value;
        ShortcutHintSurface.IsHitTestVisible = value;
        ShortcutHintSurface.IsTabStop = value;
        ApplicationDockSurface.IsHitTestVisible = value;
        AllApplicationsButton.IsTabStop = value;
        SettingsButton.IsHitTestVisible = value;
        SettingsButton.IsTabStop = value;
        pageStrip.SetInteractionEnabled(value);
        previews.SetInteractionEnabled(value);

        if (value)
        {
            _ = WindowSearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
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

        animator.AnimateOutward(PreviewSurface, GetAnimationWidth(), GetAnimationHeight(), () =>
        {
            ClearLayoutTransitions();
            completed();
        });
    }

    private double GetAnimationWidth() => ActualWidth > 0 ? ActualWidth : XamlRoot?.Size.Width ?? workspace.Width;

    private double GetAnimationHeight() => workspace.Height > 0 ? workspace.Height : ActualHeight > 0 ? ActualHeight : XamlRoot?.Size.Height ?? 0;

    private bool RefreshMonitorOrigin()
    {
        int x = workspace.WorkAreaX;
        int y = workspace.WorkAreaY;
        int offsetY = Math.Max(0, y - overlayScreenOriginY);
        bool changed = monitorOriginX != x || monitorOriginY != y || workAreaOffsetY != offsetY;
        monitorOriginX = x;
        monitorOriginY = y;
        workAreaOffsetY = offsetY;
        PreviewSurface.Translation = new Vector3(0, workAreaOffsetY, 0);
        ShortcutHintSurface.Margin = new Thickness(0, Math.Max(0, workAreaOffsetY + workspace.Height - 60), 24, 0);
        ApplicationDockSurface.Margin = new Thickness(0, Math.Max(0, workAreaOffsetY + workspace.Height - 88), 0, 0);
        pageStrip.SetWorkAreaOffsetY(workAreaOffsetY);
        cursorConfinement.SetWorkAreaOffsetY(workAreaOffsetY);
        snapInteractionCoordinator.UpdateMonitorOrigin(monitorOriginX, monitorOriginY);
        return changed;
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
    }

    private void HandleCollectionChanged(object? sender, TrackedWindow trackedWindow) => QueueSynchronise();

    private void HandleRemoved(object? sender, nint handle) => QueueSynchronise();

    private void HandleChanged(object? sender, TrackedWindow trackedWindow) => QueueWindowRefresh(trackedWindow);

    private void HandleSynchroniseRequested(object? sender, EventArgs args) => QueueSynchronise();

    private void HandleLayoutRefreshRequested(object? sender, EventArgs args) => QueueLayoutRefresh();

    private void HandleOffsetChanged()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplicationPickerFlyout.Hide();
            RefreshLayout();
        }
        else
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ApplicationPickerFlyout.Hide();
                RefreshLayout();
            });
        }
    }

    private void HandleDismissSurfaceTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;

        Dismiss();
    }

    private void HandleSettingsButtonClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs args)
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

    private void HandlePageReorderPreviewChanged(DesktopPageReorderPreviewState? state, TimeSpan? transitionDuration)
    {
        layoutPresenter.SetPageReorderState(state, monitorOriginX, monitorOriginY, spacingProgress, transitionDuration);
    }

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

    private async void HandleAllApplicationsClicked(object sender, RoutedEventArgs args)
        => await OpenApplicationPickerAsync(ApplicationDockSurface, new DesktopApplicationTarget(pager.CurrentPage));

    private async Task OpenApplicationPickerAsync(FrameworkElement anchor, DesktopApplicationTarget target)
    {
        if (!isRunning)
        {
            return;
        }

        try
        {
            await ApplicationPicker.LoadAsync(target);
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!isRunning)
                {
                    return;
                }

                ApplicationPickerFlyout.ShowAt(anchor);
                _ = ApplicationSearchBox.Focus(FocusState.Programmatic);
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void HandleDockApplicationClicked(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: DesktopApplicationPickerItemViewModel item })
        {
            await LaunchApplicationAsync(item.Application, new DesktopApplicationTarget(pager.CurrentPage), hidePicker: false);
        }
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
        applicationLaunchCancellation = new CancellationTokenSource();
        SetInteractionEnabled(false);

        try
        {
            await applicationLaunchCoordinator.LaunchAsync(application, target, monitorOriginX, monitorOriginY, applicationLaunchCancellation.Token);
            DispatcherQueue.TryEnqueue(() =>
            {
                if (isRunning)
                {
                    SetInteractionEnabled(true);
                }
            });
        }
        catch (OperationCanceledException)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (isRunning)
                {
                    SetInteractionEnabled(true);
                }
            });
        }
    }

    private async void HandleApplicationContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.Item is DesktopApplicationPickerItemViewModel item)
        {
            await ApplicationPicker.LoadIconAsync(item);
        }
    }

    private void HandleWindowSearchBoxTextChanged(object sender, TextChangedEventArgs args)
        => inputController.ApplyFilter(WindowSearchBox.Text, isRunning);

    private void HandleWindowSearchBoxKeyDown(object sender, KeyRoutedEventArgs args)
        => args.Handled = inputController.HandleKeyDown(args.Key);

    private void HandleWindowSearchBoxKeyUp(object sender, KeyRoutedEventArgs args)
        => inputController.HandleKeyUp(args.Key);

    private void HandleWindowSearchBoxLostFocus(object sender, RoutedEventArgs args)
        => inputController.ResetModifiers();

    private void HandleCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        if (!isRunning || WindowSearchBox.FocusState != FocusState.Unfocused || ApplicationPickerFlyout.IsOpen || pageStrip.IsEditorActive || args.Character < 0x20 || args.Character == 0x7F)
        {
            return;
        }

        string character = char.ConvertFromUtf32((int)args.Character);
        WindowSearchBox.Text += character;
        WindowSearchBox.SelectionStart = WindowSearchBox.Text.Length;
        _ = WindowSearchBox.Focus(FocusState.Programmatic);
        args.Handled = true;
    }

    internal bool TryHandleGlobalKeyDown(int virtualKeyCode, bool controlDown, bool shiftDown, bool menuDown)
    {
        if (!isRunning ||
            ApplicationPickerFlyout.IsOpen ||
            pageStrip.IsEditorActive)
        {
            return false;
        }

        return inputController.TryHandleGlobalKeyDown(virtualKeyCode, controlDown, shiftDown, menuDown, RemoveLastFilterCharacter, AppendFilterText, FocusWindowSearchBox);
    }

    private void FocusWindowSearchBox()
    {
        InputFocusRequested?.Invoke(this, EventArgs.Empty);
        _ = WindowSearchBox.Focus(FocusState.Programmatic);
    }

    private void RemoveLastFilterCharacter()
    {
        if (WindowSearchBox.Text.Length == 0)
        {
            return;
        }

        int[] textElementOffsets = StringInfo.ParseCombiningCharacters(WindowSearchBox.Text);
        WindowSearchBox.Text = WindowSearchBox.Text[..textElementOffsets[^1]];
        WindowSearchBox.SelectionStart = WindowSearchBox.Text.Length;
    }

    private void AppendFilterText(string text)
    {
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
