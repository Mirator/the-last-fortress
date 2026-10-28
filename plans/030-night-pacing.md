# Plan 030: call early for gold, earlier bosses

Status: **DONE** (2026-10-28; results in section 3). Owner live check pending. Priority P1. Effort M. Implements **`specs/archive/012-night-pacing.md` v0.3** (was `specs/012-...` v0.2 when planned) (approved 2026-10-28: D1–D4). Current rules: `specs/002-game-rules.md` v1.13. Baseline commit: `7629cd0`.

## 1. Pre-check

Paths are under `game/The Last Fortress/Assets/TheLastFortress/Runtime/`.

- **Spawns.** `RunSimulation.ReleaseDueSpawns` compares each stored `SpawnEntry.Tick` with `Tick`. The early call is kept as a call tick (−1 when none) plus an effective-tick function, so the stored plan and the checkpoints are unchanged. The call tick is reset by `StartNight` and by a reset.
- **Income.** `PayIncome` already adds War Bonds from `LastNight`. The early-call gold goes into `NightRecap.EarlyCallGold` and a new `IncomeBreakdown.EarlyCall` line (part of `Total`, never part of the forecast). Nights are not checkpointed: a reload during Reward replays the night from the Day checkpoint, so no save field is needed.
- **Gate.** `Campaign.RulesVersion == ReleaseCampaign.RulesV4` (legacy profiles unchanged).
- **Boss time.** `ReleaseCampaign.Recipe.BossSeconds` is 30. `Create` passes the rules version, so the table uses 15 for `release-rules-4` only.
- **HUD and input.** C is free (the used keys are Esc, F, H, P, R, Space, Tab, Z and WASD). The button sits above the speed buttons.

## 2. Stages

1. **Core.** The call itself (`CanCallEarly`, `EarlyCallGoldNow`, `CallEarly`), the effective ticks, the bonus in the recap and the dawn income, the boss at 15 s for V4, and an `EarlyCalled` event. The snapshot line adds the call tick only when one exists, so older fingerprints are unchanged.
2. **EditMode test** in `ReleaseRulesTests`, per spec section 4, using a value where floor and round differ. Mutation-check it.
3. **Policy runs:** `ReleasePolicyTests` (recorded wins asserted), plus the pacing probe kept outside the repository (`scratchpad/probe/`). Compare with the baseline.
4. **HUD and input.**
   - The button "CALL EARLY +N" with the C key; it is hidden when a call is not possible.
   - The recap line and the income line.
   - Run the PlayMode scene tests.
5. **Capture and close.**
   - Add an early-call frame to the full run (Night 2).
   - Merge the rules into `002-game-rules.md` (v1.14), add a §16 tuning row for the boss time, archive the spec, and update the indexes and the results.

## 3. Results (fresh, 2026-10-28)

| Check | Result |
|---|---|
| `EarlyCallTests` (call at tick 10; spawns at 30, 61, 106 compress to 15, 23, 34; 3 gold, where rounding would give 4; paid once at dawn; nothing to call at a later night's start) and `LegacyRulesCannotCallEarly`; boss time checked in `ReleaseContentTests` (15 s for V4, 30 s for V3) | Passed (`after/EarlyCall.xml`) |
| Mutation: plain division instead of ceiling | Failed as intended: "compressed spawn ticks", 22 instead of 23. Restored. |
| Mutation: no dawn payout | Failed as intended: "paid at the next dawn as its own line", 0 instead of 3. Restored. |
| Full EditMode (`after/EditMode.xml`) | 131/131 |
| Full PlayMode (`after/PlayMode.xml`) | 24 passed, 1 skipped (same skip as before) |
| Recorded policy tests (`policy/Policy.xml`, boss at 15 s) | 3/3 passed, so recorded wins hold |
| Pacing probe, 15 runs (V4 active hero, seeds 1-5 × 3 strategies) | See the next table |
| Release build; full-run capture (`captures/`) | Succeeded. Night 2 call "+10 gold (C)", the button hides after the call, the recap reads "called early: +10 gold at dawn", Day 3 shows "Received +50 at dawn (early call +10)". Victory 12/12. |

**Probe:** `pacing-baseline-boss30.txt`, `pacing-after-boss15.txt`, `pacing-probe-boss20.txt`. Night averages in seconds; the scripts never call early:

| | Night 6 | Night 9 | Night 11 | Night 12 | Losses (of 15) |
|---|---:|---:|---:|---:|---:|
| Boss 30 s (baseline) | 94 | 88 | 89 | 100 | 2 |
| Boss 15 s (shipped) | 84 | 84 | 88 | 91 | 3 (corridor seed 1, Night 9) |
| Boss 20 s (probe only) | 87 | 85 | 87 | 93 | 4 |

The owner kept 15 s (2026-10-28). The probe test and the step hook are not committed; they are kept as `PacingProbe.cs.txt` and `probe-hook.diff`, to re-apply for later pacing checks.

**Not verified:**
- How the call feels in play.
- Whether 1 gold per second is worth the risk. Scripts never call, so no scripted run measures the risk.
- Hero-view use of C.
