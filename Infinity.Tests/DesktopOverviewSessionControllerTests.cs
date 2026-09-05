using Elysium.Application.Abstractions;
using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;
using Infinity.Shell;

namespace Infinity.Tests;

public sealed class DesktopOverviewSessionControllerTests
{
    [Fact]
    public void ModifiedScrollOpensTheSurfaceWithoutStartingPresentation()
    {
        SessionFixture fixture = new();
        fixture.ModifierKeyState.IsActive = true;
        fixture.Pointer.RaiseScroll(120);
        Assert.True(fixture.Controller.State.IsOpen);
        Assert.False(fixture.Controller.State.IsPreviewActive);
        Assert.True(fixture.GlanceBridge.IsDesktopOverviewVisible);
    }


    [Fact]
    public void ScrollerStartBeginsAnOpenPresentationSession()
    {
        SessionFixture fixture = new();
        fixture.Scroller.RaiseStarted();
        Assert.True(fixture.PresentationSession.IsActive);
        Assert.True(fixture.Controller.State.IsOpen);
        Assert.True(fixture.Controller.State.StaysOpen);
        Assert.True(fixture.Controller.State.IsPreviewActive);
    }


    [Fact]
    public void DismissalBecomesReadyOnlyAfterTheExitAnimation()
    {
        SessionFixture fixture = new();
        fixture.Scroller.RaiseStarted();
        fixture.Controller.DismissPreview();
        Assert.True(fixture.Controller.State.IsCompletionRequested);
        Assert.False(fixture.Controller.State.IsReadyToClose);
        fixture.Controller.NotifyExitAnimationCompleted();
        Assert.True(fixture.Controller.State.IsReadyToClose);
    }


    [Fact]
    public void SettingsNavigationRunsAfterThePreviewCompletes()
    {
        SessionFixture fixture = new();
        fixture.Scroller.RaiseStarted();
        fixture.Controller.NavigateToSettings();
        Assert.Equal(0, fixture.SettingsNavigator.NavigationCount);
        fixture.Controller.NotifyExitAnimationCompleted();
        fixture.Controller.CompletePreview();
        Assert.Equal(1, fixture.SettingsNavigator.NavigationCount);
        Assert.False(fixture.Controller.State.IsOpen);
        Assert.False(fixture.PresentationSession.IsActive);
    }


    private sealed class SessionFixture
    {
        public SessionFixture() => Controller = new(new TestDispatcher(), Pointer, ModifierKeyState, new TestPageGestureSource(), new TestPager(), Scroller, PresentationSession, new TestWindowPreviewSurface(), new TestWindowNavigationCoordinator(), GlanceBridge, SettingsNavigator);

        public TestPointerInputSource Pointer { get; } = new();

        public TestModifierKeyState ModifierKeyState { get; } = new();

        public TestScroller Scroller { get; } = new();

        public TestScrollPresentationSession PresentationSession { get; } = new();

        public TestGlanceBridge GlanceBridge { get; } = new();

        public TestSettingsNavigator SettingsNavigator { get; } = new();

        public DesktopOverviewSessionController Controller { get; }
    }


    private sealed class TestDispatcher : IDispatcher
    {
        public void Dispatch(Action action) => action();
    }


    private sealed class TestPointerInputSource : IPointerInputSource
    {
        public event Action<int>? ScrollDeltaReceived;

        event Action<int, int>? IPointerInputSource.CursorMoved
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action? IPointerInputSource.LeftButtonClicked
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action? IPointerInputSource.MiddleButtonClicked
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action? IPointerInputSource.RightButtonClicked
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action<double>? IPointerInputSource.ScrollVelocityIdle
        {
            add
            {
            }

            remove
            {
            }
        }


        public void RaiseScroll(int delta) => ScrollDeltaReceived?.Invoke(delta);

        public void Dispose() => GC.SuppressFinalize(this);
    }


    private sealed class TestModifierKeyState : IModifierKeyState
    {
        event Action<bool>? IModifierKeyState.StateChanged
        {
            add
            {
            }

            remove
            {
            }
        }


        public bool IsActive { get; set; }


        public void SetKeys(List<List<int>> combinations)
        {
        }


        public void Dispose() => GC.SuppressFinalize(this);
    }


