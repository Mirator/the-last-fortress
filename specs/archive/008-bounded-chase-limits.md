# Bounded chase limits — feature specification

Version **0.3**, 2026-10-28. Status: **IMPLEMENTED and merged into `../002-game-rules.md` v1.8 section 18 (2026-10-28); archived.** Not current: plan from `002-game-rules.md`. The owner's live check is still pending. Approved by the owner (options 2026-10-27; D1 and D2 on 2026-10-28, both as recommended).

This document extends `002-game-rules.md` v1.7 and respects `001-vision.md`. It changes only the "Threat: bounded pursuit" paragraph of section 18 (`release-rules-4`). Section 17 and older profiles are unchanged.

## 1. Problem

A new-player review (2026-10-21) reported that the hero can kite enemies and pull them back and forth over Spike Traps. That report was based on reading the code, not on play. Checking `RunSimulation.StartsPursuit`, `PursueStep` and `MoveOff` confirms the exploit, with two corrections:

1. **Nothing limits chases started by a hero strike.** The 2 s cooldown applies only to proximity chases. A struck Grunt, Runner or Brute starts a new chase at its next decision point, so a hero that keeps tapping it can keep it chasing for the whole night.
2. **A chase moves the enemy for good.** After the chase, the enemy walks back only to the centre of the cell it is standing in, not to where it left its route, and then plans a new route from there. Per 4 s chase that is up to 1.2 tiles for a Grunt, 2.4 for a Runner and 0.8 for a Brute.
3. **Traps fire during the chase.** `MoveOff` triggers a Spike Trap whenever the chasing enemy enters a trap's cell.
   - Correction: the walk back never fires a trap. It heads for the centre of the cell the enemy is already in, and `MoveOff` triggers only when the cell changes.
   - The repeat hit comes from the new route. It walks the enemy back over the trap it was pulled across.
   - Each cycle therefore hits every trap twice: once on the chase across, once on the route back. A chase can cross several adjacent traps (a Runner covers 2.4 tiles in 4 s).
4. **The hero can stall an enemy for most of the night.** The hero runs at 0.7 tiles/s, against a Grunt's 0.3 and a Brute's 0.2.
   - Without striking: the hero stands about 1 tile away and backs off. The enemy chases for 4 s, then waits through the 2 s cooldown, so it is off its route for about two thirds of the time.
   - Striking it now and then raises this to almost all of the time.
5. **The impact has never been measured.** The scripted "active" hero (`ReleasePolicyTests`) walks to the nearest enemy and fights it; it never kites. The recorded results (active wins kill-zone 8/8, corridor 4/8, economy 8/8; hero share of kills 4–8%) therefore say nothing about this exploit.

Why it matters:
- Section 7 says that "routing never loops to farm trap damage", and this breaks it in spirit.
- An enemy pulled off its route leaves the lane the towers were built for.
- An enemy the hero can hold off indefinitely lets the hero, not the layout, win the night. That conflicts with the central design question in `001-vision.md`.

## 2. Goals and boundaries

Goals:
- Keep the threat the owner asked for: an enemy that is struck or approached fights the hero.
- Bound how far and how often one enemy can leave its route in a night, so neither trap farming nor stalling can be repeated.
- Keep enemies on the routes the player built for.
- Keep it deterministic, cheap and readable.

Out of scope: the hero's speed, damage or reach; Spike Trap values; enemy stats; routing; new enemy behaviours; balance tuning beyond re-checking the scripted results.

## 3. Options

| | Change | Stops trap farming | Stops stalling | Threat kept | Cost / risk |
|---|---|---|---|---|---|
| **A** | **Chase budget.** Each enemy may start at most N chases per night (strikes included). | Yes, bounded to N cycles | Yes | Fully during the chase. Afterwards it depends on C. | Small: one counter per enemy, cleared at Night start. No save change, because Night checkpoints are taken at Night start. |
| **B** | **Return to the route.** After a chase, walk back to where the enemy left its route, not to the nearest cell centre. | Partly: no lasting displacement, but cycles still repeat | No | Unchanged | Medium. The straight line back may cross a solid cell, because the chase path bends, so it needs either a stored trail or a fallback. More code for less gain than A. |
| **C** | **Fight back in place.** An enemy that has no chase left still attacks the hero when the hero is within its 0.20-tile hit reach, without leaving its route. It holds only while it can actually hit (stop and keep distance both 0.20). A struck enemy that is attacking a building turns on the hero while the hero is within 0.20, then goes back to the building. | No (pair with A) | Bounded: holding an enemy requires standing where it hits, so the hero pays HP for it | Restores the threat after A, including for Brutes at a Wall | Small. It is like section 17's engage/hold rule, and that code still exists (`MeleeHoldsHero`). However, section 17's 0.6/0.9 band must **not** be copied: a keep distance larger than the 0.20 hit reach lets the hero hold an enemy from 0.21 tiles away at no cost. Caveat: a walking enemy decides only at cell centres, so a Brute walking past may not turn until its next centre, up to 5 s away. |
| **D** | **Traps ignore movement off the route.** Chases and walks back never fire Spike Traps. | Mostly: the route back still fires once per cycle | No | Unchanged | Small, but visibly inconsistent: an enemy walks over spikes and takes nothing. |
| **E** | **Tune only.** Apply the cooldown to strike-started chases too and make it longer (for example 6 s). | Reduces | Reduces | Unchanged | Very small, but it only lowers the rate, and the exploit stays repeatable. |

## 4. Recommendation

**A with N = 1, plus C.** Traps stay unchanged.

