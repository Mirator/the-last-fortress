# Plan 019 results: third-person production slice

Implements the third-person production-slice feature spec v0.1 (registered from `plans/018-third-person-spec-proposal.md` as `specs/003-…`, now merged into `specs/002-game-rules.md` v1.7 §18 and archived as `specs/archive/007-third-person-production-slice.md`). Baseline `adac6b4`, 2026-10-20. Nothing committed.

Status: **TECHNICAL COMPLETE; HUMAN ACCEPTANCE PENDING.** Every automated gate passed on fresh runs. The owner has not reviewed the live motion, the art or the sound, so this is not an AA approval.

**Playable build:** `game/The Last Fortress/Builds/Release/TheLastFortress.exe` (built 2026-10-20 21:09, Succeeded, 0 errors, `rules=release-rules-4`, report `Builds/Release/build-report.txt`). New runs use `release-rules-4`. Saves from V1/V2/V3 and the MVP still load under their own rules. The pre-change baseline build is kept at `Builds/Release-before-019/` for comparison only.

| Stage | Status | Fresh evidence (under `evidence/019/`) |
|---|---|---|
| 1 Register spec, baseline | Done | Baseline G1 21/21 on unmodified Core (`baseline/Core.xml`); V3 pin (`baseline/Pin.xml`); before-clips (`motion/before-*`) |
| 2 V4 motor, prediction, saves | Done | G1 27/27 (`stage2/Core.xml`, `stage2/Core-afterflip.xml`) |
| 3 Contact combat, balance | Done, candidate 0 | G3 2/2 (`stage3/Policy.xml`, `stage3/hero-policy-runs-candidate0.txt`) |
| 4 Camera safety, focus | Done | G2 8/8 (`stage4/Scene.xml`); `motion/after-*-hero-camera.mp4` |
| 5 Hero animation, contact slice | Done technically; owner review pending | G2 8/8 (`stage5/Scene.xml`); `motion/after-*-hero-motion.mp4` |
| 6 Sound identity, HUD | Done technically; **listening pending** | Clips generated; G4 |
| 7 Integrated verification | Done | G4 EditMode 125/125 (`g4/EditMode-final.xml`), PlayMode 24/24 + 1 skipped (`g4/PlayMode-final.xml`), build, paired perf (`perf/`) |

Commands (sandbox disabled; Editor closed throughout, so no second Editor ever ran):

```
sh plans/evidence/019/run-tests.sh <out> <name> EditMode|PlayMode ['<fixture;fixture>']   # batch tests, reverts ProjectSettings noise
sh plans/evidence/019/build.sh <log>                                                    # BuildRelease.BuildWindows
Unity.exe -batchmode -quit -projectPath <project> -executeMethod TheLastFortress.Editor.ArtSetup.RunHeroV4
sh plans/evidence/019/capture.sh <build> <out> <w> <h> hero-motion|hero-camera|scale
sh plans/evidence/019/after-and-perf.sh                                                 # after-clips both resolutions + 8 paired stress runs
```

G1 filter: `TheLastFortress.Tests.HeroRulesTests;TheLastFortress.Tests.ReleaseMechanicsTests;TheLastFortress.Tests.ReleaseSaveTests`. The plan's `ReleaseRulesTests` is a file name, not a fixture. With that filter Unity silently ran only 7 cases. G2: `TheLastFortress.Tests.ReleaseSceneTests`. G3: `TheLastFortress.Tests.ReleasePolicyTests`.

## What changed

**Spec and indexes.** The 018 proposal was registered under `specs/` first. Then rules 1.6 removed only the contradictory lift-first camera sentence. On completion the implemented rules were merged as rules v1.7 (§1 profiles, §12 saves, §14 stress hero, §16 tuning row, new §18), the feature spec was archived as `archive/007`, and both indexes and `plans/README.md` were updated.

**Core (release-rules-4).**
- `HeroMotion.cs` (new):
  - `HeroProfile` holds one immutable profile per rules version. V2/V3 carry the old values unchanged.
  - Checked-in 64-direction integer table.
  - `IsqrtCeil`, `ClampLength`, `Accelerate`, and the old binary search extracted as `LegalAxisMove`.
  - The simulation and `RunSimulation.PredictHeroStep` (copy-only) run the same `Step`.
