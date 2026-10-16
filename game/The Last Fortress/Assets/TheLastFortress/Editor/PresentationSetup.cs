using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastFortress.Editor
{
    /// <summary>
    /// Links the generated presentation assets into MvpContent.asset: Audio/Clips/*.wav (import settings + AudioContent)
    /// and UI/Icons/*.png (HUD icons). Ownership is "generated": clips come from Audio/Source~/synth.py and icons from
    /// UI/Source~/icons.py; rerunning this only rewrites import settings and references, never the files themselves.
    /// Idempotent; also called by the MVP setup. Batch: -executeMethod TheLastFortress.Editor.PresentationSetup.Run
    /// </summary>
    public static class PresentationSetup
    {
        public const string AudioRoot = PrototypeSetup.Root + "/Audio";
        public const string ClipsPath = AudioRoot + "/Clips";
        public const string AudioContentPath = AudioRoot + "/AudioContent.asset";
        public const string IconsPath = PrototypeSetup.Root + "/UI/Icons";

        [MenuItem("The Last Fortress/Link Audio and Icons")]
        public static void Run()
        {
            var content = AssetDatabase.LoadAssetAtPath<PrototypeContent>(MvpSetup.ContentPath);
            if (content == null)
            {
                Debug.LogError("Presentation setup: MvpContent.asset missing; run Setup MVP Scene first.");
                return;
            }

            Link(content);
            AssetDatabase.SaveAssets();
            Debug.Log("Presentation setup: audio and icons linked into " + MvpSetup.ContentPath);
        }

        public static void Link(PrototypeContent content)
        {
            AssetDatabase.Refresh();
            content.audio = LinkAudio();
            content.buildingIcons = new Texture2D[9];
            content.buildingIcons[(int)BuildingKind.Wall] = Icon("icon_wall");
            content.buildingIcons[(int)BuildingKind.Tower] = Icon("icon_tower");
            content.buildingIcons[(int)BuildingKind.House] = Icon("icon_house");
            content.buildingIcons[(int)BuildingKind.Farm] = Icon("icon_farm");
            content.buildingIcons[(int)BuildingKind.SpikeTrap] = Icon("icon_trap");
            content.buildingIcons[(int)BuildingKind.Cannon] = Icon("icon_cannon");
            content.buildingIcons[(int)BuildingKind.Watchtower] = Icon("icon_watchtower");
            content.buildingIcons[(int)BuildingKind.RepairStation] = Icon("icon_repair");
            content.goldIcon = Icon("icon_gold");
            content.panCursor = Icon("cursor_pan", cursor: true);
            EditorUtility.SetDirty(content);
        }

        static AudioContent LinkAudio()
        {
            if (!AssetDatabase.IsValidFolder(ClipsPath)) return null;
            var audio = AssetDatabase.LoadAssetAtPath<AudioContent>(AudioContentPath);
            if (audio == null)
            {
                audio = ScriptableObject.CreateInstance<AudioContent>();
                AssetDatabase.CreateAsset(audio, AudioContentPath);
            }

            audio.uiClick = Clip("ui_click");
            audio.uiConfirm = Clip("ui_confirm");
            audio.uiReject = Clip("ui_reject");
            audio.build = Clip("build");
            audio.sell = Clip("sell");
            audio.repair = Clip("repair");
            audio.reward = Clip("reward");
            audio.shots = new[] { Clip("shot_0"), Clip("shot_1"), Clip("shot_2") };
            audio.impacts = new[] { Clip("impact_0"), Clip("impact_1"), Clip("impact_2") };
            audio.trap = Clip("trap");
            audio.heavyStrike = Clip("heavy_strike");
            audio.destroy = Clip("destroy");
            audio.keepHit = Clip("keep_hit");
            audio.cannonShot = Clip("cannon_shot");
            audio.raiderShot = Clip("raider_shot");
            audio.fuseLit = Clip("fuse_lit");
            audio.bomberBlast = Clip("bomber_blast");
            audio.commanderArrival = Clip("commander_arrival");
            audio.nightStart = Clip("night_start");
            audio.dawn = Clip("dawn");
            audio.golemArrival = Clip("golem");
            audio.victory = Clip("victory");
            audio.defeat = Clip("defeat");
            audio.ambienceDay = Clip("ambience_day", true);
            audio.ambienceNight = Clip("ambience_night", true);
            audio.musicPlanning = Clip("music_planning", true);
            audio.musicCombat = Clip("music_combat", true);
            audio.musicFinale = Clip("music_finale", true);
            audio.musicConclusion = Clip("music_conclusion", true);
            EditorUtility.SetDirty(audio);
            return audio;
        }

        /// <summary>Short effects decompress on load (no latency); long loops stream compressed.</summary>
        static AudioClip Clip(string name, bool loop = false)
        {
            var path = $"{ClipsPath}/{name}.wav";
            if (!File.Exists(path))
            {
                Debug.LogWarning("Presentation setup: missing clip " + path);
                return null;
            }

            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = loop ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = loop ? 0.7f : 1f;
            settings.preloadAudioData = !loop;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = loop;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static Texture2D Icon(string name, bool cursor = false)
        {
            var path = $"{IconsPath}/{name}.png";
            if (!File.Exists(path)) return null;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = cursor ? TextureImporterType.Cursor : TextureImporterType.Default; // Cursor: readable RGBA, no mips
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
