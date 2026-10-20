# Plan 013: visual polish toward AA (island, figures, animation, sky, UI)

Status: **IMPLEMENTED (agent), owner art review PENDING.** 2026-10-19. Presentation only: implements no new rules. It serves `specs/001-vision.md` (visual direction, readable battles) within the current rules of `specs/002-game-rules.md` v1.1 (§2 board and terrain, §11 decoration, §13 presentation, §14 performance, §17 hero view). No Core (simulation) file changed; balance, controls, saves and legacy profiles untouched. Every asset is generated as code by the agent: **a stronger presentation, not approved AA art.**

Built on top of the uncommitted plan 012 working tree; a snapshot of that tree before this work is stored as `refs/backup/pre-visual-013` (tracked files only).

## What changed

| Area | Change |
|---|---|
| Island | The square plateau, box cliff and flat sea plane are replaced by one ground mesh built per map from the played layout (`WorldView.Island.cs`): every land/Keep cell stays exactly flat at y = 0; water cells sink into river channels with sloped banks; fords dip under a water film; outside the board an irregular grass lip drops over a low cliff to a sand beach and the sea floor; the four side midpoints get a landing beach at the entry gates' base; rivers that leave the board run on to the sea; boulders on the cliff band. Bridges got piers down to the channel floor. |
| Ground shader | `Art/Shaders/Ground.shader` (URP lit, shadows, additional lights, SSAO, fog): grass/soil/sand/rock blended by vertex weights derived from the map (soil on banks, fords, forest floor and a trodden yard around the Keep; rock under boulders; sand on beaches and riverbeds), macro + close-range detail layers so it reads from both cameras. Forest and rock cells get round patches, not square plots. |
| Water | `Art/Shaders/Water.shader` for sea and rivers: depth from the same height field (shallows, deep tint, shore foam), two scrolling ripple normals on the presentation clock (frozen while paused), sun glint, sky fresnel; day/night colours from the view. The sea now runs to the horizon in both views. |
| Hero-view grass | `Art/Shaders/Grass.shader` + `WorldView.Grass.cs`: GPU-instanced true-scale clumps (~3k drawn) within 6.5 tiles of the hero camera, only on open land (never water, fords, rock, forest, banks, or a cell with a building or trap; rechecked on occupancy change), swaying on the presentation clock. Not drawn in the strategic view. |
| Sky | `HeroSky`: finer gradient dome with a warm sun / cool moon halo, low-poly cloud banks, a night star field, haze from 18 units. Dome, clouds and stars are active only while the hero view renders. |
| Figures | `Art/Source~/characters.py`: one articulated builder for the hero and all seven enemy kinds (tapered limbs, knees, hands, layered clothing, kind tells kept: Grunt hood and spiked club, Runner lean/scarf/dagger, Brute horned helm and huge club, Archer pointed hood/bow/quiver, Bomber's bigger banded keg with the `WickTip`/`TorchFlame` contract, Commander great helm/cape/banner, Golem stone masses with glowing core, hero kettle hat/plume/shield/sword/cape). Heights unchanged, so enemy scale, hero-view scale and bar heights stay valid. |
| Environment | Layered pines with visible branch undersides, fuller broadleaf crowns, flat-bottomed boulders; Repair Station crane now hoists a stone (it read as a gallows); plaster normal softened. |
| Animation | `CharacterRig`: knee joints, walk→run blend and stride derived from the figure's shown height, so planted feet keep pace with the ground in both views (true-scale figures run with long strides instead of sliding ~4×); idle breath; run lean and arm pump. Enemies: a contact step-in toward the building face or the hero sized from the actual gap, so blows land on the target in the hero view; bob/lunge/fall offsets scale with the figure. Hero: same gait, turn bank, step-in so the blade meets the nearest struck enemy at the simulation's reach (presentation offset only), a getting-up respawn. |
| Hero-view scale fixes | Dying figures, kill shards and hit flashes no longer render at strategic size in the hero view; tower arrows aim at the shown body. |
| Lighting | Night: darker cooler ambient, stronger directional moonlight (form and enemy visibility), warmer and brighter Keep and tower torchlight. Enemy cloth glow reduced (night clusters looked pink). |
| UI | Large headings (≥30 px) use an installed Windows serif (Palatino Linotype → Book Antiqua → Georgia; never shipped, falls back to the bundled font); the shared amber panel rule gets small diamond caps. Layout, sizes, hotkeys and information unchanged. Credits text updated. |

Simulation events stay authoritative: every change above reads simulation state and never writes it; no damage, reach, timing or hit count changed.

## Ownership and regeneration safety

New materials (`ArtIslandGround`, `ArtSeaWater`, `ArtRiverWater`, `ArtGrass`) are created by `ArtSetup` **only when missing**, and new `ArtDirection` fields are filled only when empty. The three blockout setups were not rerun in this plan; the existing `AuthoredArtSurvivesBlockoutRegeneration` test still passes. Rig joints added to `ArtSetup.Joints` (`ShinL/ShinR → LegL/LegR`, `OffHand → ArmL`). Provenance: `Art/credits.md`.

## Verification

- **Before/after captures**: new `-capture-set 013` added to `CaptureSequence` first and built into `Builds/Baseline013` (plan 012 tree + that script only). Same seeds, framing and game states: per profile (seed 7) strategic Day / placement ghost / Night combat and hero view Day/Night toward shore and Keep; the slice close in both views, low and high hero angles, a tower right behind the hero camera, a hero swing at an engaged Grunt at night; the dense 40-attacker fight in both views; all nine buildings in a row; all seven enemies passing the hero. Captured offscreen at 1280×720 and 1920×1080 from batchmode builds. Sheets: `plans/evidence/013/compare/` (1080p), finals in `after/`, logs alongside; 720p sheets in `TestResults/013/compare/` (not committed).
- **Agent's own inspection** (not owner approval): silhouettes distinct per kind at default zoom; routes, gates, chevrons, BREACH label, placement grid, ranges unchanged; HUD legible at both resolutions; hero ring and scale preserved; camera obstruction response still rises/pulls in at the tower; main-light shadows present in these batchmode builds.
- **Menus and screens with the new heading font**: the default release set (`-capture-set 008`: title, Day 1 per map, ghosts, threats incl. the Bomber fuse, reward, final-siege announcement, help) rerun on the final build at 1280×720 and 1920×1080 (`TestResults/013/menus`, not committed): no heading clips or wraps. Settings and credits panels were not in that set.
- **Tests (fresh, batchmode)**: EditMode **116/116** (115 existing + 1 new), PlayMode **21/21** + 1 skipped (motion probe, as before). New test `IslandTerrainKeepsTheBoardFlatAndSinksOnlyWater`: for every profile and seeds 1–3, every non-water, non-crossing board cell stays exactly flat, water sits below the river surface, and each gate stands on its landing. It protects the one new behavioural risk (terrain bending the played board or floating gates).
- **Performance**: see below.

## Performance

Reference machine (Ryzen 5 3600, GTX 1660 SUPER, 32 GiB, D3D12), non-development builds from batchmode, 1920×1080 windowed, VSync off, 100-enemy stress fixture, 10 s warm-up + 60 s capture, Editor closed, eight runs alternating baseline/final and strategic/`-hero-camera` (files in `evidence/013/perf/`):

| Run | Baseline013 strategic | Final strategic | Baseline013 hero | Final hero |
|---|---|---|---|---|
| Round 1 median / p95 / p99 ms | 4.03 / 6.55 / 9.44 | 3.83 / 4.96 / 6.52 | 4.84 / 5.46 / 5.80 | 4.58 / 5.54 / 6.26 |
| Round 2 median / p95 / p99 ms | 3.79 / 4.48 / 4.86 | 3.71 / 4.55 / 5.10 | 4.86 / 5.46 / 5.82 | 4.50 / 5.30 / 5.69 |

Gate (§14): strategic median ≤ 16.7, p95 ≤ 20, p99 ≤ 33.3 ms — **pass** with large margin. Hero view within 1.5× of strategic in the same session — **pass** (final 1.20× and 1.21×). The final build is about 0.1–0.3 ms *faster* than the baseline: the board lost several primitive cubes/planes and the always-on sky dome, which outweighs the island meshes, water shader and ~3k instanced grass clumps (one 58 ms spike in final hero round 1, none in round 2). Limits: one session, noise about ±0.4 ms; `-hero-camera` renders through the hero camera from a fixed point at strategic figure scale (pre-existing fixture behaviour), with grass drawn.

## Owner decision: one figure scale (2026-10-19, after the first report)

The owner ruled that figures must have the same scale in both views (the hero view may differ in detail, never in proportions), chose true scale (0.27 of the model size) and kept the rules unchanged. `PrototypeBootstrap.FigureScale` now applies in both views; the burn flame and Bomber fuse spark follow the figure's height at their old size; the hero's cyan ring keeps its world size. Spec `002-game-rules.md` §17 updated (v1.2). Fresh: EditMode 116/116, PlayMode 21/21 + 1 skipped; 720p captures in `TestResults/013/scale1` (not committed). The before/after sheets and perf table above predate this change.

**Regression, measured in captures:** at default strategic zoom the figures are a few pixels tall; enemy classes no longer read by silhouette and a night column reads as dots (`scale1/1280x720-013-crossing-s3-night.png`, `-dense-s.png`). This conflicts with pillar 3 (readable battles) and §13. Mitigation is an open owner decision.

## Remaining limitations (honest)

- Generated art, not artist-made; owner art review and a live, real-speed look at the new gaits, step-ins and grass are **pending**. Stills cannot show foot sliding; the stride math was checked, the motion was not watched.
- Buildings kept their plan 011 models (only the Repair Station crane and plaster changed); no LODs added (orthographic strategic camera, ~4k-tri buildings).
- Hero-view grass ignores point lights: by night, near the Keep's torchlight, blades stay moon-cool on warm-lit ground.
- The heading serif depends on fonts installed with Windows; licensing of runtime use of OS fonts is an owner decision (nothing is shipped). The settings and credits panels were not captured.
- At night the carved rivers render nearly black from the strategic view (readable as blocked, but flat).
- Grass, sky, clouds and stars are not drawn by the MVP or prototype scenes' strategic views (by design); MVP scenes get the island ground and water too (shared content) but were not re-captured.
