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

    public DesktopPageTitleEditor() : this(string.Empty, string.Empty, string.Empty)
    {
    }

    public DesktopPageTitleEditor(string editLabel, string saveLabel, string cancelLabel)
    {
        ViewModel = new DesktopPageTitleViewModel(editLabel, saveLabel, cancelLabel);

        InitializeComponent();

        ViewModel.PropertyChanged += HandleViewModelPropertyChanged;

        ElementCompositionPreview.SetIsTranslationEnabled(HeaderSurface, true);
        HeaderSurface.Shadow = new ThemeShadow();
        HeaderSurface.Translation = new Vector3(0, 0, ShadowDepth);

        Hide();
    }

    public DesktopPageTitleViewModel ViewModel { get; }

    public Visibility ToDisplayVisibility(bool isEditing) => isEditing ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ToEditVisibility(bool isEditing) => isEditing ? Visibility.Visible : Visibility.Collapsed;

    public void Show()
    {
        IsHitTestVisible = true;
        Opacity = 1;
    }

    public void Hide()
    {
        IsHitTestVisible = false;
        Opacity = 0;
    }

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
}
