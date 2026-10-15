# Plan 004: Complete six-night combat and run perks

## Execution contract

Status: TODO. Priority: P1. Effort: L. Planned 2026-10-15 at `4855f69`, with expected input from completed plan 003. Execute after `plans/003-mvp-economy-foundation.md`; continue to 005 only after this plan's mechanical gates pass. Follow root `AGENTS.md` and `specs/003-six-night-mvp.md`.

The owner authorized one agent to implement 003 → 004 → 005 in order, bounded MVP tuning with before/after evidence, and minimal existing-test corrections only for demonstrated harness defects. No routine approval is needed between passing stages. Human playtest signoff is pending and cannot be claimed from automated evidence.

Repository: `C:/Users/Miroslav Pavelek/Documents/Games/the-last-fortress`; Unity project `game/The Last Fortress/`; editor `6000.3.16f1`. This stage adds four enemy types, six authored nights, ten perks, deterministic offers, and combat effects. Saving, procedural terrain, and the art pass belong to 005.

## Baseline, drift, and permitted files

Run `git status --short` and inspect changes since `4855f69`. Read `plans/003-results.md` and the live implementation before editing. Required state: separately selectable MVP scene, authoritative multi-cell occupancy, Houses/Farms/income/repair, walkable traps, Wall/Trap reflection, and a three-night foundation profile. Its core/economy tests and input checks must have credible fresh results. If any of that is missing or blocked, complete/report 003 rather than inventing a second architecture.

Original source facts to recognize:

- `Runtime/Core/RunSimulation.cs` originally uses a single `Definitions.Grunt`, `gruntTicksPerTile`, `MoveTicks`, ordered phases, `SpawnEntry`, and `Finish()`. Extend the 003 implementation rather than replacing it.
- `Runtime/Core/Definitions.cs` originally validates seconds-per-tile as whole 0.05-second steps. This rejects Runner 1.75 tiles/s and Brute 0.6 tiles/s; speed handling must change without rounding either to a different speed.
- `Runtime/Core/Entities.cs` holds Enemy/Building state; definitions are shared read-only tuning, not per-instance perk state.
- `Runtime/Unity/PrototypeDefinitionsAsset.cs`, `PrototypeContent.cs`, `WorldView.cs`, `HudView.cs`, `PrototypeBootstrap.cs`, and additive MVP content/setup/build from 003 are the integration points.
- Existing `Tests/EditMode/CombatPhaseStressTests.cs`, `RoutingTests.cs`, and PlayMode scene checks establish the original contracts. Reuse appropriate scenarios and keep focused new MVP cases in those assemblies, without a new test framework.

In scope: the above core and adapter files as required, focused additions such as `EnemyKind`, `MvpWaveFactory`, `PerkRules`, `RunRandom`, perk/wave definition data under `Assets/TheLastFortress/`, essential placeholder models/UI, MVP setup/build, meaningful tests, project README, `plans/004-results.md`, and plan index. No broad package/settings changes or original prototype retuning. Append enum/definition IDs rather than reorder existing serialized IDs. No source commits/pushes unless separately authorized. Use Unity skills for live Editor/UI work; never hand-edit scene/prefab YAML.

## Steps

### 1. Add enemy kinds and exact fixed-step movement

Add stable IDs Grunt=0, Runner=1, Brute=2, SiegeGolem=3. Existing `SpawnEntry` calls without a kind remain Grunt. Each spawned enemy uses its own definition; do not globally change Grunt to simulate a Brute. Preserve stable spawn ordering and original prototype outcomes.

| Kind | HP | Damage | Attack interval | Speed tiles/s | Wall-only multiplier |
|---|---:|---:|---:|---:|---:|
| Grunt | 30 | 10 | 1.00 | 1.00 | 1 |
| Runner | 20 | 6 | 0.75 | 1.75 | 1 |
| Brute | 150 | 20 | 1.50 | 0.60 | 2 |
| Siege Golem | 800 | 40 | 2.00 | 0.40 | 2 |

