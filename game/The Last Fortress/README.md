# The Last Fortress — navigation prototype and six-night MVP

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

- Primitive blockout art only; no audio. HUD text uses the built-in legacy font (TMP Essentials not imported, to keep assets inside `Assets/TheLastFortress/`). SVG icons from `design/icons/` are not imported; simple shapes stand in for them.
- Units overlap visually; there is no crowd avoidance (by spec).
- Overlay lines can be hidden behind tall Towers; the BREACH label is screen-space, so it stays visible.
- Real frame deltas above 0.25 s are not caught up (prevents a spiral after hitches). Simulation results do not depend on frame rate.
- Tuning is provisional and appears lenient: see `plans/001-navigation-prototype-results.md`.

## Six-night MVP

Scene `Assets/TheLastFortress/Scenes/Mvp.unity` (content `Data/MvpContent.asset`, tuning `Data/MvpDefinitions.asset`, both separate from the prototype). Results: `plans/003-results.md`, `004-results.md`, `005-results.md`.

| Input | Action |
|---|---|
| `1`–`5` or cards | Wall 20, Archer Tower 70, House 40, Farm 60 (2 x 2), Spike Trap 35 |
| `R` | Rotate the Tower/House/Farm ghost, or the selected one (cosmetic; Walls auto-connect, Traps have no facing) |
| Selection panel | Sell (floor(price/2)), Repair (5 HP per gold, exact quote), Rotate — Day only |
| Reward cards | Choose one of three perks after Nights 1–5 |
| **Menu** (top-right) | Resume, Save and Quit (Day only), Settings; **Restart** = same-seed retry |

Launch opens the menu: New Run (optional seed, invariant unsigned decimal), Continue (returns to the start of the current night), Recover backup (only offered when the latest save is damaged), Settings, Quit. Saves live in `%USERPROFILE%/AppData/LocalLow/DefaultCompany/The Last Fortress/Mvp/` (`run.json`, `run.json.bak`, `run-N.json` for runs started after a damaged save, `settings.json`).

Editor: **The Last Fortress > Setup MVP Scene** (idempotent; never overwrites `MvpDefinitions.asset`, only fills newly added enemy kinds) and **The Last Fortress > Build MVP** → `Builds/Mvp/TheLastFortress.exe` (Windows x64, non-development, `Mvp.unity` only). Batch: `-executeMethod TheLastFortress.Editor.BuildMvp.BuildWindows`.

| MVP arguments | Effect |
|---|---|
| none | Menu |
| `-newrun [-seed N]` | Skip the menu and start a saved run (generated terrain) |
| `-flat` | Use the flat authored map for New Run (recorded policies/debugging) |
| `-saves <dir>` | Save directory override (tests, captures) |
| `-stress [-measure <file>]` | Labeled MVP stress fixture: 100 maintained enemies (per entry 15 Grunt, 5 Runner, 4 Brute, 1 Golem), invulnerable Keep/Walls, debug House/Farm/Trap |
| `-capture <dir>` | Evidence screenshots (use with `-saves`) and quit |

PlayMode tests set `SaveStore.DirectoryOverride` to a temp folder; storage tests use temp folders. Live-Editor test runs export XML to `TestResults/` as before.
