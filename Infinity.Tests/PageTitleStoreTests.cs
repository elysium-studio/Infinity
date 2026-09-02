using Elysium.Application.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Shell;
using Microsoft.Extensions.Options;

namespace Infinity.Tests;

public sealed class PageTitleStoreTests
{
    [Fact]
    public async Task ReorderKeepsGeneratedTitlesPositionBased()
    {
        Settings settings = new();
        PageTitleStore store = new(
            new TestOptionsMonitor(settings),
            new TestWritableOptions(settings),
            new TestTextLocalizer());

        IReadOnlyDictionary<int, string> titles = await store.ReorderAsync(2, 1);

        Assert.Equal("Page 2", titles[1]);
        Assert.Equal("Page 3", titles[2]);
        Assert.Empty(settings.PageTitles!);
    }

    [Fact]
    public async Task ReorderRemovesPreviouslyMaterialisedGeneratedTitles()
    {
        Settings settings = new()
        {
            PageTitles = new Dictionary<int, string>
            {
                [1] = "Page 2",
                [2] = "Page 3"
            }
        };
        PageTitleStore store = new(
            new TestOptionsMonitor(settings),
            new TestWritableOptions(settings),
            new TestTextLocalizer());

        IReadOnlyDictionary<int, string> titles = await store.ReorderAsync(2, 1);

        Assert.Equal("Page 2", titles[1]);
        Assert.Equal("Page 3", titles[2]);
        Assert.Empty(settings.PageTitles!);
    }

    [Fact]
    public async Task ReorderMovesCustomTitleWithItsPageIdentity()
    {
        Settings settings = new()
        {
            PageTitles = new Dictionary<int, string>
            {
                [2] = "Work"
            }
        };
        PageTitleStore store = new(
            new TestOptionsMonitor(settings),
            new TestWritableOptions(settings),
            new TestTextLocalizer());

        IReadOnlyDictionary<int, string> titles = await store.ReorderAsync(2, 1);

        Assert.Equal("Work", titles[1]);
        Assert.Equal("Page 3", titles[2]);
        Assert.Equal("Work", settings.PageTitles![1]);
    }

    private sealed class TestTextLocalizer : ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) =>
            key == "PageTitle" ? $"Page {arguments[0]}" : key;
    }

    private sealed class TestWritableOptions(Settings settings) : IWritableOptions<Settings>
    {
        public Task<Settings?> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Settings?>(settings);

        public Task WriteAsync(Action<Settings> update, CancellationToken cancellationToken = default)
        {
            update(settings);
            return Task.CompletedTask;
        }

        public Task WriteAsync(Settings value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TestOptionsMonitor(Settings settings) : IOptionsMonitor<Settings>
    {
        public Settings CurrentValue => settings;

        public Settings Get(string? name) => settings;

        public IDisposable? OnChange(Action<Settings, string?> listener) => null;
    }
}
