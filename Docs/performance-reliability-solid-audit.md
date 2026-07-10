# Performance, Reliability, Correctness, and Architecture Audit

Baseline: `origin/main` at `b367e897f4970120ac1430f33447a081c9dbec3c` (`WIP`).

The solution contains eight managed projects and one native C++ project. There are no test projects in the baseline. The established architecture separates application contracts, application logic, platform contracts, Windows implementations, shell/view-model logic, WinUI views, native composition, and dependency-injection modules. The audit preserves those boundaries and only proposes new boundaries where ownership or testability requires one.

## Baseline validation

- Managed dependency restore succeeds with .NET SDK 10.0.301.
- Managed projects compile when the external sibling `Elysium/build/Release` output is available.
- The broad Release/x64 solution build requires Visual Studio MSBuild and the ignored `packages` directory used by the native project.
- The initial isolated solution build was blocked by worktree-external package paths, not by source compilation. The validation worktree uses junctions to the existing ignored dependencies; these are not repository changes.
- No automated tests exist in the baseline.

## Prioritised confirmed findings

### 1. Critical correctness issues

#### C1. Desktop background COM work can deadlock callers

- **Affected code:** `Infinity.Platform.Windows/DesktopBackgroundSource.cs` (`DesktopBackgroundSource`, `RunOnComThread`, constructor, `GetBackground`, `Dispose`); called by `Infinity.Shell/Desktop/DesktopBackgroundController.cs`; registered as a singleton in `Infinity.Platform.DependencyInjection.Windows/IServiceCollectionExtensions.cs`.
- **Problem/root cause:** `RunOnComThread` queues an action that assigns the result and then signals a `ManualResetEventSlim`. If the delegate throws before `Set`, the COM thread continues to its next work item but the caller waits forever. Queue completion/disposal can also make `Add` fail and the method silently returns `default`, weakening the source contract.
- **Why confirmed:** the signal is not in a `finally` block and no exception is transported to the waiting caller. Constructor and UI-facing calls synchronously enter this path.
- **Impact:** startup or background refresh can hang permanently; shutdown may leave the STA worker running; failures are converted into misleading empty background state.
- **Proposed fix:** introduce a completion object that captures result/exception and always signals; reject work after disposal; make disposal idempotent, stop the timer, complete the queue, join the worker, release the COM projection on its owning STA, and dispose synchronization primitives.
- **Architecture:** keeps COM apartment and object lifetime entirely inside the Windows platform implementation.
- **Risk:** medium, because shutdown ordering and COM release affinity change.
- **Validation:** success, delegate failure, partial construction, concurrent timer callback, disposal during queued work, repeated disposal, worker termination, and controller call behavior.

### 2. Race conditions and threading issues

#### T1. Restore suppression is unsynchronised and renewed marks expire early

- **Affected code:** `Infinity.Application/WindowRestoreGuard.cs`; used by `WindowTracker.TryRegister` and `WindowTracker.HandleWindowMovedExternally`; singleton registration in `Infinity.Application.DependencyInjection/IServiceCollectionExtensions.cs`.
- **Problem/root cause:** a normal `HashSet<nint>` is read and written by event/dispatcher paths and by `Task.Delay(...).ContinueWith` on a pool thread. Concurrent access is undefined. Multiple `MarkRestoring` calls schedule independent removals, so the first continuation removes a newer mark before its own 500 ms protection window ends. The continuation is unowned and exceptions are unobserved.
- **Why confirmed:** the continuation necessarily runs asynchronously and accesses the same set without synchronization; callers can mark the same HWND repeatedly during re-registration.
- **Impact:** external-move handling can run during a programmatic restore, corrupting stored canvas coordinates; concurrent collection access can fail.
- **Proposed fix:** replace delayed mutation with a lock-protected expiry map using monotonic timestamps; `IsRestoring` removes only expired entries synchronously. This removes tasks and makes renewal deterministic.
- **Architecture:** the guard remains a focused application service with no new abstraction.
- **Risk:** low.
- **Validation:** concurrent mark/query, repeated marks, expiry, and handle reuse timing.

#### T2. Drag auto-scroll mutates shared/UI-observed state from an unowned pool task

