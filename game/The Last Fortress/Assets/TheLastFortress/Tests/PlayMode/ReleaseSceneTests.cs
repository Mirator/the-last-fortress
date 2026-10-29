using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TheLastFortress.Tests
{
    /// <summary>
    /// Plan 008 release scene integration: the new kinds through real input, overlay and HUD; every new threat event
    /// reaching the world view and audio within their budgets; and an old six-night save continuing in the release build.
    /// Saves go to a temporary directory, never the player's real saves.
    /// </summary>
    public class ReleaseSceneTests
    {
        PrototypeBootstrap game;
        string saveDir;
        Mouse mouse;
        Keyboard keyboard;
        InputSettings.EditorInputBehaviorInPlayMode previousBehavior;
        InputSettings.BackgroundBehavior previousBackground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>("TestMouse");
            keyboard = InputSystem.AddDevice<Keyboard>("TestKeyboard");
            saveDir = Path.Combine(Path.GetTempPath(), "tlf-release-" + System.Guid.NewGuid().ToString("N"));
            SaveStore.DirectoryOverride = saveDir;
            SceneManager.LoadScene("Release", LoadSceneMode.Single);
            yield return null;
            yield return null;
            game = Object.FindFirstObjectByType<PrototypeBootstrap>();
            Assert.IsNotNull(game, "bootstrap present");
            game.Player.MouseOverride = mouse;
            game.Player.KeyboardOverride = keyboard;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior;
            InputSystem.settings.backgroundBehavior = previousBackground;
            SaveStore.DirectoryOverride = null;
            if (Directory.Exists(saveDir)) Directory.Delete(saveDir, true);
            yield return null;
        }

        IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        IEnumerator Click(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, true));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
        }

        /// <summary>Writes a Day-1 save and continues it through the menu, as a player would after relaunching.</summary>
        void ContinueSaved(RunSimulation sim)
        {
            Assert.IsTrue(game.Persistence.Store.WriteRun(0, CheckpointCodec.Capture(sim), out var error), error);
            game.OpenMenu();
            Assert.IsTrue(game.LatestSave.CanContinue, game.LatestSave.Message);
            game.Continue();
            Assert.IsFalse(game.MenuOpen);
        }

        [UnityTest]
        public IEnumerator NewKindsPlaceThroughInputAndEveryNewThreatIsPresentedWithinBudgets()
        {
            Assert.IsTrue(game.IsRelease && game.MenuOpen);
            Assert.AreEqual("", game.MapProfile, "by default the seed picks the map");
            game.Hud.MapButton.onClick.Invoke();
            game.Hud.MapButton.onClick.Invoke();
            Assert.AreEqual(MapProfiles.Crossing, game.MapProfile, "the title cycles by seed → Ravine → Crossing for the next New Run");
            game.NewRun(7, false);
            yield return null;
            Assert.AreEqual((12, MapProfiles.Crossing, MapProfiles.Crossing), (game.Sim.TotalNights, game.Sim.Campaign.MapProfile, game.Sim.Config.Terrain.Profile));
            Assert.AreEqual(8, RulesProfile.ReleaseKinds.Count(k => game.Hud.CardFor(k).gameObject.activeSelf), "all eight build cards");

            // A custom stored night with every 008 threat, loaded through the real save path.
            // The Bomber and the Raider Archer come from the undefended east so they live to light a fuse and to shoot (at
            // release-rules-3 walking speed an archer crossing the Cannon's cover dies first); the rest walk into the Cannon.
            var wave = new WavePlan("threats", new[]
            {
                new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.Grunt), new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.Grunt),
                new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.Grunt), new SpawnEntry(10, MvpCampaign.East, 1, EnemyKind.Bomber),
                new SpawnEntry(10, MvpCampaign.East, 1, EnemyKind.Archer), new SpawnEntry(20, MvpCampaign.West, 0, EnemyKind.SiegeCommander),
            }, new[] { MvpCampaign.West, MvpCampaign.East });
            var campaign = new CampaignConfig
            {
                Label = "threats", Nights = new[] { wave, new WavePlan("calm", new SpawnEntry[0], new[] { MvpCampaign.West }) }, OffersRewards = true,
                RulesVersion = ReleaseCampaign.RulesVersion, CampaignId = ReleaseCampaign.CampaignId, PerkPool = PerkRules.ReleaseIds, MapProfile = MapProfiles.Meadow,
            };
            ContinueSaved(new RunSimulation(new RunConfig { Definitions = game.NewRunDefinitions, Seed = 7, Terrain = TerrainMap.Flat(), Campaign = campaign }));
            yield return null;

            yield return Press(Key.Digit6);
            Assert.AreEqual(BuildingKind.Cannon, game.Player.Tool, "hotkey 6 = Cannon");
            yield return Click(game.CameraRig.CellToScreen(new GridPos(6, 8)));
            var cannon = game.Sim.Grid.GetOccupant(new GridPos(6, 8));
            Assert.IsNotNull(cannon, "Cannon placed through the Input System");
            Assert.AreEqual((BuildingKind.Cannon, new GridPos(6, 8)), (cannon.Kind, cannon.Cell));
            Assert.IsNull(game.Sim.Grid.GetOccupant(new GridPos(7, 9)), "release-rules-4: the Cannon takes one tile (owner, 2026-10-21)");
            Assert.AreEqual(1, game.World.CountBuildingViews(BuildingKind.Cannon), "one view");

            game.AutoAdvance = false;
            yield return Press(Key.Digit7);
            game.Player.HoverOverride = new GridPos(7, 8); // beside the 1 x 1 Cannon
            yield return null; // the hover is read on the next player tick
            Assert.AreEqual(1, game.Overlay.BuffMarkCount, "the Watchtower ghost outlines the Cannon it would boost");
            game.Player.HoverOverride = null;
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.Watchtower, new GridPos(7, 8), out _));
            game.Player.CancelTool();
            game.Player.Select(cannon);
            game.SyncViews();
            Assert.AreEqual((float)(game.Sim.Definitions.Cannon.Range + game.Sim.Definitions.Watchtower.RangeBonus), game.Overlay.RangeRingRadius, 1e-4f, "ring = base + Watchtower bonus from Core");
            StringAssert.Contains("Watchtower +" + game.Sim.Definitions.Watchtower.RangeBonus.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), game.Hud.SelectionText);

            Assert.IsTrue(game.StartNight());
            bool shell = false, blast = false, fuse = false, aura = false, enemyArrow = false, boss = false;
            int maxVoices = 0, maxFamily = 0;
            for (var i = 0; i < 4000 && game.Sim.State == RunState.Night; i++)
            {
                game.Sim.Step();
                game.SyncViews(0.05f);
                shell |= game.World.ActiveShellCount > 0;
                blast |= game.World.VisibleCues().Any(c => c.kind == CueKind.Blast);
                foreach (var e in game.Sim.Enemies)
                {
                    fuse |= e.FuseLit && game.World.FuseVisible(e.Id);
                    aura |= e.Kind == EnemyKind.SiegeCommander && game.World.AuraVisible(e.Id);
                }

                enemyArrow |= game.Audio.PlayCount(SoundId.RaiderShot) > 0;
                boss |= game.Hud.BossPanel.activeSelf;
                maxVoices = Mathf.Max(maxVoices, game.Audio.ActiveVoices);
                foreach (SoundId id in System.Enum.GetValues(typeof(SoundId))) maxFamily = Mathf.Max(maxFamily, game.Audio.ActiveVoicesOf(id));
            }

            Assert.AreNotEqual(RunState.Night, game.Sim.State, "the night resolves within the step bound");
            Assert.IsTrue(shell && blast, "Cannon shells fly and land with a splash ring");
            Assert.IsTrue(fuse, "a lit Bomber shows its fuse");
            Assert.IsTrue(aura, "the Commander shows its aura");
            Assert.IsTrue(enemyArrow, "a Raider Archer stopped to shoot");
            Assert.IsTrue(boss, "the boss bar covers the Siege Commander");
            Assert.LessOrEqual(maxVoices, AudioDirector.MaxVoices);
            Assert.LessOrEqual(maxFamily, AudioDirector.MaxPerFamily);
            Assert.LessOrEqual(game.World.PendingCueCount, WorldView.CueCaps.Sum(), "cues stay pooled and capped");
        }

        /// <summary>Spec 015: a Tower selected by click goes to tier III through U and the button by Day; Night refuses with its reason.</summary>
        [UnityTest]
        public IEnumerator ASelectedTowerUpgradesTwiceByDayAndNightRefusesWithItsReason()
        {
            ContinueSaved(new RunSimulation(new RunConfig
            {
                Definitions = game.NewRunDefinitions, Seed = 7, Terrain = TerrainMap.Flat(), Campaign = ReleaseCampaign.Create(7, MapProfiles.Meadow),
            }));
            yield return null;
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.Tower, new GridPos(8, 9), out var tower));
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.Tower, new GridPos(12, 11), out var other));
            yield return Click(game.CameraRig.CellToScreen(tower.Cell));
            Assert.AreSame(tower, game.Player.Selected, "selected by a click");
            game.SyncViews();
            StringAssert.Contains("Next, tier II for 49 gold", game.Hud.SelectionText);
            var (gold, damage) = (game.Sim.Gold, game.Sim.TowerDamage(tower));

            yield return Press(Key.U);
            Assert.AreEqual((2, gold - 49), (tower.Tier, game.Sim.Gold), "U upgrades by Day");
            Assert.Greater(game.Sim.TowerDamage(tower), damage);
            game.SyncViews();
            Assert.IsTrue(game.Hud.UpgradeButton.gameObject.activeSelf && game.Hud.UpgradeButton.interactable);
            StringAssert.Contains("73 gold", game.Hud.UpgradeLabel);
            game.Hud.UpgradeButton.onClick.Invoke();
            Assert.AreEqual((3, gold - 49 - 73), (tower.Tier, game.Sim.Gold), "the button upgrades too");
            game.SyncViews();
            Assert.AreEqual((false, "Maximum tier"), (game.Hud.UpgradeButton.interactable, game.Hud.UpgradeLabel));
            Assert.AreEqual(2, game.World.TierPipCount(tower.Id), "two pips at tier III");
            Assert.AreEqual(0, game.World.TierPipCount(other.Id), "none at tier I");

            game.Player.Select(other);
            Assert.IsTrue(game.StartNight());
            gold = game.Sim.Gold;
            yield return Press(Key.U);
            game.SyncViews();
            Assert.AreEqual((1, gold), (other.Tier, game.Sim.Gold), "no upgrade at Night");
            Assert.IsFalse(game.Hud.UpgradeButton.interactable);
            StringAssert.Contains("only by day", game.Hud.UpgradeLabel);
        }

        [UnityTest]
        public IEnumerator SixNightSaveContinuesInTheReleaseBuildWithItsOwnRulesAndCards()
        {
            var mvp = new RunSimulation(new RunConfig
            {
                Definitions = MvpDefinitions.CreateDefault(), Seed = 9, Terrain = TerrainMap.Flat(), Campaign = MvpCampaign.CreateSixNights(),
            });
            ContinueSaved(mvp);
            yield return null;
            Assert.AreEqual((6, MvpCampaign.RulesVersion), (game.Sim.TotalNights, game.Sim.Campaign.RulesVersion), "never converted to twelve nights");
            game.SyncViews();
            Assert.IsTrue(game.Hud.CardFor(BuildingKind.Tower).gameObject.activeSelf);
            Assert.IsFalse(game.Hud.CardFor(BuildingKind.Cannon).gameObject.activeSelf, "release cards hidden for a six-night run");
            yield return Press(Key.Digit6);
            Assert.IsNull(game.Player.Tool, "the Cannon hotkey does nothing in a six-night run");
            yield return Press(Key.Digit2);
            Assert.AreEqual(BuildingKind.Tower, game.Player.Tool);

            Assert.IsTrue(game.StartNight(), "Night commits the Day checkpoint first");
            StringAssert.Contains("\"schemaVersion\": 1", File.ReadAllText(game.Persistence.Store.SlotPath(0)), "still saved as a schema-1 six-night run");
        }

        [UnityTest]
        public IEnumerator NightSpeedRunsMoreFixedStepsAndPauseHoldsBothClocks()
        {
            game.NewRun(7, false);
            game.AutoAdvance = false;
            yield return null;
            Assert.IsTrue(game.StartNight());
            game.Hud.SpeedButton(3).onClick.Invoke();
            Assert.AreEqual(6, game.AdvanceSimulation(0.1f), "3×: three times the 0.05 s steps for the same real time");
            Assert.AreEqual(0.3f, game.PresentationDelta(0.1f), 1e-5f, "arrows and impacts keep step with the faster simulation");

            game.Hud.SpeedButton(0).onClick.Invoke();
            Assert.IsTrue(game.Sim.Paused);
            Assert.AreEqual(0, game.AdvanceSimulation(0.1f), "paused at any speed: no steps");
            Assert.AreEqual(0f, game.PresentationDelta(0.1f), "paused: presentation frozen");

            yield return Press(Key.F); // 3× wraps to 1× and resumes
            Assert.AreEqual((1, false), (game.GameSpeed, game.Sim.Paused));
            Assert.AreEqual(2, game.AdvanceSimulation(0.1f));
        }

        [UnityTest]
        public IEnumerator EntryMarkersStandAtTonightsEntriesAndChevronsFlowTowardTheKeep()
        {
            game.NewRun(7, false); // a turned board: Night 1 does not come from the West
            yield return null;
            var entries = game.Sim.Wave.EntryCells;
            Assert.AreEqual(entries.Count, game.Overlay.VisibleEntryCount);
            var marker = game.Overlay.EntryMarkerPosition(0);
            Assert.Less(Vector3.Distance(marker, Prims.CellCenter(entries[0], marker.y)), 1.2f, "the marker stands at tonight's entry");
            Assert.IsTrue(marker.x < 0f || marker.x > 20f || marker.z < 0f || marker.z > 20f, "its torch on the road, never on a legal cell");

            Assert.Greater(game.Overlay.VisibleChevronCount, 3);
            var keep = Prims.CellCenter(GridState.KeepCell);
            var toward = 0f;
            for (var i = 0; i < game.Overlay.VisibleChevronCount; i++)
            {
                var c = game.Overlay.Chevron(i);
                toward += Vector3.Dot(c.forward, (keep - c.position).normalized);
            }

            Assert.Greater(toward, 0f, "chevrons point from the entries toward the Keep, not back out");
            Assert.IsTrue(game.StartNight());
            yield return null;
            Assert.AreEqual((entries.Count, 0), (game.Overlay.VisibleEntryCount, game.Overlay.VisibleChevronCount), "entry markers stay at Night; route chevrons are a Day preview");

            // Spec 010: Night start eases the strategic camera out until the Keep and tonight's entries clear the HUD bars.
            var rig = game.CameraRig;
            Assert.IsTrue(rig.Easing, "Start Night eases to the framing");
            var until = Time.realtimeSinceStartup + 3f; // the ease runs on real time (0.6 s)
            while (rig.Easing && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse(rig.Easing, "the framing finished within 3 s");
            Assert.Greater(Vector3.Distance(rig.Focus, CameraRig.DefaultFocus), 1f, "the entry is off the default view, so the camera moved");
            void AssertInSafeArea(System.Collections.Generic.IEnumerable<Vector3> points)
            {
                foreach (var p in points)
                {
                    var v = rig.Camera.WorldToViewportPoint(p);
                    Assert.IsTrue(v.x > 0.099f && v.x < 0.901f && v.y > 0.179f && v.y < 0.821f, $"{p} framed at {v}, outside the safe area");
                }
            }

            var framed = new System.Collections.Generic.List<Vector3> { keep };
            foreach (var entry in entries) framed.Add(Prims.CellCenter(entry));
            AssertInSafeArea(framed);
            var twoSided = new[] { keep, Prims.CellCenter(new GridPos(0, 10)), Prims.CellCenter(new GridPos(20, 10)) }; // West and East
            rig.FrameNight(twoSided, false);
            Assert.Greater(rig.Size, CameraRig.DefaultSize, "two opposite entries need a wider view");
            AssertInSafeArea(twoSided);

            rig.FrameNight(new[] { keep, new Vector3(20.5f, 0f, 20.5f) }, true);
            yield return null;
            rig.ResetView(); // Reset Camera during an ease wins
            for (var i = 0; i < 40; i++) yield return null;
            Assert.AreEqual((CameraRig.DefaultFocus, CameraRig.DefaultSize, false), (rig.Focus, rig.Size, rig.Easing), "Reset Camera ends the framing");
        }

        /// <summary>
        /// Plan 016: an enemy walks in out of the mist along its road before it spawns, and the real enemy takes over where
        /// the walk-in stood, without a pop (a missed hand-over would jump by the lane offset or the whole road).
        /// </summary>
        [UnityTest]
        public IEnumerator EnemiesWalkInAlongTheRoadAndTakeOverWithoutAJump()
        {
            game.NewRun(7, false);
            game.AutoAdvance = false;
            yield return null;
            Assert.IsTrue(game.StartNight());
            var spawn = game.Sim.Wave.Spawns[3]; // a few spawns in: long enough after the start to walk the whole road
            var early = spawn.Tick - 60;
            for (var i = 0; i < 400 && game.Sim.Tick < early; i++) game.AdvanceSimulation(0.05f);
            yield return null;
            var walking = game.World.ArrivalShownPosition(3);
            Assert.IsTrue(walking.HasValue, "three seconds before its spawn the enemy is already walking in");
            var entry = Prims.CellCenter(spawn.Cell);
            Assert.Greater(Vector2.Distance(new Vector2(walking.Value.x, walking.Value.z), new Vector2(entry.x, entry.z)), 0.4f, "out on the road, not at the entry yet");

            // One step and one frame at a time, until the step that spawns it: remember where the walk-in was drawn last.
            Vector3? last = null;
            for (var i = 0; i < 200 && game.Sim.SpawnedCount <= 3; i++)
            {
                last = game.World.ArrivalShownPosition(3);
                game.AdvanceSimulation(0.05f);
                if (game.Sim.SpawnedCount <= 3) yield return null;
            }

            Assert.IsTrue(last.HasValue, "it walked until its spawn");
            Assert.AreEqual(4, game.Sim.SpawnedCount);
            var enemy = game.Sim.Enemies[game.Sim.Enemies.Count - 1];
            yield return null;
            var shown = game.World.EnemyShownPosition(enemy.Id);
            Assert.IsTrue(shown.HasValue);
            Assert.IsFalse(game.World.ArrivalShownPosition(3).HasValue, "the walk-in is gone once the enemy exists");
            Assert.Less(Vector2.Distance(new Vector2(shown.Value.x, shown.Value.z), new Vector2(last.Value.x, last.Value.z)), 0.06f,
                "the enemy appears where its walk-in stood");
        }

        /// <summary>
        /// Plan 012: switching views is presentation only (specs/003 3.1). An untoggled twin restored from the same Start
        /// Night checkpoint must stay identical while Tab is pressed repeatedly, at 3x and while paused; the hero view locks
        /// 1x without resuming a pause. In the hero view, held W moves the hero and F leaves the speed alone.
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchingViewsNeverChangesTheNightAndTheHeroMovesWithInput()
        {
            static string Snapshot(RunSimulation s) => System.Text.RegularExpressions.Regex.Replace(s.CaptureSnapshot(), @" acc=\d+", "");
            game.NewRun(11, false);
            game.AutoAdvance = false;
            yield return null;
            Assert.IsTrue(game.Sim.HasHero, "every new release run has the hero");
            var json = JsonUtility.ToJson(CheckpointCodec.Capture(game.Sim, nightStart: true));
            var twin = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(json), game.Catalog, out var problem, out var message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            Assert.IsTrue(game.StartNight());
            Assert.IsTrue(twin.StartNight());
            game.Hud.SpeedButton(3).onClick.Invoke();
            game.Player.HeroInputOverride = HeroCommand.None; // the same (absent) command in both views: only presentation differs
            var markersSeen = new int[2]; // frames checked with enemies alive: strategic, hero camera

            for (var frame = 0; frame < 160 && game.Sim.State == RunState.Night; frame++)
            {
                if (frame % 9 == 0)
                {
                    var wasPaused = game.Sim.Paused;
                    yield return Press(Key.Tab);
                    Assert.AreEqual(wasPaused, game.Sim.Paused, "a switch never pauses or resumes");
                    if (game.HeroViewActive) Assert.AreEqual(1, game.GameSpeed, "the hero view runs at 1x");
                }

                if (frame == 40 || frame == 70) game.TogglePause();
                if (frame == 100 && !game.HeroViewActive) game.Hud.SpeedButton(3).onClick.Invoke();
                var steps = game.AdvanceSimulation(0.1f);
                for (var i = 0; i < steps; i++) twin.Step(HeroCommand.None);
                game.SyncViews(0.1f);
                // Spec 009: one class marker per living enemy while the strategic camera shows, none through the hero camera
                // (they fade in and out while it blends to or from the strategic projection).
                var heroCamera = game.World.HeroCameraShown;
                if (!heroCamera || game.World.StrategicBlend <= 0f)
                    Assert.AreEqual(heroCamera ? 0 : game.Sim.AliveEnemyCount, game.World.VisibleMarkerCount, $"class markers (hero camera {heroCamera}, frame {frame})");
                if (game.Sim.AliveEnemyCount > 0) markersSeen[heroCamera ? 1 : 0]++;
            }

            Assert.IsTrue(markersSeen[0] > 0 && markersSeen[1] > 0, $"markers checked with enemies alive in both views ({markersSeen[0]}/{markersSeen[1]})");
            Assert.AreEqual(Snapshot(twin), Snapshot(game.Sim), "toggling views changed the simulation");

            // A fresh Day (the undefended night above may have ended): the hero walks on its own Day clock.
            game.Player.HeroInputOverride = null;
            game.NewRun(12, false);
            yield return null;
            Assert.IsFalse(game.HeroViewActive, "a new run starts in the strategic view");
            yield return Press(Key.Tab);
            Assert.IsTrue(game.HeroViewActive);
            Assert.IsTrue(Camera.main == game.HeroRig.Camera && !Camera.main.orthographic, "the perspective hero camera renders");
            // Owner's choice (2026-10-20, aiming for a future bow): with Invert look off (the default), pushing the mouse forward looks up.
            game.HeroRig.SkipTransition();
            game.SyncViews(0.02f);
            var pitchBefore = game.HeroRig.Pitch;
            var lookBefore = game.HeroRig.transform.forward.y;
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(0f, 60f) });
            yield return null;
            game.SyncViews(0.02f);
            Assert.IsFalse(game.Settings.invertLook, "Invert look is off by default");
            Assert.Less(game.HeroRig.Pitch, pitchBefore, "mouse forward tilts the view up");
            Assert.Greater(game.HeroRig.transform.forward.y, lookBefore, "the camera looks further up after pushing the mouse forward");
            Assert.IsTrue(game.Hud.CrosshairShown, "the hero view shows its aiming crosshair");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            var before = new Vector2((float)game.Sim.Hero.X, (float)game.Sim.Hero.Z);
            var shownBefore = game.HeroView.ShownPosition;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return null; // the player tick samples the held key into this frame's hero command
            // Responsiveness (specs/003 3.3): within the first frame, before any whole step, the shown hero already moves.
            Assert.AreEqual(0, game.AdvanceSimulation(0.02f));
            game.SyncViews(0.02f);
            Assert.AreEqual(before, new Vector2((float)game.Sim.Hero.X, (float)game.Sim.Hero.Z), "no simulation step yet");
            // release-rules-4 accelerates from rest (14 units in the first step), so the first 0.02 s shows about 0.005 tile.
            Assert.Greater(Vector3.Distance(shownBefore, game.HeroView.ShownPosition), 0.002f, "the presented hero moves on the first frame");
            Assert.AreEqual(10, game.AdvanceSimulation(0.5f), "half a second of Day walking: ten hero steps");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.Greater(Vector2.Distance(before, new Vector2((float)game.Sim.Hero.X, (float)game.Sim.Hero.Z)), 0.05f, "held W moved the hero");
            yield return Press(Key.F);
            Assert.AreEqual(1, game.GameSpeed, "F does nothing in the hero view");
            yield return Press(Key.Escape);
            Assert.IsTrue(game.MenuOpen && !game.CursorLocked, "Esc opens the menu and frees the cursor");
            yield return null;
            Assert.IsFalse(game.Hud.CrosshairShown, "no crosshair over the menu");
        }

        /// <summary>
        /// Plan 019 (specs/003 section 6): with the camera behind the hero over the Keep, at the field-of-view and distance
        /// extremes, every shown pose keeps its near-plane volume out of the drawn buildings and the ground, the Keep pulls
        /// the camera in, and the distance settles instead of pumping; the eased entry from the strategic view stays clear
        /// or is cut to the validated pose.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHeroCameraKeepsItsNearPlaneOutOfBuildingsAndSettles()
        {
            game.NewRun(14, false);
            game.AutoAdvance = false;
            yield return null;
            yield return Press(Key.Tab); // the spawn cell is north of the Keep: looking north puts the camera over it
            Assert.IsTrue(game.HeroViewActive);
            foreach (var (fov, distance) in new[] { (50f, 0.5f), (90f, 2.2f), (50f, 2.2f), (90f, 0.5f) })
            {
                game.HeroRig.FieldOfView = fov;
                game.HeroRig.SetDefaultDistance(distance);
                game.HeroRig.SkipTransition();
                game.HeroRig.SetOrbit(0f, 16f);
                var shown = new System.Collections.Generic.List<float>();
                for (var i = 0; i < 40; i++)
                {
                    game.SyncViews(1f / 30f);
                    Assert.IsTrue(game.HeroRig.ShownPoseClear(), $"fov {fov}, distance {distance}, frame {i}: the near plane entered a blocker");
                    shown.Add(game.HeroRig.ShownDistance);
                }

                Assert.Greater(game.HeroRig.Obstacles.Boxes.Count, 0, "the drawn trees and boulders are blockers");
                if (distance > 1f)
                {
                    // Spec 013: the Keep behind the hero no longer pulls the camera in; it fades instead.
                    Assert.GreaterOrEqual(game.HeroRig.ShownDistance, distance - 0.05f, "the Keep behind pulled the camera in");
                    Assert.Greater(game.HeroFade.FadedCount, 0, "the Keep between the camera and the hero is not faded");
                }
                var turns = 0;
                for (var i = 12; i < shown.Count; i++)
                    if (Mathf.Sign(shown[i] - shown[i - 1]) * Mathf.Sign(shown[i - 1] - shown[i - 2]) < 0f) turns++;
                Assert.LessOrEqual(turns, 1, $"fov {fov}, distance {distance}: the distance pumps");
            }

            // The hero pressed against a drawn Tower (hero collision allows 0.06 tile from its box, inside the near-plane
            // padding), looking at it: the pivot steps off the surface and the camera stays behind the hero instead of falling
            // back to the hero's head (found in the 2026-10-28 captures).
            game.HeroRig.FieldOfView = 60f;
            game.HeroRig.SetDefaultDistance(1f);
            Building tower = null;
            foreach (var cell in new[] { new GridPos(8, 7), new GridPos(12, 7), new GridPos(8, 13), new GridPos(12, 13) })
                if (game.Sim.TryPlace(BuildingKind.Tower, cell, out tower) == PlacementRejection.None) break;
            Assert.IsNotNull(tower, "a Tower was placed");
            GridCoordinates.TileCenter(tower.Cell, out var tx, out var tz);
            var hero = game.Sim.Hero;
            hero.UnitsX = hero.PrevUnitsX = (int)System.Math.Round((tx + 0.49) * HeroRules.UnitsPerTile); // touching its east side
            hero.UnitsZ = hero.PrevUnitsZ = (int)System.Math.Round(tz * HeroRules.UnitsPerTile);
            game.HeroRig.SkipTransition();
            game.HeroRig.SetOrbit(270f, 10f); // looking west at the Tower, the camera out east
            var fallbacks = game.HeroRig.FallbackFrames;
            for (var i = 0; i < 20; i++) game.SyncViews(1f / 30f);
            Assert.AreEqual(fallbacks, game.HeroRig.FallbackFrames, "against the Tower the camera fell back to a held or in-head pose");
            Assert.IsTrue(game.HeroRig.ShownPoseClear(), "against the Tower: the near plane entered a blocker");
            Assert.Greater(game.HeroRig.ShownDistance, 0.2f, "against the Tower the camera stays behind the hero");
            game.HeroRig.SetOrbit(90f, 10f); // back to the Tower: the camera's line runs through it
            for (var i = 0; i < 20; i++) game.SyncViews(1f / 30f);
            Assert.Greater(game.HeroRig.ShownDistance, 0.9f, "with its back to the Tower the camera was pulled in");
            Assert.Greater(game.HeroFade.FadedCount, 0, "the Tower behind the hero is not faded");

            // Spec 011: a Wall never pulls the camera in; the Wall in front of the camera fades instead of hiding the hero.
            Building wall = null;
            foreach (var cell in new[] { new GridPos(12, 13), new GridPos(13, 12), new GridPos(7, 13), new GridPos(13, 7) })
                if (game.Sim.TryPlace(BuildingKind.Wall, cell, out wall) == PlacementRejection.None) break;
            Assert.IsNotNull(wall, "a Wall was placed");
            game.SyncViews(0f);
            // The drawn palisade's box: the hero stands just outside its thin side, looking away, so the camera is behind it.
            var meshes = new System.Collections.Generic.List<Renderer>();
            game.World.CollectFadeCandidates(meshes);
            var palisade = new Bounds();
            var found = false;
            foreach (var r in meshes)
            {
                var c = r.bounds.center;
                if (c.x < wall.Cell.X || c.x > wall.Cell.X + 1 || c.z < wall.Cell.Z || c.z > wall.Cell.Z + 1) continue;
                if (found) palisade.Encapsulate(r.bounds);
                else palisade = r.bounds;
                found = true;
            }

            Assert.IsTrue(found, "the Wall is drawn");
            var acrossX = palisade.size.x <= palisade.size.z;
            var stand = acrossX ? new Vector3(palisade.max.x + 0.08f, 0f, palisade.center.z) : new Vector3(palisade.center.x, 0f, palisade.max.z + 0.08f);
            hero.UnitsX = hero.PrevUnitsX = (int)System.Math.Round(stand.x * HeroRules.UnitsPerTile);
            hero.UnitsZ = hero.PrevUnitsZ = (int)System.Math.Round(stand.z * HeroRules.UnitsPerTile);
            game.HeroRig.SkipTransition();
            game.HeroRig.SetOrbit(acrossX ? 90f : 0f, 10f); // looking away from the Wall: the camera's line runs through it
            var keptYaw = game.HeroRig.Yaw;
            for (var i = 0; i < 20; i++) game.SyncViews(1f / 30f);
            Assert.Greater(game.HeroRig.ShownDistance, 0.5f, "the Wall pulled the camera in");
            Assert.Greater(game.HeroFade.FadedCount, 0, "the Wall hiding the hero is not faded");

            yield return Press(Key.Tab);
            Assert.IsFalse(game.HeroViewActive);
            var exitUntil = Time.realtimeSinceStartup + 3f;
            while ((game.HeroFade.FadedCount > 0 || game.ExitingHeroView) && Time.realtimeSinceStartup < exitUntil) yield return null;
            Assert.AreEqual(0, game.HeroFade.FadedCount, "the strategic view shows a faded mesh");
            Assert.IsFalse(game.ExitingHeroView, "the exit finished");
            // The last hero-camera frame is the strategic image: no jump when the views swap, on the ground or above it.
            var strategic = game.CameraRig.Camera;
            foreach (var p in new[] { game.CameraRig.Focus, game.CameraRig.Focus + new Vector3(3f, 0f, -2f), game.CameraRig.Focus + new Vector3(-2f, 1.5f, 1f) })
            {
                Vector2 shown = game.HeroRig.Camera.WorldToScreenPoint(p), swapped = strategic.WorldToScreenPoint(p);
                Assert.Less(Vector2.Distance(shown, swapped), 1f, $"{p} moves at the swap: {shown} -> {swapped}");
            }

            yield return Press(Key.Tab); // eased entry, not skipped
            for (var i = 0; i < 20; i++)
            {
                game.SyncViews(1f / 30f);
                var cam = game.HeroRig.transform.position;
                Assert.IsFalse(game.HeroRig.Obstacles.Blocked(cam, 0f), $"entry frame {i}: the camera entered a blocker");
                Assert.IsTrue(game.HeroRig.InTransition || game.HeroRig.ShownPoseClear(), $"entry frame {i}: the settled pose is not clear");
            }

            Assert.IsFalse(game.HeroRig.InTransition, "the entry finished within 0.35 s");
            Assert.AreEqual(keptYaw, game.HeroRig.Yaw, 1e-3f, "the orbit is kept within the run");
        }

        /// <summary>
        /// Plan 019 (specs/003 section 6): losing window focus frees the cursor and stops the night without touching the
        /// player's pause; after focus returns or the menu closes, the first click only recaptures the cursor and never
        /// attacks, even while held, and a later tap between steps is kept until the hero accepts it.
        /// </summary>
        [UnityTest]
        public IEnumerator FocusLossAndMenuReturnRecaptureWithAClickThatNeverAttacks()
        {
            game.NewRun(13, false);
            game.AutoAdvance = false;
            yield return null;
            Assert.IsTrue(game.StartNight());
            yield return Press(Key.Tab);
            Assert.IsTrue(game.HeroViewActive && game.CursorLocked);
            var hero = game.Sim.Hero;
            game.SetApplicationFocus(false);
            Assert.IsTrue(game.FocusSuspended && !game.CursorLocked, "focus loss frees the cursor");
            Assert.AreEqual(0, game.AdvanceSimulation(0.5f), "and stops the night");
            Assert.IsFalse(game.Sim.Paused, "without touching the pause");
            game.SetApplicationFocus(true);
            yield return null;
            Assert.IsFalse(game.CursorLocked, "focus return waits for a click");
            var started = hero.AttackStartTick;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, true));
            yield return null;
            Assert.IsTrue(game.CursorLocked, "the click recaptured the cursor");
            Assert.IsFalse(game.Player.HeroInput.Attack, "the recapture click never attacks, even held");
            for (var i = 0; i < 6; i++) game.AdvanceSimulation(0.05f);
            Assert.AreEqual(started, hero.AttackStartTick, "no attack accepted from it");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, true));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            Assert.IsTrue(game.Player.HeroInput.Attack, "a released tap is kept between steps");
            Assert.AreEqual(1, game.AdvanceSimulation(0.05f));
            Assert.Greater(hero.AttackStartTick, started, "and the hero accepts it");
            // The shown attack runs on the simulation's ticks: winding up between steps, at its contact pose on the frame
            // that shows the contact step, frozen while paused.
            Assert.AreEqual(0, game.AdvanceSimulation(0.02f));
            game.SyncViews(0.02f);
            Assert.That(game.HeroView.ShownAttackPhase, Is.GreaterThan(0f).And.LessThan(1f), "winding up before the contact step");
            Assert.IsNotNull(game.HeroView.AnimationRig, "release-rules-4 shows the articulated hero");
            for (var i = 0; i < hero.Profile.WindUpTicks; i++) game.AdvanceSimulation(0.05f);
            Assert.IsFalse(hero.AttackPending, "the contact step ran");
            game.SyncViews(0.02f);
            var atContact = game.HeroView.ShownAttackPhase;
            Assert.GreaterOrEqual(atContact, 1f, "the frame showing the contact step shows the contact pose");
            game.TogglePause();
            game.AdvanceSimulation(0.05f);
            game.SyncViews(0.05f);
            Assert.AreEqual(atContact, game.HeroView.ShownAttackPhase, 1e-5f, "pause freezes the attack pose");
            game.TogglePause();
            yield return Press(Key.Escape);
            Assert.IsTrue(game.MenuOpen);
            game.CloseMenu();
            yield return null;
            Assert.IsTrue(game.AwaitingRecapture && !game.CursorLocked, "back from the menu, a click recaptures");
        }
    }
}
