# Plan 003: Build the economy and multi-night foundation

## Execution contract

Status: TODO. Priority: P0. Effort: L. Planned 2026-10-15 at commit `4855f69`. Run this before 004 and 005. This is an implementation handoff, not a record of completed work.

Read root `AGENTS.md`. The owner authorized the complete 003 → 004 → 005 sequence for one implementing agent, bounded tuning with before/after evidence, minimal existing-test fixes only when a harness defect is demonstrated, and a later art pass with connected/rotating Walls. Implement this plan, verify it, record results, then continue to 004 without routine approval. Do not implement 005 early.

Repository: `C:/Users/Miroslav Pavelek/Documents/Games/the-last-fortress`. Unity project: `game/The Last Fortress/`, editor `6000.3.16f1`. Gameplay sources are under `Assets/TheLastFortress/`. This plan adds a separately selectable MVP foundation; the existing one-wave prototype and stress fixture remain available as reference/debug modes. Follow `specs/003-six-night-mvp.md` for economy rules, with the explicit temporary wave profile below. Original prototype behavior remains governed by specification 002.

## Current state and preflight

Run `git status --short`, `git rev-parse --short HEAD`, and `git diff 4855f69 -- "game/The Last Fortress/Assets/TheLastFortress"` before editing. A later HEAD is not automatically a blocker: inspect drift and preserve other work. Do not reset files or create a replacement project.

Observed baseline:

- `Runtime/Core/GridState.cs`: `readonly Building[] occupants`, single-cell `Install`/`Release`; `AllBuildings()` currently enumerates cells. This must not emit a Farm four times after footprints are added.
- `Runtime/Core/PlacementService.cs`: `Gold` is private-set, price from `definitions.For(kind)`, `RefundFor` uses actual instance `PurchasePrice / 2`. Reuse one balance owner for purchases, income, and repairs.
- `Runtime/Core/Definitions.cs`: Keep/Wall/Tower enum IDs 0/1/2; `CreateDefault()` is prototype tuning. Preserve these IDs and defaults.
- `Runtime/Core/RunSimulation.cs`: `RunState` Day/Night/Victory/Defeat, one `config.Wave`, `StartNight()`, `Restart()`, `Step()`, `CaptureSnapshot()`. Grid is authoritative; views consume events, not vice versa.
- `Runtime/Unity/PrototypeContent.cs`: only four prefab references; `PrefabFor` defaults to Tower. Replace fallback assumptions with explicit handling for added types, without breaking the prototype.
- `Runtime/Unity/PrototypeBootstrap.cs`: one scene entry point, normal/stress creation, Input System, HUD and view sync.
- Existing Editor setup/build: `Editor/PrototypeSetup.cs` and `BuildPrototype.cs`; do not replace their prototype outputs.

Verification evidence: fresh review run on 2026-10-15 passed 47/47 EditMode tests, but PlayMode passed 7/11, failing `ClickOnHudNeverPlacesBehindIt`, `HotkeyToolAndClickPlaceThroughInputSystem`, `PausedNightBlocksBuildingButKeepsCameraAndInspection`, and `RoutePreviewMatchesPlannerAndShowsBreach`. Reports are currently `TestResults/EditMode-20261015-113526.xml` and `PlayMode-20261015-113733.xml`. The older passing reports in `plans/evidence/` do not override this result. These failures involve virtual input/hover; their cause is not established.

First reproduce the failing checks against the current Editor/input configuration. Fix a runtime cause when demonstrated. A test change requires a concrete harness defect and a short explanation in the results; never bypass input by replacing the assertion with a direct game command. Do not rewrite the suite, remove failures, or require historical test counts. If the original failure cannot be reproduced, record the environment and a fresh passing run. If the runner is unusable after two focused attempts, record a verification blocker; do not claim DONE or continue dependent plans.

## Allowed files and boundaries

In scope: existing `Assets/TheLastFortress/Runtime/Core/` and `Runtime/Unity/` files that must support the features below; new focused `EconomyRules.cs`, `MvpCampaign.cs`, and `MvpDefinitions.cs` or a small equivalent; new MVP data/prefabs/scene under the same asset root; additive Editor `MvpSetup.cs` and `BuildMvp.cs`; focused tests in existing test assemblies; project README and this plan's results/index. Unity owns new `.meta` files. Editor build-scene registration may add the MVP scene.

Keep namespaces `TheLastFortress`, ordinary C# simulation classes, and thin Unity adapters. Extend current architecture; do not build an abstract framework. No package upgrades, gameplay NavMesh, ECS, networking, external assets, hand-written scene/prefab YAML, source-control commits/pushes, or broad ProjectSettings edits. Do not overwrite prototype definition assets to tune the MVP.

