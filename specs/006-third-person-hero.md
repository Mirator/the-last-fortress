# Third-person hero and switchable views

Version 0.1, 2026-10-19. Status: **current specification**; direction owner-approved, proposed defaults await the owner decisions at the end; nothing implemented. An implementing agent must first prepare a separate implementation plan.

## Purpose and decisions so far

Add one player-controlled hero who fights during Night alongside the fortress, played from a third-person camera, while the existing isometric strategy view remains available. The player can switch between the two views at any time.

Owner-confirmed (2026-10-19): option B from the feasibility analysis, a true third-person perspective camera, not a hero steered on the fixed isometric camera. Both views stay in the game with a switch between them.

Everything else in this document is a proposed default for review. Numbers are tagged as in [005-release-rules.md](005-release-rules.md): **[I]** invariant rule (changing it is a rules-version change), **[P]** provisional tuning (may change with recorded evidence inside the bounded-tuning policy).

## Baseline and what this supersedes

- The rules contract this extends is [005-release-rules.md](005-release-rules.md) (`release-rules-1`, campaign `twelve-nights`, map profiles `ravine`/`crossing`/`meadow`), together with the current release presentation (entry gates, route chevrons, authored art, day/night lighting). Specs 001-004 describe earlier milestones.
- For the **release profile only**, this supersedes two lines of `specs/001-game-design.md`: "Camera: Fixed isometric" and the exclusion of heroes. Soldiers, Barracks and activated abilities remain deferred.
- The six-night MVP profile (`mvp-rules-1`) and the one-wave prototype are unchanged in rules, presentation, controls and saves. They get no hero and no hero view.

## Scope

In scope: one hero entity in the simulation, enemy engagement with the hero, hero death and respawn, a third-person hero view, switching between views, input mapping for both views, a responsive control requirement, the presentation work the perspective camera exposes (horizon, island edge, billboards, readability), settings, a performance gate and a balance guard.

Out of scope (each needs its own specification): hero progression (levels, gear, upgrades, hero perks), more than one hero, soldiers, activated abilities, mount or ranged hero weapons, mid-night saving, multiplayer, hero-specific waves or enemies, new map profiles.

## 1. Rules version and profiles

1. Hero runs use a new rules version **`release-rules-2`** [I] on the same `twelve-nights` campaign and map profiles. All `release-rules-1` rules apply unless this document changes them.
2. `release-rules-1` stays in the catalog. Existing schema-2 saves tagged `release-rules-1` load and continue exactly as before, without a hero [I]. A rules-1 run is never converted to rules-2.
3. New Run in the release build offers **Hero: On (default) / Off**. Off starts a `release-rules-1` run; On starts `release-rules-2` [P: default value]. The off setting is also the control group for balance comparisons.
4. The release perk pool (24), its weights and the reward RNG stream are unchanged [I]. No perk refers to the hero in this milestone, so offer integrity and saved offers keep their meaning.

## 2. The hero in the simulation

The hero is a Core simulation entity like enemies and buildings, not a Unity object with its own physics. All hero outcomes are decided in `RunSimulation` steps; the view only presents them.

### 2.1 State and movement

| Property | Value |
|---|---|
| Max HP | 200 [P] |
| Move speed | 3.0 tiles/s [P] = 180 units/step; must be a whole number of 1/1200-tile units per step [I] |
| Body radius | 0.3 tile [P] |
| Spawn / respawn point | Keep-adjacent spawn cell: the first walkable non-solid cell edge-adjacent to the Keep in N, E, S, W order [I] |
| Position | Continuous (same numeric representation as enemy `X`, `Z`), not cell-bound [I] |

- **Per-step command** [I]: one `HeroCommand` per simulation step: move heading quantized to 16 directions or none, attack pressed (bool), facing quantized to 64 directions. The view samples input into this record; the simulation reads nothing else from input. The same initial state plus the same command stream yields the same outcome.
- **Collision** [I]: the hero's body cannot overlap a cell occupied by a solid building or a non-walkable terrain cell (forest, rock, water; fords are walkable). It cannot leave the 20×20 board. Movement resolves axis-separated against those cells (slide along walls), never pushes anything, and never breaches.
- **No grid side effects** [I]: the hero never occupies cells, never blocks enemy routes, never changes `GridState.Revision`, and does not trigger Spike Traps. Enemies and the hero pass through each other (the game has no unit collision avoidance).
- **Day**: the hero exists and can walk during Day [P], but nothing in Day is timed or fought. A building cannot be placed on a cell the hero's body currently overlaps; placement is rejected with a distinct reason [I].

### 2.2 Hero attack

