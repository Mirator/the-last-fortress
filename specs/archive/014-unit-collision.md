# Feature spec 014: units collide — enemies keep their distance, the hero cannot walk through them

Version 0.3, 2026-10-28. Status: **IMPLEMENTED and merged into `../002-game-rules.md` v1.16 (2026-10-28); archived.** Not current: plan from `002-game-rules.md`. Approved by the owner on 2026-10-28 (D1–D3 as below; D4 in place; starting values accepted). The owner's live check is pending. Changes `002-game-rules.md` v1.15:
- section 2 ("units never block each other");
- section 5 (routing, cell decisions);
- section 7 (attacks on buildings);
- sections 17 and 18 (hero collision, touch hits).

This is a simulation change, so balance moves.

## 1. Problem

Today units have no bodies against each other.
- **Enemies.** They walk the cell centrelines of their routes and stand inside each other, and a column is spread out only when drawn: a fixed offset of up to ±0.12 tile per enemy, presentation only.
- **The hero.** The hero walks through enemies, and enemies walk through the hero.

The owner wants enemies not to pass through each other, and the hero not to pass through enemies.

## 2. Decisions

- **D1: Real collision, in the simulation** (owner). Enemies get bodies and spacing in the simulation, so what is drawn is where they are. The alternative, a visual-only separation, was rejected: the hero would collide with positions that differ from the drawing.
- **D2: Enemies push the hero** (owner).
  - The hero cannot walk through enemies; it slides along them as it does along buildings.
  - An enemy that walks into the hero shoves the hero aside.
  - The hero never blocks an enemy, never changes a route and never occupies cells; that rule stays [I]. One hero cannot plug a corridor.
  - The alternative, the hero blocking enemies like a wall, was rejected.
- **D3: Attackers spread along their target** (owner). Melee attackers of one building stand side by side along the faces they can reach. Those that find no room wait behind and step in when a slot frees. The alternative, overlapping while attacking, was rejected.
- **D4: Rules version** (owner: in place). Change `release-rules-4` in place. It has never shipped, and plans 019, 022 and 030 did the same. The owner's own `release-rules-4` saves would then replay their current night differently on Continue. The alternative is a new `release-rules-5`, which keeps old saves exact but adds one more legacy profile to maintain.

## 3. Rules (proposed)

1. **Bodies** [P values; I: integer units, deterministic order].
   - Each enemy has a circular body: a radius by kind (section 4) and a position in integer 1/1200-tile units.
   - The hero keeps its 72-unit body.
2. **Lanes** [I].
   - Each ordinary enemy walks its route at a fixed sideways offset from the cell centreline, chosen from 5 lanes by enemy ID, between −0.25 and +0.25 tile [P].
   - Bosses walk the centreline.
   - The offset moves from presentation into the simulation, and turns smoothly at corners.
   - Routing is unchanged: same cells, same decisions at cell centres, same costs [I].
3. **Spacing.**
   - Enemies move in ascending ID order.
   - An enemy does not advance on a step if that step would bring its body within `r_self + r_other` of an enemy that is ahead of it (in front along its own direction of travel) and not pursuing. It keeps its progress and tries again on the next step. Speed is otherwise unchanged; there is no acceleration.
   - Pursuing and returning enemies (the hero chase) use the same body test against every enemy.
4. **No deadlock.**
   - Two enemies that block each other head-on for more than 2 s [P] stop testing each other until they are apart. This rarely happens, because every route leads toward the Keep.
   - An enemy that waits 4 s [P] behind a non-moving enemy that is not attacking may pass through it, again until they are apart.
   - The night must always be able to end.