Retain a 0.05 s simulation step. Represent movement progress in fixed units: 1 tile = 1200 units, movement budget per step = `speed * 60` units (Grunt 60, Runner 105, Brute 36, Golem 24). Validate conversion as a finite positive integer, independently of attack intervals, which remain integer ticks. Consume remaining distance at intermediate cell centers so rounding does not slow a Runner or Brute on every tile. At a center, honor route invalidation before selecting the next segment; never cut diagonals, enter occupied cells, or teleport. At the final attack cell discard residual movement; acquire the attack on the next simulation step and wait a full interval, retaining prototype timing. Existing diagnostic `MoveTicks` may remain for compatibility, but fractional progress is authoritative.

All enemy types take open routes before breaching. Wall damage bonuses do not change routing and do not apply against Houses, Farms, Towers, or Keep. Tower targeting remains nearest living in-range enemy, spawn-ID tie break, held while valid. No armor, splash, projectiles-as-damage, flying, ranged enemies, or avoidance.

**Gate:** extend one movement fixture with the new speeds, checking total travel/remaining progress, cell-boundary replanning, and equal elapsed simulation time in different frame chunks. Check a Brute's damage against Wall versus Keep. Existing Grunt timing/range fixtures continue to pass. Avoid a test per property/default.

### 2. Store and preview the six authored wave plans

Create the full normal MVP profile below before Day 1; preview and combat reference the same stored plans. Original prototype/stress profiles stay separate.

| Night | Grunts | Runners | Brutes | Golem | Entries, round-robin | Non-boss cadence |
|---|---:|---:|---:|---:|---|---:|
| 1 | 10 | 0 | 0 | 0 | W | 1.5 s |
| 2 | 12 | 4 | 0 | 0 | W,E | 1.5 s |
| 3 | 14 | 4 | 2 | 0 | W,E | 1.25 s |
| 4 | 18 | 8 | 3 | 0 | W,E,N | 1.0 s |
| 5 | 22 | 10 | 4 | 0 | W,E,N | 1.0 s |
| 6 | 24 | 12 | 4 | 1 | W,E,N,S | 1.0 s |

W `(0,10)`, E `(19,10)`, N `(10,19)`, S `(10,0)`. This named MVP ordering differs from the old stress array's south/north ordering: do not relabel the stress array globally.

Build non-boss order by cycling Grunt, Runner, Brute, appending one of each with remaining count until exhausted. First spawn time zero, then cadence; assign active entries round-robin in the table's order. Add the Night 6 Golem from W at 30 s; at equal timestamps put non-boss before boss, then stable spawn ID. A 100-living-enemy cap delays due spawns FIFO instead of dropping them; pending spawns still prevent completion. Current authored counts fit the cap.

Show exact types/counts/directions and first/last spawn time, with a boss warning. No threat-budget generation or random hidden reinforcements. Clearing Night 6 requires the Golem AND every remaining/pending enemy to be resolved, not merely boss death.

**Gate:** one data-driven wave check verifies composition/order/time and preview/execution identity; a bounded final-wave fixture checks that dead boss plus surviving support does not win. Existing pending-spawn completion coverage should be reused.

### 3. Lock the reward RNG and offer contract

Use a small deterministic unsigned 32-bit xorshift32 generator, not `UnityEngine.Random`, `System.Random`, or string `GetHashCode()`. Next unsigned word: in unchecked uint arithmetic, `state ^= state << 13; state ^= state >> 17; state ^= state << 5`. Nonzero initial state; replace zero with `0x6D2B79F5`. For seed `1`, raw next words start `270369, 67634689, 2647435461`.

Run seed is uint, displayed/parsed as invariant unsigned decimal (0..4294967295). Reward initial state = `seed XOR 0xC8013EA4`, substituting the nonzero fallback only if needed. To select within positive bound n, reject words below `unchecked(0u - n) % n`, then use word `% n`; persist actual RNG state, not just original seed. 005 must reuse this algorithm and separate map streams.

