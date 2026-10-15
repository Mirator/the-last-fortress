using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static TheLastFortress.Tests.SimTestUtil;

namespace TheLastFortress.Tests
{
    public class CombatTests
    {
        /// <summary>Enclosure plus a Tower whose range touches the attack cell (8,10) at exactly four tiles.</summary>
        static RunSimulation BoundaryScenario(out Building nearTower, out Building farTower, WavePlan wave = null)
        {
            var sim = NewRun(wave ?? SingleGrunt());
            EnclosureWalls(sim);
            nearTower = Place(sim, BuildingKind.Tower, 8, 6); // (8.5,6.5) -> (8.5,10.5) = 4.0
            farTower = Place(sim, BuildingKind.Tower, 8, 5); // 5.0 at the closest approach
            Place(sim, BuildingKind.Wall, 8, 8); // between tower and target; must not block arrows
            return sim;
        }

        // Fixtures 4 (tower half) and 5
        [Test]
        public void ThreeTowerHitsKillGruntAtExactRangeBoundary()
        {
            var sim = BoundaryScenario(out var near, out var far);
            Assert.IsTrue(sim.StartNight());
            var grunt = sim.Enemies[0];
            StepUntil(sim, () => near.TargetEnemyId == grunt.Id);
            var acquiredTick = sim.Tick - 1;
            Assert.AreEqual(new GridPos(8, 10), grunt.Cell, "acquired only once exactly four tiles away");
            Assert.IsFalse(grunt.Moving);

            var events = new List<SimEvent>();
            StepUntil(sim, () => !grunt.Alive || sim.State != RunState.Night);
            sim.DrainEvents(events);
            var shots = events.Where(e => e.Type == SimEventType.TowerShot).ToList();
            Assert.AreEqual(3, shots.Count);
            Assert.IsTrue(shots.All(s => s.SourceId == near.Id), "the tower five tiles away never fires");
            Assert.AreEqual(new long[] { acquiredTick + 15, acquiredTick + 30, acquiredTick + 45 }, shots.Select(s => s.Tick).ToArray());
            var kills = events.Where(e => e.Type == SimEventType.EnemyKilled).ToList();
            Assert.AreEqual(1, kills.Count, "a death resolves once");
            Assert.AreEqual(shots[2].Tick, kills[0].Tick);
            Assert.AreEqual(-1, far.TargetEnemyId);
            Assert.AreEqual(RunState.Victory, sim.State);
        }

        [Test]
        public void RangePredicateIsInclusiveEuclidean()
        {
            var sim = NewRun();
            var t = Place(sim, BuildingKind.Tower, 5, 5);
            Assert.IsTrue(RunSimulation.InRange(t, 9.5, 5.5));
            Assert.IsTrue(RunSimulation.InRange(t, 5.5, 1.5));
            Assert.IsFalse(RunSimulation.InRange(t, 9.51, 5.5));
            Assert.IsFalse(RunSimulation.InRange(t, 8.5, 8.5), "diagonal 3,3 = 4.24 > 4");
        }

        // Fixture 6
        [Test]
        public void PauseFreezesEverythingAndResumeContinues()
        {
            RunSimulation Build()
            {
                var s = BoundaryScenario(out _, out _, WavePlan.CreateMainWave());
                Assert.IsTrue(s.StartNight());
                return s;
            }

            var paused = Build();
            var reference = Build();
            for (var i = 0; i < 167; i++) // mid spawn interval (next at 180) and mid tower/Grunt cooldowns
            {
                paused.Step();
                reference.Step();
            }

            paused.Advance(0.02); // a partial step also stays pending across the pause
            Assert.IsTrue(paused.SetPaused(true));
            var frozen = paused.CaptureSnapshot();
            Assert.AreEqual(0, paused.Advance(10.0));
            paused.Step();
            Assert.AreEqual(frozen, paused.CaptureSnapshot());
            Assert.IsTrue(paused.SetPaused(false));
            paused.Advance(0.03); // completes the step that was 0.02 in
            reference.Step();
            Assert.AreEqual(reference.CaptureSnapshot(), paused.CaptureSnapshot());
            for (var i = 0; i < 100; i++)
            {
                paused.Step();
                reference.Step();
            }

            Assert.AreEqual(reference.CaptureSnapshot(), paused.CaptureSnapshot());
        }

        [Test]
        public void PauseOnlyAllowedDuringNight()
        {
            var sim = NewRun();
            Assert.IsFalse(sim.SetPaused(true));
            Assert.IsFalse(sim.Paused);
        }

        static RunSimulation BusyMainWave()
        {
            var sim = NewRun();
            Place(sim, BuildingKind.Tower, 4, 8);
            Place(sim, BuildingKind.Tower, 7, 12);
            Place(sim, BuildingKind.Tower, 9, 8);
            PlaceWalls(sim, (5, 10), (6, 10));
            Assert.IsTrue(sim.StartNight());
            return sim;
        }

