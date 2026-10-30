using System.Collections.Generic;

namespace TheLastFortress
{
    /// <summary>
    /// Plan 008 twelve-night campaign (specs/002-game-rules.md section 9). Authored recipes with bounded seeded variation: per night
    /// one draw for the entry rotation and, on a boss night, one for the boss entry, from a wave stream separate from the
    /// map and reward streams. All twelve plans are built before Day 1 and stored; no director reacts to the fortress.
    /// </summary>
    public static class ReleaseCampaign
    {
        /// <summary>Twelve nights without the hero; kept only so saves from before the hero continue (specs/003 section 1).</summary>
        public const string RulesV1 = "release-rules-1";

        /// <summary>Twelve nights with the player hero (specs/003). Every new release run uses it.</summary>
        public const string RulesV2 = "release-rules-2";

        /// <summary>
        /// Twelve nights with the hero at real-scale speeds (plan 015): people walk and march instead of sprinting. Enemies move
        /// at about a third of release-rules-2 speed with 1.5x HP (2.5x overshot: slow enemies bunch at the walls);
        /// the hero runs at 0.6 tiles/s. Every new release run uses it.
        /// </summary>
        public const string RulesV3 = "release-rules-3";

        /// <summary>
        /// release-rules-3 enemies, economy, maps and waves with the third-person production-slice hero (specs/003): an
        /// accelerating motor with a 0.48 m body, and a wind-up/contact/recovery attack at physical reach.
        /// </summary>
        public const string RulesV4 = "release-rules-4";

        /// <summary>Rules version of new release runs (release-rules-4 since plan 019).</summary>
        public const string RulesVersion = RulesV4;

        public static bool IsReleaseRules(string rulesVersion) =>
            rulesVersion == RulesV1 || rulesVersion == RulesV2 || rulesVersion == RulesV3 || rulesVersion == RulesV4;

        /// <summary>Whether this rules version has the player hero (release-rules-2 and later).</summary>
        public static bool HasHero(string rulesVersion) => rulesVersion == RulesV2 || rulesVersion == RulesV3 || rulesVersion == RulesV4;

        /// <summary>Whether this rules version plays release-rules-3 enemies (walking speeds, their HP; release-rules-4 inherits them).</summary>
        public static bool WalkingEnemies(string rulesVersion) => rulesVersion == RulesV3 || rulesVersion == RulesV4;

        /// <summary>
        /// Hero top speed in 1/1200 tile per step: 1.5 tiles/s in release-rules-2; 0.8 (about 6.4 m/s, a run) in release-rules-3,
        /// faster than a Runner so the hero can catch one and reach a lane in about 11 s (plan 015 review); 0.7 in release-rules-4.
        /// </summary>
        public static int HeroMoveUnits(string rulesVersion) => HeroProfile.For(rulesVersion)?.MaxSpeedUnits ?? HeroRules.MoveUnitsPerStep;

        /// <summary>
        /// release-rules-3 (plan 015): a Bomber lights its fuse at the first solid building it walks up to, not only at its
        /// route's attack cell. At walking speed almost none reached the attack cell alive. release-rules-4 inherits it.
        /// </summary>
        public static bool BombersLightOnContact(string rulesVersion) => WalkingEnemies(rulesVersion);

        /// <summary>release-rules-4 definitions: release-rules-3 enemies, a 1 x 1 Cannon (owner, 2026-10-21) and upgrade tiers (spec 015).</summary>
        public static PrototypeDefinitions Contact(PrototypeDefinitions defs) => WithLand(WithMine(Pressure(Bodies(Walking(defs))).WithCompactCannon())).WithUpgrades();

        /// <summary>Spec 017 D3 [P]: a House earns 4 + 2 per free fertile neighbour (at most 4), a Farm 8 + 3 (at most 8).</summary>
        public static PrototypeDefinitions WithLand(PrototypeDefinitions defs)
        {
            if (defs.House == null || defs.House.LandIncome > 0) return defs;
            var d = defs.WithEnemies(e => e);
            d.House = defs.House.Copy();
            (d.House.DailyIncome, d.House.LandIncome) = (4, 2);
            d.Farm = defs.Farm.Copy();
            (d.Farm.DailyIncome, d.Farm.LandIncome) = (8, 3);
            return d;
        }

