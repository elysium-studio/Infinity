using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Infinity.Tests;

public sealed class DesktopApplicationPinStoreTests
{
    [Fact]
    public async Task PinAndUnpinPersistInfinityApplications()
    {
        Settings settings = new();
        DesktopApplicationPinStore store = CreateStore(settings);
        LaunchableApplication calculator = new("calculator", "Calculator");
        int changes = 0;
        store.PinsChanged += () => changes++;
        await store.PinAsync(calculator);
        await store.PinAsync(calculator);
        Assert.Equal([calculator], store.Applications);
        Assert.Equal([calculator], settings.PinnedApplications);
        Assert.Equal(1, changes);
        await store.UnpinAsync(calculator);
        Assert.Empty(store.Applications);
        Assert.Empty(settings.PinnedApplications!);
        Assert.Equal(2, changes);
    }


    [Fact]
    public void SavedPinsAreRestoredWithoutDuplicates()
    {
        LaunchableApplication calculator = new("calculator", "Calculator");
        Settings settings = new()
        {
            PinnedApplications = [calculator, calculator]
        };
        DesktopApplicationPinStore store = CreateStore(settings);
        Assert.Equal([calculator], store.Applications);
    }


    private static DesktopApplicationPinStore CreateStore(Settings settings) => new(new TestOptionsMonitor(settings), new TestWritableOptions(settings), NullLogger<DesktopApplicationPinStore>.Instance);

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
