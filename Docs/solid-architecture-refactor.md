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

## Reviewed without refactoring

- `WindowPageCoordinator` is large but its foreground-follow suppression and navigation state form one timing-sensitive state machine. The interface is segregated, but the implementation is not split into mutually dependent services.
- `WindowTracker` combines registration, minimize suspension, and reconciliation around one tracked-window lifecycle and one store. Splitting it would duplicate ownership of window state.
- `TrackedWindowCollectionViewModel` has many presentation responsibilities, but its window projection, filtering, selection, peek, and navigation behavior share observable collection state. The unrelated filter-history storage is moved locally; no manager/facade is introduced solely to reduce constructor or line count.
- WinUI tutorial views contain repeated animation mechanics, but they encode view-specific timing and visual elements. A shared animation framework would increase coupling without an ownership or testing benefit.
- `DwmWindowPreviewSurface`, `DwmWindowPreview`, proxy handles, and the native DLL retain their existing single-owner resource boundaries.
- `DesktopBackgroundSource` retains its private STA queue because extracting a general executor used by one COM service would add indirection without changing ownership.
