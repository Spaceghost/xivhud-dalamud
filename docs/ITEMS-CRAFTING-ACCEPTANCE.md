# Inventory and Crafting development acceptance

These modules implement the first bag-browser and recipe-planning slices of the
UI rewrite. They are development modules, not accepted native UI replacements.

## Offline validation

Use `tools/build.sh --test --locked` with the reviewed references, then
`tools/package.sh`. The Items suite verifies case-insensitive category/name
search, separate HQ stacks, aggregate stock, shared ingredients, craft count
versus recipe yield, unknown partial bags/crystals, collectables, excess stock
and quantity bounds. Protocol tests verify queued window-only capabilities and
rejection of item/crafting actions. Navigator tests verify discovery and supported
screen controls for both modules. The existing family suites remain required.

Packaging must include Items.Core, Items.Dalamud and Protocol in each new ZIP,
exclude host/game assemblies, include both entries in the explicit development
catalogue and exclude them from the normal testing catalogue.

On 2026-10-29, all 527 tests across ten offline suites passed against the
reviewed references. No live game acceptance is implied by that result.

## Live acceptance pending

- Compare each carried bag, empty slot count, HQ/collectable identity, item level
  and quantity with native inventory after moving and consuming items manually.
- Verify loading, zone changes, cutscenes, NPC dialogue, logout, relog and unload
  clear stale reads and release every registered command/event/IPC provider.
- Compare recipe output/yield, discipline, level and every ingredient (including
  crystals) with the native crafting log in the installed game language.
- Add two recipes sharing a material and compare worklist totals; verify missing
  counts after manual inventory changes and unknown counts for unavailable bags
  and crystals. Verify preview edits never change the live plan.
- Inspect local icons, text at supported UI scales, minimum window sizes,
  scrolling, search, focus, keyboard/controller behavior and `/xivnav` opening.
- Verify two simultaneous plugin instances (Inventory and Crafting) and disable
  either independently. Unknown binding identities must leave bag data unavailable.

No FFXIV session was started by the build or tests. Item actions, crafting
execution/progress, storage beyond carried bags, recipe eligibility, controller
broker integration, world hosting and persisted layouts/worklists remain future
work. The current modules do not change native UI visibility.
