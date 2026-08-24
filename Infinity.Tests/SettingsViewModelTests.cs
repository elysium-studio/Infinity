using Infinity.Shell;
using System.ComponentModel;

namespace Infinity.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void CurrentViewFollowsSettingsNavigation()
    {
        TestSettingViewModel overview = new();
        TestSettingViewModel detail = new();
        SettingsViewModel viewModel = new(null!, null!, null!, null!, [overview]);

        Assert.Same(overview, viewModel.CurrentView);

        viewModel.NavigateTo(detail);

        Assert.Same(detail, viewModel.CurrentView);
    }

    private sealed class TestSettingViewModel :
        List<object>,
        ISettingViewModel
    {
        event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
        {
            add { }
            remove { }
        }

        public void Dispose()
        {
        }
    }
}