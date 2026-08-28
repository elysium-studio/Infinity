using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Platform.Windows;

namespace Infinity.Tests;

public sealed class PointerInputSourceTests
{
    [Fact]
    public void ModifiedMiddleButtonIsHandledBeforeRaisingTrigger()
    {
        TestMouseInputSource mouse = new();
        TestModifierKeyState modifiers = new() { IsActive = true };
        using PointerInputSource pointer = new(mouse, modifiers, new ScrollPresentationSession());
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
        using PointerInputSource pointer = new(mouse, modifiers, new ScrollPresentationSession());
        int triggers = 0;
        pointer.MiddleButtonClicked += () => triggers++;

        MouseButtonEventArgs args = mouse.RaiseMiddleButtonPressed();

        Assert.False(args.Handled);
        Assert.Equal(0, triggers);
    }

    [Fact]
    public void UnmodifiedWheelIsCapturedWhilePresentationIsActive()
    {
        TestMouseInputSource mouse = new();
        TestModifierKeyState modifiers = new();
        ScrollPresentationSession presentation = new();
        using PointerInputSource pointer = new(mouse, modifiers, presentation);
        int receivedDelta = 0;
        pointer.ScrollDeltaReceived += delta => receivedDelta = delta;
        presentation.Begin();

        MouseWheelEventArgs args = mouse.RaiseWheelScrolled(120);

        Assert.True(args.Handled);
        Assert.Equal(120, receivedDelta);
    }

    [Fact]
    public void UnmodifiedWheelRemainsAvailableWhenPresentationIsInactive()
    {
        TestMouseInputSource mouse = new();
        TestModifierKeyState modifiers = new();
        using PointerInputSource pointer = new(mouse, modifiers, new ScrollPresentationSession());
        int triggers = 0;
        pointer.ScrollDeltaReceived += _ => triggers++;

        MouseWheelEventArgs args = mouse.RaiseWheelScrolled(120);

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

        public event EventHandler<MouseWheelEventArgs>? WheelScrolled;

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

        public MouseWheelEventArgs RaiseWheelScrolled(int delta)
        {
            MouseWheelEventArgs args = new(delta);
            WheelScrolled?.Invoke(this, args);
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
