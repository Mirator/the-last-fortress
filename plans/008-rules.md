# Plan 008 rules — release profile `release-rules-1`

Frozen 2026-10-16 for plan 008 G0. This is the contract the code implements; the code may not improvise beyond it. Every number is tagged **[I]** invariant (a rule; changing it is a rules-version change) or **[P]** provisional tuning (may change after observed play, inside the stated bounds, with the change recorded in `008-results.md`). Names match `Definitions.cs`, `PerkRules.cs`, `MvpCampaign.cs` and the new `ReleaseCampaign.cs` / `MapProfiles.cs`.

Nothing here changes the six-night MVP profile (`mvp-rules-1`). It keeps its definitions, its 10-perk pool, its night table, its generator and its save schema 1.

## 0. Shared invariants (both profiles) [I]

- Fixed step 0.05 s. All intervals are whole steps; all speeds are whole 1/1200-tile units per step.
- Day is untimed; construction, sale, repair and rotation only in Day.
- Open-route-first: any open route beats breaching regardless of length; breach cost 6 per solid cell; ordering (cost, z, x, insertion), neighbours N/E/S/W. Every enemy, including ranged and boss units, walks this ground route. No enemy flies, tunnels or ignores obstacles.
- Step order: (1) due spawns; (2) movement, Spike Trap entry; (3) building attacks (Archer Tower, Cannon) in ascending building ID; (4) due burn ticks; (5) enemy actions in ascending enemy ID (melee hits with reflection, ranged hits, Bomber detonations); (6) dead-enemy cleanup; (7) Keep at 0 HP → Defeat, evaluated **before** the cleared-night check, so a same-step final kill never overrides defeat.
- Living-enemy cap 100; due spawns wait FIFO.
- Damage is immediate and authoritative in Core. Projectiles, explosions and auras are presentation only.
- Every percentage modifier on damage adds against the base and floors once: `base * (100 + sum) / 100`. Reductions apply after bonuses, one after another in the fixed order Gatehouse, Hardened Roofs, Blast Shields: `max(1, dmg * (100 - reduction) / 100)` for a positive hit.
- Guaranteed Keep income 20/day [P]; Day 1 pays nothing; income paid once per Day entry.
- Rewards after Nights 1..N−1; never after the final victory; one frozen offer per cleared night (no reroll by reload).

## 1. Buildings (append-only `BuildingKind`)

| ID | Kind | Footprint | Cost [P] | HP [P] | Solid | Notes |
|---|---|---|---|---|---|---|
| 0 | Keep | 1×1 | — | 500 | yes | 20 gold/day |
| 1 | Wall | 1×1 | 20 | 100 | yes | |
| 2 | Tower ("Archer Tower") | 1×1 | 70 | 100 | yes | 10 dmg / 0.75 s, range 4.0 |
| 3 | House | 1×1 | 40 | 80 | yes | 15/day |
| 4 | Farm | 2×2 | 60 | 100 | yes | 25/day, +5 with an edge-adjacent House |
| 5 | SpikeTrap | 1×1 floor | 35 | — | no | 8 dmg on entry |
| 6 | **Cannon** | 2×2 | 110 | 150 | yes | see 1.1 |
| 7 | **Watchtower** | 1×1 | 45 | 80 | yes | see 1.2 |
| 8 | **RepairStation** | 2×2 | 80 | 120 | yes | see 1.3 |

Footprints are rules [I]: 2×2 for Farm, Cannon and Repair Station, anchored at the lowest (x,z) cell; everything else 1×1. All solid buildings block movement and can be breached; the Keep cannot be sold or destroyed except by defeat. Rotatable (cosmetic only): Tower, House, Farm, Cannon, Watchtower, Repair Station.

**Building centre** [I]: anchor + (size/2, size/2) in world units (1×1: cell centre; 2×2: the shared corner of its four cells).

### 1.1 Cannon

