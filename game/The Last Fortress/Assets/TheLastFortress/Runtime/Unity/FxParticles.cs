using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Pooled cube particles for fire, sparks, smoke and debris. Presentation only: advanced on the presentation clock (so
    /// they freeze while paused), never read by the simulation. Bounded: when the pool is full new particles are dropped,
    /// never the ones already showing. Unlit transparent material, so flames read at night.
    /// </summary>
    public sealed class FxParticles
    {
        public const int MaxParticles = 900;

        sealed class P
        {
            public Transform T;
            public MeshRenderer R;
            public Vector3 Pos, Vel, Spin;
            public float Age, Life, Size0, Size1, Gravity, Drag;
            public Color C0, C1;
            public bool Flat, Ground;
        }

        readonly Transform root;
        readonly Material material;
        readonly List<P> live = new List<P>();
        readonly Stack<P> pool = new Stack<P>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly System.Random random = new System.Random(20261016); // fixed: repeatable captures
        int created;

        public FxParticles(Transform parent, Material unlitTransparent)
        {
            root = new GameObject("Particles").transform;
            root.SetParent(parent, false);
            material = unlitTransparent;
        }

        public int LiveCount => live.Count;

        public float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

        public Vector3 InSphere(float r)
        {
            Vector3 v;
            do v = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
            while (v.sqrMagnitude > 1f);
            return v * r;
        }

        /// <param name="gravity">Downward acceleration (negative rises, like hot air).</param>
        /// <param name="drag">Fraction of velocity lost per second (0 = none).</param>
        /// <param name="flat">A flat ground mark (scorch, ring) instead of a cube.</param>
        /// <param name="ground">Stops on the ground instead of passing through it (debris).</param>
        public void Emit(Vector3 pos, Vector3 vel, float life, float size0, float size1, Color c0, Color c1,
            float gravity = 0f, float drag = 0f, bool flat = false, bool ground = false)
        {
            P p;
            if (pool.Count > 0) p = pool.Pop();
            else if (created < MaxParticles)
            {
                var go = Prims.Create(PrimitiveType.Cube, root, Vector3.zero, Vector3.one, material, "Particle");
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                p = new P { T = go.transform, R = go.GetComponent<MeshRenderer>() };
                created++;
            }
            else return;

            p.Pos = pos;
            p.Vel = vel;
            p.Age = 0f;
            p.Life = Mathf.Max(0.02f, life);
            p.Size0 = size0;
            p.Size1 = size1;
            p.C0 = c0;
            p.C1 = c1;
            p.Gravity = gravity;
            p.Drag = drag;
            p.Flat = flat;
            p.Ground = ground;
            p.Spin = flat ? Vector3.zero : new Vector3(Range(-200f, 200f), Range(-200f, 200f), Range(-200f, 200f));
            p.T.localRotation = flat ? Quaternion.identity : Quaternion.Euler(Range(0f, 90f), Range(0f, 90f), Range(0f, 90f));
            p.T.gameObject.SetActive(true);
            Apply(p, 0f);
            live.Add(p);
        }

        public void Update(float dt)
        {
            if (dt <= 0f) return;
            for (var i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.T.gameObject.SetActive(false);
                    pool.Push(p);
                    live[i] = live[live.Count - 1];
                    live.RemoveAt(live.Count - 1);
                    continue;
                }

                p.Vel.y -= p.Gravity * dt;
                if (p.Drag > 0f) p.Vel *= Mathf.Max(0f, 1f - p.Drag * dt);
                p.Pos += p.Vel * dt;
                if (p.Ground && p.Pos.y < 0.03f)
                {
                    p.Pos.y = 0.03f;
                    p.Vel = new Vector3(p.Vel.x * 0.3f, Mathf.Abs(p.Vel.y) * 0.25f, p.Vel.z * 0.3f); // a small, dead bounce
                    p.Spin *= 0.3f;
                }

                if (!p.Flat) p.T.localRotation *= Quaternion.Euler(p.Spin * dt);
                Apply(p, p.Age / p.Life);
            }
        }

        void Apply(P p, float t)
        {
            var s = Mathf.Lerp(p.Size0, p.Size1, t);
            p.T.localPosition = p.Pos;
            p.T.localScale = p.Flat ? new Vector3(s, 0.005f, s) : Vector3.one * s;
            block.SetColor("_Color", Color.Lerp(p.C0, p.C1, t));
            p.R.SetPropertyBlock(block);
        }

        public void Clear()
        {
            foreach (var p in live)
            {
                p.T.gameObject.SetActive(false);
                pool.Push(p);
            }

            live.Clear();
        }
    }
}
