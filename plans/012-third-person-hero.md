# Plan 012 — Third-person hero and switchable views

Status: **IMPLEMENTED** 2026-10-19, see [012-results.md](012-results.md) (planned at `4b5baba`). Priority P1. Effort L, staged (rough: 3-5 weeks for one agent-driven developer, most of it in Stages 3-4).

Implements [specs/003-third-person-hero.md](../specs/archive/006-third-person-hero.md) (archived as `specs/archive/006-third-person-hero.md` after implementation; rules now in `specs/002-game-rules.md` section 17) (version 0.1 with owner decisions 1-12, 2026-10-19) on top of [specs/002-game-rules.md](../specs/002-game-rules.md). The spec is the contract; this plan only orders the work and names files. If the code and the spec disagree in a way this plan does not cover, stop and ask the owner (root `AGENTS.md`).

Paths below are relative to `game/The Last Fortress/Assets/TheLastFortress/` unless they start with `specs/` or `plans/`. Line anchors are from `4b5baba` and will drift.

## Entry checks

1. Read root `AGENTS.md`, `specs/README.md`, spec 003 in full, and spec 002 §4, §5, §8, §12, §13.
2. `git status --short`; `git log --oneline -3`. Expect a clean tree at or after `4b5baba`.
3. Run the existing EditMode and PlayMode suites once to have a fresh baseline (106/106 and 20/20 at plan 011). Unity constraints on this machine: the open Editor and batchmode exclude each other; batch Unity runs need the sandbox disabled; a locked Windows session stalls the Editor; build release players from the Editor (batchmode builds lost main-light shadows).

## Stage 1 — Hero in the simulation (Core only, no Unity changes)

Goal: `release-rules-2` exists, the hero lives in `RunSimulation`, all spec §2 rules work and are tested, and nothing changes for `release-rules-1` or the MVP.

### 1.1 Rules version plumbing

- `Core/ReleaseCampaign.cs:12`: keep `RulesVersion = "release-rules-1"` as `RulesV1`; add `RulesV2 = "release-rules-2"`. `Create(...)` takes the rules version (default V2 for new runs). Today three places compare against the single constant: `RulesProfile.IsRelease` (`Core/RulesProfile.cs:23`), `CampaignConfig.IsRelease` (`Core/MvpCampaign.cs:30`), and `ReleaseCampaign.Create` (`:67`). Make `IsRelease` accept both, and add `HasHero` (true only for V2).
- `Core/RulesProfile.cs:41-46`: add a `TwelveNightsHero` factory (same definitions, pool, kinds, content version 2, schema 2).
- `Unity/PrototypeBootstrap.cs:159-161`: catalog order `[TwelveNightsHero, TwelveNights, SixNights]`, so New Run plays V2 and both older profiles still restore. `CreateCampaign` (`:383`) passes V2.
- `Tests/EditMode/ReleaseRulesTests.cs:266` pins the literal `"release-rules-1"`, not the constant.
- The simulation has no rules-version branching today. Gate every hero code path on `Campaign?.HasHero == true`, never on definitions (both profiles share them).

### 1.2 Hero entity and command

- `Core/Entities.cs`: add `Hero` (X, Z, PrevX, PrevZ as doubles like enemies; facing index 0..63; HP; `Alive`; `RespawnTick`; `SwingReadyTick`; `ProvokedArchers` not stored here, see 1.5).
- New `Core/HeroCommand.cs`: `readonly struct HeroCommand { sbyte Heading (−1 none, 0..15); bool Attack; byte Facing (0..63); }`.
- Movement uses an **integer unit-vector table** for the 16 headings (fixed-point, per step 180 units = 3.0 tiles/s), not `Math.Sin/Cos`, so results cannot drift between runtimes. Facing uses the same kind of table (64 entries) for the attack arc test.
- `RunSimulation.Step()` (`:610`) becomes `Step()` → `Step(HeroCommand.None)` plus `Step(HeroCommand)`. `Advance(seconds, command)` applies the same command to every step it runs, which keeps `CombatPhaseStressTests.EqualElapsedTimeInDifferentChunksGivesIdenticalState` valid for a constant command. Do not latch input per `Advance` call.
- `CaptureSnapshot` (`:1449-1479`) includes hero state, so determinism tests see it.

### 1.3 Spawn, collision, Day movement, building on the hero

