# The Last Fortress — navigation prototype, six-night MVP and twelve-night release profile

One-wave proof for plan `plans/001-navigation-prototype.md` and specification `specs/002-navigation-prototype.md`: buy Walls and Archer Towers, preview the route, start one Grunt wave, watch automatic combat, win or lose, restart. The six-night MVP (`specs/003`, plans 003–005) is a separate scene; see **Six-night MVP** below.

- Editor: Unity `6000.3.16f1` (URP 17.3, Input System 1.19, Test Framework 1.6). No packages were added or upgraded.
- Scene: `Assets/TheLastFortress/Scenes/NavigationPrototype.unity` (enabled in Build Settings; `SampleScene` left untouched).
- All prototype assets live under `Assets/TheLastFortress/`.

## Controls

| Input | Action |
|---|---|
| `1` / `2` or the bottom cards | Wall (20 gold) / Archer Tower (70 gold) tool |
| Left click | Place with a tool (tool stays active) · select a structure without one |
| Right click / `Esc` | Cancel tool (`Esc` with no tool clears selection) |
| WASD / arrows | Pan · mouse wheel zooms · **Reset Camera** button restores the view |
| **Start Night** (bottom-right) | Starts the wave; becomes **Pause/Resume** during Night |
| `Space` | Pause / resume during Night |
| `F`, or **II / 1× / 2× / 3×** above Start Night (MVP and release) | Night speed. The choice lasts for the session (later nights and runs) and is never saved with a run. Picking a speed while paused resumes. The fixed-step simulation plays a night out the same at any speed |
| **Sell** (selection panel, Day only) | Refunds `floor(price / 2)` once; the Keep cannot be sold |
| **Restart** (top-right, and on the outcome overlay) | Exact fresh Day |
| `F9` | Toggle the labeled stress fixture (debug only) |

Route preview: during Day the main spawn's route is a solid cyan line. A valid ghost shows the candidate route (the committed one stays faint underneath). A sealed Keep shows a coral crosshair plus a **BREACH** label on the first blocker, with the hypothetical remainder dashed. Invalid ghosts show a red cross plus the reason, and leave the committed route visible.

## Architecture

| Folder | Contents |
|---|---|
| `Runtime/Core/` | Plain C# with no UnityEngine use: `GridPos`/`GridCoordinates`, `GridState` (authoritative occupancy + revision), `PlacementService` (atomic gold/occupancy), `RoutePlanner` (open-route-first uniform-cost search, breach cost 6), `RunSimulation` (0.05 s fixed step, phases, combat, restart, stress hooks), `WavePlan`, `PrototypeDefinitions`, `LayoutFixture` |
| `Runtime/Unity/` | `PrototypeBootstrap` (single scene entry), `PlayerController` (Input System + explicit UI raycast), `CameraRig`, `WorldView` (views diffed from sim IDs), `RouteOverlay`, `HudView` (uGUI, legacy `UI.Text`), `StressMeasurement`, `CaptureSequence`, serialized `PrototypeDefinitionsAsset`/`PrototypeContent` |
| `Editor/` | `PrototypeSetup` (idempotent generator for materials, prefabs, content asset, scene, build-settings entry), `BuildPrototype`, `TestResultExport` |
| `Data/` | `PrototypeDefinitions.asset` (spec tuning), `PrototypeContent.asset`, `Layouts/{Winning,Losing,Stress}Layout.json` |
| `Tests/EditMode`, `Tests/PlayMode` | Rules/routing fixtures; scene/input/UI lifecycle |

Regenerate generated assets with **The Last Fortress > Setup Prototype Scene** (it never overwrites the definitions asset).

## Tests

From the repository root, with the project **not** open in an Editor:

```powershell
$fortressEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
$fortressProject = (Resolve-Path 'game/The Last Fortress').Path
$fortressResults = Join-Path $fortressProject 'TestResults'
New-Item -ItemType Directory -Force -Path $fortressResults | Out-Null
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform EditMode -testResults "$fortressResults/EditMode.xml" -logFile "$fortressResults/EditMode.log"
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform PlayMode -testResults "$fortressResults/PlayMode.xml" -logFile "$fortressResults/PlayMode.log"
```

If the Editor already has the project open, use the Test Runner window (or MCP `run_tests`) instead; `TestResultExport` saves every run to `TestResults/{EditMode|PlayMode}-<timestamp>.xml`. Check the XML (`result="Passed"`, non-zero total), not just the exit code.

## Build

```powershell
& $fortressEditor -batchmode -nographics -quit -projectPath $fortressProject -executeMethod TheLastFortress.Editor.BuildPrototype.BuildWindows -logFile "$fortressResults/Build.log"
```

