// Plan 006 stage 7: compare an adaptive defense-led opening with the recorded economy-first and fixed defense-first
// policies on generated seeds. Compiles the live Runtime/Core sources (no Unity). Evidence only: outcomes are not
// assertions, and nothing here changes tuning.
//
// Usage: dotnet run -- <economy|defense|adaptive|pure-defense> <seed> <perks|no-perks>
//   economy / defense : the fixed scripts from MvpPolicyTests / playtest B (skipped purchases are logged, never relocated)
//   adaptive          : DEFENSE-LED with delayed economy (rules below, fixed before any comparison)
//   pure-defense      : DIAGNOSTIC — the same adaptive defense rules with no economic buildings at all
//
// Adaptive rules (stable and deterministic):
//   * Tower cells in this order, first legal one used: (9,9) (11,11) (9,11) (11,9) (8,10) (12,10) (10,8) (10,12) (8,8) (12,12) (8,12) (12,8)
//   * Day 1: buy Towers until 3 are owned.
//   * Days 2-3 (delayed economy, adaptive only): one House at the first legal of (6,13) (6,12) (13,6) (13,7) (6,7) (7,6);
//     then one Farm on the first legal anchor that shares an edge with that House (searched in (z,x) order around it);
//     then Towers with what is left.
//   * Days 4-6: if Keep HP < 60% of max, repair it first; then Towers until 8 are owned; then Walls on the Keep's
//     neighbours (9,10) (11,10) (10,9) (10,11); then outer Walls (7,10) (13,10) (10,7) (10,13).
//   * Every Day ends by repairing the Keep with any leftover gold.
//   * Perks: first offered of Sharpshooters, FlamingArrows, ExpandedVision, FortressHeart, ReinforcedTimber, BarbedDefenses,
//     EfficientFarming, LastStand, ThriftyConstruction, Recycling.
// The driver exits 1 if a claimed purchase is not actually on the grid or a night does not resolve within 40000 steps.
using System.Text;
using TheLastFortress;

static class Adaptive
{
    static readonly string[] DefensePerks =
    {
        PerkRules.Sharpshooters, PerkRules.FlamingArrows, PerkRules.ExpandedVision, PerkRules.FortressHeart, PerkRules.ReinforcedTimber,
        PerkRules.BarbedDefenses, PerkRules.EfficientFarming, PerkRules.LastStand, PerkRules.ThriftyConstruction, PerkRules.Recycling,
    };

    static readonly string[] EconomyPerks =
    {
        PerkRules.EfficientFarming, PerkRules.Sharpshooters, PerkRules.FlamingArrows, PerkRules.ThriftyConstruction, PerkRules.ExpandedVision,
        PerkRules.FortressHeart, PerkRules.ReinforcedTimber, PerkRules.BarbedDefenses, PerkRules.LastStand, PerkRules.Recycling,
    };

    static readonly string[][] EconomyDays =
    {
        new[] { "Tower 9 9", "Tower 11 11", "Farm 4 13", "House 6 13" },
        new[] { "Farm 4 11", "House 6 12" },
        new[] { "Tower 9 11" },
        new[] { "Tower 11 9", "Wall 9 10" },
        new[] { "Wall 11 10", "Wall 10 9", "Wall 10 11", "Tower 8 10", "Tower 12 10" },
        new[] { "Tower 10 8", "Tower 10 12", "Wall 7 10", "Wall 13 10", "Wall 10 7", "Wall 10 13" },
    };

    static readonly string[][] DefenseDays =
    {
        new[] { "Tower 9 9", "Tower 11 11", "Tower 9 11" },
        new[] { "Tower 11 9" },
        new[] { "Wall 9 10", "Wall 11 10", "Wall 10 9", "Wall 10 11" },
        new[] { "Tower 8 10", "Tower 12 10" },
        new[] { "Tower 10 8", "Tower 10 12" },
        new[] { "Wall 7 10", "Wall 13 10", "Wall 10 7", "Wall 10 13" },
    };

