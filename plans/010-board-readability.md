# Plan 010 — Board readability: entries, terrain and decoration from one layout

Status: **Stages 1, 2.2 and 3 DONE (2026-10-18); Stages 2.1 and 2.3 optional, TODO.** See [Results](#results). Source: owner's solo playtest on the release build after the continuous-river change (`d5f4ab3`), seed 305509965, Crossing (turned 90°).

## Problem

Two symptoms on the same Day 1 screen:

1. **It is not obvious where enemies come from.** Only a screen-space "WEST 10" label marks the entry, and the West route runs straight to the Keep without meeting a river.
2. **Dark shapes cross the river.** These are the ground patches added in plan 007 (`WorldView.BuildShoreAndGround`). They are hard-edged rotated quads at fixed board positions, so on Crossing they continue on both sides of the river.

## Root causes (measured, not guessed)

### A. Map orientation rotates the terrain but not the entries (gameplay bug)

- `MapProfiles.Generate` applies one of 8 symmetries to the terrain. The entries (`MvpCampaign.West/East/North/South`, used by `ReleaseCampaign` recipes) stay at fixed board cells.
- `MapProfiles.Validate` checks each profile's named rule on the **canonical** layout (`Constraint(Canonical(map))`), not on the board that is played.
- Measured over Crossing seeds 0–399 with the real route planner:

  | Rotation | West route uses a ford | East | North | South |
  |---|---|---|---|---|
  | 0° / 180° (and their mirrors) | 200/200 | 200/200 | 0/200 | 0/200 |
  | 90° / 270° (and their mirrors) | **0/200** | **0/200** | 200/200 | 200/200 |

  Night 1 is always West. On **half of all Crossing runs**, the first attack (and every West/East attack after it) never touches a river. The map's stated purpose ("rivers split the west and east attacks") does not hold, and the Night 1 route is a featureless straight line. Ravine has the same mechanism: in 90°/270° orientations its corridors run north–south while the West/East attacks hit the ridges' sides.
- `ReleasePolicyTests` plays only symmetry 0 (`Generate(policy.Map, seed, 0)`). The balance evidence (kill-zone 6/8, corridor 5/8, economy 4/8) therefore comes from the orientation where terrain and entries line up. Players get other orientations 7 times out of 8.

### B. The entry has no presence in the world (presentation)

- An entry is a plain cell on the reserved ring. Nothing is built there: no gate, no path mark, no direction.
- The only marker is a HUD label (`HudView.RefreshEntryMarkers`), projected 0.6 above the entry cell. It sits exactly on the start of the route line and hides it.
- The route line (`RouteOverlay`) has no direction. It is a uniform cyan line, so it doesn't say which end is the source.

### C. Board decoration is not derived from the map (presentation)

- `BuildShoreAndGround` runs once per scene in `BuildBoard`, before any run's terrain exists. The six `GroundPatch` quads have the same positions on every map, profile and seed.
- Nothing stops decoration from overlapping water, fords, forest, rocks, entries, routes or the Keep. Each presentation layer decides alone and uses its own heights. There is no shared notion of "cells that must stay readable".

**The common cause:** the board players see is assembled from independent pieces. Terrain is per-run and rotated; entries are fixed in board coordinates; decoration is static; labels are in screen space. Validation and balance evidence look at a different board (canonical, symmetry 0) from the one played.

## Plan

### Stage 1 — One layout owns terrain and entries (Core; fixes A)

1. Rotate the entries with the terrain. `ReleaseCampaign.Create(seed, profile)` maps each recipe entry through `MapProfiles.SymmetryFor(seed)` **by side** (West → whichever side West lands on), using that side's standard midpoint cell. Don't use the raw `Apply` result: the 20-cell board has no centre cell, so a rotated (0,10) lands on (9,0), one cell off the standard South entry. Entry names in text and markers still come from the actual cell, so a rotated "West front" is announced by its real side.
2. Express each profile rule on the played board: "every front's route crosses a ford" or "stays in a channel", using the transformed fords and channels. Drop exact canonical coordinates. `Validate` checks the map as shown, with the entries as played.
3. Saves: checkpoints already store terrain and wave spawns, so existing runs continue unchanged. Bump `ReleaseCampaign.RulesVersion` only if the codec needs it to tell old and new campaign generation apart. Don't convert saves.
4. Tests (lean; each catches the actual bug):
   - EditMode: for every profile, all 8 symmetries and a seed corpus, the named rule holds on the played board with the played entries. Today this fails for 6 of 8 symmetries on Crossing and 7 of 8 on Ravine.
   - Rerun `ReleasePolicyTests` across symmetries, not only 0, and re-record the protected seeds with the evidence log. A changed protected seed needs a recorded reason (AGENTS.md: don't weaken to make it pass).
5. Accept that every release seed's campaign changes. A seed shared before this change plays differently after it. Old saves are unaffected.

*Alternative, cheaper but weaker:* restrict Crossing/Ravine to the 4 symmetries that keep their axis (0°, 180° and both mirrors). It's one line plus the same test, but every map then faces the same way relative to Night 1. Use it only if Stage 1 slips.

### Stage 2 — Board decoration built per run from the map (fixes C)

1. Move ground patches out of `BuildBoard` into the per-run terrain build (`SetTerrain`). Compute a **readability mask**: non-land cells and their banks, fords and the cell beyond each, entry cells plus their first 3 route cells, and the Keep with its neighbours. Decoration may be placed only outside the mask.
2. Rebuild patches from Land cells only, with soft edges (vertex-alpha fade or a radial-falloff texture) instead of hard rotated quads. If that isn't worth it before the art pass, **delete the patches**. That's a 10-minute fix and removes the symptom completely.
3. Put every layer height (ground detail, grid, water, banks, props, route lines) in one constants block, so new layers can't fight existing ones.

### Stage 3 — Entries readable in the world (fixes B)

1. A world-space **entry gate** on each active entry cell: two posts and a banner in the entry colour, standing outside the board edge on the beach so no legal cell looks blocked. NEW entries pulse during the Day.
2. **Direction on the route**: chevrons, or dashes flowing slowly from the entry toward the Keep, over the first ~4 cells at least. Respect the paused presentation clock.
3. Move the HUD label off the route: anchor it outward, beyond the gate, so it never covers the route's first cells. Keep the existing edge-clamp and arrow behaviour.
4. Optional, after a playtest: at Day start, flash each active gate briefly.

### Stage 4 — Verify

- EditMode and PlayMode suites. Offscreen captures of Ravine, Crossing and Meadow in **all four rotations** on Day 1, so a human can compare orientations side by side.
- Owner replays the two seeds seen so far (3065211399 and 305509965) and fills the questionnaire's visual-pass rows.

## Order, effort, risk

| Stage | Effort | Risk |
|---|---|---|
| 2.2 delete patches (quick win) | minutes | None |
| 1 layout and entries | ~1 day incl. policy rerun | Changes every release seed's campaign; balance evidence must be refreshed |
| 3 gates and route direction | ~1 day | Clutter at the board edge; check at default zoom |
| 2.1/2.3 masked decoration | ~half a day | Low |

Recommended order: 2.2 now, then 1, then 3, then the rest of 2. Stage 1 matters most: it's the one gameplay bug, and it makes the existing balance evidence representative of what players actually get.

## Out of scope

New map profiles, new art assets, camera auto-rotation, MVP six-night maps (they use `MapGenerator` and fixed entries, and keep their behaviour).

## Results

### Stage 2.2: ground patches deleted (done)

`WorldView.BuildShoreAndGround` no longer creates the six `GroundPatch` quads. Nothing replaced them. Stage 2.1 (masked, soft-edged ground detail) remains optional for the art pass.

### Stage 1: entries turn with the map; rules checked on the played board (done)

- `MapProfiles.EntryFor(s, entry)` returns the standard midpoint of the side that symmetry `s` turns an entry onto. `MapProfiles.Inverse` maps a played cell back to the profile layout.
- `ReleaseCampaign.Create(seed, profile)` turns every recipe entry by `SymmetryFor(seed)`, and `Create(seed, profile, symmetry)` takes it explicitly. `PrototypeBootstrap` passes the generated terrain's symmetry, so a flat map gets unturned entries. The RNG draws are unchanged.
- Profile constraints now take `(map, symmetry)` and check the played board. Ravine checks the turned ridges and keeps the turned West/East fronts in their channels. Crossing checks that the turned West/East fronts cross a turned ford. `MapProfiles.Canonical` was removed (no other callers).
- Saves were not touched. Checkpoints store terrain and spawns, so runs in progress continue as they were. `RulesVersion` was not bumped.
- **Every release seed's campaign changes for new runs**, except symmetry 0. For example, seed 7 Night 1 now comes from North instead of West.

Measured with a Core-only probe over seeds 0–399, with the real route planner:

| | Before | After |
|---|---|---|
| Crossing: West/East fronts cross a ford | 200/400 each (0 in 90°/270° orientations) | **400/400 each** |
| Map fallbacks (Ravine / Crossing / Meadow) | — | 0 / 0 / 0 of 400 |
| Skeleton valid in all 8 symmetries | only checked at 0 | yes, all three profiles |

Tests (lean, extending existing cases):
- `MapProfilesMeetTheirNamedConstraintsOnASeedCorpusAndFitTwoByTwoBuildings` now validates the played board. It also checks the fallback skeleton in all 8 symmetries, and that each seed's Night 1 entry equals `EntryFor(map.Symmetry, West)`. That last check fails if the campaign stops turning with the map, which was the original bug.
- `ReleasePolicyTests`: the protected seeds still play symmetry 0. Those games are identical to before, because `EntryFor(0, e) == e`, so the recorded seeds are unchanged. The same 24 runs are now also played on each seed's own orientation, with the scripts turned about the Keep. That part is logged, not asserted.

Policy evidence ([release-policy-runs.txt](evidence/010/release-policy-runs.txt)):

| Approach (profile) | Symmetry 0, seeds 1–8 | Seeds' own orientations, seeds 1–8 |
|---|---|---|
| kill-zone (Crossing) | 6/8 | 8/8 |
| corridor (Ravine) | 7/8 | 7/8 |
| economy (Meadow) | 8/8 | 7/8 |

Caveat: these symmetry-0 numbers come after the "cheaper buildings" retune (`2f27221`), so they differ from the 008 results table (6/5/4). The ring approach still wins most often. The rotated runs show the change doesn't make other orientations harder for these scripts. They don't measure fun or human difficulty.

### Verification (fresh, 2026-10-18)

- EditMode 105/105. PlayMode 18/18, plus the motion probe skipped as usual.
- Release build succeeded (`Builds/Release/`).
- Offscreen 1080p captures of Day 1, seed 7, on all three profiles: [Crossing](evidence/010/day1-crossing-seed7.png) (the North front crosses a ford), [Ravine](evidence/010/day1-ravine-seed7.png) (the North front runs down its channel), [Meadow](evidence/010/day1-meadow-seed7.png). No ground patches.
- Not done: captures of all four rotations per profile (that needs a new capture set), and the owner replaying seeds 3065211399 and 305509965.

### Stage 3: entries readable in the world (done)

- **Gates** (`RouteOverlay.UpdateGates`): every entry of tonight's wave gets a timber gate with a coral banner. The gate stands on the beach just past the board edge, so it is never on a legal cell. A coral pad marks the entry cell. Gates show by Day and Night in the MVP and release scenes. A new side's banner pulses during the Day.
- **Route direction** (`UpdateChevrons`): white chevrons drift along each drawn Day route line, from its entry toward the Keep, at 0.9 tiles/s with 1.4-tile spacing. They are hidden at Night, like the route lines.
- **Label** (`HudView.RefreshEntryMarkers`): the marker sits beside its gate, on the side away from the screen centre, so it never covers the gate or the route's first cells. The edge arrow now means the gate itself is off-screen or behind the top panels; the label being clamped no longer triggers it.
- Test: `ReleaseSceneTests.GatesStandAtTonightsEntriesAndChevronsFlowTowardTheKeep` checks a turned board (seed 7, Night 1 from North). It asserts one gate per entry, at the entry and off the board; chevrons that point toward the Keep overall, so a reversed direction fails; and gates kept with chevrons gone once Night starts.
- Captures: [Crossing Day 1](evidence/010/stage3-day1-crossing-gate.png), [four entries, Meadow](evidence/010/stage3-four-entries-meadow.png). In the second, the North and East gates are behind the final-siege announcement, so their labels show the `^` arrow. That's the existing layout, unchanged here.

Same session, separate feature: a Night speed control (II / 1× / 2× / 3× above Start Night, hotkey `F`). It's covered by `NightSpeedRunsMoreFixedStepsAndPauseHoldsBothClocks`, and described in the README controls.

Verification after both: EditMode 105/105 (after the speed change; Stage 3 touches no Core code). PlayMode 20/20, plus the motion probe skipped. Release build succeeded. Offscreen 1080p captures. Not done: a live look at the chevron motion, and the owner's replay.
