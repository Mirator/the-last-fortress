# 007 — From a functional MVP to a complete game

Player-experience specification and implementation plan. Prepared on 2026-10-16 against commit `dc8b55a`. Status: **TODO — planning only; implementation has not started**. Priority P1; effort L, staged; risk MED (UI, presentation, settings persistence), rising to HIGH for any subsequent rule changes.

## 1. Intended outcome

The Last Fortress should feel like a small, deliberately designed and finished strategy game. Players understand what they are protecting, why they build in a particular location, what went wrong during the night, and what they want to try differently next time. From launch through the first building to the final assault, visual, audio and informational feedback form a coherent experience.

The first milestone is a **complete six-night game**: the current basic scope, with a finished opening, atmosphere, progression and ending. The second milestone determines whether its decisions and replayability support a small standalone game. The third is an optional expansion toward the original full-release vision. Six nights alone do not establish insufficient scope; twelve nights alone do not establish a finished game.

Product priorities:

1. **Preparation has visible consequences.** Redirected attacks, well-positioned Towers and protected income are recognizable during battle.
2. **A settlement becomes a fortress.** Appearance, sound and the lasting consequences of construction give players a connection to the place.
3. **Each phase has an emotional role.** Day means clarity and anticipation; Night means pressure; dawn means relief and decisions; the finale provides a climax.
4. **Another run has a question to answer.** Players want to try a different defense, investment or use of terrain; changing the seed alone is insufficient.

The acceptance numbers below are proposed milestone targets, not measured results or fixed balance constants.

## 2. Existing implementation and remaining gaps

Repository root: `C:/Users/Miroslav Pavelek/Documents/Games/the-last-fortress`. Unity project: `game/The Last Fortress/`, Unity `6000.3.16f1`, URP, Input System, runtime UI built in C# with uGUI. Abbreviated paths beginning with `Runtime/`, `Editor/`, `Tests/` or `Data/` below are relative to `game/The Last Fortress/Assets/TheLastFortress/`.

| Area | Verified current state | Planning consequence |
|---|---|---|
| Gameplay loop | `Runtime/Core/MvpCampaign.cs:39–58`: six escalating nights, new directions, Runner, Brute and Golem. `PerkRules.cs`: ten perks. | Do not reimplement the campaign or rewards. Assess their experience and strategic effect. |
| Audio | `Runtime/Unity/HudView.Mvp.cs:173–174` explicitly states that the build has no audio; runtime contains no audio sources or playback. | The clearest missing layer of a finished product. |
| Combat presentation | `Runtime/Unity/WorldView.cs:330` already handles projectiles, hits, deaths, destruction and attacks, with bounded effect pools and procedural movement. | Extend and visually verify; do not duplicate plan 006 fixes. |
| Pause | `PrototypeBootstrap.cs:169–179`: `PresentationDelta` freezes combat presentation while suspended. | Audio and new transitions must respect the existing contract. |
| Learning | `HudView.Mvp.cs:159–162`: controls reference in the menu; cards and ghosts explain local actions. No first-run guide was found in the inspected runtime. | Add brief contextual guidance, then test without coaching from the author. |
| Art and UI | `design/README.md` defines a miniature-fortress direction. Generated prefabs are in `Editor/EntityPrefabBuilder.cs`; HUD in `HudView.cs` and `HudView.Mvp.cs`. | Apply one consistent hierarchy from menu to outcome. |
| Persistence | `SaveStore.cs`, `RunPersistence.cs`, `RunCheckpoint.cs`: checkpoints, backup, write-failure and reward protection. `GameSettings` at `SaveStore.cs:34` has no audio or onboarding fields. | Extend compatibly; cosmetic changes do not justify rewriting run persistence. |
| Balance | `plans/evidence/006-improvements/comparison.md`: adaptive defense and economy win seeds 1–3; economy has a larger margin. | Defense has not been shown to be broken. Do not tune the economy solely from the old fixed script. |

Short excerpts for the future executor's drift check:

```csharp
// Runtime/Unity/WorldView.cs:330 — currently the sole event consumer
fxTime += deltaTime;
events.Clear();
sim.DrainEvents(events);
shotTargets.Clear();
foreach (var e in events)
```

