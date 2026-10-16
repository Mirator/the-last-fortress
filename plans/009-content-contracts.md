# Plan 009 content contracts — NOT FROZEN

Prepared 2026-10-16 under plan 009 §1 with the same rule fields as the plan 008 additions. **Nothing here is frozen and nothing is authorized for implementation.** Each item is a proposal to validate at the production gate ([009-production-contract.md](009-production-contract.md)). Values marked [P] are starting proposals; fields marked **OPEN** must be decided by design (with playtest evidence) before the item can be frozen. An item that cannot show a distinct player decision against existing content is rejected rather than built.

Overlap check baseline: Archer Tower (reliable single target, range 4), Cannon (slow area, range 4.5), Watchtower (range support), Walls (route shaping/breach), Spike Trap (entry damage), Repair Station (dawn sustain).

## Structures (3 → 12 building kinds incl. Keep)

| Item | Role and decision | Counter / weakness | Must be frozen before coding |
|---|---|---|---|
| **Tar Pit** (slowing floor trap) | Walkable 1×1 floor; enemies entering move slower for a while, buying firing time in a kill zone. Decision: where to stall a column. | Cheap to bypass with open routes; no damage; Runners still fast | **Requires a new status/movement-speed system.** Slow %, duration, refresh vs stack, interaction with fixed-point movement units and carry, boss immunity, preview text, save state (none at Day), event. OPEN: whether speed changes break route-time previews. |
| **Bulwark** (limited reinforcement) | 1×1 heavy barrier, at most N per run [P N=3]; breaching it costs routes more, so enemies prefer other blockers. Decision: which breach point to make expensive. | Cap; cost; Bombers | **Requires a per-building breach cost** in the route planner (today a flat 6). Cost value, cap scope (alive vs ever built), sale/refund and cap interaction, preview of the changed breach choice, save fields. OPEN: whether this is a separate kind or a Wall upgrade tier (a tier would not count as a kind). |
| **Ballista** (specialist long range) | 1×1, very long range [P 7] with a minimum range [P 2.5]; slow, high single damage. Decision: placement depth behind the line. | Blind spot close in; one target; Archers can still reach it at its own range | Damage/interval, min-range metric (centre to position, inclusive/exclusive), targeting when only blind-spot enemies exist, perk eligibility (Archer perks no), overlap review against the Cannon (distinct: single target vs area). |

## Enemies (7 → 10 incl. 4 bosses)

| Item | Role | Counter | Must be frozen |
|---|---|---|---|
| **Shieldbearer** (ordinary) | Frontal mitigation rewards crossfire: hits from inside its facing arc are reduced. | Flanking towers, Cannon splash, traps | **Requires facing rules** (facing = direction of travel or of its target), arc width [P ±45°], mitigation [P 70%], which damage sources count (shots, splash, traps, burn), telegraph, event. OPEN: whether reduction ever reaches immunity (must not). |
| **Warlord of Two Fronts** (boss 3) | Tests split-front preparation: arrives with escorts on two entries at once; the escorts' aura or timing depends on both groups. | Defenses covering two fronts; corridors that merge routes | Spawn pattern, escort budget within the 100 cap, what links the groups, warning text, HP/attack, entry seeding. OPEN: the linking rule (must not be hidden or reflex-dependent). |
| **Siege Ram** (boss 4) | Tests reserve and recovery: a slow ram that destroys the structures blocking it quickly, forcing a rebuild plan for the next Day. | Gold reserve, Repair Stations, layered routes | Damage model vs structures, interaction with Bombers/Gatehouse, whether it attacks the Keep, recovery support (none beyond existing economy). OPEN: how "recovery" is tested without punishing a loss twice. |

## Biomes (1 → 3)

| Biome | Tactical condition | Profiles (≥2 each) | Must be frozen |
|---|---|---|---|
| Wooded river frontier | Rivers and dense forest: few wide build areas, natural fords | e.g. "Fords" (split), "Thicket" (corridors) | Skeletons, variation pools, named constraints, ≥ N disjoint 2×2 sites, terrain types (reuse Forest/Water; no new terrain rules unless specified), asset list |
| Exposed rocky coast | Open approaches from two sides of a peninsula; rock outcrops as natural walls | e.g. "Headland" (open), "Cliffs" (corridors) | Same; any shoreline must keep entries on the boundary ring |

No fog, elevation, height-based damage or weather simulation is implied.

## Perks (24 → 40)

Sixteen more, each needing: ID, affected kind (draw weight), exact effect with composition order, and at least one decision it changes. Proposed themes to evaluate, not a list to fill: Tar Pit and Ballista synergies (≥4), map-feature perks that interact with rivers/rocks (≥3), economy timing (≥3), counters to the two new bosses (≥2), support stacking rules (≥2), reward-economy choices (≥2). Admission rule: reject any perk that is only a percentage on an existing stat already covered.

## Difficulties (1 → 3)

| Name | Proposal [P] | Must be frozen |
|---|---|---|
| Relaxed | fewer ordinary spawns per night (authored table, not a multiplier), +gold at start | which nights change, save field, unlocks unaffected |
| Standard | the 008 table | unchanged baseline, always available |
| Hard | authored extra spawns on Nights 7–12 and an earlier boss time | same |

Difficulty must be stored with the run (008 schema 2 already has the field) and never change mid-run.

## Challenges (0 → 12)

Contract per challenge: stable `challengeId` + `version`, fixed seed, map profile, rules/difficulty, objective evaluated from actual outcomes (e.g. "win without Walls", "Keep never below 50%"), deterministic single award, no server. Twelve candidates to be chosen from playtest observations (e.g. one per map profile × objective type). Frozen only with a bounded simulation that proves each objective is achievable and evaluable.

## Status

All items **OPEN**. Freeze them one at a time after the production gate, with the same EditMode failure-mode tests 008 used.
