# XivHud windowing

An optional window host for XivHud modules. The first provider uses Ghostty's existing **generic ImGui window adoption**: an ordinary plugin window can float beside the player, dock to the view, stay in the world, or lie on the ground. It does not start a terminal, depend on Ghostty assemblies, alter Ghostty configuration, or capture controller input.

The module keeps drawing its original window. Ghostty captures the completed draw lists, handles its world panel, and remaps the pointer. Without compatible Ghostty IPC, the original window stays usable. This is world-panel integration; Ghostty's separate internal Kami native terminal frames are not exposed by this library.

## Integration

Reference `src/XivHud.Windowing/XivHud.Windowing.csproj` and distribute the resulting `XivHud.Windowing.dll` with the consuming module. It targets .NET 10 and has no Dalamud or third-party dependencies. `IWindowHost` keeps the module independent of the provider implementation.

```csharp
using XivHud.Windowing;

const string WindowName = "Adventurer's Journal###XivHud.Journal";
var call = pi.GetIpcSubscriber<string, string>(GhosttyWindowHost.CallChannel);
var post = pi.GetIpcSubscriber<string, object>(GhosttyWindowHost.PostChannel);
var host = new GhosttyWindowHost(
    WindowName, "XivHud.Journal", request => call.InvokeFunc(request),
    command => post.InvokeAction(command));

// Every UI frame, after drawing the original ImGui window using WindowName.
// Also call this when closed, disabled, or logged out, so ownership is released.
host.Update(config.WindowHostEnabled, isOpen, clientState.IsLoggedIn,
    Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency,
    WindowPlacement.Companion);

// Dispose the host before the module stops drawing/subscribing.
host.Dispose();
```

Keep the **full Begin name** fixed, including a module-unique `###` identity. A visible title is required. Do not use a separate display title, a native addon name, or another window's ID. Draw child windows and popups normally; do not manually redirect the mouse or turn on ImGui gamepad navigation. Keep normal rendering and open/close controls operational for fallback. Adoption can resize the original window; layouts should use the available content size and avoid forcing its size every frame.

Use an opt-in setting, off by default, and show `host.Status`. A local “Return to screen” action calls `ReturnToScreen()`; “Try again” calls `Retry()`. Returning through Ghostty's panel button is respected until the module closes/reopens or the user retries. Toggling the saved opt-in setting off releases the panel. An explicit user open may call `RequestInputFocus()`: after the exact adopted panel's HUD placement is acknowledged, the host sends one focus request for that panel. Ordinary adoption or a placement change does not request focus. The host requests no character movement, camera turn, terminal action, or shortcut reservation.

| Placement | Existing Ghostty argument |
| --- | --- |
| `Companion` | `pet` |
| `Hud` | `hud` |
| `World` | `here` |
| `Ground` | `ground` |

These map to the provider's existing placement API. A rejected placement is shown in the status; it does not claim to have moved successfully. Placement is requested once per adoption or explicit setting change, so manually moving a hosted panel is preserved.

## Native game frames

The separate `XivHud.Windowing.Dalamud` assembly provides **real native game windows**, independently of Ghostty. It uses the matched KamiToolKit 2.2.48 `NativeAddon` frame and a native image node backed by Dalamud's draw-list texture renderer. The game owns the frame, title, dragging, context scaling, close button, and stacking order.

```csharp
using XivHud.Windowing.Dalamud;

var native = new NativeGameWindowHost(pi, WindowName, "XivHudJournal", "Journal", new(1080, 700));

// Before drawing the known root. Release any world adoption before enabling this provider.
native.Prepare(nativeEnabled, isOpen, clientState.IsLoggedIn);
if (native.CloseRequested) Close();
if (native.ShouldDraw)
{
    // Immediately before Begin, after the module's ordinary position/size requests:
    native.ApplyNextWindowLayout();
    // Draw the root and all children/popups, then End every window.
    // Disable mouse widgets when native.IsActive && !native.AllowInput.
    // A controller lease requires native.IsFocused, independently of mouse hover.
    DrawOwnedWindow();
    native.Capture();
}
```

The content root uses the native content rectangle in screen pixels, so its input coordinates already match the image. The capture traverses only that explicitly owned root, its active children, and its own popup trees. All lists are submitted in **one** `Draw(in ImDrawData)` call. Original draw commands are suppressed only after successful submission and after a first texture has reached the native UI frame. No global ImGui or D3D hook is installed, and other windows' draw lists are untouched.

Native consumers must keep their popups inside `ContentPosition`/`ContentSize`, use a responsive layout, and turn off their separate flat-window drag logic while native hosting is active. Capture refuses unsupported draw callbacks, an input rectangle that does not match the native content, or an out-of-bounds owned popup. Those failures retain the ordinary content and close the native frame; `Retry()` or a new native request can retry. Leaving native mode restores the previous normal root position and size. A native close request applies only to the current native request, so it cannot close a subsequently opened ordinary window.

