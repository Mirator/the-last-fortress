using System.Text;
using TheLastFortress;

static class PolicyRunner
{
    sealed record Policy(string Name, string[][] Days, string[] PerkPreference);

    static readonly Policy Economy = new("economy-first", new[]
    {
        new[] { "Tower 9 9", "Tower 11 11", "Farm 4 13", "House 6 13" },
        new[] { "Farm 4 11", "House 6 12" },
        new[] { "Tower 9 11" },
        new[] { "Tower 11 9", "Wall 9 10" },
        new[] { "Wall 11 10", "Wall 10 9", "Wall 10 11", "Tower 8 10", "Tower 12 10" },
        new[] { "Tower 10 8", "Tower 10 12", "Wall 7 10", "Wall 13 10", "Wall 10 7", "Wall 10 13" },
    }, new[] { PerkRules.EfficientFarming, PerkRules.Sharpshooters, PerkRules.FlamingArrows, PerkRules.ThriftyConstruction,
        PerkRules.ExpandedVision, PerkRules.FortressHeart, PerkRules.ReinforcedTimber, PerkRules.BarbedDefenses, PerkRules.LastStand, PerkRules.Recycling });

    static readonly Policy Defense = new("defense-first", new[]
    {
        new[] { "Tower 9 9", "Tower 11 11", "Tower 9 11" },
        new[] { "Tower 11 9" },
        new[] { "Wall 9 10", "Wall 11 10", "Wall 10 9", "Wall 10 11" },
        new[] { "Tower 8 10", "Tower 12 10" },
        new[] { "Tower 10 8", "Tower 10 12" },
        new[] { "Wall 7 10", "Wall 13 10", "Wall 10 7", "Wall 10 13" },
    }, new[] { PerkRules.Sharpshooters, PerkRules.FlamingArrows, PerkRules.ExpandedVision, PerkRules.FortressHeart,
        PerkRules.ReinforcedTimber, PerkRules.BarbedDefenses, PerkRules.EfficientFarming, PerkRules.LastStand, PerkRules.ThriftyConstruction, PerkRules.Recycling });

    static void Main(string[] args)
    {
        if (args.Length != 4 || (args[0] != "economy" && args[0] != "defense") ||
            !uint.TryParse(args[1], out var seed) || (args[2] != "flat" && args[2] != "generated") ||
            (args[3] != "perks" && args[3] != "no-perks"))
        {
            Console.Error.WriteLine("Usage: PolicyRunner <economy|defense> <seed> <flat|generated> <perks|no-perks>");
            Environment.ExitCode = 2;
            return;
        }

        var policy = args[0] == "economy" ? Economy : Defense;
        var map = args[2] == "flat" ? TerrainMap.Flat() : MapGenerator.Generate(seed);
        var perks = args[3] == "perks";
        var log = new StringBuilder();
        var sim = new RunSimulation(new RunConfig
        {
            Definitions = MvpDefinitions.CreateDefault(), Campaign = MvpCampaign.CreateSixNights(), Seed = seed, Terrain = map,
        });
        log.AppendLine($"# {policy.Name}; seed={seed}; {map.Describe()}; {(perks ? "normal perk choices" : "DEBUG no-perk baseline")}");

        for (var day = 1; day <= 6 && !sim.IsTerminal; day++)
        {
            var actions = new List<string>();
            foreach (var action in policy.Days[day - 1])
            {
                var p = action.Split(' ');
                var kind = Enum.Parse<BuildingKind>(p[0]);
                var result = sim.TryPlace(kind, new GridPos(int.Parse(p[1]), int.Parse(p[2])), out var building);
                actions.Add(result == PlacementRejection.None ? $"{action} ({building.PurchasePrice}g)" : $"{action} skipped: {result}");
            }

            var keepBefore = sim.Keep.Hp;
            if (sim.TryRepair(sim.Keep) == RepairRejection.None) actions.Add($"repair Keep +{sim.Keep.Hp - keepBefore}");
            log.AppendLine($"Day {day}: gold {sim.Gold}; {string.Join("; ", actions)}");
            if (!sim.StartNight()) throw new InvalidOperationException($"Night {day} did not start from {sim.State}");
            var steps = 0;
            while (steps++ < 40000 && sim.State == RunState.Night) sim.Step();
            if (sim.State == RunState.Night) throw new InvalidOperationException($"Night {day} exceeded 40000 simulation steps");
            log.AppendLine($"  Night {day}: {sim.State}; Keep {sim.Keep.Hp}/{sim.Keep.MaxHp}; {sim.NightElapsedSeconds:0.0}s; defeated {sim.Stats.EnemiesDefeated}");
            if (sim.State != RunState.Reward) continue;

            var offer = sim.CurrentOffer;
            if (!perks)
            {
                sim.DebugSkipReward();
                log.AppendLine($"  Reward {string.Join(",", offer.PerkIds)} -> DEBUG skip");
            }
            else
            {
                var pick = policy.PerkPreference.First(id => offer.PerkIds.Contains(id));
                if (!sim.ChoosePerk(offer.OfferId, pick)) throw new InvalidOperationException($"Could not choose offered perk {pick}");
                log.AppendLine($"  Reward {string.Join(",", offer.PerkIds)} -> {pick}; dawn income +{sim.LastIncome.Total}");
            }
        }

        var stats = sim.Stats;
        log.AppendLine($"Result: {sim.State}; nights cleared {stats.NightsCleared}, defeated {stats.EnemiesDefeated}, purchased {stats.BuildingsPurchased}, gold earned {stats.GoldEarned}, synergies ever {stats.SynergiesEver.Count}, perks [{string.Join(",", sim.OwnedPerks)}]");
        Console.Write(log.ToString());
        if (sim.State == RunState.Night) Environment.ExitCode = 1;
    }
}
