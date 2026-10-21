# XivHud · Vitals

An illustrated field HUD that belongs with Journal and Character: midnight
leather, vellum ribbons, aged brass and aether-blue readings. Four independent
panels keep the centre clear: your resources below, target above, companions
at the side and a separate focus target.

This is a **manual development plugin**, not an automatically installed release.
It builds against the family's reviewed Dalamud API 15 references. Offline tests
do not establish visual quality, accuracy or performance in the live game.

## Panels

- **Your aether:** HP, a separate shield line and barrier percentage, MP/CP/GP
  appropriate to your discipline, and your current cast.
- **Target and focus:** character name, level/job when available, HP numbers and
  percentage, cast name/time/progress, and an explicit interruptible label.
  Non-character objects have no panel; this does not change your actual target.
- **Companions:** up to eight members of your local party, their managed slot
  order, job, leader/self markers, HP and KO/other-area state. This is not an
  alliance list, enmity list, dispel panel or click-to-target interface.

No native windows or input hooks are installed. No game actions are invoked.
No controller lease is acquired. Vitals does not require the other HUD plugins,
Umbra, Ghostty or KamiToolKit. It never changes native HUD visibility or layouts.
Native hotbars, status effects, job gauges, enmity and other unreplaced controls
remain important; do not hide the whole native HUD just to try these panels.

## Controls

| Command | Behaviour |
| --- | --- |
| `/xivvitals` or `/xivvitals settings` | Appearance and layout |
| `/xivvitals open` | Enable live panels; end sample preview |
| `/xivvitals close` | Disable panels, sample preview and settings |
| `/xivvitals preview` | Toggle labelled local sample data and open settings |
| `/xivvitals unlock` | Allow dragging; open settings |
| `/xivvitals lock` | Pass all panel input through to the game |
| `/xivvitals reset` | Restore panel positions and scale |
| `/xivvitals status` | Log bounded diagnostics, without character names |

The server-info **Vitals** entry opens settings; right-click toggles live panels.
New installations start disabled and locked. Appearance offers independent panel
visibility, scale, night vellum and ornament. Unlock before dragging any part of
a panel; lock again when done. Placeholders keep absent target/focus/party panels
available while editing a valid live reading. Sample preview works without login,
includes an eight-person option and never becomes persisted or live fallback data.

Positions use normalized available travel, not fixed screen pixels. Panels clamp
and fit to the current viewport after resolution, UI-scale or party-size changes.
Their default positions can overlap on small displays; arrange them for your
screen. This first version uses the game's main viewport only. Companion/glasses
transport and Ghostty world/native hosting need separate integration work.

Live snapshots are copied on the framework thread at most 20 times per second
while enabled. Hidden/disabled/logged-out/transition/cutscene states clear live
data. Failed reads discard their snapshot and back off to one attempt per second.
Readings older than 500 ms are hidden; cast interpolation is bounded to 250 ms.
Other-area party readings are unavailable rather than inferred as zero HP. The
party API may still expose delayed readings within the same area; this is not a
network-health indicator. No party-list sorting or alliance-order equivalence is
claimed.

## Build and package

From the family root:

```sh
tools/fetch-dalamud.sh
tools/build.sh --test --locked
tools/package.sh
```

`release/XivHud.Vitals-dev-0.1.0.0.zip` contains the plugin, pure core and shared
protocol library. It is intentionally absent from `pluginmaster-testing.json`.
No JavaScript, downloaded artwork or redistributed game assemblies are required.
Journal, Character and Vitals compile the same `shared/IllustratedStyle.cs` source.

## IPC and acceptance

`XivHud.Vitals.v1.Call` uses the family's [versioned IPC](../docs/IPC.md).
`status` includes enabled/preview/locked, readiness, visible panel count, screen
mode and recovery. It does not expose player or party identities/combat readings.
Only `window.open`, `window.close` and `window.settings` can change UI. Reads do
not activate the HUD or capture input; queued operations execute on the owner
thread. World/native mode and original-window handoffs are unsupported.

Before installer publication, complete [live acceptance](../docs/VITALS-ACCEPTANCE.md).
The labelled preview is an actual plugin drawing mode, not a game screenshot or
evidence that live combat/controller interaction has passed.
