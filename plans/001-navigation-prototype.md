# Plan 001: Build the one-wave fortress navigation prototype

## Status and intent

- Priority: P0. Effort: L (multi-day). Risk: medium, chiefly routing correctness and Unity scene/input integration.
- Category: direction, correctness, tests.
- Status: DONE 2026-10-15 (mechanical gates); fresh-player playtest review still pending. Evidence: `plans/001-navigation-prototype-results.md`. Depends on: no other implementation plan.
- Planned on: 2026-10-15, unborn `main` branch, no HEAD commit. The repository currently has untracked `game/`, `specs/`, and newly authored `plans/`. Do not claim a baseline SHA exists.
- Owner selected specifications and plan first. This plan is handed off for a subsequent implementation task; it does not mark the prototype implemented.

Implement only the first playable proof: buy Walls and Archer Towers, preview the route, start one Grunt wave, observe automatic combat, then win/lose and restart. The key gameplay contract is that enemies follow any open route before breaching; fully enclosing the Keep is legal and causes an attack on a blocking structure. Do not silently replace that policy with travel-time optimization.

Read the full plan and `specs/002-navigation-prototype.md` before editing. That specification is the behavioral contract; the summaries below let the executor understand the task without this conversation. `specs/003-six-night-mvp.md` is context for future expansion, not implementation scope.

## Baseline and drift check

Repository root: `C:/Users/Miroslav Pavelek/Documents/Games/the-last-fortress`.

| Existing file | Observed state |
|---|---|
| `game/The Last Fortress/ProjectSettings/ProjectVersion.txt` | `m_EditorVersion: 6000.3.16f1` |
| `game/The Last Fortress/Packages/manifest.json` | URP 17.3.0, Input System 1.19.0, Test Framework 1.6.0; existing MCP and other template dependencies |
| `game/The Last Fortress/ProjectSettings/ProjectSettings.asset` | `activeInputHandler: 1`, product name `The Last Fortress` |
| `game/The Last Fortress/ProjectSettings/EditorBuildSettings.asset` | `Assets/Scenes/SampleScene.unity` enabled |
| `game/The Last Fortress/Assets/` | Scenes, Settings, InputSystem_Actions, TutorialInfo; no gameplay scripts or gameplay tests |

Run from repository root:

```powershell
git status --short
Get-Content 'game/The Last Fortress/ProjectSettings/ProjectVersion.txt'
rg --files 'game/The Last Fortress/Assets' -g '*.cs' -g '*.asmdef' -g '*.unity'
```

Expected baseline: only sample scene/template scripts and this specified editor version. If another agent has since implemented gameplay, inspect and integrate it rather than replacing it. If a baseline commit now exists, record its SHA in the execution report and inspect changed in-scope files before proceeding. Preserve unrelated untracked/user work. There is no gameplay coding convention to copy yet; use the explicit conventions below.

## Scope and conventions

Create all new game assets under `game/The Last Fortress/Assets/TheLastFortress/`:

```text
Runtime/Core/                 ordinary C# state, placement, path planning, simulation
Runtime/Unity/                rendering, input, bootstrap, UI, definition assets
Runtime/TheLastFortress.Runtime.asmdef
Editor/BuildPrototype.cs
Editor/TheLastFortress.Editor.asmdef
Tests/EditMode/               pure rules and routing fixtures
Tests/PlayMode/               actual scene/input lifecycle coverage
Tests/EditMode/TheLastFortress.EditMode.Tests.asmdef
Tests/PlayMode/TheLastFortress.PlayMode.Tests.asmdef
Scenes/NavigationPrototype.unity
Data/                        serialized definitions and fixtures
Prefabs/                     reusable entity visuals and HUD
Materials/                   simple URP materials
```

Namespace `TheLastFortress`; one clear responsibility per class. Name core types `GridState`, `PlacementService`, `RoutePlanner`, `RunSimulation`, `WavePlan`, and `BuildingDefinition`/`EnemyDefinition`, or document a small equivalent decomposition. Treat grid/simulation state as authoritative: destroyed GameObjects do not independently determine occupancy or victory. Unity adapters reflect simulation events.

