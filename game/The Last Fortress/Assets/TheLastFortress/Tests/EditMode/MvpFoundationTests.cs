using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static TheLastFortress.Tests.SimTestUtil;

namespace TheLastFortress.Tests
{
    /// <summary>Plan 003: multi-cell footprints, trap layer, economy, Wall/Trap synergy and multi-night transitions.</summary>
    public class FootprintTests
    {
        [Test]
        public void FarmOccupiesFourCellsAsOneEntityAndReleasesOnce()
        {
            var sim = NewMvpRun();
            var revision = sim.Grid.Revision;
            var farm = Place(sim, BuildingKind.Farm, 5, 5);
            Assert.AreEqual(revision + 1, sim.Grid.Revision, "one revision per solid transaction");
            Assert.AreEqual(190, sim.Gold);
            foreach (var c in new[] { new GridPos(5, 5), new GridPos(6, 5), new GridPos(5, 6), new GridPos(6, 6) })
                Assert.AreSame(farm, sim.Grid.GetOccupant(c), $"{c} resolves to the owning Farm");
            Assert.AreEqual(1, sim.Grid.AllBuildings().Count(b => b.Kind == BuildingKind.Farm), "enumerated once, not per cell");

            Assert.AreEqual(SellRejection.None, sim.TrySell(farm));
            Assert.AreEqual(revision + 2, sim.Grid.Revision);
            Assert.AreEqual(220, sim.Gold, "refund floor(60 / 2) once");
            foreach (var c in farm.Cells) Assert.IsNull(sim.Grid.GetOccupant(c));
            Assert.AreEqual(SellRejection.AlreadyRemoved, sim.TrySell(farm));
            Assert.AreEqual(220, sim.Gold);
        }

        [Test]
        public void InvalidOrUnaffordableFootprintNeverMutates()
        {
            var sim = NewMvpRun();
            var trap = Place(sim, BuildingKind.SpikeTrap, 4, 4);
            var before = sim.CaptureSnapshot();
            Assert.AreEqual(PlacementRejection.ReservedRing, sim.TryPlace(BuildingKind.Farm, new GridPos(18, 5), out _), "east column on the ring");
            Assert.AreEqual(PlacementRejection.Occupied, sim.TryPlace(BuildingKind.Farm, new GridPos(9, 9), out _), "one cell is the Keep");
            Assert.AreEqual(PlacementRejection.Occupied, sim.TryPlace(BuildingKind.Farm, new GridPos(3, 3), out _), "one cell holds a trap");
            Assert.AreEqual(PlacementRejection.Occupied, sim.TryPlace(BuildingKind.Wall, trap.Cell, out _), "no trap under a Wall");
            Assert.AreEqual(before, sim.CaptureSnapshot());

            Place(sim, BuildingKind.Tower, 2, 2);
            Place(sim, BuildingKind.Tower, 2, 4);
            Place(sim, BuildingKind.Tower, 2, 6);
            Assert.AreEqual(5, sim.Gold);
            var poor = sim.CaptureSnapshot();
            Assert.AreEqual(PlacementRejection.InsufficientGold, sim.TryPlace(BuildingKind.Farm, new GridPos(5, 5), out _));
            Assert.AreEqual(poor, sim.CaptureSnapshot());
        }

        [Test]
        public void TrapsAreWalkableAndDoNotInvalidateRoutes()
        {
            var sim = NewMvpRun();
            var route = sim.GetLiveRoute(WavePlan.MainEntry);
            var revision = sim.Grid.Revision;
            var trap = Place(sim, BuildingKind.SpikeTrap, 5, 10);
            Assert.AreEqual(revision, sim.Grid.Revision);
            Assert.AreSame(route, sim.GetLiveRoute(WavePlan.MainEntry), "cached solid route survives a trap");
            CollectionAssert.Contains(route.Cells, trap.Cell);
            Assert.AreEqual(PlacementRejection.Occupied, sim.CheckPlacement(BuildingKind.SpikeTrap, trap.Cell));
            Assert.AreEqual(SellRejection.None, sim.TrySell(trap));
            Assert.AreEqual(revision, sim.Grid.Revision);
            Assert.AreEqual(250 - 35 + 17, sim.Gold);
        }

