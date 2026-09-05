using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed partial class DesktopApplicationPickerViewModel(IDesktopApplicationPickerCatalog applicationCatalog, IDispatcher dispatcher) : ObservableObject
{
    private IReadOnlyList<DesktopApplicationPickerItemViewModel> applications = [];
    [ObservableProperty]
    private string searchText = string.Empty;
    [ObservableProperty]
    private bool isLoading;

    public ObservableCollection<DesktopApplicationPickerItemViewModel> Results { get; } = [];

    public bool HasResults => Results.Count > 0;

    public bool ShowEmptyState => !IsLoading && !HasResults;

    public DesktopApplicationTarget Target
    {
        get; private set;
    }


    public bool TryGetApplication(string identifier, out LaunchableApplication? application)
    {
        DesktopApplicationPickerItemViewModel? item = applications.FirstOrDefault(candidate => string.Equals(candidate.Application.Id, identifier, StringComparison.Ordinal));
        application = item?.Application;
        return application is not null;
    }


    public async Task LoadAsync(DesktopApplicationTarget requestedTarget, CancellationToken cancellationToken = default)
    {
        bool loadApplications = false;
        await DispatchAsync(() =>
        {
            Target = new DesktopApplicationTarget(requestedTarget.Page);
            SearchText = string.Empty;
            loadApplications = applications.Count == 0;
            if (loadApplications)
            {
                IsLoading = true;
            }
        });
        if (loadApplications)
        {
            try
            {
                IReadOnlyList<LaunchableApplication> loadedApplications = await applicationCatalog.GetApplicationsAsync(cancellationToken);
                await DispatchAsync(() => applications = [.. loadedApplications.Select(application => new DesktopApplicationPickerItemViewModel(application))]);
            }
            finally
            {
                await DispatchAsync(() => IsLoading = false);
            }
        }

        await DispatchAsync(RefreshResults);
    }


    partial void OnSearchTextChanged(string value) => RefreshResults();

    partial void OnIsLoadingChanged(bool value) => RefreshStateProperties();

    private void RefreshResults()
    {
        string search = SearchText.Trim();
        Results.Clear();
        foreach (DesktopApplicationPickerItemViewModel application in applications)
        {
            if (search.Length == 0 || application.DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            {
                Results.Add(application);
            }
        }

        RefreshStateProperties();
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
            await DispatchAsync(() => item.CompleteIconLoad(icon));
        }
        catch (OperationCanceledException)
        {
            item.CancelIconLoad();
        }
    }


    private void RefreshStateProperties()
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowEmptyState));
    }


    private Task DispatchAsync(Action action)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Dispatch(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        return completion.Task;
    }
}
