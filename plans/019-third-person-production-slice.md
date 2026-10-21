# Plan 019: deliver a coherent third-person movement and melee slice

Status: **TECHNICAL COMPLETE; HUMAN ACCEPTANCE PENDING** (executed 2026-10-20; see [019-results.md](019-results.md); the spec was merged into `specs/002-game-rules.md` v1.7 §18 and archived as `specs/archive/007-third-person-production-slice.md`). Original planning status: TODO. Priority P1; effort L (several weeks of engineering, animation and iteration, not a one-pass polish fix); risk HIGH for new rules, MED for presentation. Planned at `adac6b4`, 2026-10-20.

Implements **Third-person production slice v0.1**, currently `plans/018-third-person-spec-proposal.md`, to be registered as `specs/003-third-person-production-slice.md` before implementation. Baseline current rules: `specs/002-game-rules.md` v1.5. Audit: `plans/017-third-person-audit.md`. No dependency on repeating historical plans 012–016.

Owner decisions: movement/combat redesign allowed; camera pulls in first and lifts only if needed. The spec supplies concrete proposed mechanics and bounded tuning. Read it fully: this plan does not replace its rules. Execution authorization must come from the user asking to implement; the audit/planning request itself did not authorize production edits.

## Intent and drift check

Deliver grounded directional locomotion, physically credible melee, safe camera behaviour and coherent feedback while preserving deterministic strategic gameplay and old saves. Core remains authoritative. Do not solve animation problems by changing damage in an animation callback or letting root motion move the simulation.

Before editing:

```powershell
git status --short
git diff --stat adac6b4..HEAD -- 'game/The Last Fortress/Assets/TheLastFortress' specs plans
```

Record existing user changes. If files drifted, compare the excerpts below and refresh affected steps; unrelated changes are not a reason to stop. Stop only dependent work if a material contract no longer matches. Do not restore user changes, regenerate all assets, commit, push or publish. Use `codex/` if a branch is requested; no branch is required for planning.

## Current implementation and conventions

Project: Unity 6000.3.16f1, C#, URP 17.3, Input System 1.19. Existing tests are NUnit EditMode and UnityTest PlayMode. No Cinemachine dependency. Match namespace `TheLastFortress`, small deterministic Core helpers and view-only MonoBehaviours; use existing test fixtures instead of a new framework.

Paths in this section are relative to `game/The Last Fortress/Assets/TheLastFortress/`:

- `Runtime/Core/Hero.cs:83–100`: `MoveUnitsPerStep = 90`, `RadiusUnits = 360`, `SwingIntervalTicks = 15`, `SwingReach = 0.6`; these currently affect old hero rules and **must not be globally overwritten**. HeroCommand constructor masks heading with 15; preserve its meaning for existing callers.
- `Runtime/Core/ReleaseCampaign.cs:25–43`: `RulesVersion = RulesV3`; `HeroMoveUnits(...) => rulesVersion == RulesV3 ? 48 : HeroRules.MoveUnitsPerStep`; `BombersLightOnContact(...) => rulesVersion == RulesV3`. V4 must inherit V3 enemy speeds/HP and contact fuses explicitly.
- `Runtime/Core/RulesProfile.cs`: `TwelveNightsWalking` constructs V3 using `ReleaseCampaign.Walking(defs)`. Keep that factory V3; add a separate V4 factory so compatibility tests can name both.
- `Runtime/Core/RunSimulation.cs:1672–1692`: `MoveHeroAxis` binary-searches the largest legal movement. `Runtime/Unity/HeroPresenter.cs:257–258` instead accepts/rejects the entire predicted axis delta. Share the correct collision calculation without changing old-profile results.
- `RunSimulation.cs:1722–1735`: `SwingReadyTick = Tick + HeroRules.SwingIntervalTicks`; emits HeroAttacked, then HeroHit, then `DamageEnemy` immediately. V4 needs accepted/contact/recovery state; preserve old branch.
- `Runtime/Core/RunCheckpoint.cs:199–203`: Start Night capture saves `heroAtNight`, `heroX`, `heroZ`; moving V4 starts additionally need velocity.
- `Runtime/Unity/PrototypeBootstrap.cs:190–194`: catalog currently begins `[TwelveNightsWalking, TwelveNightsHero, TwelveNights, SixNights]`. `SyncViews` feeds HeroInput to HeroPresenter and then follows its shown root. `CursorLocked` lacks application-focus state.
- `Runtime/Unity/HeroCameraRig.cs:135–146`: ray samples in 0.05-tile increments, clamps result to minimum 0.2; transition at 123–127 blends without validating its interpolated pose.
- `Runtime/Unity/HeroPresenter.cs:198,216`: held attack selects camera-facing body; `CharacterRig.Pose` supplies only forward gait. `Art/Source~/characters.py:122–141` builds whole arms as one rigid part.
- `Runtime/Unity/WorldView.cs:249,1013–1018` and `AudioDirector.cs:387–397`: shared 0.08-s hit delay and reused sounds. Branch by profile, not global constant changes.
- `Editor/ArtSetup.cs:226–237` offers hero-only import (`RunHero`); validation currently requires CharacterRig. Keep existing enemy validation and add a distinct V4 hero requirement.
- `Tests/EditMode/HeroRulesTests.cs`: small bounded Core scenarios, old V2 fixture. `ReleaseRulesTests` includes old-save/profile restoration. `ReleasePolicyTests.ActiveHeroCommand` is a scripted policy, not player-quality evidence. `Tests/PlayMode/ReleaseSceneTests.SwitchingViewsNeverChangesTheNightAndTheHeroMovesWithInput` uses injected devices and controls the presentation clock; extend it for actual input/lifecycle risks.

