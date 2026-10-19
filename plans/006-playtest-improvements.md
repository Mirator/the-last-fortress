# Plan 006: Make fortress decisions and combat feedback clear

> Executor: read this whole plan and root `AGENTS.md` before changing anything. Implement the required stages in order, verify the affected behavior, and record progress in `plans/006-results.md`. Conditional experiments are separate from required fixes. Do not mark visual/player acceptance passed using source inspection or headless simulation.

## Status and baseline

- Status: TODO. This document plans work; nothing was implemented or tested by the planning task.
- Priority: P1 correctness and progress protection, followed by P2 presentation/investigation; conditional P3 polish last.
- Effort: L across several focused stages. Risk: medium, concentrated in event timing, save-and-exit and recap accounting.
- Planned at commit `cea7c4f`, 2026-10-15. Depends on implemented plans 003–005.
- Specification: [004-agent-playtest-improvements.md](../specs/archive/004-agent-playtest-improvements.md). This plan covers every required section and explicitly defers its conditional items.
- Root: `C:/Users/Miroslav Pavelek/Documents/Games/the-last-fortress`; Unity project: `game/The Last Fortress`.
- Stack: Unity 6000.3.16f1, URP 17.3, Input System 1.19, uGUI with legacy `UnityEngine.UI.Text`, plain C# simulation at a 0.05-second step. Preserve these conventions.
- Existing Windows build: `game/The Last Fortress/Builds/Mvp/TheLastFortress.exe`. Normal launch opens the menu.

First run these read-only checks from the repository root:

```powershell
git status --short
git diff --stat cea7c4f..HEAD -- 'game/The Last Fortress/Assets/TheLastFortress' 'specs/archive/004-agent-playtest-improvements.md' plans
git diff --stat -- 'game/The Last Fortress/Assets/TheLastFortress'
```

The specification and agent evidence may still be untracked. Preserve them and any unrelated work. Compare the current symbols with the excerpts below before editing; if requirements or contracts changed, reconcile the plan and record the change before proceeding. An unrelated commit alone is not a blocker.

## Operating constraints

The owner requested avoiding desktop use. Do not activate/control a game window, the live Editor, mouse or keyboard, or run screenshot-capture gameplay on this machine under the current instruction. Background terminal/Core simulations are allowed. Unity batch commands below are allowed only when the project is closed and they can run without interactive windows. If it is open, complete independent source/Core work and mark dependent Unity verification pending; do not start a second Editor or drive the existing one. Rendered validation needs a suitable separate environment or a later explicit change to this constraint.

Do not commit, push, publish, install packages, contact people or download assets. Remain in the existing checkout unless isolation is needed; any newly requested branch uses `codex/`. Do not touch real saves at `%USERPROFILE%/AppData/LocalLow/DefaultCompany/The Last Fortress/Mvp/`. Tests use a temporary folder, captures use `-saves <isolated folder>`, and storage fault injection must never target user data.

## Evidence and current implementation

The two simulation agents ran Core drivers under `plans/evidence/006-playtest-a/` and `006-playtest-b/`. Economy-first won generated seeds 1–3; fixed defense-first won 1–2 and lost 3 on Night 5. The three later reviews live in `plans/evidence/007-{immersion,graphics-motion,ui}/report.md`. Those reviews used source and historical screenshots, not new gameplay. Previously saved EditMode 89/89 and PlayMode 13/13 results are historical, not a fresh baseline for this plan.

All source paths below are relative to `game/The Last Fortress/Assets/TheLastFortress/`.

| File/symbol | Current responsibility and relevant issue |
|---|---|
| `Runtime/Core/RunSimulation.cs` | Rules, effective Tower stats, damage, phases, reward transactions and run counters. `Step` attacks then removes dead enemies before presentation reads events. |
| `Runtime/Core/EconomyRules.cs` | Farm/House adjacency, per-Farm rounding, repairs and total income. Share this calculation rather than copy its formula into the HUD. |
| `Runtime/Core/SimEvent.cs` | Small immutable event struct with type, tick, IDs and cell. Current TowerShot has no recorded impact position. |
| `Runtime/Core/RunCheckpoint.cs` | Schema-v1 Day-only checkpoints; validates and constructs a fresh simulation on restore. Reward replay starts from the prior Day. |
| `Runtime/Unity/HudView.cs`, `HudView.Mvp.cs` | Code-built uGUI, selection, build cards, reward, menu, summary and entry markers. |
| `Runtime/Unity/PrototypeBootstrap.cs` | Simulation/view synchronization and user-intent methods. Restart directly resets; Quit game is currently wired to `Application.Quit`. |
| `Runtime/Unity/RunPersistence.cs`, `SaveStore.cs` | Sole durable save owner, pending snapshot/retry, atomic file replacement and separate slots. Reuse this path. |
| `Runtime/Unity/WorldView.cs` | Diffs entity views, drains events, draws arrows and building reactions; no enemy gait; burn uses unscaled wall time. |
| `Runtime/Unity/PlayerController.cs`, `CameraRig.cs`, `RouteOverlay.cs` | Input/UI-raycast exclusion, camera and effective range/route overlays. Keep fixed camera orientation and navigation rules. |

