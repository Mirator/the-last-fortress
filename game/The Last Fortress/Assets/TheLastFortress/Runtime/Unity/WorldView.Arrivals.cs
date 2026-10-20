using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Plan 016 walk-ins: before an enemy spawns, the view draws it walking out of the edge mist along its road, timed from
    /// the simulation tick to reach its entry cell exactly at its spawn tick (so pause, game speed and loading cannot drift
    /// it). Read from the known wave schedule; the simulation knows nothing of these figures. They have no HP bar, no
    /// inspection and no route line, and towers cannot shoot them. At spawn the real enemy view takes over the figure's
    /// facing and gait phase, so the hand-over does not pop. A spawn due at the very start of the night (tick 0) has no time
    /// to walk and appears at its entry, as before.
    /// </summary>
    public sealed partial class WorldView
    {
        /// <summary>Road length walked before the spawn, from the entry cell's centre (tiles): it starts deep in the mist.</summary>
        public const float ArrivalWalkTiles = 6f;

        sealed class Arrival
        {
            public int SpawnIndex;
            public SpawnEntry Spawn;
            public GameObject Root;
            public Transform Model;
            public CharacterRig Rig;
            public float GaitPhase;
            public float LastDistance = -1f;
        }

        readonly Dictionary<int, Arrival> arrivals = new Dictionary<int, Arrival>();
        readonly List<int> arrivalScratch = new List<int>();
        WavePlan arrivalWave;
        Transform arrivalsRoot;
        TerrainShape terrainShape;

        /// <summary>Figures walking in (diagnostics and tests).</summary>
        public int ArrivalCount => arrivals.Count;

        /// <summary>Where the walk-in for spawn <paramref name="spawnIndex"/> is drawn, if it is walking (tests).</summary>
        public Vector3? ArrivalShownPosition(int spawnIndex) => arrivals.TryGetValue(spawnIndex, out var a) ? a.Root.transform.position : (Vector3?)null;

        /// <summary>
        /// Point on an entry's road <paramref name="along"/> tiles out from the entry cell's centre: straight to the board
        /// edge, then along the road's centre line as it bends away (the same line the ground is carved for).
        /// </summary>
        public static Vector2 RoadPoint(GridPos entry, float along)
        {
            var c = Prims.CellCenter(entry);
            var outward = RouteOverlay.Outward(entry);
            var o = along - 0.5f; // beyond the board edge
            var across = o <= 0f ? 0f : TerrainShape.RoadBend(outward) * TerrainShape.RoadOffset(o);
            var p = c + outward * along + TerrainShape.AcrossAxis(outward) * across;
            return new Vector2(p.x, p.z);
        }

        void SyncArrivals(RunSimulation sim)
        {
            var active = HasTerrain && terrainShape != null && sim.IsCampaign && !sim.StressMode && sim.State == RunState.Night;
            if (!active || sim.Wave != arrivalWave) ClearArrivals();
            arrivalWave = sim.Wave;
            if (!active) return;
            if (arrivalsRoot == null)
            {
                arrivalsRoot = new GameObject("Arrivals").transform;
                arrivalsRoot.SetParent(transform, false);
            }

            var now = sim.Tick + Mathf.Clamp01((float)sim.InterpolationAlpha);
            var spawns = sim.Wave.Spawns;
            for (var i = sim.SpawnedCount; i < spawns.Count; i++)
            {
                var s = spawns[i];
                var definition = sim.Definitions.EnemyFor(s.Kind);
                if (definition == null) continue;
                var speed = Mathf.Max(0.05f, (float)definition.SpeedTilesPerSecond);
                // Distance still to walk: the spawn's time left at walking speed. A due spawn held by the live cap waits at the entry.
                var left = Mathf.Max(0f, (float)((s.Tick - now) * PrototypeDefinitions.StepSeconds)) * speed;
                if (left > ArrivalWalkTiles)
                {
                    if (s.Tick * PrototypeDefinitions.StepSeconds * speed > ArrivalWalkTiles + 30f) break; // far future: later spawns too
                    continue;
                }

                if (!arrivals.TryGetValue(i, out var a))
                {
                    if (s.Tick == 0) continue; // no time to walk: it appears at its entry
                    a = CreateArrival(i, s);
                    arrivals[i] = a;
                }

                PoseArrival(a, left, speed);
            }

            // Spawned (handed over in CreateEnemyView) or no longer scheduled: gone.
            arrivalScratch.Clear();
            foreach (var pair in arrivals)
                if (pair.Key < sim.SpawnedCount) arrivalScratch.Add(pair.Key);
            foreach (var key in arrivalScratch)
            {
                Destroy(arrivals[key].Root);
                arrivals.Remove(key);
            }
        }

        Arrival CreateArrival(int index, SpawnEntry spawn)
        {
            var root = new GameObject($"Arriving {spawn.Kind} {index}");
            root.transform.SetParent(arrivalsRoot, false);
            if (content.enemyScale > 0f && !Mathf.Approximately(content.enemyScale, 1f)) root.transform.localScale = Vector3.one * content.enemyScale;
            var model = Instantiate(content.EnemyPrefabFor(spawn.Kind), root.transform).transform;
            model.name = "Model";
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Destroy(c); // never a click target
            return new Arrival { SpawnIndex = index, Spawn = spawn, Root = root, Model = model, Rig = model.GetComponent<CharacterRig>() };
        }

        void PoseArrival(Arrival a, float left, float speed)
        {
            var p = RoadPoint(a.Spawn.Cell, left);
            var ahead = RoadPoint(a.Spawn.Cell, Mathf.Max(0f, left - 0.05f));
            var y = terrainShape.Height(p.x, p.y);
            var t = a.Root.transform;
            t.position = new Vector3(p.x, Mathf.Max(0f, y), p.y);
            var facing = new Vector3(ahead.x - p.x, 0f, ahead.y - p.y);
            if (left < 0.05f) facing = RouteOverlay.Outward(a.Spawn.Cell) * -1f; // waiting at the entry: face the board
            if (facing.sqrMagnitude > 1e-8f) t.rotation = Quaternion.LookRotation(facing);
            a.Model.localScale = Vector3.one * CharacterScale;
            var shownHeight = BodyHeight(a.Spawn.Kind) * t.localScale.x * CharacterScale;
            var run = a.Rig != null ? CharacterRig.RunAmount(speed, shownHeight) : 0f;
            var moved = a.LastDistance < 0f ? 0f : Mathf.Abs(a.LastDistance - left) * t.localScale.x;
            a.LastDistance = left;
            if (a.Rig != null)
            {
                a.GaitPhase += moved * CharacterRig.PhasePerTile(shownHeight, run);
                a.Rig.Pose(a.GaitPhase, left > 0f ? 1f : 0f, 0f, 0f, 0f, 0f, run, fxTime * 2.1f + a.SpawnIndex);
            }
        }

        /// <summary>
        /// Called when the real view for <paramref name="e"/> is created: takes over the matching walk-in's facing and gait
        /// phase (same kind and entry, the earliest still walking) and removes the figure.
        /// </summary>
        bool TakeArrival(Enemy e, out Quaternion facing, out float gaitPhase)
        {
            facing = Quaternion.identity;
            gaitPhase = 0f;
            var best = -1;
            foreach (var pair in arrivals)
                if (pair.Value.Spawn.Kind == e.Kind && pair.Value.Spawn.Cell == e.Cell && (best < 0 || pair.Key < best))
                    best = pair.Key;
            if (best < 0) return false;
            var a = arrivals[best];
            facing = a.Root.transform.rotation;
            gaitPhase = a.GaitPhase;
            Destroy(a.Root);
            arrivals.Remove(best);
            return true;
        }

        void ClearArrivals()
        {
            foreach (var a in arrivals.Values) Destroy(a.Root);
            arrivals.Clear();
        }
    }
}
