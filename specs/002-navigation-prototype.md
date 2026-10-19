# Navigation prototype specification

Version: 1.0, 2026-10-15. Status (2026-10-19): implemented; kept as the prototype rules reference. Current release rules: [005-release-rules.md](005-release-rules.md). This document defines the first implementation milestone, not the six-night MVP or full release. Routing policy and milestone boundaries were confirmed by the owner; numerical tuning and technical defaults below are initial implementation decisions subject to measured revision.

## Goal and boundaries

Prove that a player can place walls and towers, predict the resulting enemy route, and understand the combat outcome. Deliver a runnable Windows Unity scene with one authored wave and restart.

Included: one flat authored map, Keep, Wall, Archer Tower, Grunt, gold spending and demolition, placement and route previews, automatic combat, pause, victory, defeat, restart, and a separate stress fixture.

Deferred: passive income, Houses, Farms, traps, soldiers, abilities, building upgrades, perks, synergies, fog of war, elevated terrain, procedural generation, saves, unlocks, audio production, and Steam integration. No paid or downloaded art is required.

## Project baseline

- Project: `game/The Last Fortress/`.
- Editor: `6000.3.16f1`, as recorded in `ProjectSettings/ProjectVersion.txt`.
- Existing dependencies include URP, Input System, and Unity Test Framework. Preserve the manifest and lockfile unless a demonstrated blocker requires a change.
- Existing scene: `Assets/Scenes/SampleScene.unity`; create `Assets/TheLastFortress/Scenes/NavigationPrototype.unity` for this milestone.
- There are no existing gameplay scripts or gameplay tests. `Assets/TutorialInfo/` contains template code, not an architecture to extend.
- Use namespace `TheLastFortress`. Keep reusable simulation rules in ordinary C# classes, rendering and input in Unity components, and tuning values in serialized definitions. Do not implement the entire GDD's manager list for this milestone.

## Grid and authored map

- Grid coordinates are integers `(x, z)` from 0 through 19, with one tile equal to one Unity unit. World center of a tile is `(x + 0.5, 0, z + 0.5)`.
- World-to-grid conversion floors world X/Z. Points outside the grid are rejected, never clamped into a valid placement.
- All 400 cells are flat land. A decorative water border lies outside the logical grid and is never navigable.
- Keep occupies `(10,10)` and is one cell in this prototype. Its larger mesh must not imply extra occupied tiles.
- Main wave enters at `(0,10)`. Stress fixture may also use `(19,10)`, `(10,0)`, and `(10,19)`.
- The entire outermost ring is reserved for enemy entry and cannot be built on. Enemies may walk on it.
- All prototype buildings have a one-cell footprint and no rotation.
- All solid structures block movement. They remain destructible attack targets. Units do not reserve tiles or block each other in this milestone; visual overlap is allowed and documented.
- A Keep is attacked from a cardinally adjacent cell; enemies never enter its occupied cell.
- Spawn capacity is unlimited at a cell in this prototype. Do not add collision avoidance or queues of physical units.

## Placement and economy

- Start each fresh run with 250 gold and the Keep at full HP. There is no income or kill reward in this one-wave milestone.
- Construction and demolition are permitted only during Day.
- A valid placement requires an in-bounds land cell, outside the reserved ring, no solid occupant, and sufficient gold. Enclosing the Keep with destructible buildings is permitted.
- Validate and purchase atomically: successful placement deducts the cost once and installs one building; a rejected placement changes neither balance nor occupancy.
- A selected construction tool remains active after placement. Right click or Escape cancels it. Left click with no tool selects an existing structure.
- Show a snapped ghost, cost, and validity reason. Invalid previews cannot become actual placements. Check again on click, not just when previewing.
- Demolition requires selecting a non-Keep structure and pressing a visible Sell button. Refund `floor(original purchase price * 0.5)` once. The Keep cannot be sold.
- Damage persists until destruction; no automatic repairs or rebuilding occur during Night.
- At zero HP remove the structure from occupancy immediately, then remove its visual. Demolition and combat destruction are separate events: destruction gives no refund.
- UI clicks never place buildings or select objects behind the UI.

