# Plan 027: late-game UI fixes

Status: **DONE** (2026-10-28). Owner live check pending. Priority P1. Effort S. Implements `specs/002-game-rules.md` v1.12 section 13. The rule is new in v1.12; the owner chose "collapse after 12 s" on 2026-10-28. Baseline commit: `1dc99d3`. These are the findings of the plan 026 full-run capture.

## Changes

All paths are under `game/The Last Fortress/Assets/TheLastFortress/Runtime/Unity/`.

- **Day briefing** (`HudView.Mvp.cs`, `RefreshAnnouncements`).
  - The new-enemy or final-siege panel shows in full for the first 12 s of each Day (`AnnouncementFullSeconds` [P], real time). After that it shows only its first line plus "(click for details)".
  - A click on the panel toggles the full text, so no information is lost.
  - It resets for each Day, run and restart.
  - On Day 12 the two top panels shrink from about 7 lines to about 5.
- **Entry tags** (`RefreshEntryMarkers`). A tag beside the top-left resources panel stays below it as its perks list grows. On Days 11–12 the panel had covered the North tag.
- **Run summary** (`SummaryText`). "Building links active" becomes "Synergies active", the term the rules use.
- **Capture** (`CaptureSequence.cs`). The full run adds `full-12-a2-day-collapsed`, taken 12.5 s into Day 12, and logs `briefing collapsed=`.

## Not changed: reward cards

The full-run frames showed the board through the reward cards, but that is an offscreen-capture artefact. In offscreen captures the HUD is drawn by the world camera. The plan 008 frame, taken without offscreen mode, shows the cards opaque (alpha 0.97), as the code sets them. So no change was made.

The artefact can still mislead later reviews. Rendering the HUD in a second pass in `CaptureOffscreen` would remove it; that hasn't been done.

## Verification (fresh, 2026-10-28)

| Check | Result |
|---|---|
| Full PlayMode (`evidence/027/after/PlayMode.xml`) | 24 passed, 1 skipped (same skip as before) |
| Release build (`evidence/027/release-build.log`) | Succeeded, 0 errors, to `Builds/Release` |
| Full-run capture (`evidence/027/captures/`, `fullrun-log.txt`) | Day 12 full, then collapsed (`briefing collapsed=True`); the North tag sits below the perks panel on Nights 11–12; the summary reads "Synergies active". The run outcome is unchanged (Victory, 541 defeated). |

No new automated test was added. The changes are layout and wording. The timing rule would need a 12 s real-time wait in PlayMode or a test hook, and the full-run capture checks it instead. EditMode was not re-run, because nothing in Core changed.

## Pending

- **Owner live check:**
  - whether 12 s is long enough to read the briefing;
  - whether the clickable collapsed line is discoverable.
- **Not checked:** the briefing at 1280×720, where the panels take a larger share of the screen.
