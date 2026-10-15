# The Last Fortress — Game Design Document

Source: https://chatgpt.com/share/6ad07d7a-e294-83eb-b843-7f391179455c

(GDD v1.1, revised 2026-10-15) Single-player · Strategy / Roguelite / Base Defense · PC / Steam

## Document scope and implementation order

This document describes the full game vision. It does not authorize implementing every feature at once.

1. **Navigation prototype:** one authored map, one wave, Keep, Wall, Archer Tower, Grunt. Implement [002-navigation-prototype.md](002-navigation-prototype.md) first, following [the implementation plan](../plans/001-navigation-prototype.md).
2. **Six-night MVP:** six buildings including the Keep, four enemy types including the final boss, ten perks, and one biome. Its scope and system rules are in [003-six-night-mvp.md](003-six-night-mvp.md). Begin only after the navigation prototype passes its checks and playtesting supports further investment.
3. **Full release:** twelve nights and the expanded content targets in section 15. These remain direction, not immediate implementation work.

The milestone-specific specification takes precedence for its milestone. For example, the prototype has no reward phase or passive income; the MVP has no soldiers or activated abilities. Owner-confirmed decisions: open routes take priority over breaching, the later economy has guaranteed Keep income, and controllable soldiers are deferred beyond the MVP. Numerical values are initial tuning defaults, not validated balance.

*Visual reference search terms: minimalist isometric low poly medieval fortress on small grassy island surrounded by tiny enemies twilight indie strategy game aesthetic Thronefall; stylized minimalist medieval village palisade towers warm torchlight isometric game environment; top down isometric minimalist fantasy forest fortress tiny army battle stylized low poly indie game.*

Visual references for mood and art direction. Not actual gameplay images.

**Core concept:** Build a fortress during the day, defend it at night, and survive long enough to defeat the final invasion. Unlike traditional tower defense games, every structure changes the battlefield. Walls redirect enemies, buildings create chokepoints, and the positioning of your economy determines how your defenses function.

Target session

**25–40 minutes**

Target platform

**Steam / Windows**

Camera

**Fixed isometric**

Business model

**Premium, €9.99–14.99**

Team

**1–3 developers**

MVP target

**3–5 months**

Planning assumptions and targets, not validated production estimates.

## 1. Game vision

The Last Fortress is a minimalist real-time strategy roguelite in which the player transforms a vulnerable settlement into a defensible fortress over a series of increasingly dangerous nights.

The game combines the immediate clarity of Thronefall, the spatial decision-making of Into the Breach, and the defensive planning of Kingdoms and Castles.

### Design pillars

1. **Every building is a tactical decision.** Placement matters as much as the building's function.
2. **Simple controls, deep consequences.** The player should learn the basics in two minutes but discover new strategies after hours.
3. **Readable battles.** Hundreds of enemies are visually understandable because combat uses clear silhouettes and limited effects.
4. **Meaningful risk and reward.** Investing in economy makes later nights easier but leaves the fortress vulnerable now.
5. **Short, replayable runs.** Random maps, enemy compositions, upgrades, and building synergies make every game different.

## 2. Core gameplay loop

### Day phase

The player receives income, can build and sell, and can inspect the next attack. Direct building upgrades and soldier repositioning are full-release features; MVP perks modify buildings without individual upgrade tiers.

There is no countdown during construction. Players can plan at their own pace.

### Night phase

The player starts the enemy wave manually. Buildings attack automatically, and enemies attempt to reach and destroy the Keep. Soldiers executing assigned orders are a future full-release addition.

Construction, demolition, and repairs are disabled during combat, including while paused. Unit orders and activated abilities are deferred beyond the MVP.

### Reward phase

After surviving a non-final night, the player chooses one of three randomly offered perks. These may enhance the economy or alter combat mechanics. The final night leads directly to victory; defeat takes priority when the Keep is destroyed in the same simulation step as the final kill.