## Initial combat tuning

Values below are a reproducible starting fixture, not a claim of balanced gameplay. Record any later tuning changes and rerun the acceptance fixtures.

| Entity | Cost | HP | Damage | Attack interval | Range | Movement |
|---|---:|---:|---:|---:|---|---|
| Keep | 0 | 500 | none | none | none | stationary |
| Wall | 20 | 100 | none | none | none | stationary |
| Archer Tower | 70 | 100 | 10 | 0.75 seconds | 4 tiles | stationary |
| Grunt | none | 30 | 10 | 1 second | cardinal neighbor | 1 tile/second |

- Tower range is inclusive Euclidean distance in world X/Z between tower tile center and current enemy position. Walls do not obstruct arrows. Height and line of sight are deferred.
- Towers choose the nearest living enemy in range; equal distance is resolved by ascending spawn ID. Hold a target while it remains valid. Reacquire when it dies or leaves range.
- Damage is immediate at an attack event; arrows are visual feedback only. A visual arrow arriving after its target dies causes no additional damage.
- A newly acquired attack target takes the full attack interval before the first hit. Losing or changing the target resets this timer; no stored attacks or catch-up burst after pause.
- Enemy attacks use the same full-interval rule after reaching an adjacent attack position. Grunts cannot damage structures diagonally.
- HP clamps to `[0, maximumHP]`. Each death resolves once. Dead entities neither attack nor remain valid targets.
- Use a fixed simulation step of 0.05 seconds. Release due spawns at the start of each step, then process route invalidation and movement, tower attacks, then surviving enemy attacks; evaluate defeat before victory at the end of that step. Release the time-zero spawn when entering Night, before its first movement step. Process entities in stable ID order within each category. Newly acquired targets get a due timestamp one full interval after acquisition, not a countdown already reduced by the acquisition step.
- Restrict attack intervals to positive values; invalid definitions fail validation before starting a run.

## Wave and run state

- States: `Day`, `Night`, `Victory`, `Defeat`. Pause is a separate boolean permitted only during Night.
- A fresh run begins at Day with only the Keep and no pending objects from an earlier run.
- Start Night is available only in Day. Repeated clicks cannot enqueue multiple waves.
- Main wave: ten Grunts from `(0,10)`, first spawn at Night elapsed time 0, then one every 1.5 seconds; final scheduled spawn at 13.5 seconds.
- Night ends in Victory only when spawning has finished, no spawn is pending, the living enemy count is zero, and Keep HP is above zero.
- Keep HP reaching zero causes Defeat even when the final enemy dies in that simulation step. Terminal states cancel pending spawns and stop further gameplay updates.
- No timeout silently removes stuck enemies. A missing route must surface a diagnostic and fail the relevant test.
- Pause stops spawning, movement, cooldowns, damage, and Night elapsed time. Selection, inspection, camera movement, Resume, and Restart remain usable.
- Restart is available in either terminal state and through an in-run menu. It resets gold, structures, entities, IDs, simulation timers, pause, selection, route caches, previews, and pending spawns, then enters Day.
- There is no reward phase in this milestone.

## Presentation and controls

- Fixed isometric orientation with an orthographic camera; allow pan and zoom, no rotation. Keep the map within reachable view and offer a Reset Camera button.
- Primitive meshes and simple URP materials must distinguish Keep, Wall, Tower, and Grunt by silhouette and color.
- Left click selects or places. Right click/Escape cancels the construction tool. `1` selects Wall; `2` selects Archer Tower. WASD/arrows pan; wheel zooms; Space pauses/resumes Night.
- Show gold, Keep HP, state, remaining/scheduled enemies, build buttons with prices, Start Night, Pause/Resume, Restart, selected-building HP, and Sell during Day.
- Show selected/preview tower range. Route previews must include a line and breach icon/label when relevant, not rely on color alone.
- Terminal overlay identifies victory or defeat and offers Restart. A stress/debug mode must have an explicit visible label and must not be confused with the main wave.

