# Playtest A: first-run clarity, placement recovery, perks, and resume

Date: 2026-10-15. The requested desktop launch used an isolated save folder. Its Unity log showed engine, D3D12, and input initialization, but Computer Use timed out on two screenshot captures. No game input was sent, and there are no screenshots or visual observations from that attempt. I stopped only that game process.

To continue without the user's desktop, I ran the actual `Runtime/Core` C# sources through the standalone console scenario in `scenario/`. This is a plain simulation run, not a visual or human playtest. It uses the six-night campaign, flat authored terrain, and seed 1 or 2. Run it with:

```powershell
dotnet run --project plans/evidence/006-playtest-a/scenario/Scenario.csproj -- 1
dotnet run --project plans/evidence/006-playtest-a/scenario/Scenario.csproj -- 2
```

## Observed simulation sequence

- Started Day 1 with 250 gold and the Keep at 500/500 HP.
- A Wall placement on the enemy-entry ring returned `ReservedRing`; a Tower on the occupied Keep cell returned `Occupied`. Both were rejected without changing the gold balance. The ring rejection also left the grid revision unchanged.
- Bought three Towers for 210 gold, leaving 40. A Farm attempt returned `InsufficientGold` without spending gold. Selling the third Tower returned 35 gold, after which the Farm placed successfully for 60.
- Two Towers and the Farm cleared Night 1 on both seeds in 391 fixed steps (19.55 seconds), with the Keep at 500/500 and all ten enemies defeated.
- Seed 1 offered Last Stand, Thrifty Construction, and Sharpshooters. Choosing Sharpshooters changed effective Tower damage from 10 to 12; range stayed 4.0 and forecast income stayed 45.
- Seed 2 offered Efficient Farming, Recycling, and Flaming Arrows. With the Farm present, the pre-choice income forecast was 45; choosing Efficient Farming changed the next-day forecast to 52, and entering Day 2 paid 52 gold.
- A Day 2 checkpoint restored with no problem and matched the live snapshot after ignoring the grid revision counter. Night 2 then replayed for 623 steps and ended in Reward with identical normalized snapshots, Keep HP, enemy count, and gold on both uninterrupted and restored runs.

Full console evidence is in `scenario-run.txt` and `scenario-run-seed2.txt`. The scenario exits nonzero if Night 1 does not reach Reward, restore fails, or the resumed Night 2 differs.

## Candidate clarity improvements (source-inspected, not visually verified)

`HudView`'s selected-Tower text reads `sel.Definition.AttackDamage` and `sel.Definition.Range`; it reads base building income as well. Those values can diverge from the live simulation after perks. In the seed 1 scenario, the live effective damage became 12 while the base definition remained 10. Consider showing the effective values used by combat and income calculations in the selection panel.

The reward header computes `sim.IncomeForecast()` before the player chooses a card, while its text says the listed amount will be paid “after your choice.” In seed 2, that pre-choice forecast was 45; choosing Efficient Farming made the actual Day 2 payment 52. The displayed reward preview can therefore understate the upcoming income when Efficient Farming is in the offer. This is inferred from the inspected HUD code and the measured simulation values; no frame was captured.

## Limits

The Core checkpoint object was captured and restored directly; this did not exercise the game's file serializer or Continue menu. The desktop build's menu, controls, camera, placement ghost, multi-side warning, selection panel, repair/sell interface, saving, and continue flow remain unobserved. No production code or tests were changed.