The sequence repeats until the player wins or the Keep is destroyed.

## 3. Map and building system

The battlefield is a compact, procedurally generated island represented by a square grid. The camera stays fixed in a three-quarter isometric view.

### Starting map

- Prototype and MVP playable area: 20 × 20 tiles. Expansion to 28 × 28 is a full-release option, not an MVP requirement.
- Central Keep, surrounded by several initially empty tiles.
- MVP terrain includes land and impassable forests, rocks, and water. Elevation and ruins are deferred beyond the MVP.
- Prototype enemies enter from one marked edge. The MVP has four marked entry cells, with one to four active directions according to its wave table.
- All building positions snap to grid cells.

### Map deliverables

The prototype uses the exact authored flat map in specification 002. The MVP adds validated seeded variation under specification 003. An illustrative map is not an implementation requirement; authored fixture coordinates and generation validation are.

### The central mechanic: Spatial consequences

Buildings do not merely occupy space. They alter enemy navigation, influence neighboring structures, and create new tactical situations.

**Example:** A player places two walls to form a narrow corridor, then builds an Archer Tower overlooking it. Later, the player upgrades the corridor with spikes. A previously dangerous group of enemies becomes manageable, but flying enemies ignore the corridor entirely.

This creates strategic depth without requiring complex controls.

### Building roster across milestones

The MVP includes only Keep, House, Farm, Wall, Archer Tower, and Spike Trap. The other buildings below are deferred full-release candidates.

| Building | Initial cost | Function |
|--------|--------|--------|
| Keep | Free | Main base, must survive |
| House | 40 gold | \+15 gold/day |
| Farm | 60 gold | \+25 gold/day |
| Wall | 20 gold | Blocks enemies |
| Archer Tower | 70 gold | Single-target ranged damage |
| Barracks | 90 gold | Produces 3 soldiers |
| Spike Trap | 35 gold | Damages ground enemies |
| Cannon Tower | 130 gold | Area damage, slow fire |
| Watchtower | 50 gold | Extends vision and attack range of adjacent towers |
| Repair Station | 85 gold | Repairs nearby structures between waves |

Values are initial tuning assumptions.

### Building synergies

The MVP includes only Farm + House and Wall + Spike Trap, with adjacency and non-stacking rules in specification 003. Watchtowers, Barracks, Cannon Towers, elevation, and their synergies are deferred beyond the MVP. There is no fog of war or vision-radius system in the prototype or MVP.

Synergies should be simple enough that the player can understand them immediately.

- **Archer Tower + Watchtower:** \+25% attack range.
- **Farm + House:** Farm generates +5 additional gold per day.
- **Wall + Spike Trap:** Enemies attacking the wall take reflected damage.
- **Barracks + Watchtower:** Soldiers gain extra vision and defensive range.
- **Cannon Tower + elevated terrain:** Increased blast radius.

Each synergy is shown through simple UI highlighting when selecting a building.

## 4. Economy

The economy uses a single currency: **Gold**.

For the MVP, the Keep provides 20 gold per new day, Houses and Farms add income, and the final boss pays no gold. Kill rewards and additional economic systems are deferred. The one-wave prototype has a fixed construction budget and no income.

The player must balance early investment against immediate defenses.

### Starting conditions

- Starting gold: 250
- Starting buildings: Keep only
- Initial Keep HP: 500
- Income paid at the beginning of each new day
- MVP Keep income: 20 gold/day, beginning on Day 2
- Unspent gold carries forward
- Demolishing a building refunds 50% of its actual purchase price, rounded down; combat destruction gives no refund unless a perk explicitly grants one

For the first day, the player receives no passive income.

MVP Houses occupy 1 × 1 cell; Farms occupy 2 × 2 cells of buildable land. Farms are more capital-efficient but require more defended space. See specification 003 for repairs, income snapshots, refunds, and modifiers.

### Strategic trade-offs

