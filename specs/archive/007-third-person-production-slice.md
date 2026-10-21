# Third-person production slice — feature specification

Version **0.1**, 2026-10-20. Status: **IMPLEMENTED and merged into `../002-game-rules.md` v1.7 section 18 (2026-10-20); archived**. Technical gates passed; owner review of live motion, art and listening is still pending, so the quality gates of section 8 are not signed off. Not current: plan from `002-game-rules.md`.

Owner direction: combat and movement may be redesigned; camera obstruction pulls inward first and lifts only when needed. These directions are approved. The concrete design and tuning below are recommendations supplied by this specification, not a claim that the owner individually approved each value or reviewed the resulting motion.

This document extends `specs/002-game-rules.md` v1.5 and respects `specs/001-vision.md` v2.1. It changes hero rules only for new runs under `release-rules-4`; it replaces the hero movement, swing and presentation contract of §17 for that profile. Other rules remain unchanged unless explicitly stated below. This spec deliberately does not reference an implementation plan.

## 1. Goal and boundaries

Make one hero feel intentionally animated, controllable and grounded at close range while fortress planning remains decisive. Deliver one finished movement/combat slice before expanding mechanics or content. Retain colourful readable low-poly art, a single hero, keyboard/mouse, and both views.

No dodge, stamina, lock-on, combos, equipment, progression, unit avoidance, new enemies, wave changes, NavMesh/physics-driven hero, multiplayer or mid-night saving. Space remains the existing cosmetic hop: it grants no invulnerability, elevation, obstacle traversal or damage avoidance. A real jump needs its own rules specification.

## 2. Compatibility and authority

- New runs use `release-rules-4`. Catalog retains V1/V2/V3 and MVP. No existing checkpoint is converted. V4 inherits V3 enemy definitions, Bomber contact-fuse behaviour, economy, maps, waves and perks.
- Core remains integer/fixed-step at 0.05 s, 1200 position units/tile. Core owns movement, targets, attack acceptance, contact, damage and death. Presentation never moves Core or decides a hit.
- V1/V2/V3 commands, movement/collision, timing and outcomes remain unchanged. New V4 direction encoding must be explicit; existing `new HeroCommand(...)` callers keep their 16-heading meaning.
- V4 snapshots include motor velocity and attack phase/start/contact/ready ticks plus committed facing. Rendering does not change snapshots. Equal command streams and initial state produce equal results.
- Checkpoint schema remains 2 with optional V4 velocity fields. V4 Start Night checkpoints additionally store motor velocity so a moving Day checkpoint replays the same Night. New fields are read only for V4; old profiles ignore them. Validate velocity against the V4 maximum and reject malformed values; missing fields mean rest. Night start clears pending attack input while velocity and position continue. Save/Quit during Day still restores spawn at rest. No contact/attack phase needs mid-night persistence.

## 3. V4 motor and scale

Geometry and numeric values are provisional [P]; ownership, stepping, collision, compatibility and rounding are invariant [I]. Initial values:

| Property | V4 initial proposal | Purpose |
|---|---:|---|
| Hero height | about 0.22 tile / 1.76 m | Preserve existing world scale |
| Body radius | 72 units = 0.06 tile / 0.48 m | Replace the 2.4 m invisible clearance |
| Maximum velocity magnitude | 42 units/step = 0.7 tile/s / 5.6 m/s | A run, explicitly not a walking animation |
| Velocity change toward desired velocity | at most 14 units/step per step | Reach top speed from rest in about 150 ms |
| Braking toward zero without movement input | at most 21 units/step per step | Stop from top speed in about 100 ms |
| Attack desired-speed multiplier | 50% during wind-up through recovery | Give a planted strike without taking all control away |

- Camera-relative input quantizes to 64 headings for V4. Use a checked-in integer direction table, no runtime trig in the V4 motor. Normalize diagonals; magnitude must never exceed the profile cap after integer rounding. Legacy tables and conversion remain intact.
- Desired velocity is heading times speed, rounded toward zero. Each tick approach that vector by a Euclidean delta no larger than the acceleration cap; use deterministic integer length/normalization, ceiling integer square root for normalization denominators and division toward zero, with explicit zero-vector handling. Clamp rounded desired/output vectors to the speed cap using the same method. Braking uses its separate cap. Reversing direction follows acceleration; it does not instantly flip velocity.
- Resolve X then Z using the largest legal partial displacement per axis, against solid cells, terrain and board edge. A blocked component becomes zero to prevent pressure accumulating against walls; the free component slides. Never cross touching blocked diagonal corners. Units still pass through each other and never reserve route cells.
- Prediction uses the exact same pure integer tick solver on a copy, starting at the latest authoritative state. Predict a full next state, then interpolate toward it by the remaining fraction. It must not use a different fractional collision rule. No production-only hooks to make tests pass.
- Input begins visible motion in the first rendered frame with a positive frame interval. Normal displayed root error must stay within one maximum-speed step (42/1200 tile); correction completes within 0.1 s. Teleport/reset/respawn reset history immediately. Camera follows the stable displayed root, never feet bob, weapon recoil or local attack offsets.
- Spawn, build-over relocation, Day clock, full HP at dawn and death/respawn rules otherwise remain as §17. Reset velocity on relocation, respawn, dawn, new/restart and ordinary Day-save load; retain velocity at ordinary view switches. V4 Start Night checkpoint replay is the explicit load exception: restore validated saved velocity after the initial reset so it matches the captured start.

