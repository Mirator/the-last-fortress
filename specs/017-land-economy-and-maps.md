# Feature spec 017: land economy and map overhaul

Version 0.5, 2026-10-30. Status: **APPROVED 2026-10-30.** Owner decisions 2026-10-30: D1-a, D5-a, D6-a, D7-a, D9-a; D2-a, D3, D4-a, D8 and D10 as recommended, confirmed with plan 037; v0.3 adds the owner's answers on how tiers and perks combine with D3; v0.4 adds how nights use 3-5 entries, the fairness values and the four recipes; v0.5 records the owner loosening D10. Builds on spec 016 (raids, gold veins, the Mine), whose pressure gate it takes over.

Changes `002-game-rules.md` (v1.21 once spec 016 is merged), sections 2, 5, 6, 7, 9, 11, 12, 13 and 16, under `release-rules-4`. Legacy profiles are unchanged.

## 1. Problem

Spec 016's probe (plan 036) showed what wins: a compact sealed ring. Houses on the Keep's neighbours and Archer Towers around them win 5/5, and harder waves only widen the lead. It wins because:
- every breach point is within about two tiles of the Keep, so almost every tower covers all of them;
- a House earns the same gold next to the Keep as anywhere else, so the safest place is always the best place;
- the maps hardly change this. Every profile is four roads in an X to a central Keep, with a few forest and rock cells varied. The vision itself says "random terrain alone is not variation".

The games closest to this one make income depend on land:
- In They Are Billions you must expand for resources.
- Kingdom Two Crowns' farms work the land outside the walls.
- Bad North's and Thronefall's houses pay only if they survive.

Wealth then sits away from the core, and the player chooses how far to reach and how to protect it. This spec does the same, and gives the maps the land features that decide where wealth is.

## 2. Goals

