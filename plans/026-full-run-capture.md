# Plan 026: full-run capture set

Status: **DONE** (2026-10-28). Priority P1 (tooling). Effort S. This is verification tooling only. It changes no rule; it exercises `specs/002-game-rules.md` v1.11 as it stands. Baseline commit: `94ccc1f`.

## Why

Every capture review so far used short fixtures (Night 1–2 setups), so Nights 3–12 had never been seen: a built-up fortress, three- and four-entry nights, the final siege, the outcome screen. The 2026-10-28 review ranked this as the largest gap.

## What

`-capture-set fullrun` (`Runtime/Unity/CaptureSequence.cs`, `RunFullRun`) plays a whole twelve-night `release-rules-4` run on Crossing, seed 2.

- **The script mirrors `ReleasePolicyTests`:** the kill-zone defense and economy lists, the minimum defense per Day, the Keep repair rule, the perk order, and the active hero script. Those tests are not in the player build, so the lists are copied, and the copy must follow when the test lists change.
- **Frames per Day N:**
  - `a-day` (default view) and `b-day-board` (whole board);
  - `c-night-8s` and `d-night-25s`, both in the framed night view;
  - `e-night-hero` on every third night;
  - `f-reward`;
  - then `full-13-outcome-<state>`.
- **Run log:** `<w>x<h>-fullrun-log.txt` (purchases, framing, night recap, rewards, result).

How to run it (muted): pass `-saves` with a settings file whose volumes are 0. For example:

`TheLastFortress.exe -capture <dir> -capture-set fullrun -saves <dir with muted settings.json> -offscreen -screen-width 1440 -screen-height 900 -screen-fullscreen 0`

The run takes about 4 minutes and produces 64 frames.

## First run (2026-10-28, 1440×900): `evidence/026/`

- **Result:** Victory with 12/12 nights cleared.
  - The Keep took **0** damage on all twelve nights (650/650 throughout).
  - 18 buildings were bought, 2 were lost all run, and 541 enemies were defeated (the hero got 85).
- **Night length:** 63–130 s.
- **Framing:** half-height 4.5 on Night 1 (West only), and 7.81 on every two- to four-entry night. It never reached the 10.5 cap.
- **The findings** are in the session report. In short:
  - the run is not in danger at any point;
  - the reward cards are see-through in the frames. This is real, not a capture artefact (corrected 2026-10-28; plan 027 first said otherwise). During the 0.8 s reward lockout the disabled buttons used Unity's default tint, which halves the cards' alpha. Every capture falls inside that window. Fixed in plan 028;
  - the Day 12 briefing stacks two panels over the board;
  - the perks list grows over the north entry tag.

## Limits

- **A script, not a player.** It shows the screens and the late-game state, not feel or fun. Its build order is fixed, so other strategies are not covered.
- **One seed and one map profile.** It would take minutes to add more.
