# XivHud Combat

A manual development combat deck for the shared UI rewrite. `/xivcombat open`
shows your saved standard or cross hotbars, native key hints, action icons,
action cooldowns and charge counts, player resources, casts, effect icons and
current job-gauge readings. `/xivcombat` opens settings; `preview` displays clearly
labelled sample data with action execution disabled; `close` disables the deck.

The deck copies existing hotbar assignments. It never writes a slot, changes a
binding, hides native controls or starts a rotation. Existing keyboard/controller
bindings stay with the game. Clicking an occupied slot queues one explicit native
hotbar execution. The framework thread rechecks character, job, PvP mode, bar,
slot type, assigned command, enabled state and a 500 ms input lifetime before
submitting it. RPC controls are limited to opening, closing and settings; they
cannot trigger an action.

Live reads are bounded to 10 Hz. Snapshots older than 500 ms are hidden;
interpolation is capped at 250 ms and does not invent another available charge.
Cooldown details are available for ordinary action appearances; other slot kinds
retain their native execution and icon but may not provide a cooldown readout.
Native availability, range, costs and action queueing remain the game's decision.

Job-gauge values use Dalamud's public typed gauge properties. Active counts/enums are shown with readable labels. Raw timers and boolean
readiness fields stay out of the combat deck. This is a generic resource
readout, not a bespoke graphic for every job. Player/target casts and effects use
the existing Vitals data model. Party/focus frames remain available in Vitals.

New installations start disabled. Position, bar selection and scale are saved.
Unlock position and drag the Move grip to arrange it. No controller focus lease
is acquired by the combat deck. Native reads and explicit clicks are unavailable
when the loaded ClientStructs module identity is outside the reviewed 15.0.3.6
and locally inspected 15.0.3.9 bindings.

Offline tests cover action identity, stale/future input, native bounds, preview
isolation, charge calculations and snapshot expiry. The plugin compiles against
both reference sets. Live action correctness, charge timing, job gauges, bar
appearance and mouse/controller ergonomics still require in-game acceptance.

## Illustrated Umbra family

The UI uses the Journal and Character materials: stitched leather bindings,
brass compass ornament, vellum (light or dark), and Jupiter/Axis game fonts.
Appearance settings control dark vellum and ornament. Existing settings and
session-only chat privacy remain unchanged.

`Umbra.XivHud.Combat` is the optional Umbra toolbar plugin. Its left click toggles
the module; right click opens appearance. Ctrl+Shift+right-click remains Umbra's
widget-settings shortcut. It polls readiness and handles provider reloads without
owning game input. The underlying Dalamud module supplies the UI, game reads and
explicit actions, just as Journal and Character do.

Build: `tools/build.sh --references /path/to/reviewed/dalamud --umbra-references
/path/to/installed/Umbra --test --locked`, then `tools/package.sh`. Add the adapter
DLL in Umbra Settings → Plugins, restart Umbra, and add its widget. Family index:
https://spacegho.st/mods/ffxiv/umbra/.
