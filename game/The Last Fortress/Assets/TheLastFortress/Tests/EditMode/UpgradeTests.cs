using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static TheLastFortress.Tests.SimTestUtil;

namespace TheLastFortress.Tests
{
    /// <summary>
    /// Spec 015: building upgrade tiers under release-rules-4. Each test pins one rule of the action, a trait or slow with values
    /// that tell the rule apart; the tier values themselves come from <see cref="BuildingUpgrades"/>, not frozen here.
    /// </summary>
    public class UpgradeTests
    {
        /// <summary>Release buildings with tiers (no bodies, so stacked spawns are allowed) and gold for every upgrade.</summary>
        static PrototypeDefinitions Defs(int gold = 2000)
        {
            var d = ReleaseDefinitions.CreateDefault().WithUpgrades();
            d.StartingGold = gold;
            return d;
        }

        static RunSimulation NewRun(WavePlan wave, PrototypeDefinitions defs = null) =>
            new RunSimulation(new RunConfig { Definitions = defs ?? Defs(), Wave = wave });

        static RunSimulation NewCampaign(PrototypeDefinitions defs, params WavePlan[] nights) => new RunSimulation(new RunConfig
        {
            Definitions = defs, Seed = 1,
            Campaign = new CampaignConfig { Label = "t", Nights = nights, OffersRewards = true, PerkPool = PerkRules.ReleaseIds },
        });

        static WavePlan Wave(params (long tick, EnemyKind kind)[] spawns) =>
            new WavePlan("w", spawns.Select(s => new SpawnEntry(s.tick, MvpCampaign.West, 0, s.kind)).ToArray(), new[] { MvpCampaign.West });

        static Building Upgraded(RunSimulation sim, BuildingKind kind, int x, int z, int tier)
        {
            var b = Place(sim, kind, x, z);
            while (b.Tier < tier) Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(b), $"upgrading {kind} to tier {b.Tier + 1}");
            return b;
        }

        static SimEvent StepUntilEvent(RunSimulation sim, System.Func<SimEvent, bool> match, int max = 4000)
        {
            for (var i = 0; i < max && sim.State == RunState.Night; i++)
            {
                sim.Step();
                foreach (var e in Drain(sim))
                    if (match(e)) return e;
            }

            Assert.Fail($"event not seen within {max} steps (state {sim.State}, tick {sim.Tick})");
            return default;
        }

