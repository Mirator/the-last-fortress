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
                default: return n == 1 ? "Siege Golem" : "Siege Golems";
            }
        }

        static string Seconds(long tick) => (tick * PrototypeDefinitions.StepSeconds).ToString("0.0#", CultureInfo.InvariantCulture);

        /// <summary>Compact HUD preview: "Tonight: West 6 Grunts + 2 Runners · East 8 Grunts (0–22.5 s)" plus a boss line.</summary>
        public static string Tonight(WavePlan wave)
        {
            var counts = new int[wave.EntryCells.Count, 4];
            long first = -1, last = -1, bossTick = -1;
            var bossFrom = "";
            foreach (var s in wave.Spawns)
            {
                counts[s.EntryIndex, (int)s.Kind]++;
                if (s.Kind == EnemyKind.SiegeGolem)
                {
                    bossTick = s.Tick;
                    bossFrom = EntryName(s.Cell);
                }
                else
                {
                    if (first < 0) first = s.Tick;
                    last = s.Tick;
                }
            }

            var parts = new List<string>();
            for (var e = 0; e < wave.EntryCells.Count; e++)
            {
                var kinds = new List<string>();
                for (var k = 0; k < 3; k++)
                    if (counts[e, k] > 0) kinds.Add($"{counts[e, k]} {Plural((EnemyKind)k, counts[e, k])}");
                if (kinds.Count > 0) parts.Add($"{EntryName(wave.EntryCells[e])} {string.Join(" + ", kinds)}");
            }

            var text = $"Tonight: {string.Join(" · ", parts)}";
            if (first >= 0) text += $" ({Seconds(first)}–{Seconds(last)} s)";
            if (bossTick >= 0) text += $"\nBOSS: Siege Golem from {bossFrom} at {Seconds(bossTick)} s";
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
            var counts = new int[wave.EntryCells.Count, 4];
            long firstNormal = -1, lastNormal = -1, bossTick = -1;
            var bosses = 0;
            foreach (var s in wave.Spawns)
            {
                counts[s.EntryIndex, (int)s.Kind]++;
                if (s.Kind == EnemyKind.SiegeGolem)
                {
                    bosses++;
                    bossTick = s.Tick;
                    continue;
                }

                if (firstNormal < 0) firstNormal = s.Tick;
                lastNormal = s.Tick;
            }

            var parts = new List<string>();
            for (var e = 0; e < wave.EntryCells.Count; e++)
            {
                var kinds = new List<string>();
                for (var k = 0; k < 3; k++)
                    if (counts[e, k] > 0) kinds.Add($"{counts[e, k]} {Plural((EnemyKind)k, counts[e, k])}");
                if (kinds.Count > 0) parts.Add($"{string.Join(" + ", kinds)} from {EntryName(wave.EntryCells[e])}");
            }

            var sb = new StringBuilder();
            sb.Append("Night ").Append(nightNumber).Append(": ").Append(string.Join(", ", parts));
            if (firstNormal >= 0) sb.Append(" · spawns ").Append(Seconds(firstNormal)).Append("–").Append(Seconds(lastNormal)).Append(" s");
            if (bosses > 0)
            {
                var from = "";
                foreach (var s in wave.Spawns)
                    if (s.Kind == EnemyKind.SiegeGolem) from = EntryName(s.Cell);
                sb.Append("\nBOSS WARNING: ").Append(bosses).Append(' ').Append(Plural(EnemyKind.SiegeGolem, bosses))
                    .Append(" from ").Append(from).Append(" at ").Append(Seconds(bossTick)).Append(" s");
            }

            return sb.ToString();
        }
    }
}
