using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace TheLastFortress
{
    public enum SaveStatus
    {
        None,
        Ok,
        Corrupt,
        UnknownSchema,
        Incompatible,
        Invalid,
        Terminal,
        IoError,
    }

    public sealed class SaveLoad
    {
        public SaveStatus Status;
        public int Slot = -1;
        public RunCheckpoint Checkpoint;
        public RunSimulation Sim;
        public string Message;
        public bool FromBackup;

        public bool CanContinue => Status == SaveStatus.Ok && Sim != null;
    }

    /// <summary>
    /// Separate, versioned settings record (a corrupt run save never resets these). Version 2 added the three volumes;
    /// a version 1 file is migrated on read (volumes set to the defaults explicitly) and rewritten only when saved.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const int CurrentVersion = 2;
        public const float DefaultMasterVolume = 0.8f, DefaultMusicVolume = 0.5f, DefaultSfxVolume = 0.9f;

        public int version = CurrentVersion;
        public float cameraSensitivity = 1f;
        public bool cameraShake = true;
        public bool fullscreen;
        public int width;
        public int height;
        public float masterVolume = DefaultMasterVolume;
        public float musicVolume = DefaultMusicVolume;
        public float sfxVolume = DefaultSfxVolume;

        /// <summary>Volumes inside 0–1; NaN or infinite values fall back to their defaults.</summary>
        public void Sanitize()
        {
            masterVolume = Volume(masterVolume, DefaultMasterVolume);
            musicVolume = Volume(musicVolume, DefaultMusicVolume);
            sfxVolume = Volume(sfxVolume, DefaultSfxVolume);
        }

        static float Volume(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);

        public GameSettings Clone() => (GameSettings)MemberwiseClone();
    }

    /// <summary>
    /// First-run guidance progress, kept apart from runs and settings: skipping or finishing the guide survives
    /// New Run, Continue and restarts, and continuing an old run never restarts it.
    /// </summary>
    [Serializable]
    public sealed class OnboardingProfile
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public bool skipped;
        public bool completed;

        /// <summary>Bit per <see cref="OnboardingStep"/> already done.</summary>
        public int stepsDone;

        public bool Finished => skipped || completed;
    }

    /// <summary>
    /// Run saves under one directory (default <c>persistentDataPath/Mvp</c>; overridable for tests or <c>-saves &lt;dir&gt;</c>).
    /// Writes are synchronous: temp file in the same directory, flushed to disk, then atomic replace keeping the previous
    /// file as <c>.bak</c> (or an atomic move on first creation). Failing or unreadable files are never deleted or overwritten
    /// implicitly; a New Run after a damaged save uses the next slot file.
    /// </summary>
    public sealed class SaveStore
    {
        /// <summary>Set before the MVP scene loads (tests, <c>-saves</c>) to keep away from the player's real saves.</summary>
        public static string DirectoryOverride;

        public static string DefaultDirectory => DirectoryOverride ?? Path.Combine(Application.persistentDataPath, "Mvp");

        public const string SettingsFile = "settings.json";
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public string Directory { get; }

        public SaveStore(string directory)
        {
            Directory = directory;
        }

        public string SlotPath(int slot) => Path.Combine(Directory, slot == 0 ? "run.json" : $"run-{slot}.json");
        public string BackupPath(int slot) => SlotPath(slot) + ".bak";

        /// <summary>Highest slot index with a run file, or -1 when there is none.</summary>
        public int LatestSlot()
        {
            if (!System.IO.Directory.Exists(Directory)) return -1;
            var latest = -1;
            for (var slot = 0; slot < 1000; slot++)
                if (File.Exists(SlotPath(slot))) latest = slot;
            return latest;
        }

        /// <summary>Atomic write. Returns false with an actionable message; the previous durable file stays intact.</summary>
        public bool TryWriteAtomic(string path, string text, out string error)
        {
            error = null;
            var tmp = path + ".tmp";
            var wroteTemp = false;
            try
            {
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var bytes = Utf8.GetBytes(text);
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }

                wroteTemp = true;
                if (File.Exists(path)) File.Replace(tmp, path, path + ".bak", true);
                else File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                if (wroteTemp)
                {
                    try { File.Delete(tmp); }
                    catch (Exception) { /* leave the stray temp file; the durable file is untouched */ }
                }

                error = $"Could not save to {path}: {e.Message}. Check disk space and folder permissions, then Retry.";
                return false;
            }
        }

        public bool WriteRun(int slot, RunCheckpoint checkpoint, out string error) =>
            TryWriteAtomic(SlotPath(slot), JsonUtility.ToJson(checkpoint, true), out error);

        /// <summary>Reads and validates a slot (or its backup) as a six-night MVP save. Never modifies files.</summary>
        public SaveLoad Read(int slot, PrototypeDefinitions defs, bool backup = false) =>
            Read(slot, new RulesCatalog(RulesProfile.SixNights(defs)), backup);

        /// <summary>
        /// Reads and validates a slot (or its backup) with every rules profile this build ships. A save of a profile the
        /// build lacks is reported incompatible and left untouched. Never modifies files and never touches a live run.
        /// </summary>
        public SaveLoad Read(int slot, RulesCatalog catalog, bool backup = false)
        {
            var result = new SaveLoad { Slot = slot, FromBackup = backup };
            var path = backup ? BackupPath(slot) : SlotPath(slot);
            if (!File.Exists(path))
            {
                result.Status = SaveStatus.None;
                return result;
            }

            string text;
            try
            {
                text = File.ReadAllText(path, Utf8);
            }
            catch (Exception e)
            {
                result.Status = SaveStatus.IoError;
                result.Message = $"Cannot read {path}: {e.Message}";
                return result;
            }

            RunCheckpoint cp = null;
            try
            {
                cp = JsonUtility.FromJson<RunCheckpoint>(text);
            }
            catch (Exception)
            {
                // fall through to Corrupt
            }

            if (cp == null)
            {
                result.Status = SaveStatus.Corrupt;
                result.Message = $"{Path.GetFileName(path)} is damaged and cannot be read. It was left untouched.";
                return result;
            }

            result.Checkpoint = cp;
            result.Sim = CheckpointCodec.Restore(cp, catalog, out var problem, out var message);
            switch (problem)
            {
                case CheckpointProblem.None:
                    result.Status = SaveStatus.Ok;
                    break;
                case CheckpointProblem.UnknownSchema:
                    result.Status = SaveStatus.UnknownSchema;
                    break;
                case CheckpointProblem.IncompatibleRules:
                    result.Status = SaveStatus.Incompatible;
                    break;
                case CheckpointProblem.Terminal:
                    result.Status = SaveStatus.Terminal;
                    break;
                default:
                    result.Status = SaveStatus.Invalid;
                    break;
            }

            result.Message = message;
            return result;
        }

        public GameSettings ReadSettings(out string problem)
        {
            problem = null;
            var path = Path.Combine(Directory, SettingsFile);
            if (!File.Exists(path)) return new GameSettings();
            try
            {
                var s = JsonUtility.FromJson<GameSettings>(File.ReadAllText(path, Utf8));
                if (s != null && s.version == 1)
                {
                    // v1 had no audio: never rely on field initializers surviving deserialization.
                    s.masterVolume = GameSettings.DefaultMasterVolume;
                    s.musicVolume = GameSettings.DefaultMusicVolume;
                    s.sfxVolume = GameSettings.DefaultSfxVolume;
                    s.version = GameSettings.CurrentVersion;
                }

                if (s != null && s.version == GameSettings.CurrentVersion)
                {
                    s.Sanitize();
                    return s;
                }

                problem = "Settings file has an unknown version; defaults are used until you save settings.";
            }
            catch (Exception e)
            {
                problem = $"Settings file unreadable ({e.Message}); defaults are used until you save settings.";
            }

            return new GameSettings();
        }

        public bool WriteSettings(GameSettings settings, out string error) =>
            TryWriteAtomic(Path.Combine(Directory, SettingsFile), JsonUtility.ToJson(settings, true), out error);

        public const string ProfileFile = "profile.json";

        /// <summary>
        /// The guidance profile. A player who already has run saves from before the guide existed starts with it
        /// skipped (Help reopens it); an unreadable profile is left untouched and treated the same way.
        /// </summary>
        public OnboardingProfile ReadProfile()
        {
            var path = Path.Combine(Directory, ProfileFile);
            if (!File.Exists(path)) return new OnboardingProfile { skipped = LatestSlot() >= 0 };
            try
            {
                var p = JsonUtility.FromJson<OnboardingProfile>(File.ReadAllText(path, Utf8));
                if (p != null && p.version == OnboardingProfile.CurrentVersion) return p;
            }
            catch (Exception)
            {
                // fall through
            }

            return new OnboardingProfile { skipped = true };
        }

        public bool WriteProfile(OnboardingProfile profile, out string error) =>
            TryWriteAtomic(Path.Combine(Directory, ProfileFile), JsonUtility.ToJson(profile, true), out error);
    }
}
