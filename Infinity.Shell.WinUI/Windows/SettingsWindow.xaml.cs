using Elysium.Application.Abstractions;
using Elysium.Presentation.Abstractions;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Graphics;

namespace Infinity.Shell.WinUI;

public sealed partial class SettingsWindow :
    Window
{
    private const int WindowWidth = 1100;
    private const int WindowHeight = 680;
    private readonly AboutViewModel aboutViewModel;
    private readonly IApplicationLifetime applicationLifetime;
    private readonly ITextLocalizer localizer;
    private readonly INavigator navigator;
    private bool isAboutDialogOpen;
    private bool isClosing;
    private bool isQuitDialogOpen;
    private bool isTourOpening;

    public SettingsWindow(ITextLocalizer localizer,
        IApplicationLifetime applicationLifetime,
        INavigator navigator,
        AboutViewModel aboutViewModel)
    {
        InitializeComponent();

        this.localizer = localizer;
        this.applicationLifetime = applicationLifetime;
        this.navigator = navigator;
        this.aboutViewModel = aboutViewModel;

        Closed += HandleClosed;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        OverlappedPresenter presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.IsResizable = false;
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;

        DisplayArea displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);

        int centeredX = displayArea.WorkArea.X + (displayArea.WorkArea.Width / 2) - (WindowWidth / 2);
        int centeredY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height / 2) - (WindowHeight / 2);

        AppWindow.MoveAndResize(new RectInt32(centeredX, centeredY, WindowWidth, WindowHeight));
    }

    public SettingsViewModel ViewModel => field ??= (SettingsViewModel)((FrameworkElement)Content).DataContext;

    private async void HandleQuitTapped(object sender,
        Microsoft.UI.Xaml.Input.TappedRoutedEventArgs args)
    {
        if (isQuitDialogOpen)
        {
            return;
        }

        isQuitDialogOpen = true;

        try
        {
            QuitDialog dialog = new(localizer)
            {
                XamlRoot = ((FrameworkElement)Content).XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                isClosing = true;
                QuitInfinityNavigationItem.IsEnabled = false;
                Close();
                await applicationLifetime.ExitAsync();
            }
        }
        finally
        {
            isQuitDialogOpen = false;
        }
    }

    private async void HandleAboutTapped(object sender,
        Microsoft.UI.Xaml.Input.TappedRoutedEventArgs args)
    {
        if (isAboutDialogOpen)
        {
            return;
        }

        isAboutDialogOpen = true;

        try
        {
            AboutDialog dialog = new(aboutViewModel,
                localizer)
            {
                XamlRoot = ((FrameworkElement)Content).XamlRoot
            };
            _ = await dialog.ShowAsync();
        }
        finally
        {
            isAboutDialogOpen = false;
        }
    }

    private async void HandleTourTapped(object sender,
        Microsoft.UI.Xaml.Input.TappedRoutedEventArgs args)
    {
        if (isTourOpening)
        {
            return;
        }

        isTourOpening = true;
        TourNavigationItem.IsEnabled = false;

        try
        {
            await navigator.NavigateAsync("TourWindow");
        }
        finally
        {
            isTourOpening = false;

            if (!isClosing)
            {
                TourNavigationItem.IsEnabled = true;
            }
        }
    }

    private void HandleClosed(object sender,
        WindowEventArgs args)
    {
        isClosing = true;
        Closed -= HandleClosed;
    }
}
