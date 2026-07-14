using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public partial class DesktopHistoryViewModel :
    ObservableViewModel,
    IRecipient<OptionsChangedEventArgs<Settings>>
{
    private readonly IDispatcher dispatcher;
    private readonly IDesktopNavigationHistory history;
    private readonly ITextLocalizer localizer;
    private readonly IKeyLabelProvider keyLabelProvider;
    private Settings settings;
    private Timer? relativeTimeTimer;

    [ObservableProperty]
    private IReadOnlyList<DesktopHistoryItemViewModel> backEntries = [];

    [ObservableProperty]
    private IReadOnlyList<DesktopHistoryItemViewModel> forwardEntries = [];

    [ObservableProperty]
    private bool canGoBack;

    [ObservableProperty]
    private bool canGoForward;

    [ObservableProperty]
    private bool canClear;

    [ObservableProperty]
    private string backToolTip = string.Empty;

    [ObservableProperty]
    private string forwardToolTip = string.Empty;

    public DesktopHistoryViewModel(IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        IDispatcher dispatcher,
        IDesktopNavigationHistory history,
        ITextLocalizer localizer,
        IKeyLabelProvider keyLabelProvider,
        Settings settings) : base(provider, factory, messenger, disposer)
    {
        this.dispatcher = dispatcher;
        this.history = history;
        this.localizer = localizer;
        this.keyLabelProvider = keyLabelProvider;
        this.settings = settings;
    }

    public override void Activated()
    {
        history.Changed -= HandleHistoryChanged;
        history.Changed += HandleHistoryChanged;
        relativeTimeTimer?.Dispose();
        relativeTimeTimer = new Timer(HandleRelativeTimeTimer, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        Refresh();
    }

    public override void Deactivated()
    {
        history.Changed -= HandleHistoryChanged;
        relativeTimeTimer?.Dispose();
        relativeTimeTimer = null;
    }

    public override void Dispose()
    {
        history.Changed -= HandleHistoryChanged;
        relativeTimeTimer?.Dispose();
        relativeTimeTimer = null;
        base.Dispose();
    }

    protected override void RegisterMessages() => Messenger.Register<OptionsChangedEventArgs<Settings>>(this);

    public void Receive(OptionsChangedEventArgs<Settings> message) => dispatcher.Dispatch(() =>
    {
        settings = message.Options;
        Refresh();
    });

    public void GoBack() => history.GoBack();

    public void GoForward() => history.GoForward();

    public void Clear() => history.Clear();

    private void HandleHistoryChanged(object? sender, EventArgs args) => dispatcher.Dispatch(Refresh);

    private void HandleRelativeTimeTimer(object? state) => dispatcher.Dispatch(Refresh);

    private void Refresh()
    {
        CanGoBack = history.CanGoBack;
        CanGoForward = history.CanGoForward;

        BackEntries = history.BackEntries.Select(CreateItem).ToArray();
        ForwardEntries = history.ForwardEntries.Select(CreateItem).ToArray();
        CanClear = BackEntries.Count > 0 || ForwardEntries.Count > 0;

        string backShortcut = FormatShortcut(settings.DesktopHistoryBackShortcut);
        string forwardShortcut = FormatShortcut(settings.DesktopHistoryForwardShortcut);
        BackToolTip = localizer.GetText("DesktopHistoryBackToolTip", backShortcut);
        ForwardToolTip = localizer.GetText("DesktopHistoryForwardToolTip", forwardShortcut);
    }

    private DesktopHistoryItemViewModel CreateItem(DesktopHistoryEntry entry)
    {
        string title = entry.HasWindow && !string.IsNullOrWhiteSpace(entry.WindowTitle)
            ? entry.WindowTitle
            : localizer.GetText(entry.HasWindow ? "DesktopHistoryUntitledWindow" : "DesktopHistoryPageOnly");

        string pageLabel = settings.PageTitles?.TryGetValue(entry.Page, out string? pageTitle) == true &&
            !string.IsNullOrWhiteSpace(pageTitle)
                ? pageTitle
                : localizer.GetText("PageTitle", entry.Page + 1);

        return new DesktopHistoryItemViewModel(entry.Id,
            title,
            pageLabel,
            FormatVisitedAt(entry.VisitedAt),
            entry.HasWindow ? "\uE8A7" : "\uE7C3",
            id => history.NavigateTo(id));
    }

    private string FormatVisitedAt(DateTimeOffset visitedAt)
    {
        TimeSpan elapsed = DateTimeOffset.Now - visitedAt;

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return localizer.GetText("DesktopHistoryJustNow");
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return localizer.GetText("DesktopHistoryMinutesAgo", Math.Max(1, (int)elapsed.TotalMinutes));
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            return localizer.GetText("DesktopHistoryHoursAgo", Math.Max(1, (int)elapsed.TotalHours));
        }

        return localizer.GetText("DesktopHistoryDaysAgo", Math.Max(1, (int)elapsed.TotalDays));
    }

    private string FormatShortcut(IEnumerable<IEnumerable<int>> shortcut) => string.Join(" + ", shortcut
        .Select(group => group.FirstOrDefault())
        .Where(key => key != 0)
        .Select(keyLabelProvider.GetFullLabel));
}