## Steps

### 1. Restore a trustworthy verification baseline

Inspect the actual PlayMode failure stack traces and Input System settings/device lifecycle. Run affected cases first, then the existing suite once when the demonstrated cause is addressed. Preserve/restore any temporary Editor settings. Never run two Editors on this project, kill the user's Editor, or operate on another connected project.

**Gate:** fresh results identify the right project, contain nonzero test counts, no failed/skipped affected tests, and no compile errors. Record the cause/fix or non-reproduction evidence in `plans/003-results.md`. No additional test is needed merely to verify test infrastructure.

### 2. Add a separate campaign configuration and footprint model

Keep `RunConfig.CreateMain()` and `CreateStress()` backward compatible. Add optional campaign configuration; absence retains the single-wave prototype. Campaign plus stress configuration is invalid. Use a separate MVP definitions asset and `Assets/TheLastFortress/Scenes/Mvp.unity`. The MVP bootstrap may reuse existing adapters; do not duplicate the whole game.

Append House/Farm/SpikeTrap building IDs, preserving existing 0/1/2. Every instance has stable ID, anchor, actual purchase price, HP where applicable, and orientation metadata initialized to zero (used in 005). Define footprints as offsets: all 1 x 1 except Farm offsets `(0,0),(1,0),(0,1),(1,1)` from the lowest `(x,z)` anchor.

Placement checks every footprint cell before changing gold or occupancy. Install/release all cells once, with one revision change per solid building transaction. Each cell resolves to the owning building. Enumerate each entity once for income, targeting, views, selling, and destruction. Preview overlays include the complete footprint without changing live state. Add a separate walkable trap layer; trap placement/removal must not invalidate solid-route caches unnecessarily. Reserved ring and impassable terrain reject every affected footprint cell.

**Gate:** focused EditMode checks cover one legal Farm, one partially invalid/unaffordable Farm without mutation, owning-entity lookup/release across four cells, and preview/actual route agreement. Existing one-cell routing/placement tests remain meaningful.

### 3. Add construction, adjacency, income, and repairs

MVP values: start 250 gold; Keep 500 HP and +20 daily income from Day 2; Wall 20/100; Tower 70/100 with existing attack stats; House cost 40, HP 80, +15/day; Farm cost 60, HP 100, +25/day; Trap cost 35, no HP. Income never occurs on Day 1 or after the final night. No normal kill rewards.

Farm income is +5 once when any unique surviving adjacent House shares a footprint edge. Corners do not count; two shared edges or multiple Houses do not multiply the bonus. The HUD forecast and actual payment call the same calculation. A sold/destroyed economic building cannot earn income. Preserve actual price for refund; sale pays floor(price/2) once.

Repair during Day only, including Keep: restore `min(missingHP, gold * 5)` and charge `ceil(restoredHP / 5)`. No-op at full health/zero gold; no Trap repair; show exact quote. Construction, sale, and repairs remain forbidden at Night, including paused Night. Gold never goes negative. Repair/sale/income updates are atomic and evented through the existing balance owner.

**Gate:** use a small rules fixture with Farm + House and more than one neighboring House, destroyed infrastructure before income, a duplicate transition request, and an odd missing-HP repair. Test rounding with a genuinely discriminating value rather than even costs alone.

### 4. Add walkable traps and Wall reflection synergy

Trap deals 8 damage once when an enemy completes entry into its cell; staying does not tick damage. It does not block navigation or share a cell with a solid structure/another Trap. Spawn-on-Trap behavior is irrelevant on protected entry cells. Normal movement never deliberately cycles to trigger traps.

A Wall adjacent to at least one Trap reflects 3 damage per received melee hit, once regardless of Trap count. Reflection resolves immediately after that hit, even if the hit destroys the Wall, cannot recurse, and can kill the attacker before another action. Destroyed enemies cannot attack in the later enemy-attack phase. Record kill/destruction once, even when several damage sources share a step. Keep this ordering compatible with 004's burn phase.

**Gate:** one focused combat fixture verifies entry/stationary behavior and one verifies non-stacking adjacency plus the lethal-hit reflection case. Extend related scenarios instead of duplicating full runs for each detail.

### 5. Make transitions preserve the fortress between nights

Temporary foundation profile: three nights, Grunts only. Night 1: ten from West every 1.5 s; Night 2: sixteen split West/East round-robin every 1.5 s; Night 3: twenty-two split West/East every 1.5 s. Start at zero, preserve authored ordering. Label this profile `Foundation — 3 nights` in the HUD; 004 replaces the normal MVP profile with all six actual waves. Keep the foundation profile available only as a development fixture afterward if needed by a focused test.

