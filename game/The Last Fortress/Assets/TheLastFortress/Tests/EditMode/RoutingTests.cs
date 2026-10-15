using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static TheLastFortress.Tests.SimTestUtil;

namespace TheLastFortress.Tests
{
    public class RoutingTests
    {
        static readonly GridPos Entry = WavePlan.MainEntry;

        static string Cells(Route r) => string.Join(" ", r.Cells.Select(c => c.ToString()));

        static void AssertCardinalPath(Route r)
        {
            for (var i = 1; i < r.Cells.Count; i++)
                Assert.IsTrue(r.Cells[i - 1].IsCardinalNeighbor(r.Cells[i]), $"non-cardinal step in {Cells(r)}");
            CollectionAssert.DoesNotContain(r.Cells, GridState.KeepCell);
        }

        [Test]
        public void EmptyMapRouteIsStraightToWestNeighbor()
        {
            var sim = NewRun();
            var r = sim.GetLiveRoute(Entry);
            Assert.IsTrue(r.Found);
            Assert.IsFalse(r.IsBreach);
            Assert.AreEqual(new GridPos(9, 10), r.Goal);
            Assert.AreEqual(9, r.Cost);
            AssertCardinalPath(r);
        }

        // Fixture 10
        [Test]
        public void DetourIsFollowedAndWallsNeverDamaged()
        {
            var sim = NewRun(SingleGrunt());
            PlaceWalls(sim, (5, 9), (5, 10), (5, 11));
            var walls = new[] { new GridPos(5, 9), new GridPos(5, 10), new GridPos(5, 11) }.Select(c => sim.Grid.GetOccupant(c)).ToArray();

            var first = sim.GetLiveRoute(Entry);
            Assert.IsTrue(first.Found);
            Assert.IsFalse(first.IsBreach, "an open detour exists, so no breach");
            Assert.AreEqual(13, first.Cost);
            Assert.AreEqual(new GridPos(10, 9), first.Goal, "equal-cost goals resolve by (z,x) ascending");
            AssertCardinalPath(first);
            foreach (var w in walls) CollectionAssert.DoesNotContain(first.Cells, w.Cell);

            // Repeated queries (cached, uncached, fresh planner) are identical.
            for (var i = 0; i < 5; i++)
            {
                Assert.AreEqual(Cells(first), Cells(sim.Planner.Plan(sim.Grid, Entry, null)));
                Assert.AreEqual(Cells(first), Cells(new RoutePlanner().Plan(sim.Grid, Entry, null)));
                Assert.AreSame(first, sim.GetLiveRoute(Entry));
            }

            Assert.IsTrue(sim.StartNight());
            var grunt = sim.Enemies[0];
            var visited = new List<GridPos> { grunt.Cell };
            StepUntil(sim, () =>
            {
                if (visited[visited.Count - 1] != grunt.Cell) visited.Add(grunt.Cell);
                return grunt.TargetBuildingId == sim.Keep.Id;
            });
            Assert.AreEqual(Cells(first), string.Join(" ", visited), "actual path equals displayed route");
            StepUntil(sim, () => sim.Keep.Hp < 450);
            foreach (var w in walls) Assert.AreEqual(100, w.Hp);
            Assert.IsFalse(Drain(sim).Any(e => e.Type == SimEventType.EnemyAttacked && walls.Any(w => w.Id == e.TargetId)));
        }

        // Fixture 11
        [Test]
        public void FullEnclosureBreachesWestWallFromWestNeighbor()
        {
            var sim = NewRun(SingleGrunt());
            EnclosureWalls(sim);
            var west = sim.Grid.GetOccupant(new GridPos(9, 10));
            var route = sim.GetLiveRoute(Entry);
            Assert.IsTrue(route.Found);
            Assert.IsTrue(route.IsBreach);
            Assert.AreEqual(new GridPos(9, 10), route.Blocker);
            Assert.AreEqual(new GridPos(8, 10), route.Cells[route.ReachableEndIndex]);
            Assert.AreEqual(8 + RoutePlanner.BreachCost, route.Cost);
            AssertCardinalPath(route);

            Assert.IsTrue(sim.StartNight());
            var grunt = sim.Enemies[0];
            StepUntil(sim, () => grunt.TargetBuildingId >= 0);
            Assert.AreEqual(new GridPos(8, 10), grunt.Cell);
            Assert.AreEqual(west.Id, grunt.TargetBuildingId);
            StepUntil(sim, () => west.Hp < 100);
            var attacks = Drain(sim).Where(e => e.Type == SimEventType.EnemyAttacked).ToList();
            Assert.IsTrue(attacks.All(a => a.TargetId == west.Id && a.Cell == new GridPos(9, 10)));
        }

