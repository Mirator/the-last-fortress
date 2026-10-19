# Game rules

Version 1.0, 2026-10-19. Status: **implemented**; this is the description of the game as it is now. It consolidates the earlier milestone specs (prototype, six-night MVP, playtest improvements, release rules; see `archive/`) with every value taken from the current code and `Data/ReleaseDefinitions.asset`.

How to read and change it:

- The release profile `release-rules-1` is the game. Sections 1-14 describe it. Section 15 lists only how the legacy six-night profile differs.
- **[I]** marks an invariant rule: changing it changes behaviour of existing runs and needs a new rules version. **[P]** marks provisional tuning: it may change after recorded evidence (scripted policy runs or playtests), with the change noted in section 16. Untagged statements are rules.
- A feature spec (for example [003-third-person-hero.md](003-third-person-hero.md)) proposes changes. When it is implemented, merge its rules into this document and move the feature spec to `archive/`.
- Names in `code font` match the code (`Definitions.cs`, `RunSimulation.cs`, `PerkRules.cs`, `EconomyRules.cs`, `ReleaseCampaign.cs`, `MapProfiles.cs`).

## 1. Profiles

| Profile | Rules version / campaign | Where | Status |
|---|---|---|---|
| Release | `release-rules-1` / `twelve-nights`, content version 2 | `Release.unity`, `Builds/Release/` | The game |
| Six-night MVP | `mvp-rules-1` / six nights | `Mvp.unity`; also kept in the release build so old saves continue | Frozen legacy profile (section 15) |
| Navigation prototype | one wave, no campaign | `NavigationPrototype.unity` | Development and test scene only; rules in `archive/002-navigation-prototype.md` |

A save names its profile. A build restores only profiles it ships and never converts a run from one profile to another [I].

## 2. Board and coordinates

- 20×20 grid, cells `(x, z)` with 0 ≤ x, z ≤ 19 [I]. One cell is one world unit; a cell centre is `(x + 0.5, 0, z + 0.5)`. World-to-grid floors X and Z; points outside the grid are rejected, never clamped.
- The outermost ring is reserved: enemies may walk on it, nothing may be built on it [I]. Entries are the side midpoints West `(0,10)`, East `(19,10)`, North `(10,19)`, South `(10,0)`, turned with the map's symmetry (section 11).
- The Keep is 1×1 at `(10,10)` [I]. Its larger mesh never implies extra occupied cells. Enemies attack it from a cardinally adjacent cell and never enter it.
- Terrain types: land (walkable, buildable), forest, rock and water (neither walkable nor buildable, never attackable or harvestable). Fords are walkable land crossing a river. Terrain meshes never create hidden blockers.
- Units never block each other and do not reserve cells; visual overlap is allowed. There is no collision avoidance.

## 3. Run flow

States: `Day(n)`, `Night(n)`, `Reward`, `Victory`, `Defeat`. Pause is a separate flag, valid only during Night.

1. **New Run** picks a seed (random, or entered as an unsigned decimal) and a map profile (chosen on the title screen, or picked from the seed). It generates the map and all twelve night plans before Day 1, and saves Day 1. Start: 250 gold [P], Keep at full HP, no income on Day 1 [I].
2. **Day** is untimed. Build, sell, repair and rotate are allowed only in Day [I]. The next night's composition, entries and routes are shown.
3. **Start Night** saves the Day checkpoint, then enters Night. Repeated clicks cannot start a second wave.
4. **Night** runs the fixed-step simulation (section 4). Construction is disabled, including while paused.
5. Keep at 0 HP → **Defeat** on that step, regardless of remaining enemies [I]. Otherwise the night is cleared only when every scheduled spawn has been released and no enemy is alive. A temporary empty board between spawns is not a clear.
6. Night 12 cleared → **Victory**. Nights 1-11 cleared → **Reward**: one frozen offer of three perks; choosing one applies it, enters `Day(n+1)`, runs dawn repairs, pays income once, then saves.
7. Victory and Defeat end the run, record the summary and clear Continue for it.

