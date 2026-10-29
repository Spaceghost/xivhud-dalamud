# XivHud.Chat

Development module for the shared UI rewrite, implemented after Combat. Dalamud API 15; native chat and toasts remain available.

- `/xivchat open` enables session capture and opens conversations; `close` closes the window while capture continues.
- `/xivchat settings` configures tells, quest updates, errors and optional general toast cards.
- `/xivchat preview` displays labelled sample conversations and notifications with sending disabled.
- `/xivchat clear` clears local history; `off` disables capture and clears history and notifications.

Conversations provide channel filtering, search, unread counts, copying and scrolling. History is bounded to 500 messages in memory. Notification cards deduplicate repeats, prioritize errors, expire after seven seconds and retain a bounded 128-item session inbox. Neither history is written to disk; logout clears both. Combat log events are excluded. Chat captures unhandled incoming events, so filters applied by other plugins are respected.

The composer supports Say, Party, Alliance, Free Company and explicit `Firstname Lastname@World` tells. Every send requires Enter or the Send button. Plain single-line messages only, with a 500-byte UTF-8 limit including the channel prefix. Arbitrary slash commands are rejected. A queued send expires after one second and is rechecked against the current character, enabled state, game transition state and preview state on the framework thread. Submission does not imply delivery. Drafts clear only after successful native submission and only if the draft has not changed.

Rich item/map links remain in native chat; this module currently shows plain text. The composer has no command history or tab completion. Notification capture covers game toast events, incoming tells and explicit local mod notifications; it does not intercept every third-party notification manager.

`XivHud.Chat.v1.Call` exposes queued window controls and `notification.push` with bounded title/text and kind (`Info`, `Message`, `Quest`, `Error`). It exposes no outbound chat operation. Cached status includes counts and window state, never message bodies or recipients. `/xivnav` discovers the module under Communication.

Native chat input is guarded by the two reviewed FFXIVClientStructs assembly identities (15.0.3.6 and installed 15.0.3.9). Unknown bindings disable sending. Offline tests cover history, unread scope, Unicode limits, message validation, stale/changed-character sends, bounded notifications and IPC validation. Builds do not invoke native game code or send test messages. Live rendering, scrolling, actual chat submission, toast integration and unload/reload still require game acceptance.

## Illustrated Umbra family

The UI uses the Journal and Character materials: stitched leather bindings,
brass compass ornament, vellum (light or dark), and Jupiter/Axis game fonts.
Appearance settings control dark vellum and ornament. Existing settings and
session-only chat privacy remain unchanged.

`Umbra.XivHud.Chat` is the optional Umbra toolbar plugin. Its left click toggles
the module; right click opens appearance. Ctrl+Shift+right-click remains Umbra's
widget-settings shortcut. It polls readiness and handles provider reloads without
owning game input. The underlying Dalamud module supplies the UI, game reads and
explicit actions, just as Journal and Character do.

Build: `tools/build.sh --references /path/to/reviewed/dalamud --umbra-references
/path/to/installed/Umbra --test --locked`, then `tools/package.sh`. Add the adapter
DLL in Umbra Settings → Plugins, restart Umbra, and add its widget. Family index:
https://spacegho.st/mods/ffxiv/umbra/.
