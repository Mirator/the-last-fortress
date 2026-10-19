using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastFortress.Editor
{
    /// <summary>
    /// Additive, idempotent plan 008 release setup: ReleaseDefinitions.asset (created once; later runs only fill kinds added
    /// since), release-only materials and prefabs, ReleaseContent.asset (reusing every MVP prefab, material, audio clip
    /// and icon) and Scenes/Release.unity with its build-settings entry. The MVP and prototype assets and scenes are read,
    /// never regenerated. Generated assets are blockouts; replace them with authored art by swapping the content
    /// references, which this setup does not overwrite once they point outside Prefabs/Release*.
    /// Batch: -executeMethod TheLastFortress.Editor.ReleaseSetup.SetupBatch
    /// </summary>
    public static class ReleaseSetup
    {
        public const string Root = PrototypeSetup.Root;
        public const string ScenePath = Root + "/Scenes/Release.unity";
        public const string ContentPath = Root + "/Data/ReleaseContent.asset";
        public const string DefinitionsPath = Root + "/Data/ReleaseDefinitions.asset";

        [MenuItem("The Last Fortress/Setup Release Scene")]
        public static void SetupMenu()
        {
            if (!Run(out var message)) Debug.LogError(message);
            else Debug.Log(message);
        }

        /// <summary>Batch entry point: throws (non-zero exit) when setup cannot complete.</summary>
        public static void SetupBatch()
        {
            if (!Run(out var message)) throw new System.InvalidOperationException(message);
            Debug.Log(message);
        }

        public static bool Run(out string message)
        {
            var active = SceneManager.GetActiveScene();
            if (active.isDirty && active.path != ScenePath)
            {
                message = $"Release setup aborted: active scene '{active.path}' has unsaved changes. Save or discard them first.";
                return false;
            }

            var mvp = AssetDatabase.LoadAssetAtPath<PrototypeContent>(MvpSetup.ContentPath);
            if (mvp == null || mvp.mvpDefinitions == null || mvp.housePrefab == null)
            {
                message = "Release setup aborted: MVP content missing; run The Last Fortress > Setup MVP Scene first.";
                return false;
            }

            PrototypeSetup.EnsureFolder(Root + "/Data");
            PrototypeSetup.EnsureFolder(Root + "/Scenes");
            var mats = PrototypeSetup.CreateMaterials(out var all);
            var release = new EntityPrefabBuilder.ReleaseMaterials
            {
                Iron = PrototypeSetup.LitMaterial("Iron", Palette.Hex(0x4A4D50), all),
                ArcherCloth = PrototypeSetup.LitMaterial("ArcherCloth", Palette.Hex(0x6E7F3A), all, Palette.Hex(0x6E7F3A) * 0.2f),
                BomberCloth = PrototypeSetup.LitMaterial("BomberCloth", Palette.Hex(0x3E3A52), all),
                CommanderCloth = PrototypeSetup.LitMaterial("CommanderCloth", Palette.Hex(0x6B3F8C), all, Palette.Hex(0x6B3F8C) * 0.25f),
                BomberHazard = PrototypeSetup.LitMaterial("BomberHazard", Palette.Hex(0xC8442C), all, Palette.Hex(0xC8442C) * 0.35f),
                // Light oak with a faint warm emission, so the keg stays readable under night lighting.
                BomberKeg = PrototypeSetup.LitMaterial("BomberKeg", Palette.Hex(0xC08A52), all, Palette.Hex(0xC08A52) * 0.18f),
            };

            var definitions = AssetDatabase.LoadAssetAtPath<ReleaseDefinitionsAsset>(DefinitionsPath);
            if (definitions == null)
            {
                definitions = ScriptableObject.CreateInstance<ReleaseDefinitionsAsset>();
                definitions.CopyFrom(ReleaseDefinitions.CreateDefault());
                AssetDatabase.CreateAsset(definitions, DefinitionsPath);
            }
            else if (definitions.FillMissing(ReleaseDefinitions.CreateDefault()))
                EditorUtility.SetDirty(definitions); // new kinds only; existing tuning is kept

            var content = AssetDatabase.LoadAssetAtPath<PrototypeContent>(ContentPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<PrototypeContent>();
                AssetDatabase.CreateAsset(content, ContentPath);
            }

            // Everything the MVP already has is shared, so the slice and the six-night regression stay one family.
            content.definitions = mvp.definitions;
            content.mvpDefinitions = mvp.mvpDefinitions; // legacy six-night saves continue with these
            content.releaseDefinitions = definitions;
            content.keepPrefab = mvp.keepPrefab;
            content.wallPrefab = mvp.wallPrefab;
            content.towerPrefab = mvp.towerPrefab;
            content.gruntPrefab = mvp.gruntPrefab;
            content.housePrefab = mvp.housePrefab;
            content.farmPrefab = mvp.farmPrefab;
            content.trapPrefab = mvp.trapPrefab;
            content.runnerPrefab = mvp.runnerPrefab;
            content.brutePrefab = mvp.brutePrefab;
            content.golemPrefab = mvp.golemPrefab;
            content.wallShapePrefabs = mvp.wallShapePrefabs;
            content.enemyScale = mvp.enemyScale;
            if (content.art == null) content.art = mvp.art;
            content.cannonPrefab = Generated(content.cannonPrefab, () => EntityPrefabBuilder.Cannon(mats, release), "ReleaseCannon");
            content.watchtowerPrefab = Generated(content.watchtowerPrefab, () => EntityPrefabBuilder.Watchtower(mats), "ReleaseWatchtower");
            content.repairStationPrefab = Generated(content.repairStationPrefab, () => EntityPrefabBuilder.RepairStation(mats), "ReleaseRepairStation");
            content.raiderArcherPrefab = Generated(content.raiderArcherPrefab, () => EntityPrefabBuilder.RaiderArcher(mats, release), "ReleaseRaiderArcher");
            content.bomberPrefab = Generated(content.bomberPrefab, () => EntityPrefabBuilder.Bomber(mats, release), "ReleaseBomber");
            content.commanderPrefab = Generated(content.commanderPrefab, () => EntityPrefabBuilder.SiegeCommander(mats, release), "ReleaseSiegeCommander");
            content.grass = mvp.grass;
            content.water = mvp.water;
            content.timberShadow = mvp.timberShadow;
            content.overlay = mvp.overlay;
            content.arrow = mvp.arrow;
            content.foliage = mvp.foliage;
            content.rock = mvp.rock;
            content.overlayOnTop = mvp.overlayOnTop;
            content.winningLayout = null;
            content.losingLayout = null;
            content.stressLayout = mvp.stressLayout;
            content.panCursor = mvp.panCursor;
            PresentationSetup.Link(content); // audio and the nine HUD icons
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();

            PrototypeSetup.BuildScene(ScenePath, ContentPath);
            PrototypeSetup.AddToBuildSettings(ScenePath);
            message = $"The Last Fortress release set up: {ScenePath}";
            return true;
        }

        /// <summary>
        /// Regenerates a blockout prefab only while the reference is empty or still points at a generated Release* prefab;
        /// an authored replacement assigned in the content asset is never overwritten.
        /// </summary>
        static GameObject Generated(GameObject current, System.Func<GameObject> build, string name)
        {
            return PrototypeSetup.Generated(current, build, name);
        }
    }
}
