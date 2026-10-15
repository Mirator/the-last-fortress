# Plan 006 stage 7 — defensive opening comparison

2026-10-15. Background Core simulation only (no Unity, no desktop). Driver: `adaptive/Adaptive.csproj` (compiles the live `Runtime/Core/*.cs`; MVP defaults from `MvpDefinitions.CreateDefault()`, which match `Data/MvpDefinitions.asset` as checked by playtest B). Full logs: `comparison-runs.txt` (purchases with actual prices, skipped/relocated purchases, repairs, Keep HP and per-night recap, surviving Towers/Walls, offers and choices). All 16 runs resolved within 40000 steps per night and every claimed purchase was verified on the grid (exit 0).

The adaptive policy's rules were written into the driver header before any comparison run; they were not tuned to the outcomes. Old fixed policies and the earlier agent transcripts were left untouched.

## Outcomes (generated maps, seeds 1–3, all accepted at terrain attempt 0)

| Policy | Rewards | Seed 1 | Seed 2 | Seed 3 | Gold earned |
|---|---|---|---|---|---|
| Economy-first (fixed script) | normal | Victory, Keep 500 | Victory, 650 | Victory, 650 | 373–498 |
| Defense-first (old fixed script) | normal | Victory, 150 | Victory, 150 | **Defeat, Night 5** | 80–100 |
| **Defense-led adaptive** (Towers first, House Day 2, Farm only if affordable) | normal | Victory, 650 | Victory, 650 | Victory, 500 | 160 |
| Pure defense — DIAGNOSTIC (same rules, no economy) | normal | Victory, 650 | Victory, 650 | Victory, 500 | 100 |
| Economy-first | DEBUG no-perk | Victory, 472 | — | — | 425 |
| Defense-first (old) | DEBUG no-perk | Defeat, Night 5 | — | — | 80 |
| Defense-led adaptive | DEBUG no-perk | Victory, 392 | — | — | 160 |
| Pure defense — DIAGNOSTIC | DEBUG no-perk | Victory, 352 | — | — | 100 |

Lowest Keep HP during the run: adaptive 450 (seed 3, Night 4); old defense 150 on its wins and 0 on its loss.

## What explains the old defense loss

The old script's Day 3 buys four Walls on the Keep's neighbours (80 gold) and then never affords another Tower (Day 4 has 20 gold, Day 5 40). It fights Nights 4–5 with three Towers and loses on seed 3 (and on seed 1 without perks). The adaptive rules simply keep saving for Towers first (a 4th Tower on Day 3/4), add Keep Walls only after Tower targets are met, and repair the Keep from leftovers. The adaptive and pure-defense policies win all three seeds with large margins. Terrain is identical per seed across policies, and the Night 1–2 offers are identical across the defense variants; later offers differ because offer weights depend on owned buildings, but the chosen perks overlap heavily (Sharpshooters, Flaming Arrows, Barbed Defenses, Expanded Vision in both). The evidence points to purchase ordering as the main difference, without fully isolating reward variation; a fixed-offer diagnostic was not run.

The adaptive policy's "delayed economy" turned out small: it bought a House on Day 2 but its Farm was never affordable while it kept Tower priority (55 gold on Day 3), so it earned 160 gold versus 100 for pure defense.

## Recommendation

1. **No balance change is indicated.** A defense-led opening that adapts its purchases wins all three seeds; even pure defense on guaranteed Keep income wins them (diagnostic). The earlier "defense is weak" signal came from one fixed script's purchase order. Do not tune costs/waves on this evidence; plan 004's bounded-tuning rules remain available if human playtests later show a real gap.
2. **Product decision still needed (owner):** the remaining asymmetry is margin, not viability — economy-first ends with ~2–3x more gold and full Keep HP, and also wins without perks. Decide whether that is the intended reward for investing early, or whether defense-led play should be pushed toward a comparable margin. This is a goal decision for the owner, not something these runs can settle.
3. Mechanical viability only: these scripts say nothing about whether players understand or enjoy either approach.