```csharp
// Runtime/Unity/PrototypeBootstrap.cs:179
public float PresentationDelta(float dt) =>
    Suspended || (Sim.State == RunState.Night && Sim.Paused) ? 0f : dt;
```

```csharp
// Runtime/Unity/SaveStore.cs:36–44 — settings before extension
public const int CurrentVersion = 1;
public int version = CurrentVersion;
public float cameraSensitivity = 1f;
public bool cameraShake = true;
public bool fullscreen;
public int width;
public int height;
```

Plan 006 already fixed lethal-shot cues, Keep-hit feedback, nightly recap, Restart/Quit guards, effective stats and tooltip layout. Older uncommitted reports at `plans/evidence/007-*/report.md` mainly describe the pre-006 state and must not become a current bug list.

Historical evidence: `plans/006-results.md` records 91/91 EditMode, 15/15 PlayMode and a successful Windows build, including rendered checks. The two subsequent commits adjust HP bars and the burn marker; these counts are not a fresh run against `dc8b55a`. Real-speed Night 4/6 readability and movement of all enemy types remain open in that report.

Preparation included source, specifications and previous-results review, plus inspection of saved images: `plans/evidence/006-rendered/1920x1080-006-08-selected-tower-effective.png` and the historical menu at `plans/screenshots/005-mvp/1280x720-mvp-00-menu.png`. The first is a flat-map fixture, not an ordinary generated map. Stills cannot establish pacing, animation, sound or enjoyment. Neither the game nor Editor was launched; tests and builds were not repeated.

## 3. Target experience specification

### 3.1 First launch and the first two minutes

- Give the title screen its own world composition, title, brief promise, “Build by day. Hold until dawn.”, and clear New Run / Continue / Settings / Credits / Quit actions. The complete gameplay HUD must not remain visible underneath. Keep the product's existing English language; localization is outside this plan.
- Keep the seed available in expanded New Run details and on the outcome screen. It should not dominate first entry. Preserve backup recovery and the explanation of Continue.
- After New Run, show the Keep and one concise objective: survive six nights and the final siege. The default camera shows both the Keep and western approach.
- Provide progressive, skippable guidance: select and place a Tower → notice the route and range → introduce the option to invest in income → start Night → choose a reward afterward. Do not require an economy building or Wall, prescribe a winning layout, or block Start Night because guidance is unfinished.
- Advance from successful gameplay actions, not timeouts or card clicks alone. Invalid placement does not complete a step. Recognize actions already performed, including when the player follows a different order.
- A compact Help control opens the in-run controls reference, including pan/zoom/cancel/pause. Do not restore the removed floating sentence across the map. Help can be reopened; Skip and completion are saved in a separate profile/settings record. Continuing an old run must not restart onboarding.

**Acceptance:** at least four of five new players place a valid defense, start Night and explain the route within two minutes without spoken assistance. Winning is not required. Everyone can find Help and dismiss onboarding. If the check fails, simplify copy/hierarchy first and repeat the affected task with new participants.

### 3.2 Audio supports decisions

The minimum delivery includes usable final clips for the roles below, rather than placeholder beeps. Record provenance and licenses in `Audio/credits.md`; do not buy assets or download sources with unclear licensing. Exact clip count is not a target: variants may be shared where meaning remains clear.

| Layer | Required situations | Rule |
|---|---|---|
| UI and construction | confirmation, rejection, construction, sale/repair, reward | Rejection differs from success; hover does not create continuous noise. |
| Combat | shot/impact, traps, heavy strike, destruction, Keep damage | Keep and destruction take priority over ordinary shots; not every tick needs a clip. |
| Phases | Night start, dawn, Golem arrival, victory/defeat | Play once per actual transition, not on every Refresh or UI load. |
| Environment | calm daytime, distinct night atmosphere | Do not suggest nonexistent units or events. |
| Music | planning, combat, finale, conclusion | Smooth transitions; reopening the menu must not stack tracks. |

Add Master / Music / SFX, persistence, immediate preview and actual silence at zero. The default mix favors threat information over music. For the first implementation, cap concurrent SFX voices at 24, with at most four ordinary impacts from the same family; an important alert may displace a less important voice. Allow at most two music tracks during crossfades. Adjust numbers only after listening and measurement; keep explicit limits.

