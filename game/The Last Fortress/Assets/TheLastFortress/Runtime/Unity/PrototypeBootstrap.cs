using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastFortress
{
    /// <summary>
    /// Single scene entry point. Owns the authoritative <see cref="RunSimulation"/> and drives input, views and HUD.
    /// Normal mode is the default; the stress fixture is entered only via -stress, F9, or <see cref="EnterStressMode"/>.
    /// In the MVP scene it also owns the menu, run persistence (checkpoints) and settings.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        /// <summary>Largest real frame delta fed to the simulation; longer hitches are not caught up.</summary>
        public const float MaxFrameDelta = 0.25f;

        [SerializeField] PrototypeContent content;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] Light sun;

        public RunSimulation Sim { get; private set; }
        public PrototypeDefinitions Definitions { get; private set; }
        public PlayerController Player { get; private set; }
        public CameraRig CameraRig => cameraRig;
        public WorldView World { get; private set; }
        public RouteOverlay Overlay { get; private set; }
        public HudView Hud { get; private set; }
        public PrototypeContent Content => content;
        public bool IsStressMode => Sim != null && Sim.StressMode;

        /// <summary>Multi-night MVP scene (content carries MVP definitions); false for the one-wave prototype.</summary>
        public bool IsMvp => content.IsMvp;

        /// <summary>Build cards/hotkeys in order: 1 Wall, 2 Tower, then (MVP) 3 House, 4 Farm, 5 Spike Trap.</summary>
        public IReadOnlyList<BuildingKind> PurchasableKinds { get; private set; }

        /// <summary>When false, Update does not advance the simulation (tests step it explicitly).</summary>
        public bool AutoAdvance { get; set; } = true;

        // ---------- MVP: menu, persistence, settings ----------

        public RunPersistence Persistence { get; private set; }
        public GameSettings Settings { get; private set; } = new GameSettings();
        public string SettingsProblem { get; private set; }

        /// <summary>Main menu (MVP launch, Save and Quit, in-run menu). Gameplay input is ignored while open.</summary>
        public bool MenuOpen { get; private set; }

        /// <summary>Latest run slot as read when the menu opened (Continue/recovery state).</summary>
        public SaveLoad LatestSave { get; private set; }

        /// <summary>True when the flat authored map was requested (-flat, tests, recorded policies).</summary>
        public bool FlatMap { get; private set; }

        RunState observedState;
        int observedNight;
        int observedKeepHp;

        void Awake()
        {
            if (content == null || cameraRig == null) throw new InvalidOperationException("PrototypeBootstrap is missing scene references; run The Last Fortress > Setup Prototype Scene.");
            Definitions = IsMvp ? content.mvpDefinitions.ToDefinitions() : content.definitions.ToDefinitions();
            var errors = Definitions.Validate();
            if (errors.Count > 0) Debug.LogError("Invalid definitions: " + string.Join("; ", errors));
            PurchasableKinds = IsMvp
                ? new[] { BuildingKind.Wall, BuildingKind.Tower, BuildingKind.House, BuildingKind.Farm, BuildingKind.SpikeTrap }
                : new[] { BuildingKind.Wall, BuildingKind.Tower };

            var args = Environment.GetCommandLineArgs();
            if (IsMvp)
            {
                var saves = ArgValue(args, "-saves");
                if (saves != null) SaveStore.DirectoryOverride = saves;
                Persistence = new RunPersistence(new SaveStore(SaveStore.DefaultDirectory));
                Settings = Persistence.Store.ReadSettings(out var problem);
                SettingsProblem = problem;
                FlatMap = HasArg(args, "-flat");
            }

            Player = new PlayerController(this);
            World = new GameObject("World").AddComponent<WorldView>();
            World.transform.SetParent(transform, false);
            World.Initialize(content, sun);
            Overlay = new GameObject("RouteOverlay").AddComponent<RouteOverlay>();
            Overlay.transform.SetParent(transform, false);
            Overlay.Initialize(content, PurchasableKinds);
            Hud = gameObject.AddComponent<HudView>();
            Hud.Build(this);
            ApplySettings(Settings, false);

            if (HasArg(args, "-stress")) EnterStressMode();
            else if (IsMvp)
            {
                var seedArg = ArgValue(args, "-seed");
                if (HasArg(args, "-capture") || HasArg(args, "-newrun"))
                    NewRun(seedArg != null && uint.TryParse(seedArg, out var s) ? s : 1u, FlatMap);
                else
                {
                    EnterNormalMode(); // placeholder Day behind the menu; never saved
                    OpenMenu();
                }
            }
            else EnterNormalMode();

            var layout = ArgValue(args, "-layout");
            if (!IsStressMode && !IsMvp && layout != null)
            {
                if (layout == "win") ApplyLayout(content.winningLayout);
                else if (layout == "lose") ApplyLayout(content.losingLayout);
            }

            var measureOut = ArgValue(args, "-measure");
            if (measureOut != null && IsStressMode) gameObject.AddComponent<StressMeasurement>().Begin(this, measureOut);
            var captureDir = ArgValue(args, "-capture");
            if (captureDir != null) gameObject.AddComponent<CaptureSequence>().Begin(this, captureDir);
        }

        static bool HasArg(string[] args, string name) => Array.IndexOf(args, name) >= 0;

        static string ArgValue(string[] args, string name)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        void Update()
        {
            var dt = Mathf.Min(Time.unscaledDeltaTime, MaxFrameDelta);
            var kb = Player.Keyboard;
            if (kb != null && kb.f9Key.wasPressedThisFrame && !MenuOpen)
            {
                if (!IsStressMode) EnterStressMode();
                else if (IsMvp) QuitToMenuWithoutSaving(); // the MVP never drops into an unsaved placeholder run
                else EnterNormalMode();
            }

            if (!MenuOpen) Player.Tick(dt);
            if (AutoAdvance && !MenuOpen) Sim.Advance(dt);
            SyncViews(dt);
        }

        /// <summary>Pushes current simulation state to views and HUD (also used by tests after manual stepping).</summary>
        public void SyncViews(float dt = 0f)
        {
            ObserveTransitions();
            World.SetBuildGridVisible(Player.Tool.HasValue && Sim.State == RunState.Day);
            World.Sync(Sim, dt);
            Overlay.Refresh(Sim, Player);
            Hud.Refresh(Sim, Player, Overlay, cameraRig);
        }

        /// <summary>Checkpoint triggers that follow simulation transitions (Reward ledger, terminal record) and Keep-hit shake.</summary>
        void ObserveTransitions()
        {
            if (Sim == null) return;
            if (IsMvp && Sim.State == RunState.Night && Sim.Keep.Hp < observedKeepHp && Settings.cameraShake) cameraRig.Kick(0.12f);
            observedKeepHp = Sim.Keep.Hp;
            if (Sim.State == observedState && Sim.NightNumber == observedNight) return;
            var previous = observedState;
            observedState = Sim.State;
            observedNight = Sim.NightNumber;
            if (Persistence == null || !Persistence.HasRun || previous != RunState.Night) return;
            if (Sim.State == RunState.Reward) Persistence.CommitRewardLedger(Sim);
            else if (Sim.IsTerminal) Persistence.CommitTerminal(Sim);
        }

        // ---------- commands (HUD buttons, hotkeys, tests) ----------

        public void EnterNormalMode() => CreateRun(IsMvp
            ? new RunConfig { Definitions = Definitions, Campaign = CreateCampaign(), Terrain = TerrainMap.Flat() }
            : new RunConfig { Definitions = Definitions, Wave = WavePlan.CreateMainWave() });

        /// <summary>All wave plans are stored before Day 1.</summary>
        static CampaignConfig CreateCampaign() => MvpCampaign.CreateSixNights();

        /// <summary>Stress fixture: prototype Grunts in the prototype scene; mixed MVP archetypes in the MVP scene.</summary>
        public void EnterStressMode()
        {
            Persistence?.Detach();
            MenuOpen = false;
            CreateRun(new RunConfig { Definitions = Definitions, Wave = IsMvp ? WavePlan.CreateMvpStressWave() : WavePlan.CreateStressWave(), StressMode = true });
        }

        void CreateRun(RunConfig config)
        {
            LoadRun(new RunSimulation(config));
            if (config.StressMode) SetUpStressRun();
        }

        void LoadRun(RunSimulation sim)
        {
            Sim = sim;
            observedState = sim.State;
            observedNight = sim.NightNumber;
            observedKeepHp = sim.Keep.Hp;
            World.SetTerrain(sim.Config.Terrain);
            ResetPresentation();
        }

        void SetUpStressRun()
        {
            var layout = content.stressLayout != null ? LayoutJson.Parse(content.stressLayout) : LayoutFixture.CreateStressLayout();
            var failures = Sim.ApplyLayout(layout, bypassCost: true);
            if (IsMvp) failures.AddRange(Sim.ApplyLayout(LayoutFixture.CreateMvpStressExtras(), bypassCost: true));
            if (failures.Count > 0) Debug.LogError("Stress layout failures: " + string.Join("; ", failures));
            if (!Sim.StartNight()) Debug.LogError("Stress fixture could not start: " + string.Join("; ", Sim.Diagnostics));
        }

        void ResetPresentation()
        {
            Player.Reset();
            World.ClearAll();
            Overlay.ResetCaches();
            SyncViews();
        }

        /// <summary>Restores the exact initial state of the current mode (MVP: same-seed retry, saved as a fresh Day 1).</summary>
        public void Restart()
        {
            Sim.Restart();
            LoadRun(Sim);
            if (Sim.StressMode) SetUpStressRun();
            else if (Persistence != null && Persistence.HasRun)
            {
                Persistence.BeginNewRun(Definitions);
                Persistence.CommitDay(Sim);
            }
        }

        // ---------- MVP run lifecycle ----------

        /// <summary>A new seed for New Run. Wall-clock based on purpose: gameplay streams are derived from it with xorshift32.</summary>
        public static uint NewSeed()
        {
            var t = (ulong)DateTime.UtcNow.Ticks;
            var s = (uint)(t ^ (t >> 32));
            return s == 0 ? 1u : s;
        }

        /// <summary>Starts a fresh saved run: terrain from the seed (or the flat authored map), six stored waves, Day 1 checkpoint.</summary>
        public void NewRun(uint seed, bool flat)
        {
            var terrain = flat ? TerrainMap.Flat() : MapGenerator.Generate(seed);
            Debug.Log($"New run: {terrain.Describe()}");
            CreateRun(new RunConfig { Definitions = Definitions, Seed = seed, Terrain = terrain, Campaign = CreateCampaign() });
            MenuOpen = false;
            Persistence.BeginNewRun(Definitions);
            Persistence.CommitDay(Sim);
        }

        public void Continue()
        {
            var load = Persistence.Store.Read(Persistence.Store.LatestSlot(), Definitions);
            if (!load.CanContinue) return;
            LoadRun(load.Sim);
            Persistence.Adopt(load);
            MenuOpen = false;
        }

        /// <summary>Explicit recovery of the previous checkpoint (.bak) when the latest save is damaged.</summary>
        public bool RecoverBackup()
        {
            var slot = Persistence.Store.LatestSlot();
            var load = Persistence.Store.Read(slot, Definitions, backup: true);
            if (!load.CanContinue) return false;
            LoadRun(load.Sim);
            Persistence.Adopt(load);
            Persistence.CommitDay(Sim);
            MenuOpen = false;
            return true;
        }

        public void OpenMenu()
        {
            if (!IsMvp) return;
            MenuOpen = true;
            Player.CancelTool();
            var slot = Persistence.Store.LatestSlot();
            LatestSave = slot >= 0 ? Persistence.Store.Read(slot, Definitions) : new SaveLoad { Status = SaveStatus.None };
            if (LatestSave.Status == SaveStatus.Ok || LatestSave.Status == SaveStatus.Terminal) LatestSave.Message ??= "";
        }

        /// <summary>Resumes the current run from the in-run menu (only when a run is active).</summary>
        public void CloseMenu()
        {
            if (Persistence != null && Persistence.HasRun) MenuOpen = false;
        }

        /// <summary>Day only: commits the current construction/HP/gold as this Day's checkpoint, then returns to the menu.</summary>
        public bool SaveAndQuit()
        {
            if (Sim.State != RunState.Day || !Persistence.HasRun) return false;
            if (!Persistence.CommitDay(Sim)) return false;
            Persistence.Detach();
            EnterNormalMode();
            OpenMenu();
            return true;
        }

        /// <summary>Leaves the run without saving (Night/Reward resume from the last Day checkpoint; pending writes are lost).</summary>
        public void QuitToMenuWithoutSaving()
        {
            Persistence.Detach();
            EnterNormalMode();
            OpenMenu();
        }

        public bool RetrySave() => Persistence != null && Persistence.Retry();

        public void ApplySettings(GameSettings settings, bool persist)
        {
            Settings = settings;
            cameraRig.Sensitivity = Mathf.Clamp(settings.cameraSensitivity, 0.25f, 3f);
            cameraRig.ShakeEnabled = settings.cameraShake;
            // Window mode applies on its own; resolution falls back to the current size until one is chosen.
            if (!Application.isEditor && persist)
                Screen.SetResolution(settings.width > 0 ? settings.width : Screen.width, settings.height > 0 ? settings.height : Screen.height,
                    settings.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            else if (!Application.isEditor && settings.width > 0 && settings.height > 0)
                Screen.SetResolution(settings.width, settings.height, settings.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            if (persist && Persistence != null)
                SettingsProblem = Persistence.Store.WriteSettings(settings, out var error) ? null : error;
        }

        public void ResetCamera() => cameraRig.ResetView();

        /// <summary>A second Start Night click this soon after starting is ignored instead of pausing.</summary>
        public const float DoubleClickGuardSeconds = 0.4f;

        float nightStartedAt = float.NegativeInfinity;

        public void PrimaryAction()
        {
            if (Sim.State == RunState.Day) StartNight();
            else if (Sim.State == RunState.Night && Time.unscaledTime - nightStartedAt > DoubleClickGuardSeconds) TogglePause();
        }

        /// <summary>MVP: the Day checkpoint is committed first; Night does not start unless the write succeeded.</summary>
        public bool StartNight()
        {
            if (Sim.State != RunState.Day) return false;
            if (Persistence != null && Persistence.HasRun && (Persistence.SavePending || !Persistence.CommitDay(Sim))) return false;
            var started = Sim.StartNight();
            if (!started) return false;
            observedState = RunState.Night;
            nightStartedAt = Time.unscaledTime;
            Player.CancelTool();
            return true;
        }

        public void TogglePause() => Sim.TogglePause();

        public void SellSelected()
        {
            if (Player.Selected == null) return;
            if (Sim.TrySell(Player.Selected) == SellRejection.None) Player.Select(null);
        }

        public void RepairSelected()
        {
            if (Player.Selected != null) Sim.TryRepair(Player.Selected);
        }

        public void RotateSelected()
        {
            if (Player.Selected != null) Sim.TryRotate(Player.Selected);
        }

        /// <summary>
        /// Reward card click: allowed only after the frozen offer is durable; commits once (stale or repeated clicks are
        /// rejected by the simulation), then saves the new Day.
        /// </summary>
        public bool ChoosePerk(string offerId, string perkId)
        {
            if (Persistence != null && Persistence.HasRun && Persistence.SavePending) return false;
            var chosen = Sim.ChoosePerk(offerId, perkId);
            if (!chosen) return false;
            Player.CancelTool();
            observedState = Sim.State;
            observedNight = Sim.NightNumber;
            if (Persistence != null && Persistence.HasRun) Persistence.CommitDay(Sim);
            return true;
        }

        public List<string> ApplyLayout(TextAsset layoutAsset)
        {
            var layout = LayoutJson.Parse(layoutAsset);
            return Sim.ApplyLayout(layout, bypassCost: false);
        }

        /// <summary>Advances simulation time immediately (tests/capture); views are synced afterwards.</summary>
        public void FastForward(double seconds)
        {
            Sim.Advance(seconds);
            SyncViews();
        }
    }
}
