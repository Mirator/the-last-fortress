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
            public Material Timber, TimberShadow, Limestone, Door, Amber, EnemyBody, EnemyCloth, Crop, RunnerCloth, BruteCloth, GolemStone, GolemGlow;
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

        /// <summary>
        /// Connected palisade piece in canonical facing (north = +Z): a central post cluster plus one stake-and-rail arm per
        /// connected side of the shape's canonical mask. Arms end exactly at the tile edge so neighbors join continuously;
        /// the piece still occupies one tile and adds no colliders.
        /// </summary>
        public static GameObject WallPiece(Materials m, WallShape shape)
        {
            var root = Root("Wall" + shape);
            var t = root.transform;
            var mask = WallTopology.CanonicalMask(shape);
            if (shape == WallShape.Isolated)
            {
                for (var i = 0; i < 4; i++)
                {
                    var p = new Vector3(i % 2 == 0 ? -0.13f : 0.13f, 0, i < 2 ? -0.13f : 0.13f);
                    Stake(t, m, p, i % 3 == 0 ? 0.64f : 0.56f, i % 2 == 0);
                }

                Box(t, m.TimberShadow, new Vector3(0, 0.22f, -0.21f), new Vector3(0.44f, 0.07f, 0.05f), "RailS");
                Box(t, m.TimberShadow, new Vector3(0, 0.22f, 0.21f), new Vector3(0.44f, 0.07f, 0.05f), "RailN");
                return root;
            }

            Stake(t, m, new Vector3(-0.07f, 0, 0), 0.66f, true);
            Stake(t, m, new Vector3(0.07f, 0, 0), 0.6f, false);
            var dirs = new[] { (WallTopology.North, Vector3.forward), (WallTopology.East, Vector3.right), (WallTopology.South, Vector3.back), (WallTopology.West, Vector3.left) };
            foreach (var (bit, dir) in dirs)
            {
                if ((mask & bit) == 0) continue;
                Stake(t, m, dir * 0.22f, 0.58f, false);
                Stake(t, m, dir * 0.4f, 0.63f, true);
                var railScale = dir.x != 0 ? new Vector3(0.5f, 0.07f, 0.05f) : new Vector3(0.05f, 0.07f, 0.5f);
                var side = dir.x != 0 ? new Vector3(0, 0, 0.09f) : new Vector3(0.09f, 0, 0);
                Box(t, m.TimberShadow, dir * 0.25f + side + new Vector3(0, 0.2f, 0), railScale, "RailLow");
                Box(t, m.TimberShadow, dir * 0.25f + side + new Vector3(0, 0.42f, 0), railScale, "RailHigh");
            }

            return root;
        }

        static void Stake(Transform t, Materials m, Vector3 p, float h, bool light)
        {
            Box(t, light ? m.Timber : m.TimberShadow, p + new Vector3(0, h / 2f, 0), new Vector3(0.14f, h, 0.14f), "Stake");
            Box(t, m.Timber, p + new Vector3(0, h + 0.05f, 0), new Vector3(0.1f, 0.1f, 0.1f), "Point", new Vector3(45, 0, 45));
        }

        /// <summary>Compact cottage: pale stone base, steep timber roof, door. Height ~1.1, inside 0.9 x 0.9.</summary>
        public static GameObject House(Materials m)
        {
            var root = Root("House");
            var t = root.transform;
            Box(t, m.Limestone, new Vector3(0, 0.3f, 0), new Vector3(0.72f, 0.6f, 0.62f), "Walls");
            Box(t, m.Timber, new Vector3(0, 0.78f, 0), new Vector3(0.56f, 0.56f, 0.8f), "Roof", new Vector3(0, 0, 45));
            Box(t, m.TimberShadow, new Vector3(0, 1.04f, 0), new Vector3(0.1f, 0.06f, 0.84f), "RoofRidge");
            Box(t, m.Door, new Vector3(0, 0.17f, -0.315f), new Vector3(0.18f, 0.32f, 0.03f), "Door");
            Box(t, m.Amber, new Vector3(0.2f, 0.4f, -0.315f), new Vector3(0.12f, 0.12f, 0.02f), "Window");
            Box(t, m.TimberShadow, new Vector3(0.22f, 0.95f, 0.18f), new Vector3(0.1f, 0.28f, 0.1f), "Chimney");
            return root;
        }

        /// <summary>Low 2 x 2 crop plot centered on its footprint (pivot = footprint center). Height ~0.35.</summary>
        public static GameObject Farm(Materials m)
        {
            var root = Root("Farm");
            var t = root.transform;
            Box(t, m.TimberShadow, new Vector3(0, 0.04f, 0), new Vector3(1.8f, 0.08f, 1.8f), "Soil");
            for (var i = 0; i < 5; i++)
                Box(t, m.Crop, new Vector3(-0.68f + i * 0.34f, 0.16f, 0.05f), new Vector3(0.18f, 0.18f, 1.45f), "CropRow");
            // low fence on two sides keeps the plot readable as one building
            Box(t, m.Timber, new Vector3(0, 0.14f, -0.86f), new Vector3(1.8f, 0.06f, 0.05f), "FenceSouth");
            Box(t, m.Timber, new Vector3(-0.86f, 0.14f, 0), new Vector3(0.05f, 0.06f, 1.8f), "FenceWest");
            Box(t, m.Limestone, new Vector3(0.62f, 0.2f, -0.62f), new Vector3(0.36f, 0.32f, 0.36f), "Shed");
            Box(t, m.Timber, new Vector3(0.62f, 0.42f, -0.62f), new Vector3(0.3f, 0.3f, 0.42f), "ShedRoof", new Vector3(0, 0, 45));
            return root;
        }

        /// <summary>Flat walkable plate with short spikes; never taller than 0.15 so it does not read as a blocker.</summary>
        public static GameObject SpikeTrap(Materials m)
        {
            var root = Root("SpikeTrap");
            var t = root.transform;
            Box(t, m.TimberShadow, new Vector3(0, 0.02f, 0), new Vector3(0.78f, 0.04f, 0.78f), "Plate");
            for (var x = 0; x < 3; x++)
            for (var z = 0; z < 3; z++)
                Box(t, m.Limestone, new Vector3(-0.24f + x * 0.24f, 0.08f, -0.24f + z * 0.24f), new Vector3(0.07f, 0.12f, 0.07f), "Spike", new Vector3(0, 45, 0));
            return root;
        }

        /// <summary>Lean, forward-tilted sprinter with an ochre sash and no weapon bulk. Height ~0.6; faces +Z.</summary>
        public static GameObject Runner(Materials m)
        {
            var root = Root("Runner");
            var lean = new GameObject("Lean").transform;
            lean.SetParent(root.transform, false);
            lean.localRotation = Quaternion.Euler(22, 0, 0);
            Prims.Create(PrimitiveType.Capsule, lean, new Vector3(0, 0.27f, 0), new Vector3(0.18f, 0.22f, 0.16f), m.EnemyBody, "Body");
            Box(lean, m.RunnerCloth, new Vector3(0, 0.3f, 0), new Vector3(0.2f, 0.06f, 0.18f), "Sash", new Vector3(0, 0, 25));
            Box(lean, m.RunnerCloth, new Vector3(0, 0.52f, 0), new Vector3(0.13f, 0.13f, 0.13f), "Hood", new Vector3(0, 45, 0));
            Box(root.transform, m.EnemyBody, new Vector3(-0.06f, 0.06f, 0.06f), new Vector3(0.05f, 0.12f, 0.06f), "LegFront");
            Box(root.transform, m.EnemyBody, new Vector3(0.06f, 0.06f, -0.07f), new Vector3(0.05f, 0.12f, 0.06f), "LegBack");
            Box(lean, m.TimberShadow, new Vector3(0.12f, 0.3f, 0.1f), new Vector3(0.04f, 0.04f, 0.22f), "Dagger");
            return root;
        }

        /// <summary>Broad hunched brute with a large club. Height ~0.85, width ~0.6; faces +Z.</summary>
        public static GameObject Brute(Materials m)
        {
            var root = Root("Brute");
            var t = root.transform;
            Prims.Create(PrimitiveType.Capsule, t, new Vector3(0, 0.36f, 0), new Vector3(0.5f, 0.32f, 0.42f), m.EnemyBody, "Body");
            Box(t, m.BruteCloth, new Vector3(0, 0.36f, 0.14f), new Vector3(0.44f, 0.36f, 0.12f), "Tabard");
            Box(t, m.BruteCloth, new Vector3(0, 0.74f, 0.04f), new Vector3(0.26f, 0.2f, 0.26f), "Head", new Vector3(0, 45, 0));
            Box(t, m.EnemyBody, new Vector3(-0.14f, 0.06f, 0), new Vector3(0.12f, 0.12f, 0.14f), "Leg");
            Box(t, m.EnemyBody, new Vector3(0.14f, 0.06f, 0), new Vector3(0.12f, 0.12f, 0.14f), "Leg");
            Box(t, m.EnemyBody, new Vector3(0.3f, 0.45f, 0.12f), new Vector3(0.1f, 0.38f, 0.1f), "Arm", new Vector3(55, 0, 0));
            Box(t, m.TimberShadow, new Vector3(0.3f, 0.66f, 0.38f), new Vector3(0.2f, 0.2f, 0.34f), "Club", new Vector3(55, 0, 0));
            return root;
        }

        /// <summary>Tall stack of heavy stone masses with glowing eyes. Height ~1.5, inside 0.9 x 0.9; faces +Z.</summary>
        public static GameObject SiegeGolem(Materials m)
        {
            var root = Root("SiegeGolem");
            var t = root.transform;
            Box(t, m.GolemStone, new Vector3(-0.2f, 0.18f, 0), new Vector3(0.24f, 0.36f, 0.3f), "LegL");
            Box(t, m.GolemStone, new Vector3(0.2f, 0.18f, 0), new Vector3(0.24f, 0.36f, 0.3f), "LegR");
            Box(t, m.GolemStone, new Vector3(0, 0.66f, 0), new Vector3(0.72f, 0.6f, 0.5f), "Torso", new Vector3(0, 0, 4));
            Box(t, m.GolemStone, new Vector3(0, 1.1f, 0.02f), new Vector3(0.36f, 0.3f, 0.34f), "Head", new Vector3(0, 8, 0));
            Box(t, m.GolemGlow, new Vector3(-0.08f, 1.12f, 0.19f), new Vector3(0.07f, 0.05f, 0.02f), "Eye");
            Box(t, m.GolemGlow, new Vector3(0.08f, 1.12f, 0.19f), new Vector3(0.07f, 0.05f, 0.02f), "Eye");
            Box(t, m.GolemStone, new Vector3(-0.44f, 0.62f, 0.08f), new Vector3(0.2f, 0.56f, 0.22f), "ArmL", new Vector3(20, 0, 0));
            Box(t, m.GolemStone, new Vector3(0.44f, 0.62f, 0.08f), new Vector3(0.2f, 0.56f, 0.22f), "ArmR", new Vector3(20, 0, 0));
            Box(t, m.GolemGlow, new Vector3(0, 0.72f, 0.26f), new Vector3(0.18f, 0.18f, 0.02f), "Core", new Vector3(0, 0, 45));
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