Load-bearing excerpts read at planning time:

```csharp
// HudView.Refresh, selection: base definitions rather than effective values
if (sel.Kind == BuildingKind.Tower)
    body += $"\nDamage {sel.Definition.AttackDamage} every {sel.Definition.AttackIntervalSeconds}s, range {sel.Definition.Range}";

// HudView.RefreshReward: forecast before choosing, but unconditional payment wording
Set(rewardHeader, $"Night {offer.ClearedNight} survived — choose one reward\nDawn of Day {offer.ClearedNight + 1} pays +{sim.IncomeForecast().Total} gold after your choice");

// RunSimulation.Step: cleanup happens before view synchronization
TowerAttacks();
BurnTicks();
EnemyAttacks();
RemoveDeadEnemies();

// WorldView.SpawnArrow: lethal targets have already been removed
var tower = sim.GetBuilding(towerId);
var enemy = sim.GetEnemy(enemyId);
if (tower == null || enemy == null) return;

// RunPersistence: existing serialized pending-write contract
public bool SavePending => Pending != null;
// CommitDay captures once, Write retains that snapshot on failure; Retry writes it again.
```

Follow the existing convention of authoritative pure Core rules and thin Unity adapters. `SimEvent` notifications must not become a second simulation. `MvpSceneTests.SetUp` already creates an isolated save directory and virtual input devices; use its scene lifecycle and bounded stepping patterns. Storage tests in `MvpReplayAndSaveTests` inject a failed write using a temporary path and assert the durable file survives; reuse that pattern.

## Scope

Allowed production changes are the listed Core/Unity files where required, a small focused night-recap record or presentation helper under those same runtime folders, necessary MVP content/prefab adjustments via Editor APIs, and the existing test files named below. Add `.meta` files for new Unity assets. Extend `CaptureSequence.cs` only for a needed deterministic acceptance capture, not a new capture framework. Documentation/evidence: project README, `plans/006-results.md`, `plans/evidence/006-improvements/`, `plans/screenshots/006-improvements/`, and this plan/index for status.

Preserve the specification, original prototype tuning/assets, six buildings, four enemy types, six authored nights, fixed-step ordering, routing, reserved ring, traps, refunds and reward ledger. No new systems, audio dependency, UI framework migration, generic event bus, scene regeneration, test framework or source-generated mock layer. Do not hand-edit Unity scene/prefab YAML. Do not change packages, project quality/settings, save schema or rule version as a shortcut. Balance is investigation-only until the evidence gate in stage 7 is met.

## Verification interface

These are future execution commands; do not run them for documentation alone. Use repository-root PowerShell and quoted paths. First inspect whether the project is already open using a process inventory; do not activate it:

```powershell
Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Select-Object ProcessId,CommandLine
```

For a closed project, set these once:

```powershell
$fortressEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
$fortressProject = (Resolve-Path 'game/The Last Fortress').Path
$fortressResults = Join-Path $fortressProject 'TestResults'
New-Item -ItemType Directory -Force -Path $fortressResults | Out-Null
```

Affected test command, substituting one exact class filter from the stage's gate:

```powershell
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform EditMode -testFilter 'TheLastFortress.Tests.RewardAndPerkTests' -testResults "$fortressResults/006-affected-edit.xml" -logFile "$fortressResults/006-affected-edit.log"
```

For the scene gate use `-testPlatform PlayMode -testFilter 'TheLastFortress.Tests.MvpSceneTests'` with distinct `006-affected-play.xml/.log` paths. Test filenames do not always match their fixture names. Use these verified filters, replacing the example filter above:

