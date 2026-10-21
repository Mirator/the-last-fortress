# Third-person audit: from prototype to a polished production slice

2026-10-20. Source baseline: `adac6b4`; working tree clean at audit start.
Status: **AUDIT COMPLETE; owner directions resolved; specification proposal, implementation plan and prompt delivered.**

Follow-up owner decisions (2026-10-20): **allow combat and movement redesign; camera pulls inward first and lifts only if needed**. The earlier preserve-rules recommendation and pending-decision wording below are historical audit-stage proposals, superseded by [the V4 feature-spec proposal](018-third-person-spec-proposal.md), [implementation plan](019-third-person-production-slice.md) and [implementation prompt](020-third-person-implementation-prompt.md). No production implementation has started; concrete initial values remain proposed tuning, not playtested facts.

This is an audit and a proposed quality contract, not an instruction to modify gameplay. It reviews `specs/002-game-rules.md` **v1.5**, `specs/001-vision.md` **v2.1**, the specs index, archived hero spec v0.1, plans 012–016 and results, relevant Unity/Core/art/audio code, and existing hero tests. Current rules, not the archived hero spec or historical plans, are authoritative.

## Assessment

The foundation is useful: deterministic movement, read-only visual prediction, two cameras, buffered attacks, pause-aware presentation, hit events, and a consistent low-poly world. The weak point is the production contract connecting them. Several independent approximations—collision prediction, a one-ray camera, forward-only procedural legs, oversized gameplay geometry, and reused audio—become conspicuous from a close third-person camera.

The next milestone should be one polished hero interaction slice: move, turn, slide beside a wall, fight one enemy, fight a group, get hit, fall, respawn, and return to strategy. Additional abilities or a broad environment overhaul will not fix these issues.

“AA” here means deliberate framing, grounded directional movement, coherent attacks, useful feedback, and consistent behaviour through interruptions. It does not require photorealism or abandoning the existing colourful low-poly direction.

## How it currently works

All runtime paths below are under `game/The Last Fortress/Assets/TheLastFortress/`.

1. `Runtime/Unity/PlayerController.cs:183–213` maps camera yaw and WASD to a 16-direction movement heading and a 64-direction facing. Left click is buffered for 0.25 seconds; holding it requests repeated attacks.
2. `Runtime/Unity/PrototypeBootstrap.cs:306–326` samples input, advances the simulation, then synchronizes presentation. Core steps every 50 ms. Day has a hero-only clock; Night has the normal combat clock. Hero view forces 1× and strategic view restores the previous speed.
3. `Runtime/Core/RunSimulation.cs:1660–1692` moves the hero in integer units, resolving X then Z and finding the maximum legal distance into an obstacle. Units pass through each other. Current release speed is 0.8 tile/s; old hero saves retain their earlier speed.
4. `Runtime/Unity/HeroPresenter.cs:181–234` predicts a fraction of the next step, limits displayed movement to 2.4 tiles/s, rotates the body, and feeds its displayed feet position to the camera.
5. `Runtime/Unity/HeroCameraRig.cs:94–146` follows a shoulder pivot with direct mouse orbit, zoom easing, and an approximate grid-height obstruction query. Entry eases over 0.35 seconds; returning to strategy immediately switches cameras. Re-entry resets yaw and pitch.
6. `Runtime/Core/RunSimulation.cs:1722–1735` resolves all targets in the swing immediately. `HeroPresenter.cs:264–314` produces a procedural slash; `WorldView.cs:249,1013–1018` and `AudioDirector.cs:390–391` delay contact cues 0.08 seconds. Damage is never decided by animation.
7. `Runtime/Unity/CharacterRig.cs:79–117` generates forward gait, torso twist, hit and death poses mathematically. The hero uses the shared part rig; entire arms are rigid pieces, despite elbow-shaped geometry.

## Findings, ordered by leverage

Effort: S = hours, M = roughly 1–2 days, L = multiple days including verification. These are coarse estimates, not a schedule. Confidence is confidence in the source finding; visible severity still needs live review.

