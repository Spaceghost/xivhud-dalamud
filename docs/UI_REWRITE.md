# Shared mod interface

The rewrite is one interface made from independently loadable modules. The shared
shell owns discovery, search, focus and explicit presentation choices. Each module
owns its content, data and fallback. A missing module does not take the rest of
the interface down.

## Implemented shell slice

`/xivnav` opens **Mod workspace** in XivHud. `/xivhud workspace`, the plugin's main
UI button and the HUD server-info entry's right click open the same surface.
The settings button remains available through `/xivhud` and the navigator.

The catalog includes Journal, Character, Vitals, HUD settings, the public main and
configuration windows of other installed plugins, and Ghostty's terminals,
remote windows, adopted windows and chat panels. It does not enable unloaded
plugins. Search includes host/tab names and ancestor names. Workspace panels
retain their group colours, parent-child order and nameplates.

Keyboard arrows or D-pad move selection, Enter or controller confirm activates,
and Escape or controller back closes. The existing XivHud focus broker supplies
controller input only while this window is focused. It waits for neutral input,
respects confirm/back preference, and releases on close or focus loss. Global
Dalamud gamepad navigation is not enabled. This slice does not reserve F2 across
the game or terminal applications; `/xivnav` is the launch command.

The shell opens or focuses a selected surface, restores hidden panels, opens
module settings and offers supported screen/native/world/HUD/ground placement.
World focus requests turn off the provider's walk-up behavior. Other plugins'
public window controls report a request, not an observed visible state. Rich
module and Ghostty requests expose queued/applied/refused results. Catalog reads
never open windows, enable modules or alter native HUD visibility. Stale HUD
snapshots cannot trigger an action. Unsupported modules keep their own window
controls and can be added through future surface-provider contracts.

## Current implementation order: combat, then chat and notifications

Vitals supplies passive player, target, focus and party information. The next
major replacement is the action layer: hotbar content, cooldown and charge
readouts, job gauges, player/target cast bars, and status-effect details. Work
through one job end to end, including mouse and controller input, before expanding
across jobs. Native action bars and gauges remain available until their
replacements have equivalent functionality and live acceptance.

## Remaining interface layers

| Layer | Shared behavior to add |
| --- | --- |
| Visual system | Common type scale, spacing, colours, frames, empty/loading states and readable focus indicators |
| Layout editor | Move, resize, align and restore surfaces across screen, native-frame and world presentation; per-character layouts and scale |
| Communication | Chat channels, conversations and notifications, with text input taking precedence over navigation |
| Exploration | Map, quest tracking, duty/objective panels and navigation, sharing Journal's activities model |
| Character and items | Inventory, equipment, item tooltips, comparison and gear-set workflows using the same shell and input rules |
| Crafting and gathering | Work lists, recipe/material views and progress surfaces using the same layout and focus behavior |

The navigator is the entry point for these layers. It is not a replacement for
their content or a reason to hide unfinished native features.

## Validation

The catalog, search, provider isolation, ancestry, placement intents and queued
operation handling have offline tests. The family is built against the reviewed
Dalamud references. Controller policy already has neutral-entry, focus-loss and
lease-expiry tests; this new window still needs physical controller and in-game
visual acceptance. The build and tests do not launch FFXIV.

A manual Combat development module now implements the saved-bar deck and generic job-resource readings. This is the combat-first slice; live acceptance and bespoke job visuals remain. The subsequent Chat module implements session conversations, explicit sending, toast/tell cards and a notification inbox. Both remain development modules pending live acceptance.

## Items and crafting slice

Inventory now supplies searchable carried bags, HQ filtering, stack locations,
local item icons and item details. Crafting supplies a local recipe catalogue,
discipline and ingredient search, and a session worklist with aggregated material
requirements and carried-bag comparison. Both are separate plugins discoverable
through the shared navigator and exposed through queued UI-only IPC.

This fills the first inventory and crafting-planning slices. Native item actions,
rich tooltips/comparison, additional storage, recipe-unlock evaluation, crafting
progress, gathering/navigation and persistent per-character worklists remain.
Owned/missing quantities are unknown until all carried bags load; crystals remain
unknown because their container is outside the scan. New modules do not hide any
native UI and have no live acceptance yet.
