# Six-night MVP rules and scope

Version 1.0, 2026-10-15. Implement only after acceptance of the one-wave navigation prototype in `002-navigation-prototype.md`. This document makes the GDD's MVP scope concrete; it is not a request to implement the MVP alongside the prototype. An implementing agent must first prepare an incremental MVP implementation plan.

Owner-confirmed choices: retain open-route-first breaching; provide guaranteed Keep income; defer soldiers. Remaining rules and numerical values are initial design defaults. Balance changes require recorded fixture results; expanding scope requires a new decision.

## Exact content

| Area | MVP content |
|---|---|
| Map | 20 x 20, one grassy biome, flat logical terrain |
| Buildings, including Keep | Keep, Wall, Archer Tower, House, Farm, Spike Trap |
| Enemies, including boss | Grunt, Runner, Brute, Siege Golem |
| Run | Six nights, five perk selections, boss on Night 6 |
| Perks | Ten, listed below; all unlocked initially |
| Synergies | Farm + House; Wall + Spike Trap |
| Replay | New seed or same seed, summary, day-boundary resume |

No Barracks, soldiers, flying/ranged enemies, active abilities, building upgrade tiers, Cannon/Watch/Repair structures, elevation, fog of war, map expansion, unlock progression, or challenge mode. The GDD's other examples describe future possibilities.

## Retained prototype rules

Retain document 002's coordinate conversion, reserved outer ring, four-way movement, open-route-first breach policy, stable tie-breaking, revision invalidation, fixed simulation step, attack timing, immediate damage, targeting, pause semantics, and terminal-state priority. A 2 x 2 building occupies four cells belonging to one entity: HP, damage, sale, refund, and destruction happen once per building, not once per cell. Entering any occupied cell in a breach graph uses the prototype penalty; no actor may enter the footprint until destruction removes all its cells.

## Construction and economy

| Building | Footprint | Cost | HP | Effect |
|---|---|---:|---:|---|
| Keep | 1 x 1 | 0 | 500 | +20 gold/day from Day 2; defeat at zero HP |
| Wall | 1 x 1 | 20 | 100 | Solid, destructible |
| Archer Tower | 1 x 1 | 70 | 100 | 10 damage per 0.75 seconds, range 4 |
| House | 1 x 1 | 40 | 80 | +15 gold/day |
| Farm | 2 x 2 | 60 | 100 | +25 gold/day, requires four buildable land cells |
| Spike Trap | 1 x 1 | 35 | none | Walkable floor layer; entry damage described below |

- Start Day 1 with 250 gold, full Keep HP, and no passive income payment. There are no normal-enemy kill rewards or negative-gold debt.
- Receive 20 Keep gold plus surviving economic-building income once on entering each new Day, through Day 6. No Day 7 income after victory.
- Farm anchor is the lowest `(x,z)` cell of its footprint; no rotation. Every footprint cell must pass placement validation. Farm ghost displays all four cells and the complete price.
- Terrain has explicit flags for walkability and buildability. Land allows both. Forest, rock, and water allow neither and cannot be harvested, cleared, or attacked in the MVP. Terrain meshes never create extra hidden movement blockers.
- All solid buildings exclude traps. A trap requires a buildable cell without a solid building or another trap; units may walk over it. No stacking a trap under a Wall.
- Spike Trap damages each ground enemy for 8 once upon entering its cell. Occupying the tile does not cause repeated ticks. It has no charges, cooldown, HP, or direct enemy targeting in the MVP. Leaving and re-entering triggers another hit; navigation never loops to farm trap damage.
- Build, sell, and repair only during Day. Confirm a valid transaction at execution time. Rejected actions never spend money or partially occupy a footprint.
- Sell refunds `floor(actualPurchasePrice * 0.5)`. The Keep cannot be sold. Combat destruction gives zero by default. Store actual price on the instance so discounts cannot create refund arbitrage.
- Repairs are a selected-building button, including the Keep. Repair as much missing HP as current gold permits, at 5 HP/gold; cost `ceil(actualRestoredHP / 5)`. No partial payment without healing, no healing above maximum, and no automatic free repair. Traps have no repair action. The button shows HP restored and exact price before use.
- Destroyed buildings leave buildable terrain and no rubble blocker. Damage persists between nights. Economic buildings destroyed during Night earn no following-day income.