- `HeroCommand.Fine` makes 64-heading commands. The 16-heading constructor is unchanged and promotes ×4.
- `HeroCombatRules.cs` (new): an exact integer strike line; touching a corner blocks.
- Attack sequence: acceptance (`HeroAttacked`, facing committed) → `HeroContact` 3 steps later (victim count, 0 for a miss) → one `HeroHit` per victim, each damaged once. Contact resolves before a new acceptance. Profile reaches apply to engage, hold, hit and guard.
- Attack state is cleared on death, respawn, dawn, reset, load, relocation and Night start. Night start keeps velocity.
- Checkpoints gain optional `heroVx/heroVz`, written only in V4 Start Night checkpoints and validated against the top speed.
- Snapshot fields `v=`/`atk=` are emitted for V4 only, so legacy snapshots are byte-identical.

**View.**
- `HeroPresenter`:
  - Prediction moves the last step toward the predicted step by the elapsed fraction, for every profile; the all-or-nothing branch is removed.
  - V4: the shown root is the prediction itself (no glide), and the pose is held while frozen.
  - The attack phase runs on Core ticks, so the contact pose lands on the frame showing the contact step. Pause freezes it.
  - No standing reach sector; a 0.15 s sweep at true reach appears at contact; step-in is capped at 0.02 tile.
  - The model is swapped per profile.
  - Close-camera fallback: when the camera is within 0.12 tile, the hero's own figure renders shadow-only.
- `WorldView` / `AudioDirector`: the hit delay depends on the profile (V4: immediate).
- `PlayerController` attack input for V4:
  - Night only; a held button repeats when the hero is ready.
  - One tap is kept for five unpaused Night steps.
  - Kept input is cleared on Day, death, view switch, menu, focus loss and run reset.
- Focus gate: `FocusSuspended` joins `Suspended` and never touches the pause flag. After focus loss or the menu, a consumed click recaptures the cursor.

**Camera.**
- `HeroCameraObstacles` (new) takes blockers from what is actually drawn: one box per renderer of each building, tall props split into a trunk column plus a canopy box, and the ground. Units, effects and overlays are never blockers.
- Obstacles pull the camera in first, with no minimum distance. It lifts only if the pulled-in distance is below 0.2: the smallest 5° step, up to 15°, with hysteresis.
- It moves inward immediately and eases outward and down with a 0.15 s time constant. The final pose is re-validated.
- If the pivot is blocked, the pivot moves over the head and then lower. If no pose is clear, the last clear pose is held.
- Each entry-transition pose is validated; a blocked one cuts to the final pose.
- The orbit is kept within a run and reset per run.

**Hero asset.**
- `models_hero_v4.py` plus an `articulated` option in `characters.py` (off by default) produce `ArtHeroV4.fbx` with forearms, hands and feet.
- The arm is 0.18 h and the blade 0.28 h, so the tip reaches about 0.197 tile, inside the 0.20 reach. The legacy figure reaches about 0.175 tile at 0.229 tile tall.
- Legacy `ArtHero.fbx` was regenerated to a temp folder and measured identical.
- `ArtSetup.RunHeroV4` imports only this figure, builds `ArtHeroV4.prefab` with `HeroAnimationRig`, and assigns `contactHeroPrefab` and the hero clips. `ReleaseContentValidation` requires the complete rig.
- `HeroAnimationRig` (new):
  - Gait comes from actual shown travel, so the feet settle when the hero stops or is blocked.
  - Directional forward, back, strafe and diagonal cycles.
  - Hips turn toward travel up to 50°, the chest toward the aim up to 55°, and the figure backpedals beyond 100°.
  - Feet stay flat while planted; a small shuffle accompanies large turns.
  - One slash: coiled wind-up, wrist snap at contact, recovery. Additive hurt, slack down, crouch-to-stand rise.
  - Footfalls are counted per planted foot.
- `CharacterRig` and the enemies are untouched.
- **This is a generated low-poly asset extended for articulation, not owner-approved art.** One bounded iteration was done: a re-grip, because the generated 135° grip made the blade point backwards in the guard pose.