## Allowed scope

Under the project Assets root, modify only:

- Core: `Runtime/Core/Hero.cs`, `RunSimulation.cs`, `ReleaseCampaign.cs`, `RulesProfile.cs`, `RunCheckpoint.cs`, `SimEvent.cs`; new `HeroMotion.cs` and `HeroCombatRules.cs` if they keep deterministic algorithms local. `MvpCampaign.cs` only if profile-derived properties genuinely require it.
- Runtime Unity: `HeroPresenter.cs`, `HeroCameraRig.cs`, `PlayerController.cs`, `PrototypeBootstrap.cs`, `PrototypeContent.cs`, `HudView.Hero.cs`, `AudioDirector.cs`, `AudioContent.cs`, `WorldView.cs`; new `HeroAnimationRig.cs`, `HeroPresentationState.cs`, `HeroCameraObstacles.cs` as needed. `CharacterRig.cs` only for backward-compatible optional integration, not an enemy rewrite.
- Asset pipeline: `Editor/ArtSetup.cs`, `Editor/ReleaseContentValidation.cs`, hero-only source generation in `Art/Source~/characters.py` and `models_hero.py`; new hero-specific generation/curve files as needed; V4 hero model/prefab/animation/material outputs under `Art/`, dedicated hero clips under `Audio/`, and `Audio/Source~/synth.py`. Keep original ArtHero available for legacy presentation. Data: `Data/ReleaseContent.asset` and `Audio/AudioContent.asset` for references. Include corresponding `.meta` files.
- Tests: extend `Tests/EditMode/HeroRulesTests.cs`, `ReleaseRulesTests.cs`, `ReleasePolicyTests.cs`, `SimTestUtil.cs` only where shared by actual scenarios, and `Tests/PlayMode/ReleaseSceneTests.cs`. Add one focused camera fixture only if the existing fixture becomes unwieldy.
- Verification tools: existing capture/stress classes only when required to select and identify V4 correctly; document exact files/reason in results before changing them. Inspect their actual implementation first.
- Documentation: register the feature spec and index; on completion merge implemented rules into `specs/002-game-rules.md`, archive the feature spec and update both indexes. Evidence/results under `plans/evidence/019/` and `plans/019-results.md`.

Do not change wave recipes, enemy definitions, building/economy/perk tuning, maps, package manifests, render pipeline, project-wide settings, prototype/MVP scene content or unrelated art. If a required change falls outside scope, record the exact dependency and proposed addition before continuing dependent work. Never silently overwrite old-profile constants.

## Verification commands

These command forms are present in prior repository plans and the build entry point was inspected. They were not run for this planning task. Use them only when no Editor owns this project; otherwise use the running Editor's supported test/build tools. Do not launch a second Editor against the same project. Read relevant Unity skills before driving the Editor/CLI; use the appropriate UI framework skill before UI edits.