| Behavior/file | Exact fixture filter |
|---|---|
| Perks, income, burn in `MvpCombatAndPerkTests.cs` | `TheLastFortress.Tests.RewardAndPerkTests` |
| Movement in the same file | `TheLastFortress.Tests.EnemyMovementTests` |
| Wave/terminal timing in the same file | `TheLastFortress.Tests.SixNightWaveTests` |
| Checkpoint/storage in `MvpReplayAndSaveTests.cs` | `TheLastFortress.Tests.CheckpointTests` |
| Combat/event ordering in `CombatPhaseStressTests.cs` | `TheLastFortress.Tests.CombatTests` |
| Phase/pause in the same file | `TheLastFortress.Tests.PhaseTests` |
| Recorded policies in `MvpPolicyTests.cs` | `TheLastFortress.Tests.MvpPolicyTests` |

Stages below name existing files for where to extend tests; run the applicable fixture filters from this table, not a guessed filename-derived filter. Do not force an unrelated full-run test to cover view events.

Do not pass `-quit` with `-runTests`. Wait for each process to complete before another invocation; if the shell returns early, use `Start-Process -WindowStyle Hidden -Wait -PassThru` with correctly quoted arguments. Every gate requires fresh nonzero NUnit XML with `result="Passed"`, zero failures/unexpected skips, and no compile errors, not merely exit code zero. When checking failures, preserve the XML/log before another invocation can overwrite it.

Background Core evidence commands already supported by existing drivers:

```powershell
dotnet run --project 'plans/evidence/006-playtest-a/scenario/Scenario.csproj' -- 1
dotnet run --project 'plans/evidence/006-playtest-a/scenario/Scenario.csproj' -- 2
dotnet run --project 'plans/evidence/006-playtest-b/harness/PolicyRunner.csproj' -- economy 1 generated perks
dotnet run --project 'plans/evidence/006-playtest-b/harness/PolicyRunner.csproj' -- defense 1 generated perks
```

They compile live Core sources, not Unity adapters or serialized tuning. Their success is a useful Core check but does not replace NUnit, a Unity compile, file persistence integration or rendered acceptance. Keep generated `bin/obj` ignored and old transcripts intact.

## Stage 1 — Use authoritative effective stats and preview reward income

1. In `EconomyRules`, extract/reuse a per-building income calculation that total `DailyIncome` also calls. Keep per-Farm rounding and one House bonus per Farm. Permit a read-only prospective Efficient Farming calculation using the same function; do not clone and mutate the live simulation, install a perk, draw rewards or pay gold for a preview.
2. In `HudView.Refresh`, selected Tower details use `sim.TowerDamage(sel)` and `sim.TowerRange(sel)`. Keep direct damage, burn and Last Stand status distinct; live Last Stand text updates when Keep HP crosses its exact threshold. Selected Farm income uses its effective per-building result. Build cards use effective known values or explicit base labels for placement-dependent income. Verify range-overlay math still uses the effective range.
3. In `RefreshReward`, change unconditional payment wording to current forecast wording and add the prospective payment to the Efficient Farming card. Repeated drawing must leave RNG, gold, buildings, owned perks and offer identity unchanged.
4. Reflow the existing selection and reward fields as needed; do not postpone obvious text/action collisions caused by the new information to the polish stage.

**Gate:** affected `MvpCombatAndPerkTests` then `MvpSceneTests`. Extend `EfficientFarmingForecastEqualsPayment` only for a missing shared-calculation/preview invariant, not existing formula coverage. Extend the existing scene reward/selection scenario to compare displayed values to effective APIs. Use an offered Efficient Farming case, not an unavailable forced choice. Record 10→12 damage and the 45→52 or 65→74 income discrepancy resolved; check the exact Last Stand boundary with existing coverage. A compiled headless view test can verify values, not typography.

## Stage 2 — Preserve shot locations and unify combat presentation timing

1. Extend `SimEvent` additively with only the immutable coordinates needed for shot/impact presentation, using plain C# fields rather than Unity vector types. Capture source and target positions when `TowerShot` is emitted; constructors used by unrelated events remain source-compatible. Target death or Tower destruction later in the same frame must not invalidate an already-emitted shot. Never use a default event cell as a fallback hit position.
2. Change `WorldView` to use captured positions. Create one small bounded pooled cue path for lethal/nonlethal impacts and deaths; event coordinates remain usable if no entity view exists (including several fixed steps before sync). Preserve immediate authoritative damage and cleanup; cosmetic travel is not damage timing. Clear/recycle active and pooled objects correctly on reset/load/scene teardown. Do not duplicate a death response when shot and kill events describe the same outcome.
3. Pass an explicit presentation delta/elapsed time from the bootstrap. Menu suspension and explicit Night pause freeze arrows, burn pulse, gait and hit/death timers. Remove combat animation dependence on `Time.unscaledTime`. Keep HUD refresh and explicit-pause camera inspection available. Let the final kill's short effects finish after entering Reward/Victory when no menu/pause is suspending them; a clock restricted only to `State == Night` would strand those effects.