**Sound.**
- `synth.py` gained five hero families: two swings, two blade impacts, hurt, down and four footsteps. Each is seeded on its own, and only these 10 clips were written; no existing clip changed.
- `AudioDirector`:
  - Hero sounds play in world space (linear falloff, 0.4–6 tiles) only while the hero camera is the listener. Everything else stays 2D.
  - Low priorities (footstep 5) and per-family cap 2. The Keep hit stays highest, and the hero's fall ranks below every threat warning.
  - One impact per contact; the V4 whoosh plays on contact.
  - Footsteps follow the rig's planted feet only while presentation advances.

**HUD.** The uGUI skill was read first; only targeted text changes were made:
- In the hero view at Night the income forecast is hidden. Gold, Keep HP, threats, hero HP and hints stay.
- While a recapture is pending, the hint reads "Click to take the mouse back".

## Tests (extended, not new fixtures)

- `HeroRulesTests`:
  - Replay case parameterised V3/V4. V3 asserts an FNV fingerprint recorded on the unmodified Core. V4 covers moving Start Night replay with velocity, a Day save loading at rest, and out-of-range velocity being refused.
  - Motor caps, braking, reversing through zero, and equal snapshots across uneven chunking.
  - Wall and corner slide, with prediction equal to the step and never writing anything.
  - Contact: no damage before the due step, exactly once at T+3, committed aim, a miss by aim, a miss by moving out of reach.
  - Death cancels an unlanded attack. A strike is blocked at a solid corner.
- `ReleaseSaveTests`: literal V2/V3/V4 profile values.
- `ReleasePolicyTests`: V3/V4 comparison; the active script was adapted to V4 reach and braking (legacy branch unchanged).
- `ReleaseSceneTests`:
  - The first-frame threshold dropped from 0.01 to 0.002 tile because V4 accelerates. The rule (visible on the first frame, no step yet) is unchanged.
  - New focus/menu recapture case, which also checks contact-pose alignment and pause freeze.
  - New camera case: clear at FOV and zoom extremes, the Keep pulls the camera in, no pumping, the entry transition stays clear.

## Balance (G3, release-rules-4 candidate 0 = the spec's initial values)

Seeds 1–8 on their own orientations; idle, active and no-build hero; V3 control.

| Approach | V3 wins idle / active | V4 wins idle / active | V4 hero share N6–12 avg (peak) | V4 active contacts (misses) | Hero alone falls by |
|---|---|---|---|---|---|
| kill-zone | 7/8, 8/8 | 7/8, 7/8 | 7% (27%) | 2803 (181) | Night 3 (V3 and V4) |
| corridor | 3/8, 5/8 | 3/8, 4/8 | 7% (22%) | 2057 (158) | Night 2 |
| economy | 7/8, 8/8 | 7/8, 8/8 | 4% (19%) | 1294 (84) | Night 2 |

Hero deaths with the active hero (V3 → V4): kill-zone 51 → 52, corridor 35 → 38, economy 16 → 16. Active-hero Keep damage: kill-zone 10 → 650, corridor 2441 → 2721, economy 0 → 0. The miss rate is about 6.5%: the script does approach and wait for contact.

Both gates are met: the hero alone loses by Night 3 and the hero's share stays ≤25%. Only one candidate was used, and no waves, towers or enemies were touched.

## Motion evidence (fresh, offscreen renders of the windowed player)

**Matched clips.** `motion/before-*` (release-rules-3, presentation before any change) and `motion/after-*-hero-motion.mp4` (final build) use the same scripted camera-relative input, at 1280×720 and 1920×1080, each with a per-frame log. They are matched up to the `return` segment; the frames after that are not comparable.

**Script checks on both after-logs:**
- No footfall on a frame where neither the authoritative nor the shown position changed.
- Each first frame with phase ≥ 1 is the frame where the attack stops pending: 6/6 contacts aligned.
- 39 footfalls.

**Hero camera.** `motion/after-*-hero-camera.mp4` and its log: 279 frames covering Keep sweeps at FOV 50/90 × zoom 0.5/2.2, a Wall corner, the forest edge and the entry beside trees. Every pose was clear, with 0 fallbacks and 0 transition cuts.

An earlier build had whole-tree boxes; next to the forest the camera collapsed into the hero's head (52 fallback frames). That is fixed by the trunk-plus-canopy proxy. This was found from the clip, not by a test.

**Visual check from contact sheets:** guard grip, directional strafe and backpedal legs, contact sparks at physical range, the short sweep, pause freeze.

