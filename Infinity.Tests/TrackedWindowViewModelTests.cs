using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Infinity.Tests;

public sealed class TrackedWindowViewModelTests
{
    [Fact]
    public void PageTargetsIncludeNamedEmptyPages()
    {
        Settings settings = new()
        {
            PageTitles = new()
            {
                [3] = "Test"
            }
        };
        TrackedWindowViewModel viewModel = CreateViewModel(new TestPager(), settings);

        IReadOnlyList<WindowPageTarget> targets = viewModel.GetPageTargets(null);

        Assert.Equal(4, targets.Count);
        Assert.Equal(new WindowPageTarget(3, "Test"), targets[3]);
    }

    [Fact]
    public void PageTargetsRetainSavedEmptyDestination()
    {
        TrackedWindowViewModel viewModel = CreateViewModel(new TestPager(), new Settings());

        IReadOnlyList<WindowPageTarget> targets = viewModel.GetPageTargets(9);

        Assert.Equal(10, targets.Count);
        Assert.Equal(new WindowPageTarget(9, "Page 10"), targets[9]);
    }

    private static TrackedWindowViewModel CreateViewModel(IPager pager, Settings settings) =>
        new(new TestServiceProvider(),
            new TestServiceFactory(),
            new WeakReferenceMessenger(),
            new TestDisposer(),
            new TestWindowController(),
            new TestPageMover(),
            new TestPlacementRules(),
            new TestStickyWindowController(),
            new TestTrackedWindowDragController(),
            pager,
            new TestOptionsMonitor(settings),
            new TestLocalizer(),
            NullLogger<TrackedWindowViewModel>.Instance,
            new IntPtr(1));

    private sealed class TestPager : IPager
    {
        public event Action<int>? PageChanged;

        public int CurrentPage => 0;

        public int PageCount => 1;

        public int? MaxPages => null;

        public void NavigateToPage(int page) => PageChanged?.Invoke(page);

        public void SetMaxPages(int? maxPages)
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class TestPageMover : IWindowPageMover
    {
        public bool MoveToPage(IntPtr windowHandle, int targetPage) => true;

        public bool TryGetPage(IntPtr windowHandle, out int page)
        {
            page = 0;
            return true;
        }
    }

    private sealed class TestPlacementRules : IWindowPlacementRules
    {
        public bool CanCreateRule(IntPtr windowHandle) => true;

        public Task<bool> RemoveAsync(IntPtr windowHandle) => Task.FromResult(true);

        public Task<bool> SetTargetPageAsync(IntPtr windowHandle, int targetPage) => Task.FromResult(true);

        public bool TryGetTargetPage(IntPtr windowHandle, out int targetPage)
        {
            targetPage = 0;
            return false;
        }
    }

    private sealed class TestStickyWindowController : IStickyWindowController
    {
        public bool IsSticky(IntPtr windowHandle) => false;

        public bool Pin(IntPtr windowHandle) => true;

        public bool Unpin(IntPtr windowHandle) => true;
    }

    private sealed class TestTrackedWindowDragController : ITrackedWindowDragController
    {
        public IntPtr DraggingWindow => IntPtr.Zero;

        public bool Begin(IntPtr windowHandle) => true;

        public bool Move(IntPtr windowHandle, double horizontalDelta, double verticalDelta) => true;

        public void End(IntPtr windowHandle)
        {
        }
    }

    private sealed class TestWindowController : IWindowController
    {
        public void Close(IntPtr handle)
        {
        }

        public void Minimize(IntPtr handle)
        {
        }

        public void Restore(IntPtr handle)
        {
        }
    }

    private sealed class TestOptionsMonitor(Settings settings) : IOptionsMonitor<Settings>
    {
        public Settings CurrentValue { get; } = settings;

        public Settings Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<Settings, string?> listener) =>
            throw new NotImplementedException();
    }

    private sealed class TestLocalizer : ITextLocalizer
    {
        public string GetText(string key, params object[] arguments)
        {
            return string.Format(CultureInfo.InvariantCulture, "Page {0}", arguments);
        }
    }

    private sealed class TestServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class TestServiceFactory : IServiceFactory
    {
        public object Create(Type type, Action<object> serviceDelegate, params object?[]? parameters) =>
            throw new NotSupportedException();

        public object Create(Type type, params object?[]? parameters) => throw new NotSupportedException();

        public TService Create<TService>(Action<TService> serviceDelegate, params object?[]? parameters) =>
            throw new NotSupportedException();

        public TService Create<TService>(params object?[]? parameters) => throw new NotSupportedException();
    }

    private sealed class TestDisposer : IDisposer
    {
        public void Add(object subject, params object[] objects)
        {
        }

        public void Dispose(object subject)
        {
        }

        public void Remove(object subject, IDisposable disposer)
        {
        }

        public TDisposable Replace<TDisposable>(object subject, IDisposable disposer, TDisposable replacement)
            where TDisposable : IDisposable => replacement;
    }
}