- **Affected code:** `Infinity.Application/WindowDragScroller.cs` (`StartScroll`, `CancelScroll`, cursor handlers); consumers in `Infinity.Shell.WinUI/Modules/ShellModule.cs`, `PageTintViewModel`, `WindowCollection`, and `Scroller`/`PanState`.
- **Problem/root cause:** `Task.Run` repeatedly reads `PanState`, calls `Scroller.ScrollTo`, repositions native windows, raises `OffsetChanged` and `DragScrolled`, while lifecycle and cursor handlers mutate the same fields on the input/dispatcher thread. `scrollCancellation` is neither locked nor volatile; cancellation disposes the CTS before the loop completes; cancellation throws from `Task.Delay` into an unobserved task.
- **Why confirmed:** the code explicitly creates a pool task and directly calls non-thread-safe application services and UI-observed events from it. No dispatcher or task owner mediates those calls.
- **Impact:** races, out-of-order updates, UI-thread affinity violations in subscribers, unobserved cancellation exceptions, and work continuing after `Stop`.
- **Proposed fix:** use the established `IDispatcher` boundary for every scroll tick, own and observe one loop task, synchronize loop generation/CTS state, and make cancellation completion deterministic without blocking the UI thread.
- **Architecture:** preserves application/platform separation and uses the existing dispatcher abstraction rather than introducing a timer wrapper.
- **Risk:** medium.
- **Validation:** concurrent cursor events, direction changes, cancellation, repeated start/stop, boundary exit, disposal during delay, dispatcher rejection, and no post-stop events.

#### T3. Selection preview fire-and-forget work can publish unobserved exceptions

- **Affected code:** `Infinity.Application/SelectionPreviewQueue.cs`; called by `WindowSelector`.
- **Problem/root cause:** `ProcessAsync` is discarded. It handles cancellation only; exceptions from the handle factory or `IWindowStack.BringToFront` fault an unobserved task. `Cancel` cancels but does not dispose the current CTS until/if the asynchronous method reaches `finally`.
- **Why confirmed:** both invoked dependencies are outside the cancellation catch and the returned task has no owner.
- **Impact:** silent preview failures and process-level unobserved exceptions; nondeterministic cleanup.
- **Proposed fix:** explicitly own the in-flight task and log/observe operational failures while retaining last-request-wins behavior; make cancellation and CTS cleanup race-safe.
- **Architecture:** no new abstraction; ownership stays in the queue service.
- **Risk:** low-medium.
- **Validation:** factory failure, stack failure, replacement, cancellation, and out-of-order completion.

### 3. Resource and memory leaks

#### R1. DWM scroll timer does not deterministically terminate or release its wait handle

- **Affected code:** `Infinity.Platform.Windows/DwmFlushScrollTimer.cs`; singleton in `Infinity.Shell.WinUI/Modules/ShellModule.cs`; driven by `Scroller` and `PagerLifetime`.
- **Problem/root cause:** `Dispose` only flips a flag and signals. It does not join the worker or dispose `ManualResetEventSlim`; `Start` and `Stop` remain callable during/after disposal.
- **Why confirmed:** the thread and native wait handle have no completed cleanup path. Host disposal can return while the worker is still invoking `Tick`.
- **Impact:** callbacks can race shutdown and touch disposed services; the wait handle leaks until finalization/process exit.
- **Proposed fix:** add an idempotent disposed state, block new starts, signal and join the worker when safe, then dispose the event.
- **Architecture:** makes the platform service the sole owner of its thread and wait handle.
- **Risk:** low-medium (must avoid self-join).
- **Validation:** inactive/active disposal, repeated disposal, start/stop races, callback-time disposal, and no post-dispose ticks.

### 4. Performance improvements

#### P1. Every preview mutation rebuilds two lists and an array and repeats availability P/Invoke

- **Affected code:** `Infinity.Platform.Windows/DwmWindowPreviewSurface.cs` (`RenderCore`); called by every preview apply/remove/render and WinUI size/target update.
- **Problem/root cause:** each render allocates `List<DwmThumbnailVisualItem>`, `List<DwmWindowPreview>`, and a copied array, then calls `DwmThumbnailVisual_IsAvailable` even after the DLL/entry point has already been successfully resolved.
- **Why confirmed:** the allocations and native call occur unconditionally in the hot render method. Size changes and scrolling fan into this path across all visible previews.
- **Impact:** avoidable Gen0 pressure and repeated interop during layout/scroll bursts.
- **Proposed fix:** reuse capacity-owned buffers under the existing surface lock and cache only the stable bridge-availability result. Preserve per-render HRESULT reporting.
- **Architecture:** buffers remain private to the native-resource owner; no cross-layer caching.
- **Risk:** low-medium because stale entries must be cleared correctly.
- **Validation/evidence:** measure allocations and availability-call count over repeated identical render batches before and after; validate add/remove/reinitialize/failure paths.

