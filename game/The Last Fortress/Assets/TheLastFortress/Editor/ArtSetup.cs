using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TheLastFortress.Editor
{
    /// <summary>
    /// Plan 011 art slice: imports Art/Textures and Art/Models (exported by Art/Source~/textures.py and models.py), creates
    /// the authored materials, prefab variants, post-processing profiles and the ArtDirection asset, and assigns them to the
    /// prototype, MVP and release content. Ownership is "authored": materials, profiles and the ArtDirection asset are
    /// created only when missing and never rewritten, so an artist's edits survive reruns; prefab variants are rebuilt
    /// from the models. The generated blockout setups never overwrite a content reference that points outside their own
    /// generated prefabs (see <see cref="PrototypeSetup.Generated"/>), so assigned art survives their reruns too.
    /// Batch: -executeMethod TheLastFortress.Editor.ArtSetup.Run
    /// </summary>
    public static class ArtSetup
    {
        public const string Root = PrototypeSetup.Root + "/Art";
        const string Textures = Root + "/Textures", Models = Root + "/Models", Materials = Root + "/Materials", Prefabs = Root + "/Prefabs", Post = Root + "/Post";
        public const string DirectionPath = Root + "/ArtDirection.asset";

        static readonly string[] WallShapes = { "Isolated", "End", "Straight", "Corner", "T", "Cross" };

        [MenuItem("The Last Fortress/Art/Import and Assign Art Slice")]
        public static void Run()
        {
            foreach (var f in new[] { Materials, Prefabs, Post }) PrototypeSetup.EnsureFolder(f);
            AssetDatabase.Refresh();
            ConfigureTextures();
            var mats = CreateMaterials();
            ConfigureModels(mats);
            var keep = Variant("ArtKeep");
            var tower = Variant("ArtArcherTower");
            var grunt = Variant("ArtGrunt", AddRig);
            var enemy = new Dictionary<string, GameObject>();
            foreach (var n in new[] { "Runner", "Brute", "RaiderArcher", "Bomber", "SiegeCommander", "SiegeGolem" }) enemy[n] = Variant("Art" + n, AddRig);
            var house = Variant("ArtHouse");
            var farm = Variant("ArtFarm");
            var trap = Variant("ArtSpikeTrap", go =>
            {
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off; // a floor plate
            });
            var cannon = Variant("ArtCannon");
            var watchtower = Variant("ArtWatchtower");
            var repair = Variant("ArtRepairStation");
            var gate = Variant("ArtGate");
            var walls = new GameObject[6];
            for (var s = 0; s < 6; s++) walls[s] = Variant("ArtWall" + WallShapes[s]);
            var env = new Dictionary<string, GameObject>();
            foreach (var n in new[] { "ArtPine", "ArtBroadleaf", "ArtBush", "ArtRockLarge", "ArtRockSmall", "ArtReeds", "ArtTuft", "ArtFordStone", "ArtFarIslandA", "ArtFarIslandB" })
                env[n] = Variant(n, go =>
                {
                    // Small props cast no shadow (cheaper, and no dark blots that could read as holes or obstacles).
                    if (n == "ArtTuft" || n == "ArtFordStone" || n == "ArtRockSmall" || n == "ArtReeds")
                        foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                });
            var direction = CreateDirection(mats, env);
            if (direction.gate == null) direction.gate = gate;

            foreach (var path in new[] { PrototypeSetup.ContentPath, MvpSetup.ContentPath, ReleaseSetup.ContentPath })
            {
                var content = AssetDatabase.LoadAssetAtPath<PrototypeContent>(path);
                if (content == null) continue;
                content.keepPrefab = keep;
                content.towerPrefab = tower;
                content.gruntPrefab = grunt;
                content.wallPrefab = walls[(int)WallShape.Straight];
                if (content.wallShapePrefabs != null && content.wallShapePrefabs.Length == 6) content.wallShapePrefabs = (GameObject[])walls.Clone();
                content.art = direction;
                content.housePrefab = house;
                content.farmPrefab = farm;
                content.trapPrefab = trap;
                content.runnerPrefab = enemy["Runner"];
                content.brutePrefab = enemy["Brute"];
                content.golemPrefab = enemy["SiegeGolem"];
                content.cannonPrefab = cannon;
                content.watchtowerPrefab = watchtower;
                content.repairStationPrefab = repair;
                content.raiderArcherPrefab = enemy["RaiderArcher"];
                content.bomberPrefab = enemy["Bomber"];
                content.commanderPrefab = enemy["SiegeCommander"];
                EditorUtility.SetDirty(content);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Art slice imported and assigned (Keep, Archer Tower, Grunt, six Wall pieces, board look, post profiles).");
        }

        // ------------------------------------------------------------ textures and materials

        static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Textures }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var normal = path.EndsWith("_normal.png");
                var changed = importer.textureType != (normal ? TextureImporterType.NormalMap : TextureImporterType.Default) || importer.anisoLevel != 4;
                if (!changed) continue;
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !normal;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        sealed class Spec
        {
            public string Tex;
            public Color Tint = Color.white;
            public float Tiling = 2f, Smoothness = 0.12f, Metallic;
            public Color? Emission;
            public bool NormalOnly;
        }

        static Dictionary<string, Material> CreateMaterials()
        {
            Color C(int hex) => Palette.Hex(hex);
            var specs = new Dictionary<string, Spec>
            {
                ["ArtStone"] = new Spec { Tex = "stone", Tint = new Color(0.98f, 0.95f, 0.9f), Tiling = 2f },
                ["ArtStoneDark"] = new Spec { Tex = "stone", Tint = new Color(0.7f, 0.67f, 0.62f), Tiling = 2.5f },
                ["ArtTimber"] = new Spec { Tex = "wood", Tint = new Color(0.86f, 0.62f, 0.4f), Tiling = 2f },
                ["ArtTimberDark"] = new Spec { Tex = "wood", Tint = new Color(0.5f, 0.37f, 0.27f), Tiling = 2f },
                ["ArtShingle"] = new Spec { Tex = "shingle", Tint = new Color(0.66f, 0.38f, 0.27f), Tiling = 1.6f, Smoothness = 0.18f },
                ["ArtDoor"] = new Spec { Tex = "wood", Tint = new Color(0.36f, 0.25f, 0.17f), Tiling = 3f },
                ["ArtIron"] = new Spec { Tint = C(0x4A4D50), Smoothness = 0.42f, Metallic = 0.65f },
                ["ArtFlame"] = new Spec { Tint = new Color(1f, 0.72f, 0.35f), Emission = new Color(1f, 0.6f, 0.22f) * 3.2f },
                ["ArtWindow"] = new Spec { Tint = new Color(0.22f, 0.15f, 0.08f), Emission = new Color(1f, 0.66f, 0.32f) * 0.15f },
                ["ArtBanner"] = new Spec { Tex = "cloth", Tint = C(0x2F4F6F), Tiling = 4f },
                ["ArtPlayerCloth"] = new Spec { Tex = "cloth", Tint = C(0x4A6A86), Tiling = 4f },
                ["ArtSkin"] = new Spec { Tint = new Color(0.82f, 0.64f, 0.5f), Smoothness = 0.25f },
                ["ArtString"] = new Spec { Tint = new Color(0.9f, 0.88f, 0.8f) },
                ["ArtEnemyLeather"] = new Spec { Tex = "cloth", Tint = new Color(0.2f, 0.22f, 0.23f), Tiling = 4f, Smoothness = 0.22f },
                ["ArtGruntCloth"] = new Spec { Tex = "cloth", Tint = Palette.EnemyCloth, Tiling = 4f, Emission = Palette.EnemyCloth * 0.44f },
                ["ArtEnemyEyes"] = new Spec { Tint = Palette.WarningCoral, Emission = Palette.WarningCoral * 1.4f },
                ["ArtFoliage"] = new Spec { Tex = "grass", Tint = new Color(0.62f, 0.72f, 0.48f), Tiling = 3f, Smoothness = 0.08f },
                ["ArtFoliageDark"] = new Spec { Tex = "grass", Tint = new Color(0.42f, 0.55f, 0.4f), Tiling = 3f, Smoothness = 0.08f },
                ["ArtRock"] = new Spec { Tex = "stone", Tint = new Color(0.66f, 0.64f, 0.6f), Tiling = 3f, Smoothness = 0.1f },
                ["ArtReed"] = new Spec { Tint = Palette.Hex(0x8C9256), Smoothness = 0.1f },
                ["ArtFlower"] = new Spec { Tint = new Color(0.93f, 0.88f, 0.72f) },
                ["ArtPlaster"] = new Spec { Tex = "soil", Tint = new Color(0.98f, 0.95f, 0.88f), Tiling = 2f, Smoothness = 0.06f },
                ["ArtCrop"] = new Spec { Tex = "grass", Tint = new Color(0.66f, 0.74f, 0.42f), Tiling = 3f, Smoothness = 0.08f },
                ["ArtSoilTilled"] = new Spec { Tex = "soil", Tint = new Color(0.52f, 0.4f, 0.3f), Tiling = 1.5f, Smoothness = 0.06f },
                ["ArtHay"] = new Spec { Tex = "wood", Tint = new Color(0.95f, 0.82f, 0.5f), Tiling = 4f },
                ["ArtPennant"] = new Spec { Tex = "cloth", Tint = Palette.Amber, Tiling = 4f },
                ["ArtKeg"] = new Spec { Tex = "wood", Tint = Palette.Hex(0xC08A52), Tiling = 3f, Emission = Palette.Hex(0xC08A52) * 0.3f },
                ["ArtHazard"] = new Spec { Tex = "cloth", Tint = Palette.Hex(0xC8442C), Tiling = 4f, Emission = Palette.Hex(0xC8442C) * 0.6f },
                ["ArtRunnerCloth"] = new Spec { Tex = "cloth", Tint = Palette.Hex(0xD39A4C), Tiling = 4f, Emission = Palette.Hex(0xD39A4C) * 0.4f },
                ["ArtBruteCloth"] = new Spec { Tex = "cloth", Tint = Palette.Hex(0x7A3A33), Tiling = 4f, Emission = Palette.Hex(0x7A3A33) * 0.4f },
                ["ArtArcherCloth"] = new Spec { Tex = "cloth", Tint = Palette.Hex(0x6E7F3A), Tiling = 4f, Emission = Palette.Hex(0x6E7F3A) * 0.4f },
                ["ArtBomberCloth"] = new Spec { Tex = "cloth", Tint = Palette.Hex(0x3E3A52), Tiling = 4f, Emission = Palette.Hex(0x3E3A52) * 0.4f },
                ["ArtCommanderCloth"] = new Spec { Tex = "cloth", Tint = Palette.Hex(0x6B3F8C), Tiling = 4f, Emission = Palette.Hex(0x6B3F8C) * 0.5f },
                ["ArtWarBanner"] = new Spec { Tex = "cloth", Tint = Palette.WarningCoral, Tiling = 3f, Emission = Palette.WarningCoral * 0.25f },
                ["ArtGolemStone"] = new Spec { Tex = "stone", Tint = Palette.Hex(0x8C877E), Tiling = 3f, Emission = Palette.Hex(0x8C877E) * 0.08f },
                ["ArtGolemGlow"] = new Spec { Tint = Palette.WarningCoral, Emission = Palette.WarningCoral * 2f },
                ["ArtEnemyIron"] = new Spec { Tint = new Color(0.27f, 0.28f, 0.3f), Smoothness = 0.38f, Metallic = 0.6f },
                ["ArtBone"] = new Spec { Tint = new Color(0.86f, 0.82f, 0.7f), Smoothness = 0.2f },
                ["ArtFarGround"] = new Spec { Tex = "grass", Tint = new Color(0.62f, 0.68f, 0.62f), Tiling = 2f, Smoothness = 0.05f },
                ["ArtFarCliff"] = new Spec { Tex = "stone", Tint = new Color(0.42f, 0.44f, 0.44f), Tiling = 1f },
                ["ArtFarFoliage"] = new Spec { Tex = "grass", Tint = new Color(0.4f, 0.5f, 0.44f), Tiling = 3f, Smoothness = 0.05f },
                ["ArtFarFoliageDark"] = new Spec { Tex = "grass", Tint = new Color(0.3f, 0.4f, 0.38f), Tiling = 3f, Smoothness = 0.05f },
                ["ArtFarTrunk"] = new Spec { Tint = new Color(0.25f, 0.24f, 0.24f) },
                ["ArtGround"] = new Spec { Tex = "grass", Tint = Color.white, Tiling = 1f, Smoothness = 0.05f },
                ["ArtBank"] = new Spec { Tex = "soil", Tint = new Color(0.8f, 0.72f, 0.55f), Tiling = 1f, Smoothness = 0.1f },
                ["ArtBeach"] = new Spec { Tex = "soil", Tint = new Color(0.9f, 0.83f, 0.66f), Tiling = 1f, Smoothness = 0.08f },
                ["ArtCliff"] = new Spec { Tex = "stone", Tint = new Color(0.55f, 0.5f, 0.43f), Tiling = 1f },
                ["ArtRiver"] = new Spec { Tex = "water", NormalOnly = true, Tint = C(0x3B6E8C), Tiling = 1f, Smoothness = 0.82f },
                ["ArtFord"] = new Spec { Tex = "water", NormalOnly = true, Tint = C(0x6A9DB2), Tiling = 1f, Smoothness = 0.78f },
                ["ArtSea"] = new Spec { Tex = "water", NormalOnly = true, Tint = new Color(0.25f, 0.4f, 0.47f), Tiling = 1f, Smoothness = 0.7f },
                ["ArtBridge"] = new Spec { Tex = "wood", Tint = new Color(0.6f, 0.45f, 0.31f), Tiling = 1f },
            };
            var result = new Dictionary<string, Material>();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var (name, s) in specs)
            {
                var path = $"{Materials}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    // Created once; afterwards the material belongs to whoever tunes it.
                    m = new Material(lit) { name = name };
                    m.SetColor("_BaseColor", s.Tint);
                    if (s.Tex != null)
                    {
                        if (!s.NormalOnly) m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Textures}/{s.Tex}_albedo.png"));
                        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Textures}/{s.Tex}_normal.png");
                        if (normal != null)
                        {
                            m.SetTexture("_BumpMap", normal);
                            m.SetFloat("_BumpScale", s.NormalOnly ? 0.35f : 0.8f);
                            m.EnableKeyword("_NORMALMAP");
                        }

                        m.SetTextureScale("_BaseMap", Vector2.one * s.Tiling);
                    }

                    m.SetFloat("_Smoothness", s.Smoothness);
                    m.SetFloat("_Metallic", s.Metallic);
                    if (s.Emission.HasValue)
                    {
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", s.Emission.Value);
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }

                    m.enableInstancing = true;
                    AssetDatabase.CreateAsset(m, path);
                }

                result[name] = m;
            }

            return result;
        }

        // ------------------------------------------------------------ models and prefabs

        static void ConfigureModels(Dictionary<string, Material> mats)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Models }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.bakeAxisConversion = true;
                importer.globalScale = 1f;
                importer.importNormals = ModelImporterNormals.Calculate;
                importer.normalSmoothingAngle = 50f; // bevels round off, flat faces stay crisp
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importBlendShapes = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.addCollider = false; // views never take part in picking or physics
                importer.isReadable = false;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                foreach (var (name, m) in mats)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), m);
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// Rig joints: child part -> parent part. models.py exports every part top-level with a world-space pivot (Blender's
        /// FBX exporter only converts axes cleanly for top-level objects), so the hierarchy is rebuilt here.
        /// </summary>
        static readonly Dictionary<string, string> Joints = new Dictionary<string, string>
        {
            ["Torso"] = "Hips", ["Head"] = "Torso", ["ArmL"] = "Torso", ["ArmR"] = "Torso", ["Weapon"] = "ArmR", ["LegL"] = "Hips", ["LegR"] = "Hips",
            ["Bow"] = "Archer",
        };

        /// <summary>
        /// Prefab from a model, rebuilt on every run: unpacked so rig joints can be re-parented (meshes stay referenced from
        /// the FBX, so a re-export updates them), Torch.N empties renamed "Torch", the exporter's root empty dropped.
        /// </summary>
        static GameObject Variant(string model, System.Action<GameObject> extra = null)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{model}.fbx");
            if (source == null) throw new FileNotFoundException($"Art setup: {Models}/{model}.fbx missing; run Art/Source~/models.py in Blender first.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = model;
            var parts = new Dictionary<string, Transform>();
            foreach (Transform t in instance.transform) parts[t.name] = t;
            foreach (var (child, parent) in Joints)
                if (parts.TryGetValue(child, out var c) && parts.TryGetValue(parent, out var p)) c.SetParent(p, true);
            foreach (var t in new List<Transform>(parts.Values))
                if (t.childCount == 0 && t.GetComponent<MeshFilter>() == null && !t.name.StartsWith("Torch")) Object.DestroyImmediate(t.gameObject);
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Torch")) t.name = "Torch";
            foreach (var r in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            extra?.Invoke(instance);
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, $"{Prefabs}/{model}.prefab");
            Object.DestroyImmediate(instance);
            return prefab;
        }

        static void AddRig(GameObject go)
        {
            var rig = go.AddComponent<CharacterRig>();
            rig.hips = FindDeep(go.transform, "Hips");
            rig.torso = FindDeep(go.transform, "Torso");
            rig.head = FindDeep(go.transform, "Head");
            rig.armL = FindDeep(go.transform, "ArmL");
            rig.armR = FindDeep(go.transform, "ArmR");
            rig.weapon = FindDeep(go.transform, "Weapon");
            rig.legL = FindDeep(go.transform, "LegL");
            rig.legR = FindDeep(go.transform, "LegR");
            // Plan 011 performance: only the torso and legs cast shadows. That is the silhouette that reads on the ground;
            // head, arms, weapon and hips add many small shadow draws per enemy (100+ enemies) for no visible gain.
            foreach (var part in new[] { rig.head, rig.armL, rig.armR, rig.weapon, rig.hips })
                if (part != null && part.TryGetComponent<MeshRenderer>(out var r)) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var found = FindDeep(c, name);
                if (found != null) return found;
            }

            return null;
        }

        // ------------------------------------------------------------ board look and post-processing

        static ArtDirection CreateDirection(Dictionary<string, Material> mats, Dictionary<string, GameObject> env)
        {
            var d = AssetDatabase.LoadAssetAtPath<ArtDirection>(DirectionPath);
            if (d == null)
            {
                d = ScriptableObject.CreateInstance<ArtDirection>();
                AssetDatabase.CreateAsset(d, DirectionPath);
            }

            // Fill only what is empty: an artist's reassignment is never undone.
            if (d.ground == null) d.ground = mats["ArtGround"];
            if (d.bank == null) d.bank = mats["ArtBank"];
            if (d.river == null) d.river = mats["ArtRiver"];
            if (d.ford == null) d.ford = mats["ArtFord"];
            if (d.beach == null) d.beach = mats["ArtBeach"];
            if (d.cliff == null) d.cliff = mats["ArtCliff"];
            if (d.sea == null) d.sea = mats["ArtSea"];
            if (d.bridge == null) d.bridge = mats["ArtBridge"];
            if (d.pine == null) d.pine = env["ArtPine"];
            if (d.broadleaf == null) d.broadleaf = env["ArtBroadleaf"];
            if (d.bush == null) d.bush = env["ArtBush"];
            if (d.rockLarge == null) d.rockLarge = env["ArtRockLarge"];
            if (d.rockSmall == null) d.rockSmall = env["ArtRockSmall"];
            if (d.reeds == null) d.reeds = env["ArtReeds"];
            if (d.tuft == null) d.tuft = env["ArtTuft"];
            if (d.fordStone == null) d.fordStone = env["ArtFordStone"];
            if (d.farIslands == null || d.farIslands.Length == 0) d.farIslands = new[] { env["ArtFarIslandA"], env["ArtFarIslandB"] };
            if (d.dayProfile == null) d.dayProfile = Profile("DayProfile", false);
            if (d.nightProfile == null) d.nightProfile = Profile("NightProfile", true);
            EditorUtility.SetDirty(d);
            return d;
        }

        /// <summary>
        /// Restrained grading: neutral tonemapping, slight contrast, bloom only above HDR white (torches, flames, fuse),
        /// soft vignette. No depth of field, motion blur, grain or lens effects during gameplay.
        /// </summary>
        static VolumeProfile Profile(string name, bool night)
        {
            var path = $"{Post}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) return existing;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            var tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var color = p.Add<ColorAdjustments>(true);
            color.postExposure.Override(night ? 0.15f : 0.1f);
            color.contrast.Override(night ? 12f : 8f);
            color.saturation.Override(night ? -10f : 4f);
            if (night) color.colorFilter.Override(new Color(0.9f, 0.95f, 1.05f));
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(night ? 0.95f : 1.15f);
            bloom.intensity.Override(night ? 0.55f : 0.25f);
            bloom.scatter.Override(0.55f);
            var vignette = p.Add<Vignette>(true);
            vignette.intensity.Override(night ? 0.26f : 0.16f);
            vignette.smoothness.Override(0.45f);
            foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
            EditorUtility.SetDirty(p);
            return p;
        }
    }
}
