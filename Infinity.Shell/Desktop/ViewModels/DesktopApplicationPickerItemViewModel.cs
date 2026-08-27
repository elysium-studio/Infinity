using CommunityToolkit.Mvvm.ComponentModel;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed partial class DesktopApplicationPickerItemViewModel(LaunchableApplication application) : ObservableObject
{
    private int iconLoadState;

    [ObservableProperty]
    private ApplicationIcon? icon;

    public LaunchableApplication Application { get; } = application;

    public string DisplayName => Application.DisplayName;

    public bool ShowFallbackIcon => Volatile.Read(ref iconLoadState) == 2 && Icon is null;

    internal bool TryBeginIconLoad() => Interlocked.CompareExchange(ref iconLoadState, 1, 0) == 0;

    internal void CompleteIconLoad(ApplicationIcon? value)
    {
        Icon = value;
        Volatile.Write(ref iconLoadState, 2);
        OnPropertyChanged(nameof(ShowFallbackIcon));
    }

    internal void CancelIconLoad() => Interlocked.CompareExchange(ref iconLoadState, 0, 1);
}
