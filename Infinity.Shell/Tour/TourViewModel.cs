using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Elysium.Presentation.Abstractions;
using System.ComponentModel;

namespace Infinity.Shell;

public class TourViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IWritableOptions<Settings> writer,
    IEnumerable<ITourViewModel> items) :
    ObservableCollectionViewModel<ITourViewModel>(provider, factory, messenger, disposer, items)
{
    public event EventHandler? Finished;

    public event EventHandler? Cancelled;

    public ITourViewModel? CurrentStep => SelectedItem;

    public int SelectedIndex
    {
        get
        {
            return SelectedItem is null ? -1 : IndexOf(SelectedItem);
        }
        set
        {
            if (value >= 0 && value < Count)
            {
                SelectedItem = this[value];
            }
        }
    }

    public bool CanGoBack
    {
        get
        {
            if (SelectedItem is null)
            {
                return false;
            }

            int index = IndexOf(SelectedItem);
            return index > 0 && SelectedItem.CanGoBack;
        }
    }

    public bool CanGoNext
    {
        get
        {
            if (SelectedItem is null)
            {
                return false;
            }

            int index = IndexOf(SelectedItem);
            return index < Count - 1 && SelectedItem.CanGoNext;
        }
    }

    public bool IsLastStep => SelectedItem is not null && IndexOf(SelectedItem) == Count - 1;

    public void GoNext()
    {
        if (CanGoNext)
        {
            int index = IndexOf(SelectedItem!);
            SelectedItem = this[index + 1];
        }
    }

    public void GoBack()
    {
        if (CanGoBack)
        {
            int index = IndexOf(SelectedItem!);
            SelectedItem = this[index - 1];
        }
    }

    public void Finish()
    {
        _ = MarkCompletedAsync();
        Finished?.Invoke(this, EventArgs.Empty);
    }

    public void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    private async Task MarkCompletedAsync() => await writer.WriteAsync(settings => settings.ShowHintOnStartup = false);

    protected override void OnPropertyChanged(PropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);

        if (args.PropertyName == nameof(SelectedItem))
        {
            OnPropertyChanged(nameof(CurrentStep));
            OnPropertyChanged(nameof(SelectedIndex));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(IsLastStep));
        }
    }
}