- One melee sweep [P: damage 15, interval 0.6 s = 12 steps, reach 1.2 tiles from the hero centre, arc 120° centred on facing] [I: the geometry rule].
- An attack starts when the command's attack flag is set and the previous swing's interval has elapsed. It hits on the step it starts (no wind-up in rules; the view may animate one), damaging every living enemy whose current position is inside the arc and reach, in ascending enemy ID [I].
- Hero hits are direct damage but not Archer Tower or Cannon hits: no tower perk applies (Sharpshooters, Last Stand, Kill Zone, Flaming Arrows, Heavy Shot). Kills count as enemies defeated in run stats and the night recap, plus a separate "hero kills" counter [I].
- **Guard behaviour** [I]: when the player is not controlling the hero (Strategic view during Night, or no command), the hero holds position and attacks automatically when at least one enemy is inside the arc and reach, facing the nearest one (ties to lowest ID). It never walks on its own in this milestone.

### 2.3 Step order

[005-release-rules.md](005-release-rules.md) §0 becomes, for `release-rules-2` only [I]:

1. Due spawns.
2. Enemy movement and Spike Trap entry. **2b. Hero movement** from this step's command.
3. Building attacks (ascending building ID). **3b. Hero attack.**
4. Due burn ticks.
5. Enemy actions in ascending enemy ID: melee hits (with reflection against buildings), ranged hits, Bomber detonations, **hits on the hero**.
6. Dead-enemy cleanup; **hero death resolution** (2.5).
7. Keep at 0 HP → Defeat, before the cleared-night check.

An enemy killed by the hero in 3b never acts in step 5 of that step.

### 2.4 Enemy engagement with the hero

The invariant to preserve: enemies decide only at cell-centre decision points on a fresh step, exactly where they already decide between attacking and continuing (this is how the Raider Archer works) [I]. Enemies never re-route toward the hero and never leave their ground route.

| Enemy | Behaviour toward the hero |
|---|---|
| Grunt, Runner, Brute | At a decision point, if the hero is alive and within **1.0 tile** [P] (Euclidean, enemy position to hero centre), stop and target the hero. First hit due one full interval later. Sticky while the hero is alive and within **1.5 tiles** [P]; otherwise drop the target and resume at the next decision point. |
| Raider Archer | Hero is a ranged-target candidate within its 3.5 range at **priority class 1** (with House and Farm), nearest first [P: class, owner decision]. Normal stickiness and cooldown-reset-on-retarget rules apply. |
| Bomber | Ignores the hero. A lit fuse still splashes buildings only; the blast does not damage the hero [P]. |
| Siege Golem, Siege Commander | Ignore the hero as a target. The hero can hit them. |

- An enemy already attacking a building never switches to the hero [I]. Hero engagement only happens where the enemy would otherwise move on.
- Hits on the hero use the enemy's normal damage, no Wall multiplier, no reflection, no Barbed Defenses. The Siege Commander aura applies [I].

### 2.5 Hero death, respawn and dawn

- At 0 HP the hero is down: no commands, no attacks, not a target. It respawns at the spawn point with full HP after **10 s** [P] of simulation time [I: measured in steps, so pause and Night speed apply].
- Hero death **never** causes Defeat. Defeat remains Keep HP 0 only [I].
- On every Day entry the hero is restored to full HP at the spawn point, alive, with its swing cooldown reset [I].
- Consequence [I]: the hero has **no state in the Day checkpoint**. A `release-rules-2` save differs from a rules-1 save only by its `rulesVersion`. Quitting during Night still returns to the start of that Night, as now.

**Acceptance (section 2):** an enemy engages only at a cell-centre decision point, never mid-segment; an enemy hitting a Wall ignores the hero standing next to it; hero death on the step the Keep falls still ends in Defeat, and hero death alone never does; the hero cannot enter a solid-building or non-walkable cell, including diagonally at corners; the same seed, placements, perk choices and command stream reproduce the same night outcome; a rules-1 save loads and plays with no hero.

## 3. Views

### 3.1 Two views and the switch

| | Strategic view | Hero view |
|---|---|---|
| Camera | Existing orthographic isometric rig, unchanged | New perspective third-person orbit camera behind the hero |
| Available | Day, Night, Reward | Day and Night (Reward screen overlays either) |
| Construction, sale, repair, rotation | Yes (Day) | No. Pressing a build hotkey or opening a build card switches to Strategic view with that tool armed [P: owner decision] |
| Hero control | No; hero follows guard behaviour (2.2) | Yes |
| Night speed 1×/2×/3× | Yes | Locked to 1×. Entering Hero view sets 1×; returning to Strategic keeps 1× until changed [P] |