Guaranteed income offers a recovery path; it does not guarantee survival after a poor opening.

## Adjacency and synergies

Buildings are adjacent only when their occupied cells share an edge. Corners do not count. Evaluate unique building IDs rather than counting shared cell edges. Recompute on placement, sale, destruction, and relevant perk changes.

- **Farm + House:** a Farm with one or more adjacent surviving Houses earns +5 gold/day total, not +5 per House. A House can benefit multiple adjacent Farms. Show one highlighted link per Farm/House pair and the Farm's final forecast income.
- **Wall + Spike Trap:** a Wall adjacent to one or more traps reflects 3 damage to a melee enemy per successful hit it receives. Multiple traps do not increase this value. Reflected damage cannot trigger another reflection. A Wall destroyed by a hit still reflects for that hit once; an enemy killed by reflection does not attack again.
- No other adjacency bonuses, vision effects, or implicit synergies exist in the MVP.

## Combat additions

| Enemy | HP | Damage per hit | Interval | Speed, tiles/second | Behavior |
|---|---:|---:|---:|---:|---|
| Grunt | 30 | 10 | 1.0 s | 1.0 | Prototype ground behavior |
| Runner | 20 | 6 | 0.75 s | 1.75 | Same routing; faster movement |
| Brute | 150 | 20 | 1.5 s | 0.6 | Same routing; damage x2 against Walls only |
| Siege Golem | 800 | 40 | 2.0 s | 0.4 | Same routing; damage x2 against Walls only |

No armor, resistances, range attacks, flying movement, splash damage, friendly fire, or collision avoidance. Brutes and the boss still take any open route; their wall bonus does not override the owner's routing choice. With multiple-cell targets, attack from an unoccupied cell cardinally adjacent to any footprint cell. Damage and destruction resolve once against the owning building.

## Authored waves and preview

Generate no random composition in the MVP. Store each night's exact plan before showing its Day UI, and execute that same plan. Show counts by type, direction, first/last spawn timing, and boss warning. Enemies remaining = scheduled-but-not-spawned plus alive.

| Night | Grunts | Runners | Brutes | Golem | Active entries | Non-boss cadence |
|---|---:|---:|---:|---:|---|---|
| 1 | 10 | 0 | 0 | 0 | West | 1.5 s |
| 2 | 12 | 4 | 0 | 0 | West, East | 1.5 s |
| 3 | 14 | 4 | 2 | 0 | West, East | 1.25 s |
| 4 | 18 | 8 | 3 | 0 | West, East, North | 1.0 s |
| 5 | 22 | 10 | 4 | 0 | West, East, North | 1.0 s |
| 6 | 24 | 12 | 4 | 1 | West, East, North, South | 1.0 s |

- Entries are West `(0,10)`, East `(19,10)`, North `(10,19)`, South `(10,0)` in that order.
- Build the non-boss spawn list by repeatedly appending one Grunt, one Runner, one Brute where that type still has remaining count. Assign successive entries round-robin; first spawn at time zero, subsequent spawns at the table's cadence.
- Spawn the Golem from West at 30 seconds on Night 6; at equal times use non-boss spawn before boss, then stable spawn ID. The boss is additional to the non-boss cadence.
- Maximum 100 living enemies. If a future tuning edit reaches the cap, retain scheduled spawns in FIFO order and release them as slots open; pending entries prevent wave completion. The initial authored table never reaches this cap even if all enemies survive.
- A single exact plan is serialized for resume/retry; preview does not reroll after building or pausing.
- Threat-budget generation belongs to a later milestone. Before adding it, define costs, composition constraints, unspent-budget behavior, and seed-separated RNG streams, then compare generated waves with this authored baseline.

## Perks and stacking

Offer three distinct unowned perks after Nights 1 through 5; choose one. Every perk is available from a fresh profile and can be selected only once per run. Never offer soldier, Cannon, or other absent-system perks.

