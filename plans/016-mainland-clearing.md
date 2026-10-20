# Plan 016: mainland clearing in the mist instead of an island

Status: **IMPLEMENTED (agent); owner look PENDING.** 2026-10-20. The owner approved the plan with one change: enemies walk out of an edge mist along the last stretch of road, and the world beyond the clearing is hidden in that mist. While it was being built the owner also dropped the entry gates (see Results).

Implements: `specs/001-vision.md` v2.0 → v2.1 and `specs/002-game-rules.md` v1.4 (§17 hero view presentation and the presentation notes). The owner approved the direction; the spec text below is edited in the same change as the code.

## Why

At real scale (plan 014, 1 tile ≈ 8 m) the island is about 160 m across, about the size of a large village. Enemies come ashore on four beaches with nothing behind them, so they appear out of the sea for no reason. The owner asked for a mainland clearing: the fortress stands in a forest clearing whose surroundings fade into mist, and enemies come out of the mist along four roads.

## Spec changes (approved, made with the code)

| Where | Now | New |
|---|---|---|
| `001-vision.md`, Visual and audio direction | "low-poly 3D on a small island" | "low-poly 3D in a forest clearing; the world beyond it is lost in mist" |
| `002-game-rules.md` §17, hero view presentation | "a wide sea to the horizon" | "the forest edge fading into mist" |
| `002-game-rules.md`, presentation | An enemy appears at its entry cell when it spawns | Also: presentation only, each enemy is drawn walking out of the mist along its road and reaches its entry cell exactly at its spawn tick. The simulation is unchanged |
| `specs/archive/001-game-design.md`, `006-third-person-hero.md` | island and sea | Leave unchanged; they are history |

## Scope

Presentation only. The simulation, rules, balance, routes, saves and the 20×20 board are untouched. The board stays exactly flat with the same cells, entries and terrain types. The walk-in only reads the known spawn schedule; it adds nothing to the simulation. The work lives in:
- `WorldView.Island.cs` (ground and water) and `WorldView.Grass.cs`;
- `WorldView.cs` (enemy views);
- `HeroSky.cs`;
- `RouteOverlay.cs` (gates);
- the shaders and the art sources.

## Design

1. **Ground.** Replace the coast profile (`IslandShape.Coast`: lip, cliff, beach, sea floor) with a mainland surround:
   - a verge strip of 1–2 tiles at y = 0 around the board;
   - forest floor rising gently, 0 → about +0.3 tile;
   - a coarse far ring of plain ground under the mist, so neither camera ever sees the clear colour.

   Nothing beyond about 5 tiles from the board needs detail, because the mist hides it (item 6).
2. **Four roads.**
   - A dirt track (soil weight in the ground shader) runs from each side midpoint about 5 tiles out and bends into the mist.
   - Gates stand at the board edge on the road at y = 0, not on a landing at −0.26. Remove `LandingY` and the −0.26 gate offsets in `RouteOverlay` and `WorldView`.
3. **Forest belt.**
   - Pines and broadleaves come from the existing environment models.
   - The belt is sparse at the verge, dense from about 2–3 tiles out, and reaches into the mist. Road corridors stay clear, about 1.5 tiles wide.
   - Camera-facing sides keep a low verge and shrubs; tall trees start about 4 tiles out there.
   - Draw the trees GPU-instanced, like the hero-view grass, not as one GameObject per tree. Starting budget: about 1,000 trees.
   - No distant hills or silhouette rings: the mist replaces them.
4. **Rivers.**
   - Crossing-profile rivers that reach the board edge carry on as streams that wind into the forest and vanish in the mist, instead of falling to the sea.
   - The water stays at `RiverLevel`, with carved banks and reeds.
   - Fords, bridges and the on-board channel are unchanged.