### 5. SOLID and architecture improvements

#### A1. Desktop background source mixes queue mechanics with COM ownership without a safe ownership boundary

- **Affected code:** same path as C1.
- **Problem/root cause:** the class legitimately owns desktop-wallpaper querying, but its ad-hoc queue protocol also defines synchronization, exception transport, shutdown, and COM lifetime. Those responsibilities are inseparable operationally yet currently have no explicit work-item boundary.
- **Why confirmed:** the missing boundary directly causes C1 and the resource leak; this is not a line-count objection.
- **Impact:** failure and disposal behavior is difficult to reason about or test.
- **Proposed fix:** add a private, typed work-item implementation inside the platform service rather than a public interface or one-line wrapper. Keep the public service and DI registration unchanged.
- **SOLID justification:** improves SRP internally and preserves DIP: shell/application consumers continue to depend only on `IDesktopBackgroundSource`.
- **Risk/validation:** covered by C1.

#### A2. Obsolete native capture API mirrors exports that no longer exist

- **Affected code:** `Infinity.Platform.Windows/NativeWindowCapture.cs`; native exports in `Infinity.Platform.Windows.Native/NativeExports.h/.cpp`.
- **Problem/root cause:** the managed static class declares `Begin/Add/Update/Commit` entry points and Cdecl calling conventions, while the native DLL exports only `IsAvailable/RenderBatch/Clear/GetLast*` as Stdcall. Repository-wide tracing finds no caller of `NativeWindowCapture`.
- **Why confirmed:** declarations and exports are directly inconsistent and the class has no consumers.
- **Impact:** future callers would receive `EntryPointNotFoundException`; duplicate interop surfaces obscure the single native owner and AOT interop review.
- **Proposed fix:** remove the unreferenced obsolete class after confirming the complete solution build has no consumer.
- **Architecture:** restores one interop boundary (`DwmWindowPreviewSurface`) and one native resource owner.
- **Risk:** low.
- **Validation:** repository reference scan plus full build.

### 6. Reliability improvements

#### L1. DispatcherQueue rejection permanently wedges preview target coalescing

- **Affected code:** `Infinity.Shell.WinUI/Views/TrackedWindowView.xaml.cs` (`QueuePreviewTargetUpdate`); called by load, data-context, size, and property-change paths.
- **Problem/root cause:** `isPreviewTargetQueued` is set before `TryEnqueue`, but the return value is ignored. If the queue is shutting down or rejects the callback, the flag is never reset and every later update is suppressed.
- **Why confirmed:** `DispatcherQueue.TryEnqueue` reports rejection through `false`; only the queued delegate clears the flag.
- **Impact:** thumbnails can remain missing or incorrectly sized for the lifetime of a recycled view.
- **Proposed fix:** clear the flag immediately when enqueue fails and guard queued callbacks against data-context generation changes.
- **Architecture:** keeps WinUI queue semantics in the view layer.
- **Risk:** low.
- **Validation:** enqueue failure, unload before callback, data-context replacement, repeated size events, and duplicate notifications.

#### L2. Update restart callback is async-void and queue rejection is ignored

- **Affected code:** `Infinity.Shell.WinUI/Modules/UpdateModule.cs`; update controller and application lifetime callbacks.
- **Problem/root cause:** an `async` lambda is converted to `DispatcherQueueHandler` (`async void`). Exceptions from `ExitAsync` cannot be observed by the notifier/controller path, and a rejected enqueue silently loses the user-requested restart after the update has been marked for apply-on-exit.
- **Why confirmed:** the delegate type is void-returning and both `TryEnqueue` results are ignored.
- **Impact:** update restart can fail silently or surface as an unhandled UI exception.
- **Proposed fix:** enqueue a synchronous handler that starts an explicitly observed async operation with logging; only call `ApplyOnExit` inside the accepted operation, and report rejection.
- **Architecture:** async ownership belongs to a small WinUI update coordinator/local helper rather than the toast callback.
- **Risk:** medium because update/shutdown behavior changes.
- **Validation:** accepted/rejected dispatch, `ExitAsync` failure, duplicate toast activation, and shutdown in progress.

