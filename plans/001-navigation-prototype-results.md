# Plan 001 execution results: navigation prototype

Executed 2026-10-15 against `plans/001-navigation-prototype.md` and `specs/archive/002-navigation-prototype.md`. The six-night MVP (`specs/003`) is out of scope and was not started.

## Baseline

- Repository: unborn `main`, no HEAD commit, so there is no baseline SHA. `game/`, `specs/`, `plans/`, `design/` were untracked. No branch was created (none can be cut from an unborn HEAD) and **nothing was committed**.
- Editor `6000.3.16f1` matches `ProjectVersion.txt`. Before the work, `Assets/` held only the template (SampleScene, Settings, InputSystem_Actions, TutorialInfo). There was no gameplay code to integrate.
- `Packages/manifest.json` and the lockfile are unchanged. No packages were added or upgraded.
- Reference hardware verified on this machine: AMD Ryzen 5 3600, NVIDIA GeForce GTX 1660 SUPER, 32 GiB.

## Result summary

| Gate | Result | Evidence |
|---|---|---|
| Clean compile | Passed: no errors in the Editor console | — |
| EditMode suite | **47 / 47 passed** | `evidence/EditMode-20261015-111734.xml` |
| PlayMode suite | **11 / 11 passed** | `evidence/PlayMode-20261015-110617.xml` |
| Windows x64 non-development build | **Succeeded**, 0 errors, 129.8 MB | `evidence/build-report.txt` |
| Standalone launches in Day | Yes: launch-state screenshots at both resolutions | `screenshots/*-00-launch-state.png` |
| Recorded winning layout wins | Yes: Victory, Keep 500/500 | EditMode `WinningLayoutWinsOnNormalBudget`, PlayMode `RecordedLayoutsReproduceOutcomes`, `screenshots/*-05-victory.png` |
| Keep-only layout loses | Yes: Defeat | `LosingLayoutLoses`, `screenshots/*-06-defeat.png` |
| Open route and breach preview match planning | Yes | `RoutePreviewMatchesPlannerAndShowsBreach`, `PreviewParityWithoutMutation`, `screenshots/*-01`, `*-02`, `*-03` |
| Restart clears all state; stress flags isolated | Yes | `RestartRestoresExactInitialStateAfterEveryOutcome`, `TerminalOverlaysOfferRestartToExactInitialState`, `StressFlagsDoNotLeakIntoNormalRuns`, `StressModeIsLabeledAndDoesNotLeakIntoNormalMode` |
| 100-enemy performance gate (reference machine, 1080p) | **Passed**: median 2.41 ms, p95 3.10 ms, p99 3.62 ms | `evidence/stress-measurement-1920x1080.txt` |
| Fresh-player playtest review | **Not done**: owner action | — |

Superseded run: `TestResults/PlayMode-20261015-110128.xml` failed 11/11, every one in TearDown. The bootstrap read the virtual mouse one frame after the test removed it. Fixed in the test teardown and in `PlayerController`; the 110225 and 110617 runs passed. An earlier EditMode run (110112, 47/47) was superseded by the final 111734 run.

## How tests were run (deviation from the plan's commands)

The Unity Editor already had the project open, and the plan says not to start a second Editor against it or kill the user's. The PowerShell batch-mode commands in the plan were therefore **not** executed. Instead:

- Tests ran in the live Editor through the Unity MCP `run_tests` integration.
- `Assets/TheLastFortress/Editor/TestResultExport.cs` hooks `TestRunnerApi` and writes NUnit XML for every run to `TestResults/{EditMode|PlayMode}-<timestamp>.xml`. The XML above came from there, and the result was checked in the XML, not from exit codes.
- The build ran through the menu item `The Last Fortress > Build Windows Prototype`, which calls the same `TheLastFortress.Editor.BuildPrototype.BuildWindows`. Evidence is the `build-report.txt` it writes plus the executable. The batch command in the README should work once the Editor is closed, but **it has not been run**.

## Test coverage map (spec 002 fixtures)