5. **Edge props.** Replace beach boulders and pebbles with stumps, bushes, fallen logs, a few rocks and fence remnants along the roads. Reuse the existing models where possible; add new low-poly models (stump, log, fence) to `environment.py` at real size.
6. **Edge mist.**
   - A soft band of mist that starts about 3 tiles beyond the board and is opaque by about 5 tiles, in both views. The forest edge shows through its near side and fades out; nothing behind it is visible.
   - **Technique.** Unity's built-in fog depends on camera distance and does nothing useful for the orthographic strategic camera. Use a world-space radial term (distance from the board edge) in the project's own shaders (Ground, Water, Grass, and a tree shader if needed). Cover objects drawn with URP Lit (props, enemies inside the mist) with a few soft, noise-textured transparent layers: stacked horizontal sheets for the strategic view, and a ring of vertical curtains for the hero view.
   - **Spike first (about half a day).** Compare sheets plus curtains against a custom tree and enemy shader with the same radial term. Pick the one that hides cleanly in both views at acceptable cost.
   - **Motion.** Slow drift on the presentation clock, frozen while paused.
   - **Day and night.** Day: pale warm-grey. Night: dark blue-grey, with a faint torch glow at each gate so the four exits stay readable.
   - **Hero view.** `HeroSky`: remove `FarSea`. Below the horizon there is only mist, under the sky dome. Hero-view fog is tinted like the mist so the horizon and the edge mist match. The strategic background takes the mist colour.
7. **Enemies emerge from the road.** The spawn schedule is known (`WavePlan` spawn entries). So the view draws each enemy before it spawns, walking from inside the mist along its road at its own walking speed. It is timed to reach its entry cell exactly at its spawn tick. There it hands over to the real enemy view with no jump in position or gait phase.
   - **Before its spawn tick it is not an enemy yet.** No HP bar, no inspection, no click target, no route line. Towers cannot shoot it, because the simulation does not know it.
   - **Walk length.** About 3 tiles of road, mostly inside the mist. Spawns too early to fit the whole walk (time 0, at night start) begin partway, still inside the mist.
   - **Timing.** Walk-ins pause with the night and follow game speed: the presentation clock comes from the simulation tick, not wall time.
   - **Bosses.** They walk in the same way. Their arrival cue stays at the spawn.
8. **Night.** The forest edge is near-black, the mist dark, and torches at the four gates show where enemies come from.
9. **Title screen and map-less runs.** A flat clearing in the mist replaces the flat island (`BuildIsland(null)`).
10. **Naming.** Rename `IslandShape`, `BuildIsland` and `HasIsland` to `TerrainShape`, `BuildTerrain` and `HasTerrain` as a separate mechanical step at the end, so the behaviour diff stays readable.

## Risks

- **Mist hiding the gates or board.** The mist must start beyond the gates and never cover a board cell or a gate. Check every profile in both views, including the whole-board zoom (Z).
- **Walk-in drift.** A pre-spawn figure that arrives early or late, or in the wrong place, would pop at handover. Timing it from the simulation tick keeps pause, 2×/3× speed and loading a Day save from desynchronising it.
- **Trees covering the board in the strategic view.** A 13 m pine is about 1.6 tiles tall. The low verge on the camera-facing sides addresses this; confirm it at every zoom.
- **Hero camera in the forest.** `HeroCameraRig.ObstacleHeight` covers on-board forest only. The camera can still swing outward over the edge trees, so it needs the same pull-in there.
- **Performance.** The hero view looking outward into the trees and mist is the new worst case, because transparent layers add overdraw. Measure it as its own capture shot against the gate: strategic median ≤ 16.7 ms, p95 ≤ 20, p99 ≤ 33.3; hero ≤ 1.5× strategic.
- **Readability.** Green trees next to green enemy cloth. Check the Grunt and Runner silhouettes against the treeline and the mist in both views.

## Tests

- **Adapt `IslandTerrainKeepsTheBoardFlatAndSinksOnlyWater`** (SerializedDataTests). Board cells stay exactly flat, water stays below the river surface, and each gate stands on its road at board level instead of on a landing.
- **Add one assertion** that no surround tree lies on the board or in a road corridor; a tree blocking an entry would hide where enemies come from.
- **Walk-in handover.** Extend the existing PlayMode motion check (`MvpSceneTests`): at its spawn tick, an enemy's drawn position equals the pre-spawn figure's last position (no pop), and a pre-spawn figure cannot be inspected.
- No other new tests. Rules and simulation do not change.

