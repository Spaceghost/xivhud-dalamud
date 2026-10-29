# Independent floating-window acceptance

The family builds against its reviewed Dalamud references. All 560 tests across
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

## Ghostty-style companion motion — 0.1.0.6

Follow me now uses Ghostty's companion spring tuning instead of attaching a panel
to camera-right. Running produces trailing and a backward slot bias; turning while
moving eases the window around the character. Idle facing changes retain the slot.
Orbit adds continuous circling, paused on hover/focus. Reduce motion is persisted.
Companions outside the camera stay hosted rather than flashing a screen fallback.

The 25 Windows tests include eleven new motion cases: trailing and settling,
turning without passing through the character, stable idle heading, ordered side
slots, orbit and reading pause, consistency at 30/60/144 fps, teleport/zone resets,
reduced motion and invalid/hitched frame timing. Full family CI passes 559 tests.

Version 0.1.0.6 was installed and verified loaded. The [recorded motion clip](screenshots/companion-motion.webm)
shows brief forward/back movement and then Orbit; it is a real 31.8-second game
capture, downscaled to 1280×720 without audio. The [orbit screenshot](screenshots/companion-orbit.png)
records the same build. The camera was zoomed out for framing. Ghostty retained
its two existing sessions; the standalone host reported zero terminals. Follow me
was restored after recording, along with Combat, Vitals and the quest tracker.

This change matches the companion motion tuning. Ghostty's curved panels,
world-collision reactions, depth occlusion and other presentation effects have
not been ported to the standalone host.

## Stable hover correction — 0.1.0.7

Current companions hover in an assigned area near the character. They trail
movement and ease around turns, then settle; they never continuously circle.
The Orbit button and time-driven angular motion were removed. Old `orbit` IPC
or slash requests now select the same `pet` hover placement. The older 0.1.0.6
clip above is historical evidence of the superseded option, not current behavior.

The Windows suite now has 26 passing tests, including a two-minute idle simulation
that stays in the same horizontal area and a legacy-request compatibility check.
The other family suites are unchanged (560 tests in total).
