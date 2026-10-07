# XivHud · Character

The second illustrated interface module in the **xivhud-*** family: a live
adventurer's record that replaces the game's Character window in an
Umbra-centered FFXIV UI.

## Names and modules

| Part | Identity |
| --- | --- |
| Project | `xivhud-character` |
| Dalamud plugin and namespace | `XivHud.Character` |
| Umbra adapter | `Umbra.XivHud.Character` |
| IPC prefix | `XivHud.Character.*` |
| ImGui window / native frame | `Adventurer's Record###XivHud.Character` / `XivHudCharacter` |

Character is independently loadable and follows the conventions set by
`xivhud-journal`. Its controller navigation uses the shared XivHud host; mouse
and keyboard remain available without that optional service.

## Use

- Open the game's Character window with your existing binding (normally **C**),
  click **Character** in Umbra's server-info toolbar, or use `/xivcharacter`.
- The left column is your identity card: job seal, name and title, level (and
  level sync), world, HP with MP, CP or GP, the average item level, and your
  grand company and rank, free company, city-state, nameday, guardian and origin.
- Four pages on the parchment:
  - **Equipment**: thirteen slots grouped as arms & armour and accessories, each with icon, item
    level, HQ mark and materia sockets. The selected piece shows its figures,
    bonuses (with the HQ bonus only where the game applies one), materia,
    condition, spiritbond or collectability, glamour and dyes.
  - **Attributes**: the sections for your current discipline in two columns;
    your primary attribute and your role's attribute (Tenacity for tanks, Piety
    for healers) are emphasized.
  - **Classes & Jobs**: every discipline by role with level and experience; your
    current one is highlighted, levels at the cap read MAX, and disciplines not
    yet begun are muted. A job whose quest is not complete appears as its base
    class, as in the game.
  - **Gear Sets**: saved sets with job, number, name and saved item level. Equip
    a set with its button, a double-click or Confirm. A refusal by the game (for
    example in combat) is explained instead of failing silently.
- **Armoury Chest** and **Open original character window** hand off to the
  game's own windows and close the record. The last page is remembered.
- **Appearance** offers parchment/night palettes, ornament detail, size,
  native-window adoption, toolbar visibility, controller preferences and optional
  window hosting.
- Close with **Esc**, the close button, the toolbar toggle or `/xivcharacter close`.
- `/xivcharacter settings`, `native`, `armoury`, `screen`, `frame`, `world` and
  `status` are also available. The status command writes a diagnostic to the
  plugin log.

Adoption follows the native UI action, so rebinding Character in the game does
not require changing this plugin. A narrow typed hook on the game's main-command
function handles only Character (command 2); every other command, including the
Journal module's command 4 and this module's own Armoury Chest request (25),
passes through untouched. Opens of the `Character` addon that do not go through
that command are adopted through the addon lifecycle after the addon's identity
is revalidated against the live `AgentStatus`. `/character` and `/gear` remain
the game's own commands; this module does not register them. It installs no raw
keyboard or controller hooks and never hooks an agent's virtual table.

## Controller navigation

Open Character through the game's normal Character action. A visible
aether-blue frame and row show focus. PlayStation labels are the default;
Appearance also offers Xbox/generic labels and swapped confirm/back.

