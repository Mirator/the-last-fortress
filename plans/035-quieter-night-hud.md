# Plan 035: a quieter Night HUD

Status: **DONE** (2026-10-29). Owner look pending. Priority P2. Effort S. Implements `specs/002-game-rules.md` v1.19, section 13 ("Night HUD"); presentation only. Baseline commit: `73d6caf`. Source: the owner saw a cluttered Night 12. A muted capture of Night 12 at 25 s showed about 20 text items. The owner chose all five declutter steps ("Do 1-5", 2026-10-29).

The rules text changed. Before, "shown at all times" included the income forecast; the owner approved hiding it at Night. The rules are bumped to v1.19, and `specs/README.md` follows.

## Changes

All paths are under `game/The Last Fortress/Assets/TheLastFortress/Runtime/Unity/`.

1. **Night layout** (`HudView.cs`, `HudView.Mvp.cs`).
   - At Night the income line is empty in both views. Before, only the hero view hid it.
   - The perk list becomes "Perks: N (listed by Day)".
   - The resources panel shrinks to fit.
   - The top-right panel hides Restart, the seed and Copy seed. Reset Camera, Menu and Help move left, and the panel becomes 376×76.
   - The in-run Menu gains "Restart (same seed, Day 1)". It closes the menu and calls `RequestRestart`, so a progressed run still asks to confirm.
   - The seed label is still set while hidden, because tests read `SeedText`.
2. **Entry tags at Night** (`HudView.Mvp.cs`).
   - A tag shows only for an entry that is really off screen, and only as an edge arrow without a count.
   - A tag shows only while that entry has enemies still to send: spawns at or after `SpawnedCount` with its entry index. No Core change was needed.
   - An entry that is on screen but under the top panels gets no tag, because its pad and torch are visible.
   - Day tags are unchanged ("WEST 10", "NEW EAST").
3. **Boss** (`HudView.cs`, `HudView.Mvp.cs`).
   - In the MVP and release HUD the boss line and bar have no panel of their own: `LayoutTop` places them inside the status panel and grows it by 50.
   - The arrival banner is its title only, 58 high instead of 92. The numbers stay in the Day briefing and the Help.
   - `GolemBannerText` returns the title.
4. **Bottom right** (`HudView.cs`).
   - At Night the PAUSE/RESUME button is hidden, unless the speed row is absent, as in stress mode.
   - The speed row moves down into its place.
   - II now toggles pause.
   - CALL EARLY sits on the same line, left of the row; the build cards are hidden at Night.
   - By Day the layout is unchanged.
5. **Hero hints** (`HudView.Hero.cs`).
   - `HintNights = 3`. From Night 4 on, the hint line is empty and the hero panel shrinks to 74.
   - The prompt "Click to take the mouse back" always shows, because the player needs it.

## Verification (fresh, 2026-10-29)

| Check | Result |
|---|---|
| Release build (`evidence/035/build.log`) | Succeeded, 0 errors |
| Full PlayMode (`evidence/035/PlayMode.xml`) | 25 passed, 1 skipped (same skip as before), on the final code |
| Muted full-run captures at 1280×720 and 1920×1080 (offscreen, scratch saves, not kept in the repository) | Night 12 at 8 s and 25 s: about 10 HUD text items instead of about 20. The boss line sits inside the status panel. No entry tags show while every entry is on screen. In the hero view, NORTH and EAST show as edge arrows without counts. CALL EARLY and the speed row share one line. The perk count is legible at 720p. The arrival banner was not in a captured frame, so its title-only form was not seen. |

EditMode was not run, because nothing in Core changed. No test was added: these are layout and visibility changes. The existing PlayMode HUD checks (Day entry tags, seed text, boss bar, II pause) still pass.