**Gate:** run the nearest affected EditMode combat fixture and `MvpSceneTests`. Add/extend a focused scene case that steps to a lethal hit before synchronizing views and observes the cue at the recorded location, then tests pause/resume/menu suspension. Include the last kill transition and a nonlethal shot in the same small fixture where practical. Bound all loops; assert meaningful visible transform/state changes rather than private pool implementation. Record Unity behavior checks separately from still-pending rendered feel/readability.

## Stage 3 — Protect progressed runs and unsaved Day edits

1. Route the top Restart and in-run Quit game through request/confirm/cancel methods in the bootstrap and uGUI. Keep the confirmed reset/quit actions separate from request methods. All relevant callbacks/hotkeys go through the same guard; guard dialogs block gameplay input and underlying UI actions.
2. A progressed active run requires an explicit `Retry same seed — start Day 1` explanation. Include Night/Reward and later Days, plus Day-1 construction/repair/sale/rotation changes. Terminal retry stays immediate. A truly untouched initial Day 1 can remain a fast path.
3. Determine unsaved Day edits against `Persistence.LastDay` using fields that represent saved player progress (building records/HP/facing/price, gold, relevant run/perk state). Evaluate on action request, not by serializing checkpoints every frame. Ignore incidental view state, grid revision and synthetic offer/run identifiers. Successful commits and Continue reset the durable comparison baseline; failures do not.
4. Dirty-Day Quit offers save-and-exit, exit without those edits, and Cancel. Save-and-exit calls the existing `CommitDay`, and quits only after that exact snapshot succeeds. Do not use the current `SaveAndQuit()` method unmodified: it intentionally detaches and enters the menu rather than exiting the application. Preserve pending snapshot ownership and the failure overlay; a later Retry must resume the intended exit only when the requested save succeeds, without repeating a perk/income/reset. Cancel must leave the active run and old durable checkpoint untouched.
5. Night/Reward exit text describes replay from the prior Day and frozen reward offer; do not offer a mid-night save. Guard in-app commands in this scope; operating-system shutdown/crash interception is not required.

**Gate:** `MvpReplayAndSaveTests` and `MvpSceneTests`, affected first. Extend `MenuSeededRunCheckpointContinueOutcomeRetryAndSettings` for cancel/confirmed reset and clean/dirty boundaries. Reuse temporary-storage failure injection to prove failed save-and-exit retains the old durable file and live run; never call a real application quit in a test. Use the smallest production exit boundary needed to verify quit intent without adding a generic lifecycle framework. Check normal Save and Quit to menu remains distinct from Save and exit game. Older slots surviving restart do not make them selectable; do not claim recovery UI exists.

## Stage 4 — Add accurate night-local consequences

1. Add a small Core night-recap record with night number, enemies defeated, applied Keep damage and combat-destroyed non-Keep buildings. Reset at a successful `StartNight`, before its time-zero spawn. Update counts in authoritative kill/damage/destruction paths, not by parsing drained presentation events. Count actual HP removed, capped at pre-hit HP, excluding invulnerability and overkill. Sales/scripted removal/reset do not count as combat destruction.
2. Freeze the just-finished values on Reward/Defeat/Victory before perks or Day repair. Show a short recap on existing Reward and Defeat surfaces; leave run totals visibly distinct and preserve existing Victory summary. Do not add an intervening modal/score/reward currency.
3. Keep this recap transient to the replayed night. There are no mid-combat saves and Continue replays the night from a Day checkpoint, so resetting at StartNight prevents double-counting without a schema bump. A restored Day need not display a historical recap; do not promise a persistent recap browser. If a new requirement actually needs historical persisted recaps, stop that portion and design compatible storage separately.

**Gate:** affected `MvpCombatAndPerkTests` plus `MvpReplayAndSaveTests`, then one relevant scene assertion. A compact case/table covers clean night, building loss, capped lethal Keep damage and reset/next-night behavior. Extend the existing Day checkpoint replay case to compare the recap after uninterrupted/resumed combat; do not add a second full campaign. Demonstrate that later health perks/repairs cannot alter the frozen damage figure. Preserve schema v1 and old-save load behavior.

