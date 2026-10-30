# Plan 036: break the turtle (raids, gold veins and the Mine, pressure pass)

Status: **CLOSED 2026-10-30.** Stages 0–4 are done and merged into `specs/002-game-rules.md` v1.21; spec 016 is archived. The D5 gate could not be reached with the approved knobs (section 5); the owner moved it to spec 017 D10, implemented by plan 037. Priority P1. Effort L (about 5–8 days, tuning included). Implements **`specs/archive/016-break-the-turtle.md` v0.3**, approved 2026-10-29: D1-a, D2-a, D3-a, D4–D7, plus the owner's follow-up answers. Current rules: `specs/002-game-rules.md` v1.20. Baseline commit: `975c093`. Source: the owner's 2026-10-29 playtest (`plans/playtest-checklist.md`). One layout won the whole run: Houses sealing the Keep, Archer Towers around them, and no Keep damage.

## 1. Pre-check (2026-10-29)

Paths are under `game/The Last Fortress/Assets/TheLastFortress/`.

- **Specs agree.** Spec 016 v0.3 changes `002` v1.20 in sections 2, 4, 6–9, 11–13 and 16. The spec review found three conflicts, and the owner settled each (v0.3):
  - the vein distance against the raid reach;
  - the 100-spawn limit against the tuning knobs;
  - a raid interrupted by the hero.
  Nothing is left open.
- **Building kinds.** `BuildingKind` ends at `RepairStation = 8`, so the Mine is `Mine = 9`. The seams follow `RepairStation`:
  - Core: `Definitions.cs`, `ReleaseDefinitions.cs`, `RulesProfile.cs`, `BuildingUpgrades.cs`, `PerkRules.cs` (Hardened Roofs), `HeroMotion.cs`, `RunSimulation.cs`;
  - Unity: `HudView.cs`, `RouteOverlay.cs`, `WorldView.cs`, `PrototypeContent.cs`, `ReleaseDefinitionsAsset.cs`, `CaptureSequence.cs`;
  - Editor: `PresentationSetup.cs`;
  - data: `Data/ReleaseDefinitions.asset`.
  The legacy definitions don't change.
- **The raid hook.** In `RunSimulation.DecideAtCenter` the current order is:
  1. `RangedHold` (Raider Archer);
  2. the hero pursuit;
  3. `LightOnContact` (Bomber, cardinal neighbours);
  4. the route step.
  A raid check, `RaidOnContact`, goes right after `LightOnContact`. It uses the same cardinal scan and the same fresh-step rule. Only Grunts, Runners and Brutes raid, and only once per night. The raid holds the enemy on its cell while its target lives and the raid time runs. A pursuit start or a fight-back ends it (v0.3).
- **Economy.** `EconomyRules` holds the per-building income functions; the Mine gets its own line. The tier income comes from `BuildingUpgrades`.
- **Map.** `MapGenerator.cs` defines `TerrainType` and the layout. `MapProfiles.cs` generates and validates the terrain.
  - Veins are a separate list on the layout, not a terrain type, because a vein cell is land.
  - They come from their own stream (`seed ^ veinSalt`) after the terrain is validated, and are turned with the symmetry like everything else.
- **Saves.** `RunCheckpoint` gains an optional `veins` list, following the pattern of the tier and paid fields from spec 015. Schema 2 is unchanged.
- **Spawn limit.** The section 9 validation allows at most 100 spawns per night. It becomes 150. The living cap stays 100 (`RunSimulation`).
- **Policy scripts.** `Tests/EditMode/ReleasePolicyTests.cs` has kill-zone (Crossing), corridor (Ravine) and economy (Meadow), with fixed cells.
  - Veins are seeded, so the scripts get an item form `Mine vein<i>`: the i-th vein by `(z, x)` order, skipped if it can't be placed.
  - The pacing probe is `plans/evidence/030/PacingProbe.cs.txt` plus `probe-hook.diff`. It is re-applied for the gate, never committed.
