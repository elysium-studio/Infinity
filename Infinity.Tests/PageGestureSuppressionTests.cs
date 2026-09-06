using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class PageGestureSuppressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuppressedInputDoesNotMoveWindowsOrConsumeDragKeys(bool keyboardOnly)
    {
        Keyboard keyboard = new();
        Gesture gesture = new();
        ScrollInputSuppression suppression = new();
        PageGestureSource source = new(keyboard, new Modifiers(), [gesture], NullLogger<PageGestureSource>.Instance, suppression);
        source.Start();
        using (keyboardOnly ? suppression.SuppressKeyboard() : suppression.Suppress())
        {
            Assert.False(keyboard.Press().Handled);
            Assert.False(keyboard.Release().Handled);
            Assert.Equal(0, gesture.Count);
        }
        Assert.True(keyboard.Press().Handled);
        Assert.True(keyboard.Release().Handled);
        Assert.Equal(1, gesture.Count);
        source.Stop();
    }

    private sealed class Gesture : IPageGesture
    {
        public IReadOnlyCollection<int> TriggerKeys => [0x27];
        public IReadOnlyCollection<int> RequiredKeys => [];
        public int Count { get; private set; }
        public void Invoke(int virtualKeyCode) => Count++;
    }

    private sealed class Keyboard : IKeyboardInputSource
    {
        public event EventHandler<KeyEventArgs>? KeyDown;
        public event EventHandler<KeyEventArgs>? KeyUp;
        public bool IsKeyDown(int virtualKeyCode) => false;
        public void Dispose() { }

        public KeyEventArgs Press()
        {
            KeyEventArgs args = new(0x27);
            KeyDown?.Invoke(this, args);
            return args;
        }

        public KeyEventArgs Release()
        {
            KeyEventArgs args = new(0x27);
            KeyUp?.Invoke(this, args);
            return args;
        }
    }

    private sealed class Modifiers : IModifierKeyState
    {
        public event Action<bool>? StateChanged { add { } remove { } }
        public bool IsActive => true;
        public void SetKeys(List<List<int>> combinations) { }
        public void Dispose() { }
    }
}
