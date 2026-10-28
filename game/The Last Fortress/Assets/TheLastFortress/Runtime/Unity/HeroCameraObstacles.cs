using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Read-only camera clearance against presentation blockers (specs/003 section 6): the boxes
    /// <see cref="WorldView.CollectCameraBlockers"/> takes from what is drawn for buildings, trees and boulders, plus the
    /// ground. A pose is clear when its whole near-plane volume (the camera origin and the near-plane corners, padded) lies
    /// outside every box and above the ground, and the line from the pivot to it crosses none. Never touches the simulation.
    /// </summary>
    public sealed class HeroCameraObstacles
    {
        /// <summary>Padding around every box and above the ground, in world units (tiles).</summary>
        public const float Margin = 0.02f;

        readonly List<Bounds> boxes = new List<Bounds>();
        int version = -1;
        WorldView source;

        public IReadOnlyList<Bounds> Boxes => boxes;

        /// <summary>Re-reads the blockers when the drawn buildings or terrain changed.</summary>
        public void Refresh(WorldView world)
        {
            if (world == null) return;
            if (world == source && world.CameraBlockerVersion == version) return;
            source = world;
            version = world.CameraBlockerVersion;
            world.CollectCameraBlockers(boxes);
        }

        /// <summary>For tests and tools: blockers given directly.</summary>
        public void Set(IEnumerable<Bounds> given)
        {
            boxes.Clear();
            boxes.AddRange(given);
            source = null;
            version = -1;
        }

        /// <summary>Whether a point, padded by <paramref name="pad"/>, is inside a blocker or below the ground.</summary>
        public bool Blocked(Vector3 p, float pad)
        {
            if (p.y < pad) return true;
            foreach (var b in boxes)
            {
                var e = b.extents + Vector3.one * pad;
                var d = p - b.center;
                if (Mathf.Abs(d.x) < e.x && Mathf.Abs(d.y) < e.y && Mathf.Abs(d.z) < e.z) return true;
            }

            return false;
        }

        /// <summary>
        /// Moves a point sideways (X and Z) out of every blocker it lies within <paramref name="pad"/> of, the shortest way
        /// out of each box. False when that is not clear or needs more than <paramref name="maxStep"/>: a hero pressed against
        /// a drawn Wall or Tower stands closer to it than the near-plane padding, so its pivot steps off the surface.
        /// </summary>
        public bool StepOut(Vector3 p, float pad, float maxStep, out Vector3 clear)
        {
            clear = p;
            for (var pass = 0; pass < 4; pass++)
            {
                var moved = false;
                foreach (var b in boxes)
                {
                    var e = b.extents + Vector3.one * pad;
                    var d = clear - b.center;
                    if (Mathf.Abs(d.x) >= e.x || Mathf.Abs(d.y) >= e.y || Mathf.Abs(d.z) >= e.z) continue;
                    float inX = e.x - Mathf.Abs(d.x), inZ = e.z - Mathf.Abs(d.z);
                    if (inX <= inZ) clear.x += (d.x < 0f ? -1f : 1f) * (inX + 1e-4f);
                    else clear.z += (d.z < 0f ? -1f : 1f) * (inZ + 1e-4f);
                    moved = true;
                }

                if (!moved) break;
            }

            return new Vector2(clear.x - p.x, clear.z - p.z).magnitude <= maxStep && !Blocked(clear, pad);
        }

        /// <summary>
        /// Distance along <paramref name="dir"/> (unit) from <paramref name="from"/> to the first padded blocker or the
        /// padded ground, at most <paramref name="max"/>; 0 when the start itself is blocked.
        /// </summary>
        public float FreeDistance(Vector3 from, Vector3 dir, float max, float pad)
        {
            if (Blocked(from, pad)) return 0f;
            var t = max;
            if (dir.y < -1e-5f) t = Mathf.Min(t, (from.y - pad) / -dir.y);
            foreach (var b in boxes)
            {
                var entry = Entry(from, dir, b.center, b.extents + Vector3.one * pad);
                if (entry < t) t = entry;
            }

            return Mathf.Max(0f, t);
        }

        /// <summary>Slab test: the ray's entry parameter into the box, or +infinity if it misses (start outside).</summary>
        static float Entry(Vector3 o, Vector3 d, Vector3 c, Vector3 e)
        {
            float near = float.NegativeInfinity, far = float.PositiveInfinity;
            for (var i = 0; i < 3; i++)
            {
                var oi = o[i] - c[i];
                if (Mathf.Abs(d[i]) < 1e-7f)
                {
                    if (oi <= -e[i] || oi >= e[i]) return float.PositiveInfinity;
                    continue;
                }

                var t1 = (-e[i] - oi) / d[i];
                var t2 = (e[i] - oi) / d[i];
                if (t1 > t2) (t1, t2) = (t2, t1);
                near = Mathf.Max(near, t1);
                far = Mathf.Min(far, t2);
                if (near > far) return float.PositiveInfinity;
            }

            return far < 0f ? float.PositiveInfinity : Mathf.Max(0f, near);
        }

        /// <summary>
        /// Whether a camera pose is clear: the line from the pivot to the camera crosses no blocker, and the camera origin
        /// and its four near-plane corners each keep <see cref="Margin"/> from every blocker and the ground.
        /// </summary>
        public bool PoseClear(Vector3 pivot, Vector3 position, Quaternion rotation, Camera cam)
        {
            var toCam = position - pivot;
            var length = toCam.magnitude;
            if (length > 1e-4f && FreeDistance(pivot, toCam / length, length, 0f) < length - 1e-4f) return false;
            if (Blocked(position, Margin)) return false;
            var near = cam.nearClipPlane;
            var hh = near * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var hw = hh * cam.aspect;
            for (var i = 0; i < 4; i++)
            {
                var corner = position + rotation * new Vector3((i & 1) == 0 ? -hw : hw, (i & 2) == 0 ? -hh : hh, near);
                if (Blocked(corner, Margin)) return false;
            }

            return true;
        }

        /// <summary>Radius around the camera origin that holds its near plane, for padding the distance search.</summary>
        public static float NearPadding(Camera cam)
        {
            var near = cam.nearClipPlane;
            var hh = near * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var hw = hh * cam.aspect;
            return Mathf.Sqrt(near * near + hh * hh + hw * hw) + Margin;
        }
    }
}