1. A rich economy needs open land, so it cannot fit inside a compact ring (pillar 4: economy now against safety now).
2. Each map decides where the land is, so different maps call for different layouts (pillar 5, the vision's "different maps must change which strategies work").
3. Sealing and breaching stay as they are (the central mechanic). Raids, veins and the Mine stay.

## 3. Decisions (2026-10-30: D1, D5, D6, D7 and D9 decided; the rest as recommended, to be confirmed with the plan)

**Land economy**

- **D1: The land rule.**
  - (a) *Recommended:* a House or Farm earns a small base plus gold per **free fertile cell** touching its footprint by an edge. A free fertile cell has no building and no trap; enemies walking over it do not matter. One rule for both kinds.
  - (b) As (a), but any open land counts, with no fertile terrain. The maps would matter less.
  - (c) Only Farms use land; Houses keep a flat income. Houses would still seal the Keep for free gold.
- **D2: Shared fields.** Can one free cell feed several buildings?
  - (a) *Recommended:* yes, each building counts its own neighbours. This is the simplest to read, and spacing still matters, because a packed ring has no free cells at all.
  - (b) Each cell feeds one building (the lowest ID). This is stricter but hard to show.
- **D3: Values [P].**
  - Starting points: House 4 + 2 per fertile neighbour, at most 4 sides (4–12). Farm 8 + 3 per fertile neighbour, at most 8 cells around a 2 × 2 (8–32).
  - Tiers, Market Square, Efficient Farming and the Farm + House bonus are re-expressed on top of these. The probe tunes them.
  - v0.3 (owner, 2026-10-30), how they combine:
    - House = 4 + 2 per free fertile neighbour + tier (+5 at II, +10 at III) + Market Square (+1 more per free fertile neighbour; it no longer rewards a House next to a House).
    - Farm = floor((8 + 3 per free fertile neighbour + tier (+5 / +10) + Farm + House link (+5, kept) + Market hall (+5)) × (100 + Efficient Farming 30%) / 100).
    - Tier bonuses stay flat. Every value is [P].
- **D4: Preview.**
  - (a) *Recommended:* by Day, a ghost House or Farm shows its counted fertile cells and its expected income, and the selection panel shows the same for a placed building.

**Maps**

- **D5: Board size.**
  - (a) *Recommended:* stay 20 × 20 for this milestone. The size is a constant in 68 places, the camera already caps at the whole board, and you asked to see the whole map.
  - (b) 24 × 24: more land for spreading out, but every test fixture moves and the camera must show more.
- **D6: Keep position.**
  - (a) *Recommended:* it varies by map, anywhere in the central 8 × 8 area. A Keep against a river or a ridge has fewer open sides, which changes the defence.
  - (b) It stays at (10,10), which is cheaper (it is used in 28 places).
- **D7: Entries and roads.**
  - (a) *Recommended:* each map generates 3–5 entries anywhere on the boundary ring, at least 4 cells apart, with roads to the Keep. The campaign table names entries by index, opened night by night as now.
  - (b) The four side midpoints stay, and only the terrain between them changes.
  - v0.4 (owner, 2026-10-30): the table's W, E, N, S are entry indices 0-3. On a map with k entries a night keeps its entry count, capped at k; an index the map lacks is replaced by its lowest unused index; Nights 8-12 open every entry (so a 5-entry map gets its fifth front from Night 8, a 3-entry map peaks at 3).
- **D8: How maps are generated.** Feature recipes replace the skeleton-plus-variation profiles.
  - Each recipe places regions in turn: fertile meadows, forest belts, rock ridges, rivers with fords, then the Keep, entries and veins. Every map is validated with the real route planner.
  - Fairness checks: fertile cells within a band, Keep-to-entry distances within a band, at least one choke and one open approach.
  - *Recommended:* 4 recipes for this milestone, for example River Valley, Highlands, Forest Edge and Open Plains. The current three profiles stay only for older saves.
  - v0.4 (owner, 2026-10-30): the four recipes are River Valley (a river with 2-3 fords splits the board, meadows along it), Highlands (rock ridges make chokes, small meadows between), Forest Edge (a forest belt on one side, meadows on the far side) and Open Plains (wide meadows, few blockers).
  - v0.4 (owner) fairness values [P]: 50-110 fertile cells; each entry's open Day-1 route 9-22 steps; at least one entry route passes a choke (land at most 2 wide across the route); at least one has 3+ land cells on each side for most of its length; the Keep has at least two sides clear of forest, rock and water for 3 cells.
- **D9: Rules version.**
  - (a) *Recommended:* change `release-rules-4` in place, as for specs 012–016. Saves store their terrain, so older runs keep their maps. Their income switches to the land rule. A save without a fertile list treats every free land cell as fertile (the D1-b rule), so an older run is not starved.
  - (b) A new `release-rules-5`. Older runs keep today's rules exactly; it costs one more legacy profile.
- **D10: The acceptance gate** (takes over spec 016 D5).
  - Seeds 1–5 on each recipe, active hero, with the adaptive policy scripts (section 4).
  - house-ring wins at most 2 of 5 on every recipe.
  - At least two other strategies win at least 3 of 5 on every recipe, and the strategy that wins most differs between at least two recipes.
  - Up to 4 tuning rounds, each a section 16 row. If the gate still fails, stop and report.
  - v0.5 (owner, 2026-10-30): the gate failed after 4 rounds (plan 037 stage 6). After round 3, house-ring won at most 2/5 on every recipe. A second strategy reached 3/5 only on River Valley and Forest Edge. Kill-zone, a Wall-sealed Keep, won 5/5 on every recipe. **The owner accepted this partial result and loosened the gate**: the round 3 values are adopted, the sealed Keep is left for a later spec, and the plan continues to presentation and close.

## 4. Consequences the plan must cover

- **Adaptive policy scripts.** Today's scripts place buildings on fixed cells, which is meaningless on generated maps with a moving Keep and entries. They must become small heuristic builders:
  - kill-zone: seal the Keep, ring it with Towers;
  - corridor: towers and traps along the main route;
  - economy: Farms and Houses on the best fertile clusters, Mines on veins, defence after;
  - house-ring: your layout, kept as the control.

  They check viability only; they are not balance claims.
- **Presentation.**
  - Fertile ground needs a clear look (tilled meadow and grass colour), visible by Day and Night.
  - Roads follow the generated paths.
  - The camera starts on the whole board, which also covers a playtest note.
- **Saves.** The terrain gains a fertile-cell list, entries and the Keep cell. These fields are optional, with older defaults. Schema 2 is unchanged unless D9-b.

## 5. Out of scope

Board sizes beyond D5, new enemy types, a wave director, keystone perks, and the playtest polish batch. Fire and siege engines were considered and not chosen.

## 6. Risks

- **Too many moving parts at once.** A new economy, new maps and new scripts all change the probe. Mitigation: land a stage at a time and re-run the probe after each (plan 037).
- **Readability.** Players must see why a building earns what it does. D4 is the mitigation.
- **Scripts versus players.** Heuristic scripts can make a strategy look worse than a player would. The owner's live check across several seeds is the acceptance that matters.
