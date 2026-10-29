# Feature spec 015: building upgrade tiers

Version 0.2, 2026-10-29. Status: **IMPLEMENTED and merged into `../002-game-rules.md` v1.18 (2026-10-29); archived.** Not current: plan from `002-game-rules.md`. Approved by the owner on 2026-10-29: the scope (D1–D4), the upgrade table in section 4, and D5–D7 were all accepted as recommended. The owner's live check is pending.

Changes `002-game-rules.md` v1.17, sections 6, 7, 10, 12 and 13, under `release-rules-4`. Legacy profiles are unchanged.

## 1. Problem

Defence-first runs make only 1–2 purchases per Day: kill-zone scripts buy 16–23 buildings in a whole run. Most Days offer little to decide. Today the only ways to strengthen a layout are more buildings and perks, and good cells near the routes run out.

Upgrades add a second axis: make a well-placed building better instead of adding another one. Pillar 1, "every building is a tactical decision", is served only if each tier changes how the building plays, not just its numbers. That is why each tier adds a trait.

## 2. Decisions

- **D1: All eight buildings** have tiers: Wall, Archer Tower, House, Farm, Spike Trap, Cannon, Watchtower, Repair Station. The Keep has none.
- **D2: Each tier is a modest stat raise plus one trait.** Traits change behaviour (a second target, slowing, damage reduction, coverage) and avoid duplicating a perk.
- **D3: Two extra tiers.** Every building is tier I when built; then tier II, then tier III.
- **D4: When and how.**
  - By Day only, on a selected building, with the Upgrade button or U.
  - Tier II costs the building's base price; tier III costs 1.5 × the base price [P].
  - Available from Day 1. One tier per action; a building may go from I to III on the same Day.
- **D5: Presentation** (accepted): tier pips (II, III) on the building, and its HP bar and selection showing the tier, with model changes later. The alternative is a distinct model per tier now: 16 art variants, much more work.
- **D6: Rules version** (accepted): change `release-rules-4` in place, as for plans 019–033. A building's tier is stored in the save, and old saves load every building as tier I.
- **D7: Balance guard** (accepted). Upgrades add power to a game the scripted runs already find easy, so:
  - extend the policy scripts with an upgrade order, so their runs exercise upgrades;
  - accept the result only if no strategy's win rate rises by more than one seed in five;
  - leave difficulty itself to a later spec (difficulty tiers).

## 3. Rules (proposed)

1. **State.**
   - Every building has a tier (1–3) and a total price paid: its purchase price plus every upgrade price paid.
   - Its effective values come from its definition, its tier and perks, in that order.
   - Routing, footprints and solidity never change with tier [I].
2. **Upgrading** [I].
   - Only by Day, on a living building below tier III, with enough gold. It is atomic: one charge, one tier.
   - Thrifty Construction applies to upgrade prices as to purchases.
   - The building keeps its share of HP: `hp' = ceil(hp × max' / max)`.
   - Upgrading never changes routes, targets or cooldowns.
3. **Refunds.** Sale refunds `floor(total paid × 0.5)`. Recycling refunds 25% of the total paid.
4. **Slow** (a new status, used by the Cannon and Spike Trap traits).
   - A slowed enemy moves at `(100 − s)%` of its speed, in whole units per step, rounded down, at least 1.
   - A new slow refreshes the duration, and the stronger slow applies. Slows never stack.
   - Bosses take half the slow.
   - Slow never affects attack timing.
