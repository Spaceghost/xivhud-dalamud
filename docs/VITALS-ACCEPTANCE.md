# Vitals development acceptance

Status: implementation and offline validation only; no in-game acceptance yet.
Do not add Vitals to the automatic installer until these observations are recorded.
Keep the ordinary native HUD available during checks.

Offline evidence (2026-10-20): all four plugins build against the reviewed
15.0.3.6 reference hashes. The family's seven suites pass 389 tests, including
43 Vitals resource/cast/freshness/layout/polling/recovery cases. The final Vitals
plugin and test builds report zero warnings/errors. Packaging produces a manual
Vitals ZIP with its core/protocol DLLs and keeps it outside the testing installer.
These results do not validate the live game, actual font/rendering output, physical
input or performance.

## Layout and rendering

- Load only Vitals, then also Journal/Character. No dependency on XivHud, Umbra,
  Ghostty or KamiToolKit. Four panels share Journal's palette and typography.
- Preview while logged out: every visible panel says SAMPLE; four/eight member
  fixtures include KO and other-area readings. No sample is silently used live.
- At 720p, 1080p and 2160p and different Dalamud UI scales, inspect names,
  localised numbers, casting text, party rows, clips and ornaments. Very narrow
  panels may clip long labels; no overlapping neighbouring fields.
- Unlock/drag/lock, restart/reload, resize the window and stream at a different
  resolution. Verify saved positions recover. Reset recovers the defaults.
- With locked panels under the pointer, game clicks, mouse-look and controller
  movement continue normally. No plugin window takes focus when a target appears.
- Toggle each panel, palettes, ornament and size. Inspect a full party at 720p;
  arrange defaults if panels overlap. No unexplained per-frame allocations/CPU/GPU
  regression during a representative session. Record measurements before making
  performance claims.

## Live readings

- Match HP, shields, MP, crafting CP and gathering GP to native readings.
- Change/clear target and focus, including the same actor, players, enemies,
  non-combat NPCs and non-character objects. Old names/casts must disappear.
- Compare player and enemy cast timings, cancellation, completion and interrupt
  marker with native cast bars. Generic labels for non-Action action types must
  not show a wrong Action-sheet name.
- Join/leave/change party; check leader/self markers, a full party and an alliance.
  Only eight local-party slots appear, with documented managed ordering.
- KO, revive and members in another area show honest state. Unknown HP never
  appears as known death. Compare delayed/out-of-range readings to native UI;
  document the limits of the managed party API.
- Logout, zone, watch a cutscene, hide game UI, unload and reload. No stale combat
  data, panels or input ownership remain. Read failures and stalled updates hide
  stale readings; sample preview remains clearly explicit if requested.

## Recovery and publication

- `/xivvitals close` hides Vitals without affecting Journal/Character or native
  layouts. `/xivhud off` independently restores any XivHud native suppression.
- Probe `api.version` and `status`, queue open/settings/close and poll results.
  Unavailable module and unsupported world/native operations remain explicit.
- Record game/Dalamud build, plugin checksum, observations and any remaining
  limitations. Keep the package manual-only until this evidence is reviewed.

## Status ribbons (0.1.0.1)

Host validation covers unknown/permanent timers, bounded interpolation, stack
metadata, stable priority, ownership filtering, duplicate effects and overflow.
The existing viewport tests now include both ribbon kinds. No live acceptance.

Verify icons, localized names, timers, stacks and dispellable markings against the
native status list. Change target and status sources, including same-ID effects
from multiple sources. Check the mine/all filter and more-than-eight overflow.
Check empty versus unavailable data and finite versus permanent/unknown timers.
Confirm disabled ribbons skip status reads; no missing icon prevents textual HUD
rendering. Exercise locked input pass-through, drag, resolution changes, logout,
zoning and cutscenes. Measure representative frame cost before performance claims.
