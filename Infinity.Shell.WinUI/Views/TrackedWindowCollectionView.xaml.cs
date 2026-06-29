using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.ComponentModel;
using Windows.UI;

namespace Infinity.Shell.WinUI;

public partial class TrackedWindowCollectionView :
    UserControl
{
    private string? backgroundImagePath;
    private ImageSource? backgroundImageSource;
    private string? backgroundColour;
    private SolidColorBrush? backgroundBrush;
    private bool suppressSelectionChanged;

    public TrackedWindowCollectionView()
    {
        InitializeComponent();

        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
        DataContextChanged += HandleDataContextChanged;
    }

    public TrackedWindowCollectionViewModel? ViewModel => DataContext as TrackedWindowCollectionViewModel;

    private void HandleBackdropSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.ContentHeight = args.NewSize.Height;
    }

    private void HandleDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        SyncPageIndicator();
    }

    private void HandleLoaded(object sender, RoutedEventArgs args) =>
        Focus(FocusState.Programmatic);

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        backgroundImagePath = null;
        backgroundImageSource = null;
        backgroundColour = null;
        backgroundBrush = null;
    }

    private void OnSelectedPageChanged(PipsPager sender, PipsPagerSelectedIndexChangedEventArgs args)
    {
        if (suppressSelectionChanged)
        {
            return;
        }

        ViewModel?.NavigateToPage(sender.SelectedPageIndex);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(TrackedWindowCollectionViewModel.CurrentPage) ||
            args.PropertyName == nameof(TrackedWindowCollectionViewModel.PageCount))
        {
            SyncPageIndicator();
        }
    }

    private void SyncPageIndicator()
    {
        if (ViewModel is null || ViewModel.PageCount <= 0)
        {
            return;
        }

        suppressSelectionChanged = true;
        PageIndicator.NumberOfPages = ViewModel.PageCount;
        PageIndicator.SelectedPageIndex = Math.Clamp(ViewModel.CurrentPage, 0, ViewModel.PageCount - 1);
        suppressSelectionChanged = false;
    }

    public SolidColorBrush? ToSolidColorBrush(string? hex)
    {
        if (hex is not { Length: > 0 })
        {
            backgroundColour = null;
            backgroundBrush = null;
            return null;
        }

        if (backgroundColour == hex && backgroundBrush is not null)
        {
            return backgroundBrush;
        }

        backgroundColour = hex;

        backgroundBrush = new SolidColorBrush(Color.FromArgb(
            255,
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16)));

        return backgroundBrush;
    }

    public ImageSource? ToImageSource(string? path)
    {
        if (path is not { Length: > 0 })
        {
            backgroundImagePath = null;
            backgroundImageSource = null;
            return null;
        }

        if (backgroundImagePath == path && backgroundImageSource is not null)
        {
            return backgroundImageSource;
        }

        backgroundImagePath = path;
        backgroundImageSource = new BitmapImage(new Uri(path));

        return backgroundImageSource;
    }

    public Visibility ToVisibility(string? path) =>
        path is { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
}