- **HUD.** The build bar is two rows of four cards with hotkeys 1–8. The Mine becomes card 9 with hotkey 9, in two rows of five. The fit must be checked at 1280×720.

## 2. Expected behaviour (so the first probe isn't read as a bug)

Raids happen once per night per enemy. So each column stops at the first economy building beside its road, and up to five enemies stand on one cell face. That building should fall on most nights from about Night 3; Houses farther in stay safe once the column has raided. Whether this is enough to break the House ring is exactly what the D5 gate measures, so nothing is pre-tuned around it. Nights get longer: up to 5 s per enemy, plus queuing. The probe reports night lengths before and after.

## 3. Stages, each with a gate

0. **Baseline** (Editor closed, before any source edit).
   - Add the **house-ring** policy (tests only). It is the owner's layout on Crossing: Houses on the four Keep neighbours and then the ring around them, Archer Towers outside, economy perks first, no Walls, Cannons or traps.
   - Run the probe at `975c093` plus that policy: kill-zone, corridor, economy and house-ring, seeds 1–5 on their own orientations, active hero. Record wins, Keep damage per night, buildings lost per night and night lengths.
   - **Gate:** house-ring wins about 5/5 (this confirms the diagnosis). Full EditMode and PlayMode counts recorded.
1. **Veins** (Core).
   - Generation per D3 and validation on the board as played: 3 veins at Chebyshev ≥ 5 from the Keep, within 2 cells of a Day-1 open route, in distinct quadrants where possible, not on fords, the ring or named constraints, up to 20 attempts, then fewer.
   - Symmetry, then the save field.
   - **Gate:** EditMode, 50 seeds × 3 profiles (deterministic, all constraints held, at least 1 vein each); a save round trip; an old save loads with no veins. Legacy fingerprints unchanged.
2. **Mine** (Core and data).
   - The definition: 45 gold, HP 64, 30 gold a day.
   - Placement only on a vein, with the refusal reason "Mines need a gold vein".
   - Income line and forecast.
   - Tiers: +10 and +20 a day, and Stockade at tier III (raid damage halved).
   - Raider Archer priority (1) and Hardened Roofs.
   - **Gate:** EditMode for placement refusal, income, tiers and Stockade (the scenario hits a tier III Mine with a raid, and with a non-raid hit to compare).
3. **Raid** (Core).
   - `RaidOnContact` as in section 1, for Grunts, Runners and Brutes only.
   - Ends on the target's death, after the raid time (5 s [P]) or on the hero (v0.3).
   - The raid is a melee hit, so reflection and the aura apply. The route is unchanged.
   - Events `RaidStarted` and `RaidEnded`.
   - **Gate:** EditMode:
     - one raid that times out at 5 s and doesn't repeat that night;
     - one ended by its target's death;
     - Bomber, Raider Archer and boss never raid;
     - a route is identical with and without a raid (the same cells);
     - a hero strike ends the raid.
     Each test is checked by mutation: the raid time ignored, the once-per-night flag removed.
4. **Presentation** (Unity).
   - Vein ground decal (gold flecks), with a hover line.
   - The Mine model in `Art/Source~` (Blender), and its prefab.
   - Build card 9 and hotkey 9, laid out as 2×5.
   - A raid icon on the Day route preview for economy buildings beside a route.
   - Raiders face their target.
   - Capture set `016`: veins on each profile, a Mine, a raid.
   - **Gate:** PlayMode:
     - place a Mine on a vein by input;
     - an enemy raids a House in the release scene.
     Full PlayMode; captures at 1280×720 and 1920×1080.