        [Test]
        public void EqualElapsedTimeInDifferentChunksGivesIdenticalState()
        {
            var a = BusyMainWave();
            var b = BusyMainWave();
            var c = BusyMainWave();
            for (var i = 0; i < 300; i++) a.Advance(0.05);
            for (var i = 0; i < 100; i++)
            {
                b.Advance(0.016);
                b.Advance(0.017);
                b.Advance(0.017);
                b.Advance(0.1);
            }

            for (var i = 0; i < 15; i++)
            {
                c.Advance(0.3);
                c.Advance(0.7);
            }

            Assert.AreEqual(a.CaptureSnapshot(), b.CaptureSnapshot());
            Assert.AreEqual(a.CaptureSnapshot(), c.CaptureSnapshot());
        }

        [Test]
        public void TargetChangeResetsCooldownAndDeadEntitiesNeverAct()
        {
            var sim = BusyMainWave();
            var events = new List<SimEvent>();
            for (var i = 0; i < 20000 && sim.State == RunState.Night; i++)
            {
                sim.Step();
                sim.DrainEvents(events);
            }

            Assert.AreNotEqual(RunState.Night, sim.State);
            var killedAt = new Dictionary<int, long>();
            var destroyedAt = new Dictionary<int, long>();
            foreach (var e in events)
            {
                if (e.Type == SimEventType.EnemyKilled) killedAt[e.SourceId] = e.Tick;
                if (e.Type == SimEventType.BuildingDestroyed) destroyedAt[e.SourceId] = e.Tick;
                if (e.Type == SimEventType.TowerShot)
                    Assert.IsFalse(killedAt.ContainsKey(e.TargetId), "shot at a dead enemy");
                if (e.Type == SimEventType.EnemyAttacked)
                {
                    Assert.IsFalse(killedAt.ContainsKey(e.SourceId), "dead enemy attacked");
                    Assert.IsFalse(destroyedAt.ContainsKey(e.TargetId), "attack on a destroyed structure");
                }
            }

            foreach (var tower in events.Where(e => e.Type == SimEventType.TowerShot).GroupBy(e => e.SourceId))
            {
                var shots = tower.ToList();
                for (var i = 1; i < shots.Count; i++)
                {
                    var gap = shots[i].Tick - shots[i - 1].Tick;
                    if (shots[i].TargetId == shots[i - 1].TargetId) Assert.AreEqual(15, gap, "held target fires every interval");
                    else Assert.GreaterOrEqual(gap, 15, "new target waits a full interval; no stored attacks");
                }
            }

            Assert.Greater(killedAt.Count, 0);
        }
    }

