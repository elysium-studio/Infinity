using Elysium.Application.Abstractions;
using Infinity.Shell;
using Microsoft.Extensions.Options;

namespace Infinity.Tests;

public sealed class PageLayoutStoreTests
{
    [Fact]
    public async Task UpdatePersistsConfiguredLayoutAndRemoval()
    {
        Settings settings = new();
        TestWritableOptions writer = new(settings);
        PageLayoutStore store = new(new TestOptionsMonitor(settings), writer);
        List<(int Page, DesktopSnapLayoutKind Layout)> changes = [];
        store.LayoutChanged += (page, layout) => changes.Add((page, layout));
        await store.UpdateAsync(3, DesktopSnapLayoutKind.Quarters);
        Assert.Equal(DesktopSnapLayoutKind.Quarters, settings.PageLayouts![3]);
        Assert.Equal((3, DesktopSnapLayoutKind.Quarters), changes[^1]);
        await store.UpdateAsync(3, DesktopSnapLayoutKind.None);
        Assert.DoesNotContain(3, settings.PageLayouts);
        Assert.Equal((3, DesktopSnapLayoutKind.None), changes[^1]);
    }


    [Fact]
    public async Task ReorderMovesLayoutsWithTheirPages()
    {
        Settings settings = new()
        {
            PageLayouts = new()
            {
                [0] = DesktopSnapLayoutKind.Halves,
                [1] = DesktopSnapLayoutKind.Thirds,
                [2] = DesktopSnapLayoutKind.Quarters
            }
        };
        PageLayoutStore store = new(new TestOptionsMonitor(settings), new TestWritableOptions(settings));
        await store.ReorderAsync(0, 2);
        Assert.Equal(DesktopSnapLayoutKind.Thirds, settings.PageLayouts![0]);
        Assert.Equal(DesktopSnapLayoutKind.Quarters, settings.PageLayouts[1]);
        Assert.Equal(DesktopSnapLayoutKind.Halves, settings.PageLayouts[2]);
    }


    private sealed class TestWritableOptions(Settings settings) : IWritableOptions<Settings>
    {
        public Task<Settings?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult<Settings?>(settings);

        public Task WriteAsync(Action<Settings> update, CancellationToken cancellationToken = default)
        {
            update(settings);
            return Task.CompletedTask;
        }


        public Task WriteAsync(Settings value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }


    private sealed class TestOptionsMonitor(Settings settings) : IOptionsMonitor<Settings>
    {
        public Settings CurrentValue => settings;

        public Settings Get(string? name) => settings;

        public IDisposable? OnChange(Action<Settings, string?> listener) => null;
    }
}
