using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;
using Microsoft.Extensions.Options;

namespace Infinity.Tests;

public sealed class WindowPlacementRulesTests
{
    [Fact]
    public async Task SetTargetPagePersistsRuleForApplication()
    {
        TestIdentityProvider identityProvider = new("path:C:\\APPS\\EDITOR.EXE");
        TestOptionsMonitor options = new(new Settings());
        TestWritableOptions writer = new(options.CurrentValue);
        WindowPlacementRules rules = new(identityProvider, options, writer);

        bool saved = await rules.SetTargetPageAsync(new IntPtr(1), 4);

        Assert.True(saved);
        Assert.Equal(1, writer.WriteCount);
        Assert.Equal(4, writer.Value.ApplicationPageRules![identityProvider.ApplicationId]);
        Assert.True(rules.TryGetTargetPage(new IntPtr(2), out int page));
        Assert.Equal(4, page);
    }

    [Fact]
    public async Task RemoveDeletesExistingRule()
    {
        TestIdentityProvider identityProvider = new("path:C:\\APPS\\EDITOR.EXE");
        Settings settings = new()
        {
            ApplicationPageRules = new(StringComparer.OrdinalIgnoreCase)
            {
                [identityProvider.ApplicationId] = 2
            }
        };
        TestOptionsMonitor options = new(settings);
        TestWritableOptions writer = new(settings);
        WindowPlacementRules rules = new(identityProvider, options, writer);

        bool removed = await rules.RemoveAsync(new IntPtr(1));

        Assert.True(removed);
        Assert.Empty(writer.Value.ApplicationPageRules!);
        Assert.False(rules.TryGetTargetPage(new IntPtr(1), out _));
    }

    [Fact]
    public async Task RuleOperationsRejectWindowsWithoutStableIdentity()
    {
        TestIdentityProvider identityProvider = new(null);
        TestOptionsMonitor options = new(new Settings());
        TestWritableOptions writer = new(options.CurrentValue);
        WindowPlacementRules rules = new(identityProvider, options, writer);

        bool saved = await rules.SetTargetPageAsync(new IntPtr(1), 2);
        bool removed = await rules.RemoveAsync(new IntPtr(1));

        Assert.False(rules.CanCreateRule(new IntPtr(1)));
        Assert.False(saved);
        Assert.False(removed);
        Assert.Equal(0, writer.WriteCount);
    }

    private sealed class TestIdentityProvider(string? applicationId) : IWindowApplicationIdentityProvider
    {
        public string ApplicationId { get; } = applicationId ?? string.Empty;

        public bool TryGetApplicationId(IntPtr windowHandle, out string applicationId)
        {
            applicationId = ApplicationId;
            return applicationId.Length > 0;
        }
    }

    private sealed class TestOptionsMonitor(Settings currentValue) : IOptionsMonitor<Settings>
    {
        public Settings CurrentValue { get; } = currentValue;

        public Settings Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<Settings, string?> listener) =>
            throw new NotImplementedException();
    }

    private sealed class TestWritableOptions(Settings value) : IWritableOptions<Settings>
    {
        public Settings Value { get; } = value;

        public int WriteCount { get; private set; }

        public Task<Settings?> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Settings?>(Value);

        public Task WriteAsync(Action<Settings> update, CancellationToken cancellationToken = default)
        {
            WriteCount++;
            update(Value);
            return Task.CompletedTask;
        }

        public Task WriteAsync(Settings value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
