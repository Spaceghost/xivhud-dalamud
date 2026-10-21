# Journal tracker development acceptance

Implementation and host validation only, 2026-10-20. The current live client update
thread times out; no tracker rendering, input or performance result is claimed.
All four plugins build with zero warnings/errors; the seven family test suites
pass 404 cases, including tracker filtering, objective selection, bounded
limits, anchoring and IPC parameter validation. The published development package preserves the existing book and defaults the
new tracker to disabled. The normal testing release is not replaced by this
preview.

Before broader release, observe:

- Current tracked/bookmarked normal quests, priorities, hidden entries, ready to
  complete, absent objective text and empty lists match the full Journal.
- Long/localized names and objectives remain readable at 720p, 1080p and 2160p,
  including large global UI scale and five entries. Compact text truncation does
  not prevent opening the full page. Overflow count matches visible rows.
- Locked panel passes clicks through; unlocked title drags and quest rows open
  the existing book. No controller ownership is acquired by the tracker.
- Resize, drag to edges, reload and restart preserve reachable placement,
  palette, size, filter, limit, lock and enabled preferences.
- Logout, zoning, cutscene, hidden game UI and stale framework updates leave no
  stale panel. Opening/closing the full book does not toggle the tracker.
- Versioned IPC advertises journal.tracker; configure and poll applied results,
  then inspect actual visibleQuestIds. Older versions refuse that capability.
- Original Journal and native quest tracking remain available and unchanged.
  `/xivjournal tracker off` hides only this panel.