5. **Perks** apply on top of tier values. A perk and a trait that touch the same value add (section 8's damage-modifier rule).
6. **Saves.** Each building record stores its tier and total paid; a missing field means tier I and the purchase price. Schema 2 is unchanged.
7. **Presentation.**
   - The selection panel shows the tier, the next tier's values and its price, and an "Upgrade (U)" button. It is disabled with a reason: Night, maximum tier, or not enough gold.
   - Effective values are shown as today.
   - The Reward screen and the policy scripts do not need to change.

## 4. Upgrade table (proposed, all values [P])

Tier III values include tier II's.

| Building | Base (tier I) | Tier II | Tier III |
|---|---|---|---|
| **Wall** | 80 HP | +30% HP. **Spikes**: reflects 2 damage per melee hit received (adds to the Wall + Trap synergy and Barbed Defenses) | +60% HP; reflects 4. **Stone**: the Wall × multiplier (Brute, Bomber, bosses) no longer applies to it |
| **Archer Tower** | 10 per 0.9 s, range 4.0 | +20% damage. **Second arrow**: each shot also hits a second enemy in range (nearest to the first, then lowest ID) for half damage | +40% damage, range +0.5; the second arrow does full damage |
| **House** | 10 gold/day | +5 gold/day | +10 gold/day. **Market hall**: each Farm sharing an edge earns +5 more |
| **Farm** | 17 gold/day (+5 with a House) | +5 gold/day | +10 gold/day. **Granary**: if destroyed in combat, still pays half its income at the next dawn |
| **Spike Trap** | 7 on entry | 10 on entry. **Barbs**: slows its victim 30% for 1.5 s | 14 on entry; slows 50% for 2 s |
| **Cannon** | 20 per 2.5 s, splash 1.25 | +25% damage, splash +0.25. **Concussion**: every enemy hit is slowed 30% for 2 s | Interval 2.5 → 2.0 s; slows 50% |
| **Watchtower** | +0.8 range to adjacent towers | +1.0 range. **Spotter**: towers it buffs deal +10% damage | +1.2 range, +20% damage; buffs towers within Chebyshev 2 (as Signal Fires; with that perk, 3) |
| **Repair Station** | 40 HP at dawn, within 2 cells | 60 HP, within 3 cells | 80 HP. **Keep care**: the Keep becomes eligible (as Field Engineers; with that perk, +30 more) |

Prices are tier II at 1 × base and tier III at 1.5 × base, rounded down, as is the Thrifty Construction discount:

| | Wall | Tower | House | Farm | Trap | Cannon | Watchtower | Repair |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Tier II | 14 | 49 | 28 | 42 | 25 | 77 | 32 | 56 |
| Tier III | 21 | 73 | 42 | 63 | 37 | 115 | 48 | 84 |

**Notes for review:**
- **Watchtower III and Repair Station III overlap Signal Fires and Field Engineers.** The table makes those perks extend the trait instead of becoming useless. The alternative is new traits.
- **Slow is the only new mechanic.** Everything else reuses rules that exist.
- **Economy tiers pay for themselves.** House II is 28 gold for +5 a day, so it pays back in 6 Days, but only if bought early. That is a deliberate early-economy choice.

## 5. Acceptance

- **EditMode, rules:** the price, gold and atomicity of an upgrade; HP share kept; refunds of the total paid; Thrifty Construction on upgrades; each trait's rule (one case per building, tier II and III where the trait differs); slow (refresh, strongest wins, half on bosses, minimum speed); save round trip with tiers; legacy refusal.
- **Policy:** the scripts gain an upgrade order. Kill-zone, corridor and economy, seeds 1–5, against the pre-upgrade baseline (D7).
- **Captures:** the selection panel at each tier, tier pips, a slowed column, and a second arrow. The owner's look is the acceptance of record.

## 6. As built (2026-10-29)

The section 4 table and prices are implemented unchanged; no tuning round was needed. Where the rules above left a choice open, the implementation took these, and they are merged into the game rules:

- **Order of effects.** Definition, then tier, then perks. A tier's percentage raise is of the definition value, floored (Archer Tower 12 and 14, Cannon 25, Wall 104 and 128 HP). An Archer Tower's tier III range is `(4.0 + 0.5) × 1.1` with Expanded Vision.
- **Second arrow.** It is an Archer Tower hit like any other: Sharpshooters, Last Stand, Kill Zone, Spotter and Flaming Arrows' burn apply. Its half is floored once. It is chosen before the first arrow lands, among the other living enemies in range.
- **Market hall.** Once per Farm, however many tier III Houses touch it. It belongs to the Farm's adjacency term, so Efficient Farming multiplies it.
- **Two Watchtowers.** The highest-tier Watchtower in reach gives its range bonus and Spotter damage; still one buff per tower.
- **Slow refresh.** The stronger of the running and the new percentage, and the later of the two expiries. Bosses take half the percentage, floored (30% to 15%, 50% to 25%).
- **Granary.** `floor(income / 2)`, the income valued at the moment the Farm is destroyed (tier, adjacency and Efficient Farming included). It is its own dawn line and is never part of the forecast.
- **Repair Station III with Field Engineers.** Budget 80 + 30 + 30 = 140, reach 3.
- **Upgrade refusal order.** Maximum tier is reported before Night, so a tier III building at Night says "Maximum tier".
- **Policy scripts (D7).** Upgrades are items in the scripts' own build queues, after the planned layout (ring Towers, Cannons, Spike Traps, Houses and Farms). Scripts that upgraded earlier, instead of mid-game buildings, lost wins against the baseline. At these values a tier is worth less per gold than a new building while good cells remain. Upgrades do not make the scripted game easier.
- **Presentation.** Tier pips are amber diamonds above the HP bar. There is no visual mark for a slowed enemy: it walks slower, its gait follows its speed.