**Pause and speed.** Pause stops spawns, movement, cooldowns, damage and night time; camera, selection, inspection, menu and Restart stay usable. Night speed is 1×, 2× or 3×: it runs more fixed steps per real second and never changes outcomes [I]. Speed is a session choice, not saved with a run. Choosing a speed while paused resumes.

**Restart** retries the same seed from Day 1 (asks to confirm once the run has progressed). **Continue** resumes the latest Day checkpoint: quitting during Night or Reward returns to the start of that night. There is no mid-combat save [I].

**Run summary and night recap.** The summary shows nights cleared, enemies defeated, buildings purchased, gold earned (starting gold excluded) and synergy counts. Reward and Defeat screens show the night just played: enemies defeated, Keep HP lost to damage (overkill excluded) and buildings destroyed in combat.

## 4. Simulation

- Fixed step 0.05 s [I]. All intervals are whole steps; all speeds are whole 1/1200-tile units per step [I]. Results never depend on frame rate.
- Step order [I]:
  1. Release due spawns (the time-zero spawn is released on entering Night, before the first movement).
  2. Route invalidation and movement; Spike Trap entry damage.
  3. Building attacks (Archer Tower, Cannon) in ascending building ID.
  4. Due burn ticks on surviving enemies.
  5. Enemy actions in ascending enemy ID: melee hits (with immediate, non-recursive reflection), ranged hits, Bomber detonations.
  6. Dead-enemy cleanup.
  7. Keep at 0 HP → Defeat, evaluated before the cleared-night check, so a same-step final kill never overrides defeat.
- Damage is immediate and authoritative in the simulation [I]. Projectiles, explosions and auras are presentation only; a visual arrow arriving after its target died does nothing.
- HP clamps to `[0, max]`. Each death resolves once; dead entities neither act nor remain targets. A destroyed building releases its cells immediately and leaves buildable land with no rubble.
- Attack timing [I]: a newly acquired target's first hit is due one full interval after acquisition (half for towers with Overwatch, section 10); later hits every interval. Losing or changing target resets the timer. No stored attacks or catch-up after pause.
- Determinism [I]: the same seed, rules version and player decisions reproduce the same maps, waves, offers and outcomes. Map, wave, reward and symmetry/profile choices use separate seeded streams (`RunRandom`), so one never consumes another's numbers.
- Living-enemy cap 100 [I]; due spawns beyond it wait FIFO and keep the night from clearing.

## 5. Routing

Owner rule: **any open route beats breaching, regardless of length** [I]. There is no walk-versus-breach time comparison.

1. At a cell centre, search cardinal neighbours (no diagonals, no corner cutting) for a shortest open route to any free cardinal neighbour of the Keep. Solid buildings and non-walkable terrain cannot be crossed.
2. If an open route exists, follow it and ignore nearby buildings. At a Keep neighbour, attack the Keep.
3. Otherwise use the breach graph: a land step costs 1; entering a destructible solid building cell costs 6 [I]. The Keep and terrain are never crossed. A building occupying a Keep neighbour is an eligible breach goal.
4. Follow the breach route until cardinally adjacent to its first solid building, attack it, never enter its cells. A multi-cell building is attacked from any free cell edge-adjacent to its footprint and is damaged once per hit. When the blocker is destroyed, plan again, open search first.
5. Tie-breaks [I]: minimum cost, then goal `(z, x)` ascending; neighbours in order N, E, S, W; frontier ordered by cost, then `(z, x)`, then insertion; the first predecessor wins on equal cost.
6. Every occupancy change increments `GridState.Revision`; cached routes record it [I]. An enemy moving between centres finishes its segment, then replans at the next centre; a stationary enemy replans on the next step. A dead target is dropped before another hit.
7. Every enemy, bosses and ranged units included, walks this ground route. No enemy flies, tunnels or ignores obstacles [I].
8. A map where an entry has no route at all is invalid: Start Night is never enabled on it, and generation rejects it.

