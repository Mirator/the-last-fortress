# Plan 004 results — six-night combat and perks

Status: DONE (mechanical gates). Executed 2026-10-15 after plan 003 in the same live Editor session (Unity 6000.3.16f1, Unity MCP pinned to `The Last Fortress@ffd3a3a9`). Nothing committed. Human playtest signoff, fun, and "two viable strategies for real players" remain unvalidated; the claims below are about scripted policies only.

## Preflight

Plan 003 prerequisites present and freshly verified (003 results: EditMode 62/62, PlayMode 12/12, MVP build OK). No unrelated drift.

## Implementation

| Area | Change |
|---|---|
| Enemy kinds | `EnemyKind` Grunt=0, Runner=1, Brute=2, SiegeGolem=3; `SpawnEntry` gains an optional kind (default Grunt, so prototype calls are unchanged). MVP definitions per spec (HP/damage/interval/speed; Brute and Golem x2 vs Walls only). Each enemy uses its own definition and attack ticks. Optional in `PrototypeDefinitions` (prototype keeps Grunt only). |
| Movement | Fixed-point: 1 tile = 1200 units, budget `speed * 60` per 0.05 s step (Grunt 60, Runner 105, Brute 36, Golem 24), validated as a whole positive number independent of attack intervals. Leftover distance at a center continues into the next segment within the same step after honoring route invalidation (no lost time, no visual stall); with zero leftover (Grunt) the decision waits for the next step exactly as in spec 002. Residual distance is discarded at the attack cell and the attack is acquired on the next step. `MoveTicks` kept as a diagnostic. |
| Waves | `MvpCampaign.CreateSixNights()` builds all six nights before Day 1 from the authored table (G/R/B cycle, round-robin W,E,N,S entries, cadence, Golem from West at 30 s after equal-time non-boss spawns). 100-living cap delays due spawns FIFO. Preview text (`WaveText.Describe`) reads the same stored plan: per-entry type counts, first/last time and a `BOSS WARNING` line. The 3-night foundation profile remains as a development fixture. |
| RNG | `RunRandom` xorshift32 (vector for seed 1: 270369, 67634689, 2647435461), zero → `0x6D2B79F5`, rejection-bounded draws; reward stream = `seed ^ 0xC8013EA4`; map-attempt stream helper prepared for 005. |
| Offers | `PerkRules.DrawOffer`: canonical IDs in ordinal order, unowned only, weight 3 if the affected building exists (Keep/general always 3), else 1; three draws without replacement. Offer frozen on entering `RunState.Reward` (appended ID 4) with offer ID `run<seed>-night<n>` and RNG before/after; repeated requests return the same object and consume nothing. |
| Perks | All ten as derived effects over shared definitions (never mutated): ReinforcedTimber/FortressHeart effective max HP with a one-time current-HP grant; EfficientFarming via `DailyIncome(farmBonus)` (per-Farm floor); ExpandedVision range ×1.1 (float); Sharpshooters/LastStand add percent then floor once; FlamingArrows burn; Recycling floor(price/4) once on combat destruction; BarbedDefenses +3 reflect (max 6 with trap); ThriftyConstruction future prices `max(1, floor(base*0.9))` while instances keep actual price. |
| Ordering | Step: due spawns → movement + trap entry → Tower attacks/burn application → burn ticks (first at +20, refresh extends expiry to now+60 keeping cadence, expiry tick inclusive) → enemy attacks with immediate non-recursive reflection → cleanup → defeat before cleared night. All kills go through one `DamageEnemy` (counted once). |
| Reward flow | Nights 1–5 cleared → Reward (building/combat stopped) → `ChoosePerk(offerId, perk)` commits once → Day n+1 → that Day's income paid once. Night 6 → Victory, no Day 7/income/reward. `DebugSkipReward()` exists only for the labeled no-perk baseline and has no UI. Restart clears perks/offers/burn/stats and restores the same seed's reward stream. |
| Stats | `RunStats`: nights cleared, enemies defeated, buildings purchased (real purchases only; stress/bypass placements excluded), gold earned (income + Recycling; starting gold and sale refunds excluded), unique synergy pairs ever active. |
| UI | Reward overlay with three cards (title, exact effect text, current relevance), stale-click safe; six-night progress; boss HP bar with burn tag; owned perks list; prices follow ThriftyConstruction; perk-aware range ring; distinct Runner (lean, ochre sash), Brute (broad, dark red, club), Siege Golem (tall stone, glowing eyes) prefabs via explicit `EnemyPrefabFor`; flickering amber burn marker. Scene now runs the six-night profile. |

