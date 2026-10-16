# Graphics and motion review

2026-10-15. Read-only review for the 007 improvement specification. No game, Editor, or UI was launched or controlled. The screenshots below are saved historical captures from plan 005, not fresh captures; they establish still-image composition only.

## Ranked findings

### P1 — Preserve a visible hit and death cue for lethal Tower shots

**Evidence:** `WorldView.Sync` handles `TowerShot` by calling `SpawnArrow` ([`WorldView.cs:264-265`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Unity/WorldView.cs)); `SpawnArrow` immediately returns when `GetEnemy` returns null ([`WorldView.cs:445-449`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Unity/WorldView.cs)). In Core, the shot event and damage are emitted in `TowerAttacks`, and dead enemies are removed from `enemiesById` at the end of the same fixed step ([`RunSimulation.cs:895-900`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Core/RunSimulation.cs), [`RunSimulation.cs:995-1003`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Core/RunSimulation.cs)). The view drains events only after `Advance` returns, so a lethal shot's target lookup is already null; its arrow is skipped, and the enemy view is removed in the same sync ([`WorldView.cs:255-275`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Unity/WorldView.cs), [`WorldView.cs:364-372`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Unity/WorldView.cs)). **Source-confirmed**, not dependent on a screenshot.

**Desired behavior:** A lethal hit should still read as a hit and a kill. Use the event's recorded cell or retained impact position to play a very short, pooled impact/death cue; keep the existing arrow for nonlethal shots. Avoid delaying simulation cleanup or changing damage timing.

**Lean acceptance:** force a Tower hit to kill a one-hit target and confirm the shot still gets a brief impact/death cue after the Core has removed the target; confirm nonlethal shots keep their current travel cue. A bounded pooled cue keeps the current primitive/shared-material approach.

### P2 — Give moving enemies a small locomotion cue

**Evidence:** `SyncEnemies` interpolates position and sets facing, but no limb, weapon, body, or gait animation is applied ([`WorldView.cs:334-362`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Unity/WorldView.cs)). In the saved 1280×720 Night 4 capture, enemies remain small across the board and their silhouettes are recognizable but read as rigid pieces (`plans/screenshots/005-mvp/1280x720-mvp-07-night4-combat.png`). In the Golem still, its larger body and pale material separate it from Grunts (`plans/screenshots/005-mvp/1280x720-mvp-08-night6-boss.png`). **Screenshots establish scale and silhouette only; lack of animation is source-confirmed.**

**Desired behavior:** Add a restrained procedural step/lean that follows movement progress, with the Brute/Golem conveying heavier steps and the Runner a quicker posture. Keep the animation inexpensive, deterministic from simulation position/time, and grounded at cell centers; do not alter routing or speed.

**Lean acceptance:** at default board zoom, moving Grunts visibly advance rather than slide, while stationary attackers stop their gait; Runner/Brute/Golem motion remains distinguishable and doesn't obscure route, HP, or trap feedback. Check at 1280×720.

### P2 — Make Tower fire and enemy contact easier to read

**Evidence:** The only visible TowerShot response is a small cube arrow travelling for 0.18 seconds on a fixed arc ([`WorldView.cs:445-479`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Unity/WorldView.cs)). Enemy attacks set a building flash timer, which scales the building by at most 6% over 0.18 seconds ([`WorldView.cs:267-269`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Unity/WorldView.cs), [`WorldView.cs:314-320`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Unity/WorldView.cs)). Saved Night 4 and Night 6 captures show combat concentration around clustered Towers, but are still frames and cannot establish whether these cues read in motion (`plans/screenshots/005-mvp/1280x720-mvp-07-night4-combat.png`, `1280x720-mvp-08-night6-boss.png`). **Cue inventory is source-confirmed; perceived clarity is unverified.**

**Desired behavior:** Keep the current simple arrow, and give a successful hit a short, high-contrast impact flash or distinct hit response; ensure an attacked structure visibly reacts without looking as if its footprint or health changed more than it did. Reuse event-driven pooled primitives/materials; preserve damage and targeting rules.

**Lean acceptance:** in a representative Tower cluster at default zoom, a viewer can follow fire and identify when a target takes damage, while separate simultaneous shots remain legible and effect counts stay bounded by existing combat activity. Check both normal speed and pause.

### P2 — Review cosmetic clocks against Pause

**Evidence:** `PrototypeBootstrap.Update` calls `SyncViews(dt)` even when `Sim.Advance(dt)` does not advance because the run is paused ([`PrototypeBootstrap.cs:128-142`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Unity/PrototypeBootstrap.cs)). `WorldView.Sync` continues arrow motion using that `dt`, and burn flames scale from `Time.unscaledTime` ([`WorldView.cs:273-277`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Unity/WorldView.cs), [`WorldView.cs:360-361`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Unity/WorldView.cs)). Core pause halts its fixed steps ([`RunSimulation.cs:402-421`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Core/RunSimulation.cs)). **Source-confirmed; no pause screenshot or motion capture was available.**

**Desired behavior:** Freeze in-flight combat cues on Pause, or make any continuing cosmetic motion clearly non-simulative and deliberate. Camera input may remain live so players can inspect the board while paused.

**Lean acceptance:** pause mid-flight with an arrow and a burning enemy visible; Core state, arrow position, and any hit/death effects hold consistently until Resume. Camera pan/zoom still works.

### P3 — Soften the day/night lighting cut

**Evidence:** `ApplyLighting` immediately swaps ambient light, sun color/intensity, and Keep light when the state crosses Day/Night ([`WorldView.cs:482-507`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Unity/WorldView.cs)); `Sync` only calls it after the night-look boolean changes ([`WorldView.cs:273-277`](../../../game/The%20Last%20Fortress/Assets/TheLastFortress/Runtime/Unity/WorldView.cs)). The saved 720p Day 2 feedback shot shows clear route cyan, coral NEW warning and pale terrain against the daytime palette (`plans/screenshots/005-mvp/1280x720-feedback-zoomed-new-side-marker.png`); the saved night captures show a cool environment with visible enemies and amber Keep/Tower focal points. **Palette and trigger are source-confirmed; whether the abrupt transition feels jarring cannot be established from stills.**

**Desired behavior:** If the transition is visually abrupt in a live review, interpolate only presentation lighting over a short bounded interval. Keep night enemies, routes, HP bars, and warning colors readable throughout; no new lighting package or per-building shadow lights.

**Lean acceptance:** inspect one Day-to-Night and one Night-to-Reward transition at normal zoom; no bright/dark pop, flicker, or loss of enemy/route contrast, and the transition uses no unbounded lights or effects.

## Strengths and evidence limits

The stills support the current simple visual hierarchy: connected Towers/Keep remain the focal cluster, the pale Golem silhouette stands apart, the board and route/warning colors stay distinct, and the latest feedback capture shows the new-side marker while zoomed in. Plan 005 reports a passing historical stress fixture (100 enemies, p95 2.966 ms on its reference machine); these proposals should remain procedural, pooled, and bounded rather than add a dependency or large art pass.

The 005 screenshots are historical captures. Some were taken before final menu/HUD feedback tweaks, and the only later feedback screenshots are Day views. They cannot verify animation, flicker, pause behavior, hit readability, day/night transition smoothness, or current 720p combat HUD layout. I did not launch the game or Editor, and did not change code, tests, assets, or the existing improvement spec.



