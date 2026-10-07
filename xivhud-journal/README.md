# XivHud · Journal

The first illustrated interface module in the **xivhud-*** family: a live
adventurer's journal for an Umbra-centered FFXIV UI.

## Names and modules

| Part | Identity |
| --- | --- |
| Project | `xivhud-journal` |
| Dalamud plugin and namespace | `XivHud.Journal` |
| Umbra adapter | `Umbra.XivHud.Journal` |
| IPC prefix | `XivHud.Journal.*` |

Future interface modules should follow `xivhud-<feature>` / `XivHud.<Feature>`.
The existing `XivHud` HUD visibility controller retains its identity and settings.
Journal is independently loadable. Its controller navigation uses the shared
XivHud host; mouse and keyboard remain available without that optional service.

## Use

- Open the game's Journal with your existing binding (normally **J**), click
  **Journal** in Umbra's server-info toolbar, or use `/xivjournal`.
- Search accepted quests by title, genre or current objective; filter by chapter,
  tracked quests or personal bookmarks.
- **Show on map** opens the selected quest's native map action.
- **Quest details** and **Open original journal** retain the full game interface.
- **Appearance** offers parchment/night palettes, ornament detail, size,
  native-Journal adoption, toolbar visibility, controller preferences and
  optional window hosting.
- Close with **Esc**, the close button, the toolbar toggle or `/xivjournal close`.
- `/xivjournal settings`, `/xivjournal native`, and `/xivjournal status` are also
  available. The status command writes a diagnostic to the plugin log.

Journal adoption follows the native UI action, so rebinding Journal in the game
does not require changing this plugin. A narrow typed hook handles the game's
Journal main command; quest links open the requested page. All other native
commands pass through. This module installs no raw keyboard or controller hooks.
Bookmarks are local preferences and do not change quest priority or tracking.

## Controller navigation

Open Journal through the game's normal Journal action. A visible aether-blue
frame and row show focus. PlayStation labels are the default; Appearance also
offers Xbox/generic labels and swapped confirm/back.

| Control | Action |
| --- | --- |
| D-pad up/down or left stick | Select quest |
| D-pad left/right | Change chapter |
| L1 / R1 | All, tracked, bookmarks |
| Right stick | Scroll the reading page |
| Cross / Circle | Show on map / back |
| Square / Triangle | Bookmark / original quest details |
| Options / Create | Appearance / search keyboard |

Appearance has a visible selection and supports up/down, left/right, confirm
and back. Search offers letters, numbers and accents, with controller erase,
space and finish actions. No global Dalamud gamepad navigation mode is enabled.
Focus loss, closing, native handoff and plugin unload release the shared input
lease. Held buttons drain briefly to avoid activating the game on the close press.

## Optional world windows

The reusable `XivHud.Windowing` library connects to Ghostty's existing generic
window adoption. Appearance enables it and selects placement beside you, in
front of the camera, at a world location or on the ground. Return to screen and
retry controls are provided; `/xivjournal screen` is a recovery shortcut and
`/xivjournal world` requests visible camera placement. New installations start
as ordinary screen windows. Ghostty is optional and is not modified or reloaded.

## Native game frames

The separate `XivHud.Windowing.Dalamud` provider gives modules an owned FFXIV
window through KamiToolKit. `/xivjournal frame` selects that frame;
`/xivjournal screen` returns to the ordinary screen window. The original game's
Journal is still `/xivjournal native`.

This is a native title bar and window manager around the illustrated content.
The provider submits only the Journal's own draw lists, children and bounded
popups to one texture. Native content bounds align input coordinates; native
focus and window overlap gate interaction. A failed capture preserves ordinary
content. Native framing and Ghostty adoption are mutually exclusive.

Distribute both shared provider DLLs and the pinned KamiToolKit dependency with
Journal. Each future `xivhud-*` module can use the same host interfaces and keep
its own stable ImGui/native window identities.

The live toolbar entry works through Umbra's existing server-info widget. A
separate customizable book widget is built under `src/Umbra.XivHud.Journal`;
its README explains optional installation. It is not required to use Journal.

## Visual language

Midnight leather, warm layered vellum, aged brass rules, restrained aether-blue
focus indicators, stitched edges and compass engraving. Typography comes from
the game's Jupiter and Axis fonts; quest illustrations use the installed game's
own quest icons. No game artwork is bundled or downloaded. Borders and fibers
are drawn as resolution-independent geometry.

The reading page scrolls independently from the quest list. Both global Dalamud
scale and Journal size are honored; long quest titles expand their rows. Night
parchment keeps a warm foreground and the same readable hierarchy.

## First iteration scope

This custom page covers **accepted normal quests and their current objectives**.
Completed history, levequests, full story descriptions, reward details and
tracking edits remain available through the original Journal. Requests for
unsupported selected quests or completed-history mode are not adopted. Static
future story paragraphs are not guessed from the quest's internal sequence.

Configuration and bookmarks persist across plugin reloads. Native adoption can
be disabled in Appearance; unloading restores normal Journal opening behavior.
A drawing failure releases adoption and requests the original Journal.

## Build

Requires .NET 10 and matching Dalamud API 15 references:

```sh
dotnet build src/XivHud.Journal/XivHud.Journal.csproj -c Release \
  -p:DalamudLibPath=/path/to/dalamud/dev/
dotnet test tests/XivHud.Journal.Tests/XivHud.Journal.Tests.csproj -c Release
```

The optional Umbra adapter uses the actual installed Umbra references. See
`src/Umbra.XivHud.Journal/README.md` for its build and installation instructions.

## Local verification

- Matched main plugin and Umbra adapter builds: zero warnings or errors.
- 45 tests: current-step isolation, terminal objectives, ID/text parsing,
  native command routing, controller edges/repeat/reload, keyboard editing and
  confirm/back labels, native close transitions and popup bounds.
- Live accepted quest and illustration verified with **A Mizzenmast Repast**.
- Opening the native Journal on that quest opened the custom book and hid both
  native Journal windows. This tested the same native UI action, without injecting
  a physical J keystroke.
- Map handoff opened the game's `AreaMap`; the custom book closed first.
- Day/night appearance inspected in-game; night preference saved to config.
- The actual game Journal command opened and closed the book; original Journal
  fallback retained its own normal toggle behavior.
- Live controller broker reached Active in Journal and Appearance. The host's
  capture count stopped after its focused settings window closed. Physical pad
  button navigation has not yet been observed during this development check.
- Native framing displayed the illustrated page and bounded Appearance/search
  popups. Clicking the search keyboard edited the query at the expected position.
  A different plugin's focused window released Journal's controller ownership.
- Native close followed by ordinary screen reopening worked. The corrected
  asynchronous lifecycle unloaded a visible native frame cleanly at
  2026-10-04 02:11:32 UTC: Dalamud reported `Unloaded`, the owned addon was gone,
  and cleanup logged successful window/input release without new errors.
  Enabling Journal again succeeded without restarting FFXIV.
- `/xivjournal world` displayed the book in Ghostty's HUD placement and acquired
  controller focus after placement without a mouse click. `/xivjournal screen`
  returned to the normal window; closing released controller ownership.

Installed locally for design iteration. No public release has been published.