        [Test]
        public void UpgradingIsDayOnlyAndAtomicChargesTheTierPriceKeepsTheHpShareAndRefundsHalfTheTotalPaid()
        {
            var sim = NewRun(Wave((0, EnemyKind.Grunt)));
            var wall = Place(sim, BuildingKind.Wall, 4, 4);
            wall.Hp = 51;
            var gold = sim.Gold;
            Assert.AreEqual(14, sim.UpgradePrice(wall), "tier II costs the base price");
            Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(wall));
            // ceil(51 x 104 / 80) = ceil(66.3) = 67 (floor would keep 66)
            Assert.AreEqual((2, gold - 14, 104, 67), (wall.Tier, sim.Gold, wall.MaxHp, wall.Hp));
            Assert.AreEqual(21, sim.UpgradePrice(wall), "tier III costs floor(1.5 x base)");
            Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(wall));
            Assert.AreEqual((3, gold - 35, 128, 83), (wall.Tier, sim.Gold, wall.MaxHp, wall.Hp), "ceil(67 x 128 / 104) = ceil(82.5)");
            Assert.AreEqual(UpgradeRejection.MaxTier, sim.TryUpgrade(wall));
            Assert.AreEqual(gold - 35, sim.Gold, "a refused upgrade charges nothing");
            Assert.AreEqual(14 + 14 + 21, wall.TotalPaid);
            var before = sim.Gold;
            Assert.AreEqual(SellRejection.None, sim.TrySell(wall));
            Assert.AreEqual(before + 24, sim.Gold, "sale refunds floor(49 x 0.5), not half the purchase price");

            var poor = NewRun(Wave((0, EnemyKind.Grunt)), Defs(gold: 49 + 48));
            var tower = Place(poor, BuildingKind.Tower, 4, 4);
            Assert.AreEqual(UpgradeRejection.InsufficientGold, poor.TryUpgrade(tower));
            Assert.AreEqual((1, 48, 49), (tower.Tier, poor.Gold, tower.TotalPaid), "nothing changes without the gold");
            Assert.AreEqual(UpgradeRejection.NotUpgradable, poor.TryUpgrade(poor.Keep), "the Keep has no tiers");
            Assert.IsTrue(poor.StartNight());
            Assert.AreEqual(UpgradeRejection.NotDay, poor.TryUpgrade(tower));
        }

        [Test]
        public void LegacyProfilesHaveNoUpgrades()
        {
            var legacy = ReleaseDefinitions.CreateDefault();
            Assert.IsNull(ReleaseCampaign.Walking(legacy).Tower.Tiers, "release-rules-3 definitions");
            Assert.IsNotNull(ReleaseCampaign.Contact(legacy).Tower.Tiers, "release-rules-4 definitions");
            var sim = NewRun(Wave((0, EnemyKind.Grunt)), legacy);
            var tower = Place(sim, BuildingKind.Tower, 4, 4);
            var gold = sim.Gold;
            Assert.AreEqual(UpgradeRejection.NotUpgradable, sim.TryUpgrade(tower));
            Assert.AreEqual((1, gold), (tower.Tier, sim.Gold));
        }

        /// <summary>Each building-aware value follows the definition plus its tier's entry, at tiers I, II and III.</summary>
        [TestCase(BuildingKind.Wall)]
        [TestCase(BuildingKind.Tower)]
        [TestCase(BuildingKind.House)]
        [TestCase(BuildingKind.Farm)]
        [TestCase(BuildingKind.SpikeTrap)]
        [TestCase(BuildingKind.Cannon)]
        [TestCase(BuildingKind.Watchtower)]
        [TestCase(BuildingKind.RepairStation)]
        public void EffectiveValuesFollowTheTierTable(BuildingKind kind)
        {
            var sim = NewRun(Wave((0, EnemyKind.Grunt)));
            var b = Place(sim, kind, 4, 4);
            var buffed = kind == BuildingKind.Watchtower ? Place(sim, BuildingKind.Tower, 5, 4) : null;
            var def = b.Definition;
            for (var tier = 1; tier <= BuildingUpgrades.MaxTier; tier++)
            {
                if (tier > 1) Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(b));
                var t = BuildingUpgrades.For(def, tier);
                var label = $"{kind} tier {tier}";
                Assert.AreEqual(def.HasHp ? def.MaxHp * (100 + t.HpPercent) / 100 : 0, b.MaxHp, label + " max HP");
                if (def.CanAttack)
                {
                    Assert.AreEqual(def.AttackDamage * (100 + t.DamagePercent) / 100, sim.TowerDamage(b), label + " damage");
                    Assert.AreEqual(def.Range + t.RangeBonus, sim.TowerRange(b), 1e-9, label + " range");
                    Assert.AreEqual(t.IntervalSeconds > 0 ? t.IntervalSeconds : def.AttackIntervalSeconds, RunSimulation.AttackIntervalSeconds(b), 1e-9, label + " interval");
                    if (kind == BuildingKind.Cannon) Assert.AreEqual(def.SplashRadius + t.SplashBonus, sim.SplashRadius(b), 1e-9, label + " splash");
                }

                if (kind == BuildingKind.House || kind == BuildingKind.Farm) Assert.AreEqual(def.DailyIncome + t.IncomeBonus, sim.BuildingIncome(b), label + " income");
                if (kind == BuildingKind.SpikeTrap) Assert.AreEqual(t.TrapDamage > 0 ? t.TrapDamage : def.TrapDamage, sim.TrapDamage(b), label + " trap damage");
                if (kind == BuildingKind.RepairStation)
                    Assert.AreEqual((t.RepairBudget > 0 ? t.RepairBudget : def.RepairBudget, t.RepairReach > 0 ? t.RepairReach : def.RepairReach),
                        (sim.RepairBudget(b), RunSimulation.RepairReach(b)), label + " repair budget and reach");
                if (buffed != null)
                {
                    Assert.AreEqual(buffed.Definition.Range + (t.WatchRangeBonus > 0 ? t.WatchRangeBonus : def.RangeBonus), sim.TowerRange(buffed), 1e-9, label + " range bonus");
                    Assert.AreEqual(buffed.Definition.AttackDamage * (100 + t.SpotterPercent) / 100, sim.TowerDamage(buffed), label + " spotter");
                }
            }
        }

        [TestCase(2, 50)]
        [TestCase(3, 100)]
        public void TheSecondArrowHitsTheEnemyNearestTheFirstNotTheLowestId(int tier, int percent)
        {
            // A Brute (slower) spawns with the first Grunt; a second Grunt follows two steps later, just behind the first.
            var sim = NewRun(Wave((0, EnemyKind.Grunt), (0, EnemyKind.Brute), (2, EnemyKind.Grunt)));
            var tower = Upgraded(sim, BuildingKind.Tower, 2, 9, tier);
            Assert.IsTrue(sim.StartNight());
            var shots = new List<SimEvent>();
            for (var i = 0; i < 400 && shots.Count == 0; i++)
            {
                sim.Step();
                shots = Drain(sim).Where(e => e.Type == SimEventType.TowerShot && e.SourceId == tower.Id).ToList();
            }

            Assert.AreEqual(2, shots.Count, "two arrows on the first shot");
            var primary = sim.GetEnemy(shots[0].TargetId);
            var range = sim.TowerRange(tower);
            var others = sim.Enemies.Where(e => e != primary && RunSimulation.InRange(tower, e.X, e.Z, range)).ToList();
            var nearest = others.OrderBy(e => (e.X - primary.X) * (e.X - primary.X) + (e.Z - primary.Z) * (e.Z - primary.Z)).ThenBy(e => e.Id).First();
            Assert.AreNotEqual(others.Min(e => e.Id), nearest.Id, "precondition: nearest and lowest ID differ");
            Assert.AreEqual(nearest.Id, shots[1].TargetId);
            var full = RunSimulation.BaseDamage(tower);
            Assert.AreEqual((full, full * percent / 100), (primary.Definition.MaxHp - primary.Hp, nearest.Definition.MaxHp - nearest.Hp));
            Assert.AreEqual(primary.Id, tower.TargetEnemyId, "the tower keeps its own target");
        }

        [Test]
        public void SlowTakesTheStrongerPercentAndTheLaterExpiryAndNeverStacks()
        {
            // A Runner (105 units/step) at 50% crosses a tile in 23 steps, before the 40-step tier III slow runs out.
            var defs = Defs();
            defs.Runner.MaxHp = 200; // survives both traps
            var sim = NewRun(Wave((0, EnemyKind.Runner)), defs);
            var strong = Upgraded(sim, BuildingKind.SpikeTrap, 2, 10, 3);
            var weak = Upgraded(sim, BuildingKind.SpikeTrap, 3, 10, 2);
            Assert.IsTrue(sim.StartNight());
            var first = StepUntilEvent(sim, e => e.Type == SimEventType.TrapTriggered && e.SourceId == strong.Id);
            var runner = sim.Enemies.Single();
            Assert.AreEqual((50, first.Tick + 40, 52), (runner.SlowPercent, runner.SlowUntilTick, sim.MoveUnits(runner)), "floor(105 x 0.5)");
            var second = StepUntilEvent(sim, e => e.Type == SimEventType.TrapTriggered && e.SourceId == weak.Id);
            Assert.Less(second.Tick, first.Tick + 40, "precondition: the first slow still runs");
            Assert.AreEqual((50, second.Tick + 30), (runner.SlowPercent, runner.SlowUntilTick), "stronger percent, refreshed expiry, no stacking");
            StepUntil(sim, () => sim.Tick > runner.SlowUntilTick);
            Assert.AreEqual(105, sim.MoveUnits(runner), "full speed after expiry");
        }

        [Test]
        public void BossesTakeHalfTheSlowAndASlowedEnemyStillMovesOneUnit()
        {
            var sim = NewRun(Wave((0, EnemyKind.SiegeGolem)));
            var trap = Upgraded(sim, BuildingKind.SpikeTrap, 2, 10, 3);
            Assert.IsTrue(sim.StartNight());
            StepUntilEvent(sim, e => e.Type == SimEventType.TrapTriggered && e.SourceId == trap.Id);
            var golem = sim.Enemies.Single();
            Assert.AreEqual((25, golem.MoveUnitsPerStep * 75 / 100), (golem.SlowPercent, sim.MoveUnits(golem)));

            var defs = Defs();
            defs.Grunt.SpeedTilesPerSecond = 1.0 / 60; // one unit per step
            var crawl = NewRun(Wave((0, EnemyKind.Grunt)), defs);
            var near = Upgraded(crawl, BuildingKind.SpikeTrap, 1, 10, 3);
            Assert.IsTrue(crawl.StartNight());
            StepUntilEvent(crawl, e => e.Type == SimEventType.TrapTriggered && e.SourceId == near.Id, 1300);
            Assert.AreEqual(1, crawl.MoveUnits(crawl.Enemies.Single()), "floor(1 x 0.5) = 0, raised to 1");
        }

        [Test]
        public void ConcussionSlowsEveryEnemyTheCannonHits()
        {
            var sim = NewRun(Wave((0, EnemyKind.Grunt), (0, EnemyKind.Grunt)));
            var cannon = Upgraded(sim, BuildingKind.Cannon, 3, 11, 2);
            Assert.IsTrue(sim.StartNight());
            var shot = StepUntilEvent(sim, e => e.Type == SimEventType.TowerShot && e.SourceId == cannon.Id);
            var slow = BuildingUpgrades.For(cannon.Definition, 2);
            Assert.AreEqual(2, sim.Enemies.Count(e => e.SlowPercent == slow.SlowPercent && e.SlowUntilTick == shot.Tick + PrototypeDefinitions.ToTicks(slow.SlowSeconds)),
                "primary and splash target");
        }

        [Test]
        public void WallSpikesReflectAndStoneIgnoresTheWallMultiplier()
        {
            var sim = NewRun(Wave((0, EnemyKind.Grunt)));
            var wall = Place(sim, BuildingKind.Wall, 4, 4);
            var brute = new Enemy(90, 0, sim.Definitions.Brute, new GridPos(4, 5));
            var bomber = new Enemy(91, 0, sim.Definitions.Bomber, new GridPos(4, 5));
            Assert.AreEqual((0, brute.Definition.AttackDamage * 2), (sim.WallReflection(wall), sim.EnemyHitDamage(brute, wall)), "tier I");
            Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(wall));
            Assert.AreEqual((2, brute.Definition.AttackDamage * 2), (sim.WallReflection(wall), sim.EnemyHitDamage(brute, wall)), "tier II: spikes");
            Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(wall));
            Assert.AreEqual((4, brute.Definition.AttackDamage, bomber.Definition.BlastDamage),
                (sim.WallReflection(wall), sim.EnemyHitDamage(brute, wall), sim.BlastDamage(bomber, wall, true)), "tier III: stone");
            Place(sim, BuildingKind.SpikeTrap, 5, 4);
            Assert.AreEqual(4 + EconomyRules.WallTrapReflect, sim.WallReflection(wall), "spikes add to the trap synergy");
        }

        [Test]
        public void MarketHallAddsOncePerFarm()
        {
            var sim = NewRun(Wave((0, EnemyKind.Grunt)));
            var farm = Place(sim, BuildingKind.Farm, 4, 4); // cells 4..5
            var a = Place(sim, BuildingKind.House, 6, 4);
            var b = Place(sim, BuildingKind.House, 6, 5);
            var linked = farm.Definition.DailyIncome + EconomyRules.FarmHouseBonus;
            Assert.AreEqual(linked, sim.BuildingIncome(farm));
            while (a.Tier < 3) Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(a));
            Assert.AreEqual(linked + 5, sim.BuildingIncome(farm), "a tier III House beside the Farm");
            while (b.Tier < 3) Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(b));
            Assert.AreEqual(linked + 5, sim.BuildingIncome(farm), "a second one adds nothing more");
        }

        [Test]
        public void AGranaryDestroyedInCombatPaysHalfItsIncomeAtTheNextDawn()
        {
            var sim = NewCampaign(Defs(), Wave((0, EnemyKind.Archer)), RewardAndPerkTests.Empty());
            var farm = Upgraded(sim, BuildingKind.Farm, 2, 7, 3);
            farm.Hp = 1;
            Place(sim, BuildingKind.Tower, 2, 11);
            Place(sim, BuildingKind.Tower, 3, 11);
            var income = sim.BuildingIncome(farm);
            Assert.AreEqual(1, income % 2, "precondition: an odd income tells floor from rounding");
            Assert.IsTrue(sim.StartNight());
            RunToEnd(sim);
            Assert.AreEqual((RunState.Reward, 1), (sim.State, sim.LastNight.BuildingsLost), "the Archer destroyed the Farm and died");
            Assert.AreEqual(0, sim.IncomeForecast().Granary, "never part of a forecast");
            Assert.IsTrue(sim.DebugSkipReward());
            Assert.AreEqual((income / 2, 0), (sim.LastIncome.Granary, sim.LastIncome.Farms));
        }

        [Test]
        public void TierThreeRepairStationCaresForTheKeepAndWatchtowerTiersReachFurther()
        {
            var sim = NewCampaign(Defs(), RewardAndPerkTests.Empty(), RewardAndPerkTests.Empty(), RewardAndPerkTests.Empty());
            var station = Upgraded(sim, BuildingKind.RepairStation, 11, 11, 2); // a corner from the Keep, within reach
            sim.Keep.Hp = 300;
            Assert.IsTrue(sim.StartNight());
            sim.Step();
            Assert.IsTrue(sim.DebugSkipReward());
            Assert.AreEqual(300, sim.Keep.Hp, "tier II: not the Keep");
            Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(station));
            Assert.IsTrue(sim.StartNight());
            sim.Step();
            Assert.IsTrue(sim.DebugSkipReward());
            Assert.AreEqual(300 + sim.RepairBudget(station), sim.Keep.Hp, "tier III: Keep care");

            var tower = Place(sim, BuildingKind.Tower, 5, 4);
            var far = Place(sim, BuildingKind.Tower, 6, 6); // Chebyshev 2 from the Watchtower
            Place(sim, BuildingKind.Watchtower, 5, 3); // tier I beside the first tower
            var high = Upgraded(sim, BuildingKind.Watchtower, 4, 4, 2);
            Assert.AreEqual((2, 0), (sim.BuffingWatchtowerTier(tower), sim.BuffingWatchtowerTier(far)), "the highest tier in reach applies; tier II reaches only edges");
            Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(high));
            Assert.AreEqual((3, 3), (sim.BuffingWatchtowerTier(tower), sim.BuffingWatchtowerTier(far)), "tier III reaches Chebyshev 2");
        }

        [Test]
        public void PerksComposeWithTiers()
        {
            RunSimulation sim = null;
            var perks = new[] { PerkRules.ThriftyConstruction, PerkRules.SignalFires, PerkRules.FieldEngineers };
            for (uint seed = 0; seed < 5000 && sim == null; seed++)
            {
                var run = new RunSimulation(new RunConfig
                {
                    Definitions = Defs(), Seed = seed,
                    Campaign = new CampaignConfig
                    {
                        Label = "perks", Nights = Enumerable.Range(0, 4).Select(_ => RewardAndPerkTests.Empty()).ToArray(), OffersRewards = true,
                        PerkPool = PerkRules.ReleaseIds,
                    },
                });
                var ok = true;
                foreach (var perk in perks)
                {
                    Assert.IsTrue(run.StartNight());
                    run.Step();
                    if (!run.CurrentOffer.PerkIds.Contains(perk))
                    {
                        ok = false;
                        break;
                    }

                    Assert.IsTrue(run.ChoosePerk(run.CurrentOffer.OfferId, perk));
                }

                if (ok) sim = run;
            }

            Assert.IsNotNull(sim, "no seed offered the perks");
            var tower = Place(sim, BuildingKind.Tower, 4, 4);
            Assert.AreEqual(44, sim.UpgradePrice(tower), "Thrifty on tier II: floor(49 x 0.9)");
            Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(tower));
            Assert.AreEqual(65, sim.UpgradePrice(tower), "Thrifty after the tier III factor: floor(73 x 0.9), not floor(44 x 1.5) = 66");

            var watchtower = Upgraded(sim, BuildingKind.Watchtower, 8, 4, 3);
            Assert.AreEqual((3, 0), (sim.BuffingWatchtowerTier(Place(sim, BuildingKind.Tower, 11, 4)), sim.BuffingWatchtowerTier(Place(sim, BuildingKind.Tower, 12, 4))),
                "Signal Fires extends tier III's reach to Chebyshev 3");
            Assert.IsNotNull(watchtower);
            var station = Upgraded(sim, BuildingKind.RepairStation, 4, 13, 2);
            Assert.AreEqual(60 + PerkRules.FieldEngineersBudget, sim.RepairBudget(station));
            Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(station));
            Assert.AreEqual(80 + 2 * PerkRules.FieldEngineersBudget, sim.RepairBudget(station), "Field Engineers adds 30 more with Keep care");
        }

        [Test]
        public void TiersRoundTripThroughSavesAndOlderOrInvalidRecordsAreHandled()
        {
            var catalog = new RulesCatalog(RulesProfile.TwelveNightsContact(ReleaseDefinitions.CreateDefault()), RulesProfile.TwelveNightsWalking(ReleaseDefinitions.CreateDefault()));
            var sim = new RunSimulation(new RunConfig
            {
                Definitions = catalog.Find(ReleaseCampaign.RulesV4, ReleaseCampaign.CampaignId).Definitions, Seed = 3,
                Terrain = MapProfiles.Generate(MapProfiles.Meadow, 3), Campaign = ReleaseCampaign.Create(3, MapProfiles.Meadow),
            });
            var wall = Upgraded(sim, BuildingKind.Wall, 9, 9, 3);
            wall.Hp = 100; // above tier I's 80
            var json = JsonUtility.ToJson(CheckpointCodec.Capture(sim));

            var back = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(json), catalog, out var problem, out var message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            var w = back.GetBuilding(wall.Id);
            Assert.AreEqual((3, 49, 128, 100), (w.Tier, w.TotalPaid, w.MaxHp, w.Hp));

            var older = System.Text.RegularExpressions.Regex.Replace(json, "\"tier\":\\d+,\"paid\":\\d+", "\"tier\":0,\"paid\":0");
            Assert.AreNotEqual(json, older);
            CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(older), catalog, out problem, out message);
            Assert.AreEqual(CheckpointProblem.Invalid, problem, "without its tier the Wall's 100 HP exceeds tier I's 80");
            StringAssert.Contains("HP 100 outside 1..80", message);
            wall.Hp = 80;
            var olderValid = System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(CheckpointCodec.Capture(sim)), "\"tier\":\\d+,\"paid\":\\d+", "\"tier\":0,\"paid\":0");
            w = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(olderValid), catalog, out problem, out message).GetBuilding(wall.Id);
            Assert.AreEqual((CheckpointProblem.None, 1, 14), (problem, w.Tier, w.TotalPaid), "a missing tier is tier I at its purchase price");

            var legacy = json.Replace("\"rulesVersion\":\"" + ReleaseCampaign.RulesV4 + "\"", "\"rulesVersion\":\"" + ReleaseCampaign.RulesV3 + "\"");
            Assert.AreNotEqual(json, legacy);
            CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(legacy), catalog, out problem, out message);
            Assert.AreEqual(CheckpointProblem.Invalid, problem, "release-rules-3 has no tiers");
            StringAssert.Contains("invalid tier", message);
        }
    }
}