Allowed supporting changes: add root `.gitignore` for Unity-generated Library/Temp/Obj/Logs/UserSettings, Builds/TestResults and IDE outputs (do not ignore Assets, Packages, ProjectSettings, specs, or plans); add `game/The Last Fortress/README.md`; update build scene list to enable the prototype; update this plan's status. Preserve any existing ignore rules. Keep `.meta` files for new Unity assets. No hand-authored scene/prefab YAML: use the Editor or an idempotent editor setup script.

Do not edit other specifications, remove template packages, upgrade Unity/URP, or add networking, NavMesh gameplay routing, ECS, purchased assets, procedural maps, soldiers, traps, economy income, rewards, saving, music, or release content. Avoid a service locator or ten singleton managers.

Git: use `codex/navigation-prototype` if a branch is needed and creation is possible with the current repository state. There is no existing commit style. Do not create a baseline commit, commit user files, push, or open a PR without authorization for that action. No destructive reset/cleanup.

## Implementation sequence

### 1. Establish the scene and test baseline

Create runtime and test assemblies, a minimal bootstrap scene, and initial meaningful coordinate/initial-state tests. Use installed packages. The project uses the new Input System: do not use legacy-only `UnityEngine.Input` APIs. For new runtime HUD use uGUI as the default unless inspection finds an established gameplay UI framework; template readme styles are not gameplay UI precedent.

Invoke the Unity UI and uGUI skills if available before generating UI. Use the Unity CLI/Editor integration skill if available for live Editor work. If the Editor already owns this project, use its Test Runner/live integration instead of starting a second batch process against it.

**Verify:** Unity compiles without errors; EditMode coordinate tests pass for centers, boundaries, and negative coordinates. Enter the saved scene and confirm exactly one bootstrap instance and one Keep. At this point establish the test commands described below; no gameplay test command had been run at planning time.

### 2. Implement authoritative placement and path planning

Build the 20 x 20 flat logical grid; all outer-ring cells are non-buildable. Keep is at `(10,10)`. Tile center is `(x+0.5,0,z+0.5)`. Cardinal movement only; units do not block each other. All three buildings occupy one cell. Start with 250 gold; Wall costs 20, Tower 70; sell refunds half the actual price rounded down, once; Keep cannot be sold. No Night construction, selling, or repairs.

Implement transactional placement and temporary preview occupancy without mutation. Search first for an open route to a Keep neighbor. When none exists, use uniform-cost search with cost 1 for free land and 6 for destructible occupied land; never traverse Keep/indestructible terrain. Select first blocker and attack from its neighbor. Resolve ties by the exact ordering in specification 002. Track grid revisions and invalidate caches on occupancy changes.

**Verify:** EditMode tests pass for atomic gold/occupancy changes, rejection, sale idempotence, detour, full enclosure, tie stability, unreachable indestructible barrier, blocker removal, and preview parity. Fixtures include the exact coordinates in specification 002, not only mocked pathfinder output.

### 3. Implement simulation, movement, combat, and wave completion

Use a 0.05-second fixed simulation step, independent of visual frame rate. Release due spawns, process invalidation/movement, Towers, surviving enemies, then defeat before victory. Release the time-zero spawn upon entering Night. Use stable entity/spawn IDs. Interpolate rendering separately; enemies finish an in-progress tile segment before route replanning, without teleporting.

Definitions: Keep 500 HP; Wall/Tower 100 HP; Tower damage 10 every 0.75 s, range 4 inclusive Euclidean; Grunt 30 HP, speed 1 tile/s, damage 10 every 1 s from a cardinal neighbor. Tower targets nearest enemy, tie by spawn ID, retains valid target. First hit waits a full interval; target changes reset cooldown; arrows are cosmetic, damage immediate. No line-of-sight or crowd collision.