| Perk ID | Effect |
|---|---|
| ReinforcedTimber | Walls +30% maximum HP; add the same maximum-HP delta to existing current HP |
| EfficientFarming | Farm daily income +30%, including its adjacency bonus |
| ExpandedVision | Archer Tower attack range +10%; name is flavor, no fog/vision mechanic |
| LastStand | Tower damage +40% while current Keep HP is strictly below 30% of current maximum |
| FortressHeart | Keep +150 maximum and current HP, preserving existing missing HP |
| FlamingArrows | Tower hits apply burn: 2 damage every 1 second for 3 ticks; repeated hits refresh remaining duration without stacking burn instances |
| Recycling | Combat-destroyed non-Keep buildings refund `floor(actualPurchasePrice * 0.25)` once; does not apply to sale |
| Sharpshooters | Tower direct damage +20%; no bonus to burn or reflection |
| BarbedDefenses | Walls reflect 3 damage per received melee hit, adding to the trap synergy for maximum 6 |
| ThriftyConstruction | Future construction prices -10%, `max(1, floor(basePrice * 0.9))`; existing instance prices unchanged |

- Percent bonuses within the same stat add against the base, then round once. Example: base Tower damage 10 with Sharpshooters and active Last Stand is `floor(10 * (1 + 0.2 + 0.4)) = 16`. Range remains a float. HP, prices, and income are integers.
- Farm income is `floor((25 + adjacencyBonus) * (1 + farmingBonus))` per surviving Farm. Sum those integers, House income, and Keep income for the displayed forecast. The actual payment uses the same calculation after reward selection.
- Perks affect existing and future eligible entities unless explicitly marked future-only. Recompute maximum HP without repeatedly granting current HP when recalculating the same modifier.
- Burn tick timing uses simulation time. Hits refresh the three-second expiration but do not reset the next one-second tick. Ticks occur only up to and including expiration. No damage after the target dies. MVP update order: due spawns, movement, Tower attacks/applications, due burn ticks on surviving enemies, surviving enemy attacks with immediate non-recursive reflection, then defeat before victory. Process stable IDs within each category and test same-step outcomes before coding it.
- An unowned perk has offer weight 3 when its affected building type exists, otherwise weight 1; Keep/general perks always have weight 3. Draw without replacement using a dedicated reward RNG stream. All ten are eligible regardless of current ownership of a building, so selling everything cannot make three choices impossible; clearly label perks for a building not currently owned.
- Keep reward offerings and chosen IDs in run state. Reloading cannot reroll or choose twice. Same seed and same player decisions reproduce offerings under the same rules version. Different builds may lead to different weighted offers by design.

## Phase transitions and persistence

1. Fresh run enters `Day(1)` with starting budget; validate/generate map and store all wave plans. Save initial state without income payment.
2. Start Night first stores the current Day checkpoint, including placement, gold, damage, and chosen perks; then enters `Night(n)`. Building transactions are no longer permitted.
3. Keep HP reaching zero enters Defeat on that simulation step, regardless of remaining enemies/spawns. Otherwise a cleared Night resolves only after all scheduled/pending/alive enemies are gone: Night 6 enters Victory, and Nights 1-5 enter Reward.
4. Reward pauses combat, generates/preserves one offer, and permits one choice. Apply the chosen perk, enter `Day(n+1)`, pay that Day's income once, then save the resulting complete checkpoint. A repeated callback cannot pay again.
5. Quitting during Night or Reward resumes the previous Day checkpoint, before that Night's combat. Persist the planned reward offer when first generated so retrying cannot reroll it. Explain "Continue returns to the start of your current night" in the menu. This is not mid-combat saving.
6. Victory/Defeat clear Continue for the completed run and save summary/settings. Same-seed retry creates a fresh run with 250 gold, no perks, full HP, and the same terrain/wave definitions under the same rules version.

Day checkpoints are committed on Day entry and immediately before Night, not after every construction click. If the player quits during Day, offer Save and Quit to persist current placement/gold/damage as that Day's checkpoint. The later MVP plan must define and test failure handling for these writes; never show a successful save indicator before atomic replacement succeeds.