        [Test]
        public void FarmGhostPreviewMatchesCommittedRoute()
        {
            var sim = NewMvpRun();
            var anchor = new GridPos(5, 9); // covers (5..6, 9..10): blocks the straight west route
            var revision = sim.Grid.Revision;
            var preview = sim.PreviewRoute(WavePlan.MainEntry, BuildingKind.Farm, anchor);
            Assert.AreEqual(revision, sim.Grid.Revision);
            Assert.AreEqual(250, sim.Gold);
            CollectionAssert.DoesNotContain(preview.Cells, new GridPos(5, 10));
            Place(sim, BuildingKind.Farm, anchor.X, anchor.Z);
            var actual = sim.GetLiveRoute(WavePlan.MainEntry);
            Assert.AreEqual(string.Join(" ", preview.Cells), string.Join(" ", actual.Cells));
        }
    }

    public class EconomyTests
    {
        [Test]
        public void IncomeCountsUniqueEdgeAdjacencyOnly()
        {
            var sim = NewMvpRun();
            Place(sim, BuildingKind.Farm, 5, 5); // cells (5..6, 5..6)
            Place(sim, BuildingKind.House, 7, 5); // shares an edge
            Place(sim, BuildingKind.House, 7, 6); // second House on the same Farm: no extra bonus
            var cornerFarm = Place(sim, BuildingKind.Farm, 12, 5); // cells (12..13, 5..6)
            Place(sim, BuildingKind.House, 14, 7); // touches (13,6) only at a corner
            var income = sim.IncomeForecast();
            Assert.AreEqual(20, income.Keep);
            Assert.AreEqual(45, income.Houses);
            Assert.AreEqual(30 + 25, income.Farms, "+5 once for the linked Farm; corners do not count");
            Assert.AreEqual(1, income.LinkedFarms);
            var pairs = EconomyRules.FarmHousePairs(sim.Grid);
            Assert.AreEqual(2, pairs.Count, "one link per unique Farm/House pair");
            Assert.IsFalse(pairs.Any(p => p.farm == cornerFarm));
        }

        [TestCase(7, 100, 7, 2, TestName = "OddMissingHpRoundsCostUp")]
        [TestCase(7, 1, 5, 1, TestName = "GoldLimitsRestoredHp")]
        [TestCase(0, 100, 0, 0, TestName = "FullHealthIsNoOp")]
        [TestCase(7, 0, 0, 0, TestName = "ZeroGoldIsNoOp")]
        public void RepairQuoteUsesFiveHpPerGold(int missing, int gold, int restored, int cost)
        {
            EconomyRules.RepairQuote(missing, gold, out var r, out var c);
            Assert.AreEqual(restored, r);
            Assert.AreEqual(cost, c);
        }

        [Test]
        public void RepairIsDayOnlyAtomicAndExcludesTraps()
        {
            var sim = NewMvpRun(SingleGrunt());
            var wall = Place(sim, BuildingKind.Wall, 5, 5);
            var trap = Place(sim, BuildingKind.SpikeTrap, 6, 6);
            Assert.AreEqual(RepairRejection.FullHealth, sim.TryRepair(wall));
            Assert.AreEqual(RepairRejection.NotRepairable, sim.TryRepair(trap));
            wall.Hp -= 7;
            var gold = sim.Gold;
            sim.RepairQuote(wall, out var restored, out var cost);
            Assert.AreEqual((7, 2), (restored, cost));
            Assert.AreEqual(RepairRejection.None, sim.TryRepair(wall));
            Assert.AreEqual(100, wall.Hp);
            Assert.AreEqual(gold - 2, sim.Gold);

            sim.Keep.Hp -= 9;
            Assert.IsTrue(sim.StartNight());
            Assert.AreEqual(RepairRejection.NotDay, sim.TryRepair(sim.Keep));
            Assert.IsTrue(sim.SetPaused(true));
            Assert.AreEqual(RepairRejection.NotDay, sim.TryRepair(sim.Keep), "not even while paused");
            Assert.AreEqual(491, sim.Keep.Hp);
        }
    }

