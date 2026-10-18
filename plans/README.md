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
| [007](007-complete-game-experience.md) | Complete player experience: audio, opening minutes, consistent UI and world, escalation, replayability and a stable Windows package | P1 | L, staged | Implemented 006; source baseline `dc8b55a` | TECHNICAL DONE ([results](007-results.md)): EditMode 92/92, PlayMode 16/16, MVP build, offscreen renders at 720p/1080p, 1× Night 4/6 audio stats, leak cycles. Listening, live motion review and Stage E players PENDING |
| [008](008-aa-gameplay-and-production-slice.md) | AA gameplay foundation: twelve nights, nine buildings, seven enemies/two bosses, 24 perks, three tactical map profiles and one finished production slice | P1 | L, multi-month | 007 technical baseline; close audio/live/player gates before content production | TECHNICAL DONE ([results](008-results.md)): rules/matrix/diagnostic, release profile, six mechanics, 12 nights, 3 maps, 24 perks, legacy-safe saves, validator, `Builds/Release/`; EditMode 105/105, PlayMode 18/18. G0 player, G3 blind, G4 art/audio/live/visible-FPS/participants PENDING |
| [009](009-aa-content-production-and-release.md) | AA edition: scoped content production, final art/audio, accessibility, progression, compatibility, performance, QA and release package | P1 after 008 | L, multi-month | Passed 008 slice, complete campaign, rules/catalog and measured throughput | PLANNING ONLY ([results](009-results.md)): production contract and content contracts drafted, NOT frozen; production gate NOT passed; all content/release work PENDING until the 008 slice is validated |
| [010](010-board-readability.md) | Board readability: entries rotate with the map, profile rules checked on the played board, per-run masked decoration, world-space entry gates and route direction | P1 | M, ~2–3 days | Owner solo playtest 2026-10-18 | Stages 1, 2.2 and 3 DONE ([results](010-board-readability.md#results)): entry gates, route chevrons, label beside gate; plus Night speed control. EditMode 105/105, PlayMode 20/20, release build, 1080p captures. Live motion look PENDING |

## Latest handoff — 008 implemented, 009 planned (2026-10-16)

Plan 008's technical milestone is implemented; see [008 results](008-results.md), the frozen [rules](008-rules.md), [content matrix](008-content-matrix.md) and [art/audio bible](008-art-and-audio-bible.md). Fresh run after the last change: EditMode 105/105, PlayMode 18/18, release content validation 0 errors, release and MVP builds succeeded, offscreen captures at 720p/1080p, MVP regression capture. Deliverable: the whole `game/The Last Fortress/Builds/Release/` folder; old six-night saves continue in it.

Plan 009 is **planning only** by its own entry rule: [production contract](009-production-contract.md) and [content contracts](009-content-contracts.md) are drafts with owner decisions left open; [009 results](009-results.md) lists what was deliberately not started.

Next actions, all needing people or the owner, in order: (1) 007 listening pass, live motion review and five new players; (2) 008 G0/G3 sessions on the release build across Ravine, Crossing and Meadow, recording which approaches players use and lose with; (3) 008 G4 decisions: art/audio direction and budget (purchasing/commissioning needs authorization), one finished building/enemy/encounter with measured time, listening approval, live and visible-window measurements, five returning + five new participants; (4) the 009 launch-scope and capacity decisions. Do not start 009 content before (3). Nothing is committed.

## Earlier handoff — 007 (implemented 2026-10-16)

Plan 007's technical milestone is implemented; see [007 results](007-results.md). Fresh run after the last change: EditMode 92/92, PlayMode 16/16, MVP build succeeded, offscreen renders at 1280×720 and 1920×1080, Nights 4 and 6 at 1× with audio voice statistics, five leak-check cycles. Still PENDING: listening to the synthesized audio, live motion review on a visible window, and Stage E with five new players ([protocol](evidence/007-experience/playtest.md)). The content-scope decision (3.6) depends on Stage E. The plan document's own status line is historical, as for 003–006; this index is authoritative. Nothing is committed.

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

## Previous follow-up handoff — 006 (implemented)

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