| Priority | Finding and impact | Evidence | Effort | Change risk | Confidence |
|---|---|---|---|---|---|
| P1 | Prediction disagrees with authoritative collision. Near a wall, fractional prediction may advance and then retract when its endpoint becomes blocked; Core instead moves up to contact. This can disturb both hero and camera. | `HeroPresenter.cs:249–258`; `RunSimulation.cs:1672–1692` | M | Low if existing integer movement outcomes remain identical | High |
| P1 | The camera protects a sampled centre ray, not its visible volume. The minimum 0.2 distance can remain unsafe in tight spaces, and the entry blend does not recheck its final pose. | `HeroCameraRig.cs:123–146`; `PrototypeBootstrap.cs:360` | M–L | Medium: correction may introduce snapping | High mechanism; medium observed severity |
| P1 | Combat-facing movement uses forward-only legs. Holding attack keeps facing camera aim even between swings, while gait has no local strafe/backward direction. | `HeroPresenter.cs:198,216–219`; `CharacterRig.cs:79–112` | L | Medium: pose blending and aim consistency | High |
| P1 design | Physical scale and gameplay geometry disagree. A roughly 0.22-tile person has a 0.3-tile collision radius and 0.6-tile attack reach. At 8 m/tile these are 2.4 m and 4.8 m. A sword plus the capped visual step-in cannot cover that entire area. | Rules §17; `Runtime/Core/Hero.cs:86,90`; `HeroPresenter.cs:19,303–309`; `PrototypeBootstrap.cs:48` | M decision; L redesign | High if geometry changes: traversal, engagement and balance | High |
| P1 spec | One current camera paragraph prescribes opposite obstacle priorities. Implementers cannot satisfy both. | `specs/002-game-rules.md:330` | S | Low for clarification | High |
| P2 | Cursor capture has no explicit application-focus lifecycle. The update reapplies capture based on run/menu state; focus return and stale transient input depend on platform behaviour. | `PrototypeBootstrap.cs:54,327–331`; `PlayerController.cs:211–213` | S–M | Low–medium | High missing lifecycle; medium platform symptoms |
| P2 | Hero motion is constrained by the rig and one procedural envelope. There are no articulated elbows or authored starts, stops and turns. More easing constants cannot supply those poses. | `Art/Source~/characters.py:122–141`; `CharacterRig.cs:79–117`; `HeroPresenter.cs:264–314` | L | Medium; preserve enemy animation while improving the hero | High |
| P2 | Re-entry discards orbit and exit cuts instantly. The transition policy is asymmetric and weakly specified. | `HeroCameraRig.cs:64–73`; `PrototypeBootstrap.cs:394–402` | M | Medium: deliberate UX choice | High behaviour; medium preferred change |
| P2 | Hero sound identity is borrowed from strategy combat, and every source is 2D. Swing uses tower Shot, down uses KeepHit; there is no foot-contact pathway. | `AudioDirector.cs:116–123,387–397`; `CharacterRig.cs:79–117` | M | Low–medium: preserve voice limits and threat priorities | High |
| P1 validation | Existing tests establish useful integration behaviour, but previous results explicitly leave live feel pending. Static captures and passing tests do not establish movement quality. | `Tests/PlayMode/ReleaseSceneTests.cs:284–359`; `plans/README.md` rows 012–016 | M review | Low | High |

Current collision geometry and instant damage are documented gameplay, not accidental bugs. Proposed changes must distinguish those decisions from defects such as prediction divergence.

## Visual evidence and limits

Inspected saved capture: `plans/evidence/016/captures/after-crossing-h1-day-edge.png`. It shows a readable hero silhouette and coherent palette, but also a large reach sector relative to the character and a HUD still dominated by strategic information. It is an existing screenshot, not a fresh render or evidence of smooth motion.

The saved 016 XML reports 119 passed EditMode cases and 22 passed / 1 skipped PlayMode cases. They were read, not rerun. The existing hero integration test covers simulation invariance, input, look direction, speed lock, initial movement and menu release; it explicitly skips the camera entry transition. That coverage is valuable and should be extended only for concrete new risks.