    public class TrapAndReflectionTests
    {
        [Test]
        public void TrapHitsOnceOnEntryNotWhileStanding()
        {
            var sim = NewMvpRun(SingleGrunt());
            var trap = Place(sim, BuildingKind.SpikeTrap, 9, 10); // the Grunt's final attack cell beside the Keep
            Assert.IsTrue(sim.StartNight());
            var grunt = sim.Enemies[0];
            StepUntil(sim, () => grunt.TargetBuildingId == sim.Keep.Id);
            Assert.AreEqual(trap.Cell, grunt.Cell);
            Assert.AreEqual(22, grunt.Hp, "8 damage on completing entry");
            for (var i = 0; i < 200; i++) sim.Step();
            Assert.AreEqual(22, grunt.Hp, "standing on the trap does not tick");
            Assert.AreEqual(1, Drain(sim).Count(e => e.Type == SimEventType.TrapTriggered));
        }

        [Test]
        public void TwoTrapsReflectOnceAndLethalHitStillReflects()
        {
            var sim = NewMvpRun(SingleGrunt());
            EnclosureWalls(sim);
            var west = sim.Grid.GetOccupant(new GridPos(9, 10));
            Place(sim, BuildingKind.SpikeTrap, 9, 9); // two traps beside the breached Wall, off the route
            Place(sim, BuildingKind.SpikeTrap, 9, 11);
            Assert.AreEqual(EconomyRules.WallTrapReflect, sim.WallReflection(west), "3, not 3 per trap");
            Assert.IsTrue(sim.StartNight());
            var grunt = sim.Enemies[0];
            var events = new List<SimEvent>();
            for (var i = 0; i < 4000 && sim.State == RunState.Night; i++)
            {
                sim.Step();
                sim.DrainEvents(events);
            }

            // Ten 10-damage hits destroy the 100 HP Wall; ten 3-damage reflections kill the 30 HP Grunt on that same hit.
            var hits = events.Where(e => e.Type == SimEventType.EnemyAttacked && e.TargetId == west.Id).ToList();
            var reflections = events.Where(e => e.Type == SimEventType.DamageReflected).ToList();
            Assert.AreEqual(10, hits.Count);
            Assert.AreEqual(10, reflections.Count);
            var destroyed = events.Single(e => e.Type == SimEventType.BuildingDestroyed);
            var killed = events.Single(e => e.Type == SimEventType.EnemyKilled);
            Assert.AreEqual(destroyed.Tick, killed.Tick, "the Wall's lethal hit still reflected");
            Assert.AreEqual(west.Id, killed.TargetId);
            Assert.IsFalse(grunt.Alive);
            Assert.AreEqual(0, events.Count(e => e.Type == SimEventType.EnemyAttacked && e.TargetId == sim.Keep.Id));
            Assert.AreEqual(RunState.Victory, sim.State);
        }
    }

    public class CampaignTests
    {
        static WavePlan OneGrunt(string name) =>
            new WavePlan(name, new[] { new SpawnEntry(0, MvpCampaign.West, 0) }, new[] { MvpCampaign.West });