**Route preview** [I]: during Day the entries' current routes are shown, and a valid construction ghost shows the candidate route, computed by the same planner on a temporary overlay that never changes live state, gold, revision or caches. A breach route is drawn to its first blocker, marked with a BREACH label; the rest may be dashed. On an unchanged map, actual routes match the preview.

## 6. Construction and economy

- **Placement** [I]: every footprint cell must be in bounds, outside the reserved ring, buildable land with no solid building and no trap. Validation repeats on click; a rejected placement changes neither gold nor occupancy. Placement is atomic: one charge, one building. Sealing the Keep is legal and shows a breach warning, not a rejection. A tool stays active after placing; right click or Esc cancels.
- **Footprints** [I]: 2×2 for Farm, Cannon and Repair Station, anchored at the lowest `(x, z)` cell; all others 1×1. A 2×2 building is one entity: HP, damage, sale, refund and destruction happen once.
- **Rotation**: Tower, House, Farm, Cannon, Watchtower and Repair Station can be rotated in 90° steps; it is cosmetic and never changes footprint, routing or combat [I]. Walls connect to neighbouring Walls automatically.
- **Price** paid is stored on the building; refunds use it, never the current price [I].
- **Sale**: refunds `floor(price × 0.5)` once [I]. The Keep cannot be sold. Combat destruction refunds nothing unless Recycling is owned.
- **Manual repair**: on a selected building including the Keep, restores as much missing HP as gold allows at 5 HP per gold, cost `ceil(restored / 5)` [I]. The quote shows HP and price first. Traps cannot be repaired. No free or automatic repair except dawn repair (Repair Station).
- **Income** [I]: paid once on entering each Day from Day 2, after the perk choice and dawn repair. Keep 20 [P] + each surviving House and Farm. Buildings destroyed during the night earn nothing. No kill rewards, no debt. Forecast and payment use the same calculation.
  - Farm: `floor((17 + adjacency) × (100 + farming%) / 100)` per Farm, where adjacency is +5 [P] if at least one surviving House shares an edge (once per Farm, not per House).
  - House: 10 [P], plus Market Square bonus when owned.
- **Adjacency** [I]: two buildings are adjacent only when cells share an edge; corners do not count. Count unique building pairs, not shared edges. Recompute on placement, sale, destruction and perk changes.
- **Wall + Spike Trap synergy**: a Wall with at least one edge-adjacent trap reflects 3 damage [P] to the attacker of each melee hit it receives, once regardless of trap count. Reflection never triggers another reflection. A Wall destroyed by a hit still reflects that hit; an enemy killed by reflection does not attack again.

## 7. Buildings

Current values [P], from `Data/ReleaseDefinitions.asset`.

| Kind | Footprint | Cost | HP | Solid | Effect |
|---|---|---:|---:|---|---|
| Keep | 1×1 | — | 500 | yes | 20 gold/day; defeat at 0 HP; cannot be sold |
| Wall | 1×1 | 14 | 80 | yes | Blocks movement |
| Archer Tower | 1×1 | 49 | 80 | yes | 10 damage every 0.9 s, range 4.0 |
| House | 1×1 | 28 | 64 | yes | 10 gold/day |
| Farm | 2×2 | 42 | 80 | yes | 17 gold/day, +5 with an adjacent House |
| Spike Trap | 1×1 floor | 25 | — | no | 7 damage on entry |
| Cannon | 2×2 | 77 | 120 | yes | 20 damage every 2.5 s, range 4.5, splash 1.25 |
| Watchtower | 1×1 | 32 | 64 | yes | +0.8 range to adjacent towers |
| Repair Station | 2×2 | 56 | 96 | yes | 40 HP dawn repair within 2 cells |

**Building centre** [I]: anchor + (size/2, size/2): the cell centre for 1×1, the shared corner of the four cells for 2×2.

**Archer Tower.** Range is inclusive Euclidean distance (1e-9 tolerance) from the building centre to the enemy's current position; Walls never block shots [I]. Keeps its target while alive and in range; otherwise acquires the nearest enemy in range (squared distance), ties to the lowest enemy ID [I]. Perks may change damage, range and acquisition (section 10).