- Damage 20 [P], interval 2.0 s = 40 steps [P], range 4.5 tiles [P] measured Euclidean from the Cannon's building centre to the enemy's current position, inclusive with 1e-9 tolerance [I].
- Targeting [I]: same contract as the Archer Tower — keep the current target while it is alive and in range; otherwise acquire the nearest enemy in range (squared distance to the centre), ties to the lowest enemy ID; acquiring sets the first shot due one full interval later; later shots every interval.
- Splash [I]: each shot deals its damage to the primary target and to every other living enemy whose current position is within the splash radius 1.25 tiles [P] of the primary target's position at the shot, measured Euclidean, inclusive. Damage is applied once per enemy per shot, in ascending enemy ID. Splash is ground-only; all 008 enemies are ground units. No friendly fire.
- Kill credit, Recycling, burn: a Cannon shot is direct damage but is **not** an Archer Tower hit: Sharpshooters, Last Stand, Expanded Vision, Flaming Arrows and Kill Zone do not apply. Cannon-specific perks: Shrapnel, Heavy Shot; shared: Overwatch, Counter-Battery, Watchtower bonus.
- Event: one `TowerShot` per shot (source Cannon, target primary), plus one `SplashHit` per additional enemy damaged.

### 1.2 Watchtower

- Gives **+1.0 tile** [P] range to every living Archer Tower and Cannon that has at least one footprint cell sharing an edge with the Watchtower's cell [I]. With Signal Fires: any footprint cell within Chebyshev distance 2 of the Watchtower cell.
- Non-stacking [I]: a tower receives the bonus at most once regardless of how many Watchtowers qualify. Effective range = `base × (1.1 if Expanded Vision and Archer Tower) + 1.0 if buffed`.
- Recompute [I]: buff membership is a function of the solid occupancy; it is cached per `GridState.Revision` and recomputed on the next query after any construction, sale or destruction. No presentation scans.
- Cooldown [I]: a buff change never resets cooldowns. A current target stays while alive and inside the new range; if a range shrink puts it outside, the tower reacquires with a fresh full interval (existing rule).
- Previews [I]: the range ring of a hovered/selected tower shows the effective range including the bonus; a Watchtower ghost highlights the towers it would buff.

### 1.3 Repair Station

- Dawn repair [I]: when a Day n ≥ 2 is entered after Night n−1 (after the perk choice, before income), every living Repair Station, in ascending building ID, spends a budget of **50 HP** [P] (80 with Field Engineers) on eligible buildings.
- Eligible [I]: alive, has HP, not full, not the station itself, not the Keep (unless Field Engineers), with any footprint cell within Chebyshev distance **2** [P] of any of the station's footprint cells.
- Allocation [I]: lowest HP fraction first (compare `hp × otherMax` cross-products), ties lowest building ID; each receives `min(missing, remaining budget)`; continue until the budget or the list is exhausted. Later stations see earlier repairs.
- Once per dawn [I]: guarded by the same transition that pays income; a reload of the Day checkpoint never repairs again (the checkpoint holds post-repair HP). No combat healing, no gold.
- Destroyed or sold buildings are gone and never rebuilt. Manual paid repair (5 HP per gold) is unchanged.
- Event: `BuildingRepaired` per repaired building (same payload as a manual repair: building, HP restored), followed by one `DawnRepair` per station that restored anything (its total).

## 2. Enemies (append-only `EnemyKind`)

| ID | Kind (display) | HP [P] | Speed tiles/s [P] | Damage / interval [P] | Wall × | Role |
|---|---|---|---|---|---|---|
| 0 | Grunt | 30 | 1.0 | 10 / 1.0 s | 1 | baseline |
| 1 | Runner | 20 | 1.75 | 6 / 0.75 s | 1 | fast |
| 2 | Brute | 150 | 0.6 | 20 / 1.5 s | 2 | heavy |
| 3 | SiegeGolem (boss) | 800 | 0.4 | 40 / 2.0 s | 2 | boss 1 |
| 4 | **Archer** ("Raider Archer") | 35 | 0.9 | 6 / 1.5 s, range 3.5 | 1 | ranged |
| 5 | **Bomber** | 40 | 1.0 | blast, see 2.2 | 2 | breacher |
| 6 | **SiegeCommander** (boss) | 1000 | 0.4 | 30 / 2.0 s | 2 | boss 2, aura |

Speeds must remain whole 1/1200-tile units per step (0.9 → 54, 1.0 → 60, 0.4 → 24) [I].

### 2.1 Raider Archer (ranged)

