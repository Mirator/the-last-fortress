# UI playtest findings

Date: 2026-10-15. Scope: six-night MVP UI only. No game was launched and no fresh UI interaction was performed, per the desktop-use constraint. I inspected the UI skill route (the runtime HUD is code-built uGUI), source, `plans/005-results.md`, and saved PNGs. Findings below distinguish saved visual evidence from source-derived behavior.

The screenshots under `plans/screenshots/005-mvp/` were captured during the Plan 005 visual pass before the later “Owner playtest feedback fixes” listed at the end of `plans/005-results.md`. The two `feedback-*` screenshots are later and show those specific fixes. Do not treat any of these as a fresh 2026-10-15 UI run. `1280x720-mvp-00-menu.png`, `05-reward.png`, `06-menu-continue.png`, `09-outcome-Victory.png`, `1920x1080-mvp-04-rotated-farm-and-tower.png`, and `1920x1080-mvp-05-reward.png` were visually inspected. The feedback screenshot shows the updated day HUD/build cards; it does not cover every menu, reward, selection, pause, or outcome state.

## P1 — Protect unsaved Day work from immediate Restart and Quit game

**Evidence:** The top-right `Restart` button is built with `game.Restart` at `HudView.cs:127-130`; `PrototypeBootstrap.Restart` immediately resets the active simulation and begins/commits a fresh Day 1 run at `PrototypeBootstrap.cs:219-229`. The in-run menu also always builds `Quit game` as `Application.Quit` (`HudView.Mvp.cs:108-112`), while a Day is only saved by `SaveAndQuit` (`PrototypeBootstrap.cs:291-300`) or before Start Night (`:340-349`). Plan 005 already records that top-right Restart is immediate and has no confirmation. The menu status explains Save and Quit at `HudView.Mvp.cs:324-328`, but neither control explains the distinction between resetting/leaving the active run and the last durable checkpoint still stored in its slot.

**Observed vs inferred:** Immediate reset and checkpoint timing are confirmed by source and the saved Plan 005 note. Restart resets the active run even just after a checkpoint; the last durable checkpoint remains stored in its prior slot, so do not describe that saved Day as deleted. Unsaved construction/gold/repairs since the last checkpoint are lost from the active run. No screenshot captures the warning state because there is no warning.

**Required behavior:** Before Restart resets the active run, show what happens: the active run returns to Day 1 on the same seed, while the last durable checkpoint remains in its prior slot; offer Cancel. Before Quit game exits an uncheckpointed Day, explain that the active Day's changes are not saved and offer Cancel. Keep the existing terminal “Retry same seed” flow quick after a run has ended.

**Lean acceptance:** Make a build or repair during Day, then invoke Restart and Quit game separately. Each must explain the effect and leave the active run untouched on Cancel; explicit confirmation performs the requested action. Repeat after a successful Day checkpoint: Restart must still explain that it resets the active run, while making clear the prior checkpoint remains stored; Quit game must not claim the checkpointed Day is unsaved.

## P2 — Keep placement tooltip inside the screen at board edges

**Evidence:** `HudView.cs:179-184` gives the tooltip a fixed 330×74 canvas-unit size and top-left pivot. `:424-439` positions it at the pointer or hovered-cell screen coordinate plus `(28, -12)` without checking viewport bounds. At a pointer near the right edge, the tooltip extends beyond the right edge; near the bottom it can extend below the screen. Existing ghost screenshots show the tooltip near the center/right area only, not at an edge.

**Observed vs inferred:** This is a geometric source-level finding, not a captured runtime failure. The saved `1280x720-mvp-03-farm-ghost-day1.png` shows a readable centered tooltip and therefore does not disprove edge clipping.

**Required behavior:** Clamp or flip the tooltip horizontally and vertically so the full price, footprint, rotation hint, and rejection reason stay visible at every screen edge.

**Lean acceptance:** At 1280×720 and 1920×1080, hover valid and invalid placements at each board corner; verify the complete tooltip stays within the canvas and remains near the ghost.

## P2 — Expose camera controls during play without restoring the removed floating sentence