```powershell
$fortressEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe'
$fortressProject = Join-Path (Get-Location) 'game/The Last Fortress'
$fortressEvidence = Join-Path (Get-Location) 'plans/evidence/019'
New-Item -ItemType Directory -Force -Path $fortressEvidence | Out-Null

# G1: focused Core/profile/save tests (filter separator is Unity's semicolon).
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform EditMode -testFilter 'TheLastFortress.Tests.HeroRulesTests;TheLastFortress.Tests.ReleaseRulesTests' -testResults "$fortressEvidence/Core.xml" -logFile "$fortressEvidence/Core.log"

# G2: actual scene/input/camera integration.
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform PlayMode -testFilter TheLastFortress.Tests.ReleaseSceneTests -testResults "$fortressEvidence/Scene.xml" -logFile "$fortressEvidence/Scene.log"

# G3: policy/balance evidence, only after the V4 scripted policy is adapted.
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform EditMode -testFilter TheLastFortress.Tests.ReleasePolicyTests -testResults "$fortressEvidence/Policy.xml" -logFile "$fortressEvidence/Policy.log"

# G4: complete suites once integrated, not repeatedly after cosmetic edits.
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform EditMode -testResults "$fortressEvidence/EditMode.xml" -logFile "$fortressEvidence/EditMode.log"
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform PlayMode -testResults "$fortressEvidence/PlayMode.xml" -logFile "$fortressEvidence/PlayMode.log"

# G5: release build; use Editor build path if batch rendering loses shadows.
& $fortressEditor -batchmode -quit -projectPath $fortressProject -executeMethod TheLastFortress.Editor.BuildRelease.BuildWindows -logFile "$fortressEvidence/Build.log"
```

Success: XML has failed=0 and expected named cases actually ran; report skipped cases separately. Exit status alone is insufficient. Build report must say Succeeded, errors=0 and intended rules/profile. If filters select zero tests, correct the invocation; do not report a passing gate. Launch commands sequentially and wait for completion. Inspect side effects: previous batch runs rewrote project settings; preserve user changes and revert only demonstrably generated, task-owned noise.

## Step 1 — register the contract and capture the baseline

Read AGENTS.md, specs index, current rules v1.5 and proposal v0.1. Register the proposal at the feature-spec path, with status proposed/in progress; add its row in specs index. Clarify the current §17 camera paragraph to pull-in-first as authorized, deleting only contradictory prose; do not claim new camera collision or V4 mechanics already exist. Record the clarification in the current spec version/date history and index together.

Record the existing runtime behaviour using a repeatable seed/layout and input scenario. Save source SHA, profile, machine/settings and matched Day/Night clips for movement, wall slide, close combat and camera obstruction. Reuse available tooling; capture screenshots as supplemental evidence, not a substitute for motion. If desktop/video/audio verification is unavailable, record that and continue independent Core work; retain all human quality gates.

**Verify:** `git diff -- specs plans` shows correct status and no new rules claimed implemented; inspect the feature spec for contradictions before coding. Record baseline G1 if needed to distinguish existing failures. Do not run builds/performance merely to register documentation.

## Step 2 — add profile-safe V4 motor, shared prediction and saves

Add RulesV4 and a distinct V4 factory; retain named V3 factory. Update IsRelease/HasHero and V3-derived enemy/fuse selection explicitly. Keep the public default on V3 until the new Core is playable, then make the new-run catalog/default V4 in this step's integrated change. Preserve all old restore entries and content versions.

Add a V4 command factory with explicit 64-heading encoding while the old constructor still means 16 headings; define deterministic promotion of old commands for V4 script callers. Add motor velocity to Hero; put profile tuning in one profile-selected immutable rules object, not scattered `RulesVersion ==` branches. Do not read Unity types/time/input inside Core.

Extract the existing largest-legal-axis calculation into a pure solver taking position/radius/grid and returning state. Keep old-profile results identical. V4 solver adds the specified integer acceleration/braking, speed normalization, blocked-axis velocity reset and 64-direction lookup. Prediction invokes one next tick on a copy and interpolates; it cannot mutate the live hero or grid. Remove the all-or-nothing predictor branch.