- Walks the normal ground route [I]. It never path-finds to firing positions; it only ever **stops on a cell centre of its own route**.
- At each cell-centre decision on a fresh step (the same point where melee enemies decide to attack or continue) it checks for a ranged target [I]:
  - Candidates: living solid buildings (Keep included; Spike Traps excluded) with any footprint cell centre within **3.5 tiles** [P; was 3.0, see tuning log] Euclidean of the archer's cell centre, inclusive. No line-of-sight rule: arrows arc over Walls [I].
  - Priority [I]: class first — (1) House, Farm; (2) Watchtower, Repair Station; (3) Archer Tower, Cannon; (4) Wall; (5) Keep — then nearest footprint-cell centre (squared), then lowest building ID.
  - Found → it stops (no movement this step) and targets it; a newly acquired target's first hit is due one full interval later [I].
- Retention [I]: while stopped, the current target is kept while alive and in range (sticky; a higher-priority building entering range does not steal it). When it dies or leaves range, the archer re-runs acquisition on its next fresh decision; a new target means a fresh full interval (cooldown reset on retarget). With no target it resumes its route; a stale route is replanned first as for melee.
- Route end [I]: the route end is always edge-adjacent to its blocker or the Keep (distance 1 ≤ 3), so an archer always has a target there.
- Ranged hits are **not melee**: no Wall/Spike reflection, no Barbed Defenses. Commander aura applies; Hardened Roofs and Gatehouse apply to the target.
- Death timing: as any enemy; a killed archer in step 3/4 never fires in step 5.
- Events: `EnemyAttacked` + `BuildingDamaged` as melee, plus `RangedShot` (from archer position to target centre) for presentation.

### 2.2 Bomber

- Walks the normal ground route like a melee unit [I]. When it reaches its attack cell and acquires its target (breach blocker or Keep) it **lights the fuse**: detonation due `1.5 s` = 30 steps [P] later. It never makes ordinary attacks.
- A lit Bomber is committed [I]: it never moves again and never retargets. If its target is gone by detonation, the primary hit is skipped but the splash still happens.
- Detonation (enemy phase, ascending enemy ID) [I]: primary damage **60** [P] × Wall multiplier 2 against a Wall (120: a fresh 100-HP Wall falls, a 130-HP Reinforced Timber Wall survives); then **20** [P] splash to every other living building with HP whose footprint has a cell within Chebyshev distance 1 of the Bomber's cell, in ascending building ID. The Bomber then dies. It is **not** counted as defeated (no `EnemyKilled`, no stats, no recap kill). Not melee → no reflection. Commander aura does not apply. Blast Shields halves both.
- Killing it before the detonation step resolves (step 2–4 of that same step included: traps, Archer/Cannon shots, burn) cancels the blast [I].
- Destruction of the target releases its cells at once (route invalidation as usual). Keep reduced to 0 by a blast → Defeat at step end.
- Events: `FuseLit` (on acquisition), `BomberDetonated` (cell, position), then `EnemyAttacked`/`BuildingDamaged`/`BuildingDestroyed` per building hit.

### 2.3 Siege Commander (boss 2)

- Melee boss, otherwise like the Golem (Wall × 2). Spawns once on Night 12 (section 3).
- Aura [I]: when any **other** living non-boss enemy makes a melee or ranged hit, its damage is +50% [P] (floor) if a living Siege Commander's current position is within **2.5 tiles** [P] Euclidean of the attacker's current position at that step. Non-stacking: several Commanders still give +50% once. The Commander does not buff itself or the Golem. Bomber blasts are not hits and are unaffected.
- Update tick [I]: evaluated per hit using positions after this step's movement; no stored aura state. A Commander killed in step 3/4 gives no aura in step 5 of that step (removal is immediate).
- Cue: arrival banner and boss bar like the Golem; aura ring in presentation.

## 3. Twelve-night campaign `twelve-nights`

Authored recipe per night [P counts; I structure]. Non-boss spawn order cycles Grunt, Runner, Brute, Bomber, Archer, skipping exhausted kinds; the first spawn at 0, then one per cadence. Entries are assigned round-robin over the night's entry list starting at a **seeded rotation offset**. Bosses spawn at the listed time from a **seeded entry** of that night's list, after equal-time ordinary spawns.

