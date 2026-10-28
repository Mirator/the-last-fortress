# Plan 022 results: one hero chase per enemy per night

Executed 2026-10-28 against `702bd71`. The working tree had no code drift. Implements `specs/archive/008-bounded-chase-limits.md` v0.3, now merged into `specs/002-game-rules.md` v1.8 section 18 (with a new row in section 16). Nothing is committed.

## What changed

All paths are under `game/The Last Fortress/Assets/TheLastFortress/`. Only `release-rules-4` behaviour changes. Older profiles keep `MeleeHoldsHero` and their own values.

- `Runtime/Core/Entities.cs`: two new fields on each enemy. `Chased` records that it has used its one chase (an enemy lives one night). `ResumeBuildingId` remembers the building it turned from.
- `Runtime/Core/RunSimulation.cs`:
  - `StartsPursuit` sets `Chased` when a chase starts. Once it is set, the enemy only fights back.
  - The new `FightsBackInPlace` implements the spec text:
    - the enemy attacks the hero only while the hero is within its hit reach, and never leaves its route;
    - a struck enemy may also turn at its route end or while attacking a building;
    - a building it turned from is resumed with the same attack timer (D1-a), so a hit that is already due lands at once.
  - The debug state dump shows `Chased` and the remembered building.
- `Runtime/Core/HeroMotion.cs`: the V4 `EnemyHitReach` is 0.225 instead of 0.20 (D2-a). `HitHero` and the chase's stopping distance follow from it. Comments updated; the cooldown field is kept but has no effect while each enemy may chase only once.
- **Deviation from plan step 2.3/2.4.** The building is not kept in `TargetBuildingId` during the hold; it is stored in a separate field, `ResumeBuildingId`, which the plan allowed. This keeps the invariant that an enemy targets either a building or the hero, never both, so no WorldView reader or building-attack counter had to change.
- **Not changed.** The first chase still resets the enemy's building attack timer when it starts; the timer rule applies to fighting back in place, which is where the stall was. Spike Traps still fire during the one chase.

## Tests

- `Tests/EditMode/HeroRulesTests.cs`: the chase test was extended and renamed `AStruckEnemyChasesTheHeroOnceThenOnlyFightsBackInPlace`. After the first chase it checks that:
  - a struck Grunt at the Keep does not chase again;
  - it turns on the hero where it stands;
  - it lets go at 0.25 tile;
  - it resumes the Keep with its attack timer unchanged;
  - its first Keep hit lands no later than that timer.
  
  A guard assertion makes the test fail if the kept timer happens to equal a restarted one.
  - **Not done:** the test was not mutation-checked by reverting the Core change.
  - **Reasoning:** without the one-chase limit, the "no second chase" assertion fails; with a timer restart, the timing equality fails.
- `Tests/EditMode/ReleasePolicyTests.cs`:
  - a new `HeroScript.Kite`: fight nearest, then after each contact retreat for a fixed 40 steps toward and past the Spike Trap nearest the struck enemy;
  - chase and off-route counters in `HeroLog`;
  - a test that only logs, `KitingEnemiesAcrossTrapsIsLogged` (corridor, `release-rules-4`, seeds 1-8).

## Verification (fresh, 2026-10-28; logs in `plans/evidence/022/`)

| Check | Result |
|---|---|
| Baseline `HeroRulesTests`, before the change (`base/Core`) | 16/16 passed |
| `HeroRulesTests` after the change (`after/Core`) | 16/16 passed |
| Full EditMode (`after/EditMode`) | 129/129 passed |
| PlayMode `ReleaseSceneTests` (`after/Scene`) | 8/8 passed |
| Release build (`after/Build.log`) | Succeeded, 0 errors, 2026-10-28 09:59; `Builds/Release/TheLastFortress.exe` |

**Kite run, corridor, seeds 1-8** (`kite-before.txt` → `kite-after.txt`):

| | Before | After |
|---|---|---|
| Most chases by one enemy | 2–7 (seed 6: 7) | 1 on every seed |
| Chases in total | 347 | 291 |
| Most off-route trap hits on one enemy | 1–2 | 1 |
| Enemy steps off the route | 0.9–2.6% | 0.9–2.4% |
| Wins (outcomes per seed) | 3/8 | 3/8 (identical) |

- **The share of time off the route barely moves.** The script retreats a fixed short distance and then fights the enemy to death, usually inside the first chase, so most of its time off the route was already spent in that chase.
- So the run shows that the limit holds: one chase and one off-route trap hit per enemy. It does not show how much a determined human kiter gains. Plan 022 expected the off-route share to drop clearly, and that part was **not** demonstrated by this script.

**Policy re-run, `release-rules-4`.** The same test was run twice on 2026-10-28, without the change (the three `Runtime/Core` files stashed; `hero-policy-runs-baseline.txt`) and with it (`hero-policy-runs.txt`). Results with the change:
- active wins: kill-zone 8/8, corridor 4/8, economy 8/8;
- the hero's share of kills on Nights 6-12: 8%, 8%, 4%;
- Keep damage and the idle-hero results are identical in both runs;
- a hero alone falls by Night 3 or earlier.

What the change moved:
- active hero deaths: 70/50/22 → 64/50/21;
- hero contacts: slightly different;
- the peak share of kills in a single night (on corridor): 20% → 24%.

This is within the plan's tolerance.

Compared with the older 2026-10-20 record (`plans/evidence/019/threat/hero-policy-runs-threat.txt`), the idle kill-zone Keep damage fell from 900 to 160. The baseline run shows that this drop came from the 2026-10-21 changes (1 x 1 Cannon, drawn collision), which had no policy run of their own, and not from this change.

## Limits and pending

- **Owner live check is pending.** On a map with Spike Traps:
  1. strike a Brute at a Wall and let it chase you across a trap;
  2. strike it again: it must fight where it stands;
  3. step back: it must hit the Wall at once, without a fresh wind-up.
- Scripted play is not a player. Whether fighting back in place feels like enough threat has not been judged by anyone.
- **Known caveat** (from the spec): a walking enemy decides only at cell centres. After its chase, a Brute walking past the hero may not turn until its next centre, up to 5 s away.
