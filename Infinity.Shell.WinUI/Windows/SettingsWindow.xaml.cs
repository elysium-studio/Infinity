using Elysium.Application.Abstractions;
using Elysium.Presentation.Abstractions;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    private readonly Dictionary<ISettingViewModel, NavigationViewItem> navigationItems = [];
    private readonly List<ISettingViewModel> navigationPath = [];
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

    public ObservableCollection<string> BreadcrumbItems { get; } = [];

    public SettingsViewModel ViewModel => field ??= (SettingsViewModel)((FrameworkElement)Content).DataContext;

    private void HandleLoaded(object sender,
        RoutedEventArgs args)
    {
        if (!isClosing &&
            ((FrameworkElement)Content).DataContext is SettingsViewModel)
        {
            BuildNavigation();
        }
    }

    private void HandleNavigationSelectionChanged(NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (isClosing ||
            args.SelectedItem is not NavigationViewItem item ||
            item.Tag is not ISettingViewModel selectedItem)
        {
            return;
        }

        List<ISettingViewModel>? path = FindNavigationPath(selectedItem);

        if (path is null)
        {
            return;
        }

        if (selectedItem.Children.Count > 0)
        {
            path.Add(selectedItem.Children[0]);
            SettingsNavigation.SelectedItem = navigationItems[selectedItem.Children[0]];
        }

        Navigate(path);
    }

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

    private void HandleBackRequested(TitleBar sender,
        object args) => GoBack();

    private void HandleBreadcrumbItemClicked(BreadcrumbBar sender,
        BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index < 0 ||
            args.Index >= navigationPath.Count - 1)
        {
            return;
        }

        List<ISettingViewModel> path = [.. navigationPath.Take(args.Index + 1)];
        ISettingViewModel target = path[^1];

        if (target.Children.Count > 0)
        {
            target = target.Children[0];
            path.Add(target);
        }

        if (navigationItems.TryGetValue(target, out NavigationViewItem? item))
        {
            SettingsNavigation.SelectedItem = item;
        }

        Navigate(path);
    }

    private void HandleClosed(object sender,
        WindowEventArgs args)
    {
        isClosing = true;
        navigationItems.Clear();
        navigationPath.Clear();
        Closed -= HandleClosed;
    }

    private void GoBack()
    {
        if (navigationPath.Count < 2)
        {
            return;
        }

        List<ISettingViewModel> path = [.. navigationPath.Take(navigationPath.Count - 1)];
        ISettingViewModel target = path[^1];

        if (navigationItems.TryGetValue(target, out NavigationViewItem? item))
        {
            SettingsNavigation.SelectedItem = item;
        }

        Navigate(path);
    }

    private void BuildNavigation()
    {
        SettingsNavigation.MenuItems.Clear();
        navigationItems.Clear();

        foreach (ISettingViewModel root in ViewModel)
        {
            SettingsNavigation.MenuItems.Add(CreateNavigationItem(root));
        }

        ISettingViewModel? initial = ViewModel.FirstOrDefault();

        if (initial is null)
        {
            return;
        }

        List<ISettingViewModel> path = [initial];

        if (initial.Children.Count > 0)
        {
            path.Add(initial.Children[0]);
        }

        ISettingViewModel selectedItem = path.Last(navigationItems.ContainsKey);
        SettingsNavigation.SelectedItem = navigationItems[selectedItem];
        Navigate(path);
    }

    private NavigationViewItem CreateNavigationItem(ISettingViewModel viewModel)
    {
        NavigationViewItem item = new()
        {
            Content = viewModel.Title,
            IsExpanded = true,
            Margin = new Thickness(8, 0, 0, 0),
            Tag = viewModel
        };

        if (!string.IsNullOrEmpty(viewModel.Glyph))
        {
            item.Icon = new FontIcon { Glyph = viewModel.Glyph };
        }

        navigationItems[viewModel] = item;

        foreach (ISettingViewModel child in viewModel.Children)
        {
            item.MenuItems.Add(CreateNavigationItem(child));
        }

        return item;
    }

    private List<ISettingViewModel>? FindNavigationPath(ISettingViewModel target)
    {
        foreach (ISettingViewModel root in ViewModel)
        {
            List<ISettingViewModel> path = [];

            if (TryFindNavigationPath(root, target, path))
            {
                return path;
            }
        }

        return null;
    }

    private static bool TryFindNavigationPath(ISettingViewModel current,
        ISettingViewModel target,
        List<ISettingViewModel> path)
    {
        path.Add(current);

        if (ReferenceEquals(current, target))
        {
            return true;
        }

        foreach (ISettingViewModel child in current.Children)
        {
            if (TryFindNavigationPath(child, target, path))
            {
                return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }

    private void Navigate(IReadOnlyList<ISettingViewModel> path)
    {
        if (isClosing || path.Count == 0)
        {
            return;
        }

        navigationPath.Clear();
        navigationPath.AddRange(path);

        BreadcrumbItems.Clear();

        foreach (ISettingViewModel item in path)
        {
            BreadcrumbItems.Add(item.Title);
        }

        ViewModel.NavigateTo(path[^1]);
        bool canGoBack = path.Count > 2;
        AppTitleBar.IsBackButtonEnabled = canGoBack;
        AppTitleBar.IsBackButtonVisible = canGoBack;
    }
}