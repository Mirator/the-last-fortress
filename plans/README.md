# Implementation plans

Prepared on 2026-10-15 using the improve skill. These plans are implementation handoffs; a document marked DONE is not evidence that gameplay has been implemented.

## Baseline

- Repository root: `C:/Users/Miroslav Pavelek/Documents/Games/the-last-fortress`.
- Unity project: `game/The Last Fortress/`.
- Editor: `6000.3.16f1`; installed executable verified at `C:/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe`.
- Source-control state when planning: unborn `main` branch, no HEAD commit; `game/` and `specs/` are untracked. There is no commit SHA against which to run a drift diff. Compare the recorded files and specifications against the working tree before execution. Do not create a baseline commit or discard files without user authorization.
- Only template scripts and `SampleScene.unity` were found; no gameplay tests or verified gameplay build command exist yet.

## Execution order and status

| Plan | Deliverable | Priority | Effort | Dependency | Status |
|---|---|---|---|---|---|
| [001](001-navigation-prototype.md) | One-wave navigation prototype, tests, and Windows build | P0 | L, multi-day | None; owner decisions recorded in specs | DONE (mechanical gates, [results](001-navigation-prototype-results.md)); playtest review pending |

Execute plan 001 first. Six-night MVP implementation follows successful prototype playtesting and requires its own implementation plan against `specs/003-six-night-mvp.md`. Full-release targets in the GDD are not immediate tasks.

## Read order

1. `specs/001-game-design.md`: overall vision and milestone boundaries.
2. `specs/002-navigation-prototype.md`: exact first-milestone rules and acceptance fixtures.
3. `plans/001-navigation-prototype.md`: ordered implementation, allowed files, verification, and stop conditions.
4. `specs/003-six-night-mvp.md`: future scope and rules; do not implement yet.

Owner confirmed: specifications/plan only in this task; follow any open route before breaching; Keep base income in the later economy; defer soldiers beyond MVP. Tuning defaults are explicitly provisional.

## Findings disposition

- Scope contradictions, navigation policy, placement, combat definitions, phase transitions, economy safety, wave planning, and undefined supporting mechanics are being addressed in the revised specifications.
- NavMesh migration, ECS, multiplayer, paid assets, generic manager frameworks, and dependency upgrades are not planned: none is needed to answer the prototype's gameplay question.
- Balance, fun, and the production schedule remain playtest-dependent. Specification completeness does not validate those claims.

## Status meanings

TODO, IN PROGRESS, DONE, BLOCKED (with reason), or REJECTED (with reason). DRAFT means the plan has unresolved design inputs and must not yet be executed.
