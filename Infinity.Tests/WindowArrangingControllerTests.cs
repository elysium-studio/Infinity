using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Platform.Windows;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class WindowArrangingControllerTests
{
    [Fact]
    public void WatchdogDoesNotRestoreBeforeLeaseExpires()
    {
        using TestContext context = new();

        Assert.True(context.Controller.Disable());
        context.Clock.Timestamp = 199;

        context.Controller.RunWatchdog();

        Assert.False(context.System.Enabled);
        Assert.Equal([false], context.System.Writes);
    }

    [Fact]
    public void WatchdogRestoresAfterLeaseExpires()
    {
        using TestContext context = new();

        Assert.True(context.Controller.Disable());
        context.Clock.Timestamp = 200;

        context.Controller.RunWatchdog();

        Assert.True(context.System.Enabled);
        Assert.Equal([false, true], context.System.Writes);
        Assert.False(File.Exists(context.RecoveryFlagPath));
    }

    [Fact]
    public void RepeatedDisableRefreshesWatchdogLease()
    {
        using TestContext context = new();

        Assert.True(context.Controller.Disable());
        context.Clock.Timestamp = 150;
        Assert.True(context.Controller.Disable());
        context.Clock.Timestamp = 249;

        context.Controller.RunWatchdog();

        Assert.False(context.System.Enabled);
        Assert.Equal([false], context.System.Writes);

        context.Clock.Timestamp = 250;
        context.Controller.RunWatchdog();

        Assert.True(context.System.Enabled);
        Assert.Equal([false, true], context.System.Writes);
    }

    [Fact]
    public void WatchdogRetriesFailedRestore()
    {
        using TestContext context = new();
        context.System.RestoreFailuresRemaining = 1;

        Assert.True(context.Controller.Disable());
        context.Clock.Timestamp = 200;

        context.Controller.RunWatchdog();
        Assert.False(context.System.Enabled);
        Assert.True(File.Exists(context.RecoveryFlagPath));

        context.Controller.RunWatchdog();

        Assert.True(context.System.Enabled);
        Assert.Equal([false, true, true], context.System.Writes);
        Assert.False(File.Exists(context.RecoveryFlagPath));
    }

    [Fact]
    public void WatchdogPreservesAnOriginallyDisabledSetting()
    {
        using TestContext context = new(false);

        Assert.True(context.Controller.Disable());
        context.Clock.Timestamp = 200;

        context.Controller.RunWatchdog();

        Assert.False(context.System.Enabled);
        Assert.Empty(context.System.Writes);
        Assert.False(File.Exists(context.RecoveryFlagPath));
    }

    [Fact]
    public void StopRestoresAnActiveDisable()
    {
        using TestContext context = new();
        context.Controller.Start();

        Assert.True(context.Controller.Disable());
        context.Controller.Stop();

        Assert.True(context.System.Enabled);
        Assert.Equal([false, true], context.System.Writes);
        Assert.False(File.Exists(context.RecoveryFlagPath));
    }

    [Fact]
    public void WatchdogRetriesFailedStartupRecovery()
    {
        using TestContext context = new();
        Directory.CreateDirectory(Path.GetDirectoryName(context.RecoveryFlagPath)!);
        File.WriteAllText(context.RecoveryFlagPath, "1");
        context.System.Enabled = false;
        context.System.RestoreFailuresRemaining = 1;
        context.Controller.Start();

        context.Controller.RunWatchdog();

        Assert.True(context.System.Enabled);
        Assert.Equal([true, true], context.System.Writes);
        Assert.False(File.Exists(context.RecoveryFlagPath));
    }

    [Fact]
    public void DragEndRestoresWhileModifierRemainsDown()
    {
        using TestContext context = new();
        TestWindowEventListener listener = new();
        TestModifierKeyState modifierKeyState = new();
        WindowDragGuard guard = new(listener,
            modifierKeyState,
            new IgnoringDispatcher(),
            context.Controller,
            NullLogger<WindowDragGuard>.Instance);
        guard.Start();

        try
        {
            listener.RaiseDragStarted(new IntPtr(1));
            modifierKeyState.SetActive(true);
            Assert.False(context.System.Enabled);

            listener.RaiseDragEnded(new IntPtr(1));

            Assert.True(context.System.Enabled);
            Assert.Equal([false, true], context.System.Writes);
        }
        finally
        {
            guard.Stop();
        }
    }

    private sealed class TestContext :
        IDisposable
    {
        private const long LeaseTimeoutTicks = 100;

        private readonly string directory;

        public TestContext(bool initiallyEnabled = true)
        {
            directory = Path.Combine(Path.GetTempPath(), "Infinity.Tests", Guid.NewGuid().ToString("N"));
            RecoveryFlagPath = Path.Combine(directory, "windowArranging.flag");
            System = new TestWindowArrangingSystem(initiallyEnabled);
            Clock = new TestClock { Timestamp = 100 };
            Controller = new WindowArrangingController(NullLogger<WindowArrangingController>.Instance,
                System,
                () => Clock.Timestamp,
                LeaseTimeoutTicks,
                RecoveryFlagPath);
        }

        public WindowArrangingController Controller { get; }

        public TestWindowArrangingSystem System { get; }

        public TestClock Clock { get; }

        public string RecoveryFlagPath { get; }

        public void Dispose()
        {
            Controller.Stop();

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            GC.SuppressFinalize(this);
        }
    }

    private sealed class TestClock
    {
        public long Timestamp { get; set; }
    }

    private sealed class TestWindowArrangingSystem(bool enabled) :
        IWindowArrangingSystem
    {
        public bool Enabled { get; set; } = enabled;

        public int RestoreFailuresRemaining { get; set; }

        public List<bool> Writes { get; } = [];

        public bool TryRead(out bool enabled, out int error)
        {
            enabled = Enabled;
            error = 0;
            return true;
        }

        public bool TryWrite(bool enabled, out int error)
        {
            Writes.Add(enabled);

            if (enabled && RestoreFailuresRemaining > 0)
            {
                RestoreFailuresRemaining--;
                error = 5;
                return false;
            }

            Enabled = enabled;
            error = 0;
            return true;
        }
    }

    private sealed class TestModifierKeyState :
        IModifierKeyState
    {
        public event Action<bool>? StateChanged;

        public bool IsActive { get; private set; }

        public void SetActive(bool value)
        {
            IsActive = value;
            StateChanged?.Invoke(value);
        }

        public void SetKeys(List<List<int>> combinations)
        {
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    private sealed class TestWindowEventListener :
        IWindowEventListener
    {
        public event Action<IntPtr>? DragStarted;

        public event Action<IntPtr>? DragEnded;

        event Action<IntPtr>? IWindowEventListener.WindowCreated
        {
            add { }
            remove { }
        }

        event Action<IntPtr>? IWindowEventListener.WindowShown
        {
            add { }
            remove { }
        }

        event Action<IntPtr>? IWindowEventListener.WindowDestroyed
        {
            add { }
            remove { }
        }

        event Action<IntPtr>? IWindowEventListener.WindowTitleChanged
        {
            add { }
            remove { }
        }

        event Action<IntPtr>? IWindowEventListener.WindowLocationChanged
        {
            add { }
            remove { }
        }

        event Action<IntPtr>? IWindowEventListener.MinimizeStarted
        {
            add { }
            remove { }
        }

        event Action<IntPtr>? IWindowEventListener.MinimizeEnded
        {
            add { }
            remove { }
        }

        event Action<IntPtr>? IWindowEventListener.ForegroundChanged
        {
            add { }
            remove { }
        }

        event Action? IWindowEventListener.WindowStackChanged
        {
            add { }
            remove { }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public void RaiseDragStarted(IntPtr handle) => DragStarted?.Invoke(handle);

        public void RaiseDragEnded(IntPtr handle) => DragEnded?.Invoke(handle);

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class IgnoringDispatcher :
        IDispatcher
    {
        public void Dispatch(Action action)
        {
        }
    }
}