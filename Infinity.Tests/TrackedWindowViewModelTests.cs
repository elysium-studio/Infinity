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

public class TrackedWindowViewModelTests
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

    [Fact]
    public void PreviewTargetOverridePreservesLatestNormalTarget()
    {
        TestWindowPreview preview = new();
        TrackedWindowViewModel viewModel = CreateViewModel(new TestPager(),
            new Settings(),
            new TestPreviewSurface(preview));

        viewModel.SetPreviewTarget(new IntPtr(10), 100.0, 50.0);
        viewModel.SetPreviewTargetOverride(new IntPtr(20), 120.0, 60.0);
        viewModel.SetPreviewTarget(new IntPtr(30), 140.0, 70.0);

        Assert.Equal(new IntPtr(20), preview.TargetHandle);
        Assert.Equal(120.0, preview.Width);
        Assert.Equal(60.0, preview.Height);

        viewModel.ClearPreviewTargetOverride();

        Assert.Equal(new IntPtr(30), preview.TargetHandle);
        Assert.Equal(140.0, preview.Width);
        Assert.Equal(70.0, preview.Height);
    }

    private static TrackedWindowViewModel CreateViewModel(IPager pager,
        Settings settings,
        IWindowPreviewSurface? previewSurface = null) =>
        new(new TestServiceProvider(),
            new TestServiceFactory(),
            new WeakReferenceMessenger(),
            new TestDisposer(),
            new TestWindowController(),
            previewSurface ?? new TestPreviewSurface(),
            new TestPageMover(),
            new TestPlacementRules(),
            new TestStickyWindowController(),
            new TestTrackedWindowDragController(),
            pager,
            new TestOptionsMonitor(settings),
            new TestLocalizer(),
            NullLogger<TrackedWindowViewModel>.Instance,
            new IntPtr(1));

    private class TestPager : IPager
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

    private class TestPageMover : IWindowPageMover
    {
        public bool MoveToPage(IntPtr windowHandle, int targetPage) => true;

        public bool TryGetPage(IntPtr windowHandle, out int page)
        {
            page = 0;
            return true;
        }
    }

    private class TestPlacementRules : IWindowPlacementRules
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

    private class TestStickyWindowController : IStickyWindowController
    {
        public bool IsSticky(IntPtr windowHandle) => false;

        public bool Pin(IntPtr windowHandle) => true;

        public bool Unpin(IntPtr windowHandle) => true;
    }

    private class TestTrackedWindowDragController : ITrackedWindowDragController
    {
        public IntPtr DraggingWindow => IntPtr.Zero;

        public bool Begin(IntPtr windowHandle) => true;

        public bool Move(IntPtr windowHandle, double horizontalDelta, double verticalDelta) => true;

        public void End(IntPtr windowHandle)
        {
        }
    }

    private class TestWindowController : IWindowController
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

    private class TestPreviewSurface(IWindowPreview? preview = null) : IWindowPreviewSurface
    {
        public bool IsAvailable => false;

        public int LastHResult => 0;

        public int LastBridgeHResult => 0;

        public void Clear()
        {
        }

        public void Commit()
        {
        }

        public IWindowPreview? CreatePreview(IntPtr windowHandle) => preview;

        public void Initialize(IntPtr ownerWindowHandle)
        {
        }

        public void Render()
        {
        }
    }

    private class TestWindowPreview : IWindowPreview
    {
        public IntPtr WindowHandle => new(1);

        public object? KeepAlive { get; set; }

        public IntPtr TargetHandle { get; private set; }

        public double Width { get; private set; }

        public double Height { get; private set; }

        public event Action? PreviewInvalidated
        {
            add
            {
            }
            remove
            {
            }
        }

        public void SetTarget(IntPtr sharedTargetHandle, double width, double height, bool isVisible)
        {
            TargetHandle = sharedTargetHandle;
            Width = width;
            Height = height;
        }

        public void SetPlacement(double x, double y, double width, double height, bool isVisible) =>
            SetTarget(TargetHandle, width, height, isVisible);

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    private class TestOptionsMonitor(Settings settings) : IOptionsMonitor<Settings>
    {
        public Settings CurrentValue { get; } = settings;

        public Settings Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<Settings, string?> listener) =>
            throw new NotImplementedException();
    }

    private class TestLocalizer : ITextLocalizer
    {
        public string GetText(string key, params object[] arguments)
        {
            return string.Format(CultureInfo.InvariantCulture, "Page {0}", arguments);
        }
    }

    private class TestServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private class TestServiceFactory : IServiceFactory
    {
        public object Create(Type type, Action<object> serviceDelegate, params object?[]? parameters) =>
            throw new NotSupportedException();

        public object Create(Type type, params object?[]? parameters) => throw new NotSupportedException();

        public TService Create<TService>(Action<TService> serviceDelegate, params object?[]? parameters) =>
            throw new NotSupportedException();

        public TService Create<TService>(params object?[]? parameters) => throw new NotSupportedException();
    }

    private class TestDisposer : IDisposer
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