        /// <summary>Spec 016 pressure [P]: release-rules-4 enemy HP against release-rules-3, as a percentage (floor).</summary>
        public const int PressureHpPercent = 100; // round 1 (120, with counts 130) favoured the sealed ring: see plan 036

        /// <summary>Spec 016 pressure [P]: release-rules-4 ordinary enemy counts on Nights 4-12, as a percentage (floor); the
        /// spawn cadence shrinks by the same factor, so a night's spawns take about as long.</summary>
        public const int PressureCountPercent = 100;

        public const int PressureFromNight = 4;

        public static PrototypeDefinitions Pressure(PrototypeDefinitions defs) => defs.WithEnemies(e =>
        {
            e.MaxHp = e.MaxHp * PressureHpPercent / 100;
            return e;
        });

        /// <summary>Spec 016 raid time [P]: how long a raider attacks an economy building beside its route before walking on.</summary>
        public const double RaidSeconds = 5.0;

        /// <summary>Spec 016 Mine [P]: 1 x 1, only on a gold vein, 45 gold, 64 HP, 30 gold a day.</summary>
        public static PrototypeDefinitions WithMine(PrototypeDefinitions defs)
        {
            if (defs.Mine != null) return defs;
            var d = defs.WithEnemies(e => e);
            d.Mine = new BuildingDefinition { Kind = BuildingKind.Mine, DisplayName = "Mine", Cost = 45, MaxHp = 64, DailyIncome = 30 };
            return d;
        }

        /// <summary>Spec 014 (release-rules-4): body radii in tiles [P]; bosses are wider.</summary>
        public static PrototypeDefinitions Bodies(PrototypeDefinitions defs) => defs.WithEnemies(e =>
        {
            e.BodyRadius = e.Kind == EnemyKind.Brute ? 0.10 : EnemyDefinition.IsBossKind(e.Kind) ? 0.18 : 0.07;
            return e;
        });

        /// <summary>release-rules-3 enemy HP against release-rules-2 [P]; Brute and Raider Archer have their own values below.</summary>
        public const double WalkingHpScale = 1.5;

        /// <summary>release-rules-3 enemies [P]: speeds in tiles/s (whole units per step) and HP x WalkingHpScale against release-rules-2.</summary>
        public static PrototypeDefinitions Walking(PrototypeDefinitions defs) => defs.WithEnemies(e =>
        {
            switch (e.Kind)
            {
                case EnemyKind.Runner: e.SpeedTilesPerSecond = 0.6; break;
                case EnemyKind.Brute: e.SpeedTilesPerSecond = 0.2; break;
                case EnemyKind.Archer: e.SpeedTilesPerSecond = 0.25; break;
                case EnemyKind.SiegeGolem:
                case EnemyKind.SiegeCommander: e.SpeedTilesPerSecond = 0.15; break;
                default: e.SpeedTilesPerSecond = 0.3; break; // Grunt, Bomber: a brisk march
            }

            // Brute 180 and Raider Archer 45 (plan 015 review): at x1.5 a Brute outlasted the hero one-on-one, and Brutes and
            // Archers caused nearly all Keep damage on under-built defences, which then failed in a single night.
            e.MaxHp = e.Kind == EnemyKind.Brute ? 180 : e.Kind == EnemyKind.Archer ? 45
                : (int)System.Math.Round(e.MaxHp * WalkingHpScale, System.MidpointRounding.AwayFromZero);
            return e;
        });
        public const string CampaignId = "twelve-nights";
        public const int ContentVersion = 2; // 2: cheaper, weaker buildings; content-1 saves are refused, not rescaled
        public const string Label = "Twelve nights";
        public const uint WaveSalt = 0x5BD1E995;

        /// <summary>Per-night cap on scheduled spawns (the living cap of 100 still applies during play).</summary>
        public const int MaxSpawnsPerNight = 150; // spec 016: 100 → 150; the living cap of 100 is unchanged

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

