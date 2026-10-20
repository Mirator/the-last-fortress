using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// What the hero view sees past the board (specs 002 §17): a gradient sky dome blended with the day/night amount, with
    /// a warm glow around the sun by day and a cool one around the moon at night, low-poly clouds on the horizon, stars at
    /// night, and distance haze matching the edge mist (plan 016). The dome, clouds and stars sit beyond the strategic
    /// camera's far clip and are only active while the hero view renders; fog is switched on only for that view.
    /// Presentation only.
    /// </summary>
    public sealed class HeroSky : MonoBehaviour
    {
        const float DomeRadius = 190f;
        const int Rings = 24, Segments = 64;

        static readonly Color DayZenith = new Color(0.3f, 0.5f, 0.8f);
        static readonly Color NightZenith = new Color(0.02f, 0.035f, 0.085f);

        /// <summary>Horizon colours; the edge mist (plan 016) uses the same, so the hero view's horizon and the mist are one.</summary>
        public static readonly Color DayHorizon = new Color(0.8f, 0.86f, 0.88f), NightHorizon = new Color(0.12f, 0.16f, 0.26f);
        static readonly Color SunGlow = new Color(1f, 0.9f, 0.7f), MoonGlow = new Color(0.4f, 0.5f, 0.7f);

        Mesh dome, clouds, stars;
        Color[] colors, cloudColors, starColors;
        Vector3[] directions;
        float[] cloudShade;
        float shownBlend = -1f;
        Vector3 shownSun;
        bool fogOn;
        GameObject cloudGo, starGo, domeGo;

        public Color Horizon { get; private set; } = DayHorizon;

        /// <summary>The scene's directional light (sun by day, moon by night); its direction places the glow.</summary>
        public Light Sun { get; set; }

        public void Initialize(Material vertexColorUnlit)
        {
            domeGo = new GameObject("SkyDome");
            domeGo.transform.SetParent(transform, false);
            domeGo.transform.localPosition = new Vector3(10f, 0f, 10f);
            dome = BuildDome(out directions);
            colors = new Color[directions.Length];
            domeGo.AddComponent<MeshFilter>().sharedMesh = dome;
            var background = new Material(vertexColorUnlit) { renderQueue = 1000 }; // background: drawn before everything else
            background.color = Color.white;
            Renderer(domeGo, background);

            cloudGo = new GameObject("SkyClouds");
            cloudGo.transform.SetParent(domeGo.transform, false);
            clouds = BuildClouds(out cloudShade);
            cloudColors = new Color[cloudShade.Length];
            cloudGo.AddComponent<MeshFilter>().sharedMesh = clouds;
            Renderer(cloudGo, new Material(vertexColorUnlit) { renderQueue = 1001, color = Color.white });

            starGo = new GameObject("SkyStars");
            starGo.transform.SetParent(domeGo.transform, false);
            stars = BuildStars();
            starColors = new Color[stars.vertexCount];
            starGo.AddComponent<MeshFilter>().sharedMesh = stars;
            Renderer(starGo, new Material(vertexColorUnlit) { renderQueue = 1001, color = Color.white });
            domeGo.SetActive(false);

            Apply(0f);
        }

        static void Renderer(GameObject go, Material m)
        {
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>Upper hemisphere (slightly below the horizon) facing inward; per vertex the unit direction from the centre.</summary>
        static Mesh BuildDome(out Vector3[] directions)
        {
            var verts = new Vector3[(Rings + 1) * (Segments + 1)];
            directions = new Vector3[verts.Length];
            for (var i = 0; i <= Rings; i++)
            {
                // Rings bunch toward the horizon, where the gradient changes fastest.
                var e = Mathf.Lerp(-8f, 90f, Mathf.Pow(i / (float)Rings, 1.6f)) * Mathf.Deg2Rad;
                for (var j = 0; j <= Segments; j++)
                {
                    var a = j / (float)Segments * Mathf.PI * 2f;
                    var k = i * (Segments + 1) + j;
                    directions[k] = new Vector3(Mathf.Cos(e) * Mathf.Sin(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Cos(a));
                    verts[k] = directions[k] * DomeRadius;
                }
            }

            var tris = new int[Rings * Segments * 6];
            var t = 0;
            for (var i = 0; i < Rings; i++)
            for (var j = 0; j < Segments; j++)
            {
                int a = i * (Segments + 1) + j, b = a + 1, c = a + Segments + 1, d = c + 1;
                // Wound so the faces point inward (seen from the board).
                tris[t++] = a; tris[t++] = b; tris[t++] = c;
                tris[t++] = b; tris[t++] = d; tris[t++] = c;
            }

            var mesh = new Mesh { name = "HeroSkyDome", vertices = verts, triangles = tris };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * DomeRadius * 2f);
            return mesh;
        }

        /// <summary>Low-poly cumulus banks low over the horizon: flattened faceted puffs, lit from above (shade per vertex).</summary>
        static Mesh BuildClouds(out float[] shade)
        {
            var rng = new System.Random(19);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var shadeList = new List<float>();
            for (var c = 0; c < 11; c++)
            {
                var yaw = (c + (float)rng.NextDouble() * 0.6f) / 11f * Mathf.PI * 2f;
                var elevation = (3f + 7f * (float)rng.NextDouble()) * Mathf.Deg2Rad;
                var dist = DomeRadius * 0.86f;
                var centre = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Cos(yaw) * Mathf.Cos(elevation)) * dist;
                var along = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
                var puffs = 4 + rng.Next(4);
                var width = 8f + 10f * (float)rng.NextDouble();
                for (var p = 0; p < puffs; p++)
                {
                    var t = puffs == 1 ? 0f : p / (float)(puffs - 1) - 0.5f;
                    var r = width * (0.32f + 0.22f * (1f - Mathf.Abs(t) * 2f)) * (0.8f + 0.4f * (float)rng.NextDouble());
                    var at = centre + along * t * width * 1.6f + Vector3.up * r * 0.25f * (1f - Mathf.Abs(t) * 2f);
                    Puff(verts, tris, shadeList, at, new Vector3(r, r * 0.55f, r * 0.8f), rng);
                }
            }

            shade = shadeList.ToArray();
            var mesh = new Mesh { name = "HeroSkyClouds" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * DomeRadius * 2f);
            return mesh;
        }

        /// <summary>An octahedron-based faceted puff (flat bottom), split per face so each facet keeps one shade.</summary>
        static void Puff(List<Vector3> verts, List<int> tris, List<float> shade, Vector3 c, Vector3 r, System.Random rng)
        {
            var ring = 7;
            var pts = new Vector3[ring * 2 + 1];
            for (var i = 0; i < ring; i++)
            {
                var a = i / (float)ring * Mathf.PI * 2f;
                pts[i] = c + new Vector3(Mathf.Cos(a) * r.x, -r.y * 0.15f, Mathf.Sin(a) * r.z) * (0.9f + 0.2f * (float)rng.NextDouble());
                pts[ring + i] = c + new Vector3(Mathf.Cos(a + 0.45f) * r.x * 0.62f, r.y * 0.6f, Mathf.Sin(a + 0.45f) * r.z * 0.62f);
            }

            pts[ring * 2] = c + Vector3.up * r.y;
            void Face(Vector3 a, Vector3 b, Vector3 d)
            {
                var n = Vector3.Cross(b - a, d - a).normalized;
                var k = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(d);
                // Inward-facing camera: the dome centre is inside, so both windings are needed; draw the side facing in.
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
                var s = Mathf.Clamp01(0.55f + 0.45f * Mathf.Abs(n.y));
                shade.Add(s); shade.Add(s); shade.Add(s);
            }

            for (var i = 0; i < ring; i++)
            {
                int j = (i + 1) % ring;
                Face(pts[i], pts[j], pts[ring + i]);
                Face(pts[j], pts[ring + j], pts[ring + i]);
                Face(pts[ring + i], pts[ring + j], pts[ring * 2]);
            }
        }

        /// <summary>A few hundred tiny quads across the upper sky; their alpha follows the night amount.</summary>
        static Mesh BuildStars()
        {
            var rng = new System.Random(23);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (var s = 0; s < 320; s++)
            {
                var a = (float)rng.NextDouble() * Mathf.PI * 2f;
                var e = Mathf.Asin(Mathf.Lerp(0.12f, 1f, (float)rng.NextDouble()));
                var d = new Vector3(Mathf.Cos(e) * Mathf.Sin(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Cos(a));
                var c = d * DomeRadius * 0.97f;
                var right = Vector3.Cross(d, Vector3.up).normalized;
                if (right.sqrMagnitude < 0.01f) right = Vector3.right;
                var up = Vector3.Cross(right, d);
                var size = 0.25f + 0.35f * (float)rng.NextDouble();
                var k = verts.Count;
                verts.Add(c - right * size - up * size); verts.Add(c + right * size - up * size);
                verts.Add(c + right * size + up * size); verts.Add(c - right * size + up * size);
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
                tris.Add(k); tris.Add(k + 3); tris.Add(k + 2);
            }

            var mesh = new Mesh { name = "HeroSkyStars" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * DomeRadius * 2f);
            return mesh;
        }

        /// <summary>Per frame: day/night blend and whether the hero view renders (fog, clouds and stars belong to that view only).</summary>
        public void Sync(float nightBlend, bool heroView, Camera heroCamera)
        {
            var sunDir = Sun != null ? -Sun.transform.forward : Vector3.up;
            if (!Mathf.Approximately(nightBlend, shownBlend) || Vector3.Dot(sunDir, shownSun) < 0.9999f) Apply(nightBlend);
            if (heroCamera != null) heroCamera.backgroundColor = Horizon;
            if (domeGo != null && domeGo.activeSelf != heroView) domeGo.SetActive(heroView);
            if (heroView == fogOn) return;
            fogOn = heroView;
            RenderSettings.fog = heroView;
            if (!heroView) return;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 18f; // light haze across the board, full on the horizon
            RenderSettings.fogEndDistance = 170f;
            RenderSettings.fogColor = Horizon;
        }

        void Apply(float blend)
        {
            shownBlend = blend;
            var sunDir = Sun != null ? -Sun.transform.forward : Vector3.up;
            shownSun = sunDir;
            var zenith = Color.Lerp(DayZenith, NightZenith, blend);
            Horizon = Color.Lerp(DayHorizon, NightHorizon, blend);
            var glow = Color.Lerp(SunGlow, MoonGlow, blend);
            var glowStrength = Mathf.Lerp(0.55f, 0.35f, blend);
            for (var i = 0; i < colors.Length; i++)
            {
                var d = directions[i];
                var height = Mathf.Clamp01(d.y);
                var c = Color.Lerp(Horizon, zenith, Mathf.Pow(height, 0.55f));
                // A broad halo plus a tight core toward the light, strongest near the horizon (haze around a low sun).
                var toward = Mathf.Max(0f, Vector3.Dot(d, sunDir));
                var halo = Mathf.Pow(toward, 6f) * 0.6f + Mathf.Pow(toward, 64f) * 0.9f;
                c = Color.Lerp(c, glow, Mathf.Clamp01(halo * glowStrength * (1.2f - height * 0.5f)));
                colors[i] = c;
            }

            dome.colors = colors;
            // Clouds: lit tops, shaded bellies; warm-white by day, slate by night.
            Color top = Color.Lerp(new Color(1f, 0.99f, 0.96f), new Color(0.13f, 0.16f, 0.24f), blend);
            Color belly = Color.Lerp(new Color(0.72f, 0.77f, 0.84f), new Color(0.06f, 0.075f, 0.12f), blend);
            for (var i = 0; i < cloudColors.Length; i++) cloudColors[i] = Color.Lerp(belly, top, cloudShade[i]);
            clouds.colors = cloudColors;
            var starAlpha = Mathf.Clamp01((blend - 0.4f) / 0.6f);
            for (var i = 0; i < starColors.Length; i++) starColors[i] = new Color(0.9f, 0.93f, 1f, starAlpha * (0.5f + 0.5f * ((i / 4 * 37) % 11) / 10f));
            stars.colors = starColors;
            if (starGo != null && starGo.activeSelf != starAlpha > 0f) starGo.SetActive(starAlpha > 0f);
            if (fogOn) RenderSettings.fogColor = Horizon;
        }

        void OnDisable()
        {
            if (fogOn) RenderSettings.fog = false;
            fogOn = false;
        }
    }
}
