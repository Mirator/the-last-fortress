# Plan 022: limit hero chases to one per enemy per night

Status: **DONE** (executed 2026-10-28; see [022-results.md](022-results.md)). Owner live check pending. Original planning status: TODO after D1-a and D2-a were decided on 2026-10-28. Priority P1. Effort S.

Implements **`specs/archive/008-bounded-chase-limits.md` v0.3** (approved 2026-10-27; D1/D2 on 2026-10-28): option A + C, N = 1, applied in place in `release-rules-4`, with traps still firing during a chase. Current rules: `specs/002-game-rules.md` v1.7, section 18 "Threat: bounded pursuit". Baseline commit: `702bd71`.

The spec's section 5 rules text is the contract, and this plan does not restate it. Read the spec fully before starting.

## 1. Pre-check (done while planning, 2026-10-27)

- **The spec matches the code.** `StartsPursuit`, `PursueStep` and `MoveOff` (`Runtime/Core/RunSimulation.cs:2086–2180`) behave as section 1 of the spec describes.
- **Chases run on V4 only.** `DecideAtCenter` (`:1111`) uses pursuit only when `hero.Profile.PursuitTicks > 0`, which is V4 only. V2 and V3 keep `MeleeHoldsHero`, which stays untouched.
- **An enemy attacking a building decides every step.** It is not `Moving`, so `MoveEnemies` calls `DecideAtCenter(e, true)` each step (`:1073`). The "struck while attacking a building" case therefore needs no new decision point.
- **Hits on the hero already check reach.** `HitHero` (`:2222`) lands a melee hit only within `EnemyHitReach` (0.20), and the hold in spec section 5 uses the same distance.
- **V4's `EngageRange`/`StickyRange` (0.2/0.35) are not read.** They are only used by `MeleeHoldsHero`. Do not reuse them, because 0.35 is exactly the free-hold band the spec forbids.
- **No save change.** An enemy exists within a single night (the night ends when all enemies are defeated, and a checkpoint is taken only at Start Night), so "per night" is the same as "per enemy". Confirm this during Stage 2.
- **Unit tests to extend.** `HeroRulesTests.ATouchingEnemyIsStruckThenChasesTheHeroBrieflyAndReturnsToItsRoute` ends with the Grunt back on the Keep, which is the right starting point for the new assertions. `DeathCancelsAnUnlandedContactAttack` relies on a first proximity chase, which the change keeps.
- **Script for the kite run.** `ReleasePolicyTests.Corridor` builds Spike Traps along row 10 of Ravine.

## 2. Decision D1: attack timing when an enemy switches between a building and the hero (resolved: D1-a)

Spec section 5 says that a struck enemy attacking a building "turns its attacks on the hero while the hero is within 0.20, then resumes the building". It does not say what happens to the enemy's attack timer.

The existing convention starts a fresh full interval on every target change:
- the route-end code at `:1122–1125` sets `AttackDueTick = Tick + AttackTicks` when the target changes;
- chase and hold do the same when they take the hero.

Under that convention a new stall appears:
1. The hero strikes a Brute at a Wall, and the Brute turns to the hero with a fresh 1.5 s timer.
2. Before the timer runs out, the hero steps just beyond 0.20.
3. The Brute goes back to the Wall, again with a fresh 1.5 s timer.
4. The hero steps back in and strikes again (every 0.75 s).

The Brute then never hits the Wall or the hero, while the hero hits it every cycle. That breaks spec goal 2 ("neither trap farming nor stalling can be repeated").

| | Rule | Effect |
|---|---|---|
| **D1-a (recommended)** | The attack timer carries over both ways. If a hit is already due when the enemy switches back to the building, that hit lands at once. | No stall. Each enemy has one attack rhythm whatever it is hitting. Small code. |
| D1-b | Keep the convention: fresh interval on each switch. | Simplest wording, but allows the stall above. |

**Owner decision 2026-10-28: D1-a.** Spec section 5 (v0.3) now says so. Stage 2 below implements it.

## 2b. Decision D2: the hero out-reaches the fight-back (resolved: D2-a)

