using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastFortress
{
    /// <summary>
    /// Spec 011: in the hero view, every building, tree or boulder mesh whose box crosses the line from the camera to the
    /// hero's chest or head, or holds the camera's near plane, eases to see-through (<see cref="FadedAlpha"/>) and back.
    /// Faded meshes use run-time copies of the fade material carrying the source's base map and colour; the originals
    /// return at full opacity and whenever the hero camera stops rendering. Presentation only.
    /// </summary>
    public sealed class HeroOccluderFade
    {
        /// <summary>Opacity of a faded mesh and the time to fade in or out, real time [P].</summary>
        public const float FadedAlpha = 0.3f, FadeSeconds = 0.15f;

        /// <summary>Heights above the hero's feet that must stay visible: chest and head.</summary>
        static readonly float[] TargetHeights = { 0.15f, 0.3f };

        static readonly int AlphaId = Shader.PropertyToID("_Alpha"), BaseMapId = Shader.PropertyToID("_BaseMap"), BaseColorId = Shader.PropertyToID("_BaseColor");

        sealed class Faded
        {
            public Material[] Originals;
            public ShadowCastingMode Shadows;
            public float Alpha = 1f;
        }

        readonly Material template;
        readonly List<Renderer> candidates = new List<Renderer>();
        readonly Dictionary<Renderer, Faded> faded = new Dictionary<Renderer, Faded>();
        readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        readonly List<Renderer> scratch = new List<Renderer>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        int version = -1;
        WorldView source;

        public HeroOccluderFade(Material template) => this.template = template;

        /// <summary>Meshes drawn see-through (or easing) this frame.</summary>
        public int FadedCount => faded.Count;

        /// <summary>
        /// Updates the fade for this frame. With <paramref name="shown"/> false (strategic view) everything is restored at once;
        /// while <paramref name="leaving"/> (easing out to the strategic view) everything eases back, so nothing pops at the swap.
        /// </summary>
        public void Sync(WorldView world, Camera cam, Vector3 hero, bool shown, float dt, bool leaving = false)
        {
            if (template == null) return;
            if (!shown || world == null || cam == null)
            {
                RestoreAll();
                return;
            }

            if (world != source || world.CameraBlockerVersion != version)
            {
                RestoreAll(); // a rebuilt or removed mesh must not keep a stale copy
                source = world;
                version = world.CameraBlockerVersion;
                world.CollectFadeCandidates(candidates);
            }

            var eye = cam.transform.position;
            var pad = HeroCameraObstacles.NearPadding(cam);
            var step = dt > 0f ? dt / FadeSeconds : 1f;
            foreach (var r in candidates)
            {
                if (r == null) continue;
                var hides = !leaving && Hides(r.bounds, eye, hero, pad);
                faded.TryGetValue(r, out var state);
                if (!hides && state == null) continue;
                if (state == null)
                {
                    state = new Faded { Originals = r.sharedMaterials, Shadows = r.shadowCastingMode };
                    faded[r] = state;
                    r.sharedMaterials = FadeCopies(state.Originals);
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }

                state.Alpha = Mathf.MoveTowards(state.Alpha, hides ? FadedAlpha : 1f, step * (1f - FadedAlpha));
                r.GetPropertyBlock(block);
                block.SetFloat(AlphaId, state.Alpha);
                r.SetPropertyBlock(block);
            }

            scratch.Clear();
            foreach (var pair in faded)
                if (pair.Key == null || pair.Value.Alpha >= 1f) scratch.Add(pair.Key);
            foreach (var r in scratch) Restore(r);
        }

        static bool Hides(Bounds b, Vector3 eye, Vector3 hero, float pad)
        {
            var near = b;
            near.Expand(pad * 2f);
            if (near.Contains(eye)) return true;
            foreach (var h in TargetHeights)
            {
                var target = hero + new Vector3(0f, h, 0f);
                var d = target - eye;
                var length = d.magnitude;
                if (length < 1e-4f) continue;
                if (b.IntersectRay(new Ray(eye, d / length), out var hit) && hit < length) return true;
            }

            return false;
        }

        Material[] FadeCopies(Material[] originals)
        {
            var result = new Material[originals.Length];
            for (var i = 0; i < originals.Length; i++)
            {
                var m = originals[i];
                if (m == null) continue;
                if (!copies.TryGetValue(m, out var copy))
                {
                    copy = new Material(template) { name = m.name + " (hero fade)" };
                    if (m.HasProperty(BaseMapId)) copy.SetTexture(BaseMapId, m.GetTexture(BaseMapId));
                    if (m.HasProperty(BaseColorId)) copy.SetColor(BaseColorId, m.GetColor(BaseColorId));
                    copies[m] = copy;
                }

                result[i] = copy;
            }

            return result;
        }

        void Restore(Renderer r)
        {
            if (!faded.TryGetValue(r, out var state)) return;
            faded.Remove(r);
            if (r == null) return;
            r.sharedMaterials = state.Originals; // _Alpha may stay in the block: the original materials ignore it
            r.shadowCastingMode = state.Shadows;
        }

        void RestoreAll()
        {
            if (faded.Count == 0) return;
            scratch.Clear();
            scratch.AddRange(faded.Keys);
            foreach (var r in scratch) Restore(r);
        }
    }
}
