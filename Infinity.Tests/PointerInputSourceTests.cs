using Elysium.Platform.Abstractions;
using Infinity.Platform.Windows;

namespace Infinity.Tests;

public sealed class PointerInputSourceTests
{
    [Fact]
    public void ModifiedMiddleButtonIsHandledBeforeRaisingTrigger()
    {
        TestMouseInputSource mouse = new();
        TestModifierKeyState modifiers = new() { IsActive = true };
        using PointerInputSource pointer = new(mouse, modifiers);
        int triggers = 0;
        pointer.MiddleButtonClicked += () => triggers++;

        MouseButtonEventArgs args = mouse.RaiseMiddleButtonPressed();

        Assert.True(args.Handled);
        Assert.Equal(1, triggers);
    }

    [Fact]
    public void UnmodifiedMiddleButtonRemainsAvailableToWindows()
    {
        TestMouseInputSource mouse = new();
        TestModifierKeyState modifiers = new();
        using PointerInputSource pointer = new(mouse, modifiers);
        int triggers = 0;
        pointer.MiddleButtonClicked += () => triggers++;

        MouseButtonEventArgs args = mouse.RaiseMiddleButtonPressed();

        Assert.False(args.Handled);
        Assert.Equal(0, triggers);
    }

    private sealed class TestMouseInputSource :
        IMouseInputSource
    {
        public event Action? MiddleButtonDown;
        public event EventHandler<MouseButtonEventArgs>? MiddleButtonPressed;

        event Action? IMouseInputSource.LeftButtonDown
        {
            add { }
            remove { }
        }

        event Action? IMouseInputSource.RightButtonDown
        {
            add { }
            remove { }
        }

        event EventHandler<MouseMoveEventArgs>? IMouseInputSource.MouseMoved
        {
            add { }
            remove { }
        }

        event EventHandler<MouseWheelEventArgs>? IMouseInputSource.WheelScrolled
        {
            add { }
            remove { }
        }

        public MouseButtonEventArgs RaiseMiddleButtonPressed()
        {
            MouseButtonEventArgs args = new();
            MiddleButtonPressed?.Invoke(this, args);

            if (!args.Handled)
            {
                MiddleButtonDown?.Invoke();
            }

            return args;
        }

        public void Dispose() => GC.SuppressFinalize(this);
    }

    private sealed class TestModifierKeyState :
        IModifierKeyState
    {
        event Action<bool>? IModifierKeyState.StateChanged
        {
            add { }
            remove { }
        }

        public bool IsActive { get; set; }

        public void SetKeys(List<List<int>> combinations)
        {
        }

        public void Dispose() => GC.SuppressFinalize(this);
    }
}
