using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TheLastFortress.Tests
{
    /// <summary>
    /// Diagnostic (Explicit, not part of the suites): plays a mixed release night at a steady 60 fps through the real scene
    /// and measures every enemy view frame by frame — position steps against its speed, stalls while walking, and turn rate.
    /// Writes TestResults/008/motion.txt.
    /// </summary>
    [Explicit("diagnostic")]
    public class MotionProbe
    {
        sealed class Track
        {
            public EnemyKind Kind;
            public Vector3 LastPos;
            public float LastYaw;
            public bool Has;
            public int Frames, Jumps, Stalls, Snaps, ModelJerks;
            public float MaxStepRatio, MaxTurn, MaxModelStep;
            public Vector3 LastModel;
        }

        [UnityTest]
        public IEnumerator MeasureEnemyMotionAt60Fps()
        {
            var dir = Path.Combine(Path.GetTempPath(), "tlf-motion-" + System.Guid.NewGuid().ToString("N"));
            SaveStore.DirectoryOverride = dir;
            SceneManager.LoadScene("Release", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var game = Object.FindFirstObjectByType<PrototypeBootstrap>();
            var kinds = new[] { EnemyKind.Grunt, EnemyKind.Runner, EnemyKind.Brute, EnemyKind.Bomber, EnemyKind.Archer, EnemyKind.SiegeGolem, EnemyKind.SiegeCommander };
            var spawns = new List<SpawnEntry>();
            for (var i = 0; i < kinds.Length; i++)
            {
                spawns.Add(new SpawnEntry(i * 20, MvpCampaign.West, 0, kinds[i]));
                spawns.Add(new SpawnEntry(i * 20 + 10, MvpCampaign.North, 1, kinds[i]));
            }

            var wave = new WavePlan("motion", spawns, new[] { MvpCampaign.West, MvpCampaign.North });
            var campaign = new CampaignConfig
            {
                Label = "motion", Nights = new[] { wave, new WavePlan("calm", new SpawnEntry[0], new[] { MvpCampaign.West }) }, OffersRewards = true,
                RulesVersion = ReleaseCampaign.RulesVersion, CampaignId = ReleaseCampaign.CampaignId, PerkPool = PerkRules.ReleaseIds, MapProfile = MapProfiles.Meadow,
            };
            var setup = new RunSimulation(new RunConfig { Definitions = game.NewRunDefinitions, Seed = 3, Terrain = TerrainMap.Flat(), Campaign = campaign });
            setup.TryPlace(BuildingKind.Tower, new GridPos(6, 8), out _);  // shots, recoil, an Archer target
            setup.TryPlace(BuildingKind.Wall, new GridPos(5, 12), out _);
            setup.TryPlace(BuildingKind.Wall, new GridPos(9, 14), out _);  // a detour on the north route: corner turns
            setup.TryPlace(BuildingKind.Wall, new GridPos(10, 14), out _);
            setup.TryPlace(BuildingKind.Wall, new GridPos(11, 14), out _);
            Assert.IsTrue(game.Persistence.Store.WriteRun(0, CheckpointCodec.Capture(setup), out var err), err);
            game.OpenMenu();
            game.Continue();
            game.AutoAdvance = false;
            game.ExternalViewClock = true; // one presentation advance per frame, by exactly 1/60 s
            Assert.IsTrue(game.StartNight());

            const float dt = 1f / 60f;
            var tracks = new Dictionary<int, Track>();
            var enemiesRoot = game.World.transform.Find("Enemies");
            var log = new StringBuilder();
            var buildingsRoot = game.World.transform.Find("Buildings");
            var lastScale = new Dictionary<string, Vector3>();
            float maxScaleStep = 0f;
            int scaleJerks = 0, poppedBeforeHit = 0, deaths = 0, buildingPops = 0;
            var alive = new Dictionary<int, Vector3>();
            var lastBuildings = new HashSet<string>();
            for (var frame = 0; frame < 60 * 70 && game.Sim.State == RunState.Night; frame++)
            {
                game.Sim.Advance(dt);
                game.SyncViews(dt);
                // Deaths: an enemy view that vanished this frame while a drawn arrow/shell is still flying at it pops early.
                var nowAlive = new Dictionary<int, Vector3>();
                foreach (Transform v in enemiesRoot)
                {
                    var hash = v.name.LastIndexOf('#');
                    if (hash >= 0 && int.TryParse(v.name.Substring(hash + 1), out var id)) nowAlive[id] = v.position;
                }

                foreach (var pair in alive)
                {
                    if (nowAlive.ContainsKey(pair.Key)) continue;
                    deaths++;
                    if (game.World.ArrowTargets().Any(a => new Vector2(a.x - pair.Value.x, a.z - pair.Value.z).magnitude < 0.8f)) poppedBeforeHit++;
                }

                alive = nowAlive;
                var nowBuildings = new HashSet<string>();
                foreach (Transform b in buildingsRoot)
                {
                    nowBuildings.Add(b.name);
                    if (lastScale.TryGetValue(b.name, out var ls))
                    {
                        var d = (b.localScale - ls).magnitude;
                        maxScaleStep = Mathf.Max(maxScaleStep, d);
                        if (d > 0.05f) scaleJerks++;
                    }

                    lastScale[b.name] = b.localScale;
                }

                foreach (var name in lastBuildings)
                    if (!nowBuildings.Contains(name)) buildingPops++;
                lastBuildings = nowBuildings;

                foreach (var e in game.Sim.Enemies)
                {
                    var view = enemiesRoot.Find($"{e.Kind} #{e.Id}");
                    if (view == null) continue;
                    if (!tracks.TryGetValue(e.Id, out var t)) tracks[e.Id] = t = new Track { Kind = e.Kind };
                    var pos = view.position;
                    var yaw = view.eulerAngles.y;
                    if (t.Has)
                    {
                        var step = new Vector2(pos.x - t.LastPos.x, pos.z - t.LastPos.z).magnitude;
                        var expected = (float)e.Definition.SpeedTilesPerSecond * dt;
                        var ratio = step / expected;
                        t.MaxStepRatio = Mathf.Max(t.MaxStepRatio, ratio);
                        if (ratio > 1.6f) t.Jumps++;
                        if (e.Moving && step < 1e-5f) t.Stalls++;
                        var turn = Mathf.Abs(Mathf.DeltaAngle(t.LastYaw, yaw));
                        t.MaxTurn = Mathf.Max(t.MaxTurn, turn);
                        if (turn > 25f) t.Snaps++;
                        var model = view.Find("Model").localPosition;
                        var ms = (model - t.LastModel).magnitude;
                        t.MaxModelStep = Mathf.Max(t.MaxModelStep, ms);
                        if (ms > 0.04f) t.ModelJerks++;
                        t.Frames++;
                    }

                    t.LastModel = view.Find("Model").localPosition;

                    t.LastPos = pos;
                    t.LastYaw = yaw;
                    t.Has = true;
                }

                yield return null;
            }

            log.AppendLine($"night ended: {game.Sim.State} after {game.Sim.NightElapsedSeconds:0.0}s; frames at fixed 1/60 s");
            log.AppendLine("kind            frames  maxStep/expected  jumps(>1.6x)  stalls-while-moving  maxTurn/frame  snaps(>25deg)  maxModelStep  modelJerks(>0.04)");
            foreach (var g in tracks.Values.GroupBy(t => t.Kind))
                log.AppendLine($"{g.Key,-15} {g.Sum(t => t.Frames),6}  {g.Max(t => t.MaxStepRatio),16:0.00}  {g.Sum(t => t.Jumps),12}  {g.Sum(t => t.Stalls),19}  {g.Max(t => t.MaxTurn),13:0.0}  {g.Sum(t => t.Snaps),13}  {g.Max(t => t.MaxModelStep),12:0.000}  {g.Sum(t => t.ModelJerks),17}");
            log.AppendLine($"enemy views removed: {deaths}; removed while a drawn arrow/shell was still flying at them: {poppedBeforeHit}");
            log.AppendLine($"building views removed: {buildingPops}; building scale max step/frame {maxScaleStep:0.000}, jerks(>0.05) {scaleJerks}");
            Directory.CreateDirectory(Path.Combine("TestResults", "008"));
            File.WriteAllText(Path.Combine("TestResults", "008", "motion.txt"), log.ToString());
            TestContext.WriteLine(log.ToString());
            SaveStore.DirectoryOverride = null;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