**Spike Trap.** A walkable floor layer on a buildable cell with no solid building [I]. Damages each enemy once on entering its cell; standing on it does nothing more. No HP, charges, cooldown or targeting. Leaving and re-entering hits again; routing never loops to farm trap damage.

**Cannon.** Targeting as the Archer Tower [I]. Each shot damages the primary target and every other living enemy within the splash radius of the primary's position, inclusive, once per enemy per shot, in ascending enemy ID [I]. No friendly fire. A Cannon shot is not an Archer Tower hit: Archer Tower perks do not apply.

**Watchtower.** Every living Archer Tower and Cannon with a footprint cell sharing an edge with the Watchtower gets the range bonus; with Signal Fires, any footprint cell within Chebyshev distance 2 [I]. A tower is buffed at most once, however many Watchtowers qualify [I]. Effective range = `base × 1.1 (Expanded Vision, Archer Tower only) + 0.8 if buffed`. Buff membership is recomputed per grid revision. A buff change never resets cooldowns; a target pushed out of range by a shrink is dropped and the tower reacquires with a fresh interval (full, or half with Overwatch) [I]. Range rings and ghost previews show effective range and the towers a Watchtower would buff.

**Repair Station** [I]. On entering Day n ≥ 2 (after the perk choice, before income), each living station in ascending ID spends its budget (40 HP [P], +30 with Field Engineers) on eligible buildings: alive, with HP, not full, not itself, not the Keep (unless Field Engineers), with a footprint cell within Chebyshev distance 2 [P] of the station's footprint. Lowest HP fraction first (compare `hp × otherMax`), ties lowest ID; each gets `min(missing, remaining)`. Later stations see earlier repairs. Runs once per dawn, guarded with income; a reloaded checkpoint already holds the repaired HP. No combat healing, no rebuilding destroyed buildings.

## 8. Enemies

Current values [P].

| Kind | HP | Speed tiles/s | Damage / interval | Wall × | Role |
|---|---:|---:|---|---:|---|
| Grunt | 30 | 1.0 | 10 / 1.0 s | 1 | Baseline melee |
| Runner | 20 | 1.75 | 6 / 0.75 s | 1 | Fast |
| Brute | 150 | 0.6 | 20 / 1.5 s | 2 | Heavy |
| Raider Archer | 35 | 0.9 | 6 / 1.5 s, range 3.5 | 1 | Ranged |
| Bomber | 40 | 1.0 | Blast 60, splash 20, fuse 1.5 s | 2 | Breacher |
| Siege Golem (boss) | 800 | 0.4 | 40 / 2.0 s | 2 | Boss on Nights 6, 9, 11 |
| Siege Commander (boss) | 1000 | 0.4 | 30 / 2.0 s | 2 | Final boss, Night 12; aura |

- Wall × multiplies damage against Walls only. It never overrides route choice: heavy enemies still take any open route.
- No armour, resistances or flying. No melee splash.

**Raider Archer.** Walks its normal route and only ever stops on a cell centre of it; it never paths to firing positions [I]. At each fresh cell-centre decision it looks for a target: living solid buildings (Keep included, traps excluded) with a footprint cell centre within 3.5 tiles, no line of sight needed [I]. Priority [I]: (1) House, Farm; (2) Watchtower, Repair Station; (3) Archer Tower, Cannon; (4) Wall; (5) Keep; then nearest footprint-cell centre; then lowest building ID. Found → stops and targets it. Sticky while the target is alive and in range; then reacquires at its next decision (fresh full interval) or resumes its route. Ranged hits are not melee: no reflection, no Barbed Defenses.

**Bomber** [I]. Walks like a melee unit. At its attack cell it acquires its target (breach blocker or Keep) and lights its fuse; it never moves or retargets again and never makes ordinary attacks. On detonation (enemy phase): the primary blast hits its target if still alive (×2 against a Wall), then splash hits every other living building with HP that has a cell within Chebyshev distance 1 of the Bomber's cell, in ascending ID. The Bomber then dies without counting as defeated. Killing it before the detonation resolves (traps, shots or burn in the same step included) cancels the blast. Blasts are not hits: no reflection, no Commander aura.