## Acceptance fixtures independent of routing policy

1. **Coordinates:** tile centers round-trip; world X = -0.01 and X = 20 are rejected; `(0,10)` accepts enemies and rejects building placement.
2. **Atomic spending:** place a Tower for 70, leaving 180; attempt an occupied or unaffordable placement and verify gold/occupancy remain unchanged.
3. **Refund:** sell that Tower for 35; repeated sell requests do not pay twice. Keep sale is rejected.
4. **Damage:** three Tower hits kill a full-health Grunt; ten Grunt hits destroy a full-health Wall; a destroyed entity resolves once.
5. **Range:** a target at exactly four tiles can be acquired; one beyond four cannot; a Wall between Tower and target does not block damage.
6. **Pause:** freeze midway through a spawn interval and attack cooldown; advancing real time does not change simulation state. Resume continues remaining simulation time.
7. **Completion:** a temporary zero living-enemy count between spawns does not win. After the final scheduled spawn is killed, victory occurs once.
8. **Defeat:** Keep destruction cancels scheduled spawns; a same-step final kill cannot override defeat.
9. **Restart:** restart after both outcomes and after pausing; verify the exact initial state and absence of old callbacks/entities.

## Routing policy

The owner chose open-route preference: any open route takes priority over breaking a structure, regardless of detour length. Do not introduce a walking-versus-breaching time comparison.

1. At a cell center, search cardinal neighbors for a shortest open route to any free cardinal neighbor of the Keep. Solid buildings and impassable terrain cannot be traversed in this search. No diagonal moves or corner cutting.
2. If an open route exists, follow it and ignore nearby infrastructure. On reaching a Keep neighbor, attack the Keep.
3. Otherwise search a breach graph. Land steps cost 1; entering a destructible solid building cell costs 6 (1 movement plus a fixed 5 breach penalty). The Keep and indestructible terrain are never traversable. A destructible occupied Keep-neighbor cell is an eligible breach goal.
4. Follow the selected breach route until cardinally adjacent to its first solid structure, attack that structure, and never walk into its occupied cell. Once destroyed, search again, starting with the open-route search. All destructible structures, including Towers and later economic buildings, use the same breach eligibility; only the Keep is the final target.
5. Choose minimum total cost, then goal `(z,x)` ascending. For reproducibility, visit neighbors North `(0,+1)`, East `(+1,0)`, South `(0,-1)`, West `(-1,0)`; order the search frontier by accumulated cost, then `(z,x)`, then insertion sequence. Keep the first predecessor on equal cost. Implement a simple uniform-cost search initially; A* or shared fields are optional measured optimizations, not requirements.
6. Every occupancy change increments one grid revision. Cached routes record the revision. Invalidate on the same simulation step as destruction/placement/demolition; reject dead attack targets before another hit. An enemy already moving between centers completes that walk segment, then replans at the next center (at most one second for a Grunt). Stationary enemies replan on the next simulation step.
7. If no breach route exists, mark the authored fixture invalid and report its spawn/goal coordinates. Do not teleport, delete, or silently leave the enemy idle to make a wave complete. The flat production fixture must always have a breach route.

The fixed penalty chooses a reproducible breach location; it does not model structure HP or time-to-destroy. Long-maze dominance is an explicit prototype playtest question.

## Route preview contract

- During Day, show the main spawn's current route on selection/hover and the candidate route while a valid construction ghost is present. Use the same route planner as enemies, with a temporary occupancy overlay that cannot mutate live state or cached live routes.
- Draw the open route to a Keep neighbor. For a breach route, draw only its reachable segment to the first blocker, mark that blocker with a breach icon and label, and optionally draw the remaining planned route dashed.
- An invalid ghost shows its rejection reason and leaves the committed route visible. A sealed fortress is legal and displays a breach warning, not a placement rejection.
- Update after preview cell changes, actual placement, demolition, or destruction. Do not recompute an unchanged preview every rendered frame.
- Actual routes must match the displayed route on an unchanged map; crowd overlap does not alter navigation in this prototype.