Pause and the in-run menu suspend combat one-shots and their scheduled completions. Menu UI sounds remain active; music/ambience may continue at reduced volume. Align impact audio with the existing projectile's presentation impact rather than preceding the picture. Reset/Continue clear pending sounds and do not replay loaded combat history. The game remains fully understandable with audio muted.

**Acceptance:** listen to Night 1, dense Night 4 and the finale at normal speed. Keep damage is identifiable in the mix; no clipping or flood of identical samples. Check mute, saving, application restart, pause mid-projectile and repeated menu entry. Saved volumes and old settings load correctly.

### 3.3 A world that feels inhabited and stays readable

- Follow `design/README.md`: matte low-poly miniatures, timber/stone, a warm center against a cool night. Do not introduce a second art direction.
- Finish one complete environment: shoreline and water, ground variation without checkerboard noise, readable forests/rocks, distinct paths and small decorations outside logical obstacles. Do not make legal cells appear blocked with props.
- The Keep remains identifiable behind a Tower. First address scale, value contrast, camera and overlap; add a local occlusion silhouette only if these are insufficient. Do not automatically move the camera during planning.
- Houses have warm windows, Farms recognizable rows, Towers a visible firing moment; heavy enemies convey weight and brief readable anticipation. Animate the presentation model without moving hitboxes or changing damage timing.
- Damaged buildings have at least one clear damage state. Collapse fragments may persist briefly but must not imply an occupied cell; clear them during construction or reset. Do not retain an unbounded permanent object for every loss.
- Blend Day/Night lighting over 0.5–1.0 seconds in presentation only. Start Night immediately starts the defined simulation; alerts and spawns are not delayed by animation.
- Optional settlement life initially consists of smoke, light and environmental movement. Independent villagers, labor economics and additional simulation are not completion requirements.

**Acceptance:** inspect an actual generated map and the flat fixture at 1280×720 and 1920×1080. A new observer distinguishes all five construction options and four enemy types at normal zoom, recognizes occupied cells and sees threats to the Keep. Verify movement and density live or on video at 1×; skipped simulation frames are not evidence.

### 3.4 UI supports play

- Use one visual system: typography, spacing, panel surfaces, buttons and hover/pressed/disabled/focus states. Retain uGUI; migrating every screen or the font system is not a goal.
- In-run hierarchy: Keep condition and budget → tonight's threat → current decision → details. Long exact enemy tables may live in a detail view; a new attack direction remains prominent and accurate.
- Cards receive consistent building icons and clear prices. `design/icons/` contains initial sources, not imported assets for every MVP building. Add missing icons in the same style; use a supported import route without introducing a dependency solely for SVG.
- Perks show their concrete effects and relevance to the current fortress. Preserve the implemented income forecast and effective stats. Brief highlighting of affected buildings is a presentation addition, not another rules calculation in the HUD.
- Keep the nightly recap concise. Outcomes provide a clear conclusion, seed and Retry same seed / New Run / Menu actions; they may use existing facts. Do not invent automatic defeat-cause advice unsupported by data.
- Settings and Credits are finished parts of the game. Remove the unavailable-audio row only when audio works. Credits include fonts, icons, music and applicable licenses. Identify the build version for bug reports.
- Critical information must not rely solely on color, sound or camera shake. Preserve text, shapes, visible focus and the existing shake toggle.

**Acceptance:** inspect menu, New Run, Continue, long selection details, invalid edge placement, reward, pause, both outcome screens, Settings, save failure and confirmation dialogs. Neither supported resolution has overlaps, clipping or hidden primary actions. Use real longest-case content, not just an empty Day 1.

### 3.5 Escalation, decisions and reasons to replay

The campaign already introduces new directions and enemies. First emphasize these changes: announce the new direction before Night 2, briefly explain the first Brute's role, and announce the final siege before Night 6. Information must come from the actual stored wave. Give Golem arrival audio, a visible signal and the existing boss bar through nonblocking presentation, without a long cutscene or overriding camera focus.