No gameplay session, new capture, listening session, build, benchmark or tests ran during this audit. No production code or current spec was changed. This is not a whole-repository security/dependency audit. Gamepad, broad campaign balance, all close-up assets and final release readiness were not audited.

## 1. Suggested specification improvements

Create a new feature spec, proposed name `specs/003-third-person-production-slice.md`, **v0.1 proposed**, after the decisions below. It extends current rules v1.5; it must not treat archived `006-third-person-hero.md` as current requirements. Keep `002` a description of implemented behaviour. On implementation, merge the accepted contract, update its version/status and `specs/README.md`, and archive the feature spec according to the existing workflow.

### A. Explicit scope and ownership

Preserve strategic planning as the central game. Core owns position, collision, command quantization, targets, cooldowns, damage and death. Presentation owns mesh pose, camera, audio and effects. Animations and Unity physics must never write simulation position or trigger damage.

Recommended first milestone: retain current gameplay values, 20 Hz simulation, instant-hit timing, save compatibility and pass-through units. Correct prediction, camera safety and animation coherence first. Realistic body clearance/reach, wind-up attacks, dodge, lock-on, stamina and root-motion locomotion are separate rules proposals.

### B. Resolve the scale/contact decision

Choose one honest attack language:

- **Tactical sweep, existing rules:** preserve 0.6 reach and 120° area; communicate a deliberately stylized sweep. The sword need not physically touch every victim. Replace the impossible promise of universal blade contact with a coherent area-impact cue, correctly oriented and limited to actual authoritative hits.
- **Physical melee, gameplay redesign:** choose hero clearance, weapon dimensions, hit reach, enemy engagement and contact-phase timing together. Validate space beside buildings and crowd fights. Define version/save behaviour for every changed invariant; preserve old profiles. Do not merely shrink radius or extend the mesh until the screenshot looks right.

Recommendation: tactical sweep for the initial polish milestone, followed by a separately approved physical-melee experiment if desired. The geometry choice has more effect on close combat credibility than adding more particles.

### C. Camera contract

Recommended obstruction policy: pull inward first, lift only when needed, bounded by the existing 15° maximum. Remove the contradictory lift-first sentence from the current rule description once the owner resolves it.

Specify camera clearance in terms of near-plane volume and presentation geometry, including corners, roof overhangs, the pivot starting inside an obstacle, zoom limits and entry transitions. A clear centre ray alone does not pass. Use fast safe obstruction response and slower release, without oscillation at cell boundaries. If no safe interpolated route exists, use a documented safe cut rather than crossing geometry.

Separate camera orbit from character animation offsets: foot bob, hit flinch and visual step-ins must not shake the camera. Recommended re-entry: preserve last hero orbit within a run; reset on a new/loaded/restarted run. Recommended exit: quick deliberate return to retained strategic framing; a safe cut is acceptable, but document it. Do not add mandatory camera shake or automatic recentering.

### D. Movement and animation contract

Require visible movement by the first rendered frame with usable input, and prediction that uses the same collision resolution as Core without mutating state. Bound normal visual root error to one current-profile step; reconcile within 0.1 s. Teleports, reset and respawn are explicit exceptions and must reset presentation history.

Specify signed local forward/sideways velocity, start/stop behaviour, moving turns, idle turns and attack locomotion. Legs must communicate movement direction while the upper body aims. At rest, do not continue stepping. After a blocked move, feet must settle. Define interruption priority: reset/relocation and death clear invalid attack/hit state; pause freezes combat animation and effects while camera orbit remains available.

Deliver one hero-specific animation adapter before considering replacement of the shared enemy rig. Target asset contract: articulated elbows, wrists and ankles, stable feet, sword grip and shield pose; idle, directional locomotion, starts/stops, turns, one finished attack, additive hurt, down and rise. A second attack variation is optional only after the first meets the quality gate. Authored curves or skeletal clips are both possible; choose based on approved quality and available assets rather than mandating a package.

### E. Feedback, audio and HUD contract

