using UnityEngine;

namespace TheLastFortress.Editor
{
    /// <summary>
    /// Primitive blockouts following design/README.md silhouettes. Pivot = tile center on the ground;
    /// base extents stay inside the 0.9 x 0.9 tile square.
    /// </summary>
    static class EntityPrefabBuilder
    {
        public sealed class Materials
        {
            public Material Timber, TimberShadow, Limestone, Door, Amber, EnemyBody, EnemyCloth;
        }

        static GameObject Root(string name) => new GameObject(name);

        static void Box(Transform p, Material m, Vector3 pos, Vector3 scale, string name, Vector3? euler = null) =>
            Prims.Create(PrimitiveType.Cube, p, pos, scale, m, name, euler.HasValue ? Quaternion.Euler(euler.Value) : (Quaternion?)null);

        /// <summary>Squat stone mass, corner blocks, stepped pyramidal roof, dark door, torches. Height 1.6.</summary>
        public static GameObject Keep(Materials m)
        {
            var root = Root("Keep");
            var t = root.transform;
            Box(t, m.Limestone, new Vector3(0, 0.5f, 0), new Vector3(0.72f, 1.0f, 0.72f), "Body");
            foreach (var c in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
                Box(t, m.Limestone, new Vector3(c.x * 0.33f, 0.6f, c.y * 0.33f), new Vector3(0.24f, 1.2f, 0.24f), "CornerBlock");
            Box(t, m.TimberShadow, new Vector3(0, 1.08f, 0), new Vector3(0.78f, 0.16f, 0.78f), "RoofBase");
            Box(t, m.Timber, new Vector3(0, 1.25f, 0), new Vector3(0.6f, 0.22f, 0.6f), "RoofMid", new Vector3(0, 45, 0));
            Box(t, m.Timber, new Vector3(0, 1.43f, 0), new Vector3(0.36f, 0.18f, 0.36f), "RoofTop", new Vector3(0, 45, 0));
            Box(t, m.TimberShadow, new Vector3(0, 1.56f, 0), new Vector3(0.12f, 0.1f, 0.12f), "RoofCap", new Vector3(0, 45, 0));
            Box(t, m.Door, new Vector3(0, 0.2f, -0.365f), new Vector3(0.2f, 0.36f, 0.04f), "Door");
            Box(t, m.Amber, new Vector3(0.36f, 1.27f, -0.36f), new Vector3(0.07f, 0.1f, 0.07f), "Torch");
            Box(t, m.Amber, new Vector3(-0.36f, 1.27f, 0.36f), new Vector3(0.07f, 0.1f, 0.07f), "Torch");
            return root;
        }

        /// <summary>Low repeated pointed stakes on two rails. Height 0.8.</summary>
        public static GameObject Wall(Materials m)
        {
            var root = Root("Wall");
            var t = root.transform;
            for (var i = 0; i < 4; i++)
            {
                var x = -0.3f + i * 0.2f;
                var h = i % 2 == 0 ? 0.62f : 0.56f;
                Box(t, i % 2 == 0 ? m.Timber : m.TimberShadow, new Vector3(x, h / 2f, 0), new Vector3(0.15f, h, 0.15f), "Stake");
                Box(t, m.Timber, new Vector3(x, h + 0.06f, 0), new Vector3(0.11f, 0.11f, 0.11f), "Point", new Vector3(45, 0, 45));
            }

            Box(t, m.TimberShadow, new Vector3(0, 0.2f, 0.09f), new Vector3(0.86f, 0.07f, 0.05f), "RailLow");
            Box(t, m.TimberShadow, new Vector3(0, 0.42f, 0.09f), new Vector3(0.86f, 0.07f, 0.05f), "RailHigh");
            return root;
        }

        /// <summary>Tall narrow open timber frame with platform, roof, braces. Height 2.0.</summary>
        public static GameObject Tower(Materials m)
        {
            var root = Root("ArcherTower");
            var t = root.transform;
            Box(t, m.Limestone, new Vector3(0, 0.06f, 0), new Vector3(0.74f, 0.12f, 0.74f), "Foundation");
            foreach (var c in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
                Box(t, m.Timber, new Vector3(c.x * 0.27f, 0.8f, c.y * 0.27f), new Vector3(0.1f, 1.5f, 0.1f), "Post");
            Box(t, m.TimberShadow, new Vector3(0, 0.75f, -0.27f), new Vector3(0.07f, 0.85f, 0.05f), "Brace", new Vector3(0, 0, 38));
            Box(t, m.TimberShadow, new Vector3(-0.27f, 0.75f, 0), new Vector3(0.05f, 0.85f, 0.07f), "Brace", new Vector3(38, 0, 0));
            Box(t, m.Timber, new Vector3(0, 1.52f, 0), new Vector3(0.78f, 0.1f, 0.78f), "Platform");
            Box(t, m.TimberShadow, new Vector3(0, 1.63f, 0), new Vector3(0.7f, 0.12f, 0.7f), "Railing");
            Box(t, m.Timber, new Vector3(0, 1.84f, 0), new Vector3(0.52f, 0.18f, 0.52f), "Roof", new Vector3(0, 45, 0));
            Box(t, m.TimberShadow, new Vector3(0, 1.96f, 0), new Vector3(0.2f, 0.08f, 0.2f), "RoofCap", new Vector3(0, 45, 0));
            Box(t, m.Amber, new Vector3(0.3f, 1.66f, -0.3f), new Vector3(0.07f, 0.1f, 0.07f), "Torch");
            return root;
        }

        /// <summary>Small hooded mass with one blunt weapon arm. Height 0.55; faces +Z.</summary>
        public static GameObject Grunt(Materials m)
        {
            var root = Root("Grunt");
            var t = root.transform;
            Prims.Create(PrimitiveType.Capsule, t, new Vector3(0, 0.22f, 0), new Vector3(0.3f, 0.2f, 0.26f), m.EnemyBody, "Body");
            Box(t, m.EnemyCloth, new Vector3(0, 0.2f, 0.1f), new Vector3(0.24f, 0.22f, 0.08f), "Tabard");
            Box(t, m.EnemyCloth, new Vector3(0, 0.44f, 0), new Vector3(0.2f, 0.18f, 0.2f), "Hood", new Vector3(0, 45, 0));
            Box(t, m.EnemyBody, new Vector3(0, 0.53f, -0.02f), new Vector3(0.1f, 0.06f, 0.1f), "HoodTip", new Vector3(0, 45, 0));
            Box(t, m.EnemyBody, new Vector3(-0.08f, 0.04f, 0), new Vector3(0.07f, 0.08f, 0.09f), "Leg");
            Box(t, m.EnemyBody, new Vector3(0.08f, 0.04f, 0), new Vector3(0.07f, 0.08f, 0.09f), "Leg");
            Box(t, m.EnemyBody, new Vector3(0.18f, 0.28f, 0.1f), new Vector3(0.05f, 0.28f, 0.05f), "WeaponArm", new Vector3(50, 0, 0));
            Box(t, m.TimberShadow, new Vector3(0.18f, 0.38f, 0.24f), new Vector3(0.1f, 0.1f, 0.14f), "Club", new Vector3(50, 0, 0));
            return root;
        }
    }
}
