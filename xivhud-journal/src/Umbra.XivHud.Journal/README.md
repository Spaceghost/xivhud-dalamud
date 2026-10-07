# XivHud · Journal for Umbra

`Umbra.XivHud.Journal.dll` adds a **Journal** book button to Umbra. It opens the separate **XivHud · Journal** Dalamud plugin through IPC.

- Left-click opens or closes Journal.
- Right-click opens Journal settings when that optional action is available.
- Ctrl+Shift+right-click keeps Umbra's widget settings shortcut.
- The label, icon, size, colors and display mode use Umbra's normal customization. Additional options control dimming while unavailable and right-click behavior.
- If Journal is unloaded or getting ready, the button shows an explanatory tooltip and makes no toggle call. It reconnects automatically.

The adapter has no direct reference to the Journal plugin and installs no keyboard or controller hooks.

## Build

Use .NET 10 and reference the actual installed Umbra and Dalamud assemblies. The default Umbra directory is version `3.1.18.0`; override it when the installation differs. Reference paths need a trailing slash.

```sh
dotnet build src/Umbra.XivHud.Journal/Umbra.XivHud.Journal.csproj \
  -c Release \
  -p:DalamudLibPath=/path/to/Dalamud/dev/ \
  -p:UmbraLibPath=/path/to/installedPlugins/Umbra/3.1.18.0/ \
  --artifacts-path /path/to/isolated-build
```

The output is `bin/Umbra.XivHud.Journal/release/Umbra.XivHud.Journal.dll` under the artifacts directory. Umbra supplies its own dependency assemblies; deploy the adapter DLL, with its PDB optionally alongside it.

## Install in Umbra

1. Keep the built DLL in a stable directory visible to the game. For Wine, select its Windows path in the file dialog, such as `Z:\path\to\Umbra.XivHud.Journal.dll`.
2. Open Umbra settings → Plugins. If custom plugins are not enabled, use Umbra's own enable control.
3. Choose **Install from file** and select the adapter DLL.
4. Apply the restart requested by Umbra, then add the **XivHud · Journal** widget from its widget browser. Search for “Journal.”
5. Load the separate `XivHud.Journal.dll` Dalamud plugin for the button's actions to become available.

This adapter is an Umbra custom plugin, so do not register it as a standalone Dalamud developer plugin. Umbra loads custom assemblies during its framework compilation. Adding an entry does not immediately load it. Its local-file watcher also restarts the **whole Umbra framework** after a watched DLL changes; updating an existing adapter therefore needs the same coordinated restart as the other Umbra widgets.

No live configuration edits, Umbra restart, or adapter installation were performed while implementing this module.

## IPC contract

| Name | Provider | Adapter behavior |
| --- | --- | --- |
| `XivHud.Journal.Toggle` | `Action` | Invoked on left-click only when ready. |
| `XivHud.Journal.Ready` | `Func<bool>` | Polled once per second and immediately before an action. |
| `XivHud.Journal.OpenSettings` | Optional `Action` | Invoked on ordinary right-click when present and enabled. |

Action gates use `GetIpcSubscriber<object>` and `InvokeAction()`; the generic parameter is not an action argument. Provider absence and call exceptions are handled locally. Event handlers detach when Umbra removes the widget, and the client stops polling on service disposal.

## Verification

The adapter compiles with **zero warnings and errors** using the current Dalamud references and exact installed Umbra `3.1.18.0` DLLs. The live Umbra DLLs differ from the local source build, so the installed copies were used for this check. Toolbar appearance, clicks and provider reload recovery still require an in-game check.

The install behavior above was checked against Umbra's `PluginRepository`, `PluginManager`, `PluginLoadContext` and local-file watcher implementation. No release URL, repository listing or author identity is asserted by this adapter.
