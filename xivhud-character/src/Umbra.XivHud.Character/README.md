# XivHud · Character for Umbra

`Umbra.XivHud.Character.dll` adds a **Character** record button to Umbra. It opens the separate **XivHud · Character** Dalamud plugin through IPC.

- Left-click opens or closes the Adventurer's Record.
- Right-click opens Character settings when that optional action is available.
- Ctrl+Shift+right-click keeps Umbra's widget settings shortcut.
- The label, icon (an ID card by default), size, colors and display mode use Umbra's normal customization. Additional options control dimming while unavailable and right-click behavior.
- If Character is unloaded or getting ready, the button shows an explanatory tooltip and makes no toggle call. It reconnects automatically.

The adapter has no direct reference to the Character plugin and installs no keyboard or controller hooks.

## Build

Use .NET 10 and reference the actual installed Umbra and Dalamud assemblies. The default Umbra directory is version `3.1.18.0`; override it when the installation differs. Reference paths need a trailing slash.

```sh
dotnet build src/Umbra.XivHud.Character/Umbra.XivHud.Character.csproj \
  -c Release \
  -p:DalamudLibPath=/path/to/Dalamud/dev/ \
  -p:UmbraLibPath=/path/to/installedPlugins/Umbra/3.1.18.0/ \
  --artifacts-path /path/to/isolated-build
```

The output is `bin/Umbra.XivHud.Character/release/Umbra.XivHud.Character.dll` under the artifacts directory. Umbra supplies its own dependency assemblies; deploy the adapter DLL, with its PDB optionally alongside it.

## Install in Umbra

1. Keep the built DLL in a stable directory visible to the game. For Wine, select its Windows path in the file dialog, such as `Z:\path\to\Umbra.XivHud.Character.dll`.
2. Open Umbra settings → Plugins. If custom plugins are not enabled, use Umbra's own enable control.
3. Choose **Install from file** and select the adapter DLL.
4. Apply the restart requested by Umbra, then add the **XivHud · Character** widget from its widget browser. Search for “Character.”
5. Load the separate `XivHud.Character.dll` Dalamud plugin for the button's actions to become available.

This adapter is an Umbra custom plugin, so do not register it as a standalone Dalamud developer plugin. Umbra loads custom assemblies during its framework compilation. Adding an entry does not immediately load it. Its local-file watcher also restarts the **whole Umbra framework** after a watched DLL changes; updating an existing adapter therefore needs the same coordinated restart as the other Umbra widgets.

No live configuration edits, Umbra restart, or adapter installation were performed while implementing this module.

## IPC contract

| Name | Provider | Adapter behavior |
| --- | --- | --- |
| `XivHud.Character.Toggle` | `Action` | Invoked on left-click only when ready. |
| `XivHud.Character.Ready` | `Func<bool>` | Polled once per second and immediately before an action. |
| `XivHud.Character.OpenSettings` | Optional `Action` | Invoked on ordinary right-click when present and enabled. |

Action gates use `GetIpcSubscriber<object>` and `InvokeAction()`; the generic parameter is not an action argument. Provider absence and call exceptions are handled locally. Event handlers detach when Umbra removes the widget, and the client stops polling on service disposal.

## Verification

The adapter compiles with **zero warnings and errors** against the exact live Dalamud `15.0.3.6` references and the Umbra `3.1.18.0` reference DLLs used for the Journal adapter's build (`~/build/xivjournal-umbra-refs/3.1.18.0/`). Toolbar appearance, clicks and provider reload recovery still require an in-game check.

The install steps follow the same Umbra custom-plugin path documented for the Journal adapter. No release URL, repository listing or author identity is asserted by this adapter.
