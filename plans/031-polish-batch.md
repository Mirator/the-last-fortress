# Plan 031: small polish batch

Status: **DONE** (2026-10-28). Owner look pending. Priority P2. Effort S. These are presentation fixes within `specs/002-game-rules.md` v1.14 section 13; no rule text changes. Baseline commit: `f0f6d79`. Source: the 2026-10-28 capture reviews.

## Changes

All paths are under `game/The Last Fortress/Assets/TheLastFortress/Runtime/Unity/`.

- **Hero panel hint** (`HudView.Hero.cs`).
  - 18 pt at 75% opacity becomes 20 pt at 90%. The old setting was about 12 px tall at 1280×720.
  - The panel is now 380 wide (was 340) and the label 350 (was 310), so "Tab strategy · Enter night · 1-8 build" stays on one line.
  - The low-left clamp for entry tags moves from 400 to 420 to clear the wider panel (`HudView.Mvp.cs`).
- **Route lines** (`RouteOverlay.cs`).
  - The primary and per-entry route lines get a dark, 0.06 wider under-stroke, drawn just before them (render queue −1). This lets the cyan read over Crossing's pale water and the sand.
  - The points are copied after every route refresh (`SyncUnderStrokes`).
  - The faint committed route, the range rings and the selection outlines are unchanged.
- **Stress label** (`HudView.cs`). "100 Grunts active" now reads "100 enemies active"; the fixture mixes Grunts, Runners, Brutes and Golems.

Not changed: with the hero's back to a Tower, the Keep or a House, the hero is still hidden. That needs letting those buildings through the camera, which reverses spec 011's D2 and is the owner's call.

## Verification (fresh, 2026-10-28)

| Check | Result |
|---|---|
| Release build (`release-build.log`) | Succeeded, 0 errors |
| Full PlayMode (`after/PlayMode.xml`) | 24 passed, 1 skipped (same skip as before) |
| Captures at 1280×720, muted: release, 013, fullrun (`captures/`) | Route lines are outlined over water on Crossing (Day 1 and the whole board). At 720p the Day 12 briefing is full, then collapsed, and the North tag stays clear. The hero hint is on one line in Day and Night hero views. |

EditMode was not re-run, because nothing in Core changed. No test was added: these are layout and colour changes with no rule behind them.
