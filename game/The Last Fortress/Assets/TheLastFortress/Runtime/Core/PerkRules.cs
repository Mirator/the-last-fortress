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

    /// <summary>Ten MVP perks, their canonical IDs, offer weights and the locked draw algorithm.</summary>
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

        /// <summary>Canonical IDs in ordinal order (the order candidates are weighed in).</summary>
        public static readonly string[] AllIds = CreateSorted();

        static string[] CreateSorted()
        {
            var ids = new[]
            {
                ReinforcedTimber, EfficientFarming, ExpandedVision, LastStand, FortressHeart,
                FlamingArrows, Recycling, Sharpshooters, BarbedDefenses, ThriftyConstruction,
            };
            Array.Sort(ids, StringComparer.Ordinal);
            return ids;
        }

        public static bool IsKnown(string id) => Array.IndexOf(AllIds, id) >= 0;

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
                case Sharpshooters: return BuildingKind.Tower;
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
                case LastStand: return "Tower damage +40% while the Keep is below 30% of max HP.";
                case FortressHeart: return "Keep +150 max HP and +150 current HP.";
                case FlamingArrows: return "Tower hits burn: 2 damage per second for 3 s; hits refresh, never stack.";
                case Recycling: return "Buildings destroyed in combat refund 25% of their price (not on sale).";
                case Sharpshooters: return "Tower direct damage +20% (not burn or reflection).";
                case BarbedDefenses: return "Walls reflect 3 damage per melee hit (6 with an adjacent Spike Trap).";
                case ThriftyConstruction: return "Future construction prices -10% (minimum 1). Existing prices unchanged.";
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
        public static string[] DrawOffer(RunRandom rng, ICollection<string> owned, Func<BuildingKind, bool> owns)
        {
            var candidates = new List<string>();
            var weights = new List<int>();
            foreach (var id in AllIds)
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