Use separate versioned settings and run-save records, atomic replacement via a temporary file, and stable definition IDs rather than Unity instance references. Track Day/income transaction IDs to prevent duplicate payment. Include rules version, seed, terrain cells, wave plans, owned perks, reward offers, building type/anchor/current HP/actual purchase price, gold, and checkpoint night. Corrupt/unknown-version saves show a clear error and offer New Run without silently overwriting the original file. No cloud save or unlock data in the MVP.

## Procedural variation

- Keep `(10,10)`, four spawn cells, and the full boundary ring are fixed land. Keep the 5 x 5 area centered on the Keep as empty buildable land at run start.
- Initial generator candidate: start all land; choose 30 distinct eligible cells outside the reserved areas, without replacement, using a dedicated map RNG stream. Assign ten forest, ten rock, ten water cells in the sampled order. Visual water remains below the walkable surface.
- Validate open connectivity from every entry to a Keep neighbor before construction, and at least two non-overlapping buildable 2 x 2 Farm sites outside the Keep footprint. Invalid candidates retry with deterministic attempt-index sub-seeds, at most 20 attempts; then use the authored flat fallback map and disclose the fallback in diagnostics.
- RNG algorithm and seed encoding must be specified and locked in the later MVP plan; do not depend on process-randomized string hashes or unrelated effects consuming the map/reward RNG.
- Reject enclosed isolated walkable pockets unless reachable from the Keep region. Preview routes with the actual navigation rules, not a separate approximate validator.
- Validate at least seeds 0-999 in automated tests, report fallback rate, and inspect ten fixed seeds visually. This tests validity, not whether random terrain creates interesting strategies.
- Add further layout templates or terrain rules only after playtests show that this variation changes useful build choices. Elevation and forest harvesting are out of scope.

## UI, settings, summary, and validation

- Build hotkeys: 1 Wall, 2 Archer Tower, 3 House, 4 Farm, 5 Spike Trap. Keep is not purchasable. Sell/Repair are explicit selected-building buttons. No unit-command mode.
- Add Day/night number, income forecast, exact upcoming composition, adjacency highlighting, reward screen, Continue/New Run/same-seed retry, and a seed display/copy action to the prototype HUD.
- Settings: master/music/effects volume, camera sensitivity, camera shake toggle, window mode, and resolution. All threats must have visual cues; do not require audio to understand attacks. Preserve text legibility at 1280 x 720 and 1920 x 1080.
- Summary: nights cleared (0-6), enemies defeated, buildings purchased, gold earned (starting gold excluded), and currently/ever activated synergy counts. Drop the ambiguous "strongest synergy" ranking until a meaningful metric is designed.
- Re-run prototype regressions with multi-cell footprints, trap entry, adjacency de-duplication, income exactly once, actual-price refund, repair rounding, perk stacking, weighted distinct offers, boss completion, save/load, corrupt saves, and same-seed replay.
- Re-run the reference 100-enemy performance gate with actual MVP content and record results. Prototype performance is not a substitute.
- Playtest with at least five new players: target four understanding placement/start-night/route warning within two minutes without verbal coaching, and at least three correctly explaining why their first run ended. Record whether they voluntarily restart. These are proposed small-sample decision gates, not validated market predictions.
- Demonstrate at least two distinct winning layouts on the same authored map, one with economy investment and one with earlier defense investment. Record placements, purchases, perks, and outcomes; establish no-perk baseline viability through a debug fixture before asserting that perks are optional.
- Record six-night session duration with construction time separated from combat time. The 15-20 minute target is a playtest goal; do not add a construction countdown to force it.

## Before MVP implementation

Finish and review the prototype first. Then create a separate implementation plan with explicit runtime/test files, a locked RNG algorithm, tests for the burn/reflection update order, save-failure behavior, fixture layouts, and measured prototype results. Revisit the wave counts, economy rate, and Farm footprint from those results. Do not treat this scope document as permission to invent unspecified full-release systems.