Preparation is the main source of agency. At Night, players observe and evaluate consequences; do not add an active ability solely because automatic combat looks passive in source. During playtests, record time without meaningful decisions and intervals when players stop watching combat. Consider speed-up only for repeated, observed waiting; it needs a separate timing contract and pause/presentation checks.

After presentation work, verify two understandable approaches on several maps: economy with delayed defense, and defense exploiting terrain/corridors. Do not require equal final HP or profit. If one arrangement of Towers around the Keep solves everything without spatial decisions, restore the importance of placement first; additional cosmetic perks will not solve it.

If strategic shallowness is demonstrated:

1. Record the specific situation, seed, purchases, reward offers and problem: unclear information, a dominant tactic, or a missing decision.
2. Improve explanation first if players do not know an existing rule. Change balance only when the problem repeats for informed players too.
3. Test one change in a separate experiment: terrain/direction roles, existing synergies, costs/timing or wave composition. Preserve a control baseline and define saved-run compatibility for changed rules.
4. Only if the existing roster cannot support two different solutions, prepare a bounded content addition. A candidate is a Cannon Tower with slow area attacks against dense groups. Before implementation, specify footprint, splash/friendly-fire rules, targeting, cost, save ID, perk interactions and tests. This candidate is not an approved complete mechanic specification.

**Acceptance:** in five observed runs, at least four players describe a concrete consequence of their placement and what they would change next time. At the end, offer another run without encouragement; record the choice and reason. At least three of five choosing to restart is an indicative signal, not statistical evidence of retention. Do not lead players toward desired answers.

### 3.6 Full-release scope

The original `specs/archive/001-game-design.md` describes twelve nights and a larger roster. This plan preserves that vision while preventing implementation of its entire content list before validating the core game.

After the complete six-night version, decide from playtests:

| Finding | Next step | Requires a separate specification |
|---|---|---|
| Players learn and want more threats | Expand toward twelve nights with a midpoint turn and finale. | Full wave table, run length, economy, rewards, checkpoint/rules version; Nights 7–12 cannot merely increase counts. |
| Maps play the same way | A small set of distinct tactical map profiles with seeded variation. | Space guarantees for economy/defense, connectivity, attack telegraphs, fallback and determinism. A new biome alone is insufficient. |
| Repeating one Tower is the only right answer | One new defensive tool and a validated threat that changes preparation. | Complete combat/navigation/save contract; validate the role before final assets. |
| The game works as a short standalone game | Finish the small scope; add optional challenges if demand supports them. | Separate challenge rules and compatibility; no silent hard mode for old saves. |

Defer soldiers, multiplayer, a large campaign, mandatory-grind meta progression and multiple biomes for the first finished product. Horizontal unlocks and challenges may follow; do not gate basic clarity, defensive tools needed to win or better stats behind repeated losses. Steam publishing, commercial pricing, achievements and cloud saves form a separate distribution milestone, not a measure of core quality.

## 4. Implementation order

Each stage ends with a playable handoff and an entry in `plans/007-results.md`: changes, exact verification, evidence, unresolved items and next action. Begin implementation only after a separate instruction to execute. Do not repeat plans 003–006.

| Stage | Deliverable | Dependency | Rough effort for one developer |
|---|---|---|---|
| A | Baseline and concrete visual/audio inventory | Existing MVP | 0.5–1 day |
| B | Shared event distribution, audio, mix and volume | A | 3–6 days plus available clips |
| C | Menu, consistent HUD, contextual guidance, Settings/Credits | A; finish audio controls with B | 3–6 days |
| D | One finished world, animation, transitions and finale | B + C | 5–10 days depending on assets |
| E | New players, focused pacing/strategy fixes, second run | B–D | 2–5 working days plus participant availability |
| F | Stabilization and complete distributable Windows package | E | 2–4 days |

Total: approximately 3–7 working weeks without a major content expansion, not a committed estimate. Commissioned music, new models, a fundamental strategy problem or a twelve-night version change the estimate. If time is limited, reduce decorations and audio variants while preserving clarity, basic audio, the opening/ending and stability.

### A — Establish the baseline