- Spawn rule (spec 2.1): nearest walkable non-solid cell to the Keep by Manhattan distance, ties N, E, S, W, then `(z, x)`. Use `GridState.IsImpassableTerrain` (`Core/GridState.cs:39`), `IsSolid` (`:42-48`), `GridCoordinates.IsInBounds` (`Core/GridPos.cs:45`). Fords are plain land; traps are ignored.
- Collision: circle radius 0.3 against solid and non-walkable cells and the board edge, resolved axis-separated (X then Z) so the hero slides. Cover the diagonal-corner case explicitly.
- Day clock: `AdvanceDay(seconds, command)` steps hero movement only, at the fixed step; it is ignored outside Day. Nothing else advances.
- Building on the hero: in `RunSimulation.TryPlace` (`:225`) after a successful placement, if the footprint overlaps the hero's body, move the hero by the spawn rule measured from its own cell. Do not add a `PlacementRejection` value. `CheckPlacement` (`:219`) and `ApplyLayout` (`:308`) need no hero logic.

### 1.4 Step order and hero attack

- In `Step()`: hero movement right after `MoveEnemies` (`:614`); hero attack right after `TowerAttacks` (`:615`); hits on the hero inside `EnemyAttacks`; hero death and respawn resolution right after `RemoveDeadEnemies` (`:618`).
- Hero attack: arc 120°, reach 1.2, damage 15, interval 12 steps, all living enemies in the arc in ascending ID, through `DamageEnemy(e, amount, HeroSourceId)` (`:1204`) with a sentinel `HeroSourceId = -2`. `WorldView` keys kill handling on `SourceId` (`Unity/WorldView.cs:955-958`), so check that −2 is handled there in Stage 3.
- Guard behaviour when no command (Strategic view): hold position, auto-swing when an enemy is in the arc, face the nearest (ties lowest ID).
- Stats: `RunStats` (`:44-55`) gains `HeroKills`; enemies killed by the hero also count as defeated. `NightRecap` (`:62-72`) gains the same transient count.

### 1.5 Enemy engagement