        [Test]
        public void ClearedNightKeepsFortressAndPaysIncomeOnce()
        {
            var campaign = new CampaignConfig { Label = "test", Nights = new[] { OneGrunt("N1"), OneGrunt("N2") } };
            var sim = NewMvpRun(campaign: campaign);
            // Keep sealed with a House as the west blocker; the Tower only reaches the Keep's west neighbor.
            PlaceWalls(sim, (11, 10), (10, 9), (10, 11));
            var house = Place(sim, BuildingKind.House, 9, 10);
            var farm = Place(sim, BuildingKind.Farm, 8, 11); // (9,11) shares an edge with the House
            var tower = Place(sim, BuildingKind.Tower, 11, 13);
            Assert.AreEqual(20 + 15 + 30, sim.IncomeForecast().Total);

            Assert.IsTrue(sim.StartNight());
            var events = new List<SimEvent>();
            var hp = new Dictionary<int, int>();
            for (var i = 0; i < 4000 && sim.State == RunState.Night; i++)
            {
                foreach (var b in sim.Grid.AllBuildings()) hp[b.Id] = b.Hp;
                var gold = sim.Gold;
                sim.Step();
                sim.DrainEvents(events);
                if (sim.State == RunState.Day) Assert.AreEqual(gold + 45, sim.Gold, "Keep 20 + Farm 25; destroyed House earns nothing");
            }

            Assert.AreEqual(RunState.Day, sim.State);
            Assert.AreEqual(2, sim.NightNumber);
            Assert.IsTrue(house.Removed);
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.IncomePaid));
            foreach (var b in sim.Grid.AllBuildings()) Assert.AreEqual(hp[b.Id], b.Hp, $"{b.Kind} HP preserved into Day 2");
            Assert.AreSame(farm, sim.GetBuilding(farm.Id));
            Assert.AreEqual(-1, tower.TargetEnemyId);
            Assert.AreEqual(0, sim.AliveEnemyCount);
            Assert.AreEqual(1, sim.ScheduledRemaining, "next night's plan is ready, not yet started");

            var day2Gold = sim.Gold;
            for (var i = 0; i < 50; i++) sim.Step();
            Assert.AreEqual(day2Gold, sim.Gold, "repeated stepping in Day never pays again");
            Assert.AreEqual(RunState.Day, sim.State, "next night waits for a manual start");

            Assert.IsTrue(sim.StartNight());
            Assert.AreEqual(2, sim.Enemies[0].Id, "enemy IDs stay unique across nights");
            RunToEnd(sim);
            sim.DrainEvents(events);
            Assert.AreEqual(RunState.Victory, sim.State, "clearing the final night wins");
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.IncomePaid), "no income after the final night");
        }

        [Test]
        public void FoundationProfileIsWinnableOnRealBudgetAndRestartsFresh()
        {
            var campaign = MvpCampaign.CreateFoundation();
            var sim = NewMvpRun(campaign: campaign);
            var fresh = sim.CaptureSnapshot();
            CollectionAssert.AreEqual(new[] { 10, 16, 22 }, campaign.Nights.Select(n => n.Spawns.Count));

            // Day 1: two Towers covering the west approach and a House. Later Days spend income on an east Tower.
            Place(sim, BuildingKind.Tower, 8, 9);
            Place(sim, BuildingKind.Tower, 8, 11);
            Place(sim, BuildingKind.House, 12, 12);
            var paid = new List<int>();
            for (var night = 1; night <= 3; night++)
            {
                if (night == 2) Place(sim, BuildingKind.Tower, 12, 9);
                if (night == 3 && sim.Gold >= 70) Place(sim, BuildingKind.Tower, 12, 11);
                Assert.IsTrue(sim.StartNight(), $"night {night}");
                for (var i = 0; i < 20000 && sim.State == RunState.Night; i++)
                {
                    var gold = sim.Gold;
                    sim.Step();
                    if (sim.State == RunState.Day) paid.Add(sim.Gold - gold);
                }

                Assert.AreNotEqual(RunState.Defeat, sim.State, $"night {night}");
            }

            Assert.AreEqual(RunState.Victory, sim.State);
            CollectionAssert.AreEqual(new[] { 35, 35 }, paid, "Keep 20 + House 15 on Days 2 and 3 only");

            sim.Restart();
            Assert.AreEqual(fresh, sim.CaptureSnapshot(), "restart resets the whole campaign");
            Assert.AreEqual(1, sim.NightNumber);
            Assert.AreEqual(250, sim.Gold);

            Assert.IsTrue(sim.StartNight());
            RunToEnd(sim);
            Assert.AreEqual(RunState.Defeat, sim.State, "Keep-only loses Night 1");
            sim.Restart();
            Assert.AreEqual(fresh, sim.CaptureSnapshot());
        }

        [Test]
        public void CampaignCannotBeStressFixture()
        {
            Assert.Throws<System.ArgumentException>(() => new RunSimulation(new RunConfig
            {
                Definitions = MvpDefinitions.CreateDefault(), Campaign = MvpCampaign.CreateFoundation(), StressMode = true,
            }));
        }
    }
}
