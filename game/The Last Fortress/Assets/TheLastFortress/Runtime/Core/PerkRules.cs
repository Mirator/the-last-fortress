using System;
using System.Collections.Generic;

namespace TheLastFortress
{
    /// <summary>A frozen Reward offer. Generated once on entering Reward; repeated requests return the same object.</summary>
    public sealed class RewardOffer
    {
        /// <summary>Night whose clearing produced this offer (1..5).</summary>
        public int ClearedNight;

        /// <summary>"(run,clearedNight)" identity used by UI clicks so stale cards cannot choose.</summary>
        public string OfferId;

        public string[] PerkIds;
        public uint RngBefore;
        public uint RngAfter;
        public string ChosenPerk;
    }

    /// <summary>
    /// Perk IDs, offer weights and the locked draw algorithm. The six-night MVP draws from its ten IDs (<see cref="AllIds"/>);
    /// the plan 008 release profile draws from <see cref="ReleaseIds"/> (those ten plus fourteen). A run's pool is fixed by
    /// its campaign, so adding release perks never changes an MVP seed's offers.
    /// </summary>
    public static class PerkRules
    {
        public const string ReinforcedTimber = "ReinforcedTimber";
        public const string EfficientFarming = "EfficientFarming";
        public const string ExpandedVision = "ExpandedVision";
        public const string LastStand = "LastStand";
        public const string FortressHeart = "FortressHeart";
        public const string FlamingArrows = "FlamingArrows";
        public const string Recycling = "Recycling";
        public const string Sharpshooters = "Sharpshooters";
        public const string BarbedDefenses = "BarbedDefenses";
        public const string ThriftyConstruction = "ThriftyConstruction";

        // Plan 008 release perks (008-rules.md section 4).
        public const string KillZone = "KillZone";
        public const string FocusedFire = "FocusedFire";
        public const string CounterBattery = "CounterBattery";
        public const string Overwatch = "Overwatch";
        public const string SpikedCorridor = "SpikedCorridor";
        public const string Gatehouse = "Gatehouse";
        public const string MarketSquare = "MarketSquare";
        public const string WarBonds = "WarBonds";
        public const string SignalFires = "SignalFires";
        public const string FieldEngineers = "FieldEngineers";
        public const string Shrapnel = "Shrapnel";
        public const string HeavyShot = "HeavyShot";
        public const string BlastShields = "BlastShields";
        public const string HardenedRoofs = "HardenedRoofs";

        public const int OfferSize = 3;

        // Effect constants (spec 003, "Perks and stacking").
        public const int ReinforcedTimberPercent = 30;
        public const int EfficientFarmingPercent = 30;
        public const double ExpandedVisionFactor = 1.1;
        public const int LastStandPercent = 40;
        public const int LastStandThresholdPercent = 30;
        public const int FortressHeartHp = 150;
        public const int BurnDamage = 2;
        public const int BurnIntervalTicks = 20;
        public const int BurnDurationTicks = 60;
        public const int SharpshootersPercent = 20;
        public const int BarbedReflect = 3;
        public const int KillZonePercent = 30;
        public const int SpikedCorridorPerTrap = 4;
        public const int SpikedCorridorMax = 8;
        public const int GatehouseReductionPercent = 30;
        public const int MarketSquareBonus = 4;
        public const int WarBondsGold = 30;
        public const int SignalFiresReach = 2;
        public const int FieldEngineersBudget = 30;
        public const double ShrapnelRadius = 0.5;
        public const int HeavyShotPercent = 50;
        public const int BlastShieldsReductionPercent = 50;
        public const int HardenedRoofsReductionPercent = 40;

        /// <summary>The six-night MVP pool: canonical IDs in ordinal order (the order candidates are weighed in).</summary>
        public static readonly string[] AllIds = Sorted(
            ReinforcedTimber, EfficientFarming, ExpandedVision, LastStand, FortressHeart,
            FlamingArrows, Recycling, Sharpshooters, BarbedDefenses, ThriftyConstruction);

        /// <summary>The release (plan 008) pool of 24, in ordinal order.</summary>
        public static readonly string[] ReleaseIds = Sorted(
            ReinforcedTimber, EfficientFarming, ExpandedVision, LastStand, FortressHeart,
            FlamingArrows, Recycling, Sharpshooters, BarbedDefenses, ThriftyConstruction,
            KillZone, FocusedFire, CounterBattery, Overwatch, SpikedCorridor, Gatehouse, MarketSquare,
            WarBonds, SignalFires, FieldEngineers, Shrapnel, HeavyShot, BlastShields, HardenedRoofs);

        static string[] Sorted(params string[] ids)
        {
            Array.Sort(ids, StringComparer.Ordinal);
            return ids;
        }

        /// <summary>Whether the ID is an MVP perk (the six-night pool).</summary>
        public static bool IsKnown(string id) => Array.IndexOf(AllIds, id) >= 0;

        /// <summary>Whether the ID belongs to the given pool.</summary>
        public static bool InPool(IReadOnlyList<string> pool, string id)
        {
            for (var i = 0; i < pool.Count; i++)
                if (pool[i] == id) return true;
            return false;
        }

        /// <summary>Building type a perk is about, or null for Keep/general perks.</summary>
        public static BuildingKind? AffectedKind(string id)
        {
            switch (id)
            {
                case ReinforcedTimber:
                case BarbedDefenses: return BuildingKind.Wall;
                case EfficientFarming: return BuildingKind.Farm;
                case ExpandedVision:
                case LastStand:
                case FlamingArrows:
                case Sharpshooters:
                case KillZone:
                case FocusedFire:
                case CounterBattery:
                case Overwatch: return BuildingKind.Tower;
                case SpikedCorridor: return BuildingKind.SpikeTrap;
                case Gatehouse: return BuildingKind.Wall;
                case MarketSquare:
                case HardenedRoofs: return BuildingKind.House;
                case SignalFires: return BuildingKind.Watchtower;
                case FieldEngineers: return BuildingKind.RepairStation;
                case Shrapnel:
                case HeavyShot: return BuildingKind.Cannon;
                default: return null;
            }
        }

