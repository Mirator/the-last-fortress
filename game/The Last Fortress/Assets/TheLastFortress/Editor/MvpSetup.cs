using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastFortress.Editor
{
    /// <summary>
    /// Additive, idempotent MVP setup: MVP definitions (created once, never overwritten), MVP-only prefabs,
    /// MvpContent.asset, Scenes/Mvp.unity and its build-settings entry. Prototype prefabs, tuning and scene are reused
    /// read-only and never regenerated here.
    /// </summary>
    public static class MvpSetup
    {
        public const string Root = PrototypeSetup.Root;
        public const string ScenePath = Root + "/Scenes/Mvp.unity";
        public const string ContentPath = Root + "/Data/MvpContent.asset";
        public const string DefinitionsPath = Root + "/Data/MvpDefinitions.asset";

        [MenuItem("The Last Fortress/Setup MVP Scene")]
        public static void SetupMenu()
        {
            if (!Run(out var message)) Debug.LogError(message);
            else Debug.Log(message);
        }

        static Material OverlayOnTopMaterial()
        {
            const string path = Root + "/Materials/OverlayOnTop.mat";
            var shader = Shader.Find("TheLastFortress/OverlayOnTop");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }

            m.shader = shader;
            m.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static bool Run(out string message)
        {
            var active = SceneManager.GetActiveScene();
            if (active.isDirty && active.path != ScenePath)
            {
                message = $"MVP setup aborted: active scene '{active.path}' has unsaved changes. Save or discard them first.";
                return false;
            }

            var prototype = AssetDatabase.LoadAssetAtPath<PrototypeContent>(PrototypeSetup.ContentPath);
            if (prototype == null || prototype.keepPrefab == null || prototype.gruntPrefab == null)
            {
                message = "MVP setup aborted: prototype content missing; run The Last Fortress > Setup Prototype Scene first.";
                return false;
            }

            PrototypeSetup.EnsureFolder(Root + "/Data");
            PrototypeSetup.EnsureFolder(Root + "/Scenes");
            var mats = PrototypeSetup.CreateMaterials(out var all);

            var definitions = AssetDatabase.LoadAssetAtPath<MvpDefinitionsAsset>(DefinitionsPath);
            if (definitions == null)
            {
                definitions = ScriptableObject.CreateInstance<MvpDefinitionsAsset>();
                definitions.CopyFrom(MvpDefinitions.CreateDefault());
                AssetDatabase.CreateAsset(definitions, DefinitionsPath);
            }
            else if (definitions.FillMissing(MvpDefinitions.CreateDefault()))
                EditorUtility.SetDirty(definitions); // new kinds only; existing tuning is kept

            var content = AssetDatabase.LoadAssetAtPath<PrototypeContent>(ContentPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<PrototypeContent>();
                AssetDatabase.CreateAsset(content, ContentPath);
            }

            content.definitions = prototype.definitions;
            content.mvpDefinitions = definitions;
            content.keepPrefab = prototype.keepPrefab;
            content.wallPrefab = prototype.wallPrefab;
            content.towerPrefab = prototype.towerPrefab;
            content.gruntPrefab = prototype.gruntPrefab;
            content.housePrefab = PrototypeSetup.SavePrefab(EntityPrefabBuilder.House(mats), "House");
            content.farmPrefab = PrototypeSetup.SavePrefab(EntityPrefabBuilder.Farm(mats), "Farm");
            content.trapPrefab = PrototypeSetup.SavePrefab(EntityPrefabBuilder.SpikeTrap(mats), "SpikeTrap");
            content.runnerPrefab = PrototypeSetup.SavePrefab(EntityPrefabBuilder.Runner(mats), "Runner");
            content.brutePrefab = PrototypeSetup.SavePrefab(EntityPrefabBuilder.Brute(mats), "Brute");
            content.golemPrefab = PrototypeSetup.SavePrefab(EntityPrefabBuilder.SiegeGolem(mats), "SiegeGolem");
            content.wallShapePrefabs = new GameObject[6];
            for (var s = 0; s < 6; s++)
                content.wallShapePrefabs[s] = PrototypeSetup.SavePrefab(EntityPrefabBuilder.WallPiece(mats, (WallShape)s), "MvpWall" + (WallShape)s);
            content.enemyScale = 1.35f;
            content.foliage = all["Foliage"];
            content.rock = all["Rock"];
            content.overlayOnTop = OverlayOnTopMaterial();
            content.grass = all["Grass"];
            content.water = all["Water"];
            content.timberShadow = all["TimberShadow"];
            content.overlay = all["Overlay"];
            content.arrow = all["Arrow"];
            content.winningLayout = null;
            content.losingLayout = null;
            content.stressLayout = prototype.stressLayout;
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();

            PrototypeSetup.BuildScene(ScenePath, ContentPath);
            PrototypeSetup.AddToBuildSettings(ScenePath);
            message = $"The Last Fortress MVP set up: {ScenePath}";
            return true;
        }
    }
}
