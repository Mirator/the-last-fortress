# Gameplay immersiveness review

Date: 2026-10-15. Scope: review of the six-night MVP's combat feedback, player agency, danger/escalation, pacing, and reward relevance. No game code, tests, or specification files were changed. No desktop automation, Unity Editor, or game window was used. Findings combine source inspection with historical saved captures in `plans/screenshots/005-mvp/`; those captures predate some final HUD fixes documented in `plans/005-results.md`. They are not a fresh playtest and cannot establish player comprehension or enjoyment.

## Prioritized findings

### P1 — Make damage to the fortress legible as an event, not only a changing bar

**Observation:** In `Runtime/Unity/WorldView.cs:257-269`, the view reacts to `EnemyAttacked` by setting a generic building flash timer. `Runtime/Unity/WorldView.cs:314-320` renders that as a brief, small scale pulse. The Keep gets camera shake when its HP drops (`Runtime/Unity/PrototypeBootstrap.cs:158`, `Runtime/Unity/CameraRig.cs:29-33`), and the HUD continuously shows Keep HP (`Runtime/Unity/HudView.cs:302-305`). I found no damage amount, attacker identity, or localized alert in this event path. The saved Night 4/Night 6 captures show multiple small units and structures competing around the Keep; the 005 results separately note that a Tower can partly occlude the Keep.

**Interpretation / hypothesis:** A player watching the wider battlefield may notice HP loss without understanding which enemy or structure caused it, especially when damage goes to a frontline building. That weakens the link between placement decisions and battle consequences. This is a source-backed concern, not a demonstrated usability bug; the screenshots do not show motion or reactions.

**Proposed behavior for the existing MVP:** Add a brief, readable visual cue at the damaged building, with a stronger distinct cue when the Keep is hit and a clear destroyed-building cue. Include a compact damage/attacker label only if the cue alone does not communicate the event. Keep the cue visual so attacks remain understandable without audio; do not add new enemy types, abilities, or combat rules.

**Acceptance:** In a rendered Night 4 and Night 6, at default zoom and while the camera is panned away from the Keep, an uncoached observer can identify the first Keep hit and a destroyed frontline building within a few seconds. Check both 1280×720 and 1920×1080; verify cues stay attached to the correct target through panning and do not obscure enemy health/boss UI. Treat this as a small-sample usability check, not a fun claim.

### P2 — Give each cleared night a short consequence recap

**Observation:** The Reward state headline says the night was cleared and asks for one reward (`Runtime/Unity/HudView.cs:330-333`); its cards explain the selected perk. The terminal summary reports run-wide kills, purchases, gold, and synergies (`Runtime/Unity/HudView.Mvp.cs:228-235`), while defeat identifies the night, elapsed time, and living attackers. There is no per-night recap of losses or defensive performance before the next build decision.

**Interpretation / recommendation:** A compact recap would help players connect a night's outcome to the next day's layout and perk choice, strengthening the build-defend-recover arc without adding gameplay systems. This is an experience recommendation, not a defect established by the source or simulations. Avoid turning it into a detailed combat log.

**Proposed behavior:** On the existing Reward and Defeat screens, show a few authoritative facts for the just-finished night: enemies defeated, Keep HP lost, and buildings destroyed (if any). Victory can keep the existing run summary. Do not add a night score or change rewards/balance.

**Acceptance:** For a clear night, a night with a building loss, and a defeat, each displayed number matches the simulation event/state totals. In a small fresh-player check, at least four of five can state one concrete consequence of the preceding night before making the next build/reward decision. No claim about outcome causality until players can explain what placement or enemy behavior produced it.

## Existing strengths and limits

The six-night arc already has useful structure: the Day preview identifies tonight's mix and directions; new entry sides are marked; Brutes and the Golem have distinct silhouettes; the Golem receives a named HP bar once active; and reward choices pause combat between nights. The 005 results report that the boss and Brutes read at default zoom. These are good supports for anticipation and escalation, and there is no evidence here that the MVP needs more content.

The Core simulations in `plans/evidence/006-playtest-a/` and `006-playtest-b/` establish deterministic campaign outcomes and simulated combat durations only. They cannot answer whether nights feel tense, whether automatic combat feels passive, whether combat time feels slow, or whether players notice and learn from losses. In particular, 54–68 simulated seconds for Night 6 is not wall-clock session time or a pacing verdict. Keep the five-new-player validation gate in `specs/003-six-night-mvp.md` open and collect these questions there before considering speed controls or additional combat agency.
