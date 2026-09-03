using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Numerics;
using Windows.System;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopPageTitleEditor :
    UserControl,
    IDisposable
{
    private const float ShadowDepth = 64;

    private bool disposed;

    public DesktopPageTitleEditor() : this(new DesktopPageEditorLabels(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty), new DesktopSnapLayoutCatalog())
    {
    }

    public DesktopPageTitleEditor(DesktopPageEditorLabels labels, DesktopSnapLayoutCatalog layoutCatalog)
    {
        ViewModel = new DesktopPageTitleViewModel(labels, layoutCatalog);

        InitializeComponent();

        ViewModel.PropertyChanged += HandleViewModelPropertyChanged;

        ElementCompositionPreview.SetIsTranslationEnabled(HeaderSurface, true);
        HeaderSurface.Shadow = new ThemeShadow();
        HeaderSurface.Translation = new Vector3(0, 0, ShadowDepth);

    }

    public DesktopPageTitleViewModel ViewModel { get; }

    public Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static double ToOpacity(bool value) => value ? 1 : 0;

    public void SetInteractionEnabled(bool value)
    {
        IsHitTestVisible = value;

        if (!value)
        {
            CloseLayoutFlyout();
        }
    }

    public void CloseLayoutFlyout() => LayoutFlyout.Hide();

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        ViewModel.PropertyChanged -= HandleViewModelPropertyChanged;

        GC.SuppressFinalize(this);
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ViewModel.IsEditing) || !ViewModel.IsEditing)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            _ = TitleTextBox.Focus(FocusState.Programmatic);
            TitleTextBox.SelectAll();
        });
    }

    private void HandleEditorKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter)
        {
            args.Handled = true;
            ViewModel.Submit();
        }
        else if (args.Key == VirtualKey.Escape)
        {
            args.Handled = true;
            ViewModel.Cancel();
        }
    }

    private void HandleLayoutClicked(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: DesktopSnapLayoutOptionViewModel option })
        {
            ViewModel.SelectLayout(option.Kind);
            LayoutFlyout.Hide();
        }
    }

    private void HandleClearLayoutClicked(object sender, RoutedEventArgs args)
    {
        ViewModel.ClearLayout();
        LayoutFlyout.Hide();
    }

    private void HandleTitleEditButtonInteractionChanged(object sender, RoutedEventArgs args) =>
        TitleEditIcon.Opacity = TitleEditButton.IsPointerOver || TitleEditButton.FocusState != FocusState.Unfocused ? 1 : 0;

    private void HandleLayoutInteractionStateChanged(object sender, RoutedEventArgs args)
    {
        if (sender is DesktopSnapLayoutOptionButton { Tag: DesktopSnapLayoutOptionViewModel option } button)
        {
            option.SetInteractionState(button.InteractionState);
        }
    }

}
