using System.Collections.Generic;

namespace TheLastFortress
{
    /// <summary>
    /// Plan 008 twelve-night campaign (008-rules.md section 3). Authored recipes with bounded seeded variation: per night
    /// one draw for the entry rotation and, on a boss night, one for the boss entry, from a wave stream separate from the
    /// map and reward streams. All twelve plans are built before Day 1 and stored; no director reacts to the fortress.
    /// </summary>
    public static class ReleaseCampaign
    {
        public const string RulesVersion = "release-rules-1";
        public const string CampaignId = "twelve-nights";
        public const int ContentVersion = 2; // 2: cheaper, weaker buildings; content-1 saves are refused, not rescaled
        public const string Label = "Twelve nights";
        public const uint WaveSalt = 0x5BD1E995;

        /// <summary>Per-night cap on scheduled spawns (the living cap of 100 still applies during play).</summary>
        public const int MaxSpawnsPerNight = 100;

        public sealed class Recipe
        {
            public int Grunts, Runners, Brutes, Bombers, Archers;
            public EnemyKind? Boss;
            public double BossSeconds = 30.0;
            public GridPos[] Entries;
            public double CadenceSeconds;

            public int OrdinaryCount => Grunts + Runners + Brutes + Bombers + Archers;
        }

        static readonly GridPos W = MvpCampaign.West, E = MvpCampaign.East, N = MvpCampaign.North, S = MvpCampaign.South;

        public static Recipe[] Table() => new[]
        {
            new Recipe { Grunts = 10, Entries = new[] { W }, CadenceSeconds = 1.5 },
            new Recipe { Grunts = 12, Runners = 4, Entries = new[] { W, E }, CadenceSeconds = 1.5 },
            new Recipe { Grunts = 14, Runners = 4, Brutes = 2, Entries = new[] { W, E }, CadenceSeconds = 1.25 },
            new Recipe { Grunts = 14, Runners = 6, Brutes = 2, Bombers = 3, Entries = new[] { W, E }, CadenceSeconds = 1.25 },
            new Recipe { Grunts = 16, Runners = 6, Brutes = 2, Bombers = 3, Archers = 4, Entries = new[] { W, E, N }, CadenceSeconds = 1.0 },
            new Recipe { Grunts = 18, Runners = 8, Brutes = 3, Bombers = 3, Archers = 4, Boss = EnemyKind.SiegeGolem, Entries = new[] { W, E, N }, CadenceSeconds = 1.0 },
            new Recipe { Grunts = 20, Runners = 8, Brutes = 4, Bombers = 4, Archers = 6, Entries = new[] { W, E, S }, CadenceSeconds = 1.0 },
            new Recipe { Grunts = 26, Runners = 10, Brutes = 5, Bombers = 5, Archers = 8, Entries = new[] { W, E, N, S }, CadenceSeconds = 0.9 },
            new Recipe { Grunts = 30, Runners = 12, Brutes = 6, Bombers = 6, Archers = 9, Boss = EnemyKind.SiegeGolem, Entries = new[] { W, E, N, S }, CadenceSeconds = 0.85 },
            new Recipe { Grunts = 34, Runners = 14, Brutes = 7, Bombers = 7, Archers = 10, Entries = new[] { W, E, N, S }, CadenceSeconds = 0.8 },
            new Recipe { Grunts = 36, Runners = 16, Brutes = 8, Bombers = 8, Archers = 12, Boss = EnemyKind.SiegeGolem, Entries = new[] { W, E, N, S }, CadenceSeconds = 0.75 },
            new Recipe { Grunts = 40, Runners = 18, Brutes = 10, Bombers = 10, Archers = 12, Boss = EnemyKind.SiegeCommander, Entries = new[] { W, E, N, S }, CadenceSeconds = 0.7 },
        };

        /// <summary>
        /// The twelve stored nights for a seed and map profile, with the release perk pool, on the board in the seed's own
        /// symmetry (<see cref="MapProfiles.SymmetryFor"/>, as <see cref="MapProfiles.Generate(string, uint)"/> builds it).
        /// </summary>
        public static CampaignConfig Create(uint seed, string mapProfile) => Create(seed, mapProfile, MapProfiles.SymmetryFor(seed));

        /// <summary>The recipes' entries are written for the profiles' own layout; they turn with the map's symmetry.</summary>
        public static CampaignConfig Create(uint seed, string mapProfile, int symmetry) => Create(seed, mapProfile, Table(), symmetry);

