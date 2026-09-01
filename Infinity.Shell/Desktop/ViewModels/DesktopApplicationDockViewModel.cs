using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;
using System.Collections.ObjectModel;

namespace Infinity.Shell;

public sealed partial class DesktopApplicationDockViewModel : ObservableObject
{
    private const int MaximumApplications = 10;

    private readonly IDesktopApplicationPinStore pinStore;
    private readonly IDesktopApplicationDockOrderStore orderStore;
    private readonly IDesktopApplicationDockCatalog dockCatalog;
    private readonly IDesktopApplicationPickerCatalog applicationCatalog;
    private readonly IDispatcher dispatcher;

    public DesktopApplicationDockViewModel(
        IDesktopApplicationPinStore pinStore,
        IDesktopApplicationDockOrderStore orderStore,
        IDesktopApplicationDockCatalog dockCatalog,
        IDesktopApplicationPickerCatalog applicationCatalog,
        IDispatcher dispatcher)
    {
        this.pinStore = pinStore;
        this.orderStore = orderStore;
        this.dockCatalog = dockCatalog;
        this.applicationCatalog = applicationCatalog;
        this.dispatcher = dispatcher;

        foreach (LaunchableApplication application in pinStore.Applications.Take(MaximumApplications))
        {
            AddItem(new DesktopApplicationDockEntry(application, DesktopApplicationDockSource.Infinity));
        }

        pinStore.PinsChanged += HandlePinsChanged;
        _ = RefreshAsync();
    }

    public ObservableCollection<DesktopApplicationDockItemViewModel> Applications { get; } = [];

    public bool HasApplications => Applications.Count > 0;

    public bool CanPin(LaunchableApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return !Applications.Any(item =>
            string.Equals(item.Application.Id, application.Id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task PinAsync(LaunchableApplication application, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        DesktopApplicationDockItemViewModel? existing = Applications.FirstOrDefault(item =>
            string.Equals(item.Application.Id, application.Id, StringComparison.OrdinalIgnoreCase));

        if (existing?.Source is DesktopApplicationDockSource.Taskbar or DesktopApplicationDockSource.Infinity)
        {
            return;
        }

        await pinStore.PinAsync(application, cancellationToken);
    }

    public async Task UnpinAsync(
        DesktopApplicationDockItemViewModel item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!item.CanUnpin)
        {
            return;
        }

        await pinStore.UnpinAsync(item.Application, cancellationToken);
    }

    public Task SaveOrderAsync(CancellationToken cancellationToken = default) =>
        orderStore.SaveAsync(
            Applications.Select(item => item.Application.Id),
            cancellationToken);

    public async Task LoadIconAsync(
        DesktopApplicationDockItemViewModel item,
        CancellationToken cancellationToken = default)
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

    private async Task RefreshAsync()
    {
        IReadOnlyList<DesktopApplicationDockEntry> applications =
            await dockCatalog.GetApplicationsAsync(MaximumApplications);

        dispatcher.Dispatch(() => ApplyApplications(applications));
    }

    private void HandlePinsChanged() => _ = RefreshAsync();

    private void ApplyApplications(IReadOnlyList<DesktopApplicationDockEntry> applications)
    {
        Applications.Clear();

        foreach (DesktopApplicationDockEntry application in applications)
        {
            AddItem(application);
        }

        OnPropertyChanged(nameof(HasApplications));
    }

    private void AddItem(DesktopApplicationDockEntry entry)
    {
        DesktopApplicationDockItemViewModel item = new(entry.Application, entry.Source);
        Applications.Add(item);
        _ = LoadIconAsync(item);
    }
}