**Siege Commander aura** [I]. When another living non-boss enemy makes a melee or ranged hit, its damage is +50% [P] (floored) if a living Siege Commander is within 2.5 tiles [P] of the attacker at that step. Several Commanders still give +50% once. No stored aura state; a Commander killed earlier in the same step gives no aura.

**Damage modifiers** [I]: percentage bonuses on one damage value add against the base and floor once: `base × (100 + sum) / 100`. Reductions apply after bonuses, one after another in the order Gatehouse, Hardened Roofs, Blast Shields: `max(1, d × (100 − r) / 100)` for a positive hit.

## 9. Campaign and waves

Twelve authored nights [P counts; I structure].

| Night | Grunt | Runner | Brute | Bomber | Archer | Boss (at 30 s) | Entries | Cadence s |
|---|---:|---:|---:|---:|---:|---|---|---:|
| 1 | 10 | | | | | | W | 1.5 |
| 2 | 12 | 4 | | | | | W E | 1.5 |
| 3 | 14 | 4 | 2 | | | | W E | 1.25 |
| 4 | 14 | 6 | 2 | 3 | | | W E | 1.25 |
| 5 | 16 | 6 | 2 | 3 | 4 | | W E N | 1.0 |
| 6 | 18 | 8 | 3 | 3 | 4 | Siege Golem | W E N | 1.0 |
| 7 | 20 | 8 | 4 | 4 | 6 | | W E S | 1.0 |
| 8 | 26 | 10 | 5 | 5 | 8 | | W E N S | 0.9 |
| 9 | 30 | 12 | 6 | 6 | 9 | Siege Golem | W E N S | 0.85 |
| 10 | 34 | 14 | 7 | 7 | 10 | | W E N S | 0.8 |
| 11 | 36 | 16 | 8 | 8 | 12 | Siege Golem | W E N S | 0.75 |
| 12 | 40 | 18 | 10 | 10 | 12 | Siege Commander | W E N S | 0.7 |

- Arc: Nights 1-3 teach Walls, Towers and economy; 4 introduces Bombers, 5 ranged pressure, 6 the first boss; 7-9 split fronts (South from 7, all four from 8); 10-12 test the build and end on the second boss.
- Spawn list [I]: ordinary spawns cycle Grunt, Runner, Brute, Bomber, Archer, skipping exhausted kinds; first at time 0, then one per cadence. Entries are assigned round-robin from a seeded rotation offset. The boss spawns at its time from a seeded entry, after equal-time ordinary spawns.
- Seeded variation [I]: wave stream `RunRandom(seed ^ 0x5BD1E995)`; per night, one draw for the rotation offset, then on a boss night one draw for the boss entry. Entries are turned with the map symmetry. All plans are stored in the run and the checkpoint; preview and combat read the same plan. No director reacts to the fortress.
- Validation [I]: each night has entries on the boundary ring, only defined kinds, non-decreasing spawn times, at most one boss and at most 100 spawns; every entry has a route on the generated map at Day 1.
- The Day UI shows counts by kind, active entries (new sides highlighted), first and last spawn times and a boss warning. Enemies remaining = not yet spawned + alive.

## 10. Perks

- After each Nights 1-11, offer three distinct unowned perks from the 24-perk pool; choose one [I]. Each perk can be taken once per run; all are available from the start. No perk is random-on-hit.
- Draw [I]: canonical IDs in ordinal order; weight 3 if the affected building kind is currently owned or the perk is general, else 1; three bounded draws without replacement from the dedicated reward stream. Perks for unowned kinds stay eligible and are labelled.
- Offer integrity [I]: an offer is generated once per cleared night and stored; reloading cannot reroll it or choose twice. A saved offer may hold only IDs of the run's pool.
- Perks affect existing and future eligible buildings unless stated otherwise. Recomputing a max-HP modifier never grants current HP twice.
- Reward screen values are effective values, and an income-changing perk shows the next dawn payment it would produce.