**Evidence:** The MVP hides the in-game `Hint` object (`HudView.cs:169-176`). The full control list (number keys, mouse placement/selection, cancel, rotate, pan, zoom, pause) is only constructed in the main/in-run menu (`HudView.Mvp.cs:113-115`). Build-card number badges and the selected-building Rotate button provide some local discovery; the saved feedback screenshot confirms those cards exist. Pan/zoom and the Space pause shortcut have no on-screen in-run reminder. The 005 owner feedback explicitly removed the floating controls line because it was misplaced/weird, so this is a clarity tradeoff rather than a missed implementation.

**Observed vs inferred:** Source confirms the menu-only list and hidden hint. I did not run a fresh-player task to measure whether this causes confusion. The old 005 screenshots show the earlier floating sentence; they predate the feedback fix and must not be used as evidence for the current HUD.

**Required behavior:** Keep the playfield uncluttered while making pan, zoom, pause, and cancel discoverable in the live run from a compact help affordance or equivalent existing surface. Do not add a tutorial framework.

**Lean acceptance:** In a fresh-player check, verify players can discover how to pan, zoom, pause, and cancel without needing to reopen the menu or receive coaching. Keep the existing menu control reference accurate.

## Resolved historically — old multi-source income wrapping screenshot

**Evidence:** Plan 005 records “the income line wraps at 1080p with two economy types” (`plans/005-results.md`, Remaining issues), and the saved 1920×1080 Farm-selection and reward screenshots show the old source breakdown wrapping `linked)` onto a third line. Owner feedback subsequently compacted the HUD income to `Income +total per day`, with a separate dawn receipt; the old breakdown is superseded and should not be restored. The current line and receipt occupy the 350×50 field inside the resource panel (`HudView.cs:90-104, 491-499`).

**Status and follow-up:** Resolved by the later compact-income feedback fix. The historical screenshots predate that change and are not evidence of a current issue. During the next rendered UI pass, check only that the compact forecast plus optional dawn receipt stays readable at 1280×720 and 1920×1080; do not require or restore an income-source breakdown.

## P2 — Validate selection details against their live action controls at supported sizes

**Evidence:** Selection text and actions use fixed extents: selection body is 350×70 at `HudView.cs:155-159`, while Tower damage/range, income, Wall reflection, and sale refund are appended at `:403-412`; Repair and Rotate controls occupy additional rows (`:414-420` and `:162-166`). The saved 1920×1080 selection screenshot shows Farm details fit, but only that short case. Tower, linked Farm, reflected Wall, and repaired low-health building produce different content lengths. Text uses wrap plus vertical overflow (`HudView.cs:534-545`), so longer combinations can spill into the Sell/Repair/Rotate buttons rather than clipping cleanly.

**Observed vs inferred:** Potential overlap is a source-level risk based on variable body text and fixed 70-unit height; it was not visible in the Farm screenshot and has not been reproduced in a fresh run. Treat this as a focused check before calling selection layout complete, not a confirmed defect.

**Required behavior:** Keep all selected-building facts and the current action/refund labels visually separate at both supported resolutions, including the longest combined detail case. Reflow the selection panel or reserve enough body height when a new line is present.

**Lean acceptance:** Inspect the longest Tower detail plus Sell refund, linked Farm plus refund, and a damaged building with Repair at 1280×720 and 1920×1080. Confirm text never overlaps action buttons or falls outside the panel; sale refund remains visible before selling.

## Checks already supported by saved evidence

- The updated build cards explain each building in one effect line; `1280x720-feedback-day1-cards-water.png` shows the post-feedback card copy. No extra prompt is indicated for routine placement because the ghost already shows validity/rejection text (`HudView.cs:424-435`).
- The selected Farm shows its exact sale refund before the Sell button in `1920x1080-mvp-04-rotated-farm-and-tower.png`; the follow-up should protect that clarity, not add a redundant sell confirmation by default.
- The Reward screen states “choose one reward,” shows three effect/relevance cards, and says building is closed until the next Day in the saved reward shots. It is still darkened behind the overlay, but no specific control ambiguity is established from the static image.
- Continue shows the saved Day/seed in the button and explains current-night continuation in the menu status (`HudView.Mvp.cs:310-332`). The screenshot is pre-feedback and there is no fresh UI claim here.
- Night pause is represented by the primary button changing between PAUSE and RESUME and by state text (`HudView.cs:326-328, 385-390`). A future visual check should cover actual pause behavior, but no missing feedback is established from the inspected material.