Canonical perk IDs are the exact strings in step 4, sorted using ordinal comparison. Unowned perks all remain eligible. Weight 3 if the affected building exists, otherwise 1; Keep/general perks always 3. Draw three without replacement by cumulative integer weights, removing each chosen candidate; selecting does not consume extra RNG. Owner choices/builds can affect weighted offers. Freeze the three IDs and offer ID `(run,clearedNight)` on entering Reward; repeated callbacks never regenerate it. Declining/invalid clicks mutate nothing.

**Gate:** one focused deterministic offer fixture checks seed repeatability, distinct/unowned IDs, weighted eligibility, and preserved offer/RNG state on repeated requests. Assert an agreed algorithm vector once; do not snapshot every possible seed/offer.

### 4. Implement ten perks as derived instance effects

After Nights 1-5, offer three distinct unowned perks and accept exactly one. Each can be selected once per run. Prototype mode has no Reward state/perks. Add Reward without changing existing enum IDs. No reward after Night 6.

| ID | Effect |
|---|---|
| ReinforcedTimber | Walls +30% max HP; add max-HP delta to current HP once |
| EfficientFarming | Farm income, including adjacency bonus, +30% |
| ExpandedVision | Tower range +10%; no fog/vision system |
| LastStand | Tower direct damage +40% while Keep HP is strictly below 30% max |
| FortressHeart | Keep max/current HP +150, retaining missing HP |
| FlamingArrows | Tower hits apply non-stacking burn, 2 damage per second, three-second duration refreshed on hit |
| Recycling | Combat-destroyed non-Keep structure returns floor(actual price * 0.25) once; no bonus on sale |
| Sharpshooters | Tower direct damage +20%, excluding burn/reflection |
| BarbedDefenses | Wall reflects 3 per melee hit, adding to Trap synergy for max 6 |
| ThriftyConstruction | Future prices max(1, floor(base cost * 0.9)); existing actual prices unchanged |

Percent bonuses on the same base stat add, then floor once for integer stats: Tower base 10 with Sharpshooters + active LastStand gives 16, not 16.8 rounded through stages. Keep range floating point. Farm payment: floor((base output + adjacency bonus) * farming multiplier) per Farm, then sum with Houses and Keep. Apply current-HP deltas once when acquiring an HP perk, never on every HUD refresh/recalculation. Validate against effective max HP in damage, repairs, bars, and saves, not the old immutable definition max.

Store perks on the run. Compute effective values from shared definitions plus run modifiers; never mutate shared definition assets or leak into normal prototype/stress/fresh runs. Preserve actual discounted purchase prices for sale/Recycling. Killing/destroying an entity can pay once even if burn, reflection, and a Tower share a step.

**Gate:** group meaningful edge cases: additive damage + exact LastStand boundary, one-time HP grant/recompute, actual-price sale/destruction without double refund, and income forecast/payment agreement with perk+adjacency. Do not add one trivial test per perk field.

### 5. Define effect ordering and integrate the reward transition

MVP step order: due spawns; movement and Trap entry damage; Tower attacks/burn applications; due burn ticks on living enemies; living enemy attacks with immediate non-recursive reflection; dead-enemy cleanup; defeat before cleared-wave handling. Process stable entity IDs within categories. Stop evaluating attacks from already dead entities. A Wall's lethal received hit still reflects once.

Initial burn schedules first tick 20 simulation ticks after application, expiry 60 ticks after. Refresh extends expiry to current tick +60 but retains the next tick's cadence; no concurrent burn stacks. Tick at expiry is included; the first application alone causes exactly three ticks. An already expired burn reapplied starts a fresh cadence. Pausing stops all timers. Expired/dead targets cannot produce damage, effects, or refund twice.

Night clear with positive Keep HP enters Reward for Nights 1-5. Combat/spawns remain stopped and building actions disabled there. Perk choice commits once, applies effects, enters the next Day, then pays that Day's income once. Night 6 goes directly to Victory with no Day 7 or reward. Restart clears offers/perks/burn/RNG and restores the same seed's fresh state unless New Seed was explicitly requested. Track run summary counters now: nights cleared, enemies defeated, buildings purchased, gold earned excluding starting budget, synergy activations. These must not count preview/debug actions as real purchases.

