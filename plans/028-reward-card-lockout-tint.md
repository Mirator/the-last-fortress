# Plan 028: reward cards stay opaque during the reward lockout

Status: **DONE** (2026-10-28). Priority P2. Effort XS. A presentation fix within `specs/002-game-rules.md` v1.12 section 13; no rule text changes. Baseline commit: `780515e`.

## Bug

The reward cards were half see-through for the first 0.8 s of the Reward screen, the click lockout (`HudView.RewardLocked`). While locked, the card buttons are disabled, and Unity's default disabled tint has alpha 0.5. That multiplied the cards' 0.97 opacity, so the board showed through the text. Every capture falls inside that window.

Plans 026 and 027 first called this an offscreen-capture artefact. That was wrong, and both are corrected. An onscreen capture shows the same translucency. Offscreen and onscreen frames of the release set otherwise differ only in text anti-aliasing and shadow edges.

## Change

`HudView.cs`: the card buttons' `disabledColor` is (0.8, 0.8, 0.8, 1). Locked cards dim but stay opaque.

## Verification

- The release capture's reward frame before and after (`evidence/028/reward-cards-before-after.jpg`): a tree showed through the Last Stand card before; the card is solid after.
- Full PlayMode passed with plan 029's run (24 passed, 1 skipped).