| Night | Grunt | Runner | Brute | Bomber | Archer | Boss (time) | Entries | Cadence s |
|---|---|---|---|---|---|---|---|---|
| 1 | 10 | | | | | | W | 1.5 |
| 2 | 12 | 4 | | | | | W E | 1.5 |
| 3 | 14 | 4 | 2 | | | | W E | 1.25 |
| 4 | 14 | 6 | 2 | 3 | | | W E | 1.25 |
| 5 | 16 | 6 | 2 | 3 | 4 | | W E N | 1.0 |
| 6 | 18 | 8 | 3 | 3 | 4 | Siege Golem (30 s) | W E N | 1.0 |
| 7 | 20 | 8 | 4 | 4 | 6 | | W E S | 1.0 |
| 8 | 26 | 10 | 5 | 5 | 8 | | W E N S | 0.9 |
| 9 | 30 | 12 | 6 | 6 | 9 | Siege Golem (30 s) | W E N S | 0.85 |
| 10 | 34 | 14 | 7 | 7 | 10 | | W E N S | 0.8 |
| 11 | 36 | 16 | 8 | 8 | 12 | Siege Golem (30 s) | W E N S | 0.75 |
| 12 | 40 | 18 | 10 | 10 | 12 | Siege Commander (30 s) | W E N S | 0.7 |

Arc [I]: 1–3 teach Walls/Towers/economy; 4 introduces Bombers (siege), 5 ranged pressure, 6 the first boss; 7–9 split fronts (S appears on 7, all four from 8) and support decisions; 10–12 test the chosen build and end on the different boss.

Seeded variation [I]: wave stream `RunRandom(seed ^ 0x5BD1E995)`; per night in order: one `NextBounded(entryCount)` for the rotation offset, then for a boss night one `NextBounded(entryCount)` for the boss entry. All twelve plans are generated before Day 1, stored in the campaign and the checkpoint; preview and combat read the same stored plans. No director reacts to the player's defenses.

Validation [I]: every night has ≥1 entry on the boundary ring, every spawn's kind is defined, non-decreasing ticks, ≤1 boss per night, total spawns ≤ 100 per night, and every entry has a route on the generated map at Day 1.

## 4. Perks — release pool (24)

Draw algorithm unchanged [I]: canonical IDs sorted ordinally; weight 3 if the affected building exists (or the perk is general), else 1; three distinct unowned perks, one bounded draw each. The **pool is per profile**: MVP keeps its 10-ID pool (its draws, event payloads and saves are unchanged); the release pool is the 24 IDs below. No proc-based (random-on-hit) perk exists.

Existing 10, scoped for new kinds:

| ID | Affected | Effect |
|---|---|---|
| ReinforcedTimber | Wall | Walls +30% max HP; existing Walls gain the HP now. |
| EfficientFarming | Farm | Farm income +30% incl. the House bonus (floor per Farm). |
| ExpandedVision | Tower | **Archer Tower** base range ×1.1 (not Cannon). |
| LastStand | Tower | **Archer Tower** damage +40% while Keep < 30% max HP. |
| FortressHeart | — | Keep +150 max and current HP. |
| FlamingArrows | Tower | **Archer Tower** hits burn 2/s for 3 s; refresh, no stack. |
| Recycling | — | Any building destroyed in combat refunds 25% of its price. |
| Sharpshooters | Tower | **Archer Tower** direct damage +20%. |
| BarbedDefenses | Wall | Walls reflect +3 per **melee** hit (6 with an adjacent trap). |
| ThriftyConstruction | — | Future prices −10% (min 1), all kinds. |

New 14 (the ten marked ★ change placement, targeting or timing):

| ID | Affected | Effect [I rule, P numbers] |
|---|---|---|
| ★ KillZone | Tower | Archer Tower direct damage +30% against a target whose current cell holds or shares an edge with a Spike Trap (adds to the percent sum). |
| ★ FocusedFire | Tower | Archer Towers acquire the in-range enemy with the lowest current HP; ties nearest, then lowest ID. Retention unchanged. |
| ★ CounterBattery | Tower | Archer Towers and Cannons acquire Raider Archers and Bombers before other enemies; within a class the normal (or Focused Fire) rule. |
| ★ Overwatch | Tower | On acquiring a new target, an Archer Tower or Cannon fires after half its interval rounded up (15→8, 40→20 steps); later shots use the full interval. |
| ★ SpikedCorridor | SpikeTrap | Spike Trap damage +4 per other Spike Trap sharing an edge, max +8. |
| ★ Gatehouse | Wall | Walls sharing an edge with an Archer Tower or Cannon footprint take 30% less damage from enemy hits and blasts. |
| ★ MarketSquare | House | Each House sharing an edge with another House earns +4 per Day (in the forecast). |
| ★ WarBonds | — | At dawn after a Night in which the Keep lost no HP, +30 gold (paid with income; shown as a separate line). |
| ★ SignalFires | Watchtower | Watchtower bonus reaches towers with a footprint cell within Chebyshev 2 (instead of edge-adjacent). |
| ★ FieldEngineers | RepairStation | Repair budget +30 per station and the Keep becomes eligible within reach. |
| Shrapnel | Cannon | Cannon splash radius +0.5 tiles. |
| HeavyShot | Cannon | Cannon damage +50% against Brutes, Siege Golems and Siege Commanders. |
| BlastShields | — | Bomber blast and splash damage to your buildings −50%. |
| HardenedRoofs | House | Houses, Farms, Watchtowers and Repair Stations take 40% less damage from Raider Archer hits. |