### 7. Smaller confirmed bugs

#### S1. Repeated lifecycle starts can duplicate event subscriptions

- **Affected code:** `WindowDragScroller.Start`, `Scroller.Start`, and several application lifecycle services; orchestrated by `PagerLifetime`.
- **Problem/root cause:** unlike `ForegroundWindowTracker` and `WindowStack`, these `Start` methods subscribe unconditionally. A repeated host/lifetime start doubles callbacks, while one `Stop` removes only one registration in event implementations that allow duplicates.
- **Why confirmed:** the methods have no state guard and are public lifecycle contracts. The host contract permits start/stop failure recovery and tests/manual reinitialization can call them repeatedly.
- **Impact:** duplicate scrolling and state transitions after reinitialization.
- **Proposed fix:** add idempotent start/stop guards to affected services while preserving ordering.
- **Architecture:** lifecycle ownership remains in each subscriber.
- **Risk:** low.
- **Validation:** repeated start, repeated stop, start-stop-start, and event counts.

## Deliberately not classified as defects

- Large coordinator/view-model classes are not split solely for line count. `WindowPageCoordinator` has a broad but cohesive state-machine responsibility; changes require a separately demonstrated contract or ownership defect.
- HWND values in application contracts are represented as `nint`/`IntPtr`, not Win32 `HWND`, so platform-native structs do not leak into platform-independent layers.
- The native composition module uses process-lifetime cached devices and a singleton managed surface. Its global state is currently serialized by that owner; speculative multi-surface support is not introduced.
- Empty WinUI composition catches are recorded for manual diagnostics review, but are not automatically changed where best-effort visual behavior is intentional and no functional contract is lost.

## Resolution and final evidence

All confirmed findings above were addressed on the rollup branch. During validation, four additional confirmed build/runtime-contract issues were found and fixed:

- The composition root manually constructed `SelectionPreviewQueue`; it now supplies the queue's logger, preserving explicit constructor injection.
- The shared `IDispatcher` adapter silently discarded rejected `DispatcherQueue` work even though background coordinators already handle `InvalidOperationException`; rejection now surfaces through that established contract.
- The debug window's try-pattern lacked a `[NotNullWhen(true)]` contract, producing a nullable warning at its only caller.
- `PreviewPositionView` used reflective `SelectedValuePath` lookup. It now uses a compiled `SelectedIndex` mapping, preserving the displayed order and values while removing the Native AOT/trimming warning without suppression.

Performance evidence for P1 is structural and call-count based:

- **Before:** every `RenderCore` call allocated two `List<T>` instances plus one copied array and called `DwmThumbnailVisual_IsAvailable` once.
- **After:** render buffers grow geometrically and are reused under the existing surface lock, resulting in zero steady-state render-buffer allocations after capacity is reached; bridge availability is probed once per surface instead of once per render. The batch still reports each native item HRESULT and clears retained preview references after every call.

Final managed validation:

- `dotnet build Infinity.Shell.WinUI/Infinity.Shell.WinUI.csproj -c Release -p:Platform=x64 --no-restore`: succeeded with 0 warnings and 0 errors. This transitively built all eight managed projects.
- No test projects exist in the repository, so there were no baseline automated tests to run.
- The native C++ build reached `CL.exe` after resolving all ignored package dependencies, then failed in the local MSBuild host before compilation because the process environment contains duplicate case-variant `Path` and `PATH` keys (`MSB6001`). This environment failure also occurs without source changes and is recorded as a validation limitation rather than modified around in the repository.

Manual validation still required on a normal desktop session:

- Repeated drag-to-edge scrolling, direction changes, boundary cancellation, and shutdown during auto-scroll.
- Wallpaper path/colour refresh and shutdown while a background query is pending.
- Thumbnail creation, resize, recycling, unload/reload, DWM bridge failure, and multi-window render batches.
- Update-ready toast activation, restart acceptance, and dispatcher shutdown rejection.
- Preview-position selection persistence across Auto, Top, and Bottom.
