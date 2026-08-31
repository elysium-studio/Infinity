using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopApplicationPickerCatalogTests
{
    [Fact]
    public async Task ApplicationsAreFetchedOnlyOnce()
    {
        TestApplicationCatalog source = new();
        DesktopApplicationPickerCatalog catalog = new(source);

        IReadOnlyList<LaunchableApplication> first = await catalog.GetApplicationsAsync();
        IReadOnlyList<LaunchableApplication> second = await catalog.GetApplicationsAsync();

        Assert.Same(first, second);
        Assert.Equal(1, source.ApplicationRequestCount);
    }

    [Fact]
    public async Task IconsRemainLazyAndAreDelegatedPerRequest()
    {
        TestApplicationCatalog source = new();
        DesktopApplicationPickerCatalog catalog = new(source);
        LaunchableApplication application = Assert.Single(await catalog.GetApplicationsAsync());

        Assert.Equal(0, source.IconRequestCount);

        await catalog.GetIconAsync(application);

        Assert.Equal(1, source.IconRequestCount);
    }

    private sealed class TestApplicationCatalog : IApplicationCatalog
    {
        public int ApplicationRequestCount { get; private set; }

        public int IconRequestCount { get; private set; }

        public Task<IReadOnlyList<LaunchableApplication>> GetApplicationsAsync(CancellationToken cancellationToken = default)
        {
            ApplicationRequestCount++;
            return Task.FromResult<IReadOnlyList<LaunchableApplication>>([new("calculator", "Calculator")]);
        }

        public Task<ApplicationIcon?> GetIconAsync(LaunchableApplication application, CancellationToken cancellationToken = default)
        {
            IconRequestCount++;
            return Task.FromResult<ApplicationIcon?>(null);
        }
    }
}
