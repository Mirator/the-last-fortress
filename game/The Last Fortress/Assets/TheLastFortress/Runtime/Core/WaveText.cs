using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TheLastFortress
{
    /// <summary>Exact human-readable preview of a stored wave plan (types/counts per entry, first/last time, boss warning).</summary>
    public static class WaveText
    {
        public static string EntryName(GridPos cell)
        {
            if (cell == MvpCampaign.West) return "West";
            if (cell == MvpCampaign.East) return "East";
            if (cell == MvpCampaign.North) return "North";
            if (cell == MvpCampaign.South) return "South";
            return cell.ToString();
        }

        public static string Plural(EnemyKind kind, int n)
        {
            switch (kind)
            {
                case EnemyKind.Grunt: return n == 1 ? "Grunt" : "Grunts";
                case EnemyKind.Runner: return n == 1 ? "Runner" : "Runners";
                case EnemyKind.Brute: return n == 1 ? "Brute" : "Brutes";
                case EnemyKind.Archer: return n == 1 ? "Raider Archer" : "Raider Archers";
                case EnemyKind.Bomber: return n == 1 ? "Bomber" : "Bombers";
                case EnemyKind.SiegeCommander: return n == 1 ? "Siege Commander" : "Siege Commanders";
                default: return n == 1 ? "Siege Golem" : "Siege Golems";
            }
        }

        /// <summary>Ordinary kinds in preview order (the six-night MVP only ever shows the first three).</summary>
        static readonly EnemyKind[] OrdinaryOrder = { EnemyKind.Grunt, EnemyKind.Runner, EnemyKind.Brute, EnemyKind.Bomber, EnemyKind.Archer };

        static string Seconds(long tick) => (tick * PrototypeDefinitions.StepSeconds).ToString("0.0#", CultureInfo.InvariantCulture);

        /// <summary>Compact HUD preview: "Tonight: West 6 Grunts + 2 Runners · East 8 Grunts (0–22.5 s)" plus a boss line.</summary>
        public static string Tonight(WavePlan wave)
        {
            var counts = new int[wave.EntryCells.Count, EnemyDefinition.KindCount];
            long first = -1, last = -1, bossTick = -1;
            var bossFrom = "";
            var boss = EnemyKind.SiegeGolem;
            foreach (var s in wave.Spawns)
            {
                counts[s.EntryIndex, (int)s.Kind]++;
                if (EnemyDefinition.IsBossKind(s.Kind))
                {
                    bossTick = s.Tick;
                    bossFrom = EntryName(s.Cell);
                    boss = s.Kind;
                }
                else
                {
                    if (first < 0) first = s.Tick;
                    last = s.Tick;
                }
            }

            var parts = new List<string>();
            var kindsTonight = 0;
            foreach (var k in OrdinaryOrder)
                for (var e = 0; e < wave.EntryCells.Count; e++)
                    if (counts[e, (int)k] > 0)
                    {
                        kindsTonight++;
                        break;
                    }

            string text;
            if (kindsTonight > 3)
            {
                // Release nights (four or five ordinary kinds on up to four entries): totals per kind, then per entry, so the
                // status panel stays two lines. The six-night MVP never has more than three kinds and keeps the form below.
                var totals = new List<string>();
                foreach (var k in OrdinaryOrder)
                {
                    var sum = 0;
                    for (var e = 0; e < wave.EntryCells.Count; e++) sum += counts[e, (int)k];
                    if (sum > 0) totals.Add($"{sum} {Plural(k, sum)}");
                }

                for (var e = 0; e < wave.EntryCells.Count; e++)
                {
                    var sum = 0;
                    foreach (var k in OrdinaryOrder) sum += counts[e, (int)k];
                    if (sum > 0) parts.Add($"{EntryName(wave.EntryCells[e])} {sum}");
                }

                text = $"Tonight: {string.Join(", ", totals)}\nfrom {string.Join(" · ", parts)}";
            }
            else
            {
                for (var e = 0; e < wave.EntryCells.Count; e++)
                {
                    var kinds = new List<string>();
                    foreach (var k in OrdinaryOrder)
                        if (counts[e, (int)k] > 0) kinds.Add($"{counts[e, (int)k]} {Plural(k, counts[e, (int)k])}");
                    if (kinds.Count > 0) parts.Add($"{EntryName(wave.EntryCells[e])} {string.Join(" + ", kinds)}");
                }

                text = $"Tonight: {string.Join(" · ", parts)}";
            }

            if (first >= 0) text += $" ({Seconds(first)}–{Seconds(last)} s)";
            if (bossTick >= 0) text += $"\nBOSS: {Plural(boss, 1)} from {bossFrom} at {Seconds(bossTick)} s";
            return text;
        }

        /// <summary>Entry names active tonight that were not active the previous night (null when none / Night 1).</summary>
        public static string NewEntries(CampaignConfig campaign, int nightNumber)
        {
            if (nightNumber < 2 || nightNumber > campaign.TotalNights) return null;
            var previous = campaign.Nights[nightNumber - 2].EntryCells;
            var names = new List<string>();
            foreach (var e in campaign.Nights[nightNumber - 1].EntryCells)
            {
                var seen = false;
                foreach (var p in previous)
                    if (p == e) seen = true;
                if (!seen) names.Add(EntryName(e));
            }

            return names.Count == 0 ? null : string.Join(" and ", names);
        }

        public static string Describe(WavePlan wave, int nightNumber)
        {
            var counts = new int[wave.EntryCells.Count, EnemyDefinition.KindCount];
            long firstNormal = -1, lastNormal = -1, bossTick = -1;
            var bosses = 0;
            var boss = EnemyKind.SiegeGolem;
            var from = "";
            foreach (var s in wave.Spawns)
            {
                counts[s.EntryIndex, (int)s.Kind]++;
                if (EnemyDefinition.IsBossKind(s.Kind))
                {
                    bosses++;
                    bossTick = s.Tick;
                    boss = s.Kind;
                    from = EntryName(s.Cell);
                    continue;
                }

                if (firstNormal < 0) firstNormal = s.Tick;
                lastNormal = s.Tick;
            }

            var parts = new List<string>();
            for (var e = 0; e < wave.EntryCells.Count; e++)
            {
                var kinds = new List<string>();
                foreach (var k in OrdinaryOrder)
                    if (counts[e, (int)k] > 0) kinds.Add($"{counts[e, (int)k]} {Plural(k, counts[e, (int)k])}");
                if (kinds.Count > 0) parts.Add($"{string.Join(" + ", kinds)} from {EntryName(wave.EntryCells[e])}");
            }

            var sb = new StringBuilder();
            sb.Append("Night ").Append(nightNumber).Append(": ").Append(string.Join(", ", parts));
            if (firstNormal >= 0) sb.Append(" · spawns ").Append(Seconds(firstNormal)).Append("–").Append(Seconds(lastNormal)).Append(" s");
            if (bosses > 0)
            {
                sb.Append("\nBOSS WARNING: ").Append(bosses).Append(' ').Append(Plural(boss, bosses))
                    .Append(" from ").Append(from).Append(" at ").Append(Seconds(bossTick)).Append(" s");
            }

            return sb.ToString();
        }
    }
}
