using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TheLastFortress.Tests
{
    static class SimTestUtil
    {
        public static RunSimulation NewRun(WavePlan wave = null, PrototypeDefinitions defs = null, IReadOnlyList<GridPos> terrain = null)
        {
            var config = new RunConfig
            {
                Definitions = defs ?? PrototypeDefinitions.CreateDefault(),
                Wave = wave ?? WavePlan.CreateMainWave(),
                ImpassableCells = terrain,
            };
            return new RunSimulation(config);
        }

        public static WavePlan SingleGrunt(GridPos? cell = null) =>
            new WavePlan("single", new[] { new SpawnEntry(0, cell ?? WavePlan.MainEntry, 0) }, new[] { cell ?? WavePlan.MainEntry });

        public static Building Place(RunSimulation sim, BuildingKind kind, int x, int z)
        {
            var r = sim.TryPlace(kind, new GridPos(x, z), out var b);
            Assert.AreEqual(PlacementRejection.None, r, $"placing {kind} at ({x},{z})");
            return b;
        }

        public static void PlaceWalls(RunSimulation sim, params (int x, int z)[] cells)
        {
            foreach (var c in cells) Place(sim, BuildingKind.Wall, c.x, c.z);
        }

        public static void EnclosureWalls(RunSimulation sim) =>
            PlaceWalls(sim, (9, 10), (11, 10), (10, 9), (10, 11));

        /// <summary>Steps until predicate holds or the run ends. Fails after maxSteps.</summary>
        public static int StepUntil(RunSimulation sim, Func<bool> predicate, int maxSteps = 4000)
        {
            for (var i = 0; i < maxSteps; i++)
            {
                if (predicate()) return i;
                if (sim.State != RunState.Night) break;
                sim.Step();
            }

            if (predicate()) return maxSteps;
            Assert.Fail($"Condition not reached within {maxSteps} steps; state {sim.State} tick {sim.Tick}");
            return -1;
        }

        public static void RunToEnd(RunSimulation sim, int maxSteps = 20000)
        {
            for (var i = 0; i < maxSteps && sim.State == RunState.Night; i++) sim.Step();
            Assert.AreNotEqual(RunState.Night, sim.State, "run did not finish");
        }

        public static List<SimEvent> Drain(RunSimulation sim)
        {
            var list = new List<SimEvent>();
            sim.DrainEvents(list);
            return list;
        }
    }
}
