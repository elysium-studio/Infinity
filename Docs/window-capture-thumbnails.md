# Capture-backed thumbnails

The checkpoint before this migration is `2dbf8fc`.

The overview now uses Windows Graphics Capture for individual HWNDs. Captured
D3D11 surfaces are drawn GPU-to-GPU into a Win2D composition swap chain. A normal
WinUI `CompositionSurfaceBrush` and `SpriteVisual` display that swap chain inside
the existing thumbnail host. No screen-region capture or CPU bitmap readback is
used. The private DWM thumbnail exports and system visual proxy are removed.

## Responsibilities

- `WindowCaptureSupport`: caches the capability check during startup; input hooks
  only read that cached value and never make a capture COM call.
- `WindowCaptureItemFactory`: documented HWND capture-item interop.
- `WindowCaptureAccess`: requests borderless-capture access once per app session.
- `WindowCaptureSessionOptions`: cursor and optional secondary-window exclusion.
- `WindowCapturePreviewSurface`: starts/stops captures with the overlay lifetime.
- `WindowCapturePreview`: owns one window's frame pool and capture session.
- `WindowCaptureWorkQueue`: serialises capture lifecycle and rendering off the UI
  thread; native frame/closed callbacks only enqueue work and return.
- `WindowCaptureFrameState`: invalidates old session images immediately, and
  allows visibility only after a frame from the current epoch is presented.
- `WindowCaptureFrameReader`: discards superseded queued frames using a bounded
  drain, without starving lifecycle work.
- `WindowCaptureFrameRenderer`: copies valid frame content to the owned swap chain.
- `WindowCaptureSwapChainInterop`: public Win2D/WinUI swap-chain interop.
- `ThumbnailCompositionPreview`: thumbnail size, clipping, shadow and composition.

The page/drag/zoom controllers are not changed. Source window geometry still
determines placement; capture frame geometry independently determines which
pixels can safely be presented. The 180 ms DWM source-refresh retry is removed.

## Resize and lifetime

Frame arrival signals a bounded (one pending frame) serial capture worker. The
XAML thread does not wait for frame rendering or session startup/shutdown. After
growth, a frame clipped by the old allocation is discarded; the previous image
remains until a complete frame arrives. After shrink, only `ContentSize` is
copied, excluding undefined pixels outside it. The old frame is released before
the pool is recreated.

Closing the overlay closes capture sessions and frame pools. The retained
swap-chain allocation is not revealed on reopening until a frame from the new
capture epoch has been presented; a late completion from an old epoch cannot
reveal it. Until then only the existing thumbnail backing is visible; the UI and
overview animation do not wait for capture. Removing a tracked window disposes its remaining
resources. Capture errors are logged with the HWND and invalidate image visibility.
Reopening retries failed sessions; detected device loss replaces the renderer.
Capture sessions are closed outside the callback lock.

Win2D bitmap wrapping, drawing-session creation/drawing/disposal, and swap-chain
resource operations share one graphics gate across all thumbnails. The captured
2026-09-04 hang showed an inverted lock order between Win2D's ResourceManager and
Direct2D when different per-window workers performed those operations concurrently.
Per-window queues alone do not prevent that deadlock. Win2D presentation also
uses its Direct2D resource lock, so it is included in the same serialised operation
after both bitmap and drawing-session wrappers are released.

The native overlay host also monitors UI responsiveness on an independent thread.
After five seconds without a dispatcher response it makes its cached HWNDs fully
transparent and releases cursor confinement, without waiting for XAML or capture.
Overlay keyboard interception is bypassed while emergency-hidden. Once the
dispatcher resumes, it dismisses the overlay normally. An in-process safety
thread cannot run while a debugger suspends the entire process.

There is no fallback to a foreign DWM visual. A window that cannot be captured
has an opaque placeholder. Capture protection is respected. If borderless access
is not granted/available, Windows may display its capture indicator. Optional
secondary-window exclusion is used only where the public interface is supported.
Initial capture waits asynchronously for the borderless-access result and applies
it before starting sessions, avoiding a bordered start while permission is pending.

## Verification still required

No app build or runtime validation was performed during implementation, at the
user's request. Geometry regression tests were added, but have not been run.
Syntax checks and a non-emitting API/type check against the installed assemblies
passed for the capture implementation. These are not runtime rendering tests.

Before release, test on x64 and ARM64:

1. Videosoft playing video: open overview, move to another page, restore,
   maximise, close/reopen. Repeat quickly and with delays. No stale crop,
   oversized content or strip outside the thumbnail should appear.
2. Repeat with and without Visual Studio's WPF in-app debugging tools enabled.
   A separate real owned window leaking above the overview is a window-lifetime/
   z-order issue, not automatically repaired by capturing the main HWND.
3. Exercise windows parked off-screen by Infinity, minimised windows, closed
   windows, and new windows first discovered off-screen. Some applications stop
   producing frames when not visible; do not assume a live frame is available.
4. Compare first opening and rapid page scrolling against the checkpoint, with
   multiple video windows. Measure GPU memory/usage and frame latency. Retained
   swap chains and full-window capture buffers increase memory use versus shared
   DWM visuals.
5. Check mixed-DPI monitors, window resize, rounded corners, drag stacking,
   shadows, closing/reopening during animation, and graphics-device reset.
6. Check permission allowed/denied and protected windows. No transparent hole or
   fallback screen capture should expose underlying desktop content.

The initial capture pipeline is SDR (`B8G8R8A8UIntNormalized`); HDR colour fidelity
requires a separate end-to-end HDR/tone-mapping implementation and validation.

References:

- [Microsoft screen capture guidance](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture)
- [Microsoft HWND capture sample](https://github.com/microsoft/Windows.UI.Composition-Win32-Samples/tree/master/cpp/ScreenCaptureforHWND)
- Public interop declarations are checked against the installed
  `Microsoft.UI.Composition.Interop.h`, `Microsoft.Graphics.Canvas.native.h`,
  and Windows SDK `windows.graphics.capture.h` headers.
