using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace TheLastFortress.Tests
{
    /// <summary>
    /// Plan 004 viability evidence: two bounded, replayable six-night policies on the flat authored map using only legal
    /// purchases (no granted gold or invulnerability). Each Day runs its scripted actions in order (unaffordable ones are
    /// logged and skipped), then repairs the Keep with leftover gold. The no-perk variant uses the labeled debug skip.
    /// The full night-by-night log is written to TestResults/004-policy-runs.txt.
    /// </summary>
    public class MvpPolicyTests
    {
        public sealed class Policy
        {
            public string Name;
            public string[][] Days;
            public string[] PerkPreference;
        }

        public static readonly Policy DefenseFirst = new Policy
        {
            Name = "defense-first",
            Days = new[]
            {
                new[] { "Tower 9 9", "Tower 11 11", "Tower 9 11" },
                new[] { "Tower 11 9" },
                new[] { "Wall 9 10", "Wall 11 10", "Wall 10 9", "Wall 10 11" },
                new[] { "Tower 8 10", "Tower 12 10" },
                new[] { "Tower 10 8", "Tower 10 12" },
                new[] { "Wall 7 10", "Wall 13 10", "Wall 10 7", "Wall 10 13" },
            },
            PerkPreference = new[]
            {
                PerkRules.Sharpshooters, PerkRules.FlamingArrows, PerkRules.ExpandedVision, PerkRules.FortressHeart, PerkRules.ReinforcedTimber,
                PerkRules.BarbedDefenses, PerkRules.EfficientFarming, PerkRules.LastStand, PerkRules.ThriftyConstruction, PerkRules.Recycling,
            },
        };

        public static readonly Policy EconomyFirst = new Policy
        {
            Name = "economy-first",
            Days = new[]
            {
                new[] { "Tower 9 9", "Tower 11 11", "Farm 4 13", "House 6 13" },
                new[] { "Farm 4 11", "House 6 12" },
                new[] { "Tower 9 11" },
                new[] { "Tower 11 9", "Wall 9 10" },
                new[] { "Wall 11 10", "Wall 10 9", "Wall 10 11", "Tower 8 10", "Tower 12 10" },
                new[] { "Tower 10 8", "Tower 10 12", "Wall 7 10", "Wall 13 10", "Wall 10 7", "Wall 10 13" },
            },
            PerkPreference = new[]
            {
                PerkRules.EfficientFarming, PerkRules.Sharpshooters, PerkRules.FlamingArrows, PerkRules.ThriftyConstruction, PerkRules.ExpandedVision,
                PerkRules.FortressHeart, PerkRules.ReinforcedTimber, PerkRules.BarbedDefenses, PerkRules.LastStand, PerkRules.Recycling,
            },
        };

        /// <summary>Plays one run; returns the final state and appends a replay log.</summary>
        public static RunState Play(Policy policy, uint seed, bool perks, StringBuilder log)
        {
            var sim = new RunSimulation(new RunConfig
            {
                Definitions = MvpDefinitions.CreateDefault(), Campaign = MvpCampaign.CreateSixNights(), Seed = seed,
            });
            log.AppendLine($"## {policy.Name} seed={seed} perks={(perks ? "normal" : "DEBUG no-perk baseline")}");
            for (var day = 1; day <= 6 && !sim.IsTerminal; day++)
            {
                var actions = new List<string>();
                foreach (var a in policy.Days[day - 1])
                {
                    var p = a.Split(' ');
                    var kind = (BuildingKind)Enum.Parse(typeof(BuildingKind), p[0]);
                    var r = sim.TryPlace(kind, new GridPos(int.Parse(p[1]), int.Parse(p[2])), out var b);
                    actions.Add(r == PlacementRejection.None ? $"{a} ({b.PurchasePrice}g)" : $"{a} skipped: {r}");
                }

                var keepBefore = sim.Keep.Hp;
                if (sim.TryRepair(sim.Keep) == RepairRejection.None) actions.Add($"repair Keep +{sim.Keep.Hp - keepBefore}");
                log.AppendLine($"Day {day}: gold before night {sim.Gold}; {string.Join("; ", actions)}");
                Assert.IsTrue(sim.StartNight(), $"night {day} starts");
                for (var i = 0; i < 40000 && sim.State == RunState.Night; i++) sim.Step();
                Assert.AreNotEqual(RunState.Night, sim.State, "night must resolve within the step bound");
                log.AppendLine($"  Night {day}: {sim.State}, Keep {sim.Keep.Hp}/{sim.Keep.MaxHp}, {sim.NightElapsedSeconds:0.0}s, defeated {sim.Stats.EnemiesDefeated}");
                if (sim.State != RunState.Reward) continue;
                var offer = sim.CurrentOffer;
                if (!perks)
                {
                    sim.DebugSkipReward();
                    log.AppendLine($"  Reward {string.Join(",", offer.PerkIds)} -> DEBUG skip");
                    continue;
                }

                var pick = policy.PerkPreference.First(id => offer.PerkIds.Contains(id));
                Assert.IsTrue(sim.ChoosePerk(offer.OfferId, pick));
                log.AppendLine($"  Reward {string.Join(",", offer.PerkIds)} -> {pick}; dawn income +{sim.LastIncome.Total}");
            }

            var s = sim.Stats;
            log.AppendLine($"Result: {sim.State}; nights cleared {s.NightsCleared}, defeated {s.EnemiesDefeated}, purchased {s.BuildingsPurchased}, " +
                           $"gold earned {s.GoldEarned}, synergies ever {s.SynergiesEver.Count}, perks [{string.Join(",", sim.OwnedPerks)}]");
            log.AppendLine();
            return sim.State;
        }

        [Test]
        public void BothRecordedPoliciesCompleteSixNightsOnRealBudget()
        {
            var log = new StringBuilder();
            log.AppendLine("# Plan 004 policy runs (flat authored map, MVP default tuning)");
            log.AppendLine();
            var outcomes = new List<string>();
            foreach (var policy in new[] { EconomyFirst, DefenseFirst })
            foreach (var seed in new uint[] { 1, 2, 3 })
            {
                var won = Play(policy, seed, true, log);
                var baseline = Play(policy, seed, false, log);
                outcomes.Add($"{policy.Name} seed {seed}: perks {won}, no-perk {baseline}");
                Assert.AreEqual(RunState.Victory, won, $"{policy.Name} seed {seed} with normal rewards");
            }

            log.Insert(0, string.Join(Environment.NewLine, outcomes) + Environment.NewLine + Environment.NewLine);
            Directory.CreateDirectory("TestResults");
            File.WriteAllText(Path.Combine("TestResults", "004-policy-runs.txt"), log.ToString());
            TestContext.WriteLine(string.Join(Environment.NewLine, outcomes));
        }
    }
}
