using CommunityToolkit.Mvvm.ComponentModel;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using System.Collections.ObjectModel;

namespace Infinity.Shell;

public sealed record DesktopApplicationDestination(string DisplayName, DesktopApplicationTarget Target);

public sealed partial class DesktopApplicationPickerViewModel(IApplicationCatalog applicationCatalog,
    DesktopSnapLayoutCatalog snapLayoutCatalog,
    ITextLocalizer localizer) : ObservableObject
{
    private IReadOnlyList<LaunchableApplication> applications = [];

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private DesktopApplicationDestination? selectedDestination;

    public ObservableCollection<LaunchableApplication> Results { get; } = [];

    public ObservableCollection<DesktopApplicationDestination> Destinations { get; } = [];

    public bool HasDestinations => Destinations.Count > 1;

    public bool HasResults => Results.Count > 0;

    public bool ShowEmptyState => !IsLoading && !HasResults;

    public string DestinationLabel => localizer.GetText("DesktopAppPickerDestinationHeader");

    public DesktopApplicationTarget Target => SelectedDestination?.Target ?? default;

    public async Task LoadAsync(DesktopApplicationTarget requestedTarget, CancellationToken cancellationToken = default)
    {
        ConfigureDestinations(requestedTarget);
        SearchText = string.Empty;

        if (applications.Count == 0)
        {
            IsLoading = true;

            try
            {
                applications = await applicationCatalog.GetApplicationsAsync(cancellationToken);
            }
            finally
            {
                IsLoading = false;
            }
        }

        RefreshResults();
    }

    partial void OnSearchTextChanged(string value) => RefreshResults();

    partial void OnIsLoadingChanged(bool value) => RefreshStateProperties();

    private void ConfigureDestinations(DesktopApplicationTarget requestedTarget)
    {
        Destinations.Clear();
        Destinations.Add(new DesktopApplicationDestination(localizer.GetText("DesktopAppPickerPageDestination"), new DesktopApplicationTarget(requestedTarget.Page)));

        DesktopSnapLayoutDefinition? layout = snapLayoutCatalog.Get(requestedTarget.Layout);

        if (layout is not null)
        {
            for (int slot = 0; slot < layout.Slots.Count; slot++)
            {
                string name = localizer.GetText("DesktopAppPickerSlotDestination", slot + 1);
                Destinations.Add(new DesktopApplicationDestination(name, new DesktopApplicationTarget(requestedTarget.Page, requestedTarget.Layout, slot)));
            }
        }

        SelectedDestination = Destinations[0];
        OnPropertyChanged(nameof(HasDestinations));
    }

    private void RefreshResults()
    {
        string search = SearchText.Trim();
        Results.Clear();

        foreach (LaunchableApplication application in applications)
        {
            if (search.Length == 0 || application.DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            {
                Results.Add(application);
            }
        }

        RefreshStateProperties();
    }

    private void RefreshStateProperties()
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowEmptyState));
    }
}
