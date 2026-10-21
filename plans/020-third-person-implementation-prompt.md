# Prompt: implement the third-person production slice

Planning deliverable, 2026-10-20. Paste the following into the implementing chat when ready. No implementation was dispatched by the audit task.

---

Implement the third-person production slice in `C:/Users/Miroslav Pavelek/Documents/Games/the-last-fortress`.

Read root `AGENTS.md`, `specs/README.md`, `specs/001-vision.md`, `specs/002-game-rules.md`, `plans/018-third-person-spec-proposal.md` **v0.1**, and `plans/019-third-person-production-slice.md` in full before editing. The audit context is `plans/017-third-person-audit.md`. Follow the implementation plan in order and verify the live code against its `adac6b4` baseline, preserving unrelated user work.

The owner explicitly allowed movement/combat redesign and chose **camera pulls inward first, lifts only when needed**. The spec supplies the initial proposed values and bounded tuning for this task. Proceed with those recommendations under this implementation request; do not ask again about these already resolved directions. Raise only new material contradictions, out-of-scope requirements or failures beyond the specified tuning bounds.

Register the feature spec under `specs/` and its index first. Introduce `release-rules-4` for new runs; retain V1/V2/V3 and MVP saves and behaviour. Implement a deterministic integer motor with 64-direction V4 commands, acceleration/braking, physically scaled body clearance and a shared read-only prediction solver. Preserve existing 16-heading command callers. Preserve moving Start Night checkpoint replay, including velocity; ordinary Day-save loads still start at rest.

Implement the specified Night-only wind-up/contact/recovery attack: commit aim at acceptance, resolve damage once at the contact step against actual current victims and blocking geometry, and drive animation, reaction, audio and death from that authoritative contact. Never let animation events, root motion or Unity physics decide gameplay. Maintain guard, pause, death, view-switch and input-buffer semantics exactly as specified. Keep old-profile swing timing and values intact.

Make the camera safe for its near-plane volume and transition poses, with the chosen obstruction order and documented tight-space fallback. Add explicit focus suspension, cursor release and consumed-click recapture without overwriting player pause. Finish a hero-specific articulated animation adapter/asset with directional locomotion, starts/stops, turns and one polished attack; preserve shared enemy and legacy hero rigs. Add dedicated hero sound families and actual-contact footfalls, keeping voice limits and threat priorities. Keep the low-poly direction, owner crosshair/look polarity and essential tactical HUD information.

Run affected tests first. Extend existing fixtures only for named rules, bugs or meaningful new risks; preserve all legacy assertions. Use bounded simulation loops. Adapt the active-hero policy to V4 reach and acceleration before judging balance; compare V3/V4 with seeds 1–8, three policies and idle/active/no-build cases. Respect the three-candidate tuning limit. Do not retune waves, towers or enemies to conceal a failed hero design.

Use the plan's test/build gates and supported Unity tooling. Read applicable Unity skills before controlling the Editor or changing UI. Never start a second Editor against an open project. Capture matched live motion and listen to the slice; screenshots and passing tests alone cannot establish quality. Measure paired visible-build performance with the actual V4 hero and correct metadata at both resolutions. Distinguish fresh runs from historical files. If graphics, listening or owner review is unavailable, complete independent work and mark those checks pending rather than claim they passed.

Continue through passing stages without routine approval requests. If a real blocker appears, record the exact cause and stop only dependent work; do not invent new mechanics or weaken tests. No dependencies, purchases, commits, pushes, publishing or broad asset regeneration are authorized. Do not expand into dodge, combos, stamina, progression, unit avoidance or a campaign redesign.

Write `plans/019-results.md` with stage status, commands/evidence, tuning candidates, build path, last verified step, next action and limitations. On implementation, merge only actual completed rules into the current rules spec, update versions/status/indexes together, and archive the feature spec. Report technical completion separately from owner-approved motion/art/listening quality. Finish with the playable build path and concise verification results.