## Routing acceptance fixtures

10. **Detour:** on an otherwise empty map, place Walls `(5,9)`, `(5,10)`, `(5,11)`. A Grunt from `(0,10)` follows an open detour and never damages those Walls. The selected route remains identical across repeated queries.
11. **Full enclosure:** place Walls `(9,10)`, `(11,10)`, `(10,9)`, `(10,11)`. A route from `(0,10)` selects the west Wall `(9,10)` for breach. The Grunt attacks from `(8,10)`; no attack crosses a diagonal or empty gap.
12. **Destruction:** destroy `(9,10)` while that enemy attacks. On its next simulation step it rejects the dead target and takes the now-open route to `(9,10)` before attacking the Keep. No duplicate occupancy release occurs.
13. **Preview parity:** with a temporary ghost completing the four-Wall enclosure, preview and actual committed planning choose the same blocker; preview does not deduct gold, change occupancy/revision, or overwrite the committed route cache.
14. **Indestructible barrier:** in a test-only map, an impassable complete dividing barrier yields a validation error, not an attack target. Never enable Start Night on an invalid map.
15. **Moving enemy:** remove a blocker during an enemy's movement; it completes its current segment without teleporting, then replans within one tile's travel time.

## Performance fixture and gate

- Reference machine observed on 2026-10-15: AMD Ryzen 5 3600, NVIDIA GeForce GTX 1660 SUPER, 32 GiB RAM. This is a prototype reference, not a published minimum requirement.
- Measure a Windows x64 standalone non-development build at 1920 x 1080, VSync off, no frame cap, with the shipped prototype quality settings recorded. Editor FPS is diagnostic only.
- Stress fixture: same grid, 100 simultaneously active Grunts split evenly across the four entry points. Place 16 Walls on the perimeter of the square `8 <= x <= 12, 8 <= z <= 12` and 14 more at `(6,z)` for `3 <= z <= 16`. Place ten Towers at `(7,8)`, `(7,10)`, `(7,12)`, `(13,8)`, `(13,10)`, `(13,12)`, `(8,7)`, `(10,7)`, `(12,7)`, `(10,13)`. Keep and Walls are invulnerable only in this fixture until explicitly removed by the scripted event. Respawn killed Grunts to maintain 100 active, preserving their original entry assignment; no automatic target count inflation. Label debug mode clearly; normal-game definitions remain unchanged. Fixture setup bypasses purchase costs.
- Warm up for 10 seconds, then capture 60 seconds of frame times. Gate: median <= 16.7 ms, p95 <= 20 ms, p99 <= 33.3 ms. Record resolution, quality, build type, hardware, samples, percentiles, and any failing spikes. Define percentile as sorted sample at index `ceil(p * count) - 1`.
- Before measurement, record the first Wall blocker on the west entry's selected breach route as the event target. Include a scripted destruction event after 30 measured seconds that removes that Wall despite fixture invulnerability; record its frame time and verify route invalidation. Validate that the selected target still blocks an active attack immediately before the event, otherwise mark the performance run invalid. Do not count this scripted deletion as normal combat damage.
- Do not rewrite the architecture merely to chase Editor allocations or speculative scale. Profile a failing gate, then optimize the observed hotspot. A run on different hardware is useful evidence but does not claim the reference gate passed.
- Normal-wave proof: record one winning layout with its placement list and one losing layout (Keep-only is acceptable). Preserve these as reproducible fixtures after initial tuning.

## Delivery evidence

Supply passing EditMode and PlayMode test results, a Windows build, screenshots of open-route and breach previews plus a terminal overlay, the winning/losing layout records, and the stress measurement. Report blockers or unmeasured gates explicitly. Passing automated tests is not evidence that the game is fun: ask playtesters whether the route was predictable and whether they can explain the outcome before moving to the MVP.
