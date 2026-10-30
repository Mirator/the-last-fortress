# Plan 037: land economy and map overhaul

Status: **PAUSED 2026-10-30 at stage 6: the D10 gate failed after 4 tuning rounds; owner decision needed (section 4).** Stages 0–4 done. Spec 017 D1, D5, D6, D7 and D9 were decided on 2026-10-30; approving this plan confirmed D2-a, D3, D4-a, D8 and D10 as recommended. Priority P1. Effort XL (about 3–5 weeks, staged). Implements **`specs/017-land-economy-and-maps.md` v0.4**. Builds on plan 036: raids, gold veins and the Mine are implemented, tested and uncommitted; its pressure gate moves to spec 017 D10.

The owner chose the options that keep stage 3 in the plan (a Keep that varies by map, generated entries) and a 20 × 20 board changed in place.

## 1. Pre-check (2026-10-30)

- **Board shape in the code.**
  - The board size is `GridCoordinates.Size = 20`, read in 68 places across 11 runtime files and 9 test sites.
  - The Keep is `GridState.KeepCell = (10,10)`, `static readonly`, used in 28 runtime places and 14 in tests.
  - The four side-midpoint entries (`MvpCampaign.West/East/North/South`) are used in 43 runtime places and 51 in tests.
  - D6-a and D7-a turn the Keep cell and the entries into per-map values. The MVP and legacy profiles keep the constants.
- **Income.** `EconomyRules.IncomeOf` is a single per-building rule, used by the payment, the forecast and the HUD. The land rule replaces its House and Farm branches under `release-rules-4` only.
- **Maps.** `MapProfiles` uses a skeleton plus N variation cells, validated with `RoutePlanner`. Veins (`PlaceVeins`) already show how a feature list is stored on `TerrainMap` and in the save.
- **Policy scripts.** `ReleasePolicyTests` uses fixed cells turned with the map's symmetry. Generated maps need adaptive scripts (spec 017 section 4).
- **Probe.** `Tests/EditMode/Probe036.cs` and its `BeforeNight` hook are temporary and never committed.

## 2. Stages, each with a gate

Owner, 2026-10-30: stage 2 runs before stage 1, because its gate is measured before any rule change.

0. **Close plan 036 (stages 1–4).**
   - Merge raids, veins, the Mine and the 150 spawn limit into `002-game-rules.md` v1.21, and archive spec 016 with "gate superseded by spec 017".
   - Remove the probe hook. Commit when the owner says so.
   - **Gate:** full EditMode and PlayMode on the commit.
1. **Land income** (Core).
   - Fertile cells as a terrain flag, an optional save list like the veins.
   - The D1/D2 rule in `EconomyRules`, starting with the D3 values.
   - Tiers, Market Square, Efficient Farming and the Farm + House bonus re-expressed on top.
   - Until the new generator exists, today's three profiles get a provisional fertile layer, so the stage can be measured on its own.
   - **Gate:** EditMode tests:
     - a packed Farm earns its base, and a Farm in the open earns base plus fields;
     - an enemy or a trap on a cell: the enemy does not change a field, a trap removes it;
     - a shared cell counts for each neighbour (D2-a);
     - a save round trip.
   - Re-run the probe and record what the land rule alone does to house-ring.
2. **Adaptive policy scripts** (tests only).
   - kill-zone, corridor, economy and house-ring as heuristic builders that read the map: the Keep, entries, routes, fertile clusters and veins.
   - **Gate:** on today's maps, before any rule change, they come within one seed of the fixed scripts' wins (or the gap is explained), so the new probe can be trusted.
3. **Per-map Keep and entries** (Core refactor; only with D6-a and D7-a).
   - `KeepCell` and the entries move onto the map and the run.
   - Routing, placement, the reserved ring, waves (the table names entries by index), symmetry, saves, the camera framing and the HUD entry tags all read them from there.
   - The constants remain for the MVP and legacy runs.
   - **Gate:** the full suites pass with today's maps (the same Keep and entries), and legacy fingerprints are unchanged.
4. **Map generator** (Core).
   - Four recipes (D8), each placing regions in turn: meadows (fertile), forests, ridges, rivers with fords, then the Keep, entries, roads and veins.
   - Validation with the route planner, plus the fairness bands.
   - Its own seeded streams.
   - **Gate:** EditMode over 100 seeds per recipe:
     - deterministic;
     - every entry has an open route;
     - the fairness bands hold;
     - no isolated pockets;
     - fallbacks under 2%.
5. **Presentation** (Unity).
   - Fertile ground, generated roads, a Keep off centre, varied entry gates.
   - The ghost and selection preview of income and counted fields (D4).
   - The title shows the recipe; the camera starts on the whole board.
   - **Gate:** PlayMode:
     - a Farm's preview matches its dawn income;
     - a run on each recipe starts and plays one night;
     - captures of each recipe at 1280 × 720 and 1920 × 1080.
