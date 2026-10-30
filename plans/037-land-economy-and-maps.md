# Plan 037: land economy and map overhaul

Status: **APPROVED 2026-10-30, IN PROGRESS.** Spec 017 D1, D5, D6, D7 and D9 were decided on 2026-10-30; approving this plan confirmed D2-a, D3, D4-a, D8 and D10 as recommended. Priority P1. Effort XL (about 3–5 weeks, staged). Implements **`specs/017-land-economy-and-maps.md` v0.2**. Builds on plan 036: raids, gold veins and the Mine are implemented, tested and uncommitted; its pressure gate moves to spec 017 D10.

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