| Fixture | Tests |
|---|---|
| 1 Coordinates | `TileCentersRoundTripForEveryCell`, `OutsidePointsAreRejectedNotClamped` (-0.01, 20, negatives), `BoundariesFloorIntoCells`, `MainEntryAcceptsEnemiesButRejectsBuilding` |
| 2 Atomic spending | `TowerPurchaseIsAtomic` |
| 3 Refund | `SellRefundsHalfOnceAndKeepCannotBeSold`, `WallRefundFloorsHalf` |
| 4 Damage | `ThreeTowerHitsKillGruntAtExactRangeBoundary`, `DestroyedBlockerReleasesOnceAndEnemyTakesOpenedRoute` (10 hits, 20-tick spacing) |
| 5 Range | `ThreeTowerHitsKillGruntAtExactRangeBoundary` (acquired at exactly 4.0; Tower at 5.0 never fires; Wall between does not block), `RangePredicateIsInclusiveEuclidean` |
| 6 Pause | `PauseFreezesEverythingAndResumeContinues` (mid-spawn interval, mid-cooldown, partial step), PlayMode `PausedNightBlocksBuildingButKeepsCameraAndInspection` |
| 7 Completion | `ZeroAliveBetweenSpawnsDoesNotWinAndVictoryHappensOnce` |
| 8 Defeat | `KeepOnlyLosesAndDefeatCancelsPendingSpawns`, `SameStepFinalKillCannotOverrideDefeat` |
| 9 Restart | `RestartRestoresExactInitialStateAfterEveryOutcome` (snapshot equality after Defeat, Victory, paused Night) |
| 10 Detour | `DetourIsFollowedAndWallsNeverDamaged` (actual walked path equals the displayed route) |
| 11 Full enclosure | `FullEnclosureBreachesWestWallFromWestNeighbor` |
| 12 Destruction | `DestroyedBlockerReleasesOnceAndEnemyTakesOpenedRoute` |
| 13 Preview parity | `PreviewParityWithoutMutation`, PlayMode `RoutePreviewMatchesPlannerAndShowsBreach` |
| 14 Indestructible barrier | `IndestructibleBarrierIsValidationErrorNotTarget` |
| 15 Moving enemy | `MovingEnemyFinishesSegmentThenReplans` |
| Determinism | `EqualElapsedTimeInDifferentChunksGivesIdenticalState` (0.05 s vs 0.016/0.017/0.1 s vs 0.3/0.7 s chunks) |
| Scene / UI / input | PlayMode: one bootstrap and one Keep; hotkey + click placement via virtual Input System devices; UI click never reaches the world (and does select the card); Start Night double-click; paused restrictions; destruction removes the visual after the sim event; terminal Restart; scene reload; stress label isolation |

## Normal-wave layouts

Stored as `Assets/TheLastFortress/Data/Layouts/*.json` and loaded by tests and by `-layout win|lose`.

- **Winning** (`WinningLayout.json`): Tower (9,9), Tower (9,11), Tower (11,11), Wall (5,10), Wall (6,10). Costs 250, leaving 0 gold. Victory at 21.5 s Night time, Keep **500/500**.
- **Losing** (`LosingLayout.json`): Keep only. Defeat.

**Tuning observation (definitions not changed):** the provisional values look too lenient. A sweep of every legal single-Tower placement (70 gold, nothing else) won in **43 of 323** positions, and none of the five three-Tower candidate layouts I tried let the Keep take any damage. That points to a weak main wave (10 Grunts × 30 HP against a 500 HP Keep) rather than a strong Tower. A tuning pass, e.g. more Grunts or a lower Keep HP, is the owner's call, and this fixture should be re-run afterwards. As the plan instructs, no tuning change was made.

## Stress measurement (reference machine)

From `evidence/stress-measurement-1920x1080.txt` (`TheLastFortress.exe -stress -measure ... -screen-width 1920 -screen-height 1080 -screen-fullscreen 0`):

| Field | Value |
|---|---|
| Build | non-development Windows x64, Direct3D12 |
| Resolution / mode | 1920 x 1080, windowed |
| Quality / sync | `PC` quality level, vSync 0, targetFrameRate -1 (forced by measure mode) |
| Hardware | AMD Ryzen 5 3600, GTX 1660 SUPER, 32 GiB |
| Warmup / capture | 10 s / 60 s, **24 860 frame samples**, 1 200 simulation steps during capture |
| Active Grunts | min 100, max 100 throughout capture |
| Median / p95 / p99 / max | **2.406 / 3.095 / 3.616 / 17.188 ms** (gate 16.7 / 20 / 33.3), percentile = `sorted[ceil(p*n)-1]` |
| Spikes > 33.3 ms | none |
| Scripted event | west entry's first blocker **Wall (6,9)**, 21 Grunts actively attacking it immediately before removal; grid revision 41→42; the west route then breaches at (8,9). `event_valid=True` |
| Event frame | sample 12 540: 3.767 ms (neighbours 3.313, 2.741, 2.926, 3.163) |
| Gate | **pass** |

