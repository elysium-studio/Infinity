using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopWindowSelectionModelTests
{
    [Fact]
    public void ToggleSelectedAddsAndRemovesWindowWithoutChangingFocus()
    {
        DesktopWindowSelectionModel selection = new();
        selection.Focus(2);
        bool selected = selection.ToggleSelected(1);
        Assert.Equal((nint)2, selection.FocusedHandle);
        Assert.True(selected);
        Assert.Equal([(nint)1], selection.SelectedHandles);
        selected = selection.ToggleSelected(1);
        Assert.Equal((nint)2, selection.FocusedHandle);
        Assert.False(selected);
        Assert.Empty(selection.SelectedHandles);
    }


    [Fact]
    public void ClearSelectedHandlesRetainsKeyboardFocus()
    {
        DesktopWindowSelectionModel selection = new();
        selection.Focus(2);
        selection.ToggleSelected(1);
        selection.ToggleSelected(2);
        selection.ToggleSelected(3);
        selection.ClearSelectedHandles();
        Assert.Empty(selection.SelectedHandles);
        Assert.Equal((nint)2, selection.FocusedHandle);
    }


    [Fact]
    public void RemoveSelectedLeavesOtherWindowsSelected()
    {
        DesktopWindowSelectionModel selection = new();
        selection.ToggleSelected(1);
        selection.ToggleSelected(2);
        selection.RemoveSelected(1);
        Assert.Equal([(nint)2], selection.SelectedHandles);
    }
}
