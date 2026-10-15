using NUnit.Framework;
using static TheLastFortress.Tests.SimTestUtil;

namespace TheLastFortress.Tests
{
    public class CoordinateTests
    {
        [Test]
        public void TileCentersRoundTripForEveryCell()
        {
            for (var x = 0; x < GridCoordinates.Size; x++)
            for (var z = 0; z < GridCoordinates.Size; z++)
            {
                var cell = new GridPos(x, z);
                GridCoordinates.TileCenter(cell, out var wx, out var wz);
                Assert.AreEqual(x + 0.5, wx);
                Assert.AreEqual(z + 0.5, wz);
                Assert.IsTrue(GridCoordinates.TryWorldToGrid(wx, wz, out var back));
                Assert.AreEqual(cell, back);
            }
        }

        [TestCase(-0.01, 5.0)]
        [TestCase(20.0, 5.0)]
        [TestCase(5.0, -0.01)]
        [TestCase(5.0, 20.0)]
        [TestCase(-3.7, -1.0)]
        public void OutsidePointsAreRejectedNotClamped(double x, double z)
        {
            Assert.IsFalse(GridCoordinates.TryWorldToGrid(x, z, out _));
        }

        [Test]
        public void BoundariesFloorIntoCells()
        {
            Assert.IsTrue(GridCoordinates.TryWorldToGrid(0, 0, out var a));
            Assert.AreEqual(new GridPos(0, 0), a);
            Assert.IsTrue(GridCoordinates.TryWorldToGrid(19.999, 19.999, out var b));
            Assert.AreEqual(new GridPos(19, 19), b);
            Assert.IsTrue(GridCoordinates.TryWorldToGrid(10.0, 9.9999, out var c));
            Assert.AreEqual(new GridPos(10, 9), c);
        }

        [Test]
        public void MainEntryAcceptsEnemiesButRejectsBuilding()
        {
            var sim = NewRun();
            Assert.AreEqual(PlacementRejection.ReservedRing, sim.CheckPlacement(BuildingKind.Wall, new GridPos(0, 10)));
            Assert.IsTrue(sim.StartNight());
            Assert.AreEqual(1, sim.AliveEnemyCount, "time-zero spawn released on entering Night");
            Assert.AreEqual(new GridPos(0, 10), sim.Enemies[0].Cell);
        }

        [Test]
        public void FreshRunInitialState()
        {
            var sim = NewRun();
            Assert.AreEqual(RunState.Day, sim.State);
            Assert.AreEqual(250, sim.Gold);
            Assert.AreEqual(GridState.KeepCell, sim.Keep.Cell);
            Assert.AreEqual(500, sim.Keep.Hp);
            Assert.AreEqual(0, sim.AliveEnemyCount);
            Assert.AreEqual(10, sim.ScheduledRemaining);
            var count = 0;
            foreach (var _ in sim.Grid.AllBuildings()) count++;
            Assert.AreEqual(1, count, "only the Keep");
        }

        [Test]
        public void DefaultDefinitionsValidateAndMatchSpec()
        {
            var d = PrototypeDefinitions.CreateDefault();
            CollectionAssert.IsEmpty(d.Validate());
            Assert.AreEqual(20, d.Wall.Cost);
            Assert.AreEqual(70, d.Tower.Cost);
            Assert.AreEqual(15, PrototypeDefinitions.ToTicks(d.Tower.AttackIntervalSeconds));
            Assert.AreEqual(20, PrototypeDefinitions.ToTicks(d.Grunt.AttackIntervalSeconds));
            Assert.AreEqual(20, d.GruntTicksPerTile);
        }

        [Test]
        public void NonPositiveOrOffStepIntervalsFailValidation()
        {
            var d = PrototypeDefinitions.CreateDefault();
            d.Tower.AttackIntervalSeconds = 0;
            Assert.IsNotEmpty(d.Validate());
            d.Tower.AttackIntervalSeconds = 0.77;
            Assert.IsNotEmpty(d.Validate());
            var sim = NewRun(defs: d);
            Assert.IsFalse(sim.StartNight(), "invalid definitions must not start a run");
        }
    }

