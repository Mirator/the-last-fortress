# Plan 003 results — MVP economy foundation

Status: DONE (mechanical gates). Executed 2026-10-15 from HEAD `4855f69` in the live Editor (Unity 6000.3.16f1) pinned through Unity MCP to `The Last Fortress@ffd3a3a9` (`game/The Last Fortress`). Batch commands were not used because the project is open in that Editor; `TestResultExport` wrote the XML files below. Nothing was committed. Fresh-player validation remains pending; no fun/balance claim is made.

## Step 1 — verification baseline

Preflight: `git status` showed only the planning files (`plans/README.md` modified; `AGENTS.md`, plans 003–005 untracked). No drift in `Assets/TheLastFortress` since `4855f69`.

Reproduction: `plans/evidence/003-PlayMode-20261015-120052-reproduced-failures.xml` — 7/11 passed; the same four failures as the recorded review (`ClickOnHudNeverPlacesBehindIt`, `HotkeyToolAndClickPlaceThroughInputSystem`, `PausedNightBlocksBuildingButKeepsCameraAndInspection`, `RoutePreviewMatchesPlannerAndShowsBreach`). Each failed at the first assertion depending on a queued virtual keyboard/mouse event (tool still null after `Digit2`, `PointerOverUi` false, Space not pausing, Wall ghost absent).

Cause: this project has no Input System settings asset, so `backgroundBehavior = ResetAndDisableNonBackgroundDevices`. The runner reported `editor_is_focused: false` (Editor not the OS foreground window) and `Application.runInBackground = false`. Under that policy the Input System disables/ignores devices without background support while the application is unfocused, so `TestMouse`/`TestKeyboard` delivered nothing. The fixture already overrode `editorInputBehaviorInPlayMode` for the same reason, but not `backgroundBehavior`; results therefore depended on desktop focus — a harness defect, not game code. Standalone builds legitimately ignore input while unfocused, so no runtime change was made.

Fix: `PrototypeSceneTests` SetUp also sets `backgroundBehavior = IgnoreFocus`; TearDown restores the previous value. No assertion, input path or test body changed. Fresh run with the Editor still unfocused: 11/11 (`TestResults/PlayMode-20261015-120147.xml`); EditMode baseline 47/47.

## Implementation

| Area | Change |
|---|---|
| IDs/definitions | `BuildingKind` appended House=3, Farm=4, SpikeTrap=5 (0/1/2 unchanged). `PrototypeDefinitions` gained optional House/Farm/SpikeTrap plus `DailyIncome`/`TrapDamage`; `CreateDefault()` is still the untouched prototype set (new kinds absent → `NotPurchasable`). `MvpDefinitions.CreateDefault()` holds spec-003 values (start 250, Keep 500 HP +20/day, Wall 20/100, Tower 70/100, House 40/80/+15, Farm 60/100/+25, Trap 35/8 damage). |
| Footprints | Square footprint from the lowest `(x,z)` anchor; Farm offsets (0,0),(1,0),(0,1),(1,1), everything else 1 x 1. `Building` stores anchor, `Cells`, actual `PurchasePrice`, effective `MaxHp` and `Orientation` (0; used in 005). |
| Occupancy | `GridState` installs/releases every footprint cell with one `Revision` change; each cell resolves to its owner; `AllBuildings()` yields each entity once. Separate trap layer with its own `TrapRevision`, never touching `Revision`, so traps do not invalidate route caches. |
| Placement | Checks every footprint cell (bounds, ring, terrain, solid or trap) before affordability; no mutation on rejection. Footprint ghost preview via `PreviewRoute(start, kind, anchor)`. |
| Economy | `EconomyRules`: income (Keep + surviving Houses/Farms; Farm +5 once if any surviving House shares an edge, corners excluded), repair quote `min(missing, gold*5)` for `ceil(restored/5)`, unique Farm/House pairs. `PlacementService` remains the single gold owner (purchase, sale `floor(price/2)`, repair, income). Day-only repairs incl. Keep; no Trap repair. |
| Combat | Trap: 8 damage once on completing entry (hook at segment arrival). Wall with any adjacent trap reflects 3 per received melee hit (non-stacking, non-recursive, decided before the hit so the lethal hit still reflects); kills recorded once via one `DamageEnemy` path. |
| Campaign | Optional `RunConfig.Campaign` (`CampaignConfig`); campaign + stress throws. `MvpCampaign.CreateFoundation()` = "Foundation — 3 nights" (10 W; 16 W/E; 22 W/E; Grunts every 1.5 s). Cleared non-final night → `EnterDay` (once, guarded) resets spawns/cooldowns, keeps gold/HP/buildings, pays income once (`LastPaidDay`); final night → Victory; no income on Day 1 or after the last night. IDs stay unique across nights; `Restart()` resets the whole campaign. |
| Unity | `MvpDefinitionsAsset` (separate tuning asset), `PrototypeContent.mvpDefinitions` selects MVP mode; explicit `PrefabFor` per kind (throws instead of defaulting to Tower). Hotkeys/cards 1–5, footprint ghost with per-cell outline, routes for every active entry, raised shared-edge Farm/House and Wall/Trap highlights, income forecast, night progress, exact upcoming entries/counts/first–last time, dawn income message, HP with effective max, Day-only Sell/Repair with exact quote, trap selection. Prototype HUD/controls unchanged in the prototype scene. |
| Editor/build | `MvpSetup` (menu *Setup MVP Scene*; idempotent; stops on unsaved scenes; never overwrites `MvpDefinitions.asset` or prototype prefabs) created `Data/MvpDefinitions.asset`, `Data/MvpContent.asset`, `Prefabs/{House,Farm,SpikeTrap}.prefab`, `Materials/Crop.mat`, `Scenes/Mvp.unity` and its build-settings entry. `BuildMvp.BuildWindows` (menu *Build MVP*) → `Builds/Mvp/TheLastFortress.exe`. `BuildPrototype` unchanged. `CaptureSequence` gained an MVP branch (`-capture <dir>`). |