- **Switching is presentation only** [I]: it never changes simulation state, RNG, gold, pause state or the hero's command beyond "no command" while not controlled. Toggling views any number of times, on any frame, leaves the simulation snapshot identical to not toggling.
- Default view: Strategic at Day 1 and after loading; on Start Night the view does not change unless a setting "Enter Hero view at Night" is on [P: default off, owner decision].
- The Strategic camera keeps its own focus and zoom across switches. Switching to Strategic does not jump to the hero; a key centres the strategic camera on the hero.
- Switching shows a short camera transition (≤ 0.4 s [P], skippable, unscaled time). It must work while Night is paused.

### 3.2 Hero camera

- Perspective orbit behind the hero, controlled with mouse look while the cursor is locked. Pitch clamp −10° to 60° [P], distance 3–7 tiles [P] (wheel), FOV 60° default with a 50–90° setting [P].
- Camera collision against walls, buildings and terrain: the camera moves in rather than clipping through. Authored art has no colliders, so the view adds presentation-only colliders or an equivalent query. These colliders never reach the simulation [I].
- Shoulder offset so the hero does not hide the aim point. Camera shake respects the existing shake setting.
- Settings (settings file version bump, with migration): look sensitivity, invert Y, FOV, camera distance default, "Enter Hero view at Night". The existing camera sensitivity setting keeps applying to the strategic rig only.

### 3.3 Responsiveness

The simulation steps at 20 Hz and the view interpolates. Without care, input-to-visible-motion is about 100 ms, which feels sluggish in third person.

- Required: hero movement and turning start in the first rendered frame after input [P: ≤ 1 frame], and the presented hero is never more than one simulation step away from the authoritative position [I].
- Allowed: presentation-side prediction of the hero, reconciled to the authoritative simulation each step. The simulation result always wins; a mismatch snaps or blends within 0.1 s [P].
- The swing animation starts on input; its damage is the simulation's (the hit marker follows the authoritative hit event).

### 3.4 Input map

Proposed bindings [P]. Keyboard and mouse only in this milestone; gamepad deferred.

| Input | Strategic, Day | Strategic, Night | Hero view, Day | Hero view, Night |
|---|---|---|---|---|
| **Tab** | Switch to Hero view | Switch to Hero view | Switch to Strategic | Switch to Strategic |
| W A S D | Pan | Pan | Move hero (camera-relative) | Move hero |
| Mouse move | Hover cell | Hover cell | Look (cursor locked) | Look |
| Left click | Place / select | Select | — | Attack |
| Right drag / click | Pan / cancel tool | Pan | — | — (reserved: block, later) |
| Wheel | Zoom | Zoom | Camera distance | Camera distance |
| 1–9 | Arm build card | — | Switch to Strategic with card armed | — |
| R | Rotate ghost / selection | — | — | — |
| Space | — | Pause / resume | — | Pause / resume |
| F | — | Cycle speed 1/2/3× | — | Disabled (shows "1× in Hero view") |
| H | Centre camera on hero | Centre camera on hero | — | — |
| Esc | Cancel tool, else menu | Menu (pauses as now) | Menu (unlocks cursor) | Menu (pauses, unlocks cursor) |

The menu, reward screen, dialogs and focus loss always unlock the cursor. UI clicks never pass into the world in either view (existing `PlayerController` rule).

**Acceptance (section 3):** toggle repeatedly during an active Night, including while paused and at 3×, and the simulation snapshot matches an untoggled run; Hero view forces 1× and Strategic restores speed control; a build hotkey in Hero view lands in Strategic with that tool armed and places nothing by itself; the cursor is never left locked over a menu; input-to-motion meets 3.3 at 60 fps (measured, not judged by eye).

## 4. Presentation at close range

The world was authored for a fixed 45°/40° orthographic view. The hero view exposes things that view never shows. Required in this milestone:

1. **Horizon.** Today the background is a flat clear colour (`WorldView` sets `Camera.main.backgroundColor` to the sea colour). Hero view needs a sky (gradient or skybox with day/night blend), distant sea and a visible island edge beyond the 20×20 board, plus distance fog tuned for both lighting states.
2. **Billboards and overlays.** World-space HP bars are rotated to the constant `CameraRig.Yaw`. They must face the active camera. Screen-space anchors in `HudView` and `HudView.Mvp` use the strategic camera's `WorldToScreenPoint`. They must use the active camera and hide anchors behind the camera. Route chevrons and entry gates must stay readable or be toned down at low angles.
3. **Art audit.** Check every release building, enemy and terrain piece from hero eye height for missing back faces, unfinished undersides, z-fighting and scale problems. Hero height ≈ Grunt height [P]. Fix or record each finding.
4. **Hero model.** One authored hero on the existing `CharacterRig` part rig (walk, swing, hit, fall, respawn), with a silhouette distinct from every enemy at both camera distances. The hero must be findable in Strategic view (marker or outline).
5. **Night readability in Hero view.** HUD shows hero HP, Keep HP, enemies remaining, the respawn timer when down, and a directional indicator when the Keep or a building outside the view is hit. New-side entry warnings stay visible. A hero torch or rim light may be added; it must not change what is readable as dangerous.
6. **Comfort.** FOV setting, shake toggle, no forced head-bob. Camera transitions use unscaled time.