## 4. V4 contact-phase melee

One deliberate sweep. Initial [P] values: damage 10; centre-to-centre reach 240 units = 0.20 tile / 1.6 m; arc 100°; wind-up 3 steps (0.15 s); contact once; total start-to-next-start interval 15 steps (0.75 s). Recovery lasts from contact until ready. Keep HP 200 and respawn 10 s. V4 attacks are accepted only during Night; Day advances movement only, with no damaging or cosmetic swing acceptance and no buffered Day click carried into Night.

- At the hero attack phase of tick T, an alive ready hero may accept one attack. Commit aim facing from that command. Emit accepted/start event with tick, facing and origin. Contact is due at T+3; next acceptance no earlier than T+15. A held button repeats at readiness. Retain one tap for up to 0.25 s of advancing, unpaused gameplay time (five Night steps); consume it on acceptance. Additional taps replace the pending tap rather than queue combos. Clear pending taps on leaving Hero view, Day/Night transition, menu/focus loss, death and run boundaries. An accepted attack survives a view switch as specified below.
- Facing is committed at acceptance; moving the mouse during wind-up does not redirect the hit. Hero may move at the attack motor cap; contact origin is the hero's authoritative position at contact. No auto-lunge, target magnetism, root-motion displacement or tracking rotation.
- At T+3, after hero/enemy movement and building attacks, resolve living enemies currently within reach and arc in ascending ID. Damage each at most once. An enemy killed by a tower earlier that tick is not a victim. Contact of an attack accepted on a prior tick is processed before accepting any new attack; recovery prevents a same-tick second contact.
- V4 melee cannot hit through solid buildings or impassable terrain. Use deterministic segment traversal between the hero and enemy centres; any blocking cell touched by the segment, including corner contact, blocks it. Endpoints in walkable cells are allowed. No Unity physics query decides this.
- Emit contact even on a miss, and actual hit events only for actual victims. Visual pose, victim reaction, impact sound and death use the contact event in the same rendered update. Remove the old extra 0.08 s delay for V4 only. Visual miss has whoosh but no impact or hit flash.
- Pause freezes attack ticks. Death cancels an unlanded attack and any buffered tap. Dawn, Night start, reset, relocation and load clear attack state. Switching view does not cancel accepted wind-up, reset cooldown or generate an extra attack; guard takes over only when ready. Menu/focus loss clears unaccepted input but does not delete an accepted authoritative attack.
- Guard holds position and chooses nearest eligible enemy in reach, ties lowest ID; starts and resolves the same V4 attack. It does not retarget committed facing during wind-up. Its motor brakes toward rest under normal V4 braking rather than snapping velocity on a view change.

## 5. Enemy interaction

Retain cell-centre route decisions, building-target stickiness, Archer provocation, bosses/Bombers ignoring the hero, aura rules and absence of unit avoidance. For V4 melee-versus-hero only: engage within 0.20 tile; hold within 0.35; hit reach 0.20. Acquisition reach always equals hit reach, so an enemy cannot newly acquire while already unable to hit. After a hero retreats beyond hit reach but remains within hold reach, the enemy deliberately waits as before; moving beyond hold releases it. These are initial [P] values; do not reuse V4 constants for old profiles. Enemy attack cadence remains unchanged. This reduces invisible long-range hits without turning this milestone into an enemy navigation rewrite.

Hero attack reach and enemy engagement must be validated together. A hero close to a route centre must be engageable; a hero beside an enemy attacking a building can damage it without forcing that enemy to switch. Walking through units remains a documented tactical simplification; full body separation is deferred.

## 6. Camera and input lifecycle

- Owner decision: **pull inward first; lift only if the close pose still cannot clear the obstacle**, max lift 15°. Never implement lift-first. Retain look polarity, invert option, FOV 50–90, distance setting and crosshair.
- Protect the near-plane volume, shoulder pivot and final rendered transition pose against presentation-only blockers matching building/terrain silhouettes. Exclude hero, enemies, effects, overlays and triggers. Blockers never affect simulation collision/routing.
- Respond inward immediately when necessary for safety; release outward with time-based damping. Initial release time constant 0.15 s; settle to within 5% in 0.5 s. No repeated inward/outward corrections at a stationary corner. Evaluate final pose after smoothing so damping cannot put it back inside geometry.
- If the pivot starts obstructed or no legal interpolated path exists, use a validated close fallback or safe cut. Never enforce a minimum distance that overrides clearance. Screen fade of only the hero mesh may help very close views but cannot hide scenery clipping.
- Preserve last orbit within a run; reset on new/load/restart. Enter transition ≤0.35 s, skippable and interruptible. Exit deliberately cuts to retained strategic framing. Both work paused and maintain existing pause/speed semantics; no transition is allowed to advance Core.
- Focus loss unlocks cursor, clears look delta and unaccepted attack input, and suspends advancement through an explicit focus gate. Focus return shows an uncaptured state; the first click recaptures and is consumed, so it cannot attack. Do not overwrite the player's explicit pause flag. Returning from a menu uses the same consumed recapture click. No camera head bob; shake remains optional and off by default.

