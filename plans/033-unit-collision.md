# Plan 033: unit collision

Status: **DONE** (2026-10-28; results in section 4). Owner live check pending. D4: `release-rules-4` changes in place; the starting values were accepted. Priority P1. Effort L (about 3–4 days with tuning). Implements **`specs/archive/014-unit-collision.md` v0.3** (approved 2026-10-28; was `specs/014-...` v0.2 when executed). Current rules: `specs/002-game-rules.md` v1.15. Baseline commit: `022934e`.

## 1. Pre-check (2026-10-28)

Paths are under `game/The Last Fortress/Assets/TheLastFortress/Runtime/`.

- **Enemy movement** is cell-based: `Enemy.Cell`/`NextCell`, `ProgressUnits` (1200 per tile), and `RunSimulation.MoveEnemies` → `AdvanceSegment` / `DecideAtCenter`. Positions are `double X, Z` set by `SetSegmentPosition` on the centreline. Pursuit already uses integer units (`OffUnitsX/Z`, `PursueStep`).
- **The drawn spread** is presentation only: `WorldView.CreateEnemyView` gives each enemy a fixed offset of up to ±0.12 tile by ID. It must move into Core (rule 2), and the view then draws the simulation position.
- **Hero collision** goes through the `HeroMotion.Overlaps` delegate (`HeroOverlapsBlocked`, buildings and terrain). Enemy bodies are added as a second check. The push-out runs after `MoveEnemies`.
- **Attacks on buildings:** `DecideAtCenter` → the attack cell is a cardinal neighbour of the target. Slots are new per-target state, rebuilt each step from the living attackers (not saved: nights are not checkpointed).
- **Saves:** nights are not checkpointed, so no save field is needed. D4 decides whether `release-rules-4` changes in place.
- **Tests to expect to change:** routing tests (same cells, but positions are now offset), the hero touch test in `HeroRulesTests` (0.1 centre distance → body contact), and replay fingerprints for `release-rules-4` only.

## 2. Stages, each with a gate

0. **Baseline.** Record the pacing probe (`evidence/030/PacingProbe.cs.txt` with `probe-hook.diff`), the stress fixture timing, and the full EditMode and PlayMode results. Captures: the full run, 023 and 013.
1. **Bodies and lanes in Core.**
   - Body radius per kind in the definitions, and integer positions with the lane offset.
   - The view draws the simulation position, and the presentation offset is removed.
   - **Gate:** EditMode passes, apart from tests that pin centreline positions, which are updated with a stated reason. Legacy fingerprints are unchanged.
2. **Spacing and deadlock release** (rules 3–5).
   - **Gate:** new EditMode cases (column spacing, resume, head-on release, blocked spawn), and the full probe completes all 15 runs without hitting the step bound.
3. **Attack slots** (rule 6).
   - **Gate:** EditMode cases (3 per face; a waiting attacker steps in when one dies; a destroyed target frees its slots).
4. **Hero** (rule 7). Enemy bodies in the motor, the push-out, and touch at body contact.
   - **Gate:** an EditMode case (cannot pass, is pushed, touch hit) and the PlayMode hero tests.
5. **Balance check and tuning.**
   - Run the probe and the recorded policy tests against the baseline. Expected: slower throughput, but crowds in Cannon splash.
   - If outcomes move a lot either way, tune only the [P] values of spec 014 (lanes, slots, radii) in at most 3 rounds, each a full probe run. Record a section 16 row.
   - **Gate:** the owner's "not much more difficult".
6. **Performance.** Stress fixture paired runs: the frame budget of section 14 holds, and the body test stays O(n²) or better.
7. **Captures and close.**
   - Columns, Wall crowds, the Keep siege, hero contact, and a boss column, at 1280×720 and 1920×1080.
   - Merge the rules into `002-game-rules.md`, archive the spec, and write the results.

## 3. Risks

