# Agent guidelines

## Review specs before planning

- Before writing an implementation plan, read `specs/README.md`, the spec the plan implements, and `specs/002-game-rules.md` (current rules). Check that they agree with each other and with the code the plan will touch.
- If a spec is stale, contradicts another spec or the code, or leaves a decision open that the plan depends on, report it to the owner and get it resolved first. Do not quietly resolve it inside the plan.
- Every plan names the spec (and version) it implements. When a spec is implemented or superseded, update its status line and `specs/README.md` in the same change.

## Keep tests useful and lean

- Add or change a test only to protect a concrete gameplay rule, reproduce a real bug, or cover a meaningful risk introduced by the current change. Be able to name the failure it would catch.
- A request to review or verify tests means inspect and run them, not rewrite, delete, or expand them. Respect the user's requested scope.
- Before adding a test, read the nearest existing tests. Extend an appropriate case or use a small parameterized case when it covers the same behavior. Avoid duplicating coverage in another file or layer without a distinct reason.
- Prefer fast EditMode tests for simulation, placement, navigation, economy, and phase rules. Use PlayMode tests only when the risk involves actual scenes, Unity lifecycle, input, UI, or presentation integration.
- Keep a few representative end-to-end scenarios. Do not replay the full game in separate tests for every low-level rule.
- Test observable behavior and invariants. Avoid tests for trivial getters, implementation-shaped mocks, private helper details, or constant/default-value equality without a real compatibility requirement.
- Do not freeze provisional balance values or visual details unnecessarily. Assert exact values when the rule requires them, such as refund rounding, damage timing, deterministic routing, or an explicitly maintained acceptance fixture.
- Make the scenario exercise what its name claims. A rounding test needs a value that distinguishes rounding policies; a cooldown-reset test needs an actual target change before the old cooldown expires. Passing assertions alone do not prove coverage.
- Keep fixtures and helpers small and local. Do not introduce a generic test framework, production-only test hooks, or new dependencies for a single case.
- Bound simulation loops and asynchronous waits so regressions fail with a useful message instead of hanging the runner. Avoid arbitrary real-time sleeps when simulation stepping or a condition is available.
- Do not add tests for documentation, cosmetic changes, or reversible low-risk edits unless they introduce a specific behavioral risk. Do not add tests merely to increase counts or coverage percentages.
- Run the affected tests first. Broaden verification when shared behavior or integration is affected; repeat a passing run only after relevant changes or to investigate a concrete concern. Do not rebuild or rerun performance captures for documentation-only work.
- Report what was actually verified and any limits. Distinguish existing saved results from fresh runs; never weaken assertions or bless changed snapshots just to make a failure disappear.

There is no target test count or coverage quota. Prefer the smallest set of meaningful tests that protects the current milestone.