**Gate:** one bounded integration fixture reaches Reward, double-submits a choice, checks one perk and one income transaction, then restarts cleanly. One focused combat fixture covers refresh/pause and burn/reflection deaths. Do not duplicate every prototype phase test.

### 6. Complete usable six-night UI and measure viability

Add reward cards with exact effect text, current build relevance, offer ID safeguards, six-night progress, enemy silhouettes/colors sufficient to distinguish types, burn feedback, boss HP/warning, and actual income/price/repair quotes. No complex art production yet. Update saved MVP scene/data and the existing `BuildMvp` method from 003 without disturbing prototype output.

Record two bounded replayable policies on the flat authored map: economy-first and defense-first. Store night-by-night purchases/sales/repairs/placements, perk choices, seed, and outcomes; do not only store the final layout. At least one legal six-night completion is required to claim a mechanically viable profile. Attempt no-perk debug baseline to evaluate whether perks are mandatory; debug skipping is visibly labeled and unavailable as a normal reward choice. A failed attempt is evidence, not proof that no strategy exists. Do not claim two viable strategies or optional perks unless the recorded runs demonstrate them.

Tuning authorization: up to three documented passes, each value remaining within ±20% total of specification 003, affecting only MVP building costs, House/Farm output, enemy HP, Tower direct damage, and positive non-boss counts. Starting gold 250, base Keep HP 500/income 20, footprints, refund ratios, routing/order, boss count/time, and perk identities remain fixed. Keep original prototype values unchanged. Record before/after using the same seed/policy, and update the MVP tuning table/report. If it still cannot complete within those bounds, record the blocker and stop dependent work; do not silently grant resources or invulnerability.

## Verification and evidence

Use the live Editor Test Runner/Unity MCP pinned to this exact project when open. Otherwise run sequentially from repository root, waiting for each result:

```powershell
$fortressEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
$fortressProject = (Resolve-Path 'game/The Last Fortress').Path
$fortressResults = Join-Path $fortressProject 'TestResults'
New-Item -ItemType Directory -Force -Path $fortressResults | Out-Null
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform EditMode -testResults "$fortressResults/004-EditMode.xml" -logFile "$fortressResults/004-EditMode.log"
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform PlayMode -testResults "$fortressResults/004-PlayMode.xml" -logFile "$fortressResults/004-PlayMode.log"
& $fortressEditor -batchmode -nographics -quit -projectPath $fortressProject -executeMethod TheLastFortress.Editor.BuildMvp.BuildWindows -logFile "$fortressResults/004-Build.log"
```

New build method is supplied by 003. These batch invocations were not rerun during planning; live export was used for the prior review. No `-quit` on tests; no second Editor for an open project. If necessary wait with quoted `Start-Process -WindowStyle Hidden -Wait -PassThru`. Check fresh nonzero Passed XML with zero failures/unexpected skips, compile logs, and successful BuildReport/executable, not merely shell exit. Run affected tests first and broad relevant suites once after integration; do not chase test-count/coverage quotas. Existing test corrections require a demonstrated harness defect. Add focused new-feature cases to appropriate fixtures without rewriting original scenarios or weakening assertions to bless a regression.

Done when the six waves, four enemies, five single-choice rewards/ten perks, effect ordering, UI, restart isolation, focused verification, and at least one legal full-run policy are evidenced. Record unresolved balance/human-playtest claims honestly. Save `plans/004-results.md`, update the index, then proceed 005. No fresh-player claim can be satisfied by an agent pretending to be a player.

Stop for missing 003 prerequisites, broken original routing/occupancy/campaign state, unresolved data loss, or verification failure after two focused fixes. Keep cosmetics and procedural-map issues scoped to 005; do not start over or broaden into release features.