Initialization is lazy and optional. One owned Kami lifetime is shared by native windows in the same consuming plugin load context. A conflicting toolkit owner is refused. `Dispose()` begins addon closure; `ShutdownTask` includes native-addon destruction and toolkit retirement for the current backend and any earlier retry retirements. **The consuming plugin must await this task before Dalamud disposes its plugin interface.** With the matched API, implement `IAsyncDalamudPlugin`: its `DisposeAsync()` initiates window disposal on the framework thread, then asynchronously awaits `ShutdownTask`. Dalamud awaits that method before service teardown, independently of a cached manifest. A synchronous plugin instead requires `CanUnloadAsync: true` and an off-framework unload that waits before `Dispose()` returns. Never synchronously wait on the game/UI thread: Kami's async close needs subsequent framework ticks. Cleanup serializes its final addon snapshot with framework callbacks, then explicitly dispatches the actual addon-disposal invocation to a worker; `ConfigureAwait(false)` alone does not guarantee that thread change. Faults remain observable through `ShutdownTask`. Missing/failing toolkit support leaves normal window rendering available.

Build with `-p:DalamudLibPath=/path/to/matched/dev` or `DALAMUD_HOME`; Kami is pinned in `packages.lock.json`. Distribute `XivHud.Windowing.Dalamud.dll`, `KamiToolKit.dll`, and the dependency assets beside the consuming plugin, in addition to its world-provider dependency if used. Native layout access is currently guarded against the exact verified FFXIVClientStructs API; a changed API falls back until reviewed.

Twelve native-provider tests pass: nine capture/focus tests use allocated instances of the actual matched ImGui structs, and three shutdown tests cover retained retry retirements, fault propagation, and waiting for every outstanding retirement. Capture checks cover root/child/popup ordering, unrelated-window exclusion, callback refusal, failed submission, hidden children, bounds mismatch, suppression only after success, and foreign ImGui focus overriding stale native focus. These tests do not invoke native game or GPU functions. Journal has shown native rendering, owned popup input, and focus gating in game. On 2026-10-04 at 02:11:32 UTC, a ready, focused native frame (addon 125) unloaded successfully: cleanup completed without warnings/errors, the native addon list was empty, and reenabling the plugin returned `Loaded`. Dragging, scaling, and hide/reopen are not established by these host tests.

## Lifecycle and scope

- Uses `GhosttyDalamud.v1.Call` (`panel.list`, `panel.place`, `window.list`) and `GhosttyDalamud.v1.Post` (`adopt NAME`, `release NAME`). JSON errors and asynchronous placement results are checked.
- Matches only `kind: adopted` with the exact `app` identity Ghostty records for the supplied full window name. It never uses title matching, a default focused window, or unnamed release.
- Closing, disabling, logout, and disposal request release of that exact window, including an adoption that has been queued but has not appeared yet. A temporary provider outage retains pending cleanup while the consumer remains loaded.
- An adoption that is not observed within five seconds is cancelled. It requires an explicit retry or a new open, avoiding an endless recapture loop.
- `IsHosted` means Ghostty lists the matching adopted panel; its IPC does not expose a separate first-image-rendered acknowledgement. Post commands also have no completion acknowledgement. Cleanup on final disposal is best effort if Ghostty is unavailable; Ghostty's own shutdown removes its adoption hooks, and a stopped source window follows Ghostty's existing lost-window lifecycle.
- The consumer must serialize calls on its UI thread. The world-provider assembly has no native pointers, native hooks, background tasks, or module-owned input routing; the separate native-provider assembly has the explicitly awaited lifecycle described above.

## Verification

Host tests exercise the actual serialized command/JSON protocol with delayed frame processing: exact identity, missing providers, placement results, early close, reopen during cleanup, logout, unload, explicit return/retry, malformed replies, and respect for Ghostty's “Back to UI”. Run:

```sh
dotnet test tests/XivHud.Windowing.Tests/XivHud.Windowing.Tests.csproj
```

Journal's local world-provider integration has been observed in the HUD placement: content appeared, its controller polling stopped while unfocused and resumed after explicit focus. On 2026-10-04 at 02:12:57 UTC, `/xivjournal world` opened a visible HUD Journal without a mouse click; status reported `controller=Active` and `window=World window · hud` after the requested focus settled (`/tmp/xivhud-journal-world-settled.png`). `/xivjournal screen` then returned to the normal window, confirmed visually and by status at 02:14:24 UTC. Other placement modes and physical controller actions remain unverified. Native-provider evidence is recorded separately above.

Protocol references: local Ghostty `docs/ADOPT.md`, `docs/IPC.md`, `core/app/panels.nelua`, `core/app/ipc.nelua`, and `lua/ipc.lua`; no Ghostty modifications are required.

## Provider-independent hosting

The adoption state machine is now `AdoptedWindowHost`. `GhosttyWindowHost` remains
its existing channel adapter; `FloatingWindowHost` talks to the independent
`XivHud.Windows.v1.Call`/`.Post` service. `WindowHostRouter` releases ownership
before changing providers. Independent placement requires the standalone Windows
module, with screen fallback when it is unavailable. Native frames remain a
separate provider. See [Floating Windows](../xivhud-windows/README.md) for the
capture, pointer, world-projection and transport boundaries.
