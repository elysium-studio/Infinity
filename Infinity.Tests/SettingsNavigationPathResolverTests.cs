using Infinity.Shell;
using System.ComponentModel;

namespace Infinity.Tests;

public sealed class SettingsNavigationPathResolverTests
{
    private readonly SettingsNavigationPathResolver resolver = new();

    [Fact]
    public void InitialPathIncludesDefaultChild()
    {
        TestSetting child = new("Child");
        TestSetting root = new("Root", [child]);

        IReadOnlyList<ISettingViewModel> path = resolver.GetInitialPath([root]);

        Assert.Equal([root, child], path);
    }

    [Fact]
    public void SelectionPathFindsNestedItemAndOpensItsDefaultChild()
    {
        TestSetting leaf = new("Leaf");
        TestSetting section = new("Section", [leaf]);
        TestSetting root = new("Root", [section]);

        IReadOnlyList<ISettingViewModel> path = resolver.GetSelectionPath([root], section);

        Assert.Equal([root, section, leaf], path);
    }

    [Fact]
    public void BreadcrumbPathReturnsToSectionDefaultChild()
    {
        TestSetting firstLeaf = new("First");
        TestSetting secondLeaf = new("Second");
        TestSetting section = new("Section", [firstLeaf, secondLeaf]);
        TestSetting root = new("Root", [section]);

        IReadOnlyList<ISettingViewModel> path = resolver.GetBreadcrumbPath([root, section, secondLeaf], 1);

        Assert.Equal([root, section, firstLeaf], path);
    }

    [Fact]
    public void BackPathRemovesOnlyCurrentItem()
    {
        TestSetting leaf = new("Leaf");
        TestSetting section = new("Section");
        TestSetting root = new("Root");

        IReadOnlyList<ISettingViewModel> path = resolver.GetBackPath([root, section, leaf]);

        Assert.Equal([root, section], path);
    }

    private sealed class TestSetting(string title, IReadOnlyList<ISettingViewModel>? children = null) :
        List<object>,
        ISettingViewModel
    {
        public IReadOnlyList<ISettingViewModel> Children { get; } = children ?? [];

        public string Title => title;

        event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
        {
            add { }
            remove { }
        }

        public void Dispose() { }
    }
}
