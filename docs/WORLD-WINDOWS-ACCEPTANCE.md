# Independent floating-window acceptance

The family builds against its reviewed Dalamud references. All 540 tests across
11 offline suites passed on 2026-10-29. The eight optional Umbra adapters build
against installed Umbra 3.1.18.0. Locked builds complete without warnings.

The standalone host allocates draw-list textures, not terminal sessions. Its
implementation is separate from Ghostty. Existing Ghostty provider tests remain
unchanged; added tests cover independent selection and release-before-switch.
The capture traversal is shared with native frames. Additional tests verify
popup overflow while retaining the native provider's stricter rectangle policy.

Live captures and installation results are added below after the runtime check.
No physical-controller, depth-occlusion or cross-device export acceptance is
implied by the offline results. A phone/other-computer viewer is not implemented.