- **Balance.** The biggest risk. Queues mean less damage on Walls and longer exposure to Towers (easier), while crowds take more Cannon splash. Stage 5 exists for this and may need owner calls.
- **Deadlocks** at corners, breach cells and ring entries. Covered by rule 4, the step-bounded probe and the full run.
- **Hero feel.** Being shoved in a crowd may feel bad. Checked in play; the push limit is [P].
- **Pursuit.** Chasing enemies now meet bodies, so chases may be shorter. Covered by the plan 022 kite log in the probe.
- **Determinism.** Everything is integer and in ID order. Covered by the replay tests.

## 4. Results (fresh, 2026-10-28)

**Code** (all under `Runtime/`):
- `Core/UnitBodies.cs` (new): the constants.
- `EnemyDefinition.BodyRadius`, set by `ReleaseCampaign.Bodies` for `release-rules-4` only, so legacy profiles have no bodies.
- `Enemy`: lane, lane offset, blocked steps, pass units, blocker.
- `RunSimulation`:
  - `AdvanceSegment`, `EaseLane`, `BodyMayStep`, `QueuedBehindEngaged`;
  - the spawn wait (`EntryTaken`);
  - `HeroOverlapsAnything` and `PushHeroOutOfBodies`;
  - reach extended for wide bodies;
  - the snapshot adds `body=` for bodies only.
- `HeroMotion.InSwing(extra)`, and V4 touch 0.15.
- `WorldView` draws bodies at their simulation position.
- The capture condition is 6 attackers.

Deviations are listed in the archived spec's section 6.

| Check | Result |
|---|---|
| New EditMode tests: `EnemyBodiesKeepTheirDistanceQueueAtTheKeepAndTheNightStillEnds` and `TheHeroCannotWalkThroughAnEnemyAndAWalkingEnemyPushesTheHero` | Passed (`stage4/Collision.xml`) |
| Mutation: spacing off | Failed as intended: "E9 and E10 overlap" |
| Mutation: hero ignores bodies | Failed as intended: "the hero walked into the Grunt at step 8" (renamed Golem since) |
| Mutation: no push | Failed as intended: the hero was not pushed 0.3 within 400 steps |
| Existing tests changed, with reasons | The hero contact and chase tests measure from where the Grunt stands (its lane), not its cell centre; "beyond touching" moves from 0.15 to 0.17 (touch is 0.15 now). The early-call test spaces its spawns 10+ steps apart so the spawn wait does not change the compression being tested (ticks 15, 31, 54; 6 gold, where rounding would give 7). |
| Deadlock found and fixed | kill-zone seed 2, Night 6: 10 bodies waited in a circle forever, because a self-sustaining "queued" flag never cleared. Fixed by following the blocker chain afresh. All policy nights now end. |
| Full EditMode (`stage5/EditMode.xml`; final code `final/EditMode.xml`) | 134/134 with the temporary pacing probe; 133/133 on the final code (probe removed, stress-spawn fix in), recorded policy wins included |
| Full PlayMode (`stage5/PlayMode.xml`; final code `final/PlayMode.xml`) | 24 passed, 1 skipped (same skip as before), both runs |
| Pacing probe (`pacing-after-collision.txt`, compared with `evidence/030/pacing-after-boss15.txt`) | 2 defeats in 15 (was 3; one corridor run now wins). Nights 0-15 s longer (Night 5 67 to 82 s). Keep damage moved both ways. No retuning: "not much more difficult" holds. |
| Performance, stress with the hero, 1440x900 (`perf/`) | Before 3.74 / 3.77 / 3.73 ms median. After the stress-spawn fix: 3.53 / 3.54 ms, max 5.8 ms. Earlier after-runs, made while stress spawns still waited, had one 57 ms frame in one run (others max 10 and 6 ms); not seen after the fix. |
| Release build and captures (`captures/`) | Walls crowds spread across lanes; stress 100 alive. The first capture's DEFEAT was a fixture artefact: it waited for 8 simultaneous attackers, which queues cap at 7. |

**Not verified:**
- How shoving feels in play.
- How crowds look at 1080p.
- Whether 4 s waits at corners feel sluggish.

Owner live check pending.
