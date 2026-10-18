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

        /// <summary>MVP sound (null in the one-wave prototype). Receives the same event batch as <see cref="World"/>.</summary>
        public AudioDirector Audio { get; private set; }

        /// <summary>MVP first-run guide (null in the prototype); observes the run, never changes it.</summary>
        public OnboardingState Onboarding { get; private set; }
        public PrototypeContent Content => content;
        public bool IsStressMode => Sim != null && Sim.StressMode;

        /// <summary>Multi-night MVP scene (content carries MVP definitions); false for the one-wave prototype.</summary>
        public bool IsMvp => content.IsMvp;

        /// <summary>Plan 008 release scene: twelve nights on a chosen map profile; old six-night saves still continue.</summary>
        public bool IsRelease => content.IsRelease;

        /// <summary>
        /// Build cards/hotkeys in order: 1 Wall, 2 Tower, then (MVP) 3 House, 4 Farm, 5 Spike Trap, then (release) 6 Cannon,
        /// 7 Watchtower, 8 Repair Station. A card whose kind the loaded run does not define (a legacy save) is hidden.
        /// </summary>
        public IReadOnlyList<BuildingKind> PurchasableKinds { get; private set; }

        /// <summary>Rules profiles this scene can play and restore; the first one starts every New Run (MVP scenes only).</summary>
        public RulesCatalog Catalog { get; private set; }

        /// <summary>Definitions every new run starts with (the loaded run may use another profile's: see <see cref="Definitions"/>).</summary>
        public PrototypeDefinitions NewRunDefinitions { get; private set; }

        /// <summary>
        /// Release: map profile for the next New Run; "" (the default) lets the seed pick one. The title cycles
        /// by seed → Ravine → Crossing → Meadow; -map ravine|crossing|meadow|random overrides.
        /// </summary>
        public string MapProfile { get; private set; } = "";

        public void CycleMapProfile()
        {
            var i = System.Array.IndexOf(MapProfiles.All, MapProfile); // -1 for "by seed"
            MapProfile = i + 1 < MapProfiles.All.Length ? MapProfiles.All[i + 1] : "";
        }

        public void SetMapProfile(string id)
        {
            if (MapProfiles.IsKnown(id)) MapProfile = id;
            else if (id == "random") MapProfile = "";
        }

        /// <summary>The profile a New Run on this seed will use.</summary>
        public string MapProfileFor(uint seed) => MapProfile == "" ? MapProfiles.ForSeed(seed) : MapProfile;

        /// <summary>When false, Update does not advance the simulation (tests step it explicitly).</summary>
        public bool AutoAdvance { get; set; } = true;

        /// <summary>
        /// When true, Update leaves view syncing to the caller, so frame-exact captures and motion probes own the
        /// presentation clock (otherwise each frame would advance effects twice).
        /// </summary>
        public bool ExternalViewClock { get; set; }

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

        /// <summary>A guard dialog's text and choices; the HUD renders it and play is suspended while it is open.</summary>
        public sealed class ConfirmRequest
        {
            public string Title;
            public string Body;
            public readonly List<(string label, Action action)> Options = new List<(string, Action)>();
        }

        /// <summary>The open guard dialog (Restart/Quit), or null.</summary>
        public ConfirmRequest PendingConfirm { get; private set; }

        /// <summary>Application exit boundary (replaced in tests so nothing ever really quits).</summary>
        public Action QuitApplication = Application.Quit;

        /// <summary>Save-and-exit is waiting for its save to succeed (Retry); the exit happens only after that commit.</summary>
        public bool ExitAfterSave { get; private set; }

        /// <summary>Combat and input are suspended by the menu or a guard dialog.</summary>
        public bool Suspended => MenuOpen || PendingConfirm != null;

        /// <summary>The launch/title menu: no run is active behind it.</summary>
        public bool TitleScreen => MenuOpen && (Persistence == null || !Persistence.HasRun);

        /// <summary>Title composition: the Keep sits right of the menu panel, closer than the play view.</summary>
        public static readonly Vector3 TitleFocus = new Vector3(7.7f, 0f, 13.3f);
        public const float TitleSize = 7f;

        /// <summary>This frame's simulation events: drained once here and shared read-only by world view and audio.</summary>
        readonly List<SimEvent> frameEvents = new List<SimEvent>();

        RunState observedState;
        int observedNight;
        int observedKeepHp;

        void Awake()
        {
            if (content == null || cameraRig == null) throw new InvalidOperationException("PrototypeBootstrap is missing scene references; run The Last Fortress > Setup Prototype Scene.");
            if (IsRelease)
            {
                var release = RulesProfile.TwelveNights(content.releaseDefinitions.ToDefinitions());
                var legacy = RulesProfile.SixNights(content.mvpDefinitions != null ? content.mvpDefinitions.ToDefinitions() : MvpDefinitions.CreateDefault());
                Catalog = new RulesCatalog(release, legacy);
                Definitions = release.Definitions;
                PurchasableKinds = RulesProfile.ReleaseKinds;
            }
            else if (IsMvp)
            {
                Catalog = new RulesCatalog(RulesProfile.SixNights(content.mvpDefinitions.ToDefinitions()));
                Definitions = Catalog.Default.Definitions;
                PurchasableKinds = RulesProfile.MvpKinds;
            }
            else
            {
                Definitions = content.definitions.ToDefinitions();
                PurchasableKinds = new[] { BuildingKind.Wall, BuildingKind.Tower };
            }

            NewRunDefinitions = Definitions;
            var errors = Definitions.Validate();
            if (errors.Count > 0) Debug.LogError("Invalid definitions: " + string.Join("; ", errors));

            var args = Environment.GetCommandLineArgs();
            if (IsMvp)
            {
                var saves = ArgValue(args, "-saves");
                if (saves != null) SaveStore.DirectoryOverride = saves;
                Persistence = new RunPersistence(new SaveStore(SaveStore.DefaultDirectory));
                Settings = Persistence.Store.ReadSettings(out var problem);
                SettingsProblem = problem;
                FlatMap = HasArg(args, "-flat");
                var map = ArgValue(args, "-map");
                if (map != null) SetMapProfile(map);
            }

            Player = new PlayerController(this);
            World = new GameObject("World").AddComponent<WorldView>();
            World.transform.SetParent(transform, false);
            World.Initialize(content, sun);
            cameraRig.PanCursor = content.panCursor;
            if (IsMvp)
            {
                Audio = new GameObject("Audio").AddComponent<AudioDirector>();
                Audio.transform.SetParent(transform, false);
                Audio.Initialize(content.audio);
                Onboarding = new OnboardingState(Persistence.Store.ReadProfile());
            }

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

            if (!Suspended) Player.Tick(dt);
            else if (cameraRig.IsDragging) cameraRig.CancelDrag(); // a menu or dialog never keeps a drag (or its cursor)
            if (AutoAdvance && !Suspended) Sim.Advance(dt);
            if (!ExternalViewClock) SyncViews(dt);
        }

        /// <summary>Pushes current simulation state to views and HUD (also used by tests after manual stepping).</summary>
        public void SyncViews(float dt = 0f)
        {
            ObserveTransitions();
            var fxDt = PresentationDelta(dt);
            frameEvents.Clear();
            Sim.DrainEvents(frameEvents); // the only drain: both consumers see each batch exactly once
            World.SetBuildGridVisible(Player.Tool.HasValue && Sim.State == RunState.Day);
            if (Overlay.gameObject.activeSelf == TitleScreen) Overlay.gameObject.SetActive(!TitleScreen); // no route lines on the title view
            World.Sync(Sim, frameEvents, fxDt);
            Audio?.Consume(Sim, frameEvents, fxDt, dt, CombatFrozen, Suspended, TitleScreen);
            ObserveOnboarding();
            Overlay.Refresh(Sim, Player);
            Hud.Refresh(Sim, Player, Overlay, cameraRig);
            Hud.TickEffects(fxDt);
        }

        /// <summary>Night paused, menu or guard dialog: the presentation clock is frozen.</summary>
        public bool CombatFrozen => Suspended || (Sim.State == RunState.Night && Sim.Paused);

        void ObserveOnboarding()
        {
            if (Onboarding != null && Onboarding.Observe(Sim, Player)) SaveProfile();
        }

        /// <summary>Guide progress is a convenience record: a failed write is logged, never blocks play.</summary>
        void SaveProfile()
        {
            if (!Persistence.Store.WriteProfile(Onboarding.Profile, out var error)) Debug.LogWarning(error);
        }

        /// <summary>The guide's Skip: saved at once so later runs and restarts do not show it again.</summary>
        public void SkipOnboarding()
        {
            if (Onboarding == null) return;
            Onboarding.Skip();
            SaveProfile();
        }

        /// <summary>Help's "Show guide": resumes the guide for this run at its first unfinished step.</summary>
        public void ReopenOnboarding()
        {
            if (Onboarding == null) return;
            Onboarding.Reopen();
            SaveProfile();
        }

        /// <summary>
        /// Combat presentation clock: frozen while Night is paused or the menu suspends play, so arrows, cues, gait and burn
        /// hold exactly where they are. It keeps running into Reward/Victory/Defeat so the final kill's cues finish.
        /// HUD refresh and camera inspection stay live regardless.
        /// </summary>
        public float PresentationDelta(float dt) => Suspended || (Sim.State == RunState.Night && Sim.Paused) ? 0f : dt;

        /// <summary>Checkpoint triggers that follow simulation transitions (Reward ledger, terminal record) and Keep-hit shake.</summary>
        void ObserveTransitions()
        {
            if (Sim == null) return;
            // Actual Keep damage only (HP drop): a HUD cue that works off-screen and with shake disabled, plus optional shake.
            if (IsMvp && Sim.Keep.Hp < observedKeepHp && (Sim.State == RunState.Night || Sim.State == RunState.Defeat))
            {
                Hud.KeepHit(observedKeepHp - Sim.Keep.Hp);
                Audio?.KeepHit();
                if (Settings.cameraShake) cameraRig.Kick(0.12f);
            }

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

        /// <summary>
        /// Prototype: the one-wave run. MVP: the never-saved placeholder behind the menu, staged as the title view — a
        /// small settlement on a generated island (decorative placements; failures on terrain are simply skipped).
        /// </summary>
        public void EnterNormalMode()
        {
            if (!IsMvp)
            {
                CreateRun(new RunConfig { Definitions = Definitions, Wave = WavePlan.CreateMainWave() });
                return;
            }

            var terrain = IsRelease ? MapProfiles.Generate(MapProfiles.Meadow, TitleSeed) : MapGenerator.Generate(TitleSeed);
            CreateRun(new RunConfig { Definitions = NewRunDefinitions, Campaign = CreateCampaign(TitleSeed, MapProfiles.Meadow, terrain), Terrain = terrain });
            Sim.ApplyLayout(TitleLayout, bypassCost: true);
            Audio?.ResetRun(Sim); // staging is not construction: no build sounds for it
        }

        const uint TitleSeed = 3;

        static readonly LayoutFixture TitleLayout = new LayoutFixture("Title settlement", new[]
        {
            new Placement(BuildingKind.Tower, new GridPos(8, 9)), new Placement(BuildingKind.Tower, new GridPos(12, 11)),
            new Placement(BuildingKind.Tower, new GridPos(9, 12)), new Placement(BuildingKind.House, new GridPos(11, 8)),
            new Placement(BuildingKind.House, new GridPos(12, 13)), new Placement(BuildingKind.House, new GridPos(7, 12)),
            new Placement(BuildingKind.Farm, new GridPos(4, 12)), new Placement(BuildingKind.Wall, new GridPos(7, 9)),
            new Placement(BuildingKind.Wall, new GridPos(7, 10)), new Placement(BuildingKind.Wall, new GridPos(7, 8)),
            new Placement(BuildingKind.Wall, new GridPos(13, 10)), new Placement(BuildingKind.Wall, new GridPos(13, 11)),
            new Placement(BuildingKind.SpikeTrap, new GridPos(6, 10)),
        });

        /// <summary>
        /// All wave plans are stored before Day 1 (release: seeded entry variation for the chosen map profile, with the
        /// entries turned to the terrain's symmetry so its rivers and channels face the fronts they were made for).
        /// </summary>
        CampaignConfig CreateCampaign(uint seed, string mapProfile, TerrainMap terrain) =>
            IsRelease ? ReleaseCampaign.Create(seed, mapProfile, terrain.Symmetry) : MvpCampaign.CreateSixNights();

        /// <summary>Stress fixture: prototype Grunts in the prototype scene; mixed MVP archetypes in the MVP scene.</summary>
        public void EnterStressMode()
        {
            Persistence?.Detach();
            MenuOpen = false;
            CreateRun(new RunConfig { Definitions = NewRunDefinitions, Wave = IsMvp ? WavePlan.CreateMvpStressWave() : WavePlan.CreateStressWave(), StressMode = true });
        }

        void CreateRun(RunConfig config)
        {
            LoadRun(new RunSimulation(config));
            if (config.StressMode) SetUpStressRun();
        }

        void LoadRun(RunSimulation sim)
        {
            Sim = sim;
            Definitions = sim.Definitions; // a loaded legacy save plays with its own profile's definitions
            observedState = sim.State;
            observedNight = sim.NightNumber;
            observedKeepHp = sim.Keep.Hp;
            World.SetTerrain(sim.Config.Terrain);
            Onboarding?.EndRun(); // only NewRun guides a run; a loaded or placeholder run never restarts the guide
            Audio?.ResetRun(sim); // pending sounds cleared; the load's own events are not replayed
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
                Persistence.BeginNewRun(Catalog);
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

        /// <summary>
        /// Starts a fresh saved run: terrain from the seed (release: from the chosen map profile; or the flat authored map),
        /// all waves stored, Day 1 checkpoint.
        /// </summary>
        public void NewRun(uint seed, bool flat)
        {
            var profile = MapProfileFor(seed);
            var terrain = flat ? TerrainMap.Flat() : IsRelease ? MapProfiles.Generate(profile, seed) : MapGenerator.Generate(seed);
            Debug.Log($"New run: {terrain.Describe()}");
            CreateRun(new RunConfig { Definitions = NewRunDefinitions, Seed = seed, Terrain = terrain, Campaign = CreateCampaign(seed, profile, terrain) });
            MenuOpen = false;
            cameraRig.ResetView(); // the Keep and the western approach
            Onboarding?.BeginRun();
            Persistence.BeginNewRun(Catalog);
            Persistence.CommitDay(Sim);
        }

        public void Continue()
        {
            var load = Persistence.Store.Read(Persistence.Store.LatestSlot(), Catalog);
            if (!load.CanContinue) return;
            LoadRun(load.Sim);
            Persistence.Adopt(load);
            MenuOpen = false;
            cameraRig.ResetView();
        }

        /// <summary>Explicit recovery of the previous checkpoint (.bak) when the latest save is damaged.</summary>
        public bool RecoverBackup()
        {
            var slot = Persistence.Store.LatestSlot();
            var load = Persistence.Store.Read(slot, Catalog, backup: true);
            if (!load.CanContinue) return false;
            LoadRun(load.Sim);
            Persistence.Adopt(load);
            Persistence.CommitDay(Sim);
            MenuOpen = false;
            cameraRig.ResetView();
            return true;
        }

        public void OpenMenu()
        {
            if (!IsMvp) return;
            MenuOpen = true;
            Player.CancelTool();
            if (!Persistence.HasRun) cameraRig.SetView(TitleFocus, TitleSize);
            var slot = Persistence.Store.LatestSlot();
            LatestSave = slot >= 0 ? Persistence.Store.Read(slot, Catalog) : new SaveLoad { Status = SaveStatus.None };
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
            ExitAfterSave = false;
            PendingConfirm = null;
            Persistence.Detach();
            EnterNormalMode();
            OpenMenu();
        }

        public bool RetrySave()
        {
            if (Persistence == null || !Persistence.Retry()) return false;
            if (ExitAfterSave) FinishExit();
            return true;
        }

        // ---------- Restart / Quit guards ----------

        /// <summary>
        /// Top-bar Restart. Prototype, ended runs and an untouched Day 1 reset immediately; a progressed MVP run first asks
        /// to confirm "Retry same seed — start Day 1".
        /// </summary>
        public void RequestRestart()
        {
            if (!IsMvp || Persistence == null || !Persistence.HasRun || Sim.IsTerminal || IsUntouchedDayOne())
            {
                Restart();
                return;
            }

            var request = new ConfirmRequest
            {
                Title = "Retry same seed — start Day 1?",
                Body = $"This run goes back to Day 1 on seed {Sim.Seed} with fresh gold, buildings and perks. " +
                       "Your current progress is replaced; its last save file is kept on disk but cannot be continued from the menu.",
            };
            request.Options.Add(("Retry from Day 1", () =>
            {
                PendingConfirm = null;
                Restart();
            }));
            request.Options.Add(("Cancel", CancelConfirm));
            OpenConfirm(request);
        }

        /// <summary>
        /// Menu "Quit game". With unsaved Day edits: save and exit / exit without them / cancel. During Night or Reward:
        /// explains that Continue replays the night from its Day checkpoint. Otherwise exits directly.
        /// </summary>
        public void RequestQuit()
        {
            if (!IsMvp || Persistence == null || !Persistence.HasRun || Sim.IsTerminal || Sim.StressMode)
            {
                QuitApplication();
                return;
            }

            if (Sim.State == RunState.Night || Sim.State == RunState.Reward)
            {
                var offer = Sim.State == RunState.Reward ? $" The reward offer for Night {Sim.NightNumber} is kept." : "";
                var nightRequest = new ConfirmRequest
                {
                    Title = "Quit during the night?",
                    Body = $"Combat is not saved. Continue will replay Night {Sim.NightNumber} from the start of Day {Sim.NightNumber}.{offer}",
                };
                nightRequest.Options.Add(("Quit game", () =>
                {
                    PendingConfirm = null;
                    QuitApplication();
                }));
                nightRequest.Options.Add(("Cancel", CancelConfirm));
                OpenConfirm(nightRequest);
                return;
            }

            if (!Persistence.HasUnsavedDayEdits(Sim))
            {
                QuitApplication();
                return;
            }

            var request = new ConfirmRequest
            {
                Title = "Save before quitting?",
                Body = $"Day {Sim.NightNumber} has changes since your last save (buildings, gold or repairs).",
            };
            request.Options.Add(("Save and exit", ConfirmSaveAndExit));
            request.Options.Add(("Exit without these changes", () =>
            {
                PendingConfirm = null;
                QuitApplication();
            }));
            request.Options.Add(("Cancel", CancelConfirm));
            OpenConfirm(request);
        }

        /// <summary>Commits this exact Day through the normal save owner; exits only once that snapshot is durable.</summary>
        public void ConfirmSaveAndExit()
        {
            PendingConfirm = null;
            ExitAfterSave = true;
            if (Persistence.CommitDay(Sim)) FinishExit();
            // On failure the save-failed overlay offers Retry (exits after success) or leaving without saving.
        }

        void FinishExit()
        {
            ExitAfterSave = false;
            QuitApplication();
        }

        public void CancelConfirm()
        {
            PendingConfirm = null;
            ExitAfterSave = false;
        }

        void OpenConfirm(ConfirmRequest request)
        {
            Player.CancelTool();
            PendingConfirm = request;
        }

        /// <summary>Day 1 exactly as created: nothing built, sold, repaired or rotated since the run began.</summary>
        bool IsUntouchedDayOne() =>
            Sim.State == RunState.Day && Sim.NightNumber == 1 && Sim.Stats.BuildingsPurchased == 0 && Sim.Gold == Sim.Definitions.StartingGold
            && !Persistence.HasUnsavedDayEdits(Sim) && Persistence.LastDay != null && Persistence.LastDay.buildings.Length == 1;

        public void ApplySettings(GameSettings settings, bool persist)
        {
            Settings = settings;
            cameraRig.Sensitivity = Mathf.Clamp(settings.cameraSensitivity, 0.25f, 3f);
            cameraRig.ShakeEnabled = settings.cameraShake;
            Audio?.ApplyVolumes(settings.masterVolume, settings.musicVolume, settings.sfxVolume);
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
            if (Persistence != null && Persistence.HasRun && (Persistence.SavePending || !Persistence.CommitDay(Sim)))
            {
                Audio?.Play(SoundId.UiReject);
                return false;
            }

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
            if (Player.Selected != null && Sim.TryRepair(Player.Selected) != RepairRejection.None) Audio?.Play(SoundId.UiReject);
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
