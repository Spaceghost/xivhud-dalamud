Experimental Dalamud API 15 builds. See README for pinned-reference build commands and verification limits. The publication and new IPC have offline validation; no new live-game acceptance was performed.
HUD and Journal appear in the testing installer. Character and Vitals are manual development assets only, pending live acceptance. Shared libraries are bundled.

Vitals adds passive player resources, target/focus HP and casts, and a local-party
panel. `/xivvitals preview` draws labelled sample data without a game login;
`/xivvitals open` enables live data. It never suppresses native HUD categories and
does not capture controller input. Journal, Character and Vitals now compile the
same illustration source. See docs/VITALS-ACCEPTANCE.md before enabling installer
publication; this package has no new live-game evidence.