| Control | Action |
| --- | --- |
| D-pad up/down or left stick | Select within the page |
| D-pad left/right | Equipment: other group · other pages: six rows |
| L1 / R1 | Equipment, Attributes, Classes & Jobs, Gear Sets |
| Right stick | Scroll the page (Equipment: the piece's details) |
| Cross | Equip the selected gear set · Equipment: Armoury Chest |
| Circle | Close |
| Square / Create | Armoury Chest |
| Triangle | Original character window |
| Options | Appearance |

Appearance has a visible selection and supports up/down, left/right, confirm
and back. No global Dalamud gamepad navigation mode is enabled. Focus loss,
closing, native handoff and plugin unload release the shared input lease.

## Optional world windows and native frames

The same `XivHud.Windowing` providers as Journal. Appearance selects an ordinary
screen window, a native game frame (KamiToolKit, `/xivcharacter frame`) or a
Ghostty world window (`/xivcharacter world`) beside you, in front of the camera,
at a world location or on the ground. `/xivcharacter screen` is the recovery
shortcut. New installations start as ordinary screen windows. Native framing and
Ghostty adoption are mutually exclusive; Ghostty is optional and not modified.

Distribute both shared provider DLLs and the pinned KamiToolKit dependency with
the plugin. The optional toolbar widget is built under
`src/Umbra.XivHud.Character`; its README explains installation.

## Visual language

Journal's midnight leather, warm vellum, aged brass rules, stitched edges,
compass engraving and restrained aether-blue focus. The identity card centers a
brass-ringed job seal; the average item level sits in a framed seal of its own.
Typography comes from the game's Jupiter and Axis fonts. Job, item and materia
art use the installed game's own icons (job icons fall back to the soul crystal,
then to a drawn abbreviation; a missing icon never throws). No game artwork is
bundled or downloaded. Night parchment keeps the same hierarchy.

## First iteration scope

This record covers **what the Character window shows about you, read live**:
identity, equipped gear, attributes, classes and jobs, and gear sets. Not
covered yet, all still available in the original window or the game's own UI:

- **3D character preview.** It needs the native window's CharaView. It is
  technically possible later through `RenderTargetManager.GetCharaViewTexture`,
  but only while the native window (and so its agent) is active; the record hides
  that agent when it adopts an open, which releases the view.
- Glamour and dye editing, materia melding, repairs and extraction.
- The PvP profile and the reputation page.
- Editing gear sets (saving, renaming, linking glamour plates or portraits).

Configuration persists across plugin reloads. Native adoption can be disabled in
Appearance; unloading restores normal Character opening behavior. A drawing
failure closes the record, disables adoption for the session and requests the
original window.

## Build

Requires .NET 10, the sibling `xivhud-windowing` checkout, and matching Dalamud
API 15 references:

```sh
dotnet build src/XivHud.Character/XivHud.Character.csproj -c Release \
  -p:DalamudLibPath=/path/to/dalamud/dev/
dotnet test tests/XivHud.Character.Tests/XivHud.Character.Tests.csproj -c Release
```

The optional Umbra adapter uses the actual installed Umbra references. See
`src/Umbra.XivHud.Character/README.md` for its build and installation instructions.

## Local verification

Only offline checks have been made. Nothing has been loaded into the game.

- Plugin and Umbra adapter build with **zero warnings and errors** against the
  exact live Dalamud `15.0.3.6` references and Umbra `3.1.18.0` references.
- 188 unit tests: average item level (two-handed double count, soul crystal and
  waist excluded), HQ bonus gating by `ItemSpecialBonus` kind, nameday and
  ordinals, progress clamps, job sections against the installed ClassJob rows
  (base-class folding, Arcanist/Summoner/Scholar, limited jobs, DoH/DoL, blank
  rows), level caps, attribute sections and emphasis, equipment columns,
  selection movement, the command policy for every gate and command, controller
  input and labels, and popup bounds.
- Sheet facts were checked offline with Lumina against the installed game data:
  MainCommand rows 2 and 25, ClassJob roles, parents, unlock quests and UI
  priorities, BaseParam names and `OrderPriority` (the attribute order),
  ParamGrow at the cap, grand company rank tables, towns, guardians and weapon
  damage columns by class category.

Needs an in-game check before relying on it: that the Character keybind reaches
command 2 (the addon-lifecycle backstop covers other paths), adoption and
explicit fallback behavior, `UIState.CurrentItemLevel` against the native figure,
equipped-container order (especially which ring is which), the spiritbond
scale, `ClassJobExperience` being experience within the level, job icon ids for
base classes and limited jobs, the gear set equip result, whether the native
window shows Physical Damage and Delay for crafting and gathering tools (the sheet
fills both), Beastmaster's level cap (not in any sheet, so it never reads MAX), the
free company name after leaving a company, and controller, native frame and world
window behavior.
