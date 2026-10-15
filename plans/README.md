# Implementation plans

Prepared on 2026-10-15 using the improve skill. These plans are implementation handoffs; a document marked DONE is not evidence that gameplay has been implemented.

## Baseline

- Current planning baseline for plans 003-005: commit `4855f69` (`Add one-wave navigation prototype with specs, plans and design`), checked 2026-10-15. Root `AGENTS.md` contains the lean-testing policy; preserve it even if still untracked when execution begins.
- Repository root: `C:/Users/Miroslav Pavelek/Documents/Games/the-last-fortress`.
- Unity project: `game/The Last Fortress/`.
- Editor: `6000.3.16f1`; installed executable verified at `C:/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe`.
- Historical baseline for plan 001 was an unborn branch/template project. It is now implemented and committed; do not reuse that historical assumption when executing 003-005.
- Prototype exists under `Assets/TheLastFortress/`. The four PlayMode input failures recorded at planning time were a test-harness focus defect, fixed in plan 003 (see `003-results.md`). Latest fresh results after plan 005: EditMode 89/89, PlayMode 13/13. Playable MVP build: `game/The Last Fortress/Builds/Mvp/TheLastFortress.exe`.
- Follow-up plan 006 was prepared against `cea7c4f` on 2026-10-15 from specification 004 and five agents' simulation/review evidence. Its planning task did not rerun Unity tests or build; the preceding counts remain historical. The owner's no-desktop-interaction constraint persists.

## Execution order and status

| Plan | Deliverable | Priority | Effort | Dependency | Status |
|---|---|---|---|---|---|
| [001](001-navigation-prototype.md) | One-wave navigation prototype, tests, and Windows build | P0 | L, multi-day | None; owner decisions recorded in specs | DONE (mechanical gates, [results](001-navigation-prototype-results.md)); playtest review pending |
| [003](003-mvp-economy-foundation.md) | Economy, multi-cell buildings, traps, three-night foundation | P0 | L | Implemented prototype; verification preflight included | DONE (mechanical gates, [results](003-results.md)); playtest pending |
| [004](004-six-night-combat-and-perks.md) | Four enemy types, six nights, deterministic rewards and ten perks | P1 | L | 003 verified | DONE (mechanical gates, [results](004-results.md)); playtest pending |
| [005](005-replay-saves-and-visual-pass.md) | Seeded maps, checkpoints, replay/menu, connected rotating Walls, simple art/HUD, final performance | P1 | L | 004 verified | DONE (implementation + mechanical gates, [results](005-results.md)); fresh-player playtest PENDING |
| [006](006-playtest-improvements.md) | Effective HUD/reward values, lethal-shot and paused effects, progress guards, nightly recap, motion/layout and defensive-strategy investigation | P1 → P2 | L, staged | Implemented 005; spec 004 | DONE ([results](006-results.md)): EditMode 91/91, PlayMode 15/15, MVP build and rendered checks at 1280×720/1920×1080; real-speed gait and Night 4/6 density not judged by eye |

Execute 003 → 004 → 005 in order. There is no implementation plan 002; `specs/002-navigation-prototype.md` and `specs/003-six-night-mvp.md` are specifications, not missing execution steps. Do not rerun implementation 001 or build the full-release content list.

For the current follow-up, start 006 directly; 003–005 are already implemented. Plan 006 stages order shared economy/event/save contracts before presentation polish and the adaptive-defense comparison. Control help and lighting blends are conditional; balance tuning is a recommendation/decision after evidence, not an automatic patch. Desktop-dependent checks remain pending until an allowed environment is available.

The owner authorized proceeding with this technical MVP sequence while away; the earlier fresh-player gate remains an unfulfilled validation task, not a reason to request routine approval between these plans. Record it honestly and leave a playable build/checklist. Do not claim fun, two viable strategies, or release readiness without corresponding evidence.

## Read order