5. **Spawning.** A due spawn whose entry point is still occupied by a body waits, in the same way as the living cap (FIFO; pending spawns still block completion). Spawn times otherwise stay as planned.
6. **Attack slots** (D3).
   - A melee attacker of a building takes a free slot on a face it can reach. The faces are the cardinally adjacent free cells, as today.
   - Each face holds up to 3 [P] attackers side by side, at the lane offsets of that face.
   - Attackers with no free slot wait at their position on the route, in ID order. They take the next slot that frees: a death, a retarget or a destroyed target.
   - Ranged attackers (Raider Archers) and Bombers are unchanged, apart from their bodies and spacing while walking. A Bomber's fuse rules stay the same.
7. **Hero** (D2).
   - Hero movement treats enemy bodies like obstacles: it slides along them, X then Z [I], and checks against the positions of the step's start.
   - After the enemies move, a hero that overlaps an enemy body is pushed straight out of it, by the shortest way that respects buildings, terrain and the edge. If no position is free within 0.1 tile [P], the hero stays overlapped this step and the enemy is not stopped.
   - The touch hit becomes "any enemy whose body touches the hero's body (gap ≤ 0.02 tile [P])", instead of "within 0.1 tile centre to centre", because bodies can no longer get that close.
8. **Traps, splash, auras, targeting** use the real body positions. Their radii and rules are unchanged.

## 4. Starting values [P], to be set from captures and policy runs

| Value | Start |
|---|---|
| Body radius (tile) | Grunt, Runner, Archer, Bomber 0.07; Brute 0.10; Siege Golem and Siege Commander 0.18 |
| Lanes | 5, at −0.25 / −0.125 / 0 / +0.125 / +0.25 tile |
| Attackers per face | 3 |
| Head-on release / pass-through after waiting | 2 s / 4 s |
| Hero push limit | 0.1 tile |

## 5. Acceptance

- **EditMode, rules:**
  - spacing in a column;
  - a waiting enemy resumes when the one ahead moves;
  - attack slots, and a waiting attacker steps in when one dies;
  - the hero cannot walk through an enemy, and an enemy pushes the hero;
  - the touch hit works at body contact;
  - deadlock release.
- **Determinism:** the existing fingerprints for legacy profiles are unchanged, and replays of `release-rules-4` are stable.
- **Policy runs:** the 15-run probe and the recorded wins. The result must not be "much more difficult" (the owner's constraint). Throughput changes are expected, so retune only the values marked [P] here, with a section 16 row.
- **Performance:** the stress fixture of 100 enemies stays within section 14's budget.
- **Captures:** columns, crowds at a Wall and the Keep, the hero brushing past enemies, and a boss in a column. The owner's look is the acceptance of record.

## 6. As built (2026-10-28)

These deviations are merged into the game rules as built.

- **No separate attack slots** (rule 6). Melee attackers stand in their lanes on their attack cell, so the lanes are the slots. A cell face holds up to 5 (not 3). Later arrivals wait behind, because the body rule stops them, and step in when a place frees.
- **Lanes 0.14 tile apart** (not 0.125), so two 0.07 bodies fit side by side.
- **One release rule** instead of two (rule 4). A body blocked for 4 s may walk through bodies for half a tile, unless the chain of bodies ahead of it ends at one that is attacking or has a lit fuse.
  - The first version kept a "queued behind an attacker" flag. It fed on itself in a circle and deadlocked a policy night: kill-zone seed 2, Night 6, 10 enemies stuck at a corner.
  - The chain is now followed afresh every step, with loop protection.
- **Sideways brushing.** A body easing into its lane at a corner may brush a neighbour beside it by one step of sideways movement, at most 0.015 tile. Only bodies ahead are tested, which avoids stalls from bodies behind.
- **Reach and body size.** A boss body (0.18) plus the hero (0.06) is wider than the hero's 0.225 swing. So reach, touch, enemy hits on the hero and the chase's stopping distance all extend by the amount an enemy's body is wider than 0.07 tile.
- **The touch hit** is 0.15 tile centre to centre: 0.06 + 0.07 + 0.02.
- **The stress fixture** does not wait at its entries, so it stays a fixed 100-enemy load. Waiting made it fill to only 52 by capture time.