## Stage 5 — Add restrained contact and locomotion feedback

1. Build on stage 2's pooled cues for successful hits and destroyed structures. Add a distinct brief HUD Keep-hit cue so damage remains noticeable while the Keep is off-screen or camera shake is disabled. Use actual damage events, not failed/invulnerable attacks; target removal must not erase a destruction cue.
2. Animate a visual child of each enemy model, keeping the simulation/root position, health-bar anchor and route footprint stable. Use movement progress to drive restrained gait, stop it when stationary, and use real attack events for contact motion. Runner motion is quicker; Brute/Golem motion heavier. Reset child pose on view reuse/reset and freeze it with the stage-2 clock. No path changes, extra colliders, per-frame material instantiation or gameplay RNG consumption.
3. Keep contrasts and silhouettes from the current low-poly palette. Limit effect lifetime and simultaneous instances explicitly and document the cap; don't hide damaging enemies behind opaque flashes, fake rubble obstacles or constant number showers.

**Gate:** rerun the stage-2 presentation case only after these relevant changes, plus the existing Core pause/determinism checks if shared code changed. Procedural aesthetics do not need vertex/pose snapshot tests. Record a later rendered checklist for all four archetypes walking, stopping and attacking; dense Night 4/6, off-screen Keep damage, shake disabled, pause/menu/retry cleanup, both resolutions. Do not claim these observations from historical screenshots. Reuse historical performance as context only; profile only if changed effects expose a concrete cost concern, not an automatic new 60-second capture quota.

## Stage 6 — Bound tooltip and selection layout; retain owner fixes

1. Clamp/flip tooltip placement using canvas-local coordinates, actual RectTransform size/pivot and a small screen margin after layout. Account for CanvasScaler and camera pan/zoom; keep validity reason, full 2×2 footprint, price and rotation text visible. Preserve world/UI raycast exclusion.
2. Make selection body height and action positions accommodate the longest effective Tower, linked Farm and reflecting Wall details, with repair quote/refund kept above distinct action hit targets. Retain the compact current income forecast and received-at-dawn line; do not restore the superseded economy-source paragraph.
3. Check invalid placement recovery without changing already-correct rules. Do not add routine sell confirmations or a tutorial just because the prior screenshots lack them.

**Gate:** `MvpSceneTests` after relevant changes. A small parameterized bounds check is justified for the edge-clipping fix if the existing fixture can inspect the laid-out RectTransform; don't create golden-image tests. Later inspect valid/invalid ghosts at all screen edges and long selection/action cases at 1280×720 and 1920×1080. If fresh-player evidence shows control-discovery failures, add compact dismissible help in a separate conditional step. Lighting blends are likewise conditional on rendered evidence of a distracting cut. Both may remain deferred with a reason; no permanent floating hint or new UI framework.

## Stage 7 — Investigate an adaptive defensive opening, without automatic tuning

1. Preserve the old fixed policy and transcripts. Add a small independent evidence driver under `plans/evidence/006-improvements/` based on the existing Core harness. Record its policy before comparing outcomes: defense-led opening, a stable search order for legal fallback cells, affordable purchase alternatives, a deterministic repair rule and a fixed preference over naturally offered perks. Do not search layouts until a winning run appears and present that as a fresh-player result.
2. Compare it with economy-first and the old defense-first on generated seeds 1–3. Log seed, terrain attempt, actual prices/perks, skipped/relocated purchases, repairs, surviving defenses, gold, per-night Keep HP and outcomes. Keep normal reward runs distinct from diagnostic no-perk runs. Recheck defaults against the serialized MVP asset; a Core-default driver does not test changed serialized tuning automatically.
3. Evaluate defense-led with delayed income first; pure defense on guaranteed Keep income is an explicitly labeled diagnostic, not a promise that every opening must win. If the evidence leaves that product goal unresolved, record the decision needed rather than forcing equal results.
4. Deliver the comparison and a recommendation. Do not patch balance as part of this stage automatically. If a later task authorizes a supported tuning pass, retain plan 004's bounds: at most three documented passes, each value within ±20% total of spec 003, and only building costs, House/Farm outputs, enemy HP, Tower direct damage and positive non-boss counts. Fixed starting gold 250, Keep HP/income, routing, footprints, refund ratios, boss count/time and perk identities are unchanged. Any actual tuning requires a separate save-compatibility decision: current rules version is `mvp-rules-1`, and old saves must not silently adopt incompatible tuning.