    static readonly GridPos[] TowerCells = Cells("9 9|11 11|9 11|11 9|8 10|12 10|10 8|10 12|8 8|12 12|8 12|12 8");
    static readonly GridPos[] HouseCells = Cells("6 13|6 12|13 6|13 7|6 7|7 6");
    static readonly GridPos[] InnerWalls = Cells("9 10|11 10|10 9|10 11");
    static readonly GridPos[] OuterWalls = Cells("7 10|13 10|10 7|10 13");

    static GridPos[] Cells(string s) => s.Split('|').Select(p => p.Split(' ')).Select(p => new GridPos(int.Parse(p[0]), int.Parse(p[1]))).ToArray();

    static RunSimulation sim;
    static List<string> actions;
    static int failures;

    static int Count(BuildingKind kind) => sim.Grid.AllBuildings().Count(b => b.Kind == kind && b.IsAlive);

    static bool Buy(BuildingKind kind, GridPos cell, bool logSkip = true)
    {
        var result = sim.TryPlace(kind, cell, out var b);
        if (result == PlacementRejection.None)
        {
            if (b == null || sim.Grid.AllBuildings().All(x => x != b) || !b.Cells.All(c => kind == BuildingKind.SpikeTrap ? sim.Grid.GetTrap(c) == b : sim.Grid.GetOccupant(c) == b))
            {
                actions.Add($"INVALID claimed {kind} {cell}");
                failures++;
                return false;
            }

            actions.Add($"{kind} {cell} ({b.PurchasePrice}g)");
            return true;
        }

        if (logSkip) actions.Add($"{kind} {cell} skipped: {result}");
        return false;
    }

    /// <summary>First legal cell in stable order; terrain/occupied cells are relocated, unaffordable stops the search.</summary>
    static bool BuyFirst(BuildingKind kind, IEnumerable<GridPos> cells)
    {
        var relocated = new List<string>();
        foreach (var c in cells)
        {
            var check = sim.CheckPlacement(kind, c);
            if (check == PlacementRejection.InsufficientGold) return false;
            if (check != PlacementRejection.None)
            {
                // Slots already holding one of our buildings are simply used up; only real obstacles count as relocation.
                if (check != PlacementRejection.Occupied) relocated.Add($"{c}:{check}");
                continue;
            }

            if (relocated.Count > 0) actions.Add($"{kind} relocated past {string.Join(",", relocated)}");
            return Buy(kind, c);
        }

        return false;
    }

    static IEnumerable<GridPos> FarmAnchorsBeside(Building house)
    {
        for (var z = house.Cell.Z - 2; z <= house.Cell.Z + 1; z++)
        for (var x = house.Cell.X - 2; x <= house.Cell.X + 1; x++)
        {
            var anchor = new GridPos(x, z);
            var cells = BuildingDefinition.FootprintCells(BuildingKind.Farm, anchor);
            if (cells.Contains(house.Cell)) continue;
            if (cells.Any(c => c.IsCardinalNeighbor(house.Cell))) yield return anchor;
        }
    }

    static void Repair()
    {
        var before = sim.Keep.Hp;
        if (sim.TryRepair(sim.Keep) == RepairRejection.None) actions.Add($"repair Keep +{sim.Keep.Hp - before} ({sim.Keep.Hp}/{sim.Keep.MaxHp})");
    }

    static void AdaptiveDay(int day, bool economy)
    {
        if (day == 1)
        {
            while (Count(BuildingKind.Tower) < 3 && BuyFirst(BuildingKind.Tower, TowerCells)) { }
            return;
        }

        if (day <= 3)
        {
            if (economy)
            {
                var house = sim.Grid.AllBuildings().FirstOrDefault(b => b.Kind == BuildingKind.House && b.IsAlive);
                if (house == null && BuyFirst(BuildingKind.House, HouseCells))
                    house = sim.Grid.AllBuildings().First(b => b.Kind == BuildingKind.House && b.IsAlive);
                if (house != null && Count(BuildingKind.Farm) == 0) BuyFirst(BuildingKind.Farm, FarmAnchorsBeside(house));
            }

            while (BuyFirst(BuildingKind.Tower, TowerCells)) { }
            return;
        }

        if (sim.Keep.Hp * 10 < sim.Keep.MaxHp * 6) Repair();
        while (Count(BuildingKind.Tower) < 8 && BuyFirst(BuildingKind.Tower, TowerCells)) { }
        while (BuyFirst(BuildingKind.Wall, InnerWalls)) { }
        while (BuyFirst(BuildingKind.Wall, OuterWalls)) { }
    }

