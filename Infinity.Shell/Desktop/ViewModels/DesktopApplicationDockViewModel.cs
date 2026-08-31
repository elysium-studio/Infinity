using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;
using System.Collections.ObjectModel;

namespace Infinity.Shell;

public sealed partial class DesktopApplicationDockViewModel :
    ObservableObject
{
    private const int MaximumApplications = 6;

    private readonly IRecentApplicationStore recentApplicationStore;
    private readonly IDesktopApplicationDockCatalog dockCatalog;
    private readonly IDesktopApplicationPickerCatalog applicationCatalog;
    private readonly IDispatcher dispatcher;

    public DesktopApplicationDockViewModel(
        IRecentApplicationStore recentApplicationStore,
        IDesktopApplicationDockCatalog dockCatalog,
        IDesktopApplicationPickerCatalog applicationCatalog,
        IDispatcher dispatcher)
    {
        this.recentApplicationStore = recentApplicationStore;
        this.dockCatalog = dockCatalog;
        this.applicationCatalog = applicationCatalog;
        this.dispatcher = dispatcher;

        foreach (LaunchableApplication application in recentApplicationStore.Applications.Take(MaximumApplications))
        {
            DesktopApplicationPickerItemViewModel item = new(application);
            RecentApplications.Add(item);
            _ = LoadIconAsync(item);
        }

        recentApplicationStore.ApplicationRecorded += HandleApplicationRecorded;
        _ = RefreshAsync();
    }

    public ObservableCollection<DesktopApplicationPickerItemViewModel> RecentApplications { get; } = [];

    public bool HasRecentApplications => RecentApplications.Count > 0;

    private async Task RefreshAsync()
    {
        IReadOnlyList<LaunchableApplication> applications =
            await dockCatalog.GetApplicationsAsync(MaximumApplications);

        dispatcher.Dispatch(() => ApplyApplications(applications));
    }

    public async Task LoadIconAsync(DesktopApplicationPickerItemViewModel item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!item.TryBeginIconLoad())
        {
            return;
        }

        try
        {
            ApplicationIcon? icon = await applicationCatalog.GetIconAsync(item.Application, cancellationToken);
            dispatcher.Dispatch(() => item.CompleteIconLoad(icon));
        }
        catch (OperationCanceledException)
        {
            item.CancelIconLoad();
        }
    }

    private void HandleApplicationRecorded(LaunchableApplication application) =>
        dispatcher.Dispatch(() =>
        {
            DesktopApplicationPickerItemViewModel? existing = RecentApplications.FirstOrDefault(item =>
                string.Equals(item.Application.Id, application.Id, StringComparison.Ordinal));

            if (existing is not null)
            {
                RecentApplications.Remove(existing);
                RecentApplications.Insert(0, existing);
            }
            else
            {
                DesktopApplicationPickerItemViewModel item = new(application);
                RecentApplications.Insert(0, item);
                _ = LoadIconAsync(item);
            }

            while (RecentApplications.Count > MaximumApplications)
            {
                RecentApplications.RemoveAt(RecentApplications.Count - 1);
            }

            OnPropertyChanged(nameof(HasRecentApplications));
        });

    private void ApplyApplications(IReadOnlyList<LaunchableApplication> applications)
    {
        RecentApplications.Clear();

        foreach (LaunchableApplication application in applications)
        {
            DesktopApplicationPickerItemViewModel item = new(application);
            RecentApplications.Add(item);
            _ = LoadIconAsync(item);
        }

        OnPropertyChanged(nameof(HasRecentApplications));
    }
}