Make HeroOverlapsBlocked/spawn/build-over/restoration use the active profile radius. Extend snapshot fields for V4 only. Capture/restore validated V4 Start Night velocity with optional schema-2 fields; missing fields mean rest. Reject malformed V4 velocity beyond the allowed cap without altering the live run. Maintain legacy position/save behaviour. Starting Night resets attack state but retains V4 velocity so the checkpoint replays.

**Tests:** extend nearest hero movement scenarios for partial wall/corner movement, reverse/brake, speed bound and prediction non-mutation. Extend profile/save cases to pin literal V2/V3 expectations instead of using a changing default as the old-profile identifier. Capture a V3 command-stream result before refactoring and compare after; do not bless a changed outcome. Add a moving Day → Start Night checkpoint/replay case.

**Verify:** G1 passes, named V2/V3/V4 cases ran, same V4 command stream gives equal snapshots at different render chunk sizes, old-profile fixture unchanged. G2 after input/prediction integration. Report actual cases, not a target count.

## Step 3 — implement contact-phase combat and balance evidence

V4 attack state contains acceptance/start, committed facing, contact tick and ready tick. Add explicit accepted/contact events (or extend event payload without breaking legacy meanings). Implement wind-up/contact/recovery exactly as feature spec §4, preserving legacy Swing. Add deterministic obstruction traversal for hero melee only. Apply V4 engage/hold/hit reach only to hero interactions; do not alter enemy routing/building targeting.

Handle guard, pause, death, view switch, Night start, relocation and dawn according to the spec. Keep buffered unaccepted input in the controller, clear it across focus/menu/death boundaries, consume once when accepted. Update snapshot state. Kill/death order remains under the same Keep-priority rules.

Adapt `ReleasePolicyTests.ActiveHeroCommand` to profile-selected reach, motor braking and pending attacks; remove hardcoded old approach distance for V4. Compare V3/V4 seeds 1–8 for all three policies and active/idle/hero-only scenarios. Keep bounded loops and record positions/misses to expose a script that attacks out of reach. Use at most three recorded candidates within the spec's tuning bounds. Do not solve failures by touching waves or asserting away balance differences.

**Tests:** contact does no early damage, resolves once at due tick, uses committed facing and current positions, misses moved/dead/out-of-range enemies, cannot hit through a solid corner, and cancels on death. Extend existing replay/terminal-order cases rather than duplicating full nights per rule.

**Verify:** G1 then G3; hero-only loses by Night 3 for every required seed, policy logs compare V3/V4, average hero share and failure modes reported. Stop at the bounded-tuning limit if the mechanics fail their role; do not proceed to broad art production around an unviable mechanic. A small animation blockout used to diagnose contact is allowed.

## Step 4 — safe camera and explicit focus lifecycle

Add read-only presentation blockers sized from actual render bounds/authored simple collision proxies. They must cover visible overhangs and be kept current on building create/destroy; do not add gameplay colliders or use Core height constants as a substitute for actual camera-volume protection. Exclude dynamic units and overlays.

Solve pull-in-first, optional bounded lift, near-plane clearance, final-pose validation and transition fallback. Preserve orbit within a run, reset at run boundaries, use time-based release damping and a deliberate exit cut. Decouple camera target from animation body offsets. Keep mouse input responsive; do not smooth the look into lag.

Add explicit app-focus state: loss suspends advancement without modifying explicit pause, releases cursor, clears transient input. Return requires a consumed recapture click. Implement the same recapture policy after menu close. Camera orbit may work during explicit Night pause but not while app focus is lost. Test logical capture and actual cursor lock separately where supported.

**Verify:** G2 with focused near-plane/corner/transition and focus/recapture regressions; repeated view toggles still preserve snapshots given equal simulation steps/commands. Record live min/max FOV/zoom and stationary-corner clips; no unsafe final pose or repeated pumping. If no safe camera exists, exercise documented fallback rather than hiding clipping.

## Step 5 — finish the hero animation and contact slice

Create a separate V4 hero prefab/adapter and hero-specific generation entry point. Preserve the legacy ArtHero and shared enemy CharacterRig. Extend import/validation to require the correct V4 articulation and adapter while allowing legacy content. Prefer a hero-only generator path; a global character regeneration is out of scope.