**Gate:** the new driver exits nonzero on invalid actions that it claimed succeeded or on unresolved nights, with at most 40000 steps per night. Outcomes themselves are evidence, not failing tests that should be changed until green. Run existing `MvpPolicyTests` only if simulation/tuning changed, preserving its authored-flat-map scenarios and assertions. Report mechanical viability separately from human strategy understanding and enjoyment. No new permanent test for every seed/policy.

## Stage 8 — Integrate, build when possible, and report honest acceptance

1. Once required implementation is integrated, run EditMode and PlayMode suites once without `-testFilter`, using distinct `006-final-edit.xml` and `006-final-play.xml` plus logs. Do not repeat earlier passing runs unless changes/failures justify them. If the current desktop constraint or an open Editor prevents these commands, record exactly what remains pending and preserve the last known playable build.
2. After Unity compilation/tests pass and the project is closed, build once with the existing additive method:

```powershell
& $fortressEditor -batchmode -nographics -quit -projectPath $fortressProject -executeMethod TheLastFortress.Editor.BuildMvp.BuildWindows -logFile "$fortressResults/006-build.log"
```

Require a successful fresh BuildReport and zero errors at `Builds/Mvp/`; do not launch it on this desktop. Keep the original prototype output separate. Restore only demonstrably incidental settings drift caused by this work, never user changes.

3. In an allowed rendered environment later, use the MVP executable with `-saves <isolated directory> -screen-width 1280 -screen-height 720 -screen-fullscreen 0` (repeat at 1920×1080). Capture stat/reward comparison, edge tooltip/long selection, lethal/nonlethal/last kill, Night 4/6, gait/contact, pause/menu and retry/exit guards. Static captures cover layout; use brief motion evidence or direct observation for gait/pause/flicker, not a screenshot claim. Avoid logging or opening personal saves.
4. Write `plans/006-results.md` with per-stage status, changed files, fresh command/XML/build evidence, comparison results and pending rendered/player gates. Store new evidence under the 006-improvements folders without overwriting earlier agent transcripts. Update the plan index and project README only for changed controls/behavior. Keep conditional help/lighting and human acceptance explicitly separate.

## Test scope and done criteria

Read nearby tests before editing. Prefer extending `MvpCombatAndPerkTests`, `MvpReplayAndSaveTests`, and `MvpSceneTests` at the existing behavioral boundary. Add only the missing lethal-event/pause, guard-save-failure, preview invariance and recap-accounting protection described above. Do not write tests for document contents, every cosmetic pose or every provisional number, weaken assertions, or bless snapshots to get green results.

- [ ] Stages 1–6 implemented against the current spec, with all changed-risk gates passed or explicitly pending because the execution environment prevents them.
- [ ] No preview mutation, changed combat timing, lost lethal-hit information, duplicate recap transaction or unsafe exit after failed save.
- [ ] Save schema/rules and user files preserved; transient recap rebuilds through replay.
- [ ] Stage-7 comparison reproducible with exact logged policy, with no automatic tuning and no human-fun claim.
- [ ] Fresh Unity suites and MVP BuildReport pass before declaring technical verification complete; headless drivers alone cannot satisfy this box.
- [ ] Rendered acceptance checklist and five-fresh-player gate separately marked passed/pending with actual evidence.
- [ ] `git diff --check` passes and `git status --short` shows no unexplained changes outside scope.
- [ ] Results/index updated; no commit, push, publication or desktop interaction without separate authorization.

Use status `IMPLEMENTED — VERIFICATION PENDING` in the results if code is complete but required Unity/render checks cannot run; do not call the whole plan DONE. Completing technical work is distinct from commercial/player acceptance. The pending conditional help/lighting experiments are not failures of an otherwise verified required implementation.

## Stop conditions and maintenance

Stop only dependent work and report if a failure persists after two focused fixes, a proposed change threatens user saves/schema compatibility, the old navigation/occupancy contract regresses, or success requires new packages, broader mechanics or desktop interaction prohibited above. Continue independent permitted stages where useful. Preserve last playable artifacts and diagnostics; do not rewrite tests or grant debug resources to hide failures.

Review future changes to events together with their consumers: event source/target IDs differ for destruction versus attacks, and recorded coordinates must survive same-step entity removal. Review preview calculations alongside actual economy payments, and new combat effects alongside the shared pause/menu clock. Recap counters are deliberately per-night and transient. Exit guards must follow the existing pending-write owner and cannot assume a completed call means a durable save. Revisit these boundaries explicitly if later milestones add mid-combat persistence, upgrades or asynchronous saving.
