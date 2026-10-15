using System;
using System.Linq;
using System.Text.RegularExpressions;
using TheLastFortress;

internal static class Scenario
{
    static uint Seed = 1;

    static void Main(string[] args)
    {
        if (args.Length > 0 && uint.TryParse(args[0], out var requestedSeed)) Seed = requestedSeed;
        var sim = new RunSimulation(new RunConfig
        {
            Definitions = MvpDefinitions.CreateDefault(),
            Campaign = MvpCampaign.CreateSixNights(),
            Terrain = TerrainMap.Flat(),
            Seed = Seed,
        });
        Console.WriteLine($"scenario=placement-perk-resume seed={sim.Seed} terrain=flat profile={sim.Campaign.Label}");
        Console.WriteLine($"initial state={sim.State} gold={sim.Gold} keepHp={sim.Keep.Hp}/{sim.Keep.MaxHp}");

        var startGold = sim.Gold;
        var startRevision = sim.Grid.Revision;
        var ring = sim.TryPlace(BuildingKind.Wall, new GridPos(0, 10), out _);
        Console.WriteLine($"mistake=reserved-ring result={ring} text='{PlacementText.Describe(ring)}' goldDelta={sim.Gold-startGold} gridRevisionDelta={sim.Grid.Revision-startRevision}");
        var occupied = sim.TryPlace(BuildingKind.Tower, new GridPos(10, 10), out _);
        Console.WriteLine($"mistake=occupied-keep result={occupied} text='{PlacementText.Describe(occupied)}'");

        Place(sim, BuildingKind.Tower, 8, 8, "defense tower 1");
        Place(sim, BuildingKind.Tower, 8, 9, "defense tower 2");
        var thirdResult = sim.TryPlace(BuildingKind.Tower, new GridPos(8, 10), out var third);
        Console.WriteLine($"strategy=third-tower result={thirdResult} gold={sim.Gold}");
        var fundsBefore = sim.Gold;
        var noFunds = sim.TryPlace(BuildingKind.Farm, new GridPos(12, 4), out _);
        Console.WriteLine($"mistake=overspend-farm result={noFunds} text='{PlacementText.Describe(noFunds)}' goldDelta={sim.Gold-fundsBefore}");
        if (third != null)
        {
            var saleGold = sim.Gold;
            var sold = sim.TrySell(third);
            Console.WriteLine($"recovery=sell-third-tower result={sold} goldDelta={sim.Gold-saleGold}");
        }
        Place(sim, BuildingKind.Farm, 12, 4, "economy farm after recovery");

        var towers = sim.Towers.ToArray();
        var tower = towers.FirstOrDefault();
        Console.WriteLine($"effective-before-perk towerDamage={sim.TowerDamage(tower)} range={sim.TowerRange(tower):0.0} income={sim.IncomeForecast().Total}");
        Console.WriteLine($"night1-start={sim.StartNight()} validationErrors={sim.ValidateMap().Count}");
        var steps = 0;
        while (sim.State == RunState.Night && steps < 5000) { sim.Step(); steps++; }
        Console.WriteLine($"night1-end state={sim.State} steps={steps} tick={sim.Tick} keepHp={sim.Keep.Hp}/{sim.Keep.MaxHp} enemies={sim.AliveEnemyCount} scheduled={sim.ScheduledRemaining} gold={sim.Gold}");
        if (sim.State != RunState.Reward)
        {
            Console.WriteLine("perk/resume=not-reached");
            Environment.ExitCode = 2;
            return;
        }

        var offer = sim.CurrentOffer;
        Console.WriteLine($"offer id={offer.OfferId} perks={string.Join(",", offer.PerkIds)}");
        var chosen = ChooseForCurrentBuild(offer, sim);
        var incomeBefore = sim.IncomeForecast().Total;
        var goldBefore = sim.Gold;
        var picked = sim.ChoosePerk(offer.OfferId, chosen);
        var incomeAfter = sim.IncomeForecast().Total;
        Console.WriteLine($"choice={chosen} rationale='{Rationale(chosen, sim)}' accepted={picked} state={sim.State} incomeForecastBefore={incomeBefore} incomeForecastAfter={incomeAfter} paidGoldDelta={sim.Gold-goldBefore}");
        Console.WriteLine($"effective-after-perk towerDamage={sim.TowerDamage(sim.Towers.FirstOrDefault()):0.##} range={sim.TowerRange(sim.Towers.FirstOrDefault()):0.0} income={incomeAfter} owned={string.Join(",", sim.OwnedPerks)}");

        var checkpoint = CheckpointCodec.Capture(sim);
        var resumed = CheckpointCodec.Restore(checkpoint, sim.Definitions, out var issue, out var message);
        var checkpointMatches = resumed != null && Normalize(sim.CaptureSnapshot()) == Normalize(resumed.CaptureSnapshot());
        Console.WriteLine($"resume restoreProblem={issue} message='{message}' snapshotEqual={checkpointMatches} gold={resumed?.Gold} day={resumed?.NightNumber} lastPaidDay={resumed?.LastPaidDay} perks={string.Join(",", resumed?.OwnedPerks ?? Array.Empty<string>())}");
        if (resumed == null) { Environment.ExitCode = 2; return; }

        var originalStarted = sim.StartNight();
        var resumedStarted = resumed.StartNight();
        var replaySteps = 0;
        while (sim.State == RunState.Night && resumed.State == RunState.Night && replaySteps < 10000)
        {
            sim.Step();
            resumed.Step();
            replaySteps++;
        }
        var combatMatches = Normalize(sim.CaptureSnapshot()) == Normalize(resumed.CaptureSnapshot());
        Console.WriteLine($"resume-combat originalStarted={originalStarted} resumedStarted={resumedStarted} steps={replaySteps} originalState={sim.State} resumedState={resumed.State} keepHp={sim.Keep.Hp}/{resumed.Keep.Hp} enemies={sim.AliveEnemyCount}/{resumed.AliveEnemyCount} gold={sim.Gold}/{resumed.Gold} snapshotEqual={combatMatches}");
        if (!checkpointMatches || !combatMatches || !originalStarted || !resumedStarted || sim.State != resumed.State || replaySteps >= 10000) Environment.ExitCode = 2;
    }