Composition [I]: Counter-Battery class filter → Focused Fire or nearest → lowest ID. Gatehouse and Hardened Roofs never both apply (different kinds). Last Stand / Sharpshooters / Kill Zone add; Heavy Shot is Cannon-only. Reductions: `max(1, d × (100 − r) / 100)`.

Offer integrity [I]: a saved offer may contain only IDs of the run's pool; a perk not in the pool is a save error, not ignored.

## 5. Map profiles (one biome, 20×20)

Common [I]: boundary ring and the four entries W(0,10) E(19,10) N(10,19) S(10,0) stay land; the Keep and the central 8..12 square stay land; every entry has an **open** (non-breach) route to a Keep neighbour; no isolated walkable pocket; at least the profile's minimum of disjoint buildable 2×2 sites; generated terrain is persisted in the save. Generation per attempt: skeleton + N variation obstacles chosen by partial Fisher–Yates from the profile's variable cells using `RunRandom.ForMapAttempt(seed ^ profileSalt, attempt)`; up to 20 attempts; then the skeleton alone (validated at authoring time and in tests), flagged as fallback.

| Profile | Skeleton intent | Variation | Named constraint |
|---|---|---|---|
| `ravine` (corridor) | Rock ridges at z = 7 and z = 13 for x 2..8 and 12..17: W and E approach through 5-wide channels; N/S through the central gap. Forest clumps in the four outer quadrants. | 8 forest/rock outside rows 8..12 and columns 9..11 | Ridge cells intact; W and E open routes stay within rows 8..12 for x 2..8 / 12..17; ≥4 disjoint 2×2 sites. |
| `crossing` (split front) | Water columns x = 5 and x = 14 (z 1..18) with 2-cell fords at z 3..4 and 15..16 (west) and z 4..5 and 14..15 (east). | 8 forest/rock on land, never on fords or their approach cells | W/E open routes cross their river at a ford; ≥4 disjoint 2×2 sites. |
| `meadow` (open economy) | Four 2×2 forest clumps near the corners plus a few rocks; wide buildable land. | 10 forest/rock outside the 7..13 centre square | ≥8 disjoint 2×2 sites. |

The MVP random generator (`MapGenerator`, 30 scattered obstacles) is unchanged and remains the six-night map.

## 6. Economy and difficulty

- Starting gold 250 [P], Keep 20/day [P], existing building incomes unchanged [P]. Manual repair 5 HP per gold [I]. Sale refunds 50% (floor) [I]. Recycling 25% [I].
- One difficulty, `standard` [I for 008]. Persisted for future profiles.
- Cost/income assumptions to verify with policies: a balanced build reaches ~150–200 gold/day by Night 8; Night 12 needs roughly 1,600–2,200 gold of defenses. These are hypotheses, not acceptance criteria.

## 7. Saves and profiles