5. **Spawn limit and the pressure gate (D5).**
   - Limit 150.
   - Give the economy policy Mines (at least two), and give kill-zone and corridor their nearest vein if they want one.
   - Probe, then tune in the D5 order: wave counts on Nights 4–12, then enemy HP per kind, then raid time, then Mine income and price. At most 4 rounds; each one is a full probe and a section 16 row.
   - **Gate (D5):**
     - house-ring wins ≤ 2/5;
     - two other strategies win ≥ 3/5 each, one of them with ≥ 2 Mines;
     - in every won run, the Keep loses HP or a building is destroyed on at least one night from Night 6.
     The recorded-win seeds in `ReleasePolicyTests` are updated to the new results, with the reason in section 16.
   - If the gate fails after 4 rounds, stop and report to the owner. Don't loosen the gate.
6. **Close.**
   - Merge into `002-game-rules.md` v1.21; archive spec 016; update `specs/README.md`, `plans/README.md` and this plan's results.
   - Release build, full EditMode and PlayMode, a muted full-run capture, and a stress pair (raids add holds, not new work per step, so expect noise).
   - The owner's live check.

## 4. Risks

- **Houses become useless.** If every House is lost, the economy becomes Mines only. The gate's economy strategy and the owner's run check this.
- **Harder, not more varied.** The gate measures win rates, not variety. The owner's live check across several seeds is the acceptance that matters.
- **Night length.** Raids and queues add waiting, which the owner already finds tedious. If nights grow by more than about 15% on average, report it before tuning further.
- **HUD width.** Two rows of five cards at 1280×720 may crowd the speed row and START NIGHT. The fallback is narrower cards.
- **Recorded wins change.** Updating protected seeds is allowed here (the gate approves the new difficulty), but each change is listed with its reason.

## 5. Results (so far)

Stages 0–4 are done. Stage 5 stopped after round 1, as section 3 says to do when tuning cannot pass the gate.

| Check | Result |
|---|---|
| Baseline at `975c093` plus the house-ring script (`evidence/036/baseline`) | EditMode 155/155 (154 plus the probe), PlayMode 25 passed and 1 skipped. Probe: kill-zone 5/5, corridor 3/5, economy 5/5, house-ring 5/5. This confirms the diagnosis. |
| Raid, Mine and vein tests (`evidence/036/stage1`) | 12/12 pass. Two mutations each fail the raid timing test, and the second also fails the hero-ends-raid test: raid time ignored; once-per-night flag removed. |
| Full EditMode after stages 1–4 (`evidence/036/stage4`) | 167/167 |
| Full PlayMode after stages 1–4 (`evidence/036/stage4`) | 25 passed and 1 skipped. The release scene test now places a Mine on a vein with hotkey 9, refuses a Mine off a vein, shows one RAID tag by Day, and sees a Grunt raid the Mine at Night. |
| Probe round 0: raids and Mines, no tuning (`evidence/036/stage5/probe-r0.txt`) | kill-zone 5/5, corridor 3/5, economy 5/5 (2–3 Mines), house-ring 5/5. Mean nights are about the same as the baseline (89–95 s and 61–71 s). |
| Probe round 1: counts ×1.3 on Nights 4–12 with matching cadence, enemy HP ×1.2 (`probe-r1.txt`) | kill-zone 3/5, corridor 0/5, economy 0/5, house-ring 4/5. Pressure hurts every other strategy more than the sealed House ring. |

**Finding.** Raids never touch the ring's inner Houses, because the towers kill the columns first. What dominates is not House-as-Wall but the sealed Keep: every route converges on one breach point under a dense tower ring, which section 16 already recorded on 2026-10-16. More pressure strengthens that strategy's lead. The approved knobs (counts, HP, raid time, Mine values) cannot pass the D5 gate. A structural rule is needed, which is a spec change and the owner's call.

The pressure knobs (`ReleaseCampaign.PressureHpPercent` and `PressureCountPercent`) are kept at a neutral 100. Round 1's values made two EditMode tests fail because they pin the old night-4 counts and Grunt HP; these are provisional values, to be updated with whatever tuning is chosen.
