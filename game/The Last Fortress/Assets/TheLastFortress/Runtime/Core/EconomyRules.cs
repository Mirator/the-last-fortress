using System.Collections.Generic;

namespace TheLastFortress
{
    /// <summary>Daily income split for the HUD forecast and the actual payment (both use <see cref="EconomyRules.DailyIncome"/>).</summary>
    public struct IncomeBreakdown
    {
        public int Keep;
        public int Houses;
        public int Farms;
        public int HouseCount;
        public int FarmCount;

        /// <summary>Farms that currently receive the Farm + House adjacency bonus.</summary>
        public int LinkedFarms;

        /// <summary>Conditional dawn bonus (War Bonds); never part of a Day forecast.</summary>
        public int Bonus;

        public int Total => Keep + Houses + Farms + Bonus;
    }

    /// <summary>Fixed MVP economy rules from specification 003. Pure functions over authoritative grid state.</summary>
    public static class EconomyRules
    {
        public const int HpPerGold = 5;
        public const int FarmHouseBonus = 5;
        public const int WallTrapReflect = 3;

        public static int ConstructionPrice(int baseCost, bool thrifty) =>
            thrifty ? System.Math.Max(1, baseCost * 9 / 10) : baseCost;

        /// <summary>restored = min(missing, gold * 5); cost = ceil(restored / 5).</summary>
        public static void RepairQuote(int missingHp, int gold, out int restored, out int cost)
        {
            restored = missingHp <= 0 || gold <= 0 ? 0 : (int)System.Math.Min(missingHp, (long)gold * HpPerGold);
            cost = (restored + HpPerGold - 1) / HpPerGold;
        }

        /// <summary>Whether any surviving House shares a footprint edge with the Farm (corners do not count).</summary>
        public static bool FarmHasAdjacentHouse(GridState grid, Building farm)
        {
            foreach (var cell in farm.Cells)
            foreach (var d in GridPos.CardinalOrder)
            {
                var n = cell + d;
                if (farm.Covers(n)) continue;
                var other = grid.GetOccupant(n);
                if (other != null && other.Kind == BuildingKind.House && other.IsAlive) return true;
            }

            return false;
        }

        /// <summary>Market Square: whether another surviving House shares an edge with this House.</summary>
        public static bool HouseHasAdjacentHouse(GridState grid, Building house)
        {
            foreach (var d in GridPos.CardinalOrder)
            {
                var other = grid.GetOccupant(house.Cell + d);
                if (other != null && other != house && other.Kind == BuildingKind.House && other.IsAlive) return true;
            }

            return false;
        }

        /// <summary>Unique surviving Farm/House pairs sharing an edge (for highlighting and the run summary).</summary>
        public static List<(Building farm, Building house)> FarmHousePairs(GridState grid)
        {
            var pairs = new List<(Building, Building)>();
            var seen = new HashSet<int>();
            foreach (var farm in grid.AllBuildings())
            {
                if (farm.Kind != BuildingKind.Farm || !farm.IsAlive) continue;
                seen.Clear();
                foreach (var cell in farm.Cells)
                foreach (var d in GridPos.CardinalOrder)
                {
                    var n = cell + d;
                    if (farm.Covers(n)) continue;
                    var other = grid.GetOccupant(n);
                    if (other != null && other.Kind == BuildingKind.House && other.IsAlive && seen.Add(other.Id))
                        pairs.Add((farm, other));
                }
            }

            return pairs;
        }

        /// <summary>
        /// Keep income plus surviving Houses and Farms. Per Farm: floor((base + adjacency) * (100 + farmBonusPercent) / 100).
        /// </summary>
        public static IncomeBreakdown DailyIncome(GridState grid, PrototypeDefinitions defs, int farmBonusPercent = 0, int houseLinkBonus = 0)
        {
            var r = new IncomeBreakdown();
            if (grid.Keep != null && grid.Keep.IsAlive) r.Keep = defs.Keep.DailyIncome;
            foreach (var b in grid.AllBuildings())
            {
                if (!b.IsAlive) continue;
                if (b.Kind == BuildingKind.House)
                {
                    r.Houses += BuildingIncome(grid, b, farmBonusPercent, houseLinkBonus);
                    r.HouseCount++;
                }
                else if (b.Kind == BuildingKind.Farm)
                {
                    r.Farms += BuildingIncome(grid, b, farmBonusPercent);
                    r.FarmCount++;
                    if (FarmHasAdjacentHouse(grid, b)) r.LinkedFarms++;
                }
            }

            return r;
        }

        /// <summary>
        /// Daily income of one surviving House or Farm (0 for anything else). The single per-building rule used by the
        /// payment, the forecast and the HUD: Farm = floor((base + 5 if any edge-adjacent House) * (100 + bonus) / 100).
        /// </summary>
        public static int BuildingIncome(GridState grid, Building b, int farmBonusPercent, int houseLinkBonus = 0)
        {
            if (b == null || !b.IsAlive) return 0;
            if (b.Kind == BuildingKind.House) return b.Definition.DailyIncome + (houseLinkBonus > 0 && HouseHasAdjacentHouse(grid, b) ? houseLinkBonus : 0);
            if (b.Kind != BuildingKind.Farm) return 0;
            var gross = b.Definition.DailyIncome + (FarmHasAdjacentHouse(grid, b) ? FarmHouseBonus : 0);
            return gross * (100 + farmBonusPercent) / 100;
        }
    }
}