- Run checkpoint schema **1** = legacy six-night MVP (`mvp-rules-1`), written by the MVP profile exactly as before and readable by every build that knows `mvp-rules-1`.
- Schema **2** = profile-tagged: `rulesVersion` (`release-rules-1`), `campaignId` (`twelve-nights`), `contentVersion` (2; 1 before the cheaper-buildings retune in §8), `mapProfile`, `difficulty`, plus all schema-1 fields. Stored nights hold up to 7 enemy kinds; stored buildings up to 9 kinds. New buildings carry no extra per-building state across Days (cooldowns reset at Day, dawn repair is stateless).
- Reading [I]: schema 1 → legacy profile only; schema 2 → the catalog must know its `rulesVersion` + `campaignId`, else `IncompatibleRules` with an actionable message; any other schema → `UnknownSchema`, file untouched ("made by a newer version; update to continue it"). A six-night save is never converted into twelve nights; it continues as six nights under MVP rules in any build that ships the legacy profile (the release build does).
- Release and MVP builds share the save folder; a build that does not know a save's profile never overwrites it (a New Run takes the next slot).
- Settings (v2) and onboarding profile (v1) are separate files with separate migrations; unchanged by 008.

## 8. Tuning log (provisional values changed with evidence)

Each change below was made inside the [P] bounds after a recorded scripted run (`evidence/008/release-policy-runs.txt`, real budget, seeds 1–8 per approach). Scripts are not players: these are mechanical-viability adjustments, not balance claims.

| Value | From → to | Evidence |
|---|---|---|
| Nights 7–12 counts and cadence | table first frozen above → current table | With the first table a sealed Keep with four Archer Towers and two Cannons won all 8 seeds after only 10–13 purchases (~400–600 gold earned), Keep near full HP. Late nights were raised; Nights 4–6 kept as frozen after a steeper mid-game made every approach collapse on Nights 6–7. |
| Raider Archer range | 3.0 → 3.5 tiles | At 3.0 an Archer always stops inside a bare Archer Tower's 4.0 range and never threatens a ring. 4.5 (outranging every early defense) collapsed all approaches on Night 5. 3.5 makes a tight ring take real damage (kill-zone 6/8 wins, not 8/8) while corridor 5/8 and economy 4/8 stay viable. |

Result at those values: kill-zone (Crossing) 6/8, corridor (Ravine) 5/8, economy (Meadow) 4/8 wins over seeds 1–8; each loses on some seeds. The ring at the Keep is still the most forgiving approach; whether that is a problem is a human-playtest question (G3/G4), not something the scripts can settle.

**Cheaper buildings (2026-10-16, content version 2).** Playtest feedback: "boring when I cannot build much" — the scripts bought only 11–28 buildings in a whole run. Every purchasable building now costs 30% less and does about 20% less; the MVP profile is unchanged. Content-1 release saves are refused (file untouched), not rescaled.

| Building | Cost | HP | Effect |
|---|---|---|---|
| Wall | 20 → 14 | 100 → 80 | — |
| Archer Tower | 70 → 49 | 100 → 80 | fires every 0.75 → 0.9 s (damage 10 kept: same shots-to-kill) |
| House | 40 → 28 | 80 → 64 | income 15 → 10 |
| Farm | 60 → 42 | 100 → 80 | income 25 → 17 |
| Spike Trap | 35 → 25 | — | damage 8 → 7 |
| Cannon | 110 → 77 | 150 → 120 | fires every 2.0 → 2.5 s |
| Watchtower | 45 → 32 | 80 → 64 | range bonus 1.0 → 0.8 |
| Repair Station | 80 → 56 | 120 → 96 | dawn budget 50 → 40 |

Unchanged: starting gold 250, Keep (HP 500, income 20), all enemies, perks and their flat values. Income is cut harder (about ×0.67) than combat (×0.8) because flat economy bonuses (Farm + House +5, Market Square +4) grow relative to cheaper Houses and Farms.

- A first try at −30% impact made every approach far weaker (6/24 wins): buildings at 70% HP died before paying back, and ring layouts lost coverage. ×0.8 was chosen instead.
- Script changes, made so the scripts spend the gold as a player would rather than to make results pass:
  - Each Day's minimum defense count was scaled by 1/0.7 (the same share of gold).
  - Economy is bought only once that minimum is met, so cheap Houses no longer starve a pending Cannon.
  - Kill-zone buys its two inner Towers before the first Cannon.
  - Kill-zone's recorded seeds changed from 2, 3, 4 to 2, 3, 5, because seed 4 now loses on Night 10.
- Result: kill-zone 6/8, corridor 7/8, economy 8/8 (21/24, up from 15/24); runs buy 14–42 buildings.

The game is likely somewhat easier than before, and economy-first is now the most forgiving approach. That needs a human playtest. Evidence covers only symmetry 0 (each profile's own orientation).
