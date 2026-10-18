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
            // The Bomber comes from the undefended east so it lives to light its fuse; the rest walk into the Cannon.
            var wave = new WavePlan("threats", new[]
            {
                new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.Grunt), new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.Grunt),
                new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.Grunt), new SpawnEntry(10, MvpCampaign.East, 1, EnemyKind.Bomber),
                new SpawnEntry(10, MvpCampaign.West, 0, EnemyKind.Archer), new SpawnEntry(20, MvpCampaign.West, 0, EnemyKind.SiegeCommander),
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
            var cannon = game.Sim.Grid.GetOccupant(new GridPos(7, 9));
            Assert.IsNotNull(cannon, "2x2 Cannon placed through the Input System");
            Assert.AreEqual((BuildingKind.Cannon, new GridPos(6, 8)), (cannon.Kind, cannon.Cell));
            Assert.AreEqual(1, game.World.CountBuildingViews(BuildingKind.Cannon), "one view for four cells");

            game.AutoAdvance = false;
            yield return Press(Key.Digit7);
            game.Player.HoverOverride = new GridPos(8, 9);
            yield return null; // the hover is read on the next player tick
            Assert.AreEqual(1, game.Overlay.BuffMarkCount, "the Watchtower ghost outlines the Cannon it would boost");
            game.Player.HoverOverride = null;
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.Watchtower, new GridPos(8, 9), out _));
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
        public IEnumerator GatesStandAtTonightsEntriesAndChevronsFlowTowardTheKeep()
        {
            game.NewRun(7, false); // a turned board: Night 1 does not come from the West
            yield return null;
            var entries = game.Sim.Wave.EntryCells;
            Assert.AreEqual(entries.Count, game.Overlay.VisibleGateCount);
            var gate = game.Overlay.GatePosition(0);
            Assert.Less(Vector3.Distance(gate, Prims.CellCenter(entries[0], gate.y)), 1.2f, "the gate stands at tonight's entry");
            Assert.IsTrue(gate.x < 0f || gate.x > 20f || gate.z < 0f || gate.z > 20f, "on the beach, never on a legal cell");

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
            Assert.AreEqual((entries.Count, 0), (game.Overlay.VisibleGateCount, game.Overlay.VisibleChevronCount), "gates stay at Night; route chevrons are a Day preview");
        }
    }
}