Main wave: ten Grunts from `(0,10)` at times 0, 1.5, ..., 13.5. States Day/Night/Victory/Defeat, with pause separate. No rewards. Pending spawns prevent victory. Keep loss overrides simultaneous final kill. Restart restores every aspect of the initial state and cancels pending callbacks.

**Verify:** EditMode simulation tests cover exact hit counts/timing, range boundary, pause/resume, target change, no dead-entity attacks, pending-spawn completion, defeat priority, same-step destruction, and restart. Simulating equal elapsed time in different render-frame chunks must produce the same final simulation state. PlayMode tests verify the actual scene transitions and visual/occupancy removal after destruction.

### 4. Add controls and readable previews

Create the fixed-orientation orthographic camera with pan/zoom/reset. Add Wall/Tower toolbar, selected structure HP and Sell, gold, Keep HP, state, alive/scheduled enemy count, Start Night, Pause/Resume, and Restart. Construction ghost shows rejection reason and price. Show tower range, current route, and candidate route; breach targets use a distinct icon/label. UI pointer events cannot leak into world placement.

Use simple silhouettes/materials. Hotkeys: 1 Wall, 2 Tower, WASD/arrows pan, wheel zoom, Space Night pause/resume, right click/Escape cancel tool. Camera and inspection still work while paused. No construction while paused in Night. Terminal overlay clearly states outcome.

Visual reference: `design/README.md` and `design/concepts/`. Use its palette and distinct silhouettes for the primitive pass; the generated images are not literal grid layouts or production 3D models. Optional SVG icon sources are in `design/icons/`; do not add a dependency just to import them. An art pass must preserve the existing gameplay scope and footprints.

**Verify:** PlayMode tests exercise tool placement and UI hit blocking with the Input System, Start Night double-click protection, paused input restrictions, terminal Restart, and scene reload without duplicate subscriptions. Manually inspect saved scene at 1280 x 720 and 1920 x 1080. Capture open-route, breach-preview, and outcome screenshots.

### 5. Add reproducible layout and stress fixtures

Keep the normal wave and stress mode separate and visibly labeled. Serialize the exact 30-Wall/10-Tower stress layout in specification 002. Maintain 100 active Grunts across four entries; Keep/Walls are invulnerable only in stress mode. Record the west route's first Wall blocker and remove it at 30 measured seconds. Validate that it is actively attacked then. Record per-frame times after a 10-second warmup for 60 seconds, compute percentiles using the specified formula, and include the event frame.

Find and serialize one normal-wave winning placement list and one losing list. A Keep-only loss is valid; a win must use the ordinary 250-gold budget and unchanged combat definitions. If no layout wins, record evidence and propose a small tuning change in the execution report; do not silently change definitions or weaken acceptance criteria.

**Verify:** fixture tests check counts, unique occupancy, valid coordinates, target blocker, and invulnerability isolation. Run normal-game tests again after stress mode to ensure debug flags do not leak. Run the standalone reference-machine measurement and record all metrics; do not substitute Editor FPS.

### 6. Package and hand off

Implement `TheLastFortress.Editor.BuildPrototype.BuildWindows` in `Editor/BuildPrototype.cs`: build only the prototype scene for Windows x64 to `Builds/NavigationPrototype/TheLastFortress.exe`; use non-development mode, inspect BuildReport, and fail the command on unsuccessful build. The build method must not overwrite unrelated settings or include stress mode as the default start state.

Write `game/The Last Fortress/README.md` with editor version, scene, controls, test/build commands, stress-mode entry, output paths, and current limitations. Save execution evidence in `plans/001-navigation-prototype-results.md` with test counts, build result, screenshots/layout references, timing evidence, and any unmeasured gates.

**Verify:** clean compile, both test suites pass, standalone launches in Day, a recorded layout wins, Keep-only loses, restart works, normal and stress results are distinguished, and all changed files fall within scope. Only mark DONE when required evidence exists; report blocked hardware/build gates explicitly.

