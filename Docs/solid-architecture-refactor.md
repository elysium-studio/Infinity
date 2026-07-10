# SOLID and Architecture Refactor

Baseline: merged `main` at `1071b1e512add701b1f3b42b4844869c73c301e7`.

This review traces project references, dependency-injection registrations, interface consumers, platform/native ownership, event lifetimes, and the responsibilities of the largest classes. Refactors are limited to demonstrated dependency, ownership, or consumer-contract problems.

## Confirmed improvements

### 1. Remove duplicate application-service registrations

`AddInfinityApplication` registers `IWindowPageCoordinator`, `IWindowFilterState`, `ISelectionPreviewQueue`, and `IWindowSelector`. `ShellModule` registers new instances for three of those contracts, relying on last-registration-wins behavior and bypassing `ISelectionPreviewQueue`. This obscures which singleton owns state and makes registration order behavioral. The shell composition root should configure shell-only services and consume the application registrations once.

### 2. Reverse the Shell-to-Application implementation dependency

`Infinity.Shell` references `Infinity.Application` only for a presentation messenger record and the mutable `WindowPeekSource` implementation. The message belongs to the shell presentation layer. The mutable peek source needs a narrow application contract because the shell sets its selected HWND while application logic evaluates visibility. Moving the message and introducing that boundary removes the concrete project reference without adding a general-purpose abstraction.

### 3. Separate filter matching from presentation-session state

`WindowFilterState` combines filter matching with selected-result history and directly mutates `ITrackedWindow` presentation objects. All selection-history and bulk-application members have exactly one consumer: `TrackedWindowCollectionViewModel`. Meanwhile `ITrackedWindowFilter` exists only so `WindowFilterState` can wrap the shell implementation. Keep `IWindowFilterState` as the narrow cross-layer matching contract, implement it directly in the shell, and keep result-selection history in the owning view model.

### 4. Remove Windows interop from drag-scroll application logic

`WindowDragScroller` imports `GetAsyncKeyState` solely to include mouse-button state in diagnostic log messages. This creates a Windows implementation dependency inside the platform-independent application project without affecting behavior. Remove the native diagnostic and retain the functional log state.

### 5. Give Windows arranging one resource owner

`WindowDragGuard` currently owns drag recognition, resize detection, modifier polling, drag restart, a process-wide Windows arranging setting, crash-recovery persistence, and process-exit cleanup. The system setting and recovery file form a separate native resource with their own lifetime. Extract a concrete `WindowArrangingController` owned through DI; keep drag state/restart behavior in `WindowDragGuard`. No interface is added because there is one Windows implementation and the concrete boundary is sufficient.

### 6. Segregate window coordinator consumers

`IWindowPageCoordinator` mixes navigation state/commands/events with raw foreground, minimize, and close notifications. The navigation view model/module and platform-event collection use disjoint subsets. Introduce consumer-driven navigation and foreground-event interfaces while retaining the composite interface for API compatibility. Register all contracts over one `WindowPageCoordinator` singleton.

### 7. Narrow hosted lifecycle dependency

`PagerLifetime` depends on the full `IWindowCollection` interface even though it only starts and stops it. That interface exposes seven events, queries, refresh commands, and reorder commands. Add a narrow lifecycle contract, retain `IWindowCollection` as the composite public contract, and map both to the same singleton instance.

### 8. Place application tracking in the application boundary

`IWindowTracker` is implemented by the application layer and orchestrated by the application lifetime, but its source file is compiled into `Infinity.Platform.Abstractions` under the `Infinity.Application.Abstractions` namespace. Move the unchanged contract to `Infinity.Application.Abstractions` so assembly ownership and namespace ownership agree and the platform boundary no longer publishes an application service contract.

### 9. Honour the navigator abstraction at startup

Startup resolves `INavigator` but only navigates when the instance is the current concrete `Navigator`, even though `NavigateAsync` is part of the interface and other consumers use it through that contract. Call the interface directly so a substitutable implementation receives the same startup behaviour.

### 10. Remove the unused window-mover operation

`IWindowMover.Flush` has no callers and its only implementation is empty. Remove the member from the interface and implementation, leaving the actual batching contract (`BeginBatch`, `MoveTo`, and `EndBatch`) explicit and eliminating a no-op guarantee that implementations were forced to provide.

## Reviewed without refactoring

- `WindowPageCoordinator` is large but its foreground-follow suppression and navigation state form one timing-sensitive state machine. The interface is segregated, but the implementation is not split into mutually dependent services.
- `WindowTracker` combines registration, minimize suspension, and reconciliation around one tracked-window lifecycle and one store. Splitting it would duplicate ownership of window state.
- `TrackedWindowCollectionViewModel` has many presentation responsibilities, but its window projection, filtering, selection, peek, and navigation behavior share observable collection state. The unrelated filter-history storage is moved locally; no manager/facade is introduced solely to reduce constructor or line count.
- WinUI tutorial views contain repeated animation mechanics, but they encode view-specific timing and visual elements. A shared animation framework would increase coupling without an ownership or testing benefit.
- `DwmWindowPreviewSurface`, `DwmWindowPreview`, proxy handles, and the native DLL retain their existing single-owner resource boundaries.
- `DesktopBackgroundSource` retains its private STA queue because extracting a general executor used by one COM service would add indirection without changing ownership.

## Validation

- Every affected managed project was built in Release/x64 after its change with zero compiler warnings and errors.
- The complete `Infinity.slnx` Release/x64 build succeeds through Visual Studio MSBuild, including `Infinity.Platform.Windows.Native`.
- Native AOT publish for `Infinity.Shell.WinUI` succeeds. It reports pre-existing trim/AOT warnings in Elysium, CommunityToolkit activation overrides, and Windows Desktop runtime assemblies; this branch does not modify the reported paths.
- No test projects are present in the solution.
- `dotnet build Infinity.slnx` cannot load the installed Visual C++ targets; Visual Studio MSBuild was used for the complete native-capable build.