    static string Normalize(string snapshot) => Regex.Replace(snapshot, @" rev=\d+", "");

    static void Place(RunSimulation sim, BuildingKind kind, int x, int z, string label)
    {
        var gold = sim.Gold;
        var result = sim.TryPlace(kind, new GridPos(x, z), out var building);
        Console.WriteLine($"place='{label}' kind={kind} cell=({x},{z}) result={result} id={building?.Id} price={building?.PurchasePrice} goldDelta={sim.Gold-gold} gold={sim.Gold}");
    }

    static string ChooseForCurrentBuild(RewardOffer offer, RunSimulation sim)
    {
        var priority = new[] { PerkRules.EfficientFarming, PerkRules.Sharpshooters, PerkRules.ExpandedVision, PerkRules.FortressHeart, PerkRules.FlamingArrows, PerkRules.ReinforcedTimber };
        return priority.FirstOrDefault(p => offer.PerkIds.Contains(p)) ?? offer.PerkIds[0];
    }

    static string Rationale(string perk, RunSimulation sim)
    {
        if (perk == PerkRules.EfficientFarming) return "an owned Farm increases recurring income";
        if (perk == PerkRules.Sharpshooters) return "an owned Tower benefits from higher direct damage";
        if (perk == PerkRules.ExpandedVision) return "an owned Tower benefits from longer range";
        if (perk == PerkRules.FortressHeart) return "adds persistent Keep health for later nights";
        if (perk == PerkRules.FlamingArrows) return "owned Towers add damage over time";
        if (perk == PerkRules.ReinforcedTimber) return "general future wall durability";
        return "first available option in the frozen offer";
    }
}