The hero's swing lands within 0.225 tile (`HeroProfile.InSwing`, `HeroMotion.cs:163`). An enemy's hit on the hero, and with it the spec's hold, works only within 0.20 (`EnemyHitReach`).

Today that 0.025 gap is harmless: a struck enemy chases and closes to 0.15, so the hero has to retreat, and the leash bounds that. Once its one chase is used, the enemy no longer moves toward the hero. A hero standing 0.20–0.225 from a Brute at a Wall could hit it with every swing and never be answered: 17 more swings for free. The same works when trailing a walking enemy.

| | Rule | Effect |
|---|---|---|
| **D2-a (recommended)** | Raise the V4 enemy hit reach from 0.20 to 0.225, equal to the hero's swing reach. One [P] value. The chase's stopping distance follows (0.75 × reach ≈ 0.17). | Any position from which the hero can strike is one where the enemy turns and can hit back. Hits on the hero land up to 0.025 tile further than now, a small rise in enemy hit chances that the policy re-run will show. |
| D2-b | Keep 0.20 and record the band as a known gap. | No balance change; the free-swing band stays, though only a precise player can stand in a 0.025-tile ring. |

**Owner decision 2026-10-28: D2-a.** The enemy hit reach (`EnemyHitReach`) is 0.225 after Stage 2, step 0. Every reference to the hit reach in this plan means that value. Stage 3's "step out" distance is beyond 0.225 but close to it: about 0.25.

## 3. Stages

Run Unity only when no Editor has the project open, with the sandbox disabled. Use `plans/evidence/019/run-tests.sh <out-dir> <name> <EditMode|PlayMode> [filter]` as the template: copy it to `plans/evidence/022/` or call it with `plans/evidence/022/...` as the output directory. It reverts the project-settings files that batch runs rewrite.

### Stage 0: preflight

```bash
git status --short -- "game/The Last Fortress/Assets/TheLastFortress" specs plans/*.md
```

- Record any drift from `702bd71` in the files listed in section 1. If those files have changed, re-check the line references.
- Run the focused baseline as `base/Core`: EditMode `TheLastFortress.Tests.HeroRulesTests`. Record the pass count.

### Stage 1: measure the exploit before the change

Add a kiting hero script to `Tests/EditMode/ReleasePolicyTests.cs`. Keep it to what this measurement needs, and reuse `Play` and `HeroLog`.

- **`HeroScript.Kite`.** Like `Active`, it picks the nearest enemy within `ActiveHeroLeash` of the Keep and strikes it within reach. After each accepted contact it retreats for a fixed 1.0 tile (or at most 40 steps), whatever the enemy does. It retreats in the direction that puts the nearest Spike Trap cell between the enemy and the hero, or straight away from the enemy if there is no trap within 3 tiles. Then it returns and strikes again. The retreat must not depend on the enemy chasing: after the change the enemy no longer chases, and a script waiting for it would never stop retreating, which would make the before/after comparison meaningless. Keep the script stateless, or give it one small state field in a local class. Do not add production hooks.
- **Counts in `HeroLog`.** Add:
  - chases started: steps on which an enemy's `Pursuing` turns true;
  - the largest number of chases by one enemy;
  - trap triggers off the route: `TrapTriggered` events whose enemy is `Pursuing || Returning` after the step;
  - the share of enemy night-steps spent off the route.
- **One new `[Test]`** that only writes `TestResults/022/kite-runs.txt` and asserts nothing about balance. It runs Corridor on `release-rules-4`, seeds 1–8 at their own orientations, with `Kite`, and logs the counts plus wins per seed.
- **Before the rule change,** run it and save the output as `plans/evidence/022/kite-before.txt`.
- **Expected:** more than one chase per enemy and repeated off-route trap triggers.
  - If the script cannot reproduce this, refine it once.
  - If it still cannot, record that and continue. The code analysis already shows the exploit, and Stage 2's unit test proves the limit directly.

### Stage 2: Core change (V4 only)

All paths are relative to `game/The Last Fortress/Assets/TheLastFortress/`.