| Perk | Affects | Effect |
|---|---|---|
| ReinforcedTimber | Wall | Walls +30% max HP; existing Walls gain the same HP now |
| EfficientFarming | Farm | Farm income +30%, House bonus included (floor per Farm) |
| ExpandedVision | Tower | Archer Tower base range ×1.1 |
| LastStand | Tower | Archer Tower damage +40% while Keep HP < 30% of max |
| FortressHeart | — | Keep +150 max and current HP |
| FlamingArrows | Tower | Archer Tower hits burn 2 damage per 1 s for 3 s; a hit refreshes expiry but not the tick cadence; no stacking; no ticks after death |
| Recycling | — | A building destroyed in combat refunds 25% of its price, once; not on sale |
| Sharpshooters | Tower | Archer Tower direct damage +20% (not burn or reflection) |
| BarbedDefenses | Wall | Walls reflect +3 per melee hit (6 with the trap synergy) |
| ThriftyConstruction | — | Future prices −10%, `max(1, floor(price × 0.9))`; existing prices unchanged |
| KillZone | Tower | Archer Tower damage +30% against a target whose cell holds or shares an edge with a Spike Trap |
| FocusedFire | Tower | Archer Towers acquire the lowest-HP enemy in range; ties nearest, then lowest ID |
| CounterBattery | Tower | Archer Towers and Cannons acquire Raider Archers and Bombers first |
| Overwatch | Tower | On a new target, an Archer Tower or Cannon fires after half its interval rounded up (18→9, 50→25 steps); then full interval |
| SpikedCorridor | SpikeTrap | Trap damage +4 per edge-adjacent trap, max +8 |
| Gatehouse | Wall | Walls sharing an edge with an Archer Tower or Cannon take 30% less damage from hits and blasts |
| MarketSquare | House | Each House sharing an edge with another House earns +4 per Day |
| WarBonds | — | +30 gold at dawn after a night where the Keep lost no HP (separate income line) |
| SignalFires | Watchtower | Watchtower bonus reaches towers within Chebyshev 2 |
| FieldEngineers | RepairStation | Repair budget +30 per station; the Keep becomes eligible |
| Shrapnel | Cannon | Cannon splash radius +0.5 |
| HeavyShot | Cannon | Cannon damage +50% against Brutes, Siege Golems and Siege Commanders |
| BlastShields | — | Bomber blast and splash damage to buildings −50% |
| HardenedRoofs | House | Houses, Farms, Watchtowers and Repair Stations take 40% less damage from Raider Archer hits |

Composition [I]: Counter-Battery class filter → Focused Fire or nearest → lowest ID. Last Stand, Sharpshooters and Kill Zone add together; Heavy Shot is Cannon only. Percentages are [P]; the rules are [I].

## 11. Maps

Common rules [I] for every profile: the boundary ring and the four entries stay land; the Keep and the central 8..12 square stay land; every entry has an open (non-breach) route to a Keep neighbour; no isolated walkable pocket; at least the profile's minimum of disjoint buildable 2×2 sites. The generated terrain is stored in the save.

| Profile | Skeleton | Variation | Named constraint |
|---|---|---|---|
| `ravine` (corridors) | Rock ridges at z = 7 and z = 13 for x 2..8 and 12..17, so West and East approach through 5-wide channels; North/South through the central gap. Forest clumps in the outer quadrants. | 8 forest/rock outside rows 8..12 and columns 9..11 | Ridges intact; West/East open routes stay within rows 8..12 for x 2..8 / 12..17; ≥4 2×2 sites |
| `crossing` (split fronts) | Rivers at x = 5 and x = 14 (z 1..18) with 2-cell fords at z 3..4 and 15..16 (west), z 4..5 and 14..15 (east); forest at (2,8), (2,12), (17,8), (17,12). | 8 forest/rock on land, never on fords or their approaches | West/East open routes cross their river at a ford; ≥4 2×2 sites |
| `meadow` (open economy) | Four 2×2 forest clumps near the corners; rocks at (6,15) and (13,4). | 10 forest/rock outside the 7..13 centre square | ≥8 2×2 sites |