An economic opening produces more gold later but increases the risk of losing on early nights. A defensive opening increases survival odds initially but risks falling behind enemy scaling.

The goal is to make both strategies viable depending on the map and the upcoming threats.

## 5. Combat system

Combat occurs in real time with automatic attacks and direct control over soldier positioning.

### Enemy AI

Enemies select a target and navigate toward it using the current state of the map.

Ground enemies first take a shortest open route to a cardinal neighbor of the Keep. Any open route is preferred, even if breaching would be faster. When none exists, they choose a route through destructible structures and attack its first blocker. Indestructible terrain cannot be attacked. Route choice, tie-breaking, destruction invalidation, and preview parity are specified in document 002 and retained for the MVP.

Pathfinding must update when walls or structures are destroyed.

### Enemy types

| Enemy | Behavior | Threat |
|--------|--------|--------|
| Grunt | Basic melee attacker | Low |
| Runner | Fast, low health | Medium |
| Brute | Slow, high health, destroys walls | High |
| Archer | Attacks from range | Medium |
| Flyer | Ignores ground obstacles | High |
| Bomber | Explodes against structures | High |
| Commander | Buffs nearby enemies | High |
| Siege Golem | Boss enemy, specializes in breaking fortifications | Extreme |

### Soldiers

Soldiers and Barracks are deferred beyond the MVP. For a later soldier milestone, the proposed orders are Hold Position, Move, and Attack Target. Patrols, formations, equipment, replacement costs, and persistence require a separate specification before implementation.

### Combat philosophy

The player should win primarily through *preparation*, not through fast clicking. Combat abilities and unit orders offer limited opportunities for tactical intervention without undermining fortress construction.

## 6. Enemy waves and difficulty scaling

A full-release run lasts 12 nights. The prototype lasts one wave, and the MVP lasts six nights with a Siege Golem on Night 6. Each later night should introduce additional pressure through enemy quantity, composition, or attack direction.

**Illustrative enemy strength progression** (Relative threat points per night, not literal enemy counts.)

| Night | Threat points |
|---:|---:|
| 1 | 10 |
| 2 | 15 |
| 3 | 22 |
| 4 | 31 |
| 5 | 43 |
| 6 | 60 |
| 7 | 82 |
| 8 | 112 |
| 9 | 151 |
| 10 | 201 |
| 11 | 265 |
| 12 | 350 |

### Full-release wave progression (not MVP)

| Nights | Enemy progression |
|--------|--------|
| 1–2 | Basic grunts, single attack direction |
| 3–4 | Runners and split attacks |
| 5–6 | Brutes, first miniboss |
| 7–8 | Ranged enemies, flying enemies |
| 9–10 | Bombers, commanders, complex multi-directional attacks |
| 11 | Combined assault testing multiple defense types |
| 12 | Siege Golem boss and supporting army |

The prototype and MVP use authored waves first. Threat-budget generation is deferred until those provide a tested balance baseline. Future generation must specify enemy costs, progression restrictions, count caps, leftovers, and spawn cadence rather than treating the illustrative threat curve as an executable algorithm.

The prototype and MVP preview exact counts, types, directions, and spawn timings during Day. That stored plan is executed during Night without rerolling. Less exact previews are a later design option.

## 7. Roguelite progression

After each surviving non-final MVP/release night, the player chooses one of three distinct eligible perks. There is no reward selection after the final victory.

### Full-release perk examples

Soldier and Cannon perks below are future candidates, not MVP content. The exact ten MVP perks and eligibility/stacking rules are in specification 003.

