using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Standalone stress measurement (-stress -measure &lt;file&gt;): 10 s warmup, 60 s of frame times,
    /// scripted removal of the west entry's first Wall blocker at 30 measured seconds, then writes results and quits.
    /// </summary>
    public sealed class StressMeasurement : MonoBehaviour
    {
        public const float WarmupSeconds = 10f;
        public const float MeasureSeconds = 60f;
        public const float EventAtMeasuredSeconds = 30f;

        PrototypeBootstrap game;
        string outPath;
        float startTime;
        readonly List<double> samples = new List<double>(20000);
        Building target;
        GridPos targetCell;
        int eventSampleIndex = -1;
        bool eventDone, eventValid, written;
        int attackersAtEvent;
        int revisionBefore, revisionAfter;
        string routeAfter = "";
        int minActive = int.MaxValue, maxActive;
        long simStepsAtStart;

        public void Begin(PrototypeBootstrap game, string outPath)
        {
            this.game = game;
            this.outPath = outPath;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
            var west = game.Sim.GetLiveRoute(WavePlan.StressEntries[0]);
            target = west.IsBreach ? game.Sim.Grid.GetOccupant(west.Blocker) : null;
            targetCell = west.IsBreach ? west.Blocker : default;
            if (target == null || target.Kind != BuildingKind.Wall) Debug.LogError("Stress measurement: west route has no Wall blocker.");
            startTime = Time.realtimeSinceStartup;
        }

        void Update()
        {
            if (game == null || written) return;
            var elapsed = Time.realtimeSinceStartup - startTime;
            if (elapsed < WarmupSeconds) return;
            if (samples.Count == 0) simStepsAtStart = game.Sim.Tick;

            samples.Add(Time.unscaledDeltaTime * 1000.0);
            var active = game.Sim.AliveEnemyCount;
            minActive = Math.Min(minActive, active);
            maxActive = Math.Max(maxActive, active);

            var measured = elapsed - WarmupSeconds;
            if (!eventDone && measured >= EventAtMeasuredSeconds)
            {
                eventDone = true;
                attackersAtEvent = target != null ? game.Sim.CountAttackers(target) : 0;
                revisionBefore = game.Sim.Grid.Revision;
                var removed = target != null && game.Sim.ScriptedRemove(target, out var underAttack) && underAttack;
                revisionAfter = game.Sim.Grid.Revision;
                eventValid = removed && attackersAtEvent > 0 && revisionAfter == revisionBefore + 1;
                eventSampleIndex = samples.Count; // the removal's cost shows in the next frame's delta
                var r = game.Sim.GetLiveRoute(WavePlan.StressEntries[0]);
                routeAfter = r.IsBreach ? $"breach at {r.Blocker}" : $"open to {r.Goal}";
            }

            if (measured >= MeasureSeconds) WriteAndQuit();
        }

        public static double Percentile(List<double> sorted, double p)
        {
            var index = (int)Math.Ceiling(p * sorted.Count) - 1;
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, index))];
        }

        void WriteAndQuit()
        {
            written = true;
            var sorted = new List<double>(samples);
            sorted.Sort();
            var inv = CultureInfo.InvariantCulture;
            double median = Percentile(sorted, 0.5), p95 = Percentile(sorted, 0.95), p99 = Percentile(sorted, 0.99);
            var pass = eventValid && median <= 16.7 && p95 <= 20.0 && p99 <= 33.3;
            var sb = new StringBuilder();
            sb.AppendLine("# The Last Fortress stress measurement");
            sb.AppendLine($"date_utc={DateTime.UtcNow:O}");
            sb.AppendLine($"build_type={(Debug.isDebugBuild ? "development" : "non-development")}");
            sb.AppendLine($"platform={Application.platform} editor={Application.isEditor}");
            sb.AppendLine($"resolution={Screen.width}x{Screen.height} fullscreen={Screen.fullScreenMode}");
            sb.AppendLine($"quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} vsync={QualitySettings.vSyncCount} targetFrameRate={Application.targetFrameRate}");
            sb.AppendLine($"cpu={SystemInfo.processorType} gpu={SystemInfo.graphicsDeviceName} ram_mb={SystemInfo.systemMemorySize} api={SystemInfo.graphicsDeviceType}");
            sb.AppendLine($"unity={Application.unityVersion} rules_version={CheckpointCodec.RulesVersion} scene={(game.IsMvp ? "MVP (MVP prefabs, connected Walls, effects)" : "prototype")}");
            var wave = game.Sim.Wave;
            var mix = wave.MaintainMix == null ? $"{wave.MaintainActivePerEntry} Grunt" : string.Join(", ", System.Array.ConvertAll(wave.MaintainMix, m => $"{m.count} {m.kind}"));
            sb.AppendLine($"fixture={wave.Name} entries={string.Join(" ", wave.EntryCells)} maintained_per_entry=[{mix}] respawn=same archetype at same entry; Keep/Walls invulnerable (fixture only)");
            var layout = new List<string>();
            foreach (var b in game.Sim.Grid.AllBuildings()) layout.Add($"{b.Kind}{b.Cell}");
            sb.AppendLine($"layout={string.Join(" ", layout)}");
            sb.AppendLine($"warmup_s={WarmupSeconds} measured_s={MeasureSeconds} samples={samples.Count}");
            sb.AppendLine($"sim_steps_during_measure={game.Sim.Tick - simStepsAtStart}");
            sb.AppendLine($"active_enemies_min={minActive} max={maxActive}");
            sb.AppendLine(string.Format(inv, "median_ms={0:0.000} p95_ms={1:0.000} p99_ms={2:0.000} max_ms={3:0.000} mean_ms={4:0.000}",
                median, p95, p99, sorted[sorted.Count - 1], Mean(samples)));
            sb.AppendLine("percentile_definition=sorted[ceil(p*count)-1]");
            sb.AppendLine($"event_target={targetCell} kind={target?.Kind} attackers_immediately_before={attackersAtEvent}");
            sb.AppendLine($"event_revision={revisionBefore}->{revisionAfter} west_route_after={routeAfter}");
            sb.AppendLine($"event_valid={eventValid}");
            if (eventSampleIndex >= 0)
            {
                sb.Append("event_sample_index=").Append(eventSampleIndex).Append(" frames_around_event_ms=");
                for (var i = Math.Max(0, eventSampleIndex - 2); i < Math.Min(samples.Count, eventSampleIndex + 4); i++)
                    sb.Append(i == eventSampleIndex ? "[" : "").Append(samples[i].ToString("0.000", inv)).Append(i == eventSampleIndex ? "]" : "").Append(' ');
                sb.AppendLine();
            }

            sb.Append("spikes_over_33.3ms=");
            for (var i = 0; i < samples.Count; i++)
                if (samples[i] > 33.3) sb.Append(i).Append(':').Append(samples[i].ToString("0.0", inv)).Append(' ');
            sb.AppendLine();
            sb.AppendLine($"gate_pass={pass}");
            sb.AppendLine("samples_ms=");
            foreach (var s in samples) sb.AppendLine(s.ToString("0.0000", inv));

            var dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"Stress measurement written to {outPath}: median {median:0.00} p95 {p95:0.00} p99 {p99:0.00} valid {eventValid}");
            Application.Quit();
        }

        static double Mean(List<double> values)
        {
            double sum = 0;
            foreach (var v in values) sum += v;
            return values.Count == 0 ? 0 : sum / values.Count;
        }
    }
}
