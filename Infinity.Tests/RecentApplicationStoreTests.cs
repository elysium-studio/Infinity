using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Infinity.Tests;

public sealed class RecentApplicationStoreTests
{
    [Fact]
    public async Task MostRecentlyRecordedApplicationMovesToTheFront()
    {
        Settings settings = new();
        RecentApplicationStore store = CreateStore(settings);
        LaunchableApplication calculator = new("calculator", "Calculator");
        LaunchableApplication paint = new("paint", "Paint");
        await store.RecordAsync(calculator);
        await store.RecordAsync(paint);
        await store.RecordAsync(calculator);
        Assert.Equal([calculator, paint], store.Applications);
        Assert.Equal([calculator, paint], settings.RecentApplications);
    }


    [Fact]
    public async Task DockHistoryRemainsCompact()
    {
        RecentApplicationStore store = CreateStore(new Settings());
        for (int index = 0; index < 8; index++)
        {
            await store.RecordAsync(new LaunchableApplication($"application-{index}", $"Application {index}"));
        }

        Assert.Collection(store.Applications, application => Assert.Equal("application-7", application.Id), application => Assert.Equal("application-6", application.Id), application => Assert.Equal("application-5", application.Id), application => Assert.Equal("application-4", application.Id), application => Assert.Equal("application-3", application.Id), application => Assert.Equal("application-2", application.Id));
    }


    [Fact]
    public void SavedHistoryIsRestoredOnStartup()
    {
        LaunchableApplication calculator = new("calculator", "Calculator");
        Settings settings = new()
        {
            RecentApplications = [calculator]
        };
        RecentApplicationStore store = CreateStore(settings);
        Assert.Equal([calculator], store.Applications);
    }


    private static RecentApplicationStore CreateStore(Settings settings) => new(new TestOptionsMonitor(settings), new TestWritableOptions(settings), NullLogger<RecentApplicationStore>.Instance);

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
