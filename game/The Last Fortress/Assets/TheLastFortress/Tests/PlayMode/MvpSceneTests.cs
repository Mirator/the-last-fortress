using System.Collections;
using System.IO;
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

            game.Hud.RewardButton(0).onClick.Invoke();
            game.Hud.RewardButton(1).onClick.Invoke(); // a stale second click must not apply a perk or pay again
            game.SyncViews();
            yield return null;
            Assert.AreEqual(RunState.Day, game.Sim.State, "Day 2 waits for a manual start");
            Assert.IsFalse(game.Hud.RewardOverlay.activeSelf);
            CollectionAssert.AreEqual(new[] { offer.PerkIds[0] }, game.Sim.OwnedPerks);
            Assert.AreEqual(2, game.Sim.NightNumber);
            var paid = game.Sim.LastIncome.Total;
            Assert.GreaterOrEqual(paid, 65);
            Assert.AreEqual(gold + paid, game.Sim.Gold);
            StringAssert.Contains("DAY 2 of 6", game.Hud.StateText);
            StringAssert.Contains($"Received +{paid} at dawn", game.Hud.IncomeText);
            StringAssert.Contains("East", game.Hud.InfoText, "Night 2 preview lists the second entry");
            Assert.AreEqual("NEW: enemies also attack from East", game.Hud.RouteText, "a newly active side is called out");
            StringAssert.Contains("NEW EAST", game.Hud.EntryMarkerText(1));
            Assert.AreEqual("START NIGHT", game.Hud.PrimaryButtonLabel);
            Assert.IsTrue(game.Hud.CardFor(BuildingKind.SpikeTrap).interactable);

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

            Assert.IsTrue(game.StartNight(), "Night starts only after its Day checkpoint commits");
            for (var i = 0; i < 20000 && game.Sim.State == RunState.Night; i++) game.Sim.Step();
            game.SyncViews();
            yield return null;
            Assert.AreEqual(RunState.Defeat, game.Sim.State, "one Wall cannot hold Night 1");
            Assert.IsTrue(game.Hud.TerminalOverlay.activeSelf);
            StringAssert.Contains("Nights cleared 0 / 6", game.Hud.TerminalBody);
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

            game.OpenMenu();
            game.Hud.SettingsButton.onClick.Invoke();
            yield return null;
            var panel = game.Hud.SettingsPanel.transform.Find("Panel");
            panel.Find("Camerasensitivity>Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            panel.Find("Camerasensitivity>Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            panel.Find("CamerashakeonKeephits>Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            panel.Find("SavesettingsButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual(1.5f, game.CameraRig.Sensitivity, 1e-4f, "applied");
            Assert.IsFalse(game.CameraRig.ShakeEnabled);
            var stored = game.Persistence.Store.ReadSettings(out var problem);
            Assert.IsNull(problem);
            Assert.AreEqual((1.5f, false), (stored.cameraSensitivity, stored.cameraShake), "persisted separately from the run");
        }
    }
}
