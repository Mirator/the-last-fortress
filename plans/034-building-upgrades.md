# Plan 034: building upgrade tiers

Status: **TODO** (2026-10-29). Priority P1. Effort L (about 5–7 days, tuning included). Implements **`specs/015-building-upgrades.md` v0.2** (approved 2026-10-29: D1–D7 and the section 4 table as recommended). Current rules: `specs/002-game-rules.md` v1.17. Baseline commit: `0dce05c`.

## 1. Pre-check (2026-10-29)

Paths are under `game/The Last Fortress/Assets/TheLastFortress/Runtime/`.

- **Building state** (`Core/Entities.cs`, `Building`): ID, kind, cells, definition, `PurchasePrice`, HP, `MaxHp`, orientation, targeting. It gains `Tier` (1–3) and `TotalPaid`.
- **Effective values are per definition, not per building.** `RunSimulation` has these:
  - `EffectiveMaxHp(def)` (`:383`);
  - `TowerDamage(def)` (`:456`), behind `TowerDamage(building)`;
  - `TowerRange(def)` (`:404`) and `TowerRange(building)` (`:396`, Watchtower buff);
  - `IsWatchtowerBuffed` (`:411`).

  Tiers need building-aware versions. Every caller of the definition-only versions moves to the building versions: tower attacks, Cannon shots, range rings, selection text and the Watchtower preview. The definition versions stay for ghosts (tier I).
- **Combat sites.**
  - `TowerAttacks` (`:1530`): tower and Cannon interval, damage, splash, target pick. This is where the second arrow goes.
  - Enemy melee on buildings (`:518`): Wall × and reflection, so spikes and stone go there.
  - Bomber blast (`:548`): Wall × against stone.
  - `TriggerTrap`: trap damage and the barbs slow.
- **Economy.**
  - `IncomeForecast` → `EconomyRules` (pure functions over the grid) for House and Farm income and Market hall.
  - `PayIncome`, for the Granary half-income of Farms destroyed last night. This needs a per-night record of destroyed Farm tiers in `NightRecap`.
  - `DawnRepair` (`:901`): budget, reach and Keep eligibility.
- **Movement for slow.** `Enemy.MoveUnitsPerStep` is fixed at spawn and used by `AdvanceSegment`, `BodyMayStep` (spec 014), `EaseLane` and `PursueStep`/`MoveOff`. Slow becomes an effective-speed function, read wherever the speed is read.
- **Sale and repair.** `PlacementService.TrySell` refunds from `PurchasePrice`, so it moves to `TotalPaid`, and so does Recycling. `TryRepair` uses `MaxHp`; it is unchanged, but `MaxHp` now includes the tier.
- **Saves.** `RunCheckpoint.BuildingData` (`:90`) holds `id, kind, x, z, orientation, hp, price`. It adds optional `tier` and `paid`; when missing they mean tier 1 and the purchase price. Validation must check the HP limit against the tier's maximum.
- **Gate.** Upgrades exist only when the definitions carry an upgrade table, which `ReleaseCampaign.Contact` adds for `release-rules-4` only. Legacy profiles and their fingerprints are unchanged.
- **HUD.** `HudView` has a selection panel with Sell, Repair and Rotate (`:200–206`). Upgrade joins them with the next tier's values and price. The free hotkey U goes in `PlayerController`.
- **Policy scripts.** `ReleasePolicyTests` (KillZone, Corridor, Economy) never upgrade, so D7 needs an upgrade order per policy, plus the pacing probe (`plans/evidence/030/PacingProbe.cs.txt` with `probe-hook.diff`). The full-run capture (`CaptureSequence.RunFullRun`) mirrors the kill-zone policy and gets the same order.

## 2. Stages, each with a gate

0. **Baseline.** The 2026-10-28 probe result after unit collision (`evidence/033/pacing-after-collision.txt`) is the pre-upgrade baseline. Record full EditMode and PlayMode at `0dce05c`.
1. **State, prices and the action.**
   - New `Core/BuildingUpgrades.cs`: the section 4 table as data, with the price factors (1×, 1.5×) and the stat and trait values per kind and tier.
   - `RunSimulation.TryUpgrade(building)` / `UpgradeQuote`, applying Thrifty Construction.
   - The HP share rule, refunds and Recycling from the total paid, and the save fields with validation.
   - **Gate:** EditMode tests for the upgrade action (price, atomicity, Day only, maximum tier, HP share), refund rounding, Thrifty on upgrades, a save round trip with tiers, and a legacy refusal. Legacy fingerprints are unchanged.
2. **Stat effects.** Building-aware max HP, damage, range, interval, splash, income, trap damage, Watchtower bonus, and repair budget and reach. Callers move to the building versions.
   - **Gate:** one parameterized EditMode case over the eight kinds and three tiers, checking the effective values against the table. Existing tests stay green.
3. **Traits and slow.**
   - Wall spikes and stone; the Archer Tower's second arrow, with its `TowerShot` event to the second target so it is drawn.
   - House Market hall and Farm Granary; Trap barbs and Cannon concussion (slow).
   - Watchtower spotter damage and reach; Repair Station Keep care.
   - Slow: `Enemy.SlowPercent` and `SlowUntilTick`, an effective speed in whole units, bosses at half.
   - **Gate:**
     - one EditMode case per trait, with values that tell the rule apart: second-arrow target order, stone versus a Brute, Granary after a destroyed Farm, slow refresh versus stacking, the boss half and the minimum speed;
     - mutation checks on the second arrow, slow and Granary;
     - the spec 014 body tests still pass with slowed bodies.
4. **HUD, input and look.**
   - The Upgrade button and U; the tier and next-tier values in the selection text; the disabled reason.
   - Tier pips over buildings in both views (presentation only); the tier on the HP bar or selection; the Help line.
   - **Gate:** a PlayMode case where a Tower is upgraded twice through input by Day, the Night refusal is shown, and gold and values change. Full PlayMode.
5. **Balance (D7).**
   - Upgrade orders for the three policies and the full run. Run the probe (seeds 1–5 × 3) and the recorded-win tests against the baseline.
   - **The gate:** no strategy's win rate rises by more than one seed in five, and no recorded win is lost.
   - If the gate fails, tune only price factors and stat percentages in at most 3 rounds, each a full probe, and add a section 16 row.
6. **Performance and captures.**
   - The stress pair with the hero: upgrades cost per shot (second arrow) and per step (slow).
   - Captures: the selection panel at tiers I–III, pips, a slowed column, a second arrow; the full run with upgrades at 1280×720 and 1920×1080.
7. **Close.**
   - Merge the rules into `002-game-rules.md` (v1.18: sections 6, 7, 10, 12 and 13; section 16 if tuned), archive the spec, and update the indexes and the results.
   - The owner's live check.

## 3. Risks

- **Effective-value refactor breadth.** Many callers read definition values directly. Mitigation: building-aware functions first (stage 2), then a grep that no combat or economy site reads `Definition.AttackDamage`, `Range`, `MaxHp` or `Income` directly.
- **Balance.** Upgrades add power to an already easy game. Stage 5's gate decides, and difficulty itself is a later spec.
- **Perk overlap.** Signal Fires and Field Engineers extend the tier III traits (spec section 4); the composition order is checked in the trait tests.
- **Slow and bodies.** Slowed enemies queue longer (spec 014). This is checked by the body invariant test and the probe's night lengths.
- **Selection panel space** at 1280×720 with four buttons and longer text. Checked in captures.
- **Presentation of the second arrow.** It needs a second `TowerShot` event; WorldView already draws one arrow per event.
