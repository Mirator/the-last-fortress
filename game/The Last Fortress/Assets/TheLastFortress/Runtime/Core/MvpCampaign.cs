using System.Collections.Generic;

namespace TheLastFortress
{
    /// <summary>
    /// Multi-night run definition. All wave plans are created before Day 1; preview and combat use the same stored
    /// plans. Absent from <see cref="RunConfig"/> for the one-wave prototype.
    /// </summary>
    public sealed class CampaignConfig
    {
        public string Label;
        public IReadOnlyList<WavePlan> Nights;

        /// <summary>Nights 1..N-1 end in a Reward choice (plan 004). False for the plan 003 foundation fixture.</summary>
        public bool OffersRewards;

        /// <summary>Rules profile this run plays under (saved; plan 008). Defaults describe the six-night MVP.</summary>
        public string RulesVersion = MvpCampaign.RulesVersion;

        public string CampaignId = MvpCampaign.CampaignId;

        /// <summary>Perk IDs this run can be offered, in ordinal order. The MVP pool unless a release profile sets it.</summary>
        public IReadOnlyList<string> PerkPool = PerkRules.AllIds;

        /// <summary>Release map profile ID ("" for the MVP generator / flat map) and difficulty; saved with the run.</summary>
        public string MapProfile = "";

        public string Difficulty = "standard";

        public bool IsRelease => RulesVersion == ReleaseCampaign.RulesVersion;

        public int TotalNights => Nights.Count;
    }

    public static class MvpCampaign
    {
        /// <summary>Rules version of the six-night MVP; its saves use checkpoint schema 1 and are never converted.</summary>
        public const string RulesVersion = "mvp-rules-1";

        public const string CampaignId = "six-nights";

        public static readonly GridPos West = new GridPos(0, 10);
        public static readonly GridPos East = new GridPos(19, 10);
        public static readonly GridPos North = new GridPos(10, 19);
        public static readonly GridPos South = new GridPos(10, 0);

        /// <summary>Living-enemy cap; due spawns wait FIFO while it is reached.</summary>
        public const int MaxLiveEnemies = 100;

        public const double BossSpawnSeconds = 30.0;

        /// <summary>Authored night table from specification 003 / plan 004 (non-boss counts are tunable within bounds).</summary>
        public sealed class NightSpec
        {
            public int Grunts, Runners, Brutes, Golems;
            public GridPos[] Entries;
            public double CadenceSeconds;
        }

        public static NightSpec[] SixNightTable() => new[]
        {
            new NightSpec { Grunts = 10, Entries = new[] { West }, CadenceSeconds = 1.5 },
            new NightSpec { Grunts = 12, Runners = 4, Entries = new[] { West, East }, CadenceSeconds = 1.5 },
            new NightSpec { Grunts = 14, Runners = 4, Brutes = 2, Entries = new[] { West, East }, CadenceSeconds = 1.25 },
            new NightSpec { Grunts = 18, Runners = 8, Brutes = 3, Entries = new[] { West, East, North }, CadenceSeconds = 1.0 },
            new NightSpec { Grunts = 22, Runners = 10, Brutes = 4, Entries = new[] { West, East, North }, CadenceSeconds = 1.0 },
            new NightSpec { Grunts = 24, Runners = 12, Brutes = 4, Golems = 1, Entries = new[] { West, East, North, South }, CadenceSeconds = 1.0 },
        };

        /// <summary>The normal six-night MVP profile with Reward choices after Nights 1-5.</summary>
        public static CampaignConfig CreateSixNights() => FromTable("Six nights", SixNightTable(), true);

        public static CampaignConfig FromTable(string label, IReadOnlyList<NightSpec> table, bool offersRewards)
        {
            var nights = new List<WavePlan>();
            for (var i = 0; i < table.Count; i++) nights.Add(BuildNight($"Night {i + 1}", table[i]));
            return new CampaignConfig { Label = label, Nights = nights, OffersRewards = offersRewards };
        }

        /// <summary>
        /// Non-boss order cycles Grunt, Runner, Brute (skipping exhausted types); entries are assigned round-robin in
        /// table order; first spawn at zero, then the cadence. Golems spawn from West at 30 s, after equal-time non-boss spawns.
        /// </summary>
        public static WavePlan BuildNight(string name, NightSpec spec)
        {
            var cadence = PrototypeDefinitions.ToTicks(spec.CadenceSeconds);
            var remaining = new[] { spec.Grunts, spec.Runners, spec.Brutes };
            var kinds = new[] { EnemyKind.Grunt, EnemyKind.Runner, EnemyKind.Brute };
            var spawns = new List<SpawnEntry>();
            var index = 0;
            while (remaining[0] + remaining[1] + remaining[2] > 0)
            {
                for (var k = 0; k < 3; k++)
                {
                    if (remaining[k] == 0) continue;
                    remaining[k]--;
                    var entry = index % spec.Entries.Length;
                    spawns.Add(new SpawnEntry(index * (long)cadence, spec.Entries[entry], entry, kinds[k]));
                    index++;
                }
            }

            var bossTick = PrototypeDefinitions.ToTicks(BossSpawnSeconds);
            for (var g = 0; g < spec.Golems; g++)
                spawns.Add(new SpawnEntry(bossTick, spec.Entries[0], 0, EnemyKind.SiegeGolem)); // stable sort keeps it after equal-time spawns
            return new WavePlan(name, spawns, spec.Entries);
        }

        /// <summary>
        /// Plan 003 development fixture: three Grunt-only nights (10 W; 16 and 22 round-robin W/E; every 1.5 s), no rewards.
        /// </summary>
        public static CampaignConfig CreateFoundation() => FromTable("Foundation — 3 nights", new[]
        {
            new NightSpec { Grunts = 10, Entries = new[] { West }, CadenceSeconds = 1.5 },
            new NightSpec { Grunts = 16, Entries = new[] { West, East }, CadenceSeconds = 1.5 },
            new NightSpec { Grunts = 22, Entries = new[] { West, East }, CadenceSeconds = 1.5 },
        }, false);
    }
}