6. **The D10 gate and tuning.**
   - Probe seeds 1–5 × 4 recipes × 4 scripts, active hero.
   - Tune D3 values, then enemy counts and HP (the knobs kept from spec 016), up to 4 rounds.
   - If it fails, stop and report.
7. **Close.**
   - Merge into `002-game-rules.md`, archive spec 017, update the indexes and this plan's results.
   - Release build, a muted full-run capture per recipe, a stress pair.
   - The owner's live check across several seeds.

## 3. Risks

- **Stage 3 is the widest change.** Doing it after stages 1–2 keeps the land rule measurable on its own. If D6-b and D7-b are chosen, it drops out and the effort falls to about 2–3 weeks.
- **Script quality.** A weak heuristic builder can fail the gate for the wrong reason. Stage 2's own gate guards against this.
- **The probe gets slower.** 80 runs per round instead of 20. Mitigation: run rounds in the background, and use 3 seeds for a quick read before the full 5.

## 4. Results (so far)

| Stage | Result |
|---|---|
| 0. Close plan 036 | Rules v1.21 merged (raids, veins, the Mine, spawn limit 150); spec 016 archived with its gate superseded by spec 017 D10; probe hook removed. On the committed tree (the temporary probe moved out of `Assets`): EditMode 166/166, PlayMode 25 passed and 1 skipped (`evidence/037/stage0`). |
| 2. Adaptive policy scripts (run before stage 1, owner 2026-10-30) | `Tests/EditMode/AdaptivePolicies.cs`: kill-zone, corridor, economy and house-ring builders that read the Keep, every night's entries, their Day-1 routes, terrain and veins (fertile land = all land until stage 1). Pacing and perks copied from the fixed twins. Probe (`evidence/037/stage2/probe037.txt`; active hero, seeds 1–5 on their own orientations, rules unchanged), fixed vs adaptive: kill-zone 5/5 vs 5/5, corridor 3/5 vs 4/5, economy 5/5 vs 5/5 (3 Mines each), house-ring 5/5 vs 5/5. **Gate passed** (all within one seed; no script tuning). The fixed results equal plan 036's round 0, so the `Play` hook changed nothing for them. New test `AdaptivePolicyTests` (plans legal, repeatable, corridor never seals an open entry; 3 profiles × 8 orientations × 2 seeds) passes. Not run: full EditMode (only test code changed; the stage 3 suites run in full). Known limit: the active-hero script still guards `GridState.KeepCell`; stage 3 moves it to the run's Keep. |
| 1. Land income | `release-rules-4` House 4 + 2 and Farm 8 + 3 per free fertile edge neighbour (`BuildingDefinition.LandIncome`, `EconomyRules.Fields`), Market Square +1 per field, the Farm + House link, Market hall, tiers and Efficient Farming on top (spec 017 v0.3). Fertile cells are an optional terrain list saved as `fertile` with `fertileListed`; a save without it counts every free land cell. Provisional layer for today's profiles: 4 seeded meadows (6 on Meadow), Manhattan radius 2, until the stage 4 recipes. Card, details and perk texts follow the rule. **Gate passed:** full EditMode 174/174 (4 new `LandIncomeTests` cases: packed vs open Farm with fertile vs plain land; trap removes a field, a raider on it does not; shared fields and perks; save round trip and older saves). Mutations: traps ignored fails the trap test; the fertile list ignored fails the fertile-land case and the round trip (`evidence/037/stage1/mutation`). Probe with the land rule alone (`evidence/037/stage1/probe037.txt`), fixed / adaptive: kill-zone 5/5 / 5/5, corridor 1/5 / 4/5, economy 3/5 / 3/5, house-ring 5/5 / 5/5. **The land rule alone does not break house-ring**: its towers carry the defence whatever its Houses earn, and the economy strategy lost two seeds. Expected; stages 3–4 (a Keep off centre, generated entries) and the stage 6 tuning are where D10 is decided. |
| 3. Per-map Keep and entries | `TerrainMap.Keep` and `TerrainMap.Entries` (index order), saved as optional `keepCell` and `entries`; `GridState.KeepAt` feeds routing, Keep placement, attack cells, hero spawn and save checks; veins and meadows read the map's Keep and entries; `ReleaseCampaign.Create(seed, map)` builds nights by entry index with the v0.4 rule (`NightEntries`); entries are named by their side; camera framing, Keep arrow, enemy facing, ground detail and the Keep yard use the run's Keep. Today's profiles keep the centre and their four turned side midpoints. Spec 017 v0.4 records the owner's answers (3–5 entries per night, fairness values, the four recipes). **Gate passed:** EditMode 181/181 (new `MapKeepAndEntriesTests`: entry mapping on 3, 4 and 5 entries; an off-centre Keep with three entries plays, validates and round-trips through a save); the probe reproduced stage 1 seed for seed, so behaviour on today's maps is unchanged (legacy fingerprint tests pass); PlayMode 25 passed and 1 skipped after one fix (the prototype scene has no map: its Keep yard falls back to the centre). Roads and entry gates are still drawn from side midpoints; stage 5 generates them. |
| 4. Map generator | `Runtime/Core/MapRecipes.cs`: River Valley, Highlands, Forest Edge and Open Plains paint their features, then a Keep in the central 8 × 8, 3–5 entries (ring, no corners, Manhattan 10–21 from the Keep, 4+ apart, **at least two sides**: a generator choice beyond the spec, after a dump showed four of five entries on one side), a pinch of two short rock walls across one route when no route has a natural choke, veins, then meadows. Validation with the route planner and the v0.4 fairness values; 40 attempts from `seed ^ RecipeSalt`, then a plain fallback. Recipe maps load from saves (`MapProfiles.IsKnown`), and the map label names them; the title screen still offers the old profiles until stage 5. **Gate passed** (`MapRecipeTests`, seeds 1–100 per recipe, `evidence/037/stage4/recipes037.txt`): deterministic; every generated map passes every rule; fallbacks 0/400; fertile cells River Valley 50–73, Highlands 57–75, Forest Edge 53–73, Open Plains 64–86 (one meadow added to the first three after the first run sat at the 50 floor); mostly first-attempt maps (mean 1.1–1.2 attempts). Full EditMode 183/183. Text dumps of seeds 1–2 per recipe in `recipes-dump.txt`. The pinched chokes read as two parallel rock bars in the dump; judge them rendered in stage 5. |
| 6. D10 gate and tuning (run before stage 5, owner 2026-10-30) | **FAILED after 4 rounds; stopped as the plan requires.** Probe: adaptive scripts on the four recipes, seeds 1–5, active hero (`evidence/037/stage6/probe-r0..r4.txt`). Wins out of 5 as kill-zone / corridor / economy / house-ring, River Valley; Highlands; Forest Edge; Open Plains. **r0** (no tuning): 4/3/5/5; 5/4/4/5; 5/2/5/5; 5/4/4/5 — everything wins. **r1** (Nights 4–12 counts ×1.3 with cadence, enemy HP ×1.2; pressure before any D3 change because income cannot make an all-winning field lose): 3/2/1/4; 5/2/1/2; 4/1/1/1; 5/2/2/2. **r2** (+ D3 House 4 + 3, Farm 8 + 4 per field): 5/3/4/4; 5/2/2/2; 5/1/4/1; 5/4/2/2 — Forest Edge and Open Plains pass the per-recipe rules. **r3** (+ Nights 9–12 counts ×1.6): 5/2/4/2; 5/2/1/1; 5/1/3/1; 5/2/2/2 — house-ring ≤ 2/5 on every recipe for the first time; River Valley and Forest Edge pass. **r4** (late counts by kind: Brutes and Bombers ×2.0, Grunts and Runners ×1.4, Archers ×1.6): identical to r3. **What fails:** (a) a second strategy ≥ 3/5 on Highlands and Open Plains; (b) the most-winning strategy never differs: kill-zone (a Wall-sealed Keep ringed by Towers and Cannons) wins 5/5 on every recipe in every round, on 400–1,300 gold a run. **Findings:** the land rule works against Houses used as walls (house-ring capped once pressure rises), but not against the sealed compact Keep itself: every route still converges on the Keep, so a sealed core covered by a few Towers and Cannons beats the same pressure that kills spread layouts. Losses fall on Nights 6–9, so late-night knobs change nothing (r4 = r3). Checks and confounds: (1) the losers die broke: in round 4's 37 defeats the gold left on the last Day is 0–70 and never more than 75 banked on any Day (`probe-r4-full.txt`), so unspent land income does not explain the failure; (2) **the economy script is not the strategy spec 017 section 4 defines**: copied from its fixed twin, it opens with four Towers and four Walls sealing the Keep (defence first), so three of the four probed strategies seal the Keep and only corridor keeps routes open, and kill-zone was measured against two other sealed layouts; the stage 2 check validated the scripts only under flat income on the old maps; (3) round 4 targeted Nights 9–12 before the loss nights (6–9) were looked at, and moved no result; whether it counts as one of the four rounds is the owner's call. Heuristic scripts may under-play spread layouts in general (spec 017 section 6). The tuning is reverted to the committed values (pressure 100%, House 4 + 2, Farm 8 + 3); round 4's diff is kept in `evidence/037/stage6/round4-tuning.diff`. Stages 5 and 7 are not started; owner decision needed. The committed tree (`4301fec`) passes EditMode 184/184 (including the untracked probe). |