1. Read root `AGENTS.md`, this document, `plans/006-results.md`, `specs/archive/003-six-night-mvp.md` and relevant current code. Run `git status --short` and `git diff --stat dc8b55a..HEAD -- "game/The Last Fortress/Assets/TheLastFortress"`. Include uncommitted changes: at planning time, `ProjectSettings/EditorBuildSettings.asset` and `EditorSettings.asset` were already modified and three 007 report folders were untracked. Do not overwrite them.
2. Record differences from the excerpts and adapt steps to the current implementation. Do not recreate resolved problems. If rules differ from spec 003, stop dependent implementation and describe the conflict.
3. Before changes, capture an ordinary generated map, menu, Day 1, Night 4, finale and outcome. Night 4/6 recordings must run at 1×. Create `plans/evidence/007-experience/baseline.md` with specific planned changes, audio/asset sources and budget. Identify captured files by date, build and seed.

Verification: the baseline report distinguishes measurement from hypotheses. If displaying the game is prohibited or rendering is unavailable, visual/listening checks remain PENDING. Source review cannot satisfy them.

### B — Add audio without losing events

1. Change event distribution: `WorldView.Sync` currently calls `RunSimulation.DrainEvents` itself. Audio must not drain separately and consume events before visuals, or vice versa. Move the single drain to `PrototypeBootstrap.SyncViews`; pass the same read-only batch to WorldView and a new `AudioDirector`. Preserve public behavior used by tests; update all actual call sites if a signature changes. Do not introduce a universal global event bus.
2. Put `AudioDirector` and referenced `AudioContent` in `Runtime/Unity/`; clips and any mixer in `Audio/`. Trigger UI sounds in the UI after successful actions; derive combat from authoritative events and align impacts with presentation time. Do not duplicate damage calculations or use gameplay RNG for audio variation.
3. Add versioning/compatible settings reads in `SaveStore.cs`. Do not assume deserializing old JSON initializes missing floats to intended values; explicitly migrate absent audio values to the new default volumes. Handle NaN, infinities and the 0–1 range. This does not change RunCheckpoint.
4. Connect mixing, limits, pause, reset and Settings as specified in 3.2. If final clips are unavailable, mark technical integration complete but audio delivery PENDING; do not present placeholders as finished sound.

Verification: focused PlayMode integration checks for both consumers receiving each event batch once, actual mute/persistence and pause/resume; then the listening checklist in 3.2. Automated tests cannot approve mix quality.

### C — Finish entry into the game and information hierarchy

1. Modify existing `HudView` and `HudView.Mvp` incrementally; new MVP sections may use corresponding partial files. Preserve `RequestRestart`, `RequestQuit`, save-failure handling, seeds and backup recovery.
2. Separate menu visibility from gameplay panels; necessary warnings remain available during an active run. Do not rebuild the UI hierarchy on refresh.
3. Create a small `OnboardingState` with explicit steps. It observes successful actions/events without changing simulation, gold, offers or waves. Persist guidance preferences/progress separately from the run.
4. Add icons, consistent button states, Help, Credits and audio controls; verify the complete screen list in 3.4.

Verification: affected `MvpSceneTests`, especially `MenuSeededRunCheckpointContinueOutcomeRetryAndSettings` and `TooltipStaysOnScreenAtEveryCornerAndSelectionActionsClearDetails`. Add a small integration safeguard only for skipping onboarding and Continue without checkpoint mutation. Then inspect actual screens at 720p/1080p.

### D — Finish the world and climax

1. Extend current prefabs/materials and their referencing `PrototypeContent`/`MvpSetup`; preserve footprints, pivots, wall masks and the separation of animated models from simulation roots. Setup regeneration must not silently overwrite manually polished assets. Choose and document one ownership mode: generated or manually authored.
2. Add building damage presentation, bounded ambient animation and lighting transitions. Record decoration, effect and light limits in the report. Do not add a shadow-casting light per building.
3. Add the boss entrance and final visual/audio motif. Read transitions from state/events and play them once; presentation duration must not delay outcome controls or another run.

Verification: existing `LethalShotCueSurvivesRemovalAndCombatEffectsFreezeWhenSuspended`, connected-wall and effective-range checks. Add a test only for a concrete new timing/reset risk. Render/video checks from 3.3 and listening to the finale. A material change alone does not need a unit test.

### E — Validate player experience and decide content scope