1. `specs/001-game-design.md`: overall vision and milestone boundaries.
2. `specs/002-navigation-prototype.md`: exact first-milestone rules and acceptance fixtures.
3. `plans/001-navigation-prototype.md`: ordered implementation, allowed files, verification, and stop conditions.
4. `specs/003-six-night-mvp.md`: MVP rules implemented incrementally by plans 003-005.
5. Root `AGENTS.md`, then 003, 004, 005 and their predecessor results when available.

Owner confirmed: this task writes plans only; the future implementing agent may execute the complete sequence. Retain open-route-first breaching, base Keep income, and no soldiers in MVP. Bounded tuning is allowed with evidence; simple low-poly/HUD work plus cosmetic wall auto-connections/manual building rotation is included in 005. Minimal changes to existing tests require a demonstrated harness defect; keep coverage lean and preserve behavioral assertions.

## Current follow-up handoff

> Read root AGENTS.md, specs/004-agent-playtest-improvements.md and plans/006-playtest-improvements.md. Implement plan 006 in stages, preserving existing prototype/MVP rules and user saves. Use the existing tests and add only focused protection for concrete changed risks. Follow the owner's no-desktop-interaction constraint: background Core work is allowed, and closed-project noninteractive Unity commands may run; do not launch/control visible game or Editor windows or start a second Editor for an open project. Record unavailable verification as pending. Do not automatically change balance, add dependencies, commit, push or publish. Write plans/006-results.md with fresh evidence and limits, and update this index. Do not rerun implementation 003–005.

This is a future handoff; the request to make the plan did not authorize starting its implementation.

## Historical MVP-sequence handoff (already implemented)

> Read root AGENTS.md and plans/README.md. Implement plans/003-mvp-economy-foundation.md, then plans/004-six-night-combat-and-perks.md, then plans/005-replay-saves-and-visual-pass.md in this same chat. Read each plan fully and check the current code before starting. Preserve the existing prototype and user work. Start by investigating the recorded four failing PlayMode input/UI checks; do not assume the older passing report is current. Make only necessary focused tests; existing-test corrections need demonstrated harness defects. Follow each plan's bounded tuning and cosmetic rotation rules. Finish and verify each stage, write plans/003-results.md, 004-results.md, and 005-results.md as applicable, update this index, and continue automatically to the next passing stage without routine approval. If a real blocker persists after the specified attempts, document the exact cause, preserve the last playable state, and stop dependent work; do not invent requirements or weaken tests. Do not commit, push, publish, buy assets, add dependencies, or contact other people. At completion provide the Windows build path, concise change summary, verification evidence, and remaining human playtest actions. Do not start full-release features.

This prompt is a handoff; no agent has been dispatched by the planning task. The work is substantial and may take multiple sessions, so each results file must record the last verified step and next action for continuation.

## Findings disposition

- Scope contradictions, navigation policy, placement, combat definitions, phase transitions, economy safety, wave planning, and undefined supporting mechanics are being addressed in the revised specifications.
- NavMesh migration, ECS, multiplayer, paid assets, generic manager frameworks, and dependency upgrades are not planned: none is needed to answer the prototype's gameplay question.
- Balance, fun, and the production schedule remain playtest-dependent. Specification completeness does not validate those claims.
- Plan 006 consolidates the subsequent agent findings; do not create duplicate plans for effective stats, reward-income preview, lethal shots, pause timing, restart/exit or recap feedback.
- Old income-source wrapping, absent building descriptions/new-side markers and coplanar water are superseded by owner-feedback fixes in 005. Preserve current compact income/receipt text; do not reintroduce those issues as new work.
- Existing desktop capture timeouts are tooling limitations, not game defects. Agent source/screenshot reviews do not satisfy fresh rendered or human validation.

## Status meanings

TODO, IN PROGRESS, DONE, BLOCKED (with reason), or REJECTED (with reason). DRAFT means the plan has unresolved design inputs and must not yet be executed.
