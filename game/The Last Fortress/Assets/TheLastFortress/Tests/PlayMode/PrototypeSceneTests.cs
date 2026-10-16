using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TheLastFortress.Tests
{
    /// <summary>Scene-level lifecycle, Input System, UI and presentation checks against the saved prototype scene.</summary>
    public class PrototypeSceneTests
    {
        const string SceneName = "NavigationPrototype";

        PrototypeBootstrap game;
        Mouse mouse;
        Keyboard keyboard;
        InputSettings.EditorInputBehaviorInPlayMode previousBehavior;
        InputSettings.BackgroundBehavior previousBackground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackground = InputSystem.settings.backgroundBehavior;
            // Virtual devices must reach the game even when the Editor window is unfocused during automated runs.
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // The default ResetAndDisableNonBackgroundDevices disables the test devices whenever the Editor application
            // itself is not the OS foreground window, so results depended on desktop focus rather than game code.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>("TestMouse");
            keyboard = InputSystem.AddDevice<Keyboard>("TestKeyboard");
            yield return LoadScene();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (game != null)
            {
                game.Player.MouseOverride = null;
                game.Player.KeyboardOverride = null;
            }

            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior;
            InputSystem.settings.backgroundBehavior = previousBackground;
            yield return null;
        }

        IEnumerator LoadScene()
        {
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            game = Object.FindFirstObjectByType<PrototypeBootstrap>();
            Assert.IsNotNull(game, "bootstrap present");
            game.Player.MouseOverride = mouse;
            game.Player.KeyboardOverride = keyboard;
        }

        IEnumerator MoveMouse(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            yield return null;
        }

        IEnumerator Click(Vector2 position, MouseButton button = MouseButton.Left)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(button, true));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
        }

        IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        Vector2 Screen(int x, int z) => game.CameraRig.CellToScreen(new GridPos(x, z));

        static Vector2 ScreenCenterOf(Selectable s)
        {
            var rt = (RectTransform)s.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return (corners[0] + corners[2]) / 2f; // Screen Space Overlay: world corners are screen pixels
        }

        void StepUntil(System.Func<bool> condition, int maxSteps = 6000)
        {
            for (var i = 0; i < maxSteps && !condition(); i++) game.Sim.Step();
            Assert.IsTrue(condition(), "condition not reached");
        }

        [UnityTest]
        public IEnumerator SavedSceneStartsInDayWithOneBootstrapAndOneKeep()
        {
            Assert.AreEqual(1, Object.FindObjectsByType<PrototypeBootstrap>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, game.World.CountBuildingViews(BuildingKind.Keep));
            Assert.AreEqual(1, game.World.BuildingViewCount);
            Assert.AreEqual(RunState.Day, game.Sim.State);
            Assert.AreEqual(250, game.Sim.Gold);
            Assert.IsFalse(game.IsStressMode, "stress is never the default start state");
            Assert.IsFalse(game.Hud.StressBanner.activeSelf);
            Assert.IsTrue(Camera.main.orthographic);
            Assert.AreEqual(1, Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HotkeyToolAndClickPlaceThroughInputSystem()
        {
            yield return Press(Key.Digit2);
            Assert.AreEqual(BuildingKind.Tower, game.Player.Tool);
            yield return MoveMouse(Screen(5, 5));
            Assert.AreEqual(new GridPos(5, 5), game.Player.HoverCell);
            Assert.IsTrue(game.Overlay.GhostVisible && game.Overlay.GhostValid);
            StringAssert.Contains("70 gold", game.Hud.TooltipText);
            Assert.IsTrue(game.World.BuildGridVisible, "placement grid shown while a tool is active");

            yield return Click(Screen(5, 5));
            var tower = game.Sim.Grid.GetOccupant(new GridPos(5, 5));
            Assert.IsNotNull(tower);
            Assert.AreEqual(BuildingKind.Tower, tower.Kind);
            Assert.AreEqual(180, game.Sim.Gold);
            Assert.AreEqual(BuildingKind.Tower, game.Player.Tool, "tool stays active after placement");
            Assert.IsTrue(game.World.HasBuildingView(tower.Id));

            yield return MoveMouse(Screen(5, 5));
            Assert.IsFalse(game.Overlay.GhostValid);
            StringAssert.Contains("Tile occupied", game.Hud.TooltipText);
            yield return Click(Screen(5, 5));
            Assert.AreEqual(180, game.Sim.Gold, "click re-validates; occupied tile rejected");
            Assert.AreEqual(PlacementRejection.Occupied, game.Player.LastRejection);

            yield return Click(Screen(0, 6));
            Assert.AreEqual(PlacementRejection.ReservedRing, game.Player.LastRejection);

            // Right drag pans like grabbing the board (the ground point stays under the pointer) and keeps the tool.
            var from = Screen(8, 8);
            var to = from + new Vector2(140, -60);
            Assert.IsTrue(game.CameraRig.TryScreenToWorld(from, out var grabbed));
            var focus = game.CameraRig.Focus;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = from });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = from }.WithButton(MouseButton.Right, true));
            yield return null;
            for (var i = 1; i <= 4; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.Lerp(from, to, i / 4f) }.WithButton(MouseButton.Right, true));
                yield return null;
            }

            Assert.IsTrue(game.CameraRig.IsDragging);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = to });
            yield return null;
            yield return null;
            Assert.IsFalse(game.CameraRig.IsDragging);
            Assert.Greater((game.CameraRig.Focus - focus).magnitude, 0.5f, "right drag moves the camera");
            Assert.IsTrue(game.CameraRig.TryScreenToWorld(to, out var under));
            Assert.Less(new Vector2(under.x - grabbed.x, under.z - grabbed.z).magnitude, 0.05f, "the grabbed ground point follows the pointer");
            Assert.AreEqual(BuildingKind.Tower, game.Player.Tool, "a drag is not a cancel click");

            yield return Click(Screen(5, 5), MouseButton.Right);
            Assert.IsNull(game.Player.Tool, "right click cancels the tool");
            Assert.IsFalse(game.World.BuildGridVisible);

            yield return Click(Screen(5, 5));
            Assert.AreSame(tower, game.Player.Selected, "left click without a tool selects");
            Assert.AreEqual(BuildingKind.Tower, game.Sim.Grid.GetOccupant(new GridPos(5, 5)).Kind);
            yield return Press(Key.Escape);
            Assert.IsNull(game.Player.Selected);

            yield return Press(Key.Digit1);
            Assert.AreEqual(BuildingKind.Wall, game.Player.Tool);
            yield return Press(Key.Escape);
            Assert.IsNull(game.Player.Tool, "Escape cancels the tool");
        }

        [UnityTest]
        public IEnumerator ClickOnHudNeverPlacesBehindIt()
        {
            yield return Press(Key.Digit2);
            var card = ScreenCenterOf(game.Hud.WallCard);
            // Pan so that a valid build cell sits exactly under the Wall card.
            Assert.IsTrue(game.CameraRig.TryScreenToWorld(card, out var hit));
            var target = new GridPos(5, 5);
            game.CameraRig.SetView(game.CameraRig.Focus + (Prims.CellCenter(target) - hit), game.CameraRig.Size);
            yield return null;
            Assert.IsTrue(game.CameraRig.TryScreenToCell(card, out var under));
            Assert.AreEqual(target, under);
            Assert.AreEqual(PlacementRejection.None, game.Sim.CheckPlacement(BuildingKind.Tower, target));

            yield return MoveMouse(card);
            Assert.IsTrue(game.Player.PointerOverUi);
            Assert.IsNull(game.Player.HoverCell, "no world hover through UI");
            yield return Click(card);
            Assert.IsNull(game.Sim.Grid.GetOccupant(target), "UI click must not reach the world");
            Assert.AreEqual(250, game.Sim.Gold);
            Assert.AreEqual(BuildingKind.Wall, game.Player.Tool, "the card itself received the click via the UI input module");
        }

        [UnityTest]
        public IEnumerator StartNightDoubleClickStartsExactlyOneWave()
        {
            game.Hud.PrimaryButton.onClick.Invoke();
            game.Hud.PrimaryButton.onClick.Invoke();
            Assert.AreEqual(RunState.Night, game.Sim.State);
            Assert.IsFalse(game.Sim.Paused, "second click is not turned into a pause");
            Assert.AreEqual(1, game.Sim.AliveEnemyCount);
            Assert.AreEqual(9, game.Sim.ScheduledRemaining);
            yield return null;
            Assert.AreEqual(1, game.World.EnemyViewCount);
            Assert.IsFalse(game.Hud.WallCard.interactable, "no construction during Night");
        }

        [UnityTest]
        public IEnumerator PausedNightBlocksBuildingButKeepsCameraAndInspection()
        {
            game.StartNight();
            yield return Press(Key.Space);
            Assert.IsTrue(game.Sim.Paused);
            StringAssert.Contains("PAUSED", game.Hud.StateText);
            var tick = game.Sim.Tick;

            yield return Press(Key.Digit1);
            Assert.IsNull(game.Player.Tool);
            yield return Click(Screen(5, 5));
            Assert.IsNull(game.Sim.Grid.GetOccupant(new GridPos(5, 5)));
            yield return Click(Screen(10, 10));
            Assert.AreSame(game.Sim.Keep, game.Player.Selected, "inspection works while paused");
            Assert.IsFalse(game.Hud.SellButton.gameObject.activeSelf, "Keep cannot be sold; no Sell at Night");

            var focus = game.CameraRig.Focus;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            // Pan speed follows real frame time: hold the key until it moves (bounded), not for a fixed frame count.
            for (var i = 0; i < 600 && (game.CameraRig.Focus - focus).magnitude <= 0.05f; i++) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.Greater((game.CameraRig.Focus - focus).magnitude, 0.05f, "camera pans while paused");

            for (var i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(tick, game.Sim.Tick, "paused simulation does not advance");
            game.Hud.MenuRestartButton.onClick.Invoke();
            Assert.AreEqual(RunState.Day, game.Sim.State, "Restart available from the in-run menu");
            Assert.IsFalse(game.Sim.Paused);
        }

        [UnityTest]
        public IEnumerator DestroyedWallReleasesOccupancyAndRemovesVisual()
        {
            game.AutoAdvance = false;
            foreach (var c in new[] { new GridPos(9, 10), new GridPos(11, 10), new GridPos(10, 9), new GridPos(10, 11) })
                Assert.AreEqual(PlacementRejection.None, game.Sim.TryPlace(BuildingKind.Wall, c, out _));
            var west = game.Sim.Grid.GetOccupant(new GridPos(9, 10));
            yield return null;
            Assert.IsTrue(game.World.HasBuildingView(west.Id));
            game.StartNight();
            StepUntil(() => west.Removed);
            Assert.IsNull(game.Sim.Grid.GetOccupant(new GridPos(9, 10)), "occupancy released immediately");
            Assert.IsTrue(game.World.HasBuildingView(west.Id), "visual removed only after the simulation event is synced");
            game.SyncViews();
            Assert.IsFalse(game.World.HasBuildingView(west.Id));
            Assert.AreEqual(4, game.World.BuildingViewCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TerminalOverlaysOfferRestartToExactInitialState()
        {
            game.AutoAdvance = false;
            var fresh = new RunSimulation(new RunConfig { Definitions = game.Definitions }).CaptureSnapshot();

            game.StartNight();
            StepUntil(() => game.Sim.IsTerminal);
            game.SyncViews();
            Assert.AreEqual(RunState.Defeat, game.Sim.State);
            Assert.IsTrue(game.Hud.TerminalOverlay.activeSelf);
            Assert.AreEqual("DEFEAT", game.Hud.TerminalTitle);
            game.Hud.TerminalRestartButton.onClick.Invoke();
            yield return null;
            Assert.IsFalse(game.Hud.TerminalOverlay.activeSelf);
            Assert.AreEqual(fresh, game.Sim.CaptureSnapshot());
            Assert.AreEqual(0, game.World.EnemyViewCount);
            Assert.AreEqual(1, game.World.BuildingViewCount);

            CollectionAssert.IsEmpty(game.ApplyLayout(game.Content.winningLayout));
            game.StartNight();
            StepUntil(() => game.Sim.IsTerminal);
            game.SyncViews();
            Assert.AreEqual(RunState.Victory, game.Sim.State);
            Assert.AreEqual("VICTORY", game.Hud.TerminalTitle);
            game.Hud.TerminalRestartButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(fresh, game.Sim.CaptureSnapshot());
            Assert.IsNull(game.Player.Selected);
            Assert.IsNull(game.Player.Tool);
        }

        [UnityTest]
        public IEnumerator SceneReloadDoesNotDuplicateBootstrapsOrSubscriptions()
        {
            yield return LoadScene();
            yield return LoadScene();
            Assert.AreEqual(1, Object.FindObjectsByType<PrototypeBootstrap>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length);
            game.Hud.PrimaryButton.onClick.Invoke();
            Assert.AreEqual(1, game.Sim.AliveEnemyCount);
            Assert.AreEqual(9, game.Sim.ScheduledRemaining);
            yield return null;
            Assert.AreEqual(1, game.World.EnemyViewCount);
        }

        [UnityTest]
        public IEnumerator RecordedLayoutsReproduceOutcomes()
        {
            game.AutoAdvance = false;
            CollectionAssert.IsEmpty(game.ApplyLayout(game.Content.winningLayout));
            Assert.GreaterOrEqual(game.Sim.Gold, 0);
            Assert.IsTrue(game.StartNight());
            while (game.Sim.State == RunState.Night) game.FastForward(1.0);
            Assert.AreEqual(RunState.Victory, game.Sim.State, "winning layout wins with the normal budget");

            game.Restart();
            CollectionAssert.IsEmpty(game.ApplyLayout(game.Content.losingLayout));
            Assert.IsTrue(game.StartNight());
            while (game.Sim.State == RunState.Night) game.FastForward(1.0);
            Assert.AreEqual(RunState.Defeat, game.Sim.State, "Keep-only layout loses");
            yield return null;
        }

        [UnityTest]
        public IEnumerator StressModeIsLabeledAndDoesNotLeakIntoNormalMode()
        {
            game.AutoAdvance = false;
            game.EnterStressMode();
            for (var i = 0; i < 100; i++) game.Sim.Step();
            game.SyncViews();
            yield return null;
            Assert.IsTrue(game.Sim.StressMode);
            Assert.IsTrue(game.Hud.StressBanner.activeSelf, "stress mode is visibly labeled");
            Assert.AreEqual(100, game.Sim.AliveEnemyCount);
            Assert.AreEqual(100, game.World.EnemyViewCount);
            Assert.AreEqual(41, game.World.BuildingViewCount);

            game.EnterNormalMode();
            yield return null;
            Assert.IsFalse(game.Sim.StressMode);
            Assert.IsFalse(game.Hud.StressBanner.activeSelf);
            Assert.AreEqual(RunState.Day, game.Sim.State);
            Assert.AreEqual(10, game.Sim.Wave.Spawns.Count);
            Assert.AreEqual(500, game.Sim.Keep.Hp);
            Assert.AreEqual(1, game.World.BuildingViewCount);
            Assert.AreEqual(0, game.World.EnemyViewCount);
            Assert.IsTrue(game.Hud.WallCard.interactable);
            CollectionAssert.IsEmpty(game.Definitions.Validate());
        }

        [UnityTest]
        public IEnumerator RoutePreviewMatchesPlannerAndShowsBreach()
        {
            foreach (var c in new[] { new GridPos(9, 10), new GridPos(11, 10), new GridPos(10, 9) })
                game.Sim.TryPlace(BuildingKind.Wall, c, out _);
            yield return null;
            var committed = game.Sim.GetLiveRoute(WavePlan.MainEntry);
            Assert.AreSame(committed, game.Overlay.PrimaryRoute);
            Assert.AreEqual(committed.Cells.Count, game.Overlay.PrimaryLinePointCount);
            for (var i = 0; i < committed.Cells.Count; i++)
            {
                var p = game.Overlay.PrimaryLinePoint(i);
                Assert.AreEqual(committed.Cells[i], new GridPos(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z)));
            }

            Assert.IsFalse(game.Overlay.BreachMarkerVisible);

            var gold = game.Sim.Gold;
            var revision = game.Sim.Grid.Revision;
            yield return Press(Key.Digit1);
            yield return MoveMouse(Screen(10, 11));
            Assert.IsTrue(game.Overlay.GhostValid, "sealing the Keep is a legal placement");
            Assert.IsNotNull(game.Overlay.CandidateRoute);
            Assert.IsTrue(game.Overlay.CandidateRoute.IsBreach);
            Assert.IsTrue(game.Overlay.BreachMarkerVisible);
            Assert.IsTrue(game.Hud.BreachLabel.activeSelf, "BREACH label, not color alone");
            Assert.AreEqual(new GridPos(9, 10), new GridPos(Mathf.FloorToInt(game.Overlay.BreachMarkerPosition.x), Mathf.FloorToInt(game.Overlay.BreachMarkerPosition.z)));
            Assert.AreEqual(game.Overlay.CandidateRoute.ReachableEndIndex + 1, game.Overlay.PrimaryLinePointCount, "solid line stops before the blocker");
            Assert.AreEqual(gold, game.Sim.Gold);
            Assert.AreEqual(revision, game.Sim.Grid.Revision);

            var plans = game.Overlay.PreviewPlanCount;
            for (var i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(plans, game.Overlay.PreviewPlanCount, "unchanged preview is not replanned every frame");

            var candidate = game.Overlay.CandidateRoute;
            yield return Click(Screen(10, 11));
            var actual = game.Sim.GetLiveRoute(WavePlan.MainEntry);
            Assert.AreEqual(candidate.Blocker, actual.Blocker, "preview and committed planning agree");
            Assert.AreEqual(string.Join(" ", candidate.Cells), string.Join(" ", actual.Cells));

            yield return MoveMouse(Screen(0, 6));
            Assert.IsFalse(game.Overlay.GhostValid);
            Assert.AreSame(actual, game.Overlay.PrimaryRoute, "invalid ghost keeps the committed route visible");
        }
    }
}