1. Arrange five participants who have not played the game. An agent running a simulation script is not a substitute. Do not coach participants; record observations, seed, wall-clock phase durations, choices and specific misunderstandings. Do not contact people externally without the owner's instruction; the owner may provide recruitment.
2. Use consistent tasks: start and survive the first night; explain routing and income; notice a new attack direction; evaluate a loss; choose a perk and explain its effect; choose the next action after the outcome. The observer must not supply interpretations.
3. Record criteria from 3.1 and 3.5, actual run length, waiting intervals and willingness to replay. The GDD's original 25–40-minute target cannot be inferred from simulated night duration or achieved by artificially slowing play.
4. Fix specific repeated obstacles. Specify each rule change briefly in the report first and update the corresponding rules document; compare identical seeds and policies before/after. If another tool or twelve nights are needed, create a separate follow-up plan under 3.6; do not label 007 a finished full commercial release.

Verification: `plans/evidence/007-experience/playtest.md` contains anonymous results, unmet goals and a decision: “small game / next content milestone / rework core decisions.” Without participants this stage remains PENDING; technical work may continue.

### F — Stabilize and hand over the complete package

1. Verify clean first launch, New Run, old settings, an old Day checkpoint, save/restart/Continue, endings, Settings, backup recovery and write failure. Use a separate `-saves` directory, never actual user saves as test material.
2. Run the full existing suites and Windows build after final integration. Document real errors and new warnings; do not demand an artificial zero for historical warnings.
3. Measure the changed build at 1080p in the existing stress fixture and a representative normal finale. Target on a recorded reference PC: stable 60 FPS, p95 frame time ≤16.7 ms, and no leaked voices/effects after five Menu → New Run → end cycles. Record CPU/GPU, settings, sampling interval and memory; do not transfer plan 005 figures to a different machine. If the target hardware changes, revise the explicit performance target before evaluation.
4. Deliver the whole `Builds/Mvp/` directory, including data, rather than the `.exe` alone. Include controls, Credits/licenses and known limitations. Normal startup shows the finished menu without debug-fixture banners. Diagnostic CLI options may remain available for verification.

Verification: build report `Succeeded`, `errors=0`; test XML with nonzero counts and no failures; standalone smoke test outside the Editor; final report distinguishes fresh checks from unperformed checks.

## 5. Verification commands and testing policy

These commands come from the README and `Editor/BuildMvp.cs`; they were not run during planning. Execute from the repository root. Use batch Unity commands only with the project closed in the Editor. If it is open, use that instance's testing tools; never start a second Editor against the same project. Respect current owner constraints on displaying/controlling windows; record unavailable live review as PENDING.

```powershell
$fortressEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
$fortressProject = (Resolve-Path 'game/The Last Fortress').Path
$fortressResults = Join-Path $fortressProject 'TestResults/007'
New-Item -ItemType Directory -Force -Path $fortressResults | Out-Null

# Run affected UI integration first (example filter for presentation stages).
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform PlayMode -testFilter TheLastFortress.Tests.MvpSceneTests -testResults "$fortressResults/PlayMode-focused.xml" -logFile "$fortressResults/PlayMode-focused.log"

# Final integration: both complete suites.
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform EditMode -testResults "$fortressResults/EditMode.xml" -logFile "$fortressResults/EditMode.log"
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform PlayMode -testResults "$fortressResults/PlayMode.xml" -logFile "$fortressResults/PlayMode.log"
& $fortressEditor -batchmode -nographics -quit -projectPath $fortressProject -executeMethod TheLastFortress.Editor.BuildMvp.BuildWindows -logFile "$fortressResults/Build.log"

# Only where launching a rendering build is permitted.
$fortressBuild = Join-Path $fortressProject 'Builds/Mvp/TheLastFortress.exe'
& $fortressBuild -capture "$fortressResults/capture-720" -saves "$fortressResults/saves-720" -screen-width 1280 -screen-height 720 -screen-fullscreen 0
& $fortressBuild -capture "$fortressResults/capture-1080" -saves "$fortressResults/saves-1080" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
& $fortressBuild -stress -measure "$fortressResults/stress.txt" -saves "$fortressResults/saves-stress" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
```

