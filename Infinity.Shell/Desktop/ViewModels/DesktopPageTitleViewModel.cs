using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Infinity.Shell;

public sealed partial class DesktopPageTitleViewModel(string editLabel, string saveLabel, string cancelLabel) :
    ObservableObject
{
    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string editingTitle = string.Empty;

    [ObservableProperty]
    private bool isEditing;

    public event Action<DesktopPageTitleViewModel, string>? TitleSubmitted;

    public string EditLabel { get; } = editLabel;

    public string SaveLabel { get; } = saveLabel;

    public string CancelLabel { get; } = cancelLabel;

    public int Page { get; private set; }

    public void Bind(int page, string value)
    {
        Page = page;
        Title = value;

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

    public void Reset()
    {
        Cancel();
        Page = 0;
        Title = string.Empty;
        EditingTitle = string.Empty;
    }
}