- **Generation** [I]: skeleton plus N variation cells by partial Fisher-Yates over the profile's variable cells (alternating forest, rock, forest, ...), from `RunRandom.ForMapAttempt(seed ^ profileSalt, attempt)`; up to 20 attempts, validated with the real route planner on the board as played; then the skeleton alone (not re-validated), flagged as a fallback.
- **Symmetry** [I]: each seed shows its profile in one of 8 symmetries of the square (4 rotations, each optionally mirrored), drawn from a separate stream. Night entries turn with it (snapped to side midpoints), so every orientation keeps the profile's promise. Validation runs on the transformed board.
- **Profile choice**: chosen on the title screen, or picked from the seed by a separate stream.
- Ground decoration is rebuilt from the played map, kept off the boundary ring, the Keep's surroundings, river banks and fords, and hidden under buildings. It never blocks anything.

## 12. Saves and settings

- **Checkpoints** [I]: written on Day 1 after creation, on each new Day after its perk and income, immediately before Night, and on Save and Quit during Day. The frozen reward offer is attached to the last Day checkpoint before a choice is allowed. Victory and Defeat write a terminal record.
- **Contents**: schema version, rules version, campaign ID, content version, map profile, difficulty (`standard`), seed, terrain, map attempt and fallback flag, all night plans, checkpoint night, gold, last paid day, run status, buildings (ID, kind, anchor, orientation, HP, price), next IDs, perks, reward stream state, pending offer, offer history, run and final statistics. The map symmetry is not stored; it is derived from the seed. Stable definition IDs, never Unity references [I].
- **Schemas** [I]: release runs write schema 2. Schema 1 is the MVP profile. A save of a profile or content version the build does not know is refused with an actionable message and left untouched (content-1 release saves are refused, not rescaled). An unknown schema reports "made by a newer version".
- **Writes** [I]: temp file, flush, atomic replace keeping the previous file as `.bak`. A failed write keeps the run available, blocks progress with a Retry, and never shows a success indicator first. A damaged latest save offers Recover backup; a New Run never overwrites an active or damaged slot (it takes the next slot). Release and MVP builds share the save folder.
- **Settings** (separate file, version 2): master, music and effects volume, camera sensitivity, camera shake, window mode, resolution. **Onboarding profile** (separate file): which guide steps are done or skipped.

## 13. Controls and presentation

| Input | Action |
|---|---|
| 1-8 or cards | Wall, Archer Tower, House, Farm, Spike Trap, Cannon, Watchtower, Repair Station tool |
| Left click | Place with a tool (stays active), else select |
| Right click / Esc | Cancel tool; Esc without a tool clears the selection (the menu opens from its button) |
| Right drag | Pan by grabbing the board (a click without drag still cancels) |
| WASD / arrows, wheel | Pan, zoom |
| R | Rotate the ghost or selected building (Day) |
| Space | Pause / resume during Night |
| F or speed buttons | Night speed 1× / 2× / 3× |
| Selection panel | Sell, Repair (exact quote), Rotate (Day only) |

- Camera: fixed-orientation orthographic isometric view (yaw 45°, pitch 40°), pan and zoom, no rotation, Reset Camera.
- UI clicks never reach the world [I]. Ghosts show footprint, price and the rejection reason; tooltips stay inside the screen.
- Shown at all times: gold, income forecast, Keep HP (with a hit signal even when the Keep is off screen), night number, the next attack. Selection shows effective values from the simulation (damage, interval, range, income), not base definitions.
- Board readability: entry gates in the world with direction, route chevrons, new-side warnings, BREACH label, range rings, Watchtower and Repair Station reach previews.
- Combat feedback: lethal and non-lethal shots stay visible at the right place even after the target is removed; distinct cues for hits, Keep hits and destruction; bounded effects cleared on retry and load. One presentation clock: arrows, gait, hit reactions and burn freeze while paused or in the menu.
- Day/Night lighting blends; enemies stay readable throughout. Audio is synthesized; volumes in settings; threats never depend on audio alone. Text must stay legible at 1280×720 and 1920×1080.
- Title menu: New Run (optional seed, map profile), Continue, Recover backup, Settings, Help, Credits, Quit, version and build ID. Quit with unsaved Day changes offers Save and exit, Exit without changes, Cancel.
- First-run guide: five skippable steps that advance only when the action happens (build a Tower, inspect its range, optional income, Start Night, choose a reward).