Build the minimum animation set in spec §7. Feed signed local velocity, accepted swing phase/committed facing, actual contact and interruption state. Root motion remains disabled for simulation movement. Feet settle when blocked; starts/stops and turns blend; locomotion and upper-body aim cooperate. Wind-up/contact pose comes from Core ticks plus interpolation, not an independent timer drifting from contact. Limit cosmetic offsets and verify sword/grip/primary victim contact across the short physical reach.

Update WorldView and HeroPresenter: V4 contact triggers victim flinch, death and sparks without legacy 80 ms delay; misses have no fake hit. Legacy cues remain unchanged. Replace V4 persistent filled reach sector with restrained accepted-swing cue at actual reach; preserve owner crosshair and useful strategic warnings.

**Verify:** G2 and a small deterministic frame sequence at accepted/contact/recovery, pause, hurt, death/respawn and relocation. Live recordings must include strafe/backpedal while holding attack and a 180° aim change. Review the clips before adding animation variations. If the available generated rig cannot meet the stated articulation/contact gate, record the specific asset gap and finish a bounded hero rig iteration; do not label the old procedural envelope AA-quality.

## Step 6 — sound identity and presentation hierarchy

Add distinct hero sound families and contact-driven footfalls using the existing bounded audio system. Extend pending/active voice data only as needed to carry position and category. World sounds spatial, UI/music 2D; do not spatialize every source indiscriminately. Preserve threat priority, pause, reset and voice caps. Use project-owned generated sounds first; no purchases or asset-license assumptions.

Reduce irrelevant economy/Day controls' emphasis in Hero Night while keeping Keep/hero HP, threats, entry directions and view controls. Keep existing bindings and style; do not restyle the whole UI. No gamepad flow.

**Verify:** G2 if event/input/UI integration changed. Listen to idle, travel, blocked movement, miss, hit, hurt and crowded battle. Confirm no footfalls at rest, no duplicated impact for multi-hit spam, and no voice leaks through reset/pause. Record honest pending status if listening is unavailable.

## Step 7 — integrated verification and delivery

Run G4 after integration, G5 for a playable release. Inspect final shadows, mist, bars, crosshair and contact in the build at 720p/1080p. Record V4 on all three map profiles; perform the full motion scenario set on one maintained layout and obstruction spot checks elsewhere. Smoke-test old V2/V3 saves and MVP presentation.

Run the maintained 100-enemy stress scenario paired/alternating hero and strategy at both resolutions with Editor closed, verifying actual V4/new-hero selection and correct metadata. Extend existing tooling only if it cannot select V4. Record median/p95/p99 and hitch evidence; enforce spec frame-time gates. Do not substitute an old MVP-labelled stress report for V4 evidence.

Finish `plans/019-results.md` with stage status, exact commands, fresh XML/log paths, tuning attempts, build path, capture/video/listening evidence, performance details, owner acceptance and remaining limits. Merge only implemented rules into current rules, update version/status/index, archive the feature spec and update plan status together. If technical work passes but owner motion/listening review is unavailable, status is **TECHNICAL COMPLETE; HUMAN ACCEPTANCE PENDING**, not AA approved.

**Verify:** `git diff --check` clean; `git status --short` changes all within scope or explicitly justified; full XML failed=0; build Succeeded/errors=0; results contain every required gate or an explicit pending/failure. No claims based solely on saved audit results.

## Stop conditions and maintenance

- Do not reinterpret a material spec contradiction or failed physical-contact/balance gate inside code. Propose a versioned amendment and stop only dependent work.
- Preserve legacy outcomes; any V2/V3 snapshot drift is a regression, not acceptable V4 tuning.
- If a meaningful check fails twice after focused repair, record cause and remaining work before expanding scope. Never weaken assertions or replace acceptance images merely to pass.
- Adding a dependency, buying assets, changing waves/towers or introducing a new gameplay system requires a separate explicit instruction.
- Keep profile tuning and attack/presentation phase ownership centralized. Future animation changes must continue to consume authoritative phase/contact; future save changes must preserve moving Start Night replay. Camera proxies must track their owning visual assets through regeneration.

## Execution prompt

Use `plans/020-third-person-implementation-prompt.md` as the self-contained task prompt once implementation is desired. This plan is a planning deliverable, not a dispatched implementation.