- **Why A is the bound.** It is the only cheap option that caps both farming and stalling. Per enemy per night:
  - extra trap damage is limited to the traps crossed during one chase (each 7–15 damage with Spiked Corridor), plus the route back across them;
  - time off its route is limited to one 4 s chase plus the walk back.
- **Why C is needed.** Without it, an enemy that has used its chase ignores the hero, and the hero gets free swings.
  - This matters mostly for Brutes: 180 HP is 18 swings, about 13.5 s. A Grunt usually dies within its one chase (5 swings, about 3.75 s).
  - Brutes spend most of a night attacking Walls. That is why C includes the "struck while attacking a building" case; without it, a Brute at a Wall takes free swings after its one chase.
  - C keeps "hit it and it hits back" without letting the enemy leave its route.
- **Why not B or D.** B costs the most and stops the least. D trades a readability inconsistency for a gain that A already gives.
- **Expected effect.** In scripted play: none or very little. The active script fights each enemy once, and most enemies die during their first chase. The change is aimed at the kiting a person would do. Section 7 says how to show that.

## 5. Proposed rules text (replaces section 18, "Threat: bounded pursuit")

> **Threat: bounded pursuit** [P: start 1.0 tile, leash 1.5, 4 s, cooldown 2 s, provocation 2 s, one chase per enemy per night, enemy hit reach 0.225; I: decision points, ordering, the budget is per enemy and per night, a hold never outlasts the enemy's hit reach]. At a cell-centre decision point a Grunt, Runner or Brute that has not yet chased this night chases the hero when the hero struck it within the last 2 s (even while it was hitting a building), or when it is not attacking a building, the hero is within 1.0 tile and its 2 s cooldown is over; in both cases only when no solid building or impassable cell lies on the line between them. It leaves its route and walks straight at the hero at its own speed, stops just inside its 0.225-tile hit reach and attacks the hero on its normal interval. The chase ends after 4 s, when the hero is beyond 1.5 tiles, down, or out of line; the enemy then walks back to its cell centre and plans its route again. Once it has chased, it never leaves its route for the hero again that night. At a decision point where it would otherwise move on (never at its route end), it stops and attacks the hero when the hero is within its 0.225 hit reach and keeps attacking only while the hero stays within 0.225; otherwise it moves on, and its next chance to stop is its next cell centre. An enemy attacking a building that the hero struck within the last 2 s turns its attacks on the hero while the hero is within 0.225, then resumes the building. Switching between the building and the hero keeps its attack timing: the timer carries over both ways, and a hit already due lands at once. Spike Traps trigger when a chasing enemy enters their cell. Bombers, bosses and Raider Archers never chase (archers keep section 17's provocation). Melee hits on the hero land within 0.225 tile, the hero's own swing reach.

The hold uses the enemy hit reach and adds no new distance. That reach rises from 0.20 to 0.225 (D2), so a hero that can strike an enemy is always within its answer. Options C and section 4 above were written at 0.20; read them with 0.225. Section 16 gets a row for this change.

## 6. Owner decisions (2026-10-27)

1. **Option: A + C with N = 1, including the "struck while attacking a building" case.** Rejected alternatives: A alone, A with N = 2, and E. Without the building case, a Brute at a Wall would take free hero swings after its one chase.
2. **Rules version: in place in `release-rules-4`.** This follows the pursuit revision of 2026-10-20 and the Cannon change of 2026-10-21. Nights restart from the Start Night checkpoint, so an existing save plays its current night under the new rule and nothing is corrupted. No `release-rules-5`.
3. **Traps: a chase still fires Spike Traps.** The one-chase limit bounds them.

4. **D1, attack timing on a switch (2026-10-28): the timer carries over.** An enemy switching between a building and the hero keeps its attack timer both ways. A restart would let the hero reset a Brute's attack on a Wall forever.
5. **D2, enemy hit reach (2026-10-28): 0.20 → 0.225,** the hero's swing reach. Otherwise, after its chase, an enemy could be struck from 0.20–0.225 without answering. The chase's stopping distance (0.75 × reach) follows.

**Merge note.** When this is merged into section 18, the chase cooldown is dropped from the text, because it cannot take effect with one chase per night.

## 7. Acceptance and verification

- **Show the exploit first.** Add a small scripted kiting hero to `ReleasePolicyTests`. It strikes an enemy, then backs away across a Spike Trap, and repeats. For a fixed seed, record:
  - trap triggers while the enemy is off its route;
  - the share of night steps each kited enemy spends off its route.
  
  Run it before and after the change. Pass, after the change:
  - each enemy chases at most once per night;
  - it enters traps off its route only during that chase;
  - no enemy is off its route for longer than one chase plus the walk back;
  - a hero standing just outside 0.225 tile holds no enemy.
- **No regression.** Re-run the `release-rules-4` policy check (seeds 1–8, own orientations, idle/active/no-build) and record it in section 16. Expected:
  - active wins are kill-zone 8/8, corridor 4/8, economy 8/8, each within one seed;
  - the hero's share of kills on Nights 6–12 stays near 4–8%;
  - a hero alone still falls by Night 3.
  
  A larger shift needs the owner's review, not quiet tuning.
- **Tests.** Extend the existing `ATouchingEnemyIsStruckThenChasesTheHeroBrieflyAndReturnsToItsRoute` case, or add one next to it. After its chase, a struck enemy does not chase again; it holds and attacks the hero while the hero is within 0.225, and continues once the hero is beyond it; switching back to a building keeps the attack timer. Nothing else.
- **Owner check.** In live play, try to kite one Brute across a trap twice in one night; the second attempt must fail.