    public class PhaseTests
    {
        // Fixture 7
        [Test]
        public void ZeroAliveBetweenSpawnsDoesNotWinAndVictoryHappensOnce()
        {
            var sim = NewRun();
            Place(sim, BuildingKind.Tower, 1, 9);
            Place(sim, BuildingKind.Tower, 1, 11);
            Place(sim, BuildingKind.Tower, 2, 9);
            Assert.IsTrue(sim.StartNight());
            Drain(sim);
            sim.Step();
            var sawEmptyNight = false;
            var events = new List<SimEvent>();
            for (var i = 0; i < 2000 && sim.State == RunState.Night; i++)
            {
                if (sim.AliveEnemyCount == 0 && sim.ScheduledRemaining > 0)
                {
                    sawEmptyNight = true;
                    Assert.AreEqual(RunState.Night, sim.State);
                }

                sim.Step();
                sim.DrainEvents(events);
            }

            Assert.IsTrue(sawEmptyNight, "fixture must actually reach zero living enemies between spawns");
            Assert.AreEqual(RunState.Victory, sim.State);
            Assert.AreEqual(9, events.Count(e => e.Type == SimEventType.EnemySpawned), "nine more spawns after the time-zero one");
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.StateChanged && e.SourceId == (int)RunState.Victory));
            var lastKill = events.Last(e => e.Type == SimEventType.EnemyKilled).Tick;
            Assert.GreaterOrEqual(lastKill, 270, "final spawn at 13.5 s");
            var tick = sim.Tick;
            sim.Step();
            sim.Advance(5);
            Assert.AreEqual(tick, sim.Tick, "terminal state stops updates");
            Assert.AreEqual(RunState.Victory, sim.State);
        }

        // Fixture 8 (cancellation) and Keep-only loss
        [Test]
        public void KeepOnlyLosesAndDefeatCancelsPendingSpawns()
        {
            var defs = PrototypeDefinitions.CreateDefault();
            defs.Keep.MaxHp = 20;
            var sim = NewRun(defs: defs);
            Assert.IsTrue(sim.StartNight());
            var events = new List<SimEvent>();
            for (var i = 0; i < 5000 && sim.State == RunState.Night; i++)
            {
                sim.Step();
                sim.DrainEvents(events);
            }

            Assert.AreEqual(RunState.Defeat, sim.State);
            var spawned = 1 + events.Count(e => e.Type == SimEventType.EnemySpawned);
            Assert.Less(spawned, 10, "Keep fell before the final spawn");
            Assert.AreEqual(0, sim.ScheduledRemaining, "pending spawns cancelled");
            sim.Advance(30);
            Assert.AreEqual(0, Drain(sim).Count(e => e.Type == SimEventType.EnemySpawned));
        }

        [Test]
        public void DefaultKeepOnlyLayoutLoses()
        {
            var sim = NewRun();
            Assert.IsTrue(sim.StartNight());
            RunToEnd(sim);
            Assert.AreEqual(RunState.Defeat, sim.State);
        }

        // Fixture 8 (same-step priority)
        [Test]
        public void SameStepFinalKillCannotOverrideDefeat()
        {
            PrototypeDefinitions Defs()
            {
                var d = PrototypeDefinitions.CreateDefault();
                d.Keep.MaxHp = 10;
                d.Tower.AttackDamage = 30;
                return d;
            }

            var a = new SpawnEntry(0, WavePlan.MainEntry, 0);
            var east = new GridPos(19, 10);

            // Probe: when does the west Grunt destroy the Keep?
            var probe = NewRun(new WavePlan("probe", new[] { a }, new[] { WavePlan.MainEntry }), Defs());
            Place(probe, BuildingKind.Tower, 17, 8);
            Assert.IsTrue(probe.StartNight());
            RunToEnd(probe);
            Assert.AreEqual(RunState.Defeat, probe.State);
            var keepFallTick = probe.Tick - 1;

            // The final scheduled Grunt spawns in the east tower's range so its kill lands on that same step.
            var b = new SpawnEntry(keepFallTick - 15, east, 1);
            var sim = NewRun(new WavePlan("same-step", new[] { a, b }, new[] { WavePlan.MainEntry, east }), Defs());
            Place(sim, BuildingKind.Tower, 17, 8);
            Assert.IsTrue(sim.StartNight());
            var events = new List<SimEvent>();
            for (var i = 0; i < 5000 && sim.State == RunState.Night; i++)
            {
                sim.Step();
                sim.DrainEvents(events);
            }

            var kill = events.Single(e => e.Type == SimEventType.EnemyKilled);
            Assert.AreEqual(keepFallTick, kill.Tick, "final scheduled enemy dies on the Keep-destroying step");
            Assert.AreEqual(0, sim.ScheduledRemaining);
            Assert.AreEqual(RunState.Defeat, sim.State);
            Assert.AreEqual(keepFallTick, events.Single(e => e.Type == SimEventType.StateChanged && e.SourceId == (int)RunState.Defeat).Tick);
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.StateChanged && e.SourceId == (int)RunState.Victory));
        }

        [Test]
        public void StartNightCannotEnqueueTwice()
        {
            var sim = NewRun();
            Assert.IsTrue(sim.StartNight());
            Assert.IsFalse(sim.StartNight());
            Assert.AreEqual(1, sim.AliveEnemyCount);
            Assert.AreEqual(9, sim.ScheduledRemaining);
        }

        // Fixture 9
        [Test]
        public void RestartRestoresExactInitialStateAfterEveryOutcome()
        {
            var fresh = NewRun().CaptureSnapshot();

            var lost = NewRun();
            Assert.IsTrue(lost.StartNight());
            RunToEnd(lost);
            Assert.AreEqual(RunState.Defeat, lost.State);
            lost.Restart();
            Assert.AreEqual(fresh, lost.CaptureSnapshot());

            var won = NewRun();
            Place(won, BuildingKind.Tower, 1, 9);
            Place(won, BuildingKind.Tower, 1, 11);
            Place(won, BuildingKind.Tower, 2, 9);
            Assert.IsTrue(won.StartNight());
            RunToEnd(won);
            Assert.AreEqual(RunState.Victory, won.State);
            won.Restart();
            Assert.AreEqual(fresh, won.CaptureSnapshot());

            var paused = NewRun();
            Place(paused, BuildingKind.Wall, 4, 4);
            Assert.IsTrue(paused.StartNight());
            paused.Advance(3.33);
            paused.SetPaused(true);
            paused.Restart();
            Assert.AreEqual(fresh, paused.CaptureSnapshot());
            Assert.IsFalse(paused.Paused);
            var events = Drain(paused);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(SimEventType.RunReset, events[0].Type);
            Assert.IsNull(paused.Planner.PeekCachedRoute(paused.Grid, WavePlan.MainEntry));

            // The restarted run plays out identically to a fresh one.
            var again = NewRun();
            Assert.IsTrue(again.StartNight());
            Assert.IsTrue(paused.StartNight());
            RunToEnd(again);
            RunToEnd(paused);
            Assert.AreEqual(again.CaptureSnapshot(), paused.CaptureSnapshot());
        }
    }

    public class StressFixtureTests
    {
        [Test]
        public void StressLayoutMatchesSpecification()
        {
            var layout = LayoutFixture.CreateStressLayout();
            Assert.AreEqual(30, layout.Placements.Count(p => p.Kind == BuildingKind.Wall));
            Assert.AreEqual(10, layout.Placements.Count(p => p.Kind == BuildingKind.Tower));
            Assert.AreEqual(40, layout.Placements.Select(p => p.Cell).Distinct().Count(), "unique occupancy");
            foreach (var p in layout.Placements)
            {
                Assert.IsTrue(GridCoordinates.IsInBounds(p.Cell));
                Assert.IsFalse(GridCoordinates.IsReservedRing(p.Cell));
                Assert.AreNotEqual(GridState.KeepCell, p.Cell);
            }
        }

        [Test]
        public void StressKeepsOneHundredActiveAndWallsInvulnerable()
        {
            var sim = new RunSimulation(RunConfig.CreateStress());
            CollectionAssert.IsEmpty(sim.ApplyLayout(LayoutFixture.CreateStressLayout(), bypassCost: true));
            Assert.AreEqual(250, sim.Gold, "fixture setup bypasses costs");
            Assert.IsTrue(sim.StartNight());
            var events = new List<SimEvent>();
            for (var i = 0; i < 600; i++)
            {
                sim.Step();
                sim.DrainEvents(events);
                if (sim.Tick > 50) Assert.AreEqual(100, sim.AliveEnemyCount);
            }

            Assert.AreEqual(RunState.Night, sim.State);
            Assert.Greater(events.Count(e => e.Type == SimEventType.EnemyKilled), 0, "towers kill and Grunts respawn");
            Assert.Greater(events.Count(e => e.Type == SimEventType.EnemyAttacked), 0);
            Assert.AreEqual(0, events.Count(e => e.Type == SimEventType.BuildingDestroyed && sim.GetBuilding(e.SourceId)?.Kind == BuildingKind.Wall));
            Assert.AreEqual(500, sim.Keep.Hp);
            foreach (var b in sim.Grid.AllBuildings().Where(b => b.Kind == BuildingKind.Wall)) Assert.AreEqual(100, b.Hp);
            for (var e = 0; e < 4; e++) Assert.AreEqual(25, sim.Enemies.Count(x => x.EntryIndex == e));
        }

        [Test]
        public void ScriptedRemovalOfWestBlockerWhileAttacked()
        {
            var sim = new RunSimulation(RunConfig.CreateStress());
            sim.ApplyLayout(LayoutFixture.CreateStressLayout(), bypassCost: true);
            var target = sim.Grid.GetOccupant(sim.GetLiveRoute(WavePlan.StressEntries[0]).Blocker);
            Assert.AreEqual(BuildingKind.Wall, target.Kind);
            Assert.IsTrue(sim.StartNight());
            for (var i = 0; i < 800; i++) sim.Step();
            Assert.Greater(sim.CountAttackers(target), 0, "west blocker under active attack before the event");
            var revision = sim.Grid.Revision;
            Assert.IsTrue(sim.ScriptedRemove(target, out var attacked));
            Assert.IsTrue(attacked);
            Assert.AreEqual(revision + 1, sim.Grid.Revision);
            sim.Step();
            Assert.AreEqual(0, sim.CountAttackers(target));
        }

        [Test]
        public void StressFlagsDoNotLeakIntoNormalRuns()
        {
            var stress = new RunSimulation(RunConfig.CreateStress());
            stress.ApplyLayout(LayoutFixture.CreateStressLayout(), bypassCost: true);
            stress.StartNight();
            for (var i = 0; i < 100; i++) stress.Step();

            var normal = NewRun(SingleGrunt());
            Assert.IsFalse(normal.StressMode);
            EnclosureWalls(normal);
            var west = normal.Grid.GetOccupant(new GridPos(9, 10));
            Assert.IsTrue(normal.StartNight());
            StepUntil(normal, () => west.Hp < 100);
            Assert.AreEqual(90, west.Hp, "normal-mode Walls take damage");
            Assert.AreEqual(500, PrototypeDefinitions.CreateDefault().Keep.MaxHp);
            Assert.AreEqual(10, WavePlan.CreateMainWave().Spawns.Count);
        }
    }
}

