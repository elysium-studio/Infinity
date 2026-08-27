using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace Infinity.Shell;

public sealed partial class DesktopPageTitleViewModel(DesktopPageEditorLabels labels, DesktopSnapLayoutCatalog layoutCatalog) :
    ObservableObject
{
    private const double MaximumPreviewWidth = 96;
    private const double MaximumPreviewHeight = 96;

    private double configuredWidth;
    private double configuredHeight;
    private double configuredRasterizationScale;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string editingTitle = string.Empty;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private DesktopSnapLayoutKind layout;

    public event Action<DesktopPageTitleViewModel, string>? TitleSubmitted;

    public event Action<DesktopPageTitleViewModel, DesktopSnapLayoutKind>? LayoutSubmitted;

    public ObservableCollection<DesktopSnapLayoutOptionViewModel> AvailableLayouts { get; } = [];

    public string EditLabel => labels.EditTitle;

    public string SaveLabel => labels.SaveTitle;

    public string CancelLabel => labels.CancelTitle;

    public string EditLayoutLabel => labels.EditLayout;

    public string ClearLayoutLabel => labels.ClearLayout;

    public bool HasLayout => Layout != DesktopSnapLayoutKind.None;

    public bool IsDisplayMode => !IsEditing;

    public int Page { get; private set; }

    public int LayoutColumnCount { get; private set; } = 2;

    public void ConfigureDisplay(double width, double height, double rasterizationScale)
    {
        if (width == configuredWidth && height == configuredHeight && rasterizationScale == configuredRasterizationScale)
        {
            return;
        }

        configuredWidth = width;
        configuredHeight = height;
        configuredRasterizationScale = rasterizationScale;

        double aspectRatio = double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > 0 ? width / height : 1;
        double previewWidth = aspectRatio >= 1 ? MaximumPreviewWidth : MaximumPreviewHeight * aspectRatio;
        double previewHeight = aspectRatio >= 1 ? MaximumPreviewWidth / aspectRatio : MaximumPreviewHeight;
        IReadOnlyList<DesktopSnapLayoutDefinition> definitions = layoutCatalog.GetAvailable(width, height, rasterizationScale);

        AvailableLayouts.Clear();

        foreach (DesktopSnapLayoutDefinition definition in definitions)
        {
            AvailableLayouts.Add(new DesktopSnapLayoutOptionViewModel(definition, previewWidth, previewHeight));
        }

        LayoutColumnCount = AvailableLayouts.Count <= 4 ? 2 : 3;
        OnPropertyChanged(nameof(LayoutColumnCount));
        RefreshLayoutSelectionProperties();
    }

    public void Bind(int page, string value, DesktopSnapLayoutKind configuredLayout)
    {
        Page = page;
        Title = value;
        Layout = configuredLayout;

        if (!IsEditing)
        {
            EditingTitle = value;
        }
    }

    public void BeginEditing()
    {
        EditingTitle = Title;
        IsEditing = true;
    }

    public void Submit()
    {
        string value = EditingTitle;
        Cancel();
        TitleSubmitted?.Invoke(this, value);
    }

    public void Cancel()
    {
        IsEditing = false;
        EditingTitle = Title;
    }

    public void SelectLayout(DesktopSnapLayoutKind selectedLayout)
    {
        if (selectedLayout == DesktopSnapLayoutKind.None)
        {
            return;
        }

        if (Layout == selectedLayout)
        {
            RefreshLayoutSelectionProperties();
            return;
        }

        Layout = selectedLayout;
        LayoutSubmitted?.Invoke(this, selectedLayout);
    }

    public void ClearLayout()
    {
        if (!HasLayout)
        {
            return;
        }

        Layout = DesktopSnapLayoutKind.None;
        LayoutSubmitted?.Invoke(this, DesktopSnapLayoutKind.None);
    }

    public void Reset()
    {
        Cancel();
        Page = 0;
        Title = string.Empty;
        EditingTitle = string.Empty;
        Layout = DesktopSnapLayoutKind.None;
        AvailableLayouts.Clear();
        configuredWidth = 0;
        configuredHeight = 0;
        configuredRasterizationScale = 0;
    }

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDisplayMode));
    }

    partial void OnLayoutChanged(DesktopSnapLayoutKind value)
    {
        OnPropertyChanged(nameof(HasLayout));
        RefreshLayoutSelectionProperties();
    }

    private void RefreshLayoutSelectionProperties()
    {
        foreach (DesktopSnapLayoutOptionViewModel option in AvailableLayouts)
        {
            option.IsSelected = option.Kind == Layout;
            option.SetHighlighted(option.IsSelected);
        }
    }
}
