# XivHud.Inventory

Development module for the Umbra-centered UI replacement. `/xivinventory` opens the
illustrated satchel; `close` hides it and `preview` uses labelled sample items.
`settings` and the Dalamud configuration button open the same satchel; there are no
persisted preferences yet. `/xivnav` lists it under Character and items.

Search four carried bags by item name or category, filter HQ stacks, select a stack
for category, item level, location and aggregate NQ + HQ quantity, and copy its
name. Details use local game icons. Reads refresh once per second while open on
the framework thread. Item snapshots never retain game pointers. Unloaded bags
are shown as incomplete. Preview is separate from live data.

This slice is a read-only bag browser. It has no moving, sorting, equipping,
using, discarding, selling or market transactions. Armoury, saddlebag, crystals,
retainers and currencies are not scanned. Native inventory remains available.
No history or item data is persisted. Logout clears the session; transitions and
NPC dialogue hide the live window and invalidate bag counts.

`XivHud.Inventory.v1.Call` implements cached `status`, version discovery and queued
`window.open`, `window.close`, `window.settings`. Status exposes readiness and bag
availability, not item contents. UI opens do not execute game actions.

Built against the reviewed Dalamud API 15 references. Native inventory reads
accept only the two assembly identities already reviewed by this family; unknown
bindings return no bag data. Offline checks cover search, HQ identity and counts.
Live bag accuracy, local icons, rendering, reload and controller acceptance remain
pending. This is a development ZIP, excluded from the normal testing catalogue.