        public static string Title(string id)
        {
            switch (id)
            {
                case ReinforcedTimber: return "Reinforced Timber";
                case EfficientFarming: return "Efficient Farming";
                case ExpandedVision: return "Expanded Vision";
                case LastStand: return "Last Stand";
                case FortressHeart: return "Fortress Heart";
                case FlamingArrows: return "Flaming Arrows";
                case Recycling: return "Recycling";
                case Sharpshooters: return "Sharpshooters";
                case BarbedDefenses: return "Barbed Defenses";
                case ThriftyConstruction: return "Thrifty Construction";
                case KillZone: return "Kill Zone";
                case FocusedFire: return "Focused Fire";
                case CounterBattery: return "Counter-Battery";
                case Overwatch: return "Overwatch";
                case SpikedCorridor: return "Spiked Corridor";
                case Gatehouse: return "Gatehouse";
                case MarketSquare: return "Market Square";
                case WarBonds: return "War Bonds";
                case SignalFires: return "Signal Fires";
                case FieldEngineers: return "Field Engineers";
                case Shrapnel: return "Shrapnel";
                case HeavyShot: return "Heavy Shot";
                case BlastShields: return "Blast Shields";
                case HardenedRoofs: return "Hardened Roofs";
                default: return id;
            }
        }

        public static string Effect(string id)
        {
            switch (id)
            {
                case ReinforcedTimber: return "Walls +30% max HP; existing Walls gain the same HP now.";
                case EfficientFarming: return "Farm income +30%, including the House bonus (rounded down per Farm).";
                case ExpandedVision: return "Archer Tower range +10% (4.0 → 4.4 tiles).";
                case LastStand: return "Archer Tower damage +40% while the Keep is below 30% of max HP.";
                case FortressHeart: return "Keep +150 max HP and +150 current HP.";
                case FlamingArrows: return "Archer Tower hits burn: 2 damage per second for 3 s; hits refresh, never stack.";
                case Recycling: return "Buildings destroyed in combat refund 25% of their price (not on sale).";
                case Sharpshooters: return "Archer Tower direct damage +20% (not burn or reflection).";
                case BarbedDefenses: return "Walls reflect 3 damage per melee hit (6 with an adjacent Spike Trap).";
                case ThriftyConstruction: return "Future construction prices -10% (minimum 1). Existing prices unchanged.";
                case KillZone: return "Archer Towers +30% damage to enemies on or next to a Spike Trap.";
                case FocusedFire: return "Archer Towers pick the weakest enemy in range instead of the nearest.";
                case CounterBattery: return "Archer Towers and Cannons pick Raider Archers and Bombers first.";
                case Overwatch: return "Archer Towers and Cannons fire their first shot at a new target in half the time.";
                case SpikedCorridor: return "Spike Traps +4 damage per neighbouring Spike Trap (max +8).";
                case Gatehouse: return "Walls next to an Archer Tower or Cannon take 30% less damage.";
                case MarketSquare: return "Each House next to another House earns +4 gold per day.";
                case WarBonds: return "+30 gold at dawn after a night in which the Keep took no damage.";
                case SignalFires: return "Watchtowers boost towers up to 2 tiles away, not only neighbours.";
                case FieldEngineers: return "Repair Stations restore 30 more HP at dawn and can repair the Keep.";
                case Shrapnel: return "Cannon splash radius +0.5 tiles (1.25 → 1.75).";
                case HeavyShot: return "Cannons +50% damage to Brutes and bosses.";
                case BlastShields: return "Bomber blasts deal 50% less damage to your buildings.";
                case HardenedRoofs: return "Houses, Farms, Watchtowers and Repair Stations take 40% less from Raider Archers.";
                default: return id;
            }
        }

        /// <summary>Weight 3 if the affected building exists (or the perk is Keep/general), otherwise 1.</summary>
        public static int Weight(string id, Func<BuildingKind, bool> owns)
        {
            var kind = AffectedKind(id);
            return kind == null || owns(kind.Value) ? 3 : 1;
        }

        /// <summary>
        /// Draws up to three distinct unowned perks by cumulative integer weights, without replacement. Consumes exactly
        /// one bounded draw per pick from <paramref name="rng"/>.
        /// </summary>
        public static string[] DrawOffer(RunRandom rng, ICollection<string> owned, Func<BuildingKind, bool> owns) =>
            DrawOffer(rng, owned, owns, AllIds);

        /// <summary>Same algorithm over an explicit pool (the run's campaign pool, already in ordinal order).</summary>
        public static string[] DrawOffer(RunRandom rng, ICollection<string> owned, Func<BuildingKind, bool> owns, IReadOnlyList<string> pool)
        {
            var candidates = new List<string>();
            var weights = new List<int>();
            foreach (var id in pool)
            {
                if (owned.Contains(id)) continue;
                candidates.Add(id);
                weights.Add(Weight(id, owns));
            }

            var picks = new List<string>();
            while (picks.Count < OfferSize && candidates.Count > 0)
            {
                var total = 0;
                foreach (var w in weights) total += w;
                var roll = (int)rng.NextBounded((uint)total);
                var i = 0;
                for (; i < weights.Count - 1; i++)
                {
                    if (roll < weights[i]) break;
                    roll -= weights[i];
                }

                picks.Add(candidates[i]);
                candidates.RemoveAt(i);
                weights.RemoveAt(i);
            }

            return picks.ToArray();
        }
    }
}