Or **The Last Fortress > Build Windows Prototype** in an open Editor. Output: `Builds/NavigationPrototype/TheLastFortress.exe` plus `build-report.txt`. Non-development, Windows x64, only the prototype scene; it throws on failure.

## Standalone command-line options

| Arguments | Effect |
|---|---|
| none | Normal one-wave game in Day |
| `-layout win` / `-layout lose` | Pre-applies the recorded winning / Keep-only layout |
| `-stress` | Stress fixture: 100 Grunts from four entries, spec layout, Keep/Walls invulnerable, red **STRESS FIXTURE** banner |
| `-stress -measure <file>` | 10 s warmup, 60 s frame-time capture, scripted west-blocker removal at 30 s, writes results and quits |
| `-capture <dir>` | Writes evidence screenshots (open route, breach preview, invalid ghost, night, victory, defeat, stress) and quits |

Example measurement on the reference machine:

```powershell
& 'Builds/NavigationPrototype/TheLastFortress.exe' -stress -measure TestResults/stress.txt -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
```

## Current limitations

- Primitive blockout art. The one-wave prototype scene has no audio; the MVP has synthesized audio (see below). HUD text uses the built-in legacy font (TMP Essentials not imported, to keep assets inside `Assets/TheLastFortress/`). The SVGs in `design/icons/` are not imported as SVG. The MVP HUD uses PNGs rasterized from them (`UI/Icons`).
- Units overlap visually; there is no crowd avoidance (by spec).
- Overlay lines can be hidden behind tall Towers; the BREACH label is screen-space, so it stays visible.
- Real frame deltas above 0.25 s are not caught up (prevents a spiral after hitches). Simulation results do not depend on frame rate.
- Tuning is provisional and appears lenient: see `plans/001-navigation-prototype-results.md`.

## Six-night MVP

Scene `Assets/TheLastFortress/Scenes/Mvp.unity` (content `Data/MvpContent.asset`, tuning `Data/MvpDefinitions.asset`, both separate from the prototype). Results: `plans/003-results.md`, `004-results.md`, `005-results.md`.

| Input | Action |
|---|---|
| `1`–`5` or cards | Wall 20, Archer Tower 70, House 40, Farm 60 (2 x 2), Spike Trap 35 |
| Right mouse drag | Move around the map (the board follows the pointer, move cursor while dragging); a right click without dragging still cancels the tool |
| `R` | Rotate the Tower/House/Farm ghost, or the selected one (cosmetic; Walls auto-connect, Traps have no facing) |
| Selection panel | Sell (floor(price/2)), Repair (5 HP per gold, exact quote), Rotate — Day only |
| Reward cards | Choose one of three perks after Nights 1–5 |
| **Menu** (top-right) | Resume, Save and Quit (Day only, back to the menu), Settings, Help; **Restart** = retry the same seed from Day 1 (asks to confirm once the run has progressed; immediate after Victory/Defeat or on an untouched Day 1) |
| **Help** (top-right) | Goal, rules in brief and the full controls list; **Show guide** resumes the first-run guide |
| Guide (top-right, first run) | Five skippable steps that advance only when the action has happened: build a Tower, inspect its range, optional income, Start Night, choose a reward. **Hide** closes it for now; **Skip guide** saves that choice in `profile.json` |
| **Quit game** (menu) | Exits directly when nothing is unsaved; with unsaved Day changes offers Save and exit / Exit without these changes / Cancel (exits only after the save succeeds); during Night/Reward explains that Continue replays the night from its Day checkpoint |

Launch opens the title menu over a staged settlement: New Run (**Seed…** reveals the optional seed, invariant unsigned decimal), Continue (returns to the start of the current night), Recover backup (only offered when the latest save is damaged), Settings, Help, Credits, Quit, plus the version and build id for bug reports. Saves live in `%USERPROFILE%/AppData/LocalLow/DefaultCompany/The Last Fortress/Mvp/` (`run.json`, `run.json.bak`, `run-N.json` for runs started after a damaged save, `settings.json` v2 with volumes, `profile.json` for the guide). If you already had run saves when the guide was added, it starts skipped.

Audio (MVP): 28 clips synthesized by `Assets/TheLastFortress/Audio/Source~/synth.py`. Provenance is in `Audio/credits.md`. Settings has Master/Music/Effects volume; changes play at once, and 0 is silence. `AudioDirector` gets the same event batch as the world view, once per frame. It allows at most 24 voices, at most 4 of one sound, and two music tracks during a crossfade. Combat sounds pause with the night, and arrow impacts sound when the drawn arrow lands. **The mix has not been listened to yet.** Regenerating clips or icons: run the scripts, then **The Last Fortress > Link Audio and Icons** (batch: `-executeMethod TheLastFortress.Editor.PresentationSetup.Run`).