## Verification

- Before and after captures, set 013, for all three profiles, by day and night, in both views. Add a hero-view shot looking outward into the mist, and a motion flipbook of enemies emerging from it.
- Perf: two alternating rounds with the Editor closed, including the new outward hero shot.
- EditMode and PlayMode fresh runs.
- An owner look at the running game, especially the mist and the walk-ins at real speed.

## Decisions (owner, 2026-10-20)

1. Spec text: approved, with the mist added.
2. One surround for every map; per-map accents later only if the maps look alike.
3. Camera-facing sides: low verge and shrubs.
4. Enemies walk out of the mist along the last stretch of road (item 7). The world outside the clearing is hidden behind mist (item 6).

## Effort

Medium to large, about the size of plan 013's island work. The mist removes the far-terrain work. The new risks are the mist technique (spike first) and the walk-in handover.

## Results (2026-10-20)

### Mist spike

Neither planned option was built. A third one replaced both: **one full-screen pass** (`EdgeMistFeature` on `PC_Renderer`, shader `Hidden/TheLastFortress/EdgeMist`).
- **How it works.** The pass runs after opaques and the sky, and before transparents. It rebuilds each pixel's world position from the depth texture and blends the mist over the frame. It needs no colour copy, so it works on the 4× MSAA target. The same rule covers every opaque object, whatever its material, in both the orthographic and the perspective camera, so there is no transparent overdraw and no custom tree or enemy shader.
- **Shape.** The mist thins with height, and the height it reaches grows with distance (`MistShape`: base 1.2, +0.12 per tile). So the hero view's horizon disappears into it while the sky above stays clear.
- **Colour.** It takes the hero sky's horizon colour (`WorldView.MistColor`), so the mist and the hero-view horizon match by day and night.
- **Off switch.** It is off (no pass) where there is no authored terrain.
- **Spike lessons.** The first version ran after transparents and misted the capture's camera-space HUD. Moving it before transparents keeps the HUD, route lines and effects on top. The far-island props stuck out above the mist and are no longer placed on the mainland.

### Built

- **Ground.** The coast became a verge, then forest floor rising gently. The board stays exactly flat.
- **Roads.** Four dirt roads leave the side midpoints and bend away into the mist (`IslandShape.RoadDistance`). There is no sea.
- **Rivers** run on in their channel into the mist.
- **Forest belt.** Pines and broadleaves are GPU-instanced (`WorldView.Forest.cs`), and only near trees cast shadows. Bushes only, on the two camera-facing sides, up to 3.8 tiles out. Roads and river outlets stay clear.
- **Edge props.** Bushes and rocks on the verge.
- **Enemies walk in** (`WorldView.Arrivals.cs`): 6 tiles of road at their own speed, timed from the simulation tick. At spawn the real view takes over their heading and gait phase, and eases into its lane offset. A spawn at tick 0 appears at its entry.
- **No gates** (owner, 2026-10-20: a gate does not make sense on an open road). An entry is marked by its coral pad on the entry cell and a torch light on the road mouth that glows at Night.
- **Lighting.** The strategic background and the hero fog take the mist colour.
- **Hero camera** pulls in for the off-board forest (`HeroCameraRig.ForestHeight`).
- **Capture set 013** gained a whole-board shot by day and by night, and a close night shot of the walk-ins.
- **Perf.** A new `-hero-camera-out` vantage looks from the west edge into the forest and mist.

### Verification (fresh)

- **EditMode 119/119.** `IslandTerrainKeepsTheBoardFlatAndSinksOnlyWater` now checks that each gate position is at board level on its road, and that no forest plant is on the board or the road.
- **PlayMode 22/22 + 1 skipped.** New: `EnemiesWalkInAlongTheRoadAndTakeOverWithoutAJump`. Three seconds before its spawn the enemy is out on the road, and the real enemy appears within 0.06 tile of where its walk-in was last drawn.
- **Perf**, visible window 1920×1080, two alternating rounds, stress fixture (`evidence/016/perf`). All pass the gate (16.7 / 20 / 33.3 ms, hero ≤ 1.5×). Plan 015 measured strategic medians of 4.72 / 5.19 ms.

