# XivHud.Windows

A standalone Dalamud floating-window host. Ordinary ImGui plugin windows do not
need Ghostty, a shell, an agent connection or a virtual terminal. Content still
belongs to its original plugin; this module owns only capture, placement and
pointer mapping. It creates zero terminal sessions.

Open `/xivwindows`, choose an open source window and select **Float selected**.
Choose **Beside me**, **View**, **Here** or **Ground**. **Screen** restores one
source. `/xivwindows screen` recovers every source. Explicit adoption and release
also accept an exact window name after `/xivwindows adopt ` or `release `.
Closing or unloading this host restores ordinary source rendering. No adoption
is automatic, and no window placements are persisted in this first version.

Capture runs at ImGui RenderPre, after all plugins have drawn. It includes the
selected root, children and owned popup trees, submits one draw-list texture,
and suppresses those original commands only after successful submission. Menus
may expand the capture rectangle. Unsupported draw callbacks and invalid capture
sizes leave the original screen window available. Unavailable or closed source
windows do not keep displaying an old image. Logout releases all sources; zone
changes release Here/Ground placements. The sixteen-source limit bounds GPU
resources. Source sizes above 4096 pixels are refused.

NewFramePre remaps queued mouse positions into the source rectangle, preserving
normal clicks, scroll and text input. Ordinary screen windows take precedence.
Pointer hover alone never changes keyboard/controller focus; explicit focus or
a click does. Source windows that were already mouse-disabled remain passive.
The host does not enable global ImGui controller navigation, move the character,
turn the camera, execute a game action, or start/stop Ghostty.

`XivHud.Windows.v1.Call` exposes cached status, panel/window lists, queued window
manager controls, focus and placement. `XivHud.Windows.v1.Post` accepts exact-name
`adopt NAME` and `release NAME`. Its adoption protocol matches the shared
`AdoptedWindowHost`; `FloatingWindowHost` supplies the independent channels.
`GhosttyWindowHost` retains the original channels and behavior. Journal and
Character route to Floating Windows by default, with an explicit **Use Ghostty
world host** appearance setting. A provider change waits for old ownership to
release. Missing providers leave the normal source window usable.

The optional `Umbra.XivHud.Windows.dll` extension adds a toolbar launcher. Umbra
is not a runtime requirement for the host. The same applies to the Inventory,
Crafting and Vitals adapters added with this version.

## Scope and transport

World placements are camera-facing projected ImGui image quads; they are overlays,
not depth-occluded scene geometry. Ground is a projected plane, not a terrain
conforming surface. Generic native game addons are not adopted by this service;
Journal and Character retain their existing dedicated native-frame provider.
Ghostty's terminal, remote-desktop streaming, physics and controls remain its own
supported presentation path, unchanged by this host.

This service does not export a game plugin window to a mobile device or another
computer. A future authenticated stream transport can consume the same content
boundary; neither local world placement nor Ghostty's existing incoming desktop
streams imply that outgoing capability. Local windows have no network listener.

Offline geometry tests cover both image triangles, invalid input and exact
identity. Capture tests cover popup overflow, ownership and unsuccessful
submission. Provider tests retain Ghostty's coverage and check switching only
after release. Live evidence and remaining checks are recorded in
[world-window acceptance](../docs/WORLD-WINDOWS-ACCEPTANCE.md).
