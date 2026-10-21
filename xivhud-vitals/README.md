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
Native hotbars, job gauges, enmity and other unreplaced controls
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

## Enchantments and target effects (0.1.0.1 development)

Two optional ribbons extend the existing Vitals HUD. Enable them in Appearance or
with `/xivvitals effects`; `/xivvitals effects off` hides both. They use the same
lock, scale, saved anchors and illustration style. Live Vitals must also be enabled.
They start off on upgrade; labelled sample preview includes both ribbons.

Each ribbon displays up to eight current effects with game icons, names, remaining
time and validated stack counts. Dispellable effects appear first; native slot
order remains stable within that priority. A visible overflow count points to the
full native list. Missing icons leave the text and a geometric fallback intact.
Game textures are borrowed from Dalamud and are never bundled with the plugin.

Target effects default to those applied by you. `/xivvitals effects all` shows all
sources and `effects mine` restores the filter. Duplicate status IDs in different
slots are preserved. Zero, negative and invalid timers show a dash; only explicit
permanent-status metadata shows infinity. Countdown interpolation is bounded to
250 ms and the normal 500 ms freshness limit hides stale snapshots.

Disabled ribbons do not read status lists. Display selection is cached between
new snapshots. No effect is removed, targeted or cleansed by these panels; locked
ribbons do not accept input. MCP module snapshots expose the ribbon preferences
and capacity without exporting effect or actor data. Existing `execute_command`
can invoke the documented `/xivvitals effects` commands; window controls remain
capability checked. Live status accuracy, icons, physical input and performance
acceptance are pending.

## Automatic placement

`/xivvitals arrange` fits all six Vitals panels around a clear centre. It adapts
when the viewport or global UI scale changes and reserves the full eight-member
party height. This makes a change between 720p, 1080p and 4K predictable; smaller
viewports reduce the group scale. Panel visibility remains your choice.

Appearance has the same switch. `/xivvitals manual` restores your saved manual
positions; automatic placement never overwrites them. Turn automatic placement
off to drag individual panels. Reset returns to the original manual defaults.
This arranges Vitals only: Journal, Character, the native HUD and other plugins
retain their own layouts. Geometry tests verify no overlap and a clear centre;
actual in-game readability and performance still need live acceptance.