| View | Median (ms) | p95 (ms) | p99 (ms) |
|---|---|---|---|
| Strategic | 5.03 / 5.13 | 6.52 / 6.86 | 7.68 / 9.16 |
| Hero, inward | 6.22 / 6.11 (1.24× / 1.19×) | — | 9.14 / 8.31 |
| Hero, outward | 5.25 / 5.20 | — | 7.70 / 8.08 |

- **Captures** in `evidence/016/captures`: whole board day and night for three maps, hero view day and night, and walk-ins at night.

### Not done

- **New stump, log and fence models.** Not needed any more: nothing stands outside the board (owner change below).
- ~~The mechanical rename (item 10)~~: done in the tidy-up below.
- **Unchecked:**
  - the title screen's clearing and the MVP scenes, which share the content;
  - the hero camera's forest pull-in;
  - the walk-ins at real speed (stills only).
- **Roads read faintly by day.**

### Owner changes after the first look (2026-10-20)

- **No forest belt, no edge props.** Outside the board there is only open ground fading into the mist, as preparation for a possibly larger field later. `WorldView.Forest.cs`, the edge props and the hero camera's off-board forest pull-in were removed; roads, rivers, walk-ins and the mist stay.
- **Hero view aiming**, ready for a future bow. A crosshair at the screen centre (while the hero is alive and the cursor is captured), and mouse forward now looks up by default (Invert look gives the old behaviour). The PlayMode hero-view test asserts the new direction and the crosshair.
- Starting the night from the hero view was already possible with **Enter** (the hint line says so).

### Tidy-up (2026-10-20)

Leftovers from the island (plan 013), the gates and the forest belt, removed or renamed. No behaviour change intended.

- **Renamed.**
  - Island code to terrain: `TerrainShape`, `BuildTerrain`, `HasTerrain`, the file `WorldView.Terrain.cs`, the material `ArtTerrainGround` (same GUID) and the test `TerrainKeepsTheBoardFlatAndSinksOnlyWater`.
  - Gates to entry markers: `RouteOverlay.RoadMouth`, `EntryMarkerPosition`, `VisibleEntryCount`, entry pads and road torches, and the test `EntryMarkersStandAtTonightsEntriesAndChevronsFlowTowardTheKeep`.
  - The capture shots `h1-day-shore` and `h4-night-shore` are now `h1-day-edge` and `h4-night-edge`.
- **Removed.**
  - Runtime: the sea material path (`ArtDirection.sea`, `beach`, `seaWater`, sea colours, `WorldView.SeaMaterial`), the hero sky's far sea, the MVP beach ring and river outlets for an art build without terrain (no such build exists), and the far islands.
  - Art: the gate model (`ArtGate` FBX, prefab and Blender source) and the far islands (`ArtFarIslandA/B` FBX, prefabs, Blender source and their `far` tree variants).
  - Materials: `ArtSea`, `ArtBeach`, `ArtSeaWater`, `ArtWarBanner` and the five `ArtFar*` materials, with their FBX material remaps and the stale `ArtDirection.asset` fields.
  - Stale comments: sea, beach, shore, gate, forest belt.
- **Kept on purpose.**
  - The prototype scene's plain plateau and water plane, used when there is no art.
  - `ArtGround`, `ArtCliff`, `ArtRiver` and `ArtFord` with their `ArtDirection` fields. They are only reachable when art is present without the terrain materials, which no current build does. Removing them would be the next cleanup, but it touches the river and ford fallback in `WorldView.BuildWater`, so it is left for a separate change.
  - The capture set's name "013".
- **Verification after the tidy-up (fresh).** Release build, capture set 013 and the tests all ran. EditMode 119/119, PlayMode 22/22 + 1 skipped. The first attempt cut too much from `WorldView.cs` and did not compile; the lost methods were restored from the session's earlier read of the file and the last committed version, and then the build compiled cleanly. Perf was not re-measured; the change only removes work.