Define one attack timeline with input intent, authoritative acceptance, resolved hit set and presentation contact. A rejected cooldown request cannot create a fake hit. With instant damage retained, document the presentation delay and ensure hit, death, sparks, audio and body orientation tell the same story. All-hit area swings may have one dominant target pose plus area feedback; never invent additional damage or enlarge reach for animation.

Add distinct swing, blade impact, hurt, down and footfall sound families. Footfalls follow actual foot contacts, not a free-running timer. World events use bounded positional playback; UI and music remain 2D. Keep existing caps, reset behaviour, pause semantics and threat priority. Verify by listening during movement and a busy fight.

Keep hero HP, Keep danger, incoming threats and strategy return legible. Reduce the persistent attack sector to an intentional, readable tactical cue; do not remove the only honest indication of broad reach before approving replacement cues. Preserve the owner-requested crosshair and look polarity. Record which HUD elements remain visible in Day, Night, pause, menu and down states.

### F. Measurable acceptance

Use one deterministic scenario layout and record seed, profile, positions, build, resolution, frame rate, FOV and settings. Compare the same route and fight before/after.

| Scenario | Required outcome | Evidence |
|---|---|---|
| Start, stop, reverse, diagonal wall slide | First-frame response; prediction collision agrees with Core; no repeated backward correction under constant input | Focused test plus frame-stepped motion trace/video |
| Move sideways/backwards while holding attack | Directional feet and aiming torso agree with movement; no forward running pose sliding backwards | Short live capture and owner review |
| 180° aim change and swing | Pose blends coherently; hit cue remains at resolved facing/location; no extra hits | Event/pose trace and video |
| Keep corner, Wall, tree, rock, zoom extremes, transition | Camera near plane stays outside defined presentation blockers or takes its documented safe fallback; no repeated pumping | Focused camera cases and live capture |
| Near/far/edge-of-arc attack, miss, multi-hit, lethal hit | Feedback reflects authoritative victims, once each; chosen sweep/contact language remains coherent | Focused integration check and video |
| Pause mid-hit, menu, focus loss/return, view switch | No simulation changes from presentation; no accidental attack, stale look delta or unwanted cursor capture | Existing suite plus focused lifecycle regression and desktop check |
| Death, respawn, Day relocation, restart/load | Invalid timers/contacts cleared; no glide from old position or ghost slash | Focused regression and video |
| 100-enemy fight at 720p/1080p | Retain current ≥60 fps / hero ≤1.5× strategic gate on reference machine; additionally report frame-time distribution and hitches | Paired visible-build measurement, Editor closed |

These are proposed acceptance criteria, not measured results. Numeric presentation thresholds need freezing in the approved feature spec before an executor tunes against them. Human motion/listening review is required in addition to automated checks; leave it pending when unavailable.

## 2. Implementation planning handoff

The requested executable implementation plan is pending the owner decisions below. Root `AGENTS.md` explicitly requires stale/contradictory specs and dependent open decisions to be resolved before writing it. This audit therefore does not silently choose one camera policy or rewrite combat inside a plan.

Recommended work packages for the later plan, in dependency order:

1. Freeze the production-slice feature spec and record current reproducible motion evidence.
2. Unify read-only prediction and authoritative collision calculation, preserving command-stream outcomes.
3. Establish camera-volume safety, obstruction recovery, transitions and focus/input lifecycle.
4. Introduce the hero animation adapter and finish directional locomotion, starts/stops, turns and interruptions.
5. Finish the chosen attack language, contact/effect timeline, hero audio and restrained HUD.
6. Validate the slice in the release build, then integrate accepted spec changes and record remaining human gates.

These are scope recommendations, not approved implementation steps. The final plan must cite the new feature spec/version, supply exact file scope, per-stage checks, accepted presentation thresholds, stop conditions and current excerpts. Do not rerun historical plans 012–016 or repeat their content production wholesale.

Use existing `HeroRulesTests` and `ReleaseSceneTests` as fixtures. Likely concrete regressions to protect: partial movement into a wall/corner, near-plane/transition collision, directional animation interruption, focus-return stale attack, and hit/death cue coherence. Extend existing cases where possible; do not create tests for decorative values or target a test count.