        // Fixtures 4 (wall half) and 12
        [Test]
        public void DestroyedBlockerReleasesOnceAndEnemyTakesOpenedRoute()
        {
            var sim = NewRun(SingleGrunt());
            EnclosureWalls(sim);
            var west = sim.Grid.GetOccupant(new GridPos(9, 10));
            Assert.IsTrue(sim.StartNight());
            var grunt = sim.Enemies[0];
            StepUntil(sim, () => grunt.TargetBuildingId == west.Id);
            var acquiredTick = sim.Tick - 1;
            var revisionBefore = sim.Grid.Revision;

            var allEvents = new List<SimEvent>();
            StepUntil(sim, () => west.Removed);
            sim.DrainEvents(allEvents);
            var destroyTick = sim.Tick - 1;
            var hits = allEvents.Where(e => e.Type == SimEventType.EnemyAttacked && e.TargetId == west.Id).ToList();
            Assert.AreEqual(10, hits.Count, "ten Grunt hits destroy a full-health Wall");
            Assert.AreEqual(acquiredTick + 20, hits[0].Tick, "first hit waits a full interval");
            for (var i = 1; i < hits.Count; i++) Assert.AreEqual(20, hits[i].Tick - hits[i - 1].Tick);
            Assert.AreEqual(destroyTick, hits[9].Tick);
            Assert.AreEqual(revisionBefore + 1, sim.Grid.Revision, "occupancy released exactly once");
            Assert.IsNull(sim.Grid.GetOccupant(new GridPos(9, 10)));
            Assert.AreEqual(0, west.Hp);

            sim.Step(); // next step: dead target rejected, open route taken into (9,10)
            Assert.AreEqual(-1, grunt.TargetBuildingId);
            Assert.IsTrue(grunt.Moving);
            Assert.AreEqual(new GridPos(9, 10), grunt.NextCell);
            Assert.IsFalse(grunt.Route.IsBreach);

            StepUntil(sim, () => grunt.TargetBuildingId == sim.Keep.Id);
            Assert.AreEqual(new GridPos(9, 10), grunt.Cell);
            StepUntil(sim, () => sim.Keep.Hp < 500);
            var later = Drain(sim);
            Assert.AreEqual(0, later.Count(e => e.Type == SimEventType.BuildingDestroyed));
            Assert.AreEqual(1, allEvents.Count(e => e.Type == SimEventType.BuildingDestroyed && e.SourceId == west.Id));
            Assert.IsFalse(later.Any(e => e.Type == SimEventType.EnemyAttacked && e.TargetId == west.Id), "no attack on a dead target");
        }

        // Fixture 13
        [Test]
        public void PreviewParityWithoutMutation()
        {
            var sim = NewRun();
            PlaceWalls(sim, (9, 10), (11, 10), (10, 9));
            var gold = sim.Gold;
            var revision = sim.Grid.Revision;
            var committed = sim.GetLiveRoute(Entry);
            Assert.IsFalse(committed.IsBreach);
            Assert.AreEqual(new GridPos(10, 11), committed.Goal);

            var ghost = new GridPos(10, 11);
            Assert.AreEqual(PlacementRejection.None, sim.CheckPlacement(BuildingKind.Wall, ghost), "sealing the Keep is legal");
            var preview = sim.PreviewRoute(Entry, ghost);
            Assert.IsTrue(preview.IsBreach);
            Assert.AreEqual(new GridPos(9, 10), preview.Blocker);
            Assert.AreEqual(gold, sim.Gold);
            Assert.AreEqual(revision, sim.Grid.Revision);
            Assert.IsNull(sim.Grid.GetOccupant(ghost));
            Assert.AreSame(committed, sim.Planner.PeekCachedRoute(sim.Grid, Entry), "preview must not overwrite the live cache");

            Place(sim, BuildingKind.Wall, 10, 11);
            var actual = sim.GetLiveRoute(Entry);
            Assert.IsTrue(actual.IsBreach);
            Assert.AreEqual(preview.Blocker, actual.Blocker);
            Assert.AreEqual(Cells(preview), Cells(actual));
        }