| Upgrade | Effect |
|--------|--------|
| Reinforced Timber | Walls gain +30% HP |
| Military Academy | New soldiers gain +20% attack |
| Efficient Farming | Farms produce +30% gold |
| Flaming Arrows | Archer Towers apply burn damage |
| Last Stand | Towers deal +40% damage when Keep HP is below 30% |
| Recycling | Destroyed buildings refund 25% of their original cost |
| Expanded Vision | All ranged buildings gain +10% range |
| Veteran Defenders | Soldiers surviving 3 waves gain a permanent combat bonus |
| Explosive Barrels | Cannon Towers have a chance to trigger a second explosion |
| Fortress Heart | Keep gains +150 maximum HP |

### Upgrade design rules

- No upgrade should be strictly required to win.
- Players should be encouraged to create builds based on available upgrades.
- Some upgrades should enable distinctive strategies, not merely increase statistics.
- Upgrade choices should be weighted to avoid offering irrelevant combinations too frequently.

### Meta-progression

Full-release progression may unlock new building variants, perk types, maps, and difficulty modifiers. The prototype and MVP have no unlock grind; the entire milestone roster is available from a fresh profile.

Permanent progression should unlock strategic options rather than provide large unconditional stat increases. A skilled player should be able to win with the initial unlock set.

## 8. Victory and defeat

**Victory:** Resolve all scheduled spawns and defeat every enemy, including the Siege Golem, on the final night: Night 6 for the MVP, Night 12 for release. The one-wave prototype has no boss.

**Defeat:** Keep HP reaches zero. Defeat takes precedence over same-step victory. Terminal states stop gameplay and cancel pending spawns.

After each run, the player receives a summary containing nights survived, enemies defeated, buildings constructed, gold earned, and strongest synergies.

Optional score-based challenges reward efficient play and create replay value.

The player may retry the same map seed or start a new procedural map.

## 9. Controls and UI

The game is designed primarily for mouse and keyboard.

| Input | Action |
|--------|--------|
| Left click | Select and place buildings |
| Right click / Escape | Cancel construction tool; unit commands are deferred |
| WASD / Arrow keys | Move camera |
| Mouse wheel | Zoom |
| Space | Pause / Resume during combat |
| 1–2 prototype; 1–5 MVP | Select a purchasable building from toolbar |
| Tab | Future detailed combat overlay; not required for prototype/MVP |

### HUD

The default HUD displays gold, income per day where applicable, Keep HP, current night, upcoming enemy threats, and building shortcuts. Fixed isometric means fixed orientation: camera pan and zoom are allowed, rotation is not.

The interface should minimize clutter by exposing building-specific statistics on selection or hover.

Important information—including attack routes, tower ranges, and available synergies—must be visually understandable without reading detailed tooltips.

## 10. Visual direction

The visual style is minimalist, colorful, and readable, with low-poly 3D assets viewed through an orthographic isometric camera.

The initial visual proposal, palette, asset silhouettes, HUD direction, and generated reference graphics are in [design/README.md](../design/README.md). Treat the images as mood/modeling references; milestone specifications still define map geometry, footprints, and content scope.

*Visual reference search terms: minimalist isometric low poly medieval miniature wooden palisade fortress green grass sunny day indie game art style.*

Daytime: warm, readable, inviting

*Visual reference search terms: stylized low poly miniature medieval castle defending against monsters at night purple blue lighting orange torch glow isometric strategy game art.*

Nighttime: darkened environment, strong contrast

### Asset principles

- Use simple geometric shapes and consistent silhouettes.
- Avoid detailed character faces and high-resolution textures.
- Buildings should be identifiable from their shape alone.
- Enemy classes must be distinguishable at low zoom.
- Damage feedback uses cracks, particles, health bars, and controlled camera shake.
- Effects should emphasize meaningful attacks rather than fill the screen with particles.

Terrain uses flat or lightly textured materials with subtle color variation. Buildings consist of reusable modular meshes.

Animation uses simple skeletal rigs or procedural motion wherever practical.

## 11. Sound design and music

Music transitions between calm daytime themes and tense nighttime combat.

Sound effects communicate combat clearly: arrows, explosions, wall damage, structure destruction, building placement, and incoming attack warnings.

