using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace TheLastFortress.Editor
{
    /// <summary>
    /// Idempotent generator for materials, entity prefabs, the content asset, the prototype scene and its
    /// build-settings entry. Existing tuning (definitions asset) is never overwritten.
    /// </summary>
    public static class PrototypeSetup
    {
        public const string Root = "Assets/TheLastFortress";
        public const string ScenePath = Root + "/Scenes/NavigationPrototype.unity";
        public const string ContentPath = Root + "/Data/PrototypeContent.asset";
        public const string DefinitionsPath = Root + "/Data/PrototypeDefinitions.asset";

        [MenuItem("The Last Fortress/Setup Prototype Scene")]
        public static void SetupMenu()
        {
            if (!Run(out var message)) Debug.LogError(message);
            else Debug.Log(message);
        }

        public static bool Run(out string message)
        {
            var active = SceneManager.GetActiveScene();
            if (active.isDirty && active.path != ScenePath)
            {
                message = $"Setup aborted: active scene '{active.path}' has unsaved changes. Save or discard them first.";
                return false;
            }

            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Data");
            EnsureFolder(Root + "/Scenes");

            var mats = CreateMaterials(out var all);
            var definitions = AssetDatabase.LoadAssetAtPath<PrototypeDefinitionsAsset>(DefinitionsPath);
            if (definitions == null)
            {
                definitions = ScriptableObject.CreateInstance<PrototypeDefinitionsAsset>();
                definitions.CopyFrom(PrototypeDefinitions.CreateDefault());
                AssetDatabase.CreateAsset(definitions, DefinitionsPath);
            }

            var content = AssetDatabase.LoadAssetAtPath<PrototypeContent>(ContentPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<PrototypeContent>();
                AssetDatabase.CreateAsset(content, ContentPath);
            }

            content.definitions = definitions;
            content.keepPrefab = SavePrefab(EntityPrefabBuilder.Keep(mats), "Keep");
            content.wallPrefab = SavePrefab(EntityPrefabBuilder.Wall(mats), "Wall");
            content.towerPrefab = SavePrefab(EntityPrefabBuilder.Tower(mats), "ArcherTower");
            content.gruntPrefab = SavePrefab(EntityPrefabBuilder.Grunt(mats), "Grunt");
            content.grass = all["Grass"];
            content.water = all["Water"];
            content.timberShadow = all["TimberShadow"];
            content.overlay = all["Overlay"];
            content.arrow = all["Arrow"];
            content.winningLayout = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/Layouts/WinningLayout.json");
            content.losingLayout = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/Layouts/LosingLayout.json");
            content.stressLayout = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/Layouts/StressLayout.json");
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();

            BuildScene(ScenePath, ContentPath);
            AddToBuildSettings(ScenePath);
            message = $"The Last Fortress prototype set up: {ScenePath}";
            return true;
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        internal static Material LitMaterial(string name, Color color, Dictionary<string, Material> all, Color? emission = null)
        {
            var path = $"{Root}/Materials/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }

            m.shader = shader;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.08f);
            m.SetFloat("_Metallic", 0f);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else m.DisableKeyword("_EMISSION");

            EditorUtility.SetDirty(m);
            all[name] = m;
            return m;
        }

        internal static EntityPrefabBuilder.Materials CreateMaterials(out Dictionary<string, Material> all)
        {
            all = new Dictionary<string, Material>();
            LitMaterial("Grass", Palette.Grass, all);
            LitMaterial("Water", Palette.WaterDay, all);
            LitMaterial("Arrow", Palette.Amber, all, Palette.Amber * 0.6f);
            var mats = new EntityPrefabBuilder.Materials
            {
                Timber = LitMaterial("Timber", Palette.Timber, all),
                TimberShadow = LitMaterial("TimberShadow", Palette.TimberShadow, all),
                Limestone = LitMaterial("Limestone", Palette.Limestone, all),
                Door = LitMaterial("DoorDark", Palette.Hex(0x2A2620), all),
                Amber = LitMaterial("TorchAmber", Palette.Amber, all, Palette.Amber * 1.6f),
                EnemyBody = LitMaterial("EnemyBody", Palette.EnemyBody, all),
                EnemyCloth = LitMaterial("EnemyCloth", Palette.EnemyCloth, all, Palette.EnemyCloth * 0.25f),
                Crop = LitMaterial("Crop", Palette.DarkGrass, all),
                RunnerCloth = LitMaterial("RunnerCloth", Palette.Hex(0xD39A4C), all, Palette.Hex(0xD39A4C) * 0.25f),
                BruteCloth = LitMaterial("BruteCloth", Palette.Hex(0x7A3A33), all, Palette.Hex(0x7A3A33) * 0.2f),
                GolemStone = LitMaterial("GolemStone", Palette.Hex(0x8C877E), all, Palette.Hex(0x8C877E) * 0.12f),
                GolemGlow = LitMaterial("GolemGlow", Palette.WarningCoral, all, Palette.WarningCoral * 2.2f),
            };

            var overlayPath = $"{Root}/Materials/Overlay.mat";
            var overlay = AssetDatabase.LoadAssetAtPath<Material>(overlayPath);
            if (overlay == null)
            {
                overlay = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(overlay, overlayPath);
            }

            overlay.shader = Shader.Find("Sprites/Default");
            overlay.color = Color.white;
            EditorUtility.SetDirty(overlay);
            all["Overlay"] = overlay;
            LitMaterial("Foliage", Palette.Hex(0x5C7444), all);
            LitMaterial("Rock", Palette.Hex(0x9A958B), all);
            return mats;
        }

        internal static GameObject SavePrefab(GameObject temp, string name)
        {
            var path = $"{Root}/Prefabs/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        internal static void BuildScene(string scenePath, string contentPath)
        {
            var scene = File.Exists(scenePath)
                ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Opening a scene in Single mode can unload in-memory asset handles; reacquire from disk.
            var content = AssetDatabase.LoadAssetAtPath<PrototypeContent>(contentPath);

            var camGo = FindOrCreate("Main Camera");
            camGo.tag = "MainCamera";
            var cam = Ensure<Camera>(camGo);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Hex(0x2E4A5A);
            cam.orthographic = true;
            var rig = Ensure<CameraRig>(camGo);
            Ensure<AudioListener>(camGo);
            var rot = Quaternion.Euler(CameraRig.Pitch, CameraRig.Yaw, 0);
            camGo.transform.SetPositionAndRotation(CameraRig.DefaultFocus - rot * Vector3.forward * 40f, rot);
            cam.orthographicSize = CameraRig.DefaultSize;

            var sunGo = FindOrCreate("Sun");
            var sun = Ensure<Light>(sunGo);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.15f;
            sunGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            var esGo = FindOrCreate("EventSystem");
            Ensure<EventSystem>(esGo);
            var module = Ensure<InputSystemUIInputModule>(esGo);
            if (module.actionsAsset == null) module.AssignDefaultActions();

            var bootGo = FindOrCreate("PrototypeBootstrap");
            var boot = Ensure<PrototypeBootstrap>(bootGo);
            var so = new SerializedObject(boot);
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("cameraRig").objectReferenceValue = rig;
            so.FindProperty("sun").objectReferenceValue = sun;
            so.ApplyModifiedPropertiesWithoutUndo();

            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.58f);
            RenderSettings.skybox = null;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        static T Ensure<T>(GameObject go) where T : Component =>
            go.TryGetComponent<T>(out var existing) ? existing : go.AddComponent<T>();

        /// <summary>Returns the single root object with this name, deleting duplicates.</summary>
        static GameObject FindOrCreate(string name)
        {
            GameObject found = null;
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name != name) continue;
                if (found == null) found = go;
                else Object.DestroyImmediate(go);
            }

            return found != null ? found : new GameObject(name);
        }

        internal static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var index = scenes.FindIndex(s => s.path == scenePath);
            if (index >= 0)
            {
                if (scenes[index].enabled) return;
                scenes[index] = new EditorBuildSettingsScene(scenePath, true);
            }
            else scenes.Add(new EditorBuildSettingsScene(scenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