    private sealed class TestPageGestureSource : IPageGestureSource
    {
        event Action? IPageGestureSource.SessionStarted
        {
            add
            {
            }

            remove
            {
            }
        }


        event Action? IPageGestureSource.SessionEnded
        {
            add
            {
            }

            remove
            {
            }
        }


        public void Start()
        {
        }


        public void Stop()
        {
        }
    }


    private sealed class TestPager : IPager
    {
        public event Action<int>? PageChanged;

        public int CurrentPage => 0;

        public int PageCount => 2;

        public int? MaxPages => null;

        public bool IsPageCentered(int page) => page == 0;

        public void SetMaxPages(int? maxPages)
        {
        }


        public void NavigateToPage(int page) => PageChanged?.Invoke(page);

        public void Start()
        {
        }


        public void Stop()
        {
        }
    }


    private sealed class TestScroller : IScroller
    {
        public event EventHandler? ScrollStarted;

        public event EventHandler? ScrollStopped;

        public double VisualOffset => 0;

        public void RaiseStarted() => ScrollStarted?.Invoke(this, EventArgs.Empty);

        public void CancelNavigation()
        {
        }


        public void CommitPresentation()
        {
        }


        public void Dispose() => GC.SuppressFinalize(this);

        public void OnTick()
        {
        }


        public void Reposition()
        {
        }


        public void Reset()
        {
        }


        public void ScrollTo(double offset, bool animate = true)
        {
        }


        public void Start()
        {
        }


        public void Stop() => ScrollStopped?.Invoke(this, EventArgs.Empty);
    }


    private sealed class TestScrollPresentationSession : IScrollPresentationSession
    {
        public bool IsActive { get; private set; }


        public void Begin() => IsActive = true;

        public void End() => IsActive = false;
    }


    private sealed class TestWindowPreviewSurface : IWindowPreviewSurface
    {
        public bool IsAvailable => true;

        public void Initialize(nint ownerWindowHandle)
        {
        }


        public void Clear()
        {
        }
    }


    private sealed class TestWindowNavigationCoordinator : IWindowNavigationCoordinator
    {
        event EventHandler<NavigationStartedEventArgs>? IWindowNavigationCoordinator.NavigationStarted
        {
            add
            {
            }

            remove
            {
            }
        }


        event EventHandler? IWindowNavigationCoordinator.NavigationCompleted
        {
            add
            {
            }

            remove
            {
            }
        }


        event EventHandler? IWindowNavigationCoordinator.WindowActivationRequested
        {
            add
            {
            }

            remove
            {
            }
        }


        public int NavigationTargetPage { get; set; } = -1;

        public double NavigationTargetOffset { get; set; }

        public nint PendingActivation { get; set; }


        public void NavigateTo(nint handle)
        {
        }


        public void NavigateToPage(nint handle)
        {
        }


        public void Activate(nint handle)
        {
        }


        public void CancelNavigation()
        {
        }


        public void CompleteNavigation()
        {
        }
    }


    private sealed class TestGlanceBridge : IInfinityGlanceBridge
    {
        public bool IsPageNavigationAvailable => false;

        public bool IsDesktopOverviewVisible { get; private set; }


        event EventHandler<InfinityGlanceAvailabilityChangedEventArgs>? IInfinityGlanceBridge.AvailabilityChanged
        {
            add
            {
            }

            remove
            {
            }
        }


        event EventHandler<InfinityGlanceMessageReceivedEventArgs>? IInfinityGlanceBridge.MessageReceived
        {
            add
            {
            }

            remove
            {
            }
        }


        public void PublishPageNavigation(InfinityPageNavigationState state)
        {
        }


        public void SetPageNavigationSurfaceVisible(InfinityPageNavigationSurface surface, bool isVisible)
        {
            if (surface == InfinityPageNavigationSurface.DesktopOverview)
            {
                IsDesktopOverviewVisible = isVisible;
            }
        }
    }


    private sealed class TestSettingsNavigator : IDesktopOverviewSettingsNavigator
    {
        public int NavigationCount { get; private set; }


        public Task NavigateAsync()
        {
            NavigationCount++;
            return Task.CompletedTask;
        }
    }
}