    public class PlacementTests
    {
        [Test]
        public void TowerPurchaseIsAtomic()
        {
            var sim = NewRun();
            var rev = sim.Grid.Revision;
            var tower = Place(sim, BuildingKind.Tower, 5, 5);
            Assert.AreEqual(180, sim.Gold);
            Assert.AreEqual(rev + 1, sim.Grid.Revision);

            // occupied
            Assert.AreEqual(PlacementRejection.Occupied, sim.TryPlace(BuildingKind.Wall, new GridPos(5, 5), out var none));
            Assert.IsNull(none);
            Assert.AreEqual(180, sim.Gold);
            Assert.AreSame(tower, sim.Grid.GetOccupant(new GridPos(5, 5)));
            Assert.AreEqual(rev + 1, sim.Grid.Revision);

            // keep cell and ring
            Assert.AreEqual(PlacementRejection.Occupied, sim.TryPlace(BuildingKind.Wall, GridState.KeepCell, out _));
            Assert.AreEqual(PlacementRejection.ReservedRing, sim.TryPlace(BuildingKind.Wall, new GridPos(19, 3), out _));
            Assert.AreEqual(PlacementRejection.OutOfBounds, sim.TryPlace(BuildingKind.Wall, new GridPos(-1, 3), out _));

            // unaffordable
            Place(sim, BuildingKind.Tower, 6, 5);
            Place(sim, BuildingKind.Tower, 7, 5); // 40 left
            var before = sim.Grid.Revision;
            Assert.AreEqual(PlacementRejection.InsufficientGold, sim.TryPlace(BuildingKind.Tower, new GridPos(8, 5), out _));
            Assert.AreEqual(40, sim.Gold);
            Assert.IsNull(sim.Grid.GetOccupant(new GridPos(8, 5)));
            Assert.AreEqual(before, sim.Grid.Revision);
        }

        [Test]
        public void SellRefundsHalfOnceAndKeepCannotBeSold()
        {
            var sim = NewRun();
            var tower = Place(sim, BuildingKind.Tower, 5, 5);
            Assert.AreEqual(SellRejection.None, sim.TrySell(tower));
            Assert.AreEqual(215, sim.Gold, "180 + floor(70 * 0.5)");
            Assert.IsNull(sim.Grid.GetOccupant(new GridPos(5, 5)));
            Assert.AreEqual(SellRejection.AlreadyRemoved, sim.TrySell(tower));
            Assert.AreEqual(215, sim.Gold);
            Assert.AreEqual(SellRejection.KeepCannotBeSold, sim.TrySell(sim.Keep));
            Assert.AreEqual(215, sim.Gold);
        }

        [Test]
        public void WallRefundFloorsHalf()
        {
            var sim = NewRun();
            var wall = Place(sim, BuildingKind.Wall, 4, 4);
            sim.TrySell(wall);
            Assert.AreEqual(240, sim.Gold);
        }

        [Test]
        public void NoConstructionOrSellingDuringNight()
        {
            var sim = NewRun();
            var wall = Place(sim, BuildingKind.Wall, 4, 4);
            Assert.IsTrue(sim.StartNight());
            Assert.AreEqual(PlacementRejection.NotDay, sim.TryPlace(BuildingKind.Wall, new GridPos(4, 5), out _));
            Assert.AreEqual(SellRejection.NotDay, sim.TrySell(wall));
            Assert.AreEqual(230, sim.Gold);
        }

        [Test]
        public void EnclosingTheKeepIsLegal()
        {
            var sim = NewRun();
            EnclosureWalls(sim);
            Assert.AreEqual(170, sim.Gold);
            CollectionAssert.IsEmpty(sim.ValidateMap());
        }
    }
}