**Acceptance (section 4):** captures at 1280×720 and 1920×1080 in Hero view, Day and Night, on all three map profiles, looking toward the board edge and toward the Keep; HP bars face the camera from every orbit angle; nothing on the HUD points to an object behind the camera; art audit list recorded with each item fixed or deferred with a reason.

## 5. Performance

The perspective camera sees more geometry and shadow casters than the orthographic one. Repeat the existing 100-enemy visible-window performance measurement in Hero view at both resolutions, looking across the board toward the busiest entry. Budget: the Hero view frame time must stay within 1.5× of the Strategic view measured in the same session, and both must hold 60 fps on the reference machine [P]. Record shadow distance and any LOD or culling change made to meet it.

## 6. Balance guard

The design pillars are "every building is a tactical decision" and readable battles. A hero strong enough to replace placement breaks the game. Required evidence before any rules-2 tuning is called done (scripted policy runs, seeds 1–8 per approach, real budget, as recorded in the spec 005 tuning log):

1. **Hero alone loses.** No construction beyond the Keep, hero controlled by a scripted "fight nearest" policy: the run must be lost by Night 3 on every seed [P: night].
2. **Idle hero changes little.** The existing release policies (kill-zone, corridor, economy) with the hero in guard behaviour at the spawn point: outcomes (win/loss, final night) match `release-rules-1` on at least 7 of 8 seeds per approach.
3. **Active hero helps, does not dominate.** With a scripted active hero, hero kills stay at or below 25% of all kills per night on Nights 6–12 [P].
4. Any wave, HP or damage change for rules-2 follows the bounded-tuning policy, is recorded with before/after logs, and never changes `release-rules-1`.

These scripts establish mechanical limits only. Whether the hero is fun and whether the views feel good needs owner and player playtests.

## 7. Saves

- Run checkpoint schema 2 with `rulesVersion: release-rules-2`; no new fields (2.5). Builds without rules-2 reject such a save with the existing `IncompatibleRules` message and never overwrite it [I].
- Settings file gains the camera settings from 3.2 with a version bump and migration from the current version.
- The current view, camera angle and hero position are not saved [I].

## 8. Tests

Follow root `AGENTS.md`. Each test names the failure it catches; no generic framework, no tests for constants or getters.

EditMode (Core):
- Engagement only at cell-centre decision points, and building attackers ignore the hero: catches enemies re-routing or switching targets mid-segment.
- Hero death is not Defeat, and Keep death with a same-step hero death is still Defeat: catches terminal-state priority regressions.
- Hero collision against a solid cell, a non-walkable cell and a diagonal corner: catches walking through walls.
- Same command stream reproduces the same night: catches non-determinism from input handling.
- A `release-rules-1` fixture still produces its recorded outcome: catches hero code leaking into rules-1 (extend an existing release fixture rather than adding a new one).

PlayMode (one scenario): toggle views and drive the hero during a Night; the simulation snapshot after toggling matches the untoggled snapshot, and the hero moves in response to injected input. Camera collision, art and feel are checked by captures and playtest, not automated tests.

## 9. Delivery order

1. Core: `release-rules-2`, hero entity, command record, collision, attack, engagement, death/respawn, tests, balance scripts.
2. Hero view: camera, switch, input map, responsiveness, minimal hero model, HUD essentials.
3. Close-range presentation: horizon, billboards, art audit fixes, readability, settings, performance gate.
4. Balance pass with evidence, then owner playtest.

Stage 1 can be judged by scripts before any camera work starts. If the balance guard shows the hero cannot be made useful without dominating, stop and revisit the design before stages 2-3.

## Owner decisions needed

1. **Hero name.** "Commander" collides with the Siege Commander enemy. Working name: "Warden".
2. **Archers vs hero.** Should Raider Archers shoot the hero (class 1, proposed) or ignore it?
3. **Construction in Hero view.** Switch to Strategic with the tool armed (proposed), or allow placing at an aimed cell from Hero view (more work)?
4. **Default view at Night.** Stay in the current view (proposed), or enter Hero view automatically?
5. **Speed in Hero view.** Lock to 1× (proposed), or allow 2× with the hero moving faster too?
6. **Hero on/off at New Run.** Keep the toggle (proposed), or make the hero mandatory in the release profile and keep rules-1 only for old saves?
7. **Respawn delay and hero numbers.** 10 s respawn, 200 HP, 15 damage per 0.6 s as starting values.
8. **Art.** Hero model and the sky/island-edge art are new content. Generated placeholder art, like the current release art, or commissioned?
