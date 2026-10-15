using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastFortress
{
    /// <summary>
    /// Single scene entry point. Owns the authoritative <see cref="RunSimulation"/> and drives input, views and HUD.
    /// Normal mode is the default; the stress fixture is entered only via -stress, F9, or <see cref="EnterStressMode"/>.
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

        /// <summary>When false, Update does not advance the simulation (tests step it explicitly).</summary>
        public bool AutoAdvance { get; set; } = true;

        void Awake()
        {
            if (content == null || cameraRig == null) throw new InvalidOperationException("PrototypeBootstrap is missing scene references; run The Last Fortress > Setup Prototype Scene.");
            Definitions = content.definitions.ToDefinitions();
            var errors = Definitions.Validate();
            if (errors.Count > 0) Debug.LogError("Invalid prototype definitions: " + string.Join("; ", errors));

            Player = new PlayerController(this);
            World = new GameObject("World").AddComponent<WorldView>();
            World.transform.SetParent(transform, false);
            World.Initialize(content, sun);
            Overlay = new GameObject("RouteOverlay").AddComponent<RouteOverlay>();
            Overlay.transform.SetParent(transform, false);
            Overlay.Initialize(content);
            Hud = gameObject.AddComponent<HudView>();
            Hud.Build(this);

            var args = Environment.GetCommandLineArgs();
            if (HasArg(args, "-stress")) EnterStressMode();
            else EnterNormalMode();

            var layout = ArgValue(args, "-layout");
            if (!IsStressMode && layout != null)
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
            if (kb != null && kb.f9Key.wasPressedThisFrame)
            {
                if (IsStressMode) EnterNormalMode();
                else EnterStressMode();
            }

            Player.Tick(dt);
            if (AutoAdvance) Sim.Advance(dt);
            SyncViews(dt);
        }

        /// <summary>Pushes current simulation state to views and HUD (also used by tests after manual stepping).</summary>
        public void SyncViews(float dt = 0f)
        {
            World.SetBuildGridVisible(Player.Tool.HasValue && Sim.State == RunState.Day);
            World.Sync(Sim, dt);
            Overlay.Refresh(Sim, Player);
            Hud.Refresh(Sim, Player, Overlay, cameraRig);
        }

        // ---------- commands (HUD buttons, hotkeys, tests) ----------

        public void EnterNormalMode() => CreateRun(new RunConfig { Definitions = Definitions, Wave = WavePlan.CreateMainWave() });

        public void EnterStressMode() => CreateRun(new RunConfig { Definitions = Definitions, Wave = WavePlan.CreateStressWave(), StressMode = true });

        void CreateRun(RunConfig config)
        {
            Sim = new RunSimulation(config);
            ResetPresentation();
            if (config.StressMode) SetUpStressRun();
        }

        void SetUpStressRun()
        {
            var layout = content.stressLayout != null ? LayoutJson.Parse(content.stressLayout) : LayoutFixture.CreateStressLayout();
            var failures = Sim.ApplyLayout(layout, bypassCost: true);
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

        /// <summary>Restores the exact initial state of the current mode.</summary>
        public void Restart()
        {
            Sim.Restart();
            ResetPresentation();
            if (Sim.StressMode) SetUpStressRun();
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

        public bool StartNight()
        {
            var started = Sim.StartNight();
            if (!started) return false;
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