Check the current `MvpSceneTests` namespace before using the filter; zero tests indicate a filter error, not a pass. Windows GUI applications may return control to the shell before exiting. For automation, use `Start-Process -PassThru -WindowStyle Hidden` with bounded process waiting, communicate progress, and read results only after termination. Use an authorized interactive environment for listening/live visual review. Existing capture sequences accelerate parts of the simulation; add a narrowly scoped real-time 1× Night 4/6 mode to `CaptureSequence`, or record authorized video. Do not judge density from skipped frames.

Expected results: XML `result="Passed"`, nonzero counts and no failures; logs without compile errors/exceptions; `Builds/Mvp/build-report.txt` contains `result=Succeeded` and `errors=0`; the stress report contains a completed measurement. Renderer failure is not automatically a simulation defect, but blocks the corresponding visual acceptance.

Follow root `AGENTS.md`. Read the nearest existing scenario before adding a test. For save compatibility, extend patterns in `Tests/EditMode/MvpReplayAndSaveTests.cs` (`DayCheckpointRoundTripsAndReplaysIdentically`, `RewardLedgerReplayReusesOfferWithoutRerollOrDoubleIncome`); use `Tests/PlayMode/MvpSceneTests.cs` for actual Unity lifecycle, settings and effects. Policies in `MvpPolicyTests.cs` establish mechanical viability, not enjoyment. Do not test exact colors, decoration counts, cosmetic constants or the number of new tests. Do not rewrite existing assertions for a new appearance when their behavioral contract still applies.

## 6. Boundaries, risks and handoff

**Allowed future implementation scope:** `Assets/TheLastFortress/Runtime/Unity/`, relevant `Editor/MvpSetup.cs`, `Editor/EntityPrefabBuilder.cs`, `Editor/BuildMvp.cs`, `Prefabs/`, `Materials/`, `Data/MvpContent.asset`, `Scenes/Mvp.unity`, new content in `Audio/` and `UI/`, focused tests, project README, associated specifications for approved rule changes, and `plans/007-*`/evidence. Unity `.meta` files belong with their assets.

**The initial stage does not change:** Core combat, routing, placement, RNG, prices, wave counts, perks, map generation, checkpoint rules or the prototype scene. Shared presentation must preserve the one-wave prototype; optional MVP references need safe fallbacks. Do not broadly refactor `RunSimulation`, migrate to ECS/NavMesh, upgrade packages, add a backend, buy assets, commit/push or publish. A Core change from E first needs a narrow explicit specification and expanded scope.

**Stop dependent work and record the specific reason if:**

- Visuals and audio do not each receive shared events exactly once, or the change requires altered damage timing.
- New art requires different footprints/connectivity, or setup deletes manually authored assets.
- An old save/settings file stops loading without a completed compatibility path. Never silently delete a damaged file.
- A player problem requires a new mechanic, unbounded content expansion or an out-of-scope change. Continue independent presentation work and document the follow-up decision.
- The same technical gate fails twice after a focused fix: preserve the last playable state, record the log and next action; do not weaken assertions or mark unperformed verification as passed.

A complete technical milestone requires B–D and F, including actual listening and rendering. A complete **player-validated** milestone additionally requires E and criteria that are met or explicitly reconsidered. “Tests passed” and “feels like a complete game” are separate claims.

During maintenance, watch single event distribution, presentation-clock pause behavior, settings migration and generated-asset ownership. Each new enemy needs a silhouette, readable role, sound, preview and save compatibility. Before adding another system, identify the new player decision it provides.

## 7. Future execution prompt

> Read AGENTS.md, plans/007-complete-game-experience.md and current plan 006 results. Execute stages A–F in dependency order, updating plans/007-results.md and the index after each stage. Preserve six-night rules and saves; do not duplicate completed plan 006 work. Prioritize audio, entry into the game, coherent presentation and validation of actual decisions. Content expansion in 3.6 is a conditional subsequent milestone, not an automatic task. Do not present simulations or screenshots as human playtests. Respect current permissions for windows, builds and contacting people; do not publish or buy anything. Final handoff: complete Windows package, concise changes, fresh evidence and clearly marked PENDING checks.

This document prepares a specification and plan only; it does not start implementation.
