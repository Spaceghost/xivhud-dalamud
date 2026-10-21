# XivHud family

Four independently loadable Dalamud API 15 plugins, with shared illustration, controller and window hosting code. Source is collected here so a checkout builds the family without sibling repositories.

| Part | Purpose | Publication |
| --- | --- | --- |
| XivHud | Reversible native HUD visibility and focused controller broker | Experimental testing package |
| XivHud.Journal | Illustrated accepted-quest journal, local bookmarks, native map handoff, compact quest tracker | Experimental testing package |
| XivHud.Character | Illustrated equipment, attributes, jobs and gear-set record | Manual development package; live acceptance pending |
| XivHud.Vitals | Passive player, target, focus-target and party HUD panels | Manual development package; live acceptance pending |
| XivHud.Windowing / Windowing.Dalamud | Screen layout, optional native frames and Ghostty hosts | Bundled libraries, no installer entry |
| XivHud.Protocol | Versioned cached status and queued UI operations | Bundled library |

Journal and Character work with mouse and keyboard without XivHud. The optional XivHud broker handles only focused local windows. KamiToolKit supplies native frames; Ghostty supplies optional world hosting. Each plugin has its own settings and lifecycle.

Vitals is the first persistent field HUD module. It works independently of XivHud,
Journal, Character, Umbra, Ghostty and KamiToolKit. Locked panels pass clicks
through and never acquire controller input. It starts disabled; `/xivvitals preview`
shows labelled sample data, including while logged out, and `/xivvitals open`
enables live readings. Native hotbars and job gauges remain necessary. Vitals does
not suppress the native HUD. See [Vitals](xivhud-vitals/README.md).

Add `https://spacegho.st/mods/ffxiv/plugins.json` as an experimental repository and enable testing builds for HUD and Journal. Do not install Character from the automatic listing: its release asset is explicitly marked `dev`, pending first live acceptance. See the [family site](https://spacegho.st/mods/ffxiv/xivhud/).

## Build and package

Requires Python 3, .NET SDK 10.0.401, and the reviewed Dalamud 15.0.3.6 reference assemblies. Reference hashes are checked before compiling native consumers. Build tools refuse different bindings rather than silently targeting a later game version.

```sh
tools/fetch-dalamud.sh
tools/build.sh --test --locked
tools/package.sh
```

For local references: `tools/build.sh --references /path/to/reviewed/dalamud --test --locked`. `--offline-feed /path/to/nuget/cache` is available for offline builds. Dependencies have committed NuGet lock files. Release ZIPs use sorted entries and the source commit timestamp; rerunning packaging for the same outputs produces identical checksums. `release/build-info.json` records the source and reference hashes. Game-provided DLLs are excluded. Four ZIPs are built, but only HUD and Journal appear in `pluginmaster-testing.json`.

CI runs builds and all seven offline suites against the reviewed references and uploads packages. A separate manually dispatched release workflow publishes the `testing` channel from `master` after the same checks. No build starts the game or uploads player data. Character and Vitals remain development assets outside the normal automatic listing. An explicit development repository can install them through MCP without manual setup; live acceptance is still pending.

## Controls and recovery

- `/xivhud on` hides native HUD categories; `/xivhud off` restores them. `/xivhud` opens settings. Defaults off and preserves saved native layouts.
- `/xivjournal` opens Journal; `/xivjournal native` opens the original; `/xivjournal screen` recovers the normal screen window.
- `/xivcharacter` opens Character; `/xivcharacter native` opens the original; `/xivcharacter screen` recovers its screen window.
- `/xivvitals settings` opens Vitals appearance; `preview` shows sample panels; `open` enables live panels; `close` hides them; `lock` enables click-through.
- Journal Appearance configures the compact tracker; `/xivjournal tracker` enables it and `tracker off` hides it.
- Journal and Character can choose screen, native frame, or optional Ghostty world hosting. Requested mode and actual presentation are separate diagnostics.

Full module documentation: [HUD](xivhud-dalamud/README.md), [Journal](xivhud-journal/README.md), [Character](xivhud-character/README.md), [Vitals](xivhud-vitals/README.md), [Windowing](xivhud-windowing/README.md). Their earlier verification records describe those checks, not acceptance of this publication build.

## MCP integration

[XivMcp](https://github.com/Spaceghost/xivmcp-dalamud) discovers the modules independently, reads immutable snapshots and capabilities, and exposes explicitly approved UI controls. Reads never open a window or acquire controller input. UI requests are queued for the framework thread; poll their result and then inspect actual window state. The remote Character page control never equips gear. See [IPC contract](docs/IPC.md).

The publication builds and IPC contract have offline validation. The FFXIV container was stopped during publication; this build has no new in-game smoke test or screenshots. Character remains manual-only until native adoption, data accuracy, controller navigation, and all host/fallback modes are checked live.

## License

MIT. [Third-party notices](THIRD-PARTY-NOTICES.md) include the bundled KamiToolKit license. Game artwork is loaded from the local game installation.

## Automatic development setup

The explicit development catalogue contains Character, Vitals and the Journal
tracker preview:
`https://github.com/Spaceghost/xivhud-dalamud/releases/download/development/pluginmaster-development.json`.
With a current XivMcp server, call `setup_plugins` with `plugins` set to
`["XivHud.Character", "XivHud.Vitals"]`, this `repositoryUrl`, and `channel` set
to `testing`; poll `get_plugin_setup_status` with the returned id. MCP registers
the repository, configures testing, installs the bundled libraries, enables the
plugins and verifies loaded versions. Source builds and game acceptance remain
separate concerns. The normal testing catalogue still excludes development modules.