        // Fixture 14
        [Test]
        public void IndestructibleBarrierIsValidationErrorNotTarget()
        {
            var barrier = Enumerable.Range(0, GridCoordinates.Size).Select(z => new GridPos(5, z)).ToList();
            var sim = NewRun(terrain: barrier);
            var route = sim.Planner.Plan(sim.Grid, Entry, null);
            Assert.IsFalse(route.Found);
            var errors = sim.ValidateMap();
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("(0,10)", errors[0]);
            StringAssert.Contains("(10,10)", errors[0]);
            Assert.AreEqual(PlacementRejection.ImpassableTerrain, sim.CheckPlacement(BuildingKind.Wall, new GridPos(5, 5)));
            Assert.IsFalse(sim.StartNight());
            Assert.AreEqual(RunState.Day, sim.State);
            Assert.AreEqual(0, sim.AliveEnemyCount);
        }

        // Fixture 15
        [Test]
        public void MovingEnemyFinishesSegmentThenReplans()
        {
            var sim = NewRun(SingleGrunt());
            EnclosureWalls(sim);
            var north = sim.Grid.GetOccupant(new GridPos(10, 11));
            Assert.IsTrue(sim.StartNight());
            var grunt = sim.Enemies[0];
            StepUntil(sim, () => grunt.Moving && grunt.Cell == new GridPos(4, 10) && grunt.MoveTicks == 10);
            var segmentTarget = grunt.NextCell;

            Assert.IsTrue(sim.ScriptedRemove(north, out _));
            var newRevision = sim.Grid.Revision;
            double px = grunt.X, pz = grunt.Z;
            var stepsToReplan = -1;
            for (var i = 1; i <= 40; i++)
            {
                sim.Step();
                var d = Math.Abs(grunt.X - px) + Math.Abs(grunt.Z - pz);
                Assert.LessOrEqual(d, 0.05 + 1e-9, "no teleport");
                px = grunt.X;
                pz = grunt.Z;
                if (i <= 10) Assert.IsTrue(grunt.Cell == new GridPos(4, 10) || grunt.Cell == segmentTarget, "completes current segment");
                if (stepsToReplan < 0 && grunt.Route.Revision == newRevision) stepsToReplan = i;
            }

            Assert.Greater(stepsToReplan, 0);
            Assert.LessOrEqual(stepsToReplan, 20, "replans within one tile's travel time");
            Assert.IsFalse(grunt.Route.IsBreach);
            Assert.AreEqual(new GridPos(10, 11), grunt.Route.Goal);
            StepUntil(sim, () => grunt.TargetBuildingId == sim.Keep.Id);
            Assert.AreEqual(new GridPos(10, 11), grunt.Cell);
        }

        [Test]
        public void TieBreakIsStableAcrossPlanners()
        {
            var sim = NewRun();
            PlaceWalls(sim, (3, 10), (8, 9), (8, 11), (12, 14), (14, 6));
            var a = new RoutePlanner().Plan(sim.Grid, Entry, null);
            for (var i = 0; i < 10; i++) Assert.AreEqual(Cells(a), Cells(new RoutePlanner().Plan(sim.Grid, Entry, null)));
            foreach (var entry in WavePlan.StressEntries)
            {
                var r1 = sim.Planner.Plan(sim.Grid, entry, null);
                var r2 = new RoutePlanner().Plan(sim.Grid, entry, null);
                Assert.AreEqual(Cells(r1), Cells(r2));
                AssertCardinalPath(r1);
            }
        }

        [Test]
        public void RevisionInvalidatesCache()
        {
            var sim = NewRun();
            var r1 = sim.GetLiveRoute(Entry);
            Place(sim, BuildingKind.Wall, 5, 10);
            var r2 = sim.GetLiveRoute(Entry);
            Assert.AreNotSame(r1, r2);
            Assert.AreEqual(sim.Grid.Revision, r2.Revision);
            CollectionAssert.DoesNotContain(r2.Cells, new GridPos(5, 10));
        }

        [Test]
        public void StressLayoutWestRouteBreachesAWall()
        {
            var sim = new RunSimulation(RunConfig.CreateStress());
            CollectionAssert.IsEmpty(sim.ApplyLayout(LayoutFixture.CreateStressLayout(), bypassCost: true));
            foreach (var entry in WavePlan.StressEntries)
            {
                var r = sim.GetLiveRoute(entry);
                Assert.IsTrue(r.Found, $"route from {entry}");
                Assert.IsTrue(r.IsBreach, "Keep square is sealed");
                AssertCardinalPath(r);
            }

            var west = sim.GetLiveRoute(WavePlan.StressEntries[0]);
            Assert.AreEqual(BuildingKind.Wall, sim.Grid.GetOccupant(west.Blocker).Kind);
            TestContext.WriteLine($"West stress route: {Cells(west)}; blocker {west.Blocker}");
        }
    }
}