Editor: **The Last Fortress > Setup MVP Scene** (idempotent; never overwrites `MvpDefinitions.asset`, only fills newly added enemy kinds) and **The Last Fortress > Build MVP** → `Builds/Mvp/TheLastFortress.exe` (Windows x64, non-development, `Mvp.unity` only). Batch: `-executeMethod TheLastFortress.Editor.BuildMvp.BuildWindows`.

| MVP arguments | Effect |
|---|---|
| none | Menu |
| `-newrun [-seed N]` | Skip the menu and start a saved run (generated terrain) |
| `-flat` | Use the flat authored map for New Run (recorded policies/debugging) |
| `-saves <dir>` | Save directory override (tests, captures) |
| `-stress [-measure <file>]` | Labeled MVP stress fixture: 100 maintained enemies (per entry 15 Grunt, 5 Runner, 4 Brute, 1 Golem), invulnerable Keep/Walls, debug House/Farm/Trap |
| `-capture <dir>` | Evidence screenshots (use with `-saves`) and quit |
| `-capture <dir> -offscreen` | The same, but rendered into a texture: works with a hidden or minimized window (`Start-Process -WindowStyle Hidden`) |
| `-capture-set 007` | Only the plan 007 sequence: title, guide, Help, Credits, Settings, announcements, Nights 4 and 6 at 1x with audio voice statistics, five Menu → New Run → end cycles. It writes `<res>-007-capture-log.txt` |

PlayMode tests set `SaveStore.DirectoryOverride` to a temp folder; storage tests use temp folders. Live-Editor test runs export XML to `TestResults/` as before.

## Twelve-night release profile (plan 008)

A third scene, `Assets/TheLastFortress/Scenes/Release.unity`, plays the plan 008 rules (`plans/008-rules.md`): twelve stored nights, nine building kinds (adds **Cannon**, **Watchtower**, **Repair Station**), seven enemy kinds (adds **Raider Archer**, **Bomber**, **Siege Commander**), a 24-perk reward pool and three map profiles. The six-night MVP scene, its tuning and its saves are unchanged and remain the regression build.

| Input | Action |
|---|---|
| `1`–`8` or the two rows of cards | Wall, Archer Tower, House, Farm, Spike Trap, Cannon, Watchtower, Repair Station |
| **Map: …** (title, next to New Run) | Cycles the map profile for the next New Run: Ravine (corridors), Crossing (split fronts at river fords), Meadow (open economy) |
| Hover a Cannon/Tower ghost or select one | Range ring from the simulation (perks and the Watchtower bonus included) |
| Watchtower ghost / selection | Outlines the towers it boosts (+1 range, never stacks) |
| Repair Station ghost / selection | The square it repairs at dawn |

- Saves share the MVP folder. A six-night save **continues in the release build as six nights under its own rules** (release cards are hidden for it); an MVP build reports a twelve-night save as incompatible and never overwrites it. Release runs save checkpoint schema 2.
- Tuning: `Data/ReleaseDefinitions.asset` (created once by setup; never overwritten). Map profiles, the night table and perks are code (`Runtime/Core/MapProfiles.cs`, `ReleaseCampaign.cs`, `PerkRules.cs`).
- Setup: **The Last Fortress > Setup Release Scene** (or `-executeMethod TheLastFortress.Editor.ReleaseSetup.SetupBatch`). Requires the MVP setup first. It regenerates only the `Prefabs/Release*.prefab` blockouts that the content asset still points to. Note: it calls the shared material generator, which rewrites a few MVP `.mat` files without visual change; restore them from git if they show up as modified.
- Validate: `-batchmode -nographics -quit -executeMethod TheLastFortress.Editor.ReleaseContentValidation.ValidateAll` → `TestResults/008/release-validation.txt`; throws on any error.
- Build: `-batchmode -nographics -quit -executeMethod TheLastFortress.Editor.BuildRelease.BuildWindows` → `Builds/Release/TheLastFortress.exe` + `build-report.txt` (validates first, release scene only).
- Extra command-line options: `-map ravine|crossing|meadow` (New Run map); `-capture <dir> -offscreen` in the release build runs the plan 008 evidence sequence (`<res>-008-capture-log.txt`).
- G0 diagnostic: `-executeMethod TheLastFortress.Editor.TacticsDiagnostic.Run` → `TestResults/008/tactics-diagnostic.txt`.

Art and audio for the new kinds are generated blockouts and reused 007 clips; see `plans/008-art-and-audio-bible.md`. Human validation of the release is pending (`plans/008-results.md`).