Enemy sounds should allow the player to distinguish a large approaching threat without constantly watching every map edge.

The game should remain playable with music disabled.

## 12. Technical architecture

### Engine

**Project decision:** Develop The Last Fortress in **Unity 6.3 LTS (6000.3.16f1)** using C#. The Unity project is in `game/The Last Fortress/`.

Use the Universal Render Pipeline (URP) for the fixed isometric 3D presentation. Keep building placement and enemy routing on a logical grid so fortress layouts can change paths predictably.

### Main systems

| System | Responsibility |
|--------|--------|
| Grid manager | Tile occupancy, terrain, building placement |
| Navigation manager | Enemy routes and dynamic obstacle updates |
| Building system | Construction, upgrades, damage, destruction |
| Economy manager | Income, purchases, refunds |
| Wave director | Enemy spawning and threat-budget generation |
| Combat manager | Damage, targeting, attack events |
| Upgrade manager | Perks, synergies, unlocks |
| Run manager | Phase transitions, victory, defeat |
| Save manager | Settings, unlocks, progress between days |
| UI manager | HUD, tooltips, menus, build controls |

### Critical technical risk: Pathfinding

Dynamic pathfinding is central to the game's identity. Walls and structures must modify navigation reliably without causing frequent recalculation spikes.

Use the grid as the source of truth, with deterministic shortest-path queries and revision-based invalidation as specified in document 002. Start with a simple uniform-cost search on this small board. Adopt A* or shared flow fields only if measured performance warrants them; installing AI Navigation does not require using NavMesh for gameplay routing.

For the MVP, limit simultaneous enemies to approximately 100 and introduce larger waves only after performance profiling.

### Other technical constraints

- Deterministic seeded map generation where practical.
- Data-driven definitions for buildings, enemies, and upgrades.
- Object pooling for projectiles and transient effects.
- Automated validation of generated maps and enemy entry points.
- Simple save structure separating unlocks from current-run state.

## 13. MVP scope

The MVP should prove that building placement is interesting enough to sustain repeated runs.

**Six-night MVP checklist**

- [ ] Navigation prototype accepted before content expansion.
- [ ] Six buildings: Keep, Wall, Archer Tower, House, Farm, Spike Trap.
- [ ] Four enemy types: Grunt, Runner, Brute, Siege Golem (boss included in count).
- [ ] Six authored waves; final boss and support army on Night 6.
- [ ] Ten compatible perks; five reward choices per run.
- [ ] One biome, an authored fallback map, and validated seeded map variation.
- [ ] Income, repairs, demolition, pause, victory/defeat, summary, same-seed retry.
- [ ] Day-boundary save/resume and basic settings.
- [ ] Required tests, Windows build, route readability, and performance measurements.

The MVP excludes multiplayer, story campaigns, heroes, soldiers/Barracks, active abilities, Cannon/Watch/Repair towers, individual building upgrade tiers, fog of war, elevation, unlock progression, challenges, and complex terrain physics.

## 14. Development milestones

| Milestone | Deliverable | Target |
|--------|--------|--------|
| Navigation prototype | One authored wave, grid placement, walls, towers, breach routing, Keep destruction | Weeks 1–3 |
| Economy increment | Houses, Farms, base income, repairs, multiple authored waves | Weeks 4–6 |
| Vertical slice | Six-night playable loop, traps, boss, ten perks | Weeks 7–10 |
| Content MVP | Validated procedural variation, save/resume, replay and summary | Weeks 11–14 |
| Polish and testing | UI, balancing, optimization, sounds, demo | Weeks 15–20 |

This schedule is aggressive and assumes a developer familiar with the chosen engine, existing asset libraries, and minimal custom tooling.

### Vertical slice acceptance criteria

The vertical slice is successful if:

