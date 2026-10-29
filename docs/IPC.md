# HUD family IPC v1

Each plugin independently registers a `Func<string,string>` gate: `XivHud.v1.Call`, `XivHud.Journal.v1.Call`, `XivHud.Character.v1.Call`, `XivHud.Vitals.v1.Call`. Call with JSON `{"method":"status","params":{}}`. Replies are `{"ok":true,"result":...}` or `{"ok":false,"error":"code"}`. No hard assembly dependency on the other modules or MCP is needed. Existing controller and Toggle/OpenSettings/Ready gates remain compatible.

Read `api.version` before operations. It reports version 1, module (`hud`, `journal`, `character`, `vitals`) and its method allowlist. `status` reports version, module, UTC `observedAt` and state. Snapshots publish at most five times per second on the owner thread; IPC readers only read serialized copies. HUD reports enabled vs applied suppression, controller state and recovery. Journal and Character report login/data readiness, native bridge diagnostics, open/focused state, requested vs actual mode, host diagnostics, controller availability and selection. Journal also reports search, chapter, filter, visible quest IDs and bookmark IDs; Character reports page and selected row. These are UI diagnostics, not a replacement for MCP's game-data providers.

| Method | Modules | Parameters |
| --- | --- | --- |
| hud.set_enabled | hud | enabled: boolean |
| window.open / window.close / window.settings / window.original | journal, character | none |
| window.close / window.settings | hud | none |
| window.open / window.close / window.settings | vitals | none |
| window.mode | journal, character | mode: screen, native_frame or world; placement optional only for world: Hud, Companion, World, Ground |
| journal.select | journal | questId: nonzero Quest row ID |
| journal.bookmark | journal | questId, bookmarked: boolean |
| journal.show_map | journal | questId |
| journal.tracker | journal | enabled: boolean; optional locked: boolean, filter: tracked or bookmarks, limit: integer 1–5 |
| character.page | character | page: Equipment, Attributes, ClassesAndJobs or GearSets |

Changes return accepted=true, state=queued and requestId. Query `request.status` with `{"id":"requestId"}` for queued/applied/refused and an error code. Applied means owner-thread processing completed; it does not prove native/world presentation or map marker success. Inspect the status snapshot for actual presentation, including a possibly later observation timestamp. An operation can be refused when quest data is unavailable or the quest is absent. Journal selection resets UI filters, not native quest tracking. Bookmarks are local configuration. Native map handoff uses the existing module code. Character's remote operation only selects a page.

Inputs are limited to 4096 characters and depth 8. Unknown methods/fields and malformed types are refused. Capacity is 32 queued requests; at most 8 are processed per framework update. The most recent 64 completed results remain until unload. Missing results after history expiry/reload do not prove failure: inspect state before retrying. Queue and cache are cleared on disposal; gates unregister. Exceptions become bounded error codes, with owner-side logging.

IPC itself is local and has no authenticated caller identity: the optional caller field is descriptive only. XivMcp's existing Action tier and local approval service gate its seven mutation tools. Other Dalamud plugins calling these local gates directly are responsible for their own user consent.

Recovery is always available locally: `/xivhud off`, `/xivjournal screen`, `/xivcharacter screen`, or open the game's original window. No synthetic key input, global gamepad navigation, movement or automatic gear changes are exposed through this contract.

Vitals identifies itself as `vitals`. Its diagnostics distinguish enabled,
preview, locked, dataReady, visiblePanels and screen mode; they contain no actor
identities or combat values. Vitals supports only screen panels, so `window.mode`
and `window.original` are refused rather than presented as successful fallbacks.
`window.open` enables live panels and stops preview; `window.close` disables
panels, preview and settings; `window.settings` opens appearance without enabling
live panels. Local recovery is `/xivvitals close`. Additive module discovery does
not change the v1 operations or validation of the three existing modules.

Journal 0.1.0.2 adds `journal.tracker` as an advertised capability. It changes only
local display preferences and works before login; live quest data is needed to
draw the panel. Status includes `tracker.enabled`, `locked`, `filter`, `limit`
and recently drawn `visibleQuestIds`. An applied preference does not imply visible
quests. The tracker never modifies native tracking or controller ownership.
Older Journal versions must be detected through the method list before use.

Vitals 0.1.0.1 adds an `effects` diagnostic object: `playerEnabled`, `targetEnabled`,
`targetMineOnly`, and `capacity`. This reports display preferences only; effect
names, status IDs, durations, sources and actor identities are not exported.

Vitals `autoArrange` reports automatic group placement versus saved manual anchors.

## Combat and chat development modules

`XivHud.Combat.v1.Call` provides cached status and queued `window.open`,
`window.close`, `window.settings`. It never exposes hotbar execution.

`XivHud.Chat.v1.Call` provides the same window controls plus `notification.push`:
`{"title":"Build complete","text":"Your local build finished.","kind":"Info"}`.
Title is required (up to 100 characters); text is required (up to 1000 characters);
kind is one of `Info`, `Message`, `Quest`, `Error`. Normal request/result polling
applies. Disabled capture or muted notifications refuse the operation with
`notifications_disabled`. This posts only a local card/inbox item, never an
outbound chat message. Status exposes counts, not chat contents or recipients.
There is no `chat.send` capability. Native game input requires a local explicit
composer action with character and age checks.

## Inventory and Crafting

`XivHud.Inventory.v1.Call` and `XivHud.Crafting.v1.Call` use the same version-one
request/result envelope with module ids `inventory` and `crafting`. Both expose
`api.version`, `status`, `request.status`, `window.open`, `window.close` and
`window.settings`; window operations accept an empty parameter object and queue
for the framework thread. Settings opens the primary screen surface. No world
placement, item mutations or crafting actions are supported. Snapshots report
readiness, login state, binding support, loaded-bag count, preview and window
state; no inventory contents, worklist or player identity is exposed.

## Additive screen-layout control for Camp

Journal, Character, Combat and Chat now advertise `window.layout` within protocol
version 1. Parameters are viewport-relative `x`, `y`, `width`, `height`, all finite
and bounded to the viewport. Current geometry appears as `window.layout` in cached
status once a screen window has drawn. Journal and Character refuse layout writes
while a native/world host is requested; request screen mode explicitly first.
Combat scales its fixed deck and preserves a viewport-relative top-left anchor.
The owning module applies its own size limits and owns all rendering and input.

Older clients ignore the additional status field. Camp checks actual method
capabilities, preserving an older provider's position while using its supported
visibility controls. Layout requests cannot send chat, trigger a hotbar or acquire
a controller lease. Vitals' multiple panels and individual world anchors retain
their existing provider-specific positioning.