**Not shown in the clips:** a death and respawn. No Grunt landed a hit during the scripted `fall` segment in either the before- or the after-run. Death cancellation and respawn are covered by `HeroRulesTests` and `ReleaseSceneTests` only. The captures cover the Meadow map only; Ravine and Crossing were not filmed.

## Performance (fresh, paired and alternating, Editor closed, desktop unlocked)

Setup:
- Reference machine: Ryzen 5 3600, GTX 1660 SUPER, 32 GB, Direct3D12.
- Non-development build, windowed, VSync off.
- Fixture `-stress -stress-hero`: 100 enemies plus the release-rules-4 hero with the `HeroAnimationRig` figure. The hero view follows the real hero, which holds the attack toward the West lane.
- Metadata now names the rules, figure and view (the old `rules_version` printed the MVP constant). All 8 runs had `event_valid=True`.

| Run | View | Resolution | Median | p95 | p99 | Max |
|---|---|---|---:|---:|---:|---:|
| 1 | strategic | 1920×1080 | 4.026 | 4.576 | 5.135 | 10.4 |
| 2 | hero | 1920×1080 | 4.614 | 5.040 | 5.430 | 8.6 |
| 3 | strategic | 1280×720 | 3.193 | 3.734 | 3.985 | 5.0 |
| 4 | hero | 1280×720 | 3.777 | 4.284 | 4.671 | 5.8 |
| 5 | strategic | 1920×1080 | 4.007 | 4.371 | 4.630 | 7.5 |
| 6 | hero | 1920×1080 | 4.664 | 5.453 | 6.410 | 66.8 (one hitch, frame 537) |
| 7 | strategic | 1280×720 | 3.190 | 3.713 | 3.988 | 9.2 |
| 8 | hero | 1280×720 | 3.783 | 4.255 | 4.586 | 6.1 |

All values are in ms. All runs pass the 16.7/20/33.3 ms gate (≥60 fps). The hero/strategic median ratio is 1.15× and 1.16× at 1080p, 1.18× and 1.19× at 720p, within the 1.5× limit. Run 6 had one 66.8 ms frame; the cause was not investigated.

The stress hero killed nothing: it stands at its spawn and swings at the air, so the load includes contacts and cues but no hit reactions. The camera case in Stage 4 and the after-clips exercise hits.

## Scope notes

Files outside the plan's list:
- `HudView.cs`: one line hiding the income forecast in hero Night.
- `StressMeasurement.cs` and `CaptureSequence.cs`: verification tooling (stress hero and metadata; `hero-motion`/`hero-camera` sets). Changed because the existing tools could not select V4 or film the hero.
- `specs/001-vision.md`: a section pointer only.

No dependencies, purchases, commits or pushes. Only the hero was regenerated, never the broad art or audio.

Side effects:
- Batch runs rewrite `ProjectSettings/*.asset` and `Assets/Settings/PC_RPAsset.asset`. These were reverted after each run; all were clean before.
- A stale `.git/index.lock` (0 bytes, no git process running) was left by a batch build and removed.
- Capture runs add save slots to the normal save folder, as earlier capture sets do.

## Limitations and pending human checks

