using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;
using Microsoft.Extensions.Options;

namespace Infinity.Tests;

public sealed class DesktopSnapAppearanceCoordinatorTests
{
    [Fact]
    public void SlotAppearanceIsAppliedOnceAndRemovedAfterAnExternalMove()
    {
        Fixture test = new();
        test.Coordinator.Start();
        test.Store.NotifyChanged(1);
        Assert.True(test.Coordinator.IsSlotted(1));
        Assert.Equal(1, test.Appearance.ApplyCount);
        test.Window.CanvasX += 100;
        test.Store.NotifyChanged(1);
        Assert.False(test.Coordinator.IsSlotted(1));
        Assert.Equal(1, test.Appearance.RestoreCount);
        test.Coordinator.Stop();
    }


    [Fact]
    public void DraggingRemovesSquareCornersUntilTheDrop()
    {
        Fixture test = new();
        test.Coordinator.Start();
        test.Coordinator.BeginMove(1);
        test.Store.NotifyChanged(1);
        Assert.False(test.Coordinator.IsSlotted(1));
        Assert.Equal(1, test.Appearance.ApplyCount);
        test.Coordinator.EndMove(1);
        Assert.True(test.Coordinator.IsSlotted(1));
        Assert.Equal(2, test.Appearance.ApplyCount);
        test.Coordinator.Stop();
    }


    [Fact]
    public async Task RemovingPageLayoutRestoresTheNativeAppearance()
    {
        Fixture test = new();
        test.Coordinator.Start();
        await test.Layouts.UpdateAsync(0, DesktopSnapLayoutKind.None);
        Assert.False(test.Coordinator.IsSlotted(1));
        Assert.Equal(1, test.Appearance.RestoreCount);
        test.Coordinator.Stop();
    }


    [Fact]
    public void UnsupportedNativeFrameIsNotRetriedOnEveryRefresh()
    {
        Fixture test = new();
        test.Appearance.Supported = false;
        test.Coordinator.Start();
        for (int index = 0; index < 10; index++)
        {
            test.Store.NotifyChanged(1);
        }

        Assert.False(test.Coordinator.IsSlotted(1));
        Assert.Equal(1, test.Appearance.ApplyCount);
        test.Coordinator.Stop();
    }


    [Fact]
    public void StoppingRestoresFramesAndUnsubscribes()
    {
        Fixture test = new();
        test.Coordinator.Start();
        test.Coordinator.Stop();
        test.Store.NotifyChanged(1);
        Assert.False(test.Coordinator.IsSlotted(1));
        Assert.Equal(1, test.Appearance.RestoreCount);
        Assert.Equal(1, test.Appearance.ApplyCount);
    }


    private sealed class Fixture
    {
        public WindowStore Store { get; } = new();

        public FrameAppearance Appearance { get; } = new();

        public TrackedWindow Window { get; } = new()
        {
            Handle = 1,
            CanvasX = 0,
            CanvasY = 0,
            Width = 960,
            Height = 1040
        };

        public PageLayoutStore Layouts { get; }

        public DesktopSnapAppearanceCoordinator Coordinator { get; }


        public Fixture()
        {
            Settings settings = new()
            {
                PageLayouts = new()
                {
                    [0] = DesktopSnapLayoutKind.Halves
                }
            };
            Layouts = new(new Monitor(settings), new Writer(settings));
            Workspace workspace = new();
            DesktopSnapLayoutCatalog catalog = new();
            Coordinator = new(Store, workspace, Layouts, catalog, new(workspace, catalog), Appearance, new(new TestWindowFrameGeometryReader()));
            Store.Add(Window);
        }
    }


    private sealed class FrameAppearance : IWindowSnapAppearance
    {
        public bool Supported { get; set; } = true;

        public int ApplyCount { get; private set; }

        public int RestoreCount { get; private set; }


        public bool TryApply(nint handle)
        {
            ApplyCount++;
            return Supported;
        }


        public void Restore(nint handle) => RestoreCount++;
    }


    private sealed class Workspace : IWorkspace
    {
        public event EventHandler? WorkspaceLayoutChanged;

        public int Width => 1920;

        public int Height => 1040;

        public int WorkAreaX => 0;

        public int WorkAreaY => 0;

        public nint GetCurrentWorkspace() => 0;
    }


    private sealed class Monitor(Settings settings) : IOptionsMonitor<Settings>
    {
        public Settings CurrentValue => settings;

        public Settings Get(string? name) => settings;

        public IDisposable? OnChange(Action<Settings, string?> listener) => null;
    }


    private sealed class Writer(Settings settings) : IWritableOptions<Settings>
    {
        public Task<Settings?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult<Settings?>(settings);

        public Task WriteAsync(Action<Settings> update, CancellationToken cancellationToken = default)
        {
            update(settings);
            return Task.CompletedTask;
        }


        public Task WriteAsync(Settings value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