- Players understand construction and combat without a long tutorial.
- Building placement changes enemy behavior in a visible and predictable way.
- At least two distinct viable fortress strategies emerge.
- A six-night run takes approximately 15–20 minutes.
- Test players voluntarily restart after losing.
- The game meets document 002's standalone-build reference performance gate under representative combat loads; rerun the measurement after adding MVP content.

If these conditions are not met, expand or revise the central mechanic before adding significant content.

## 15. Full release scope

The intended version 1.0 expands the MVP without fundamentally changing the design.

| Area | MVP | Version 1.0 |
|--------|--------|--------|
| Maps / biomes | 1 | 4 |
| Buildings, including Keep | 6 | 15 target |
| Enemy types, including bosses | 4 | 10 target |
| Bosses, subset of enemy types | 1 | 4 target |
| Perks | 10 | 50 |
| Nights per run | 6 | 12 |
| Difficulty modes | 1 | 4 |
| Challenges | 0 | 20+ |

Potential post-release content includes daily seeded challenges, additional map modifiers, new enemies, and alternate final bosses.

Co-op and competitive multiplayer are not planned for version 1.0.

## 16. Balancing model

Difficulty should not depend solely on increasing enemy HP and damage.

There are four tuning levers:

- **Quantity:** Number of enemies in each wave.
- **Composition:** Types of enemies and the counterplay required.
- **Direction:** Number and position of attack routes.
- **Economy pressure:** How much players must invest to maintain survivability.

The wave director calculates a total threat budget. Each enemy type has a cost, and the director purchases enemies within that budget while respecting progression rules.

For example, a wave with a threat budget of 30 might contain ten Grunts costing 1 each, four Runners costing 2 each, and two Brutes costing 6 each.

The initial threat budgets and building costs must be tuned through simulations and playtesting rather than treated as final values.

## 17. Commercial positioning

### Core selling proposition

*Build your fortress. Shape the battlefield. Survive the night.*

The Steam page should emphasize the visible transformation of a small settlement into a sophisticated defensive structure, rather than presenting the game as a generic tower defense title.

### Target audience

Players who enjoy accessible strategy, tower defense, roguelite progression, compact city builders, and replayable 20–40 minute sessions.

### Main commercial risks

**Genre saturation:** A simple medieval defense game has many competitors. Dynamic battlefield manipulation must be clearly visible in trailers and screenshots.

**Limited strategic depth:** If placing walls and towers quickly becomes formulaic, replayability will suffer.

**Over-scoping:** Introducing too many buildings, enemy types, or systems before validating the fundamental loop could substantially extend development.

**Weak procedural variation:** Randomizing terrain alone is insufficient. Different maps must meaningfully change viable strategies.

## 18. Success criteria

The first priority is not content quantity. It is validating the unique mechanic.

The project should proceed toward full production only after playtests show that:

1. Players understand how buildings influence enemy paths.
2. Repositioning and building selection create meaningful decisions.
3. Multiple strategies can beat the same map.
4. Players feel responsible for both their victories and defeats.
5. The game remains enjoyable across repeated runs.

The most important design question is:

**Can a player create a clever fortress layout, watch enemies interact with it, and feel that their planning—not random upgrades or raw statistics—won the battle?**

If yes, the concept has a foundation worth expanding.

---

## Next implementation milestone

Before implementing all ten building types, prototype just **the Keep, Walls, Archer Towers, and enemy pathfinding** on a single small map.

The most distinctive mechanic is not the day/night cycle or the economy. It is the interaction between fortress layout and enemy navigation.

That mechanic should be fun even without procedural generation, upgrades, or progression. If it is, the remaining systems can reinforce it.

The technical prototype specification is [002-navigation-prototype.md](002-navigation-prototype.md). Execute [plan 001](../plans/001-navigation-prototype.md) before implementing the MVP rules in [003-six-night-mvp.md](003-six-night-mvp.md). Treat the stated production schedule and content totals as planning targets, not implementation acceptance criteria.