- **Owner review of live motion, art and listening: pending.** No one has played or listened. The motion judgement above comes from offscreen frames and per-frame logs.
- Live smoke tests of old V2/V3 saves and the MVP were automated only (`ReleaseSaveTests`, MVP scene tests in G4).
- Not filmed: death and respawn, Ravine and Crossing, min/max FOV with the hero moving through obstacles. Camera FOV and zoom extremes are filmed at the Keep only.
- Tree blockers are approximate (a 0.08-tile trunk and a canopy box from 30% of the tree's height); an unusual prop silhouette could still clip.
- `-capture-set 013`'s "blade at contact one step after the swing" frame is not valid for V4.

Last verified step: final build, G4 on the final code, after-clips at both resolutions, 8 paired stress runs, all fresh on 2026-10-20.

Next action: owner plays the build (hero view at Night: strafe and backpedal while attacking, a 180° aim change, the forest edge, focus switching) and listens to the hero sounds. Changes to motion or sound feel go into a follow-up that cites this result.

## Owner playtest changes (2026-10-20, after the first build)

The owner played the build and reported a missed hit on a Grunt standing inside the hero, enemies that "immediately go left" at the entry, and asked for a threat system. Decisions (asked and answered): touching enemies are always struck and reach goes 0.20 → 0.225; bounded pursuit; turn-late routes; release-rules-4 is changed in place, since it was never shipped.

- `HeroProfile.V4`: `SwingReach` is 0.225 and `TouchReach` is 0.1. Pursuit tuning: start 1.0, leash 1.5, 80 steps, cooldown 40, provocation 40.
- Pursuit is implemented in `RunSimulation.StartsPursuit`, `PursueStep` and `MoveOff`. Chasing enemies use exact integer movement off the route. The chase starts only at cell-centre decision points and only when the line to the hero is clear. Afterwards the enemy walks back to its cell centre and plans again. Snapshot `chase=` fields appear only when a chase happened.
- `RoutePlanner.TurnLate` (V4 only, open routes): same goal and length, turning late. Legacy planners are unchanged.
- Tests:
  - The contact case was reordered (misses first, because a struck Grunt now turns on the hero).
  - New: a touching enemy behind the aim is struck, then chases briefly and returns to its route.
  - New: V4 routes go straight and turn late with the same length.
- Fresh runs:
  - Core and routing 39/39 (`evidence/019/threat/Core.xml`), including the V3 fingerprint and every legacy routing test.
  - Policy 2/2 (`threat/Policy.xml`); summary in `threat/hero-policy-runs-threat.txt` and the rules §16 row.
  - Scene 8/8 (`threat/Scene.xml`).
  - Build Succeeded, 0 errors, 22:59 (`Builds/Release`).
- Full suites re-run after this change: EditMode 127/127 (`threat/EditMode.xml`), PlayMode 24/24 + 1 skipped (`threat/PlayMode.xml`). Not re-run: the paired performance runs and the motion clips under `motion/`; those numbers predate the threat change. Fresh review frames from the new build are in `TestResults/019/review/` (gitignored).
- Pending: how chasing enemies look (facing and gait off the route) has not been checked live.


New-player review of both views (three reviewers, static frames): [021-new-player-review.md](021-new-player-review.md).

## Owner playtest, round 2 (2026-10-21)

Requests: attack by Day, a smooth Tab back to the map, collisions that match the drawn buildings, and no accidental perk pick when the Reward screen appears mid-click.

- **Day attacks** (`HeroProfile.DayAttacks`, `RunSimulation.HeroClock`). The swing runs on the hero's Day clock and has nothing to hit. Night start still clears attack state, and taps never carry across Day and Night.
- **Hero collision** (`HeroOverlapsDrawn`, `HeroProfile.BuildingInsetUnits`). A building blocks only its footprint shrunk to the measured model size (Blender bounds of the building FBXs). Walls and the Keep stay whole. `StrikeClear` ignores the cells holding its own endpoints. The legacy path is unchanged.
- **Smooth exit** (`HeroCameraRig.BeginExit/FollowExit`, `PrototypeBootstrap.SetHeroView(on, smooth)`). Tab and 1-8 ease out over 0.35 s. Turning back mid-exit eases in from the current pose. Run boundaries still cut.
- **Reward lockout** (`HudView.RewardLocked`): 0.8 s, plus the held button must be released.
- Fresh runs:
  - Core and routing 40/40 (`evidence/019/round3/Core.xml`), with a new case: the hero passes the corner gap between a Tower and a House, and a Day swing reaches contact.
  - Policy 2/2 (`round3/hero-policy-runs-round3.txt`): V4 active wins 8/8, 4/8 and 8/8; hero share 8%, 8% and 6%; a hero alone still falls by Night 3.
  - Release and MVP scene tests 13/13 (`round3/Scene.xml`).
  - Build Succeeded, 0 errors, 2026-10-21 09:18.
- Not tested automatically: the reward lockout (UI timing) and the smooth exit's visuals. Both are for live play.
- Round 2b, 2026-10-21: forest and rock cells block the hero only at their props (`TerrainPropsOverlap`, which mirrors `WorldView.PlaceArtCell`). Fresh runs: Core and routing 40/40 (`round4/Core.xml`, including passing between two boulders); policy 2/2 (`round4/`; a hero alone falls by Night 2-3); scene 8/8; build Succeeded at 09:32.
- Round 3, 2026-10-21 (owner): the Farm model is fenced on all four sides. Only `ArtFarm.fbx` was regenerated (models_ext `farm()`), with the same size and the same object name, so the prefab keeps its mesh reference.
- Hero collision fitted to the art:
  - Walls follow their drawn palisade by connection mask; connected Walls stay closed.
  - Water cells leave their drawn bank walkable.
  - A House is 0.86 × 0.76 and turns with its orientation.
  - The Farm blocks to its fence line.
  - Fresh runs: Core 40/40 (`round5/Core.xml`); policy 2/2 (`round5/hero-policy-runs-round5.txt`; V4 active wins 8/8, 4/8, 8/8; a hero alone still falls by Night 3); release and MVP scene tests 13/13; build Succeeded at 09:45.
  - The V4 slide test now uses test-only whole cells as blockers instead of a Wall, which no longer blocks its whole tile.
- Fire, 2026-10-21 (owner: "improve the fire 5 times"). The burn effect was sized and placed for the old strategic figures: 0.10–0.15-tile spinning cubes about 0.4 tile up, which read as pale hexagons over the head. Five reviewed rounds, each compared on a contact sheet in `evidence/019/fire/round-0..5b.png` (capture set `-capture-set fire`):
  1. Fit to the true-scale body; the marker became small, flame-coloured and strategic-view only.
  2. Colour ramp.
  3. Tapered pyramid flame tongues (a new `FxParticles` tongue mesh), plus smoke and embers.
  4. Emission around the body's silhouette, and a night ground glow.
  5. Round soft glow discs; shorter, warmer tongues.
- Fresh runs: scene tests 13/13 (`fire/Scene.xml`); final build Succeeded.
- Tooling added: `BuildRelease -releaseOutput <dir>`, so rounds built without touching the build being played. `capture.sh` now passes `-saves` so captures no longer write into the player's save folder.
- Cannon 1 x 1, 2026-10-21 (owner):
  - `BuildingDefinition.SizeOverride` lets one profile change a kind's footprint. The V4 definitions (`ReleaseCampaign.Contact` = walking enemies + `WithCompactCannon`) set the Cannon to 1 x 1; V1–V3 and the MVP keep 2 x 2.
  - Footprints now come from the definition everywhere: placement, building cells, checkpoint restore, route preview, ghosts and selection outline.
  - The view draws the Cannon model at half size and lowers its HP bar. Hero collision inset for the Cannon: 67.
  - Fresh runs: EditMode 128/128 and PlayMode 24/24 + 1 skipped (`evidence/019/cannon/`). The release scene test now expects one tile, and its Watchtower moved next to the 1 x 1 Cannon.
  - Policy (V4, active): kill-zone 8/8, corridor 4/8, economy 8/8; a hero alone still loses by Night 3. Build Succeeded.
  - Your existing release-rules-4 saves load their Cannons as 1 x 1.
- Cannon field gun, 2026-10-21 (owner: "no need to have a big fence around it"):
  - New model `ArtCannonField` (`models_ext.py cannon_field`, imported by `ArtSetup.RunCompactCannon` into `compactCannonPrefab`): the old gun at 0.7 scale on a low timber turntable (radius 0.37), shot and powder kegs in the corners; no stone ring. The 2 x 2 model stays for V1–V3; without the new prefab the half-size fallback remains.
  - Shot: arc peak 0.12 × distance (max 0.35 tile, was 1.1 for every shell), shell 0.045 (was 0.07), thinner trail, muzzle blast at half size. Flight time unchanged (0.42 s), so kill and sound timing are unchanged.
  - Crew (owner): the field gun has a gunner with a linstock who turns with the gun; the Archer Tower's archer stands on a firing step behind a parapet lowered from 1.1 m to 0.8 m and walks along it as he aims, so he shows over the parapet from the ground. Both are presentation only.
  - Hero collision for the Cannon: drawn shapes (`FieldGunOverlaps`): a 0.31-tile disc for the gun and gunner, a 0.09-tile disc each for the shot pile and the kegs, now in opposite corners. Owner playtest: the square turntable box (156) blocked the visible gap between gun and shot pile. Covered by the drawn-building case in `HeroRulesTests`. Build card no longer says 2x2 for a 1 x 1 Cannon.
