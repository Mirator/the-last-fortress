# Feature spec 010: the strategic camera frames tonight's entries at Night start

Version 0.3, 2026-10-28. Status: **IMPLEMENTED and merged into `../002-game-rules.md` v1.11 section 13 (2026-10-28); archived.** Not current: plan from `002-game-rules.md`. Approved by the owner on 2026-10-28 (ease out to frame; always reframe). Changed `002-game-rules.md` v1.10, section 13 (Camera). Presentation only. The owner's live check is pending.

## 1. Problem

Spec 009 (D3) moved the default zoom from half-height 5.5 to 4.5 so true-scale figures read. The entries sit on the board edge, about 10 tiles from the Keep, outside that view.

In the 2026-10-28 capture review, 3.5 s into a dense night with 24 enemies alive, none was on screen at the default zoom. Only the off-screen entry tags (`WEST 20`, `EAST 20`) showed where the threat was. The 5.5 view is barely better. The player's first seconds of each night show an empty board.

## 2. Decisions (owner, 2026-10-28)

- **D1: Ease out to frame.** On Start Night, the strategic camera eases to the closest view that shows the Keep and every entry used tonight, capped at the whole board. The alternative of clickable entry tags only (no automatic move) was rejected.
- **D2: Always reframe.** This happens at every Night start, even if the player panned or zoomed during the Day. The alternative of reframing only when the view is untouched was rejected.

## 3. Rules

1. **When.** At every Night start (Start Night button, Enter, or the speed controls that start a night), after the night has started in the simulation. A load or restart never starts a night, so it never reframes.
2. **What it frames.**
   - **Points:** the Keep's centre, the centre of each entry cell in tonight's wave, and a point 1.5 tiles beyond each entry along its road [P]. Enemies walk in out of the mist along that road before they spawn.
   - **Safe area:** every point must project inside the screen's inner area, with a margin of 18% of the height at the top and bottom and 10% of the width at each side [P]. This keeps the points clear of the HUD bars.
   - **Focus:** the centre of the points' projected bounding box, moved onto the ground plane.
3. **Zoom.**
   - **Half-height:** the smallest one that fits the safe area, clamped to between the default 4.5 and the whole board, 10.5. The camera never zooms in closer than the default.
   - **When the cap binds:** at 10.5 the points may sit closer to the edge than the safe area. This happens on the four-entry final siege.
4. **Ease.**
   - **Motion:** focus and half-height move to the framed values over 0.6 s [P], smoothed, on real time. It runs while paused and at any night speed.
   - **Player takes over:** any player camera input during the ease cancels it at the current view. That means a pan key, an edge drag, a right-drag or the wheel.
   - **Explicit views win:** any explicit view cancels it and wins. That means Reset Camera, Z (whole board), H (find the hero) and scripted views.
5. **Hero view at Night start** (setting on): the strategic camera is reframed at once, with no ease, while the hero view shows. Tab returns to the framed view.
6. **No other change.** The default view and Reset Camera stay at the Keep and 4.5. Day, the simulation, saves and settings are unchanged.

## 4. Acceptance

- **Framing:** after Start Night and the ease, the Keep and every one of tonight's entry cells project inside the safe area. The only exception is when the half-height is at the 10.5 cap; then they are inside the screen. Checked on a one-entry night and a two-sided night.
- **Cancellation:** a player pan during the ease leaves the camera where the player put it. Reset Camera during the ease ends at the Keep and 4.5.
- **Captures:** the dense two-sided night and a one-entry night, after Start Night, at 1280×720 and 1920×1080, show the incoming enemies. The owner's look is the acceptance of record.

## 5. As built (2026-10-28)

- **The road point was added during capture review** (rule 2). It is 1.5 tiles beyond each entry along its road. With only the entry cell framed, the column walking in sat under the top-right HUD buttons on a two-sided night.
- **One-entry nights pan rather than zoom.** The Keep and a single entry fit at the default 4.5 once the focus moves to their midpoint (Night 1 of seed 7: focus (10.5, 15.75), half-height 4.5). The dense West+East night opens at half-height 7.81.