- Melee (Grunt, Runner, Brute) in `DecideAtCenter` (`:1016-1079`): insert the hero check between the ranged hold (`:1046`) and the route-end attack decision (`:1048`). Fresh step only, using the `CarryUnits = 0; if (!freshStep) return;` pattern (`:1050-1051`). Engage within 1.0 and stay engaged within 1.5. A held enemy is not `Moving`, so `DecideAtCenter(e, true)` already runs every step (`:1009`), which gives the sticky check. An enemy already attacking a building never switches.
- Enemy target fields: `Enemy.TargetBuildingId` (`Core/Entities.cs:140`) is building-only. Add `TargetsHero` and branch in `EnemyAttacks` (`:1335-1366`) and the damage helper (`EnemyHitDamage`, `:474`): no Wall multiplier, no reflection, Commander aura applies (`CommanderAuraPercent`, `:487`).
- Raider Archer: acquisition (`:1141-1162`) treats the hero as `RangedClass` 0 (the code's classes are 0-based; spec "class 1"). Provocation: `RunSimulation` keeps a set of archer IDs the hero has hit; in the stickiness block (`:1088-1098`) a provoked archer retargets the hero over anything while the hero is alive and in range (fresh full interval). Clear an archer's flag when the hero dies, respawns, or leaves its range.
- Bomber and both bosses ignore the hero as a target.
- New events (`Core/SimEvent.cs:3-41`): `HeroAttacked` (swing, with facing), `HeroDamaged`, `HeroDowned`, `HeroRespawned`.

### 1.6 Checkpoint

- `Core/RunCheckpoint.cs`: optional `heroX`, `heroZ` (written only for V2 runs at the Start Night checkpoint) and `stats.heroKills`. Missing fields read as "spawn point" and 0. `CheckpointCodec.ResolveProfile` (`:237-271`) already matches `(rulesVersion, campaignId)`; no schema bump.
- A Save and Quit during Day does not store the hero's position (spec 7).

### 1.7 Tests (EditMode; spec §8 plus the 2026-10-19 decisions)

Add to the existing release fixtures (`Tests/EditMode/ReleaseRulesTests.cs`, `SimTestUtil.cs`); add one small release-campaign helper to `SimTestUtil` only if two tests need it. One test per named failure:

1. Melee engages only at a cell-centre decision point, and an enemy hitting a Wall ignores the adjacent hero.
2. A provoked Raider Archer retargets the hero over a House, and the provocation ends when the hero leaves range.
3. Hero death is never Defeat; Keep death with a same-step hero death is still Defeat.
4. Collision: solid cell, non-walkable cell, diagonal corner.
5. Spawn with all four Keep neighbours walled lands on the nearest free cell outside; placing a Wall on the hero succeeds and moves the hero.
6. The same seed, placements, perks and command stream reproduce the night (snapshot equality); a Night continued from its Start Night checkpoint starts with the same hero position.
7. A `release-rules-1` save restores and plays with no hero (extend `ReleaseSaveTests`, `:227-293`).

### 1.8 Balance scripts (spec §6) — the Stage 1 gate

- Extend `ReleasePolicyTests.Play` (`Tests/EditMode/ReleasePolicyTests.cs:154-204`) to run each approach under V1 (control) and V2 with a scripted hero: **idle** (guard at its spawn), **active** ("fight nearest": walk toward the nearest enemy within 6 tiles of the Keep, attack in arc), and **hero only** (no construction). Log to `TestResults/012/hero-policy-runs.txt`, seeds 1-8, own orientations as in plan 010.
- Note: the kill-zone and economy scripts wall all four Keep neighbours (`ReleasePolicyTests.cs:48, 101`), so the idle hero spawns outside. That is the intended spec behaviour; record where it spawns.
- Gate (spec §6): hero-only loses by Night 3 on every seed; idle hero matches V1 outcomes on ≥7/8 seeds per approach; active hero kills ≤25% of kills per night on Nights 6-12. Assert only the hero-only loss (a rule); log the other two.
- **Stop condition:** if the gate fails, tune only hero [P] values (HP, damage, interval, respawn) within ±50% and record each try. If it still fails, stop and report to the owner before Stage 2. Never change V1 values or the wave table here.

## Stage 2 — Hero view and controls (Unity, placeholder hero model)

Goal: the player can switch views, steer and fight with the hero responsively, with a blockout hero.

### 2.1 Cameras

- Keep `Unity/CameraRig.cs` untouched as the Strategic rig. Its `Apply()` (`:161-172`) forces orthographic every tick, so the hero view is a **second camera** (`HeroCameraRig`, new file), enabled instead of the strategic one. Move the `AudioListener` (`Editor/PrototypeSetup.cs:194`) to follow the active camera.
- Add an `ActiveCamera` property on the bootstrap. Replace `Camera.main` uses that matter: URP post data (`Unity/WorldView.cs:375`) on both cameras; background colour (`:2105`) on both until Stage 3 replaces it.
- Hero camera: perspective orbit, pitch −10°..60°, distance 3-7, FOV 60 (50-90 setting), shoulder offset, collision by sphere cast against presentation-only colliders added to building and terrain views (layer not used by anything else; never read by Core).
- Transition ≤0.4 s on unscaled time, skippable, works while paused.

### 2.2 View switch and input

- New `ViewMode { Strategic, Hero }` on the bootstrap; Tab toggles in Day and Night (not in Reward or menus). The switch only changes presentation and input routing. Add a PlayMode assertion that it never changes the simulation snapshot.
- `Unity/PlayerController.cs:78-116`: `CameraRig.Tick` (`:110`) consumes WASD and the wheel unconditionally. Route by mode: Strategic as today; Hero samples WASD (camera-relative → 16 headings), mouse look (cursor locked), LMB attack, wheel distance into a `HeroCommand` each frame. The bootstrap passes that command to `Advance`/`AdvanceDay`. With Strategic active during Night, pass `HeroCommand.None` (guard).
- Build hotkeys 1-8 in Hero view switch to Strategic with that tool armed (`SetTool`, `:52`). F is disabled in Hero view with a short "1× in Hero view" hint. H centres the strategic camera on the hero. Esc in Hero view unlocks the cursor and opens the menu (`OpenMenu`); Strategic keeps today's Esc (`:98-102`).
- Cursor unlock on menu, reward, dialogs, focus loss.

### 2.3 Speed lock

- `SetGameSpeed` (`Unity/PrototypeBootstrap.cs:94`) also unpauses. Entering Hero view must not unpause, so set the speed field directly (new `ForceSpeed(1)` that leaves pause alone). Returning to Strategic keeps 1×.

### 2.4 Responsiveness (spec 3.3)

- The hero view presents a **predicted** hero: each frame, run the same Core movement function on a copy of the hero state with the current command for the fractional step, starting from the last authoritative state. When the next simulation step lands, blend any difference within 0.1 s. The prediction never writes to the simulation.
- Measure input-to-motion with a PlayMode probe (like `Tests/PlayMode/MotionProbe.cs`): frames from a key press to visible displacement at 60 fps. Target ≤1 frame.

### 2.5 Placeholder hero view

- `PrototypeContent` gains `heroPrefab` (next to `:35-40`); `Editor/ReleaseSetup.cs:101-106` generates a blockout with a `CharacterRig`; `Editor/ReleaseContentValidation.cs:66-73` requires it for the release content.
- `WorldView`: hero view created like `CreateEnemyView` (`:1382-1427`), interpolated like `SyncEnemies` (`:1099-1168`), posed with `CharacterRig.Pose` (gait, swing on `HeroAttacked`, flinch on `HeroDamaged`, fall on `HeroDowned`). A marker or outline in Strategic view.
- Minimal hero HUD: hero HP, respawn timer when down.

### 2.6 Settings

- `GameSettings` (`Unity/SaveStore.cs:37-63`) to version 3: look sensitivity, invert Y, FOV, default hero camera distance, "Enter Hero view at Night" (off). `ReadSettings` (`:229-260`) migrates only v1 today: add a v2→v3 step (and keep v1→current working). UI rows in `HudView.Mvp.cs:449-495`.

### 2.7 Stage 2 tests

- One PlayMode case in `Tests/PlayMode/ReleaseSceneTests.cs` (devices and temp saves at `:27-56`): during a Night, toggle views several times (also paused and at 3×) and compare the simulation snapshot with an untoggled run; in Hero view, injected W moves the hero and F does not change speed. Fix `PrototypeSceneTests.cs:116` (asserts `Camera.main.orthographic`) only if the release changes affect that scene; they should not.

## Stage 3 — Close-range presentation

Goal: spec §4. Most of the effort and the most risk to visual quality.

1. **Sky and horizon:** gradient skybox blended with `NightBlend` in `ApplyArtLighting` (`WorldView.cs:2079-2114`), distance fog for Hero view only, sea plane extended beyond the current ~80×80 (`:427`), the far islands (`:431-437`) checked at eye height, a visible island edge and beach.
2. **Billboards:** `BarRotation` (`WorldView.cs:1531`, used at `:1050`, `:1152`) becomes per-frame, facing the active camera. Screen-space anchors use `ActiveCamera` and hide behind the camera: breach label (`HudView.cs:588`, no check today), tooltip anchor (`HudView.cs:578`), entry markers (`HudView.Mvp.cs:643-663` already flip on `z < 0`).
3. **Route overlay at low angles:** gates and chevrons (`RouteOverlay.cs:174, 213, 580`) are flat ground meshes; check readability in Hero view and lower their opacity there if they clutter.
4. **Art audit:** every release building, enemy and terrain piece from hero eye height (back faces, undersides, z-fighting, scale). Record each item with fixed or deferred plus a reason in `plans/012-results.md`. Fix in `Art/Source~/models.py` and re-import through `Editor/ArtSetup.cs`.
5. **Hero model:** generated like the plan 011 enemies (`ArtSetup.cs:35-84`, `AddRig :295-310`): human defender, sword, warm settlement palette, silhouette distinct from every enemy, Grunt height.
6. **Hero view HUD:** hero HP, Keep HP, enemies remaining, respawn timer, a directional indicator for off-screen Keep or building hits (reuse `Hud.KeepHit`, `PrototypeBootstrap.cs:329-331`), new-side warnings.

## Stage 4 — Performance, balance, verification

1. Performance (spec §5): the 100-enemy stress fixture in Hero view looking across the board, both resolutions, paired and alternating with Strategic view, Editor closed. Gate: Hero ≤1.5× Strategic frame time and both ≥60 fps on the reference machine. Record shadow distance and any culling change.
2. Rerun the Stage 1 balance scripts if any hero [P] value changed in Stages 2-3.
3. Full EditMode and PlayMode suites; release build from the Editor; offscreen captures at 1280×720 and 1920×1080 in Hero view, Day and Night, all three profiles, toward the board edge and toward the Keep; check one capture for shadows.
4. Update `specs/003` status to implemented, merge its rules into `specs/002-game-rules.md` (§1, §4, §5, §8, §12, §13) and move spec 003 to `specs/archive/`, per `specs/README.md`. Update `plans/README.md`.
5. Write `plans/012-results.md`: fresh test counts, gate numbers, balance log, art audit list, captures, and what is still unverified (live feel, motion, human playtest).

## Order and effort

| Stage | Effort | Risk |
|---|---|---|
| 1 Core hero + balance gate | ~1 week | Balance gate may fail; stop rather than retune waves |
| 2 Views, input, prediction, blockout hero | ~1 week | Responsiveness; cursor and input conflicts |
| 3 Close-range presentation | ~1-2 weeks | Art never authored for this angle; scope can grow; keep to the audit list |
| 4 Performance and verification | ~2-3 days | Perspective view may miss the frame budget |

Stages are sequential. Stage 1 is judged by scripts before any camera work starts.

## Out of scope

Hero progression, hero perks, a second hero, soldiers, gamepad, placing buildings from Hero view, mid-night saves, changes to the MVP profile or the prototype scene, wave or V1 balance changes.

## Open points, not blocking

- Spec 003 makes only Raider Archers react to being hit by the hero. If the owner later wants melee enemies provoked too, that is a spec change.
- The 25% hero-kill share and the ≥7/8 idle-hero match are logged, not asserted; the owner decides after the Stage 1 numbers.
