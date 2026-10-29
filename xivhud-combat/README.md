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

Job-gauge values use Dalamud's public typed gauge properties. Counts/enums are
shown directly; timers retain their native units. This is a generic resource
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