The stress run's `Player.log` (`%USERPROFILE%\AppData\LocalLow\DefaultCompany\The Last Fortress\Player.log`) has no exceptions; its last line is the measurement summary.

Limits of this result: one run, windowed rather than exclusive fullscreen, primitive art. Re-run after the art pass, as `design/README.md` requires.

## Screenshots

`plans/screenshots/`, captured by the standalone build's `-capture` sequence at **1920x1080** and **1280x720**:

`00-launch-state` (untouched default launch: Day, normal wave), `01-day-open-route` (detour around 3 Walls, Tower range), `02-day-breach-preview` (ghost sealing the Keep → coral crosshair + BREACH label + "If built: SEALED" text), `03-day-invalid-ghost` (reserved ring rejection, committed route kept), `04-night-combat`, `05-victory`, `06-defeat`, `07-stress-fixture` (red STRESS FIXTURE banner).

The 1080p set came from the first build. Capture mode lacked `runInBackground`, so the first 720p attempt stalled when its window lost focus; I stopped it, fixed it, rebuilt, and recaptured at 720p. Gameplay code was identical between the two builds. Two Editor screenshots are in `screenshots/editor/`.

Visual review notes: silhouettes are distinct, Grunts stay visible at night, and HUD costs and HP are readable at both sizes. Known issue: Towers placed south-west of the Keep can hide the ground route line and the breach crosshair. The screen-space BREACH label stays visible, but an overlay that draws on top of geometry would be a better fix in the art pass. At 720p the controls hint is small (~12 px); the main labels stay readable.

## Deviations and notes

- **HUD text:** legacy `UnityEngine.UI.Text` with the built-in font, not TMP. Importing TMP Essentials would add assets outside `Assets/TheLastFortress/`, which the plan's scope forbids. **SVG icons** from `design/icons/` are not imported; simple square/diamond shapes stand in for them, as the design README allows.
- **Prefabs/materials/scene** are generated by `Editor/PrototypeSetup.cs` (idempotent; it never overwrites the definitions asset). Nothing was hand-written in Unity YAML. The HUD and board are built at runtime from code; entity visuals are generated prefabs.
- **Frame hitch cap:** real frame deltas above 0.25 s are not caught up. Simulation results do not depend on frame chunking; the core determinism test covers that.
- **Start Night double-click:** a second click within 0.4 s is ignored rather than becoming Pause.
- **Stress respawn:** killed Grunts respawn at the end of the same step, so exactly 100 stay active between steps. Each entry keeps 25; initial release is staggered 0.1 s per entry.
- **Unity quirk:** the Editor kept a stale source list for the EditMode assembly, and one new file was silently left out of compilation. Renaming it (`CombatPhaseStressTests.cs`) fixed the problem. Source counts per assembly were then checked against disk.
- **ProjectSettings:** `EditorBuildSettings.asset` gained the enabled prototype scene (SampleScene kept). The Editor also re-saved `ProjectSettings.asset`, `EditorSettings.asset`, `ShaderGraphSettings.asset`, `URPProjectSettings.asset` and `SceneTemplateSettings.json` during the session. With no git baseline I cannot diff them; `activeInputHandler: 1` and the product name are unchanged. PlayMode tests set `InputSystem.settings.editorInputBehaviorInPlayMode` to `AllDeviceInputAlwaysGoesToGameView` and restore it in TearDown. The project has no `.inputsettings` asset, so nothing persists.
- **Editor state:** the setup script switched the open Editor scene from `SampleScene` to `NavigationPrototype`.
- **Added outside `Assets/`:** root `.gitignore` (Library/Temp/Obj/Logs/UserSettings/Builds/TestResults/IDE files; Assets/Packages/ProjectSettings/specs/plans stay tracked), `game/The Last Fortress/README.md`, and this file plus `plans/evidence/` and `plans/screenshots/`.

## Not done / owner actions

- Fresh-player playtests of an open corridor and a sealed fortress: can players predict the breach and explain a loss, and does maze length dominate? Automated tests do not show the game is fun.
- Decide on tuning (see the observation above), then re-run the layout fixtures.
- Commit and branch when ready; this run made no commits.