        /// <summary>Spec 012: release-rules-4 bosses spawn at 15 s (shorter boss tails); older profiles keep 30 s.</summary>
        public const double BossSecondsV4 = 15.0;

        /// <summary>The authored nights for a rules version (only the boss time differs between versions).</summary>
        public static Recipe[] Table(string rulesVersion)
        {
            var table = Table();
            if (rulesVersion != RulesV4) return table;
            for (var i = 0; i < table.Length; i++)
            {
                var r = table[i];
                r.BossSeconds = BossSecondsV4;
                if (i + 1 < PressureFromNight) continue;
                int P(int n) => n * PressureCountPercent / 100;
                (r.Grunts, r.Runners, r.Brutes, r.Bombers, r.Archers) = (P(r.Grunts), P(r.Runners), P(r.Brutes), P(r.Bombers), P(r.Archers));
                r.CadenceSeconds = System.Math.Round(r.CadenceSeconds * 100 / PressureCountPercent, 2);
            }

            return table;
        }

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
        public static CampaignConfig Create(uint seed, string mapProfile, string rulesVersion = RulesVersion) =>
            Create(seed, mapProfile, MapProfiles.SymmetryFor(seed), rulesVersion);

        /// <summary>The recipes' entries are written for the profiles' own layout; they turn with the map's symmetry.</summary>
        public static CampaignConfig Create(uint seed, string mapProfile, int symmetry, string rulesVersion = RulesVersion) =>
            Create(seed, mapProfile, Table(rulesVersion), symmetry, rulesVersion);

        /// <summary>Spec 017: the nights on a map that lists its entries; the table's W, E, N, S are entry indices 0-3 (see <see cref="NightEntries"/>).</summary>
        public static CampaignConfig Create(uint seed, TerrainMap map, string rulesVersion = RulesVersion) =>
            Create(seed, map.Profile, Table(rulesVersion), 0, rulesVersion, map.Entries);

        public static CampaignConfig Create(uint seed, string mapProfile, IReadOnlyList<Recipe> table, int symmetry = 0, string rulesVersion = RulesVersion,
            IReadOnlyList<GridPos> mapEntries = null)
        {
            var rng = new RunRandom(seed ^ WaveSalt);
            var nights = new List<WavePlan>();
            for (var i = 0; i < table.Count; i++)
                nights.Add(mapEntries == null ? BuildNight($"Night {i + 1}", table[i], rng, symmetry) : BuildNight($"Night {i + 1}", table[i], rng, NightEntries(i + 1, table[i], mapEntries)));
            return new CampaignConfig
            {
                Label = Label, Nights = nights, OffersRewards = true,
                RulesVersion = rulesVersion, CampaignId = CampaignId, PerkPool = PerkRules.ReleaseIds,
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
            return BuildNight(name, r, rng, entries);
        }

        /// <summary>From this night on, every entry of the map is open (spec 017 v0.4).</summary>
        public const int AllEntriesFromNight = 8;

        /// <summary>
        /// Spec 017 v0.4: a night's entries on a map with k entries. The recipe's W, E, N, S are indices 0-3; an index the map
        /// lacks becomes its lowest unused one, so the night keeps its count capped at k; from Night 8 every entry is open.
        /// With four entries this is the table exactly.
        /// </summary>
        public static GridPos[] NightEntries(int night, Recipe r, IReadOnlyList<GridPos> map)
        {
            var used = new List<int>();
            if (night >= AllEntriesFromNight)
                for (var i = 0; i < map.Count; i++) used.Add(i);
            else
                foreach (var e in r.Entries)
                {
                    var index = e == W ? 0 : e == E ? 1 : e == N ? 2 : 3;
                    if (index >= map.Count || used.Contains(index))
                    {
                        index = -1;
                        for (var i = 0; i < map.Count && index < 0; i++)
                            if (!used.Contains(i)) index = i;
                    }

                    if (index >= 0) used.Add(index);
                }

            return used.ConvertAll(i => map[i]).ToArray();
        }

        static WavePlan BuildNight(string name, Recipe r, RunRandom rng, GridPos[] entries)
        {
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