## 7. Animation, contact and presentation

Use a hero-specific adapter; retain legacy hero and enemy presentation. Rig must have articulated elbows/wrists/ankles, feet that can plant, a stable sword grip and shield pose. A hero-specific extension of generated assets is acceptable if it meets the gate; a new skeletal workflow is not mandatory. No paid asset or dependency assumption.

Minimum finished set: idle; forward/back/left/right locomotion with diagonal blends; start/stop; moving turns and idle turn; one wind-up/contact/recovery slash; additive hurt; down; rise. Bind local signed velocity and actual distance travelled. Lower body follows travel while upper body aims within a limited twist; larger rotations turn the hips/feet. Holding attack between swings cannot force a forward gait while moving sideways.

Wind-up starts on authoritative acceptance (within one 50 ms step of input); an optional immediate ready gesture has no fake contact. Blade/contact marker aligns with T+3. Select the visual primary victim from actual contact victims by minimum squared distance at contact, ties lowest ID. The blade must plausibly reach that victim inside physical reach; secondary victims read as the same sweep. Never extend gameplay reach to fix art. No multi-metre visual step-in. Local displacement remains ≤0.02 tile, cosmetic and collision-safe; camera ignores it.

Death/reset/relocation win over attack and hurt. Pause freezes pose, footfalls and combat cues. No footfall while blocked/stationary; no leftover slash at a former respawn position. Recoil and hurt do not erase the attack's authoritative committed orientation.

Keep low-poly visual direction. Remove the permanent oversized filled attack sector for V4; use a restrained short-lived ground/weapon sweep at true reach on an accepted attack. Keep existing tactical indicators for legacy profiles. Preserve crosshair, hero/Keep HP, offscreen Keep danger, entry warnings and view controls; reduce irrelevant Day/economy emphasis during Hero Night without removing critical information.

Dedicated hero swing/impact/hurt/down/footfall families; world-space playback with short-distance attenuation, UI/music 2D. Footfalls follow contact markers and actual locomotion, with bounded voice counts. Preserve audio reset, pause and threat priority. Surface variation can start with one land family; do not invent traversable terrain types.

## 8. Quality and balance gates

Record matching before/after motion at 720p and 1080p, seed/profile/settings/build identified. Include starts/stops/reverse, strafe/backpedal while attacking, 180° aim change, wall/corner slide, Keep/tree/rock camera obstruction, view transition, miss/near/edge/maximum-range/lethal/multiple hits, paused wind-up/contact, focus/menu return, death/respawn and building relocation. Check camera volume at min/max FOV and zoom. No visible foot skating during a planted phase, pose popping or unmotivated camera pumping. Technical contact cues must align to the same contact update; visual acceptance still needs owner motion/listening review.

Fresh affected tests first; full EditMode/PlayMode after shared profile/save integration. Protect concrete failures: V4 acceleration/brake/direction cap, wall prediction divergence, exactly-once contact and committed aim, cancellation, blocking geometry, V3 outcome compatibility and moving Start Night replay. Prefer EditMode for Core, PlayMode for input/camera/pose lifecycle; extend nearest fixtures. No tests for arbitrary tuning constants or screenshot equality.

Balance: compare V3/V4 with the same seeds 1–8 and kill-zone/corridor/economy policies, idle/active/no-build hero. Hero-only must still lose by Night 3; log hero share over Nights 6–12 (target ≤25% average per approach), win/loss, Keep damage, hero deaths and missed contacts. V3 is the relevant current control, not V1. Do not require V4 wins equal V3 after intentional mechanics changes. Confirm the active script actually approaches the smaller reach and waits for contact; a stale script is not balance evidence.

Bounded V4 tuning: at most three recorded candidates. HP/damage/interval/respawn within ±25% of the proposal (whole ticks/damage); radius 0.05–0.075 tile; reach 0.175–0.225; max velocity 36–48 units/step. Acquisition reach equals enemy hit reach; hold remains greater. Preserve wind-up ≥2 ticks and physical contact readability. Never retune waves/enemies/towers to force a gate. If the three candidates fail or the hero cannot intercept meaningful threats, record the result and seek a design revision rather than claim success.

Visible-build stress: same-session paired strategic/hero 100-enemy measurement at both resolutions on the reference machine, Editor closed; both ≥60 fps, hero frame time ≤1.5× strategic. Report median, p95, p99 and hitches plus CPU/GPU/API/build/fixture/rules version. Verify the fixture actually exercises V4 and the new hero; legacy stress metadata alone does not pass. Reuse existing capture/perf tooling where adequate.

Definition of done: new profile playable with legacy saves intact, focused/full checks passing, matched evidence and balance/performance results recorded, and owner review of live motion and listening passed. If owner review is unavailable, report technical implementation complete and human acceptance pending. Do not equate generated assets with approved AA-quality art.