    static void ScriptedDay(string[] script)
    {
        foreach (var a in script)
        {
            var p = a.Split(' ');
            Buy(Enum.Parse<BuildingKind>(p[0]), new GridPos(int.Parse(p[1]), int.Parse(p[2])));
        }
    }

    static int Main(string[] args)
    {
        var policies = new[] { "economy", "defense", "adaptive", "pure-defense" };
        if (args.Length != 3 || !policies.Contains(args[0]) || !uint.TryParse(args[1], out var seed) || (args[2] != "perks" && args[2] != "no-perks"))
        {
            Console.Error.WriteLine("Usage: Adaptive <economy|defense|adaptive|pure-defense> <seed> <perks|no-perks>");
            return 2;
        }

        var policy = args[0];
        var perks = args[2] == "perks";
        var map = MapGenerator.Generate(seed);
        sim = new RunSimulation(new RunConfig
        {
            Definitions = MvpDefinitions.CreateDefault(), Campaign = MvpCampaign.CreateSixNights(), Seed = seed, Terrain = map,
        });
        var preference = policy == "economy" ? EconomyPerks : DefensePerks;
        var log = new StringBuilder();
        var label = policy == "pure-defense" ? "pure-defense (DIAGNOSTIC: guaranteed Keep income only)" : policy;
        log.AppendLine($"# {label}; seed={seed}; {map.Describe()}; {(perks ? "normal offered rewards" : "DEBUG no-perk baseline")}");
        var keepByNight = new List<string>();

        for (var day = 1; day <= 6 && !sim.IsTerminal; day++)
        {
            actions = new List<string>();
            var goldStart = sim.Gold;
            switch (policy)
            {
                case "economy": ScriptedDay(EconomyDays[day - 1]); break;
                case "defense": ScriptedDay(DefenseDays[day - 1]); break;
                default: AdaptiveDay(day, policy == "adaptive"); break;
            }

            Repair();
            log.AppendLine($"Day {day}: gold {goldStart} -> {sim.Gold}; {string.Join("; ", actions)}");
            if (!sim.StartNight()) throw new InvalidOperationException($"Night {day} did not start from {sim.State}");
            var steps = 0;
            while (steps++ < 40000 && sim.State == RunState.Night) sim.Step();
            if (sim.State == RunState.Night)
            {
                log.AppendLine($"  Night {day}: UNRESOLVED after 40000 steps");
                Console.Write(log);
                return 1;
            }

            var r = sim.LastNight;
            log.AppendLine($"  Night {day}: {sim.State}; Keep {sim.Keep.Hp}/{sim.Keep.MaxHp} (-{r.KeepDamage}); defeated {r.EnemiesDefeated}; " +
                           $"lost {r.BuildingsLost}; surviving Towers {Count(BuildingKind.Tower)}, Walls {Count(BuildingKind.Wall)}; {sim.NightElapsedSeconds:0.0}s");
            keepByNight.Add($"N{day}:{sim.Keep.Hp}");
            if (sim.State != RunState.Reward) continue;
            var offer = sim.CurrentOffer;
            if (!perks)
            {
                sim.DebugSkipReward();
                log.AppendLine($"  Reward {string.Join(",", offer.PerkIds)} -> DEBUG skip; dawn income +{sim.LastIncome.Total}");
            }
            else
            {
                var pick = preference.First(id => offer.PerkIds.Contains(id));
                if (!sim.ChoosePerk(offer.OfferId, pick)) throw new InvalidOperationException($"Could not choose offered perk {pick}");
                log.AppendLine($"  Reward {string.Join(",", offer.PerkIds)} -> {pick}; dawn income +{sim.LastIncome.Total}");
            }
        }

        var s = sim.Stats;
        log.AppendLine($"Result: {sim.State}; nights cleared {s.NightsCleared}; Keep by night {string.Join(" ", keepByNight)}; purchased {s.BuildingsPurchased}; " +
                       $"gold earned {s.GoldEarned}; perks [{string.Join(",", sim.OwnedPerks)}]");
        Console.Write(log);
        return failures > 0 ? 1 : 0;
    }
}
