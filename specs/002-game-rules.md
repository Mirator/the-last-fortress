# Game rules

Version 1.16, 2026-10-28 (1.16: under `release-rules-4` enemies have bodies: they walk side lanes, wait behind each other, queue along a building, and the hero can neither walk through them nor stop them (they push it); merged from the feature spec archived as `archive/014-unit-collision.md`. 1.15: no building pulls the hero camera in any more, only trees, bushes, boulders and the ground; a building the camera is in or looks through fades instead; merged from the feature spec archived as `archive/013-buildings-let-the-camera-through.md`. 1.14: under `release-rules-4` the player may call the rest of a night early, once, for 1 gold per second saved at the next dawn, and bosses spawn at 15 s instead of 30 s; merged from the feature spec archived as `archive/012-night-pacing.md`. 1.13: in the hero view, buildings, trees and boulders that hide the hero turn see-through, and Walls no longer pull the camera in; presentation only, merged from the feature spec archived as `archive/011-hero-view-occluder-fade.md`. 1.12: the Day briefing collapses to its first line after 12 s and a click shows it again; off-screen entry tags stay below the resources panel; presentation only, recorded in `plans/027-late-game-ui.md`. 1.11: at Night start the strategic camera eases out to show the Keep and tonight's entries; presentation only, merged from the feature spec archived as `archive/010-night-start-framing.md`. 1.10: the hero camera pivot steps off a Wall or Tower the hero touches, so the camera stays behind the hero instead of falling back into its head; presentation only, a fix recorded in `plans/024-hero-camera-pivot-results.md`. 1.9: strategic class markers under every enemy at a steady on-screen size, enemy HP bars that keep their size when zoomed out, default zoom half-height 4.5 and zoom-out capped at the whole board (10.5); presentation only, merged from the feature spec archived as `archive/009-strategic-figure-readability.md`. 1.8: under `release-rules-4` each melee enemy chases the hero at most once (it lives one night), then only fights back in place; enemy hits on the hero reach 0.225, the hero's own reach; switching between a building and the hero keeps the attack timing; merged from the feature spec archived as `archive/008-bounded-chase-limits.md`. 1.7: `release-rules-4` (revised the same day after the owner's playtest: touch hits, bounded pursuit, turn-late routes), the third-person production slice, is the game: an accelerating hero with a 0.48 m body and a wind-up/contact/recovery attack at physical reach, section 18; a safe hero camera and focus lifecycle; merged from the feature spec archived as `archive/007-third-person-production-slice.md`. 1.6: the hero camera paragraph states one obstacle order, pull in first and lift only if needed. 1.5 and earlier: release-rules-3 walking speeds, one true scale, real-size models, a board in mist). Status: **implemented**; this is the description of the game as it is now. It consolidates the earlier milestone specs (prototype, six-night MVP, playtest improvements, release rules; see `archive/`) with every value taken from the current code and `Data/ReleaseDefinitions.asset`.

How to read and change it:

- The release profile `release-rules-4` is the game: sections 1-14, the hero of section 17 as changed by section 18, with the walking enemies of section 8. `release-rules-3` is the same with the section 17 hero, `release-rules-2` adds the earlier sprint speeds and `release-rules-1` has no hero; all three are kept for older saves. Section 15 lists only how the legacy six-night profile differs.
- **[I]** marks an invariant rule: changing it changes behaviour of existing runs and needs a new rules version. **[P]** marks provisional tuning: it may change after recorded evidence (scripted policy runs or playtests), with the change noted in section 16. Untagged statements are rules.
- A feature spec proposes changes. When it is implemented, merge its rules into this document and move the feature spec to `archive/`.
- Names in `code font` match the code (`Definitions.cs`, `RunSimulation.cs`, `PerkRules.cs`, `EconomyRules.cs`, `ReleaseCampaign.cs`, `MapProfiles.cs`).

## 1. Profiles

| Profile | Rules version / campaign | Where | Status |
|---|---|---|---|
| Release | `release-rules-4` / `twelve-nights`, content version 2 | `Release.unity`, `Builds/Release/` | The game (section 18 hero, walking speeds) |
| Release, section 17 hero | `release-rules-3` / `twelve-nights`, content version 2 | Release build only, for saves made before release-rules-4 | Legacy; the hero of section 17 |
| Release, sprint speeds | `release-rules-2` / `twelve-nights`, content version 2 | Release build only, for saves made before release-rules-3 | Legacy; the release-rules-2 speeds and HP of section 8 |
| Release before the hero | `release-rules-1` / `twelve-nights`, content version 2 | Release build only, for saves made before the hero | Legacy; same rules without section 17 |
| Six-night MVP | `mvp-rules-1` / six nights | `Mvp.unity`; also kept in the release build so old saves continue | Frozen legacy profile (section 15) |
| Navigation prototype | one wave, no campaign | `NavigationPrototype.unity` | Development and test scene only; rules in `archive/002-navigation-prototype.md` |

A save names its profile. A build restores only profiles it ships and never converts a run from one profile to another [I].

## 2. Board and coordinates

- 20×20 grid, cells `(x, z)` with 0 ≤ x, z ≤ 19 [I]. One cell is one world unit; a cell centre is `(x + 0.5, 0, z + 0.5)`. World-to-grid floors X and Z; points outside the grid are rejected, never clamped.
- The outermost ring is reserved: enemies may walk on it, nothing may be built on it [I]. Entries are the side midpoints West `(0,10)`, East `(19,10)`, North `(10,19)`, South `(10,0)`, turned with the map's symmetry (section 11).
- The Keep is 1×1 at `(10,10)` [I]. Its larger mesh never implies extra occupied cells. Enemies attack it from a cardinally adjacent cell and never enter it.
- Terrain types: land (walkable, buildable), forest, rock and water (neither walkable nor buildable, never attackable or harvestable). Fords are walkable land crossing a river. Terrain meshes never create hidden blockers.
- Units never reserve cells or change routes. Legacy profiles: units never block each other and may overlap. `release-rules-4`: enemies have bodies (section 8, Bodies), and the hero is blocked and pushed by them (section 18).

## 3. Run flow

States: `Day(n)`, `Night(n)`, `Reward`, `Victory`, `Defeat`. Pause is a separate flag, valid only during Night.

1. **New Run** picks a seed (random, or entered as an unsigned decimal) and a map profile (chosen on the title screen, or picked from the seed). It generates the map and all twelve night plans before Day 1, and saves Day 1. Start: 250 gold [P], Keep at full HP, no income on Day 1 [I].
2. **Day** is untimed. Build, sell, repair and rotate are allowed only in Day [I]. The next night's composition, entries and routes are shown.
3. **Start Night** saves the Day checkpoint, then enters Night. Repeated clicks cannot start a second wave.
4. **Night** runs the fixed-step simulation (section 4). Construction is disabled, including while paused. **Early call** (`release-rules-4`): once per night, while at least one of tonight's spawns is still pending (paused or not, either view), the player may call the rest early (C or the CALL EARLY button). A spawn originally due at tick `t` at or after the call tick `c` becomes due at `c + ceil((t - c) / 4)`; spawns already due, order, entries, kinds and the living cap are unchanged [I]. The bonus, `floor` of the seconds by which the night's last spawn moves earlier, x 1 gold [P], is shown on the button, recorded in the night recap and paid at the next dawn (section 6). Nights are not checkpointed, so a reload replays the night and the player may call again.
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
  - With the hero (section 17): hero movement after step 2, the hero's swing after step 3, hits on the hero in step 5, hero death after step 6.
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
  - Early call (`release-rules-4`): the bonus earned by calling the previous night early, as its own dawn line after War Bonds; never part of the forecast, not paid after the final night or a defeat.
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

`release-rules-3` (the game) at real scale (a tile is about 8 m): people walk or march, so speeds are about a third of `release-rules-2` and HP is ×1.5, except Brute 180 and Raider Archer 45 (plan 015). HP and speed below are `release-rules-3`; the `release-rules-2` values follow in brackets.

| Kind | HP | Speed tiles/s | Damage / interval | Wall × | Role |
|---|---:|---:|---|---:|---|
| Grunt | 45 (30) | 0.3 (1.0) | 10 / 1.0 s | 1 | Baseline melee |
| Runner | 30 (20) | 0.6 (1.75) | 6 / 0.75 s | 1 | Fast |
| Brute | 180 (150) | 0.2 (0.6) | 20 / 1.5 s | 2 | Heavy |
| Raider Archer | 45 (35) | 0.25 (0.9) | 6 / 1.5 s, range 3.5 | 1 | Ranged |
| Bomber | 60 (40) | 0.3 (1.0) | Blast 60, splash 20, fuse 1.5 s | 2 | Breacher |
| Siege Golem (boss) | 1200 (800) | 0.15 (0.4) | 40 / 2.0 s | 2 | Boss on Nights 6, 9, 11 |
| Siege Commander (boss) | 1500 (1000) | 0.15 (0.4) | 30 / 2.0 s | 2 | Final boss, Night 12; aura |

- **Bodies** (`release-rules-4`) [P values; I: integer progress, ascending ID order]. Each enemy has a body: radius 0.07 tile (0.10 Brute, 0.18 Siege Golem and Siege Commander). Ordinary enemies walk a fixed side lane of their route, one of five 0.14 tile apart (-0.28 to +0.28) chosen by ID; bosses walk the centreline; the lane turns with the route at half the walking speed. Routing, cells and cell-centre decisions are unchanged [I]. A body does not take a step that brings it within the sum of radii of a body ahead of it (in front along its travel) and closer than now; chasing and returning enemies never block. A body blocked for 4 s [P] may walk through bodies for half a tile, unless the bodies ahead of it lead to one attacking a building or the hero, or with a lit fuse; a circle of waiting bodies therefore always resolves. A due spawn waits while its entry point is taken, at most 4 s (FIFO; not in the stress fixture). Melee attackers stand in their lanes on their attack cell, so a cell face holds up to five, and the rest wait behind and step in as places free. A body easing into its lane may brush a neighbour beside it by one step of sideways movement (at most 0.015 tile).
- Wall × multiplies damage against Walls only. It never overrides route choice: heavy enemies still take any open route.
- No armour, resistances or flying. No melee splash.

**Raider Archer.** Walks its normal route and only ever stops on a cell centre of it; it never paths to firing positions [I]. At each fresh cell-centre decision it looks for a target: living solid buildings (Keep included, traps excluded) with a footprint cell centre within 3.5 tiles, no line of sight needed [I]. Priority [I]: (1) House, Farm; (2) Watchtower, Repair Station; (3) Archer Tower, Cannon; (4) Wall; (5) Keep; then nearest footprint-cell centre; then lowest building ID. Found → stops and targets it. Sticky while the target is alive and in range; then reacquires at its next decision (fresh full interval) or resumes its route. Ranged hits are not melee: no reflection, no Barbed Defenses.

**Bomber** [I]. Walks like a melee unit. At its attack cell it acquires its target (breach blocker or Keep) and lights its fuse. Under `release-rules-3` it lights earlier: at any cell centre (fresh step) with a living solid building on a cardinal neighbour, targeting the Keep first, then a Wall, then the lowest ID. Either way, once lit it never moves or retargets again and never makes ordinary attacks. On detonation (enemy phase): the primary blast hits its target if still alive (×2 against a Wall), then splash hits every other living building with HP that has a cell within Chebyshev distance 1 of the Bomber's cell, in ascending ID. The Bomber then dies without counting as defeated. Killing it before the detonation resolves (traps, shots or burn in the same step included) cancels the blast. Blasts are not hits: no reflection, no Commander aura.

**Siege Commander aura** [I]. When another living non-boss enemy makes a melee or ranged hit, its damage is +50% [P] (floored) if a living Siege Commander is within 2.5 tiles [P] of the attacker at that step. Several Commanders still give +50% once. No stored aura state; a Commander killed earlier in the same step gives no aura.

**Damage modifiers** [I]: percentage bonuses on one damage value add against the base and floor once: `base × (100 + sum) / 100`. Reductions apply after bonuses, one after another in the order Gatehouse, Hardened Roofs, Blast Shields: `max(1, d × (100 − r) / 100)` for a positive hit.

## 9. Campaign and waves

Twelve authored nights [P counts; I structure].

| Night | Grunt | Runner | Brute | Bomber | Archer | Boss (at 15 s [P]; 30 s before `release-rules-4`) | Entries | Cadence s |
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
- **Hero runs** add two optional fields: the hero's position at Start Night (only in that checkpoint) and the hero-kill count in the statistics. `release-rules-4` Start Night checkpoints also store the hero's velocity (section 18); other profiles ignore it.
- **Settings** (separate file, version 3): master, music and effects volume, camera sensitivity, camera shake, window mode, resolution, and the hero camera settings of section 17. **Onboarding profile** (separate file): which guide steps are done or skipped.

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
| C or CALL EARLY | Call the rest of tonight's enemies early for gold at dawn (`release-rules-4`, once a night) |
| Selection panel | Sell, Repair (exact quote), Rotate (Day only) |

- Camera: fixed-orientation orthographic isometric view (yaw 45°, pitch 40°), pan and zoom, no rotation, Reset Camera. Default view: centred on the Keep, half-height 4.5 tiles (zoom 2.5–10.5; 10.5 frames the whole board, and beyond it there is only mist), close enough for true-scale figures; zoom out for the whole board. At every Night start the camera eases over 0.6 s [P] to the closest view, from half-height 4.5 up to 10.5, that shows the Keep, every entry used tonight and a point 1.5 tiles beyond each along its road [P], inside a safe area clear of the HUD (18% of the height top and bottom, 10% of the width each side [P]); it does so even if the player moved the camera by Day. Player camera input or an explicit view (Reset Camera, Z, H) ends the ease where it stands. With the hero view at Night start on, the strategic view is set at once. The hero view and its controls are in section 17.
- UI clicks never reach the world [I]. Ghosts show footprint, price and the rejection reason; tooltips stay inside the screen.
- Shown at all times: gold, income forecast, Keep HP (with a hit signal even when the Keep is off screen), night number, the next attack. The Day briefing (a new enemy type, the final siege) shows in full for the first 12 s of each Day [P], then only its first line until the player clicks it; a click toggles it. Entry tags at the screen edge stay clear of the top panels, including the resources panel as its perks list grows. Selection shows effective values from the simulation (damage, interval, range, income), not base definitions.
- Surroundings (presentation only): outside the board, open ground fades into an edge mist (from about 2.5 tiles beyond the board, opaque by 5), in both views; nothing stands there, so the field can grow later. A road leaves each side midpoint; there are no gate structures: tonight's entries are marked by a coral pad on the entry cell and a torch on the road mouth lit at Night. Enemies are drawn walking out of the mist along their road before they spawn, timed from the simulation tick to reach the entry cell at their spawn tick (a spawn at the start of the night appears at its entry); until then they have no HP bar, cannot be inspected and cannot be shot, because the simulation does not have them yet.
- Board readability: entry markers in the world with direction, route chevrons, new-side warnings, BREACH label, range rings, Watchtower and Repair Station reach previews.
- Class markers (strategic view only): a flat marker on the ground under every living enemy, told apart by shape alone, with a supporting colour and a dark outline: Grunt red disc, Runner yellow triangle, Brute magenta square, Raider Archer green diamond, Bomber white ring, Siege Golem and Siege Commander hexagons (stone grey, violet) half as large again. A marker is about 18 px across at any zoom and resolution, never smaller than 0.12 tile nor larger than 0.42 tile [P]; it is drawn unlit over terrain, buildings and trees, so night and occluders never hide an enemy, but under the enemy figures, so a figure standing on its marker covers it. Zoomed in, markers fade out between half-height 4 and 3 [P], where the figures read on their own. Enemy HP bars still show only when damaged; zoomed out past half-height 5.5 they keep the size they have there.
- Combat feedback: lethal and non-lethal shots stay visible at the right place even after the target is removed; distinct cues for hits, Keep hits and destruction; buildings never bounce or squash when hit or when they fire, and camera shake on Keep hits is off by default (a setting); bounded effects cleared on retry and load. One presentation clock: arrows, gait, hit reactions and burn freeze while paused or in the menu.
- Day/Night lighting blends; enemies stay readable throughout. Audio is synthesized; volumes in settings; threats never depend on audio alone. Text must stay legible at 1280×720 and 1920×1080.
- Title menu: New Run (optional seed, map profile), Continue, Recover backup, Settings, Help, Credits, Quit, version and build ID. Quit with unsaved Day changes offers Save and exit, Exit without changes, Cancel.
- First-run guide: five skippable steps that advance only when the action happens (build a Tower, inspect its range, optional income, Start Night, choose a reward).

## 14. Performance

- Reference machine: AMD Ryzen 5 3600, GeForce GTX 1660 SUPER, 32 GiB RAM. Measure a Windows x64 non-development build at 1920×1080, VSync off, no frame cap. Editor numbers are diagnostic only.
- Stress fixture: 100 enemies kept alive across the four entries with a fixed defensive layout, Keep and Walls invulnerable, a scripted Wall removal mid-capture to exercise route invalidation, clearly labelled as debug.
- Gate: after 10 s warm-up, 60 s of frame times: median ≤ 16.7 ms, p95 ≤ 20 ms, p99 ≤ 33.3 ms (percentile = sorted sample at index `ceil(p × count) − 1`). Re-measure after content or rendering changes.
- Hero view: the same fixture through the hero camera (`-hero-camera`) must stay within 1.5× of the strategic view measured in the same session, paired and alternating. Since `release-rules-4` both runs carry the current hero (`-stress-hero`: it stands in the fixture and, in the hero view, is followed by the real camera while it attacks), and the report names the rules, hero figure and view.

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
| 2026-10-19 | Hero at true scale: speed 3.0 → 1.5 tiles/s, reach 1.0 → 0.6, enemy engage/hold 1.0/1.5 → 0.6/0.9 | Owner feedback: buildings looked small next to the hero and the hero was too fast; the figures were shrunk to true scale and the distances followed |
| 2026-10-19 | Hero swing reach 1.2 → 1.0 tile | Owner feedback: with the smaller hero-view figures a 1.2-tile swing looked like hitting enemies out of reach |
| 2026-10-20 | `release-rules-3`: a Bomber lights its fuse at the first solid building beside its path, not only at its attack cell | At walking speed almost no Bomber reached its attack cell (active hero, 24 runs: 5 fuses, 3 blasts). Now 46 fuses, 31 blasts; wins kill-zone 8/8, corridor 5/8, economy 8/8 (was 7/8, 5/8, 8/8). Still about 3% of Bombers detonate, so the role stays weak |
| 2026-10-20 | `release-rules-3` review: Brute HP 225 → 180, Raider Archer 53 → 45, hero 0.6 → 0.8 tiles/s | Four-way review: a Brute outlasted the hero one-on-one (15 s vs 16.5 s), Brutes and Archers caused nearly all Keep damage so under-built defences failed in one night, and at 0.6 the hero could not catch a Runner nor reach a lane in time |
| 2026-10-20 | `release-rules-3`: enemy speeds to about a third (Grunt/Bomber 0.3, Runner 0.6, Brute 0.2, Archer 0.25, bosses 0.15), enemy HP ×1.5, hero 0.6 tiles/s | Owner: at real scale (1 tile ≈ 8 m) people sprinted at 8–14 m/s; enemies should walk. Scripted approaches (active hero, seeds 1–8, own orientation): HP ×2.5 won 2/0/0 of 8 (slow enemies bunch at the walls); ×1.5 wins kill-zone 8/8, corridor 4/8, economy 7/8 (release-rules-2: 8/8 each); a hero alone still falls by Night 2 |
| 2026-10-19 | Hero swing 15 damage / 0.6 s → 10 / 0.75 s | With the starting values a hero without buildings survived to Night 5 on two Crossing seeds; the owner chose these values and to judge the hero's share of kills by its Nights 6-12 average |
| 2026-10-20 | `release-rules-4` hero (section 18) with the feature spec's initial values; no candidate beyond the first was needed | V3 vs V4, seeds 1-8 on their own orientations, idle/active/no-build: active wins kill-zone 8/8 → 7/8, corridor 5/8 → 4/8, economy 8/8 → 8/8; active hero share of kills on Nights 6-12 averages 7%, 7%, 4%; a hero alone falls by Night 3 on every seed. Log: `plans/evidence/019/stage3/hero-policy-runs-candidate0.txt` |
| 2026-10-20 | `release-rules-4` after the owner's first playtest: reach 0.20 → 0.225, touching enemies always struck, bounded pursuit instead of engage/hold, turn-late routes | A Grunt standing inside the hero was missed by the aimed arc; the owner asked for a threat system and found routes stepping sideways at the entry. Re-run (V4): active wins kill-zone 8/8, corridor 4/8, economy 8/8; hero share of kills N6-12 averages 8%, 8%, 4%; contact misses about 3% (was 6.5%); a hero alone still falls by Night 3. Log: `plans/evidence/019/threat/hero-policy-runs-threat.txt` |
| 2026-10-28 | `release-rules-4`: one chase per enemy per night, then fighting back in place; enemy hit reach on the hero 0.20 → 0.225; the 2 s chase cooldown dropped from the rules text (it cannot take effect with one chase) | The hero could kite an enemy indefinitely and drag it over Spike Traps (strike-started chases had no cooldown). Kiting script, corridor, seeds 1-8: most chases by one enemy 2-7 → 1, most off-route trap hits on one enemy 2 → 1. Re-run (V4) against the same code without the change: active wins kill-zone 8/8, corridor 4/8, economy 8/8 and hero share of kills N6-12 8%, 8%, 4% in both; Keep damage and idle results identical; active hero deaths 70/50/22 → 64/50/21; a hero alone still falls by Night 3. Logs: `plans/evidence/022/kite-before.txt`, `kite-after.txt`, `hero-policy-runs-baseline.txt`, `hero-policy-runs.txt` |
| 2026-10-28 | `release-rules-4`: bosses spawn at 15 s (was 30 s); new early call (no change to scripted runs, which never call) | Boss nights ended with a long wait for the 0.15 tiles/s boss. Active hero, seeds 1-5 on their own orientations: boss nights 4-10 s shorter (Night 6 94 to 84 s, Night 12 100 to 91 s); 3 defeats in 15 runs instead of 2 (corridor seed 1 now falls on Night 9); 20 s was worse (4). The recorded-win test still passes. Log: `plans/evidence/030/` |
| 2026-10-28 | `release-rules-4`: unit bodies, lanes and queues (spec 014); hero touch reach 0.1 to 0.15 (bodies touching) | Owner: enemies and the hero should not pass through enemies. Active hero, seeds 1-5 x 3 strategies (pacing probe): 2 defeats in 15 instead of 3; nights 0-15 s longer (Night 5 67 to 82 s, queues); Keep damage moved both ways (kill-zone seed 4 0 to 240, corridor seed 2 700 to 280). No values retuned. Log: `plans/evidence/033/` |

Last recorded result (`release-rules-3`, plan 015, active hero, seeds 1–8 on their own orientations): kill-zone 7/8, corridor 5/8, economy 8/8; a hero alone falls by Night 3 on every seed. Log: `plans/evidence/015/hero-policy-runs-r3.txt`.

## 17. Hero (`release-rules-2` and later)

Every new release run has one player-controlled hero (merged from the hero feature spec, archived as `archive/006-third-person-hero.md`). `release-rules-1` is the same game without the hero, kept only so older saves continue.

**State** [P values]: 200 HP; moves 0.8 tiles/s (48 units/step; 1.5 tiles/s, 90 units in `release-rules-2`); body radius 0.3 tile; position in integer 1/1200-tile units [I], not cell-bound.

- **Command** [I]: one per step: move heading (16 directions or none), attack flag, facing (64 directions), or "not controlled". The simulation reads nothing else from input; the same command stream reproduces the same night. Movement and the swing arc use fixed integer direction tables.
- **Spawn cell** [I]: the walkable cell without a solid building nearest the Keep by Manhattan distance; ties by direction from the Keep (dominant axis: N, E, S, W), then `(z, x)`. Recomputed from the current grid whenever used.
- **Collision** [I]: the body never overlaps a solid building, non-walkable terrain or the board edge; movement resolves X then Z, sliding along obstacles, and cannot pass a diagonal gap between two blocked cells. The hero never occupies cells, never blocks or changes routes, never triggers Spike Traps; enemies and the hero pass through each other (legacy profiles; under `release-rules-4` see section 18, Bodies).
- **Day**: the hero walks on its own fixed-step Day clock; nothing else advances. A building placed over the hero's body is placed anyway and the hero moves to the spawn-rule cell nearest its own cell [I]. The position at Start Night is stored in that checkpoint, so Continue replays the night from it; Save and Quit during Day returns the hero to the spawn cell.
- **Swing** [P: 10 damage, every 0.75 s = 15 steps, reach 0.6 tile, arc 120°; I: geometry]: hits on the step it starts, every living enemy in the arc, ascending ID. Hero hits are direct damage but no tower perk applies. Kills count as defeated plus a separate hero-kill count (run and night).
- **Guard** [I]: not controlled (strategic view), the hero holds position and swings at the nearest enemy in reach (ties lowest ID). It never walks on its own.
- **Engagement** [I]: enemies decide only at cell-centre decision points. Grunt, Runner and Brute stop for a hero within 0.6 tile [P] where they would otherwise move on (never at their route end, never while attacking a building) and keep it within 0.9 [P]. Raider Archers treat the hero as a class-0 target (with House and Farm, nearest first); an archer the hero hits is provoked and takes the hero over any building at its next decision while the hero is alive and within its range; provocation ends when the hero dies, respawns or leaves range. Bombers and bosses ignore the hero. Hits on the hero: normal damage, Commander aura applies, no Wall multiplier or reflection. A melee hit lands only while the hero is within its own swing reach (0.6); beyond it the due hit waits, so stepping back is a real move.
- **Death** [I]: at 0 HP the hero is down (no commands, no swings, nobody's target) and respawns at the spawn cell with full HP after 10 s [P] of simulation time. Hero death never causes Defeat. On every Day entry the hero is restored to full HP at the spawn cell.
- **Step order** [I]: hero movement (with a due respawn) right after enemy movement; hero swing right after building attacks; hits on the hero in the enemy-action phase; hero death resolved after dead-enemy cleanup; Defeat check last as before.

**Views and controls.**

| Input | Strategic view | Hero view |
|---|---|---|
| Tab | Switch to the hero view (Day or Night, outside menus) | Switch to the strategic view |
| W A S D | Pan | Move, relative to the camera |
| Mouse | Hover, select, place | Look (cursor captured); left button swings |
| 1-8 | Arm a build card | Switch to the strategic view with that card armed (never places by itself) |
| F / speed buttons | 1× / 2× / 3× | Locked to 1× (2× and 3× dimmed) |
| Space | Pause / resume at Night | Jump (presentation only: never affects the simulation) |
| P | Pause / resume at Night | Pause / resume at Night |
| Enter | Start Night (Day) | Start Night (Day), staying in the hero view |
| H | Centre the camera on the hero | — |
| Esc | Cancel tool, else clear selection | Free the cursor and open the menu |

- Switching views is presentation only [I]: it never changes the simulation, RNG, gold or pause; entering the hero view sets 1× without resuming a pause, and leaving it restores the previous speed. A click is remembered for 0.25 s, so a quick tap between steps or late in the cooldown still swings. New, loaded and restarted runs start in the strategic view. Optional setting: enter the hero view when a night starts (off by default).
- Figures (hero and enemies) are drawn at true scale in both views (owner decision 2026-10-19): a person about 0.22 tile tall, so a tile is about 8 m. Every model is built at real size on that scale (plan 014): the Keep is the tallest building (about 19 m), the Archer Tower about 12 m, the Watchtower about 16 m, the palisade about 4.5 m, trees 11–13 m. The views may differ in detail (the hero view adds grass, sky and haze; the strategic view adds class markers, section 13) but never in proportions. The hero's cyan ring keeps its readable size. Presentation only. Entry tags show only as edge arrows in the hero view.
- Hero camera: entering eases in from the strategic framing; an obstacle behind the hero first pulls the camera in, then lifts it smoothly by at most 15°; perspective orbit close behind the hero with a shoulder offset; pitch −10°…60° (by default mouse forward looks up, as for aiming; Invert look makes it look down), an aiming crosshair at the screen centre while the hero is alive and the cursor is captured, distance 0.5–2.2 (wheel, default 1.0), field of view 50–90° (default 60). Obstacle handling, transitions and window focus follow section 18 (all profiles since `release-rules-4`).
- Responsiveness: while steering, the shown hero is the last step plus the fraction of the next step the current command makes, so input moves it in the first frame; the next real step replaces the prediction.
- Swing presentation: the body faces where it is steered and turns to the aim (camera direction) to swing; a faint arc in front of the hero shows the reach while steering; the swing twists the body (front-loaded), a pale arc shows the reach and angle where the swing was resolved, and each struck enemy flinches with sparks and a sound at the blade's contact (`HeroHit` event, 0.08 s after the swing). The shown hero glides at most 2.4 tiles/s toward its predicted position, so key presses never jolt the view.
- Night recap and run summary show the hero's share of kills.
- Hero view presentation: gradient sky dome blended with day/night beyond the strategic camera's far clip, the ground fading into the same mist toward the horizon, fog only while the hero view renders; health bars face the rendering camera; screen markers project with it and never point behind it; route lines and build cards are hidden (they return with the strategic view); a coral edge tag points to the Keep when it is hit off screen. The strategic view shows the hero with a cyan ground ring. HUD: hero HP, respawn countdown, view hints.
- Settings (version 3; older files migrate with defaults): hero look sensitivity, invert look, field of view, default hero camera distance, hero view at night start.

## 18. Production-slice hero (`release-rules-4`)

Merged from the feature spec archived as `archive/007-third-person-production-slice.md` (v0.1). Applies only to `release-rules-4` runs; section 17 stays the rule for older profiles, and everything there not changed below applies. Enemies, waves, economy, maps, perks and the Bomber contact fuse are those of `release-rules-3`.

**Motor** [P values; I: integer stepping, collision, rounding]. Body radius 72 units (0.06 tile, 0.48 m); top speed 42 units/step (0.7 tiles/s); the velocity moves toward heading × speed by at most 14 units/step per step (about 150 ms to top speed) and toward rest by at most 21 (about 100 ms); reversing passes through rest. While an accepted attack runs (acceptance to ready) the desired speed is 50%. Commands carry 64 headings (a checked-in integer direction table, no runtime trigonometry); the older 16-heading commands keep their meaning and promote exactly. Lengths use the ceiling integer square root and division toward zero; desired and resulting velocities are clamped to the cap. Movement resolves X then Z by the largest legal part; a blocked velocity component becomes zero, the other slides. The body collides with off-board and impassable cells whole, but with a solid building only where it is drawn: its footprint shrunk on every side by 84 units (Archer Tower; House across, 144 front to back, turning with it), 228 (Watchtower), 154 (Farm, to its fence line), the 1 x 1 Cannon as drawn: a 0.31-tile disc for the gun and its gunner and a 0.09-tile disc for each of its two corner props (shot pile, powder kegs), 0.38 tile from the centre on both axes in opposite corners, so the hero fits between them, 144 (Repair Station); the Keep stays whole. A Wall blocks its palisade as drawn: the centre post and an arm 48 units either side of its line to the edge of every side with a neighbouring Wall (so connected Walls stay closed), a lone Wall a 108-unit ring (owner playtest 2026-10-21). A water cell blocks except its drawn bank, within 154 units of a side or corner whose neighbour is land. Forest and rock cells of a generated map block only their props: two tree trunks (radius 48 units at fixed spots) and a bush (radius 144) per forest cell, one boulder disc (about 0.31-0.35 tile, by the cell's stable hash) per rock cell. The hero may therefore stand in a building cell's margin; a strike or chase line ignores the cells holding its ends. Enemies and routes still use whole cells. An uncontrolled hero (strategic view) brakes to rest with the same motor. Velocity is zeroed on death, respawn, relocation, dawn, new/restart and Day-save load; view switches keep it.

**Attack** (Day and Night since the owner's playtest of 2026-10-21; by Day it runs on the hero's Day clock and has nothing to hit) [P: 10 damage, reach 0.225 tile centre to centre, arc 100°, and any enemy whose body touches the hero's (within 0.15 tile centre to centre) whatever the aim; reach and touch extend by the amount an enemy's body is wider than 0.07 tile (spec 014), contact 3 steps after acceptance, 15 steps between acceptances; I: phases and ordering].  In phase 3b a due contact of an attack accepted on an earlier step resolves first, then a ready hero may accept: the player's attack commits the facing of that command; the guard accepts at the nearest enemy it can strike (ties lowest ID). Contact uses the committed facing, the hero's position at that step and the living enemies then (ascending ID, each damaged once), and is blocked when the line between the hero's and the enemy's centres touches a solid building or impassable cell, a corner included. A contact is emitted on a miss too; hits only for victims. Pause freezes the phases; death cancels an unlanded attack; Night start, dawn, reset, relocation and load clear attack state; a view switch neither cancels nor adds one. Archer provocation and hero-kill counting are as section 17.

**Bodies** (`release-rules-4`, spec 014) [I: integer units, ID order; P: 0.1 tile push]. At Night the hero's motor treats enemy bodies like obstacles (sliding X then Z), except a body it already overlaps, which it may leave. After the enemies move, a hero overlapping enemy bodies is pushed straight out of each (ID order) by at most 0.1 tile per step in total, never into a building, terrain or the edge; with no room it stays overlapped. The hero never blocks or slows an enemy. Enemy hits on the hero and a chase's stopping distance extend by the same body margin as the hero's reach.

**Input** (presentation): a held button asks every step (Day and Night) and repeats at readiness; one tap is kept until an attack is accepted at or after it or for five Night steps of unpaused play; a new tap replaces it. Unaccepted input is dropped on Day, death, view switch, menu, window focus loss and run boundaries.

**Threat: bounded pursuit** (owner decision after the first playtest, 2026-10-20; limited to one chase per enemy, with fighting back in place, by the feature spec archived as `archive/008-bounded-chase-limits.md`, 2026-10-28) [P: start 1.0 tile, leash 1.5, 4 s, provocation 2 s, one chase per enemy per night, enemy hit reach 0.225; I: decision points, ordering, the budget is per enemy and per night, a hold never outlasts the enemy's hit reach]. At a cell-centre decision point a Grunt, Runner or Brute that has not yet chased this night chases the hero when the hero struck it within the last 2 s (even while it was hitting a building), or when it is not attacking a building and the hero is within 1.0 tile; in both cases only when no solid building or impassable cell lies on the line between them. It leaves its route and walks straight at the hero at its own speed, stops just inside its 0.225-tile hit reach and attacks the hero on its normal interval. The chase ends after 4 s, when the hero is beyond 1.5 tiles, down, or out of line; the enemy then walks back to its cell centre and plans its route again. Once it has chased, it never leaves its route for the hero again that night. At a decision point where it would otherwise move on (never at its route end), it stops and attacks the hero when the hero is within its 0.225 hit reach and keeps attacking only while the hero stays within 0.225; otherwise it moves on, and its next chance to stop is its next cell centre. An enemy attacking a building that the hero struck within the last 2 s turns its attacks on the hero while the hero is within 0.225, then resumes the building. Switching between the building and the hero keeps its attack timing: the timer carries over both ways, and a hit already due lands at once. Spike Traps trigger when a chasing enemy enters their cell. Bombers, bosses and Raider Archers never chase (archers keep section 17's provocation). Melee hits on the hero land within 0.225 tile, the hero's own swing reach.

**Routes** (`release-rules-4`): open routes keep their length and goal, but among equally short routes go straight from the start and turn as late as possible (first along the axis with more distance to the goal, then keeping direction, then N, E, S, W). Breach routes are unchanged. The preview uses the same planner, so it still matches.

**Cannon** (owner, 2026-10-21): under `release-rules-4` the Cannon is 1 x 1, drawn as its own field gun on a low timber turntable with no stone emplacement; its shell flies on a flatter arc (peak 0.12 × distance, at most 0.35 tile) with a smaller shell, trail and muzzle smoke, and the same flight time; cost, HP, damage, range and splash are unchanged, and range and splash are measured from its cell centre. Older profiles keep the 2 x 2 Cannon of section 7.

**Saves**: the Start Night checkpoint stores the hero's velocity next to its position; a velocity above the top speed is refused as invalid; a missing field means rest. Continue replays the night from that moving start; Save and Quit during Day returns the hero to the spawn cell at rest.

**Camera and focus** (presentation, all profiles since plan 019): camera blockers are the drawn meshes of trees, bushes and boulders (one box per mesh) and the ground, never buildings (the camera may sit in or behind one, which then fades), never units, effects or overlays, and never read by the simulation. While the hero camera renders, any building (Walls included), tree, bush or boulder mesh whose box crosses the line from the camera to the hero's chest or head, or holds the camera's near plane, eases over 0.15 s [P] to see-through at 30% [P] (main light and ambient only, no shadow) and back; leaving the hero view restores every mesh at once. The camera keeps its near-plane volume clear: obstacles pull it in first (no minimum distance); only when the pulled-in distance is below 0.2 does it lift, by the smallest 5° step up to 15°. Inward is immediate, outward and lift release ease with a 0.15 s time constant; the final pose is re-checked. A shoulder pivot inside a blocker moves over the head and lower; a pivot nearer a blocker than the near-plane padding steps sideways off it, by at most 0.1 tile (the hero can touch a drawn Wall or Tower 0.06 tile from its box); with no clear pose at all the last clear pose is held. The entry transition (0.35 s) is checked frame by frame and cuts to the final pose if it would enter a blocker; leaving the hero view (Tab, 1-8) eases the camera out over 0.35 s to the strategic framing before the views swap (input is strategic at once), and run boundaries cut. The orbit is kept within a run and reset on new/load/restart. Losing window focus stops advancement and presentation (the player's pause is untouched), frees the cursor and drops unaccepted input; after focus loss or the menu, the hero view waits for a click that only recaptures the cursor.

**Presentation**: an articulated hero (elbows, wrists, ankles; generated low-poly, a longer arm and sword than the section 17 figure so the blade reaches about 0.2 tile) faces the aim, turns its hips toward travel within 50° and its chest toward the aim within 55°, and backpedals beyond 100°; gait follows shown travel, so it settles when stopped or blocked. The shown root is the simulation's own next step moved by the step fraction (no lag). The wind-up starts with acceptance and the strike pose, flinch, sparks, death and the impact sound land on the frame showing the contact step; a miss shows a short sweep at true reach with a whoosh and no impact; there is no standing reach sector. Cosmetic step-in at most 0.02 tile. Hero sounds: swing, blade impact (one per contact), hurt, fall and footsteps where a foot plants, played in the world near the hero camera and 2D otherwise. Reward cards ignore clicks for 0.8 s after they appear and until a button held when they appeared is released. In the hero view at Night the income forecast is hidden; gold, Keep HP, threats, hero HP and view hints stay.
