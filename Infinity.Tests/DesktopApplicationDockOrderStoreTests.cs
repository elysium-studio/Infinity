using Elysium.Application.Abstractions;
using Infinity.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Infinity.Tests;

public sealed class DesktopApplicationDockOrderStoreTests
{
    [Fact]
    public async Task SavedOrderIsNormalizedAndPersisted()
    {
        Settings settings = new();
        DesktopApplicationDockOrderStore store = new(new TestOptionsMonitor(settings), new TestWritableOptions(settings), NullLogger<DesktopApplicationDockOrderStore>.Instance);
        await store.SaveAsync(["edge", "terminal", "EDGE", ""]);
        Assert.Equal(["edge", "terminal"], store.ApplicationIdentifiers);
        Assert.Equal(["edge", "terminal"], settings.DockApplicationOrder);
    }


    [Fact]
    public void SavedOrderIsRestored()
    {
        Settings settings = new()
        {
            DockApplicationOrder = ["terminal", "edge"]
        };
        DesktopApplicationDockOrderStore store = new(new TestOptionsMonitor(settings), new TestWritableOptions(settings), NullLogger<DesktopApplicationDockOrderStore>.Instance);
        Assert.Equal(["terminal", "edge"], store.ApplicationIdentifiers);
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