0. **`Runtime/Core/HeroMotion.cs:143` (D2-a).** Change the V4 `EnemyHitReach` from 0.2 to 0.225, and update the V4 summary at `:136` ("melee enemies hit within 0.225").
   - Only V4 changes; the legacy profiles use the swing reach, through `Legacy`.
   - `HitHero` and the chase's stopping distance (`stopAt = 0.75 × reach`) pick up the new value with no other edit.
   - Leave V4's unused `EngageRange`/`StickyRange` values alone.
1. **`Runtime/Core/Entities.cs`.** Add `public bool Chased;` next to `Pursuing, Returning`, with a doc comment: "release-rules-4: this enemy has used its one chase (it lives one night, so the budget is per night)".
2. **`StartsPursuit` (`RunSimulation.cs:2091`).**
   - If `e.Chased`, return `FightsBackInPlace(e, freshStep)` instead of considering a chase.
   - Otherwise keep the current logic, and set `e.Chased = true` where the chase actually starts (the `freshStep` branch, next to `e.Pursuing = true`).
   - Update the summary to state the budget.
3. **New `bool FightsBackInPlace(Enemy e, bool freshStep)`,** placed next to `StartsPursuit`, following spec section 5:
   - **Holding:** if `e.TargetsHero` and the hero is alive within `EnemyHitReach`, set `CarryUnits = 0` and return true. If not, clear `TargetsHero` and return false, so the normal route or route-end code runs at this same decision.
   - **Who never fights back:** ranged enemies, Bombers and bosses (already excluded upstream; keep the guard), and any enemy whose hero is not alive or is beyond `EnemyHitReach`.
   - **When it starts:** either it is not attacking a building and is not at its route end (`TargetBuildingId < 0 && RouteIndex < Route.ReachableEndIndex`), or it was struck within the provocation window (`Tick < ProvokedUntilTick`). Being struck covers both attacking a building and standing at the route end.
   - **How it starts:** set `CarryUnits = 0`. If `!freshStep`, return true. Otherwise set `TargetsHero = true` and `ProvokedUntilTick = 0`.
   - **Timer (D1-a):**
     - An enemy that was walking takes `AttackDueTick = Tick + AttackTicks`, as today.
     - An enemy that was attacking a building keeps both its `AttackDueTick` and its `TargetBuildingId`, so the building is resumed with the same timer on release.
     - `EnemyAttacks` (`:1479`) already gives `TargetsHero` precedence over the building.
     - If you prefer to keep the two targets mutually exclusive, use one stored field instead (for example `ResumeBuildingId`). Do not reset the timer.
4. **Readers of `TargetBuildingId`.** With D1-a, `TargetBuildingId` may be set while `TargetsHero` is true. Check every reader so that the hero wins wherever both matter, and update the `Entities.cs:142` comment ("then `TargetBuildingId` is -1").
   - `RunSimulation.cs`: 23 occurrences. In particular, `StartsPursuit`'s `near` test is harmless once `Chased` is set, `DecideAtCenter`'s dead-target check, and the route-end reacquire (same ID → timer kept).
   - `Runtime/Unity/WorldView.cs:211, 1267, 1339, 1348`: facing, attack pose and reach. The enemy must face and strike toward the hero while it targets the hero.
5. **Leave unchanged:**
   - `MeleeHoldsHero`, the V2/V3 profiles, and the V4 `EngageRange`/`StickyRange` values.
   - `PursuitCooldownTicks` and its use. With N = 1 it never has an effect; it is kept so N can be raised later. Say so in the `HeroProfile` comment.
   - Trap behaviour: `MoveOff` still calls `TriggerTrap`.
   - Saves and checkpoints.
6. **Debug state dump (`:2316`).** Add `Chased` to the chase fragment, so determinism diffs show it.

### Stage 3: the one unit test

Extend `ATouchingEnemyIsStruckThenChasesTheHeroBrieflyAndReturnsToItsRoute`, or add one case right after it that uses the same setup and the same helpers. Rename it if its name no longer says what it checks. Once the Grunt is back attacking the Keep:

1. Put the hero inside the enemy hit reach (touching) and strike again. The Grunt does **not** start `Pursuing` within the provocation window (bounded `StepWith`), and it targets the hero: `TargetsHero`.
2. Move the hero just beyond the enemy hit reach (about 0.25 tile), and record the Grunt's `AttackDueTick` at the moment of release.
   - Do not record it when the Grunt turns: under D1-a a hit may already be due then and land on the hero at once, which correctly advances the timer.
   - At the next step, assert `TargetsHero == false && TargetBuildingId == Keep.Id`. Both are set during the hold under D1-a, so check them explicitly.
   - Assert that `AttackDueTick` still equals the recorded value. The failure this catches is a timer reset.
3. The first Keep hit after the release lands no later than the recorded due tick (bounded steps), so the release really resumes the building.

What this test would catch:
- a second chase (the budget is broken);
- holding beyond hit reach (the stall band);
- a timer reset on switching (the D1 stall);
- a building attacker ignoring its striker.

Do not add separate tests for the walking-enemy hold, for traps or for legacy profiles. The kite run and the unchanged legacy tests cover them.

### Stage 4: verification

Each item gets its own evidence name under `plans/evidence/022/`:

1. **`HeroRules`** (EditMode `TheLastFortress.Tests.HeroRulesTests`): all pass, including the extended case.
2. **Kite run after the change** → `kite-after.txt`. Pass:
   - largest number of chases per enemy ≤ 1;
   - trap triggers off the route happen only while that enemy's single chase lasts;
   - the off-route share drops clearly from `kite-before.txt`.

   Report the numbers both ways.
3. **Policy run** (`ReleasePolicyTests.AHeroAloneCannotHoldTheFortressAndItsEffectIsLogged`) → copy `TestResults/019/hero-policy-runs.txt` to `plans/evidence/022/hero-policy-runs.txt`. Compare `release-rules-4` with the 2026-10-20 record in rules section 16:
   - active wins kill-zone 8/8, corridor 4/8, economy 8/8, each within one seed;
   - the hero's share of kills on Nights 6–12 near 8%, 8%, 4%;
   - a hero alone falls by Night 3, which the test asserts.

   A larger shift stops the stage for the owner's review. Do not tune.
4. **Full EditMode suite:** everything passes. Check `testcasecount`, because a wrong filter silently runs fewer cases.
5. **PlayMode `TheLastFortress.Tests.ReleaseSceneTests`:** WorldView's target readers changed, so this guards presentation. Run the full PlayMode suite only if this fails or you touched more than the four WorldView lines.
6. **Release build:** `-executeMethod TheLastFortress.Editor.BuildRelease.BuildWindows` succeeds with 0 errors. This build is what the owner checks live.

### Stage 5: documents (same change)

- **`specs/002-game-rules.md` → v1.8:**
  - Replace the section 18 "Threat: bounded pursuit" paragraph with spec section 5's text, plus the D1 sentence. Drop the cooldown from the text, since it cannot take effect while N = 1, and note that in section 16.
  - Add a row to section 16 with the kite numbers before and after and the policy re-run.
  - Update the version line.
- **Spec 008:** set its status line to "IMPLEMENTED and merged into `../002-game-rules.md` v1.8 section 18; archived", move it to `specs/archive/008-bounded-chase-limits.md`, and update `specs/archive/README.md` and the table in `specs/README.md`.
- **`plans/022-results.md`:**
  - fresh results with counts and dates;
  - which numbers are fresh and which come from earlier saved runs;
  - limits: the scripted kiting is not a player, and the live owner check is pending.
- **`plans/README.md`:** add a row for 022.
- Do not commit, push or publish unless the owner asks.

## 4. Stop conditions

- **A legacy profile test (V2/V3) changes result:** stop. The change leaked outside V4.
- **The policy re-run moves beyond the tolerance in Stage 4.3:** stop and report. Do not tune.
- **An Editor is open on the project:** use its test tools or wait. Never start a second Editor.

## 5. Owner check after delivery

In the release build, on a map with Spike Traps:
1. Strike a Brute that is attacking a Wall, let it chase you across a trap, and wait for it to go back.
2. Strike it again. It must turn and fight where it stands, not chase.
3. Step back just out of its reach. It must return to the Wall and hit it without a fresh wind-up.
