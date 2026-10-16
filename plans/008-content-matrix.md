# Plan 008 content matrix

Every 008 addition, the decision it creates, its counter, where it is taught, its cue and how it is verified. Rules are in [008-rules.md](008-rules.md). "Author" is who owns the current asset: **generated** = produced by setup scripts (`EntityPrefabBuilder`, `icons.py`, `synth.py`) and replaceable; no hand-authored final art or audio exists yet (G4 PENDING). Verification names the actual EditMode/PlayMode check or the PENDING human check.

## Buildings

| Item | Decision it enables | Counter / weakness | Teaching moment | Audiovisual cue | Author | Verification |
|---|---|---|---|---|---|---|
| Cannon | Spend 110 gold on a 2×2 kill-zone gun that rewards queues (corridors, breach points) instead of a reliable Archer | Slow cadence; weak vs a thin stream of Runners; big footprint competes with Farms; Raider Archers out-prioritise it as class 3 | Night 4 preview names Bombers/clumps; card text "Area damage, slow" | Heavy barrel, muzzle flash + ground ring at impact; `CannonShot`/`Impact` sounds | generated | EditMode: splash once per target, range boundary, ID order |
| Watchtower | Cluster defenses to extend reach vs. spreading them | 80 HP support target (Raider Archer class 2); no stacking punishes over-building | Hover ghost highlights towers it buffs; inspect text "+1 range to adjacent towers" | Tall thin post with a flag; buffed towers show the larger ring | generated | EditMode: non-stacking, recompute on sale/destruction without cooldown reset |
| Repair Station | Trade income space for sustain of a front line across nights | Does nothing during the night; cannot save a building destroyed in combat; Bomber splash | Dawn line in the recap "Repair Station restored N HP"; inspect shows reach | Crane/workshop; dawn sparkle on repaired buildings | generated | EditMode: exactly one dawn repair, deterministic allocation, not on reload |

## Enemies

| Item | Decision it enables | Counter | Teaching moment | Cue | Author | Verification |
|---|---|---|---|---|---|---|
| Bomber | Forces Wall depth or killing range in front of breach points; punishes single-Wall seals | Kill before the fuse (Archer range in front of walls, traps on the approach), Reinforced Timber, Blast Shields, Gatehouse | Night 4 announcement; visible fuse spark while lit | Round pack, fuse flicker, blast decal + `Explosion` sound | generated | EditMode: kill cancels blast; Keep blast defeat precedence |
| Raider Archer | Forces economy placement behind defenses (screening) | Towers in front of the economy; Counter-Battery; Hardened Roofs | Night 5 announcement "stops 3 tiles away and shoots your Houses first" | Bow silhouette, stops, thin arrow trail `RangedShot` | generated | EditMode: stops on its route, sticky target, cooldown reset on retarget |
| Siege Commander | Final test of the chosen build; target-priority decision (kill the Commander first) | Focus fire / Heavy Shot; separate it from escorts with corridors | Night 12 announcement + arrival banner + boss bar | Banner bearer, aura ring on the ground | generated | EditMode: aura +50%, removed on death |

## Perks (24)

Ten new perks marked ★ change placement, targeting or timing. Teaching: every card shows effect text from `PerkRules.Effect` plus an affected-kind tint; verification in EditMode is by the shared modifier tests only where the rule composes non-trivially (a test per perk is not required by AGENTS.md).

| Perk | Decision | Counter-risk / limit | Verification |
|---|---|---|---|
| ★ KillZone | Put traps where Towers can see them | Needs both traps and Towers | Composition covered with Sharpshooters in damage-sum test |
| ★ FocusedFire | Finish wounded enemies vs nearest threat | Can let fresh Runners slip | Targeting test |
| ★ CounterBattery | Prioritise ranged/siege units | Melee can reach walls | Targeting test (class filter) |
| ★ Overwatch | Faster first shots at the edge of range | No sustained DPS gain | Timing assertion in targeting test |
| ★ SpikedCorridor | Lines of traps rather than scattered | Traps cost 35 each | Policy runs |
| ★ Gatehouse | Walls hugging Towers | Reduces open space | Policy runs |
| ★ MarketSquare | Compact housing blocks | Clustered Houses are an archer target | Forecast = payment (existing invariant) |
| ★ WarBonds | Defend the Keep perfectly | Lost on any Keep hit | Policy runs |
| ★ SignalFires | Looser Watchtower placement | Still non-stacking | Non-stacking test extended |
| ★ FieldEngineers | Station near the Keep | Budget only at dawn | Dawn repair test |
| Shrapnel, HeavyShot | Cannon specialisation | Stat only | Policy runs |
| BlastShields, HardenedRoofs | Counters to 008 threats | Stat only | Policy runs |
| Existing 10 | Unchanged semantics; Archer-only where stated | — | Existing MVP tests (unchanged pool) |

## Maps

| Profile | Decision | Counter | Verification |
|---|---|---|---|
| ravine | Long channels invite corridors + traps + Cannons | Bombers at the seal; N/S still direct | Generator constraint test over a bounded seed corpus |
| crossing | Fords split W/E pressure; defenses must cover two fronts | Long routes give time; Archers at fords | Same |
| meadow | Space for economy; few natural chokepoints | Must build your own corridor | Same |
