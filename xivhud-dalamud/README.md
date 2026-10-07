# XivHud

A reversible native HUD toggle for an Umbra-centered interface. The mode hides
the native HUD, chat, nameplates, and action bars without rewriting the saved HUD
layout or globally hiding Dalamud UI. It also supplies the shared, focused-window
controller service used by XivHud interface modules.

## Controls

- Click **HUD: native / HUD: Umbra** in Umbra's server-info widget to switch.
- Right-click that entry, or run `/xivhud`, for settings.
- `/xivhud on` hides native HUD categories.
- `/xivhud off` restores them. `/xivhud toggle` switches modes.
- `/xivhud status` prints the current status.
- Disabling or unloading XivHud releases its native visibility operation.
- In the focused settings window, Cross changes the HUD mode, Circle closes,
  and Square swaps confirm/back. The same preference is available by mouse.

The preference persists across plugin reloads. New installations default to off.
Other plugin windows retain their own controls; game menus and UI shortcuts stay
available. The ordinary Dalamud server-info bar also supports the HUD entry.

## Implementation

The framework thread acquires one `UIModule.ToggleUi` suppression for category
mask 30 and balances it once on restoration. Ownership includes both native UI
addresses and ends at logout. The code avoids repeated suppression increments,
saved HUD layout edits, global UI visibility changes, and synthetic input.

World-rendered UI needs a rendering pass even when native HUD categories produce
no native UI draw. The companion Wayfinder and Rug changes provide a validated
backbuffer pass before plugin UI when that occurs. They share a depth epoch for
the actual swapchain presentation, independent of plugin callback order.

## Shared controller ownership

XivHud modules use `XivHud.v1.Controller.Renew`, `Read`, and `Release`, with a
module identity and a fresh focus generation. The owner must draw and remain
focused, renewing within 100 ms. Input is read from the matched native pad update
after its original call; only named controller input fields are filtered.
Global Dalamud gamepad mode and its notifier are never enabled.

Entry waits for neutral buttons/sticks. Losing focus releases analog input and
new buttons immediately; buttons held at close drain for at most 100 ms. Hidden
or stalled UI, logout, cutscenes, transitions, device changes, broker failure,
and unload cannot retain an indefinite lease. Quick taps accumulate between UI
draws, and a broker session ID distinguishes reloads from repeated snapshots.
Foreign global gamepad navigation prevents capture. The hook is pinned to the
verified API assembly identities and fails open when those bindings differ.

This input service is independent of native HUD visibility. With no focused
XivHud window, the game receives the controller normally. Other plugins should
use a unique `XivHud.<Feature>` owner and release before native UI handoff.

## Build

Requires .NET 10 and matched Dalamud API 15 libraries.

```sh
dotnet build src/XivHud.Plugin/XivHud.Plugin.csproj -c Release -p:DalamudLibPath=/path/to/dalamud/
dotnet test tests/XivHud.Core.Tests/XivHud.Core.Tests.csproj -c Release
```

The current work covers HUD visibility and restoration. Further in-world HUD
replacements and spell animation/effect customization are separate work.

## Verification — 2026-10-03

- XivHud, Wayfinder and Rug matched Release builds: zero warnings/errors.
- 15 HUD ownership tests and 12 shared-depth frame tests passed.
- 24 additional controller policy tests passed, covering ownership, neutral
  entry, focus loss, held-button draining, device changes and session resets.
- The controller broker reached Active in the live Journal. Capture stopped
  after closing its focused window; native HUD preferences survived host reload.
  Physical controller button navigation has not yet been observed in this check.
- Live Umbra left-click toggles native HUD; right-click opens settings.
- Unloading while enabled restored chat at the same position and size;
  reloading reapplied the saved preference. Commands also restored visibility.
- Native HUD hidden with global `GameUiHidden=false`; Umbra, imported fox and
  cloth rug visible. Verified with Rug loaded before and after Wayfinder.
- Both renderers used the fallback; shared depth used an advancing DXGI
  presentation count. No renderer warnings in the final observation window.
- Full logout/relogin, combat/cutscenes, and fallback performance benchmarking
  remain untested. The fallback currently creates a scoped temporary RTV when
  the game has unbound every color output.

Local installation is enabled. This change has not been published as a release.