## 14. Performance

- Reference machine: AMD Ryzen 5 3600, GeForce GTX 1660 SUPER, 32 GiB RAM. Measure a Windows x64 non-development build at 1920×1080, VSync off, no frame cap. Editor numbers are diagnostic only.
- Stress fixture: 100 enemies kept alive across the four entries with a fixed defensive layout, Keep and Walls invulnerable, a scripted Wall removal mid-capture to exercise route invalidation, clearly labelled as debug.
- Gate: after 10 s warm-up, 60 s of frame times: median ≤ 16.7 ms, p95 ≤ 20 ms, p99 ≤ 33.3 ms (percentile = sorted sample at index `ceil(p × count) − 1`). Re-measure after content or rendering changes.

## 15. Six-night MVP profile (`mvp-rules-1`)

Frozen. Everything above applies except:

- **Content**: buildings Keep, Wall, Archer Tower, House, Farm, Spike Trap (hotkeys 1-5); enemies Grunt, Runner, Brute, Siege Golem; the first ten perks of section 10 (pool weights as there; Archer Tower perks are "Tower" perks); six nights, rewards after Nights 1-5, Siege Golem on Night 6.
- **Values**: Wall 20 gold / 100 HP; Archer Tower 70 / 100 HP, 10 damage per 0.75 s, range 4; House 40 / 80 HP, 15 gold/day; Farm 60 / 100 HP, 25 gold/day; Spike Trap 35, 8 damage. Keep, starting gold and enemy values as in sections 7-8.
- **Nights** (Grunt / Runner / Brute / Golem, entries, cadence): 1: 10/0/0/0, W, 1.5 s · 2: 12/4/0/0, W E, 1.5 s · 3: 14/4/2/0, W E, 1.25 s · 4: 18/8/3/0, W E N, 1.0 s · 5: 22/10/4/0, W E N, 1.0 s · 6: 24/12/4/1, W E N S, 1.0 s. Spawns cycle Grunt, Runner, Brute; entries strictly round-robin without seeded offset; the Golem spawns from West at 30 s.
- **Map**: no profiles or symmetry. All land; 30 distinct eligible cells outside the boundary ring, the entries and the 5×5 area around the Keep become 10 forest, 10 rock, 10 water in sampled order. Same validation (open routes, no isolated pockets, at least two disjoint 2×2 Farm sites); up to 20 attempts, then the authored flat map, flagged as fallback.
- **Saves**: schema 1. A six-night save continues as six nights in the release build (release cards hidden); an MVP build refuses twelve-night saves without overwriting them.

## 16. Tuning history

Changes to [P] values after recorded scripted policy runs (real budget, seeds 1-8 per approach). Scripts check mechanical viability only; they are not balance claims.

| Date | Change | Reason |
|---|---|---|
| 2026-10-16 | Nights 7-12 raised to the current table; Nights 4-6 kept | A sealed Keep with four Towers and two Cannons won every seed after 10-13 purchases; a steeper mid-game made every approach collapse on Nights 6-7 |
| 2026-10-16 | Raider Archer range 3.0 → 3.5 | At 3.0 an Archer always stopped inside a Tower's range; 4.5 collapsed every approach on Night 5 |
| 2026-10-16 | Content version 2: every building about 30% cheaper and about 20% weaker (values in section 7) | Players found it boring to build so little; runs now buy 14-42 buildings instead of 11-28 |

Last recorded result: kill-zone 6/8, corridor 7/8, economy 8/8 wins on their profiles' own orientation. Economy-first is now the most forgiving approach, and the game is likely easier than before content version 2.