## Verification commands

Run in PowerShell from repository root. The editor executable and project version were verified during planning. Test/build invocations below are the interface the implementation must establish; they have not been executed against nonexistent gameplay tests. If Unity already has the project open, run equivalent EditMode/PlayMode suites via that Editor and export results, then use its build menu/integration. Do not kill the user's Editor to run these commands.

```powershell
$fortressEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
$fortressProject = (Resolve-Path 'game/The Last Fortress').Path
$fortressResults = Join-Path $fortressProject 'TestResults'
New-Item -ItemType Directory -Force -Path $fortressResults | Out-Null

& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform EditMode -testResults "$fortressResults/EditMode.xml" -logFile "$fortressResults/EditMode.log"
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform PlayMode -testResults "$fortressResults/PlayMode.xml" -logFile "$fortressResults/PlayMode.log"
& $fortressEditor -batchmode -nographics -quit -projectPath $fortressProject -executeMethod TheLastFortress.Editor.BuildPrototype.BuildWindows -logFile "$fortressResults/Build.log"
```

Execute separately and inspect each exit/result before proceeding; do not launch concurrent Editors for the same project. Where PowerShell returns before a GUI-subsystem process completes, use `Start-Process -WindowStyle Hidden -Wait -PassThru` with correctly quoted arguments and inspect its ExitCode; a returned shell prompt alone is not proof of completion.

Expected test outputs: fresh XML reports, `result="Passed"`, total test count greater than zero, zero failed tests, and no compile errors in the logs. Remove or timestamp prior result files before each run so stale XML cannot pass the gate. Verify XML, not just process exit codes. Expected build outputs: successful BuildReport, executable plus associated data folder, and no build errors.

All build/test outputs go under the project’s ignored Builds/TestResults directories. The visual performance gate requires a rendered standalone run, not `-nographics` test execution.

## Done criteria

- [x] Coordinate, placement, routing, combat, phase, pause, and restart regression fixtures in specification 002 pass.
- [x] Scene-level UI/selection/placement/pause/restart tests pass, with nonzero fresh EditMode and PlayMode results.
- [x] Saved prototype scene opens and standalone Windows build launches in Day.
- [x] Recorded legal winning and losing layouts reproduce outcomes.
- [x] Open route and breach preview match actual planning, with captured screenshots.
- [x] Reference Ryzen 5 3600 / GTX 1660 SUPER / 32 GiB machine at 1080p: 100-enemy run median <=16.7 ms, p95 <=20 ms, p99 <=33.3 ms; destruction event recorded. A failed/unmeasured gate is explicitly reported, not marked passed.
- [x] Restart clears all old state; stress overrides never affect normal mode.
- [x] No out-of-scope content or dependency upgrades; no hand-edited Unity asset YAML.
- [x] README and results evidence exist; update `plans/README.md` accurately.

## Stop and report conditions

- Existing gameplay or an unexpected editor/version mismatch contradicts the recorded baseline and cannot be reconciled without overwriting user work.
- A required route has no breach path on the flat fixture, or cached/preview and actual paths disagree after two focused fix attempts.
- Implementation requires a package upgrade, different routing policy, commercial asset, or additional game system.
- Tests/build remain blocked after two focused fixes, or an Editor lock prevents safe verification. Preserve work and explain the actual blocker.
- No normal-budget winning layout exists with the provisional values: report evidence and propose a small tuning change rather than weakening wave completion or making the Keep secretly attack.

## Maintenance and playtest review

Multi-cell Farms, trap layers, reflection/burn ordering, income, and checkpoint saves are intentionally deferred. They will require new tests and a separate MVP plan. The prototype's simple search must not become an excuse for premature flow-field/ECS infrastructure.

After mechanical verification, have fresh players try at least an open corridor and sealed fortress. Record whether they predicted the breach and can explain a loss, and whether maze length dominates every useful strategy. Passing tests and a performance gate do not establish fun or justify implementing full-release content.
