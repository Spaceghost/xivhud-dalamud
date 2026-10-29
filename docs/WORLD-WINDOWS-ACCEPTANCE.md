# Independent floating-window acceptance

The family builds against its reviewed Dalamud references. All 548 tests across
11 offline suites passed on 2026-10-29. The eight optional Umbra adapters build
against installed Umbra 3.1.18.0. Locked builds complete without warnings.

The standalone host allocates draw-list textures, not terminal sessions. Its
implementation is separate from Ghostty. Existing Ghostty provider tests remain
unchanged; added tests cover independent selection and release-before-switch.
The capture traversal is shared with native frames. Additional tests verify
popup overflow while retaining the native provider's stricter rectangle policy.

## Live game checks — 2026-10-29

All nine Dalamud modules loaded in the running game on Dalamud 15.0.3.9.
All eight optional Umbra 3.1.18 toolbar adapters loaded, with their buttons visible.
XivRug and the other installed family plugins were enabled. Floating Windows
0.1.0.3 uses no Ghostty assembly or terminal allocation. Ghostty remained at its
existing two sessions before and after the tests.

- Journal automatically used the independent host in world mode. Its Appearance
  popup rendered inside the captured texture with the Ghostty option unchecked.
- Inventory read 31 occupied slots from 140 carried-bag slots. Clicking a hosted
  stack selected its icon and quantity. A click in the old hidden source region
  did not change selection. Source positions stayed stable during input.
- Inventory followed beside the character as a fully visible projected panel.
  `/xivwindows screen` returned the ordinary source window. Unload/reload also
  returned and reloaded the host without a recorded plugin crash.
- Crafting found Bronze Hoplon, displayed its direct ingredients and added one
  craft to the worklist. The material totals showed unavailable crystal storage
  as unknown. No crafting or inventory mutation was performed.

The stream demand manager stopped the game when the viewer disconnected during
initial checks. Its journal recorded normal disconnect-driven shutdown; those
interruptions were not classified as plugin crashes. The manager was temporarily
stopped for continuous acceptance and restored afterward.

## Recorded screenshots

All images below are unedited game captures at 1920×1080, not mockups or sample
records. They are also published on the UI family site.

| Capture | Observed behavior |
| --- | --- |
| [Journal](screenshots/journal-independent-view.png) | Independent View placement and all eight toolbar launchers |
| [Hosted popup](screenshots/journal-hosted-popup.png) | Owned popup capture and explicit Ghostty opt-in |
| [Inventory selection](screenshots/inventory-hosted-input.png) | Hosted pointer interaction and item details |
| [Beside-character panel](screenshots/inventory-companion.png) | Perspective world overlay with the selection retained |
| [Crafting worklist](screenshots/crafting-worklist.png) | Direct ingredient totals for a selected recipe |

## Remaining acceptance

No physical-controller, depth-occlusion or cross-device export acceptance is
implied by the offline results. A phone/other-computer viewer is not implemented.