After clearing Nights 1/2, enter Day 2/3 directly, retain gold/HP/buildings, pay income once, reset per-wave spawns/cooldowns/enemies, and await a manual start. No perk panel in this intermediate stage. Clearing Night 3 wins. Keep destruction immediately defeats. Guard transitions with night/payment IDs so repeated callbacks do not duplicate income. Instance IDs stay unique across nights. Restart/New Run resets the entire campaign, not just the wave; never grant income for replaying a transition.

**Gate:** a bounded simulation demonstrates Day 1 → Night 1 → Day 2 with preserved damage/placements and exactly one income payment, then final victory/defeat and fresh restart. One focused scene smoke verifies HUD night/income and manual next-wave start. No artificial invulnerability or free gold in normal mode.

### 6. Connect UI, content, and a Windows build

Add cards/hotkeys 1 Wall, 2 Tower, 3 House, 4 Farm, 5 Trap, selection HP where applicable, Day-only Sell/Repair, income forecast, foundation progress, exact upcoming directions/counts, and footprint ghost/highlight. Connect new prefab references explicitly. Primitive visuals are sufficient until 005. Add edge-based Farm/House and Wall/Trap highlighting without a general synergy-editor framework.

Add `TheLastFortress.Editor.BuildMvp.BuildWindows`, menu `The Last Fortress/Build MVP`, output `Builds/Mvp/TheLastFortress.exe`, non-development Windows x64 using only `Mvp.unity`. Keep the original build command/output intact. Setup must be idempotent, preserve tuning assets, and stop on unsaved scenes instead of silently overwriting them.

**Gate:** Editor compile, relevant suites, standalone foundation launch, a real-budget multi-night fixture, visible Farm footprint, affordability/repair UI, and restart. Record build output/result. Full performance capture is deferred to 005 unless a measured regression justifies one earlier.

## Commands and focused verification

Use the live Editor Test Runner/Unity MCP when this project is open, pinned to its exact project. Otherwise use the existing project README's batch interface, sequentially:

```powershell
$fortressEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
$fortressProject = (Resolve-Path 'game/The Last Fortress').Path
$fortressResults = Join-Path $fortressProject 'TestResults'
New-Item -ItemType Directory -Force -Path $fortressResults | Out-Null
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform EditMode -testResults "$fortressResults/003-EditMode.xml" -logFile "$fortressResults/003-EditMode.log"
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform PlayMode -testResults "$fortressResults/003-PlayMode.xml" -logFile "$fortressResults/003-PlayMode.log"
& $fortressEditor -batchmode -nographics -quit -projectPath $fortressProject -executeMethod TheLastFortress.Editor.BuildMvp.BuildWindows -logFile "$fortressResults/003-Build.log"
```

Run one command at a time and wait for the process/result; PowerShell may return early for a GUI executable. In that case use a correctly quoted `Start-Process -WindowStyle Hidden -Wait -PassThru`. Never add `-quit` to a test run. The existing live-Editor export is known to work; these batch invocations were not exercised during this planning task. New MVP build method is implemented in step 6, not assumed present now. Fresh XML must show Passed, nonzero total, zero failures, and no unexpected skips; process exit alone is insufficient. Preserve earlier evidence, use new filenames/timestamps, and do not commit generated Library/TestResults/Builds.

## Bounded tuning and completion

Do not retune original prototype assets/defaults. For MVP-only viability, allow up to three documented tuning passes per plan, with any changed value staying within ±20% of the initial specification (total deviation, not compounded per pass). Eligible fields: purchasable building costs, House/Farm outputs, enemy HP, Tower direct damage, and non-boss wave counts (positive integer). Starting gold 250, Keep base HP 500, Keep income 20, footprints, combat ordering, refund ratios, boss count/timing, and open-route-first routing remain fixed. Record old/new values, rationale, and identical fixture before/after; do not adjust multiple unrelated levers without evidence. Exhausting the bound means report a balance blocker; do not cheat normal runs.

Done when the preflight input failures are explained/resolved, multi-cell placement and economy behave as specified, three-night foundation is usable without hidden grants, prototype/stress modes still work, meaningful focused coverage passes, and a standalone build launches. Save evidence/remaining risks in `plans/003-results.md` and update the index. Then continue 004.

Stop dependent work for unresolved compile/data-corruption/core-routing issues, an unusable verification environment after two focused fixes, or missing architectural preconditions. Cosmetic issues can be recorded for 005. Fresh-player testing is still pending; it must not be invented or treated as completed by simulation.