`MvpSetup` gained `FillMissing` so the existing `MvpDefinitions.asset` received the three new enemy definitions without touching any existing tuning value.

## Verification

New focused tests: `Tests/EditMode/MvpCombatAndPerkTests.cs` — exact travel steps per kind (Grunt 180, Runner 103, Brute 300, Golem 450 for 9 tiles) with per-step no-teleport/no-diagonal checks; frame-chunk independence with Runner + Brute and a detour; Brute ×2 vs Wall only; authored table/cycle/entries/cadence/boss timing and preview = execution; dead boss + surviving support stays Night; living cap FIFO; xorshift vector and bounds; offer repeatability, distinctness, weights, frozen offer/RNG, stale/outside-offer rejection; additive damage + exact LastStand boundary (150 inactive, 149 active → 16); one-time HP grant, effective-max repair quote, shared definitions untouched; discounted price → sale 9 and one Recycling 4; EfficientFarming forecast = payment (39 + 32); burn cadence/refresh/inclusive expiry and pause freeze; no burn after a tower kill; Reward double-submit → one perk, one income, then restart equals fresh. Perk fixtures use the real reward path (deterministic seed search over immediately-cleared empty nights), not a test-only hook. `Tests/EditMode/MvpPolicyTests.cs` — recorded policies. The MVP scene smoke now covers Reward via HUD card click plus a stale second click.

| Evidence | Result |
|---|---|
| `plans/evidence/004-EditMode-20261015-124320.xml` | Passed, 81/81 |
| `plans/evidence/004-PlayMode-20261015-124336.xml` | Passed, 12/12 |
| `plans/evidence/004-mvp-build-report.txt` | Succeeded, 0 errors (487 warnings = cold shader recompile of the pre-existing `com.unity.ai.inference` compute shaders after new materials; see 003) |
| `plans/evidence/004-policy-runs.txt` | Full night-by-night replay logs (purchases incl. actual prices, skips, repairs, offers, choices, income, Keep HP, outcome) |
| `plans/screenshots/004-mvp/*.png` | Standalone `-capture` at 1280 x 720: Day 1, Farm ghost valid/invalid, Farm/House link, Reward cards, Day 2 income + repair quote, Night 4 mixed enemies, Night 6 boss bar, Victory, Keep-only defeat, restart |

## Viability (scripted, flat authored map, MVP default tuning)

| Policy | Seeds 1–3 with normal rewards | Same seeds, DEBUG no-perk baseline |
|---|---|---|
| economy-first (2 Towers + Farm + House on Day 1, second Farm Day 2, Towers/Walls later) | Victory ×3 | Victory ×3 |
| defense-first (3 Towers Day 1, Keep Walls Day 3, no economy) | Victory ×3 | Defeat ×3 (Night 5) |

At least one legal six-night completion exists, and two distinct scripted strategies won under normal rules. Perks are not mandatory for the economy-first script; they were necessary for this defense-first script. These are bounded scripts, not proof about human strategies. Observation for later balance work: economy-first won without perks with large margins (Keep ≥ 264 HP), so the curve may be generous once players discover Farms; no tuning was applied (0 of 3 passes) because the plan authorizes tuning only for viability failures. Combat time of the economy-first run: about 22, 29, 32, 36, 41, 51 s per night (~3.5 min total); real session duration needs human playtests.

## Remaining issues / handoff

- Enemy silhouettes are distinguishable when zoomed but small at default zoom; the Golem is dark at night — 005 visual pass.
- Wall/Trap and Farm/House highlights still subtle; HUD night timer and the policy log use the OS decimal separator.
- Last verified step: plan 004 complete. Next action: plan 005 step 1 (terrain generation).