## Verification

Tests added (focused, plan-003 gates only): `Tests/EditMode/MvpFoundationTests.cs` — Farm four-cell ownership/once-only release; invalid/unaffordable footprint without mutation; traps walkable and route-cache-neutral; Farm ghost preview = committed route; unique edge adjacency (second House and corner House do not add); repair rounding cases (7 missing → 7 HP for 2 gold, discriminating ceil from floor; gold-limited; no-op cases); Day-only/paused-Night repair; trap entry-once/no standing tick; two traps reflect 3 not 6 and the Wall's lethal 10th hit reflects the Grunt's lethal 10th 3 damage in the same step; cleared night with a combat-destroyed House pays exactly 45 once, preserves HP, no pay on extra steps, unique enemy IDs, manual start, no income after final night; foundation profile won on the real 250 budget with a recorded policy, then restart equals a fresh snapshot, Keep-only defeat, restart again; campaign+stress rejected. `Tests/PlayMode/MvpSceneTests.cs` — one scene smoke: Farm via hotkey 4 + click through the Input System, one view for four cells, Farm/House link visible, forecast +65, Night 1 cleared → Day 2 with +65 paid, Night 2 preview shows East, manual start.

| Evidence | Result |
|---|---|
| `plans/evidence/003-EditMode-20261015-122215.xml` | Passed, 62/62 (47 original + 15 new) |
| `plans/evidence/003-PlayMode-20261015-122546.xml` | Passed, 12/12 (11 original + 1 new) |
| `plans/evidence/003-mvp-build-report.txt` | Succeeded, 0 errors, 1 warning; scenes = `Mvp.unity` only |
| `plans/screenshots/003-mvp/*.png` | Standalone build, `-capture`, 1280 x 720: launch Day 1, 2 x 2 Farm ghost valid/invalid, Night 1, Day 2 dawn income + Keep repair quote (+37 HP for 8 gold), Farm/House link, Victory after 3 nights, Keep-only defeat, restart |

The first MVP build reported 487 warnings, all shader compile warnings from the pre-existing `com.unity.ai.inference` package's `ConvGeneric.compute` on a cold shader cache; subsequent builds report 1 (same as the prototype build). No package change was made.

Recorded foundation policy (EditMode fixture, normal rules/budget): Day 1 Towers (8,9),(8,11), House (12,12); Day 2 Tower (12,9); Day 3 Tower (12,11) if affordable → Victory; income paid 35 on Days 2 and 3 only.

Tuning: none (0 of 3 passes used). Prototype definitions/assets untouched.

## Remaining issues / handoff

- Visual: synergy edge highlights are visible but small; trap plates are low-contrast; enemy kinds/art are 004/005 work.
- HUD night timer uses the OS culture decimal separator (pre-existing prototype formatting).
- After PlayMode runs the Editor rewrote `ProjectSettings/ProjectSettings.asset` (`preloadedAssets` lost `InputSystem_Actions`) and transiently `EditorSettings.asset`; the MVP setup added `Scenes/Mvp.unity` to `EditorBuildSettings.asset` (intended). The unintended ProjectSettings drift is restored before final completion (see 005 results).
- Last verified step: plan 003 complete. Next action: plan 004 step 1.
