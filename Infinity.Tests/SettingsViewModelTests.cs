using CommunityToolkit.Mvvm.Messaging;
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

    [Fact]
    public void DesktopSectionOwnsInjectedItems()
    {
        TestDesktopItem first = new();
        TestDesktopItem second = new();
        using TestDesktopSection viewModel = new([first, second]);

        Assert.Equal("Pages", viewModel.Title);
        Assert.Collection(viewModel,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
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

    public sealed class TestDesktopSection(IEnumerable<TestDesktopItem> items) :
        DesktopSettingsSectionViewModel<TestDesktopItem>(null!, null!, WeakReferenceMessenger.Default, null!, "Pages", items);

    public sealed class TestDesktopItem :
        IDisposable
    {
        public void Dispose()
        {
        }
    }
}
