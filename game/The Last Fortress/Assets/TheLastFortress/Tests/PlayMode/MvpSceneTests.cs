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
    /// Scene-level smoke for the MVP scene: footprint input path, HUD, Reward, and the menu/checkpoint/settings path.
    /// Saves go to a temporary directory, never the player's real saves.
    /// </summary>
    public class MvpSceneTests
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
            saveDir = Path.Combine(Path.GetTempPath(), "tlf-playmode-" + System.Guid.NewGuid().ToString("N"));
            SaveStore.DirectoryOverride = saveDir;
            SceneManager.LoadScene("Mvp", LoadSceneMode.Single);
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

        [UnityTest]
        public IEnumerator FarmHotkeyPlacesFootprintThenRewardChoicePaysIncomeBeforeManualStart()
        {
            Assert.IsTrue(game.MenuOpen, "the MVP opens on its menu");
            game.NewRun(1, flat: true);
            // The default view is close (plan 014); this scenario needs the whole board: entry marker on screen, cell (5,5) clickable.
            game.CameraRig.SetView(CameraRig.DefaultFocus, 10.5f);
            yield return null;
            Assert.IsTrue(game.IsMvp);
            Assert.IsTrue(game.Sim.IsCampaign);
            Assert.IsFalse(game.IsStressMode);
            Assert.AreEqual(5, game.PurchasableKinds.Count);
            StringAssert.Contains("DAY 1 of 6", game.Hud.StateText);
            StringAssert.Contains("Tonight: West 10 Grunts", game.Hud.InfoText);
            Assert.AreEqual("WEST 10", game.Hud.EntryMarkerText(0), "on-map entry marker for tonight");

            yield return Press(Key.Digit4);
            Assert.AreEqual(BuildingKind.Farm, game.Player.Tool);
            var anchor = new GridPos(5, 5);
            yield return Click(game.CameraRig.CellToScreen(anchor));
            var farm = game.Sim.Grid.GetOccupant(anchor);
            Assert.IsNotNull(farm, "Farm placed through the Input System");
            Assert.AreEqual(BuildingKind.Farm, farm.Kind);
            Assert.AreSame(farm, game.Sim.Grid.GetOccupant(new GridPos(6, 6)));
            Assert.AreEqual(190, game.Sim.Gold);
            Assert.AreEqual(1, game.World.CountBuildingViews(BuildingKind.Farm), "one view for four cells");
            StringAssert.Contains("+45", game.Hud.IncomeText, "Keep 20 + Farm 25 forecast");

            game.AutoAdvance = false;
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.House, new GridPos(7, 5), out _));
            game.SyncViews();
            Assert.AreEqual(1, game.Overlay.VisibleLinkCount, "Farm/House link highlighted");
            StringAssert.Contains("+65", game.Hud.IncomeText, "Keep 20 + House 15 + linked Farm 30");
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.Tower, new GridPos(8, 9), out _));
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.Tower, new GridPos(8, 11), out _));
            var gold = game.Sim.Gold;
            game.Hud.PrimaryButton.onClick.Invoke();
            Assert.AreEqual(RunState.Night, game.Sim.State);
            for (var i = 0; i < 6000 && game.Sim.State == RunState.Night; i++) game.Sim.Step();
            game.SyncViews();
            yield return null;

            Assert.AreEqual(RunState.Reward, game.Sim.State, "cleared Night 1 offers a reward");
            Assert.IsTrue(game.Hud.RewardOverlay.activeSelf);
            Assert.IsFalse(game.Hud.CardFor(BuildingKind.Wall).interactable, "no building during Reward");
            var offer = game.Sim.CurrentOffer;
            StringAssert.Contains(PerkRules.Title(offer.PerkIds[0]), game.Hud.RewardCardText(0));
            Assert.AreEqual(gold, game.Sim.Gold, "income waits for the choice");
            StringAssert.Contains($"Night 1: {game.Sim.LastNight.EnemiesDefeated} enemies defeated", game.Hud.RewardRecapText);
            var forecast = game.Sim.IncomeForecast().Total;
            StringAssert.Contains($"Current forecast for dawn of Day 2: +{forecast} gold", game.Hud.RewardHeaderText, "a forecast, not a promise");
            for (var i = 0; i < offer.PerkIds.Length; i++)
            {
                var with = game.Sim.IncomeForecastWith(offer.PerkIds[i]).Total;
                if (with != forecast) StringAssert.Contains($"Next dawn: +{forecast} → +{with} gold", game.Hud.RewardCardText(i));
                else StringAssert.DoesNotContain("Next dawn", game.Hud.RewardCardText(i));
            }

            var preview = game.Sim.IncomeForecastWith(offer.PerkIds[0]).Total;
            game.Hud.RewardButton(0).onClick.Invoke();
            game.Hud.RewardButton(1).onClick.Invoke(); // a stale second click must not apply a perk or pay again
            game.SyncViews();
            yield return null;
            Assert.AreEqual(RunState.Day, game.Sim.State, "Day 2 waits for a manual start");
            Assert.IsFalse(game.Hud.RewardOverlay.activeSelf);
            CollectionAssert.AreEqual(new[] { offer.PerkIds[0] }, game.Sim.OwnedPerks);
            Assert.AreEqual(2, game.Sim.NightNumber);
            var paid = game.Sim.LastIncome.Total;
            Assert.AreEqual(preview, paid, "the card's preview (or forecast) is what dawn paid");
            Assert.AreEqual(gold + paid, game.Sim.Gold);
            StringAssert.Contains("DAY 2 of 6", game.Hud.StateText);
            StringAssert.Contains($"Received +{paid} at dawn", game.Hud.IncomeText);
            StringAssert.Contains("East", game.Hud.InfoText, "Night 2 preview lists the second entry");
            Assert.AreEqual("NEW: enemies also attack from East", game.Hud.RouteText, "a newly active side is called out");
            StringAssert.Contains("NEW EAST", game.Hud.EntryMarkerText(1));
            Assert.AreEqual("START NIGHT", game.Hud.PrimaryButtonLabel);
            Assert.IsTrue(game.Hud.CardFor(BuildingKind.SpikeTrap).interactable);

            // Selected Tower shows the authoritative effective values (perks included), not the base definition.
            var tower = game.Sim.Grid.GetOccupant(new GridPos(8, 9));
            game.Player.Select(tower);
            game.SyncViews();
            StringAssert.Contains($"Damage {game.Sim.TowerDamage(tower)}", game.Hud.SelectionText);
            StringAssert.Contains("range " + game.Sim.TowerRange(tower).ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture), game.Hud.SelectionText); // same text on every OS locale
            game.Player.Select(game.Sim.Grid.GetOccupant(new GridPos(5, 5)));
            game.SyncViews();
            StringAssert.Contains($"Income +{game.Sim.BuildingIncome(game.Sim.Grid.GetOccupant(new GridPos(5, 5)))}/day · House link", game.Hud.SelectionText);
            game.Player.Select(null);

            game.Hud.PrimaryButton.onClick.Invoke();
            Assert.AreEqual(RunState.Night, game.Sim.State, "manual next-night start");
            Assert.AreEqual(2, game.Sim.NightNumber);
        }

        [UnityTest]
        public IEnumerator MenuSeededRunCheckpointContinueOutcomeRetryAndSettings()
        {
            yield return null;
            Assert.IsTrue(game.Hud.MenuOverlay.activeSelf);
            Assert.IsFalse(game.Hud.ContinueButton.interactable, "nothing to continue yet");
            StringAssert.Contains("No saved run", game.Hud.MenuStatusText);

            game.Hud.SeedInput.text = "424242";
            game.Hud.NewRunButton.onClick.Invoke();
            yield return null;
            Assert.IsFalse(game.MenuOpen);
            Assert.AreEqual(424242u, game.Sim.Seed);
            var terrain = game.Sim.Config.Terrain;
            Assert.IsFalse(terrain.Authored, "New Run generates terrain from the seed");
            Assert.AreEqual(terrain.Fallback ? 0 : 30, game.World.TerrainDecorationCount);
            StringAssert.Contains("Seed 424242", game.Hud.SeedText);

            game.AutoAdvance = false;
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.Wall, new GridPos(9, 10), out var wall));
            game.Hud.RunMenuButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(game.Hud.SaveAndQuitButton.interactable, "Day: Save and Quit allowed");
            game.Hud.SaveAndQuitButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(game.MenuOpen);
            Assert.IsFalse(game.Persistence.HasRun);
            Assert.IsTrue(game.Hud.ContinueButton.interactable);
            StringAssert.Contains("Continue returns to the start of your current night", game.Hud.MenuStatusText);

            game.Hud.ContinueButton.onClick.Invoke();
            yield return null;
            Assert.IsFalse(game.MenuOpen);
            Assert.AreEqual(424242u, game.Sim.Seed);
            CollectionAssert.AreEqual(terrain.Cells, game.Sim.Config.Terrain.Cells, "stored terrain, not regenerated");
            Assert.AreEqual(BuildingKind.Wall, game.Sim.Grid.GetOccupant(new GridPos(9, 10))?.Kind, "checkpointed construction");
            Assert.AreEqual(230, game.Sim.Gold);

            // Guards: nothing really exits; a clean Day quits directly.
            var quits = 0;
            game.QuitApplication = () => quits++;
            game.RequestQuit();
            Assert.AreEqual(1, quits, "no unsaved edits: Quit exits without asking");

            var slotFile = game.Persistence.Store.SlotPath(game.Persistence.Slot);
            var durable = File.ReadAllBytes(slotFile);
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.Wall, new GridPos(10, 9), out _));
            game.RequestRestart();
            game.SyncViews();
            Assert.IsTrue(game.Hud.ConfirmPanel.activeSelf);
            StringAssert.Contains("Retry same seed — start Day 1", game.Hud.ConfirmTitle);
            Choose("Cancel");
            Assert.IsNull(game.PendingConfirm);
            Assert.AreEqual(210, game.Sim.Gold, "Cancel leaves the run untouched");
            Assert.IsTrue(game.Sim.Grid.IsSolid(new GridPos(10, 9)));
            CollectionAssert.AreEqual(durable, File.ReadAllBytes(slotFile), "and the checkpoint untouched");

            game.RequestQuit();
            game.SyncViews();
            CollectionAssert.AreEqual(new[] { "Save and exit", "Exit without these changes", "Cancel" }, game.Hud.ConfirmOptions().Select(o => o.label));
            Choose("Cancel");
            Assert.AreEqual(1, quits);

            // Save-and-exit with a failing write: no exit, durable file and live run kept; Retry saves and only then exits.
            Directory.CreateDirectory(slotFile + ".tmp");
            game.RequestQuit();
            game.SyncViews();
            Choose("Save and exit");
            Assert.AreEqual(1, quits, "never exits after a failed save");
            Assert.IsTrue(game.Persistence.SavePending);
            game.SyncViews();
            Assert.IsTrue(game.Hud.SaveFailedPanel.activeSelf);
            CollectionAssert.AreEqual(durable, File.ReadAllBytes(slotFile));
            Assert.AreEqual(210, game.Sim.Gold);
            Directory.Delete(slotFile + ".tmp");
            Assert.IsTrue(game.RetrySave());
            Assert.AreEqual(2, quits, "exits once the exact snapshot is durable");
            Assert.IsFalse(game.Persistence.SavePending);
            Assert.IsTrue(game.Persistence.Store.Read(game.Persistence.Slot, game.Definitions).Sim.Grid.IsSolid(new GridPos(10, 9)));

            Assert.IsTrue(game.StartNight(), "Night starts only after its Day checkpoint commits");
            game.RequestQuit();
            StringAssert.Contains("Continue will replay Night 1 from the start of Day 1", game.PendingConfirm.Body);
            game.CancelConfirm();
            for (var i = 0; i < 20000 && game.Sim.State == RunState.Night; i++) game.Sim.Step();
            game.SyncViews();
            yield return null;
            Assert.AreEqual(RunState.Defeat, game.Sim.State, "one Wall cannot hold Night 1");
            Assert.Greater(game.Hud.KeepHitShown, 0, "actual Keep damage shows a HUD hit cue");
            Assert.IsTrue(game.Hud.TerminalOverlay.activeSelf);
            StringAssert.Contains("Run: nights cleared 0 / 6", game.Hud.TerminalBody);
            StringAssert.Contains($"This night: {game.Sim.LastNight.EnemiesDefeated} enemies defeated · Keep -500 HP", game.Hud.TerminalBody);
            StringAssert.Contains("Seed 424242", game.Hud.TerminalBody);
            Assert.AreEqual(SaveStatus.Terminal, game.Persistence.Store.Read(game.Persistence.Slot, game.Definitions).Status, "ended runs cannot be continued");

            game.Hud.TerminalRestartButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(RunState.Day, game.Sim.State);
            Assert.AreEqual(1, game.Sim.NightNumber);
            Assert.AreEqual(250, game.Sim.Gold);
            Assert.AreEqual(424242u, game.Sim.Seed, "same-seed retry");
            CollectionAssert.AreEqual(terrain.Cells, game.Sim.Config.Terrain.Cells);
            Assert.AreEqual(SaveStatus.Ok, game.Persistence.Store.Read(game.Persistence.Slot, game.Definitions).Status, "fresh Day 1 checkpoint");

            game.RequestRestart();
            Assert.IsNull(game.PendingConfirm, "an untouched Day 1 restarts without asking");
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.Tower, new GridPos(9, 9), out _));
            game.RequestRestart();
            game.SyncViews();
            Choose("Retry from Day 1");
            Assert.AreEqual((1, 250), (game.Sim.NightNumber, game.Sim.Gold), "confirmed: back to Day 1");
            Assert.IsFalse(game.Sim.Grid.IsSolid(new GridPos(9, 9)));

            game.OpenMenu();
            game.Hud.SettingsButton.onClick.Invoke();
            yield return null;
            var panel = game.Hud.SettingsPanel.transform.Find("Panel");
            panel.Find("Camerasensitivity>Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            panel.Find("Camerasensitivity>Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            panel.Find("CamerashakeonKeephits>Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            panel.Find("SavesettingsButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual(1.5f, game.CameraRig.Sensitivity, 1e-4f, "applied");
            Assert.IsTrue(game.CameraRig.ShakeEnabled, "shake is off by default; the toggle turned it on");
            var stored = game.Persistence.Store.ReadSettings(out var problem);
            Assert.IsNull(problem);
            Assert.AreEqual((1.5f, true), (stored.cameraSensitivity, stored.cameraShake), "persisted separately from the run");

            // Master volume to zero: real silence at once (preview), persisted on save.
            for (var i = 0; i < 12; i++) panel.Find("Mastervolume<Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual(0f, AudioListener.volume, "preview mutes immediately");
            Assert.AreEqual(0f, game.Audio.EffectiveSfxVolume);
            panel.Find("SavesettingsButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual(0f, game.Persistence.Store.ReadSettings(out _).masterVolume, "mute persisted");
            Assert.AreEqual(0f, game.Settings.masterVolume);
        }

        [UnityTest]
        public IEnumerator LethalShotCueSurvivesRemovalAndCombatEffectsFreezeWhenSuspended()
        {
            game.NewRun(1, flat: true);
            game.AutoAdvance = false;
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.Tower, new GridPos(3, 9), out _));
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.Tower, new GridPos(3, 11), out _));
            Assert.IsTrue(game.StartNight());
            var first = game.Sim.Enemies[0];

            // Gait follows real movement: the walking Grunt's legs carry a gait (plan 015: its hips bob, not the whole model,
            // which lifted the planted feet) while its root stays on the simulated position.
            var maxBob = 0f;
            for (var i = 0; i < 12; i++)
            {
                game.Sim.Step();
                game.SyncViews(0.05f);
                maxBob = Mathf.Max(maxBob, game.World.EnemyGait(first.Id) ?? 0f);
            }

            Assert.Greater(maxBob, 0.001f, "moving enemy shows a gait");

            // Several fixed steps run before the next view sync, so the lethal shot's target is already removed.
            for (var i = 0; i < 400 && first.Alive; i++) game.Sim.Step();
            Assert.IsFalse(first.Alive, "the first Grunt was killed by a Tower");
            var deathAt = new Vector3((float)first.X, 0f, (float)first.Z);
            game.SyncViews(0f);
            Assert.IsFalse(game.World.HasEnemyView(first.Id), "dead enemy removed from the board");
            Assert.IsTrue(game.World.ArrowTargets().Any(t => Flat(t, deathAt) < 0.01f), "lethal shot still gets an arrow to where the target died");

            // Pause and menu suspend combat presentation: nothing moves, lands or appears — and no impact sound plays early.
            var frozen = game.World.ArrowPositions().ToList();
            var impacts = game.Audio.PlayCount(SoundId.Impact);
            var pendingImpacts = game.Audio.PendingCount;
            Assert.Greater(pendingImpacts, 0, "the in-flight arrow's impact is scheduled for its landing");
            game.Sim.SetPaused(true);
            game.SyncViews(0.5f);
            CollectionAssert.AreEqual(frozen, game.World.ArrowPositions().ToList(), "paused: arrows hold");
            Assert.IsTrue(game.Audio.CombatPaused, "combat sounds pause with the night");
            game.Sim.SetPaused(false);
            game.OpenMenu();
            game.SyncViews(0.5f);
            CollectionAssert.AreEqual(frozen, game.World.ArrowPositions().ToList(), "menu open: arrows hold");
            Assert.IsFalse(game.World.VisibleCues().Any(c => c.kind == CueKind.Kill), "the kill cue waits for its arrow");
            Assert.AreEqual((impacts, pendingImpacts), (game.Audio.PlayCount(SoundId.Impact), game.Audio.PendingCount), "impacts wait with their arrows");
            game.CloseMenu();

            game.SyncViews(WorldView.ArrowSeconds + 0.01f);
            Assert.IsTrue(game.World.VisibleCues().Any(c => c.kind == CueKind.Kill && Flat(c.position, deathAt) < 0.01f), "kill cue at the recorded death position");
            Assert.IsTrue(game.World.VisibleCues().Any(c => c.kind == CueKind.Hit), "landing arrows show hit cues");
            Assert.IsFalse(game.Audio.CombatPaused);
            Assert.Greater(game.Audio.PlayCount(SoundId.Impact), impacts, "the impact sounds when its arrow lands");
            Assert.AreEqual(game.World.EventsSeen, game.Audio.EventsConsumed, "world and audio received the same events");
            Assert.AreEqual(game.World.BatchesSeen, game.Audio.BatchesConsumed, "one batch per frame for both");
            Assert.LessOrEqual(game.Audio.ActiveVoicesOf(SoundId.Impact), AudioDirector.MaxPerFamily);


            // The last kill's effects finish after the night ends instead of being stranded.
            for (var i = 0; i < 20000 && game.Sim.State == RunState.Night; i++) game.Sim.Step();
            Assert.AreNotEqual(RunState.Night, game.Sim.State);
            game.SyncViews(0f);
            for (var i = 0; i < 20; i++) game.SyncViews(0.05f);
            Assert.AreEqual(0, game.World.ActiveArrowCount, "arrows completed");
            Assert.AreEqual(0, game.World.PendingCueCount, "cues completed and recycled");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TooltipStaysOnScreenAtEveryCornerAndSelectionActionsClearDetails()
        {
            game.NewRun(1, flat: true);
            game.AutoAdvance = false;
            game.CameraRig.SetView(CameraRig.DefaultFocus, 4f); // zoomed in: the board fills the screen, so every corner hovers a cell
            yield return Press(Key.Digit4); // Farm: the longest tooltip (2x2, price, reason/rotation)
            var w = UnityEngine.Screen.width;
            var h = UnityEngine.Screen.height;
            var checkedCorners = 0;
            foreach (var corner in new[] { new Vector2(4, 4), new Vector2(w - 4, 4), new Vector2(4, h - 4), new Vector2(w - 4, h - 4) })
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = corner });
                yield return null;
                yield return null;
                if (!game.Player.HoverCell.HasValue) continue; // a HUD panel covers this corner: no tooltip by design
                Assert.IsNotNull(game.Hud.TooltipText, $"tooltip shown at {corner}");
                var r = game.Hud.TooltipCanvasRect;
                var c = game.Hud.CanvasSize;
                Assert.IsTrue(r.xMin >= 0 && r.yMin >= 0 && r.xMax <= c.x && r.yMax <= c.y, $"tooltip {r} inside canvas {c} at pointer {corner}");
                checkedCorners++;
            }

            Assert.GreaterOrEqual(checkedCorners, 3, "the corner check actually ran");

            yield return Press(Key.Escape);
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.Farm, new GridPos(5, 5), out var farm));
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.House, new GridPos(7, 5), out _));
            farm.Hp -= 33; // damaged: repair quote, refund, income and rotation all present
            game.Player.Select(farm);
            game.SyncViews();
            var body = game.Hud.SelectionBodyRect;
            var bodyBottom = body.anchoredPosition.y - body.sizeDelta.y;
            foreach (var button in new[] { game.Hud.SellButton, game.Hud.RepairButton, game.Hud.RotateButton })
            {
                Assert.IsTrue(button.gameObject.activeSelf);
                var b = (RectTransform)button.transform;
                Assert.LessOrEqual(b.anchoredPosition.y, bodyBottom, $"{button.name} sits below the details");
                Assert.GreaterOrEqual(game.Hud.SelectionPanelRect.sizeDelta.y, -(b.anchoredPosition.y - b.sizeDelta.y), $"{button.name} inside the panel");
            }
        }

        [UnityTest]
        public IEnumerator GuideAdvancesOnRealActionsSkipPersistsAndContinueLeavesCheckpointUntouched()
        {
            yield return null;
            Assert.IsFalse(game.Hud.GameplayLayer.activeSelf, "the title shows only the world and its menu");
            game.NewRun(7, flat: true);
            game.AutoAdvance = false;
            game.SyncViews();
            Assert.IsTrue(game.Hud.GameplayLayer.activeSelf);
            StringAssert.Contains("GUIDE 1 / 5", game.Hud.GuideText, "a first run starts the guide");

            // An invalid placement completes nothing; a real Tower does.
            yield return Press(Key.Digit2);
            yield return Click(game.CameraRig.CellToScreen(new GridPos(10, 10))); // onto the Keep: rejected
            Assert.AreNotEqual(PlacementRejection.None, game.Player.LastRejection, "the click really attempted a placement");
            Assert.AreEqual(0, game.Sim.Stats.BuildingsPurchased);
            StringAssert.Contains("GUIDE 1 / 5", game.Hud.GuideText, "a rejected placement does not advance");
            Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.Tower, new GridPos(4, 9), out var tower));
            game.SyncViews();
            StringAssert.Contains("GUIDE 2 / 5", game.Hud.GuideText);
            game.Player.Select(tower);
            game.SyncViews();
            StringAssert.Contains("GUIDE 3 / 5", game.Hud.GuideText, "inspecting the Tower shows its range");

            game.Hud.SkipGuideButton.onClick.Invoke();
            game.SyncViews();
            Assert.IsNull(game.Hud.GuideText, "Skip hides the guide");
            Assert.IsTrue(game.Persistence.Store.ReadProfile().skipped, "and is saved apart from the run");
            Assert.IsTrue(game.StartNight(), "the guide never blocks Start Night");
            game.QuitToMenuWithoutSaving();

            // Continue: the old run resumes without the guide and without rewriting its checkpoint.
            var file = game.Persistence.Store.SlotPath(game.Persistence.Store.LatestSlot());
            var durable = File.ReadAllBytes(file);
            var music = game.Audio.MusicClip;
            for (var i = 0; i < 3; i++)
            {
                game.OpenMenu();
                game.SyncViews(0.1f);
            }

            Assert.LessOrEqual(game.Audio.MusicTracks, AudioDirector.MaxMusicTracks, "reopening the menu never stacks music");
            Assert.AreSame(music, game.Audio.MusicClip);
            game.Hud.ContinueButton.onClick.Invoke();
            game.SyncViews();
            Assert.AreEqual(RunState.Day, game.Sim.State, "continues at the Day checkpoint before that night");
            Assert.IsNull(game.Hud.GuideText);
            CollectionAssert.AreEqual(durable, File.ReadAllBytes(file), "Continue does not mutate the checkpoint");
            yield return null;
        }

        void Choose(string label)
        {
            var option = game.Hud.ConfirmOptions().FirstOrDefault(o => o.label == label);
            Assert.IsNotNull(option.button, $"guard option '{label}' shown");
            option.button.onClick.Invoke();
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }
}
