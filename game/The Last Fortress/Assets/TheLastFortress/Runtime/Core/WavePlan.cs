using System.Collections.Generic;

namespace TheLastFortress
{
    public readonly struct SpawnEntry
    {
        public readonly long Tick;
        public readonly GridPos Cell;
        public readonly int EntryIndex;
        public readonly EnemyKind Kind;

        public SpawnEntry(long tick, GridPos cell, int entryIndex, EnemyKind kind = EnemyKind.Grunt)
        {
            Tick = tick;
            Cell = cell;
            EntryIndex = entryIndex;
            Kind = kind;
        }
    }

    /// <summary>Ordered spawn schedule in simulation ticks (Night elapsed time / 0.05 s).</summary>
    public sealed class WavePlan
    {
        public static readonly GridPos MainEntry = new GridPos(0, 10);

        public static readonly GridPos[] StressEntries =
        {
            new GridPos(0, 10), new GridPos(19, 10), new GridPos(10, 0), new GridPos(10, 19),
        };

        public readonly string Name;
        public readonly IReadOnlyList<SpawnEntry> Spawns;
        public readonly IReadOnlyList<GridPos> EntryCells;

        /// <summary>When positive, killed enemies respawn at their entry to keep this many active (stress only).</summary>
        public readonly int MaintainActivePerEntry;

        public WavePlan(string name, IReadOnlyList<SpawnEntry> spawns, IReadOnlyList<GridPos> entryCells, int maintainActivePerEntry = 0)
        {
            Name = name;
            var sorted = new List<SpawnEntry>(spawns);
            // Stable sort by tick preserves authored order for equal ticks.
            for (var i = 1; i < sorted.Count; i++)
            {
                var item = sorted[i];
                var j = i - 1;
                while (j >= 0 && sorted[j].Tick > item.Tick)
                {
                    sorted[j + 1] = sorted[j];
                    j--;
                }

                sorted[j + 1] = item;
            }

            Spawns = sorted.AsReadOnly();
            EntryCells = entryCells;
            MaintainActivePerEntry = maintainActivePerEntry;
        }

        /// <summary>Stress fixture: archetype for maintained slot <paramref name="slot"/> of an entry (Grunts unless a mix is set).</summary>
        public EnemyKind RespawnKind(int entry, int slot)
        {
            if (MaintainMix == null) return EnemyKind.Grunt;
            var i = 0;
            foreach (var (kind, count) in MaintainMix)
            {
                if (slot < i + count) return kind;
                i += count;
            }

            return EnemyKind.Grunt;
        }

        /// <summary>Optional per-entry maintained composition for the MVP stress fixture, in slot order.</summary>
        public (EnemyKind kind, int count)[] MaintainMix;

        /// <summary>
        /// MVP stress fixture: 100 maintained enemies, per entry 15 Grunts, 5 Runners, 4 Brutes and 1 Siege Golem (four Golems in
        /// total is debug-only), released 0.1 s apart per entry; each death respawns the same archetype at the same entry.
        /// </summary>
        public static WavePlan CreateMvpStressWave()
        {
            const int perEntry = 25;
            var mix = new[] { (EnemyKind.Grunt, 15), (EnemyKind.Runner, 5), (EnemyKind.Brute, 4), (EnemyKind.SiegeGolem, 1) };
            var spawns = new List<SpawnEntry>();
            var gap = PrototypeDefinitions.ToTicks(0.1);
            var probe = new WavePlan("probe", new SpawnEntry[0], StressEntries) { MaintainMix = mix };
            for (var i = 0; i < perEntry; i++)
            for (var e = 0; e < StressEntries.Length; e++)
                spawns.Add(new SpawnEntry(i * gap, StressEntries[e], e, probe.RespawnKind(e, i)));
            return new WavePlan("MVP STRESS FIXTURE", spawns, StressEntries, perEntry) { MaintainMix = mix };
        }

        /// <summary>Ten Grunts from (0,10) at 0, 1.5, ..., 13.5 seconds.</summary>
        public static WavePlan CreateMainWave()
        {
            var spawns = new List<SpawnEntry>();
            var interval = PrototypeDefinitions.ToTicks(1.5);
            for (var i = 0; i < 10; i++) spawns.Add(new SpawnEntry(i * interval, MainEntry, 0));
            return new WavePlan("Main wave", spawns, new[] { MainEntry });
        }

        /// <summary>100 Grunts, 25 per entry, released 0.1 s apart per entry, respawned to stay at 100.</summary>
        public static WavePlan CreateStressWave()
        {
            const int perEntry = 25;
            var spawns = new List<SpawnEntry>();
            var gap = PrototypeDefinitions.ToTicks(0.1);
            for (var i = 0; i < perEntry; i++)
            for (var e = 0; e < StressEntries.Length; e++)
                spawns.Add(new SpawnEntry(i * gap, StressEntries[e], e));
            return new WavePlan("STRESS FIXTURE", spawns, StressEntries, perEntry);
        }
    }
}
