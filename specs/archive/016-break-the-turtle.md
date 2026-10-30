# Feature spec 016: break the turtle (raids and gold veins)

Version 0.3, 2026-10-29. Status: **PARTLY IMPLEMENTED; raids, veins and the Mine merged into `../002-game-rules.md` v1.21 (2026-10-30); archived.** Not current: plan from `002-game-rules.md`. Approved by the owner on 2026-10-29 (D1-a, D2-a, D3-a, D4–D7 as written; v0.3 adds the owner's answers to four follow-up questions, marked "v0.3"). The D5 pressure gate was not reached and is superseded by spec 017 D10 (owner, 2026-10-30); see section 7.

Changes `002-game-rules.md` v1.20, sections 5, 6, 7, 8, 9, 11, 12, 13 and 16, under `release-rules-4`. Legacy profiles are unchanged.

## 1. Problem

The owner's 2026-10-29 playtest ran twelve nights with one layout:
- Houses seal the Keep;
- Archer Towers ring the Houses;
- the Keep took no damage (650/650 on Night 10).

Walls, Spike Traps, Cannons, Watchtowers and Repair Stations went unused. The owner's verdict: "I do the same thing all the time." The 2026-10-16 tuning row already recorded a sealed Keep winning every seed.

There are three causes:
1. **A House is a better Wall.**
   - It is solid like a Wall and earns 10 gold a day. That pays back its extra 14 gold over a Wall in about 1.4 days.
   - With routing rule 2, an enemy on an open route ignores nearby buildings. A House next to the road is therefore never at risk.
   - So economy and defence are the same purchase, and pillar 4 ("investing in economy leaves the fortress weaker now") does not hold.
2. **No reason to build away from the Keep.** Income is the same on every cell, so the tightest ring is always best.
3. **No pressure.** With no risk, no decision matters. Raising enemy counts alone would only make the same ring bigger.

This spec gives economy buildings a cost in danger (raids) and a reason to stand outside the ring (gold veins), then tunes the waves so the old ring is no longer safe.

## 2. Decisions (approved 2026-10-29: the recommended options)

- **D1: Raids.** Ordinary melee enemies (Grunt, Runner, Brute) attack economy buildings beside their route.
  - (a) *Recommended:* at a fresh cell-centre decision, an enemy with a living House, Farm or Mine on a cardinal neighbour stops and attacks it. It moves on when that building falls or after a raid time of 5 s [P], whichever comes first. It raids at most once per night. This mirrors the Bomber's early fuse rule.
  - (b) Raids last until the building falls. A ring of Houses then becomes a breach point on every side, which is much harder.
  - (c) No raids; instead, economy buildings may not stand within 2 cells of the Keep. This is a simpler rule, but it reads as arbitrary.
  - Not raiders: Bombers and Raider Archers (they keep their own targeting) and bosses (they go for the Keep).
- **D2: The building for gold veins.**
  - (a) *Recommended:* a new building, the **Mine** (1×1, solid, only on a vein cell, 45 gold [P], HP 64 [P], 30 gold a day [P]). It has a clear identity and leaves Houses and Farms as they are.
  - (b) No new building: a Farm with a cell on a fertile patch earns double. There is no new art, but the Farm's 2×2 footprint makes the patches hard to place.
- **D3: Vein placement.**
  - (a) *Recommended:* 3 veins per map, seeded. Each is at Chebyshev distance ≥ 5 from the Keep and within 2 cells of an entry's open route at Day 1. They are in different quadrants where the profile allows, and never on fords, the boundary ring or a profile's named constraint.
  - (b) 4 veins, one per quadrant: more symmetric, so less variety between seeds.
  - Veins are stored with the terrain. A vein cell is buildable land; any other building may stand on it, but then it earns nothing extra.
  - v0.3 (owner): "within 2 cells" stays. A Mine two cells from the route is out of melee raid reach and is threatened only by Raider Archers (range 3.5, from Night 5); accepted.
- **D4: Mine upgrades.** Tier II +10 a day; tier III +20 a day, and the **Stockade** trait: raid damage to the Mine is halved. Price factors as in section 7.
- **D5: Pressure and the balance gate.** This replaces the "do not make it easier" gate of spec 015.
  - A new **house-ring** policy script reproduces the owner's layout: Houses seal the Keep, Towers ring them, no Mines.
  - Accept the change only if, with the active hero, seeds 1–5 on their own orientations:
    - house-ring wins at most 2 of 5;
    - at least two other strategies each win at least 3 of 5, one of them using at least two Mines;
    - in every won run, on at least one night from Night 6 on, the Keep loses HP or a building is destroyed (v0.3, owner).
  - Knobs, in this order: the wave counts for Nights 4–12, enemy HP (one percentage per kind), raid time, Mine income and price. At most 4 tuning rounds, each a full probe; each is a section 16 row.
  - v0.3 (owner): the spawn cap may rise. Night 12 already has 91 spawns of the 100 allowed per night. The per-night spawn limit of section 9 becomes 150 [I]; the living-enemy cap of section 4 stays 100 [I], so extra spawns wait FIFO as now.
- **D6: Rules version.** Change `release-rules-4` in place, as for specs 012–015. Old saves have no veins in their stored terrain and load without them; raids apply from their next night.
- **D7: Scope.** These are out of scope and left to later specs:
  - a wave director;
  - keystone perks;
  - new map profiles;
  - the playtest polish batch (zoom, framing, briefing, slow mark, early-call payout, labels).

## 3. Rules (proposed, with the recommended options)

1. **Raid** (section 8) [I except the values].
   - A Grunt, Runner or Brute that has not raided tonight checks at each fresh cell-centre decision for a living House, Farm or Mine on a cardinal neighbour.
   - If it finds several, it picks by ascending building ID.
   - It stops on its lane and attacks with its normal damage and interval. The first hit is due one interval after it stops (section 4).
   - It resumes its route when the target dies or the raid time (5 s [P]) has run, whichever is first, and is marked as having raided.
   - A raid is a melee hit: it triggers reflection and receives the Siege Commander aura.
   - If the hero is struck at or chased by the raider (a chase, or fighting back in place), the raid ends and is spent (v0.3, owner); afterwards the enemy walks its route.
   - Routing (section 5) is unchanged: raids never change the route, only when the enemy moves.
2. **Gold vein** (sections 2 and 11).
   - A terrain flag on land cells, generated as in D3 from a separate seeded stream (`seed ^ veinSalt`) after the terrain is validated.
   - Up to 20 attempts, then fewer veins. A map with no valid vein is still valid.
   - A vein never blocks anything.
3. **Mine** (sections 6 and 7).
   - It can only be placed with its footprint on a vein cell. A ghost on any other cell is rejected, with the reason "Mines need a gold vein".
   - Income is 30 a day [P], paid like a House's (section 6); a destroyed Mine earns nothing.
   - It has tiers per D4. It counts as an economy building for Raider Archer priority (1) and for Hardened Roofs.
4. **Waves** (section 9). Counts on Nights 4–12 and enemy HP [P] are set by the D5 gate; the structure is unchanged except the per-night spawn limit, 100 → 150 [I].
5. **Saves** (section 12). The terrain gains vein cells (an optional list; old saves have none). Schema 2 is unchanged.
6. **Presentation** (section 13).
   - Veins show as gold-flecked ground, visible by Day and Night, with a hover line: "Gold vein: build a Mine here".
   - A raiding enemy faces its target; the target's HP bar shows.
   - The Day route preview marks economy buildings beside a route with a small raid icon.
   - The Mine has its own low-poly model (a shaft head with a cart).

## 4. Acceptance

- EditMode:
  - a raid starts, stops at 5 s and happens once per night;
  - a raid ends when its target dies;
  - Bombers, Raider Archers and bosses never raid;
  - routes are unchanged by raids;
  - veins are deterministic per seed and meet D3 on all three profiles over seeds 1–50;
  - a Mine is refused off a vein;
  - Mine income and tiers;
  - old saves load without veins.
- The D5 gate passes, with its probe results recorded in section 16.
- PlayMode: place a Mine on a vein by input; an enemy raids a House in the release scene.
- Captures at 1280×720 and 1920×1080: veins on each profile, a Mine, a raid.
- Owner live check: does the run now ask for different layouts on different seeds?

## 5. Risks

- **Houses become useless.** If raids make every House a loss, economy collapses into Mines only. The D5 gate's economy strategy and the owner's run are the check.
- **Harder only, not more varied.** The gate measures win rates, not variety. The owner's live check of layouts across seeds is the real acceptance.
- **Scope.** One building, one terrain flag, one enemy rule and a tuning loop is a large change. Estimate: L, about 5–8 days including tuning.

## 7. As built (2026-10-30)

- Raids, gold veins, the Mine (with tiers and Stockade) and the per-night spawn limit of 150 are implemented as in section 3 and merged into `002-game-rules.md` v1.21.
- Veins are also kept at least 3 cells apart (Chebyshev), and avoid a river's whole crossing line rather than only its fords.
- The enemy-HP and count pressure knobs exist at a neutral 100%. No pressure value was adopted.
- **D5 gate not reached.** With raids and Mines and no tuning, house-ring still won 5/5. One tuning round (counts ×1.3, HP ×1.2) hurt every other strategy more (kill-zone 3/5, corridor 0/5, economy 0/5, house-ring 4/5). The sealed ring wins because every breach point lies within about two tiles of the Keep, which no approved knob changes. The owner moved the gate to spec 017 (land economy and maps), D10.
- Not built: the vein hover line ("Gold vein: build a Mine here"). Captures of veins, a Mine and a raid were not taken here; spec 017's captures of each map recipe include them.
- The owner's live check is pending.