Verification tooling already present: Unity `6000.3.16f1`; editor executable at `C:/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe` exists. The repository documents `-batchmode -nographics -projectPath <project> -runTests -testPlatform EditMode|PlayMode -testFilter <fixture> -testResults <xml> -logFile <log>`. Run affected fixtures first, then full suites only for shared integration risk. Build method is `TheLastFortress.Editor.BuildRelease.BuildWindows` (`Editor/BuildRelease.cs`). These commands were inspected, not executed. Do not start a second Editor against an already open project; use the running Editor's supported test/build path or wait for safe access. Rendering, listening and visible frame pacing require graphics and are separate from headless test success.

## 3. Prompt for the implementing agent

Use this only after the spec and executable plan have been finalized; the unresolved state is a deliberate guard, not an implementation authorization.

> Read root AGENTS.md, specs/README.md, specs/001-vision.md, specs/002-game-rules.md and plans/017-third-person-audit.md. Confirm that the owner has resolved the audit's camera-order contradiction and chosen the gameplay/contact scope. If either decision is missing, report the exact missing decision and continue only independent read-only investigation; do not invent approval. Read the approved third-person production-slice feature spec and its separate executable implementation plan in full. Verify their recorded versions, baseline and allowed files against the current tree. Implement that plan in its stated order, preserving user work, deterministic outcomes, legacy saves and the strategic game. Use one movement calculation for authoritative simulation and read-only visual prediction; keep animation and camera from driving gameplay. Deliver the bounded hero slice before expanding art or mechanics. Run affected tests first and add only focused protection for named bugs or new risks. Capture matched motion, listen to hero actions, and measure paired visible-build performance as required; distinguish fresh evidence from existing results and leave unavailable human acceptance pending. Record results, last verified stage, next action and limitations; update spec/index status together only when implementation is actually complete. Do not commit, push, publish, purchase assets, add dependencies, alter invariant rules or broaden the plan without explicit authorization. Stop dependent work on spec contradictions or an unapproved gameplay change, and report a concrete proposed resolution.

## Decisions to resolve before the executable plan

1. **Gameplay scope:** preserve current instant-hit rules and geometry for the first polish slice (recommended), or authorize a separately versioned movement/melee redesign? Preserving rules implies deliberately stylized tactical sweep feedback, not a promise of physical sword contact across 4.8 m.
2. **Camera precedence:** pull in first, lift only if needed (recommended), or lift first? The current spec says both.

Proposed non-gameplay defaults for the feature spec are stated above and remain proposals: retained orbit within a run, explicit focus recapture with transient input cleared, directional locomotion and bounded hero asset scope. Approving a direction does not imply a passing quality gate or authorize paid assets.

## Considered and rejected

- More generic smoothing everywhere: can hide discrepancies while adding latency; first fix prediction and state ownership.
- Immediate Cinemachine migration: current project has no Cinemachine dependency; a package switch does not fix scale, locomotion or contact. Evaluate only if the bounded camera implementation proves inadequate.
- Physics CharacterController / NavMesh hero rewrite, higher simulation frequency, ECS: no evidence these are needed for the reported presentation problem; they increase determinism and integration risk.
- New combos, dodge, stamina, lock-on, progression: gameplay expansion does not establish a professional baseline for the existing movement and attack.
- Particle density, bloom or motion blur as the main improvement: cannot repair directional feet, reach mismatch or camera clipping.

## Primary implementation references

Unity's documented blend trees support blending motions by speed/direction; animation layers and masks support separating body regions. They are viable options for a hero adapter, not a mandatory migration or a substitute for approved clips. See [Unity 6.3 animation blend trees](https://docs.unity.com/en-us/engine/6000.3/manual/animation-section/animation-mecanim/animation-animator-controller/animation-state-machines/animation-blend-trees) and [Unity Avatar Masks](https://docs.unity.cn/Manual/class-AvatarMask.html). Camera damping and follow behaviour can be compared with the [official Cinemachine follow implementation documentation](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/com.unity.cinemachine/Documentation~/CinemachineFollow.md); no package installation is proposed by this audit.