        public static CampaignConfig Create(uint seed, string mapProfile, IReadOnlyList<Recipe> table, int symmetry = 0)
        {
            var rng = new RunRandom(seed ^ WaveSalt);
            var nights = new List<WavePlan>();
            for (var i = 0; i < table.Count; i++) nights.Add(BuildNight($"Night {i + 1}", table[i], rng, symmetry));
            return new CampaignConfig
            {
                Label = Label, Nights = nights, OffersRewards = true,
                RulesVersion = RulesVersion, CampaignId = CampaignId, PerkPool = PerkRules.ReleaseIds,
                MapProfile = mapProfile ?? "", Difficulty = "standard",
            };
        }

        static readonly EnemyKind[] CycleOrder = { EnemyKind.Grunt, EnemyKind.Runner, EnemyKind.Brute, EnemyKind.Bomber, EnemyKind.Archer };

        /// <summary>
        /// Ordinary spawns cycle Grunt, Runner, Brute, Bomber, Archer (skipping exhausted kinds), first at zero then every
        /// cadence; entries round-robin from a seeded offset. A boss spawns at its time from a seeded entry, after equal-time
        /// ordinary spawns. Consumes one draw per night plus one per boss.
        /// </summary>
        public static WavePlan BuildNight(string name, Recipe r, RunRandom rng, int symmetry = 0)
        {
            var entries = new GridPos[r.Entries.Length];
            for (var i = 0; i < entries.Length; i++) entries[i] = MapProfiles.EntryFor(symmetry, r.Entries[i]);
            var count = entries.Length;
            var offset = (int)rng.NextBounded((uint)count);
            var cadence = PrototypeDefinitions.ToTicks(r.CadenceSeconds);
            var remaining = new[] { r.Grunts, r.Runners, r.Brutes, r.Bombers, r.Archers };
            var spawns = new List<SpawnEntry>();
            var index = 0;
            var left = r.OrdinaryCount;
            while (left > 0)
            {
                for (var k = 0; k < CycleOrder.Length; k++)
                {
                    if (remaining[k] == 0) continue;
                    remaining[k]--;
                    left--;
                    var entry = (index + offset) % count;
                    spawns.Add(new SpawnEntry(index * (long)cadence, entries[entry], entry, CycleOrder[k]));
                    index++;
                }
            }

            if (r.Boss.HasValue)
            {
                var entry = (int)rng.NextBounded((uint)count);
                spawns.Add(new SpawnEntry(PrototypeDefinitions.ToTicks(r.BossSeconds), entries[entry], entry, r.Boss.Value));
            }

            return new WavePlan(name, spawns, entries);
        }

        /// <summary>Structural problems with stored nights (empty when valid). Route checks need a map: see <see cref="RunSimulation.ValidateMap"/>.</summary>
        public static List<string> Validate(CampaignConfig campaign, PrototypeDefinitions defs)
        {
            var errors = new List<string>();
            if (campaign?.Nights == null || campaign.Nights.Count == 0)
            {
                errors.Add("Campaign has no nights.");
                return errors;
            }

            for (var n = 0; n < campaign.Nights.Count; n++)
            {
                var w = campaign.Nights[n];
                var label = $"Night {n + 1}";
                if (w.EntryCells == null || w.EntryCells.Count == 0) errors.Add($"{label}: no entries.");
                else
                    foreach (var e in w.EntryCells)
                        if (!GridCoordinates.IsReservedRing(e)) errors.Add($"{label}: entry {e} is not on the boundary ring.");
                if (w.Spawns.Count == 0) errors.Add($"{label}: no spawns.");
                if (w.Spawns.Count > MaxSpawnsPerNight) errors.Add($"{label}: {w.Spawns.Count} spawns exceed {MaxSpawnsPerNight}.");
                var bosses = 0;
                long last = 0;
                foreach (var s in w.Spawns)
                {
                    if (s.Tick < last) errors.Add($"{label}: spawn ticks decrease.");
                    last = s.Tick;
                    if (defs.EnemyFor(s.Kind) == null) errors.Add($"{label}: {s.Kind} is not defined.");
                    if (EnemyDefinition.IsBossKind(s.Kind)) bosses++;
                    if (w.EntryCells == null || s.EntryIndex < 0 || s.EntryIndex >= w.EntryCells.Count || w.EntryCells[s.EntryIndex] != s.Cell)
                        errors.Add($"{label}: spawn does not match its entry.");
                }

                if (bosses > 1) errors.Add($"{label}: more than one boss.");
            }

            return errors;
        }
    }
}
