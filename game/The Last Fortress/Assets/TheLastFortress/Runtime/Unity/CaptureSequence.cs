using System.Collections;
using System.IO;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Scripted evidence screenshots (-capture &lt;dir&gt;): open route, breach preview, invalid ghost,
    /// night combat, victory, defeat, stress label. Quits when done.
    /// </summary>
    public sealed class CaptureSequence : MonoBehaviour
    {
        PrototypeBootstrap game;
        string dir;

        public void Begin(PrototypeBootstrap game, string directory)
        {
            this.game = game;
            dir = directory;
            Application.runInBackground = true; // an unfocused window must keep rendering
            Directory.CreateDirectory(dir);
            StartCoroutine(Run());
        }

        IEnumerator Shot(string name)
        {
            game.SyncViews();
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{Screen.width}x{Screen.height}-{name}.png"));
            yield return null;
            yield return null;
        }

        IEnumerator Run()
        {
            yield return null;
            var sim = game.Sim;
            game.AutoAdvance = false;

            // 0. Untouched launch state (default mode must be Day, normal wave)
            yield return Shot("00-launch-state");

            // 1. Open detour route with a selected Tower's range
            game.EnterNormalMode();
            sim = game.Sim;
            foreach (var c in new[] { new GridPos(5, 9), new GridPos(5, 10), new GridPos(5, 11) }) sim.TryPlace(BuildingKind.Wall, c, out _);
            sim.TryPlace(BuildingKind.Tower, new GridPos(8, 7), out var tower);
            game.Player.Select(tower);
            yield return Shot("01-day-open-route");

            // 2. Valid ghost that seals the Keep: breach preview
            game.Restart();
            sim = game.Sim;
            foreach (var c in new[] { new GridPos(9, 10), new GridPos(11, 10), new GridPos(10, 9) }) sim.TryPlace(BuildingKind.Wall, c, out _);
            game.Player.SetTool(BuildingKind.Wall);
            game.Player.HoverOverride = new GridPos(10, 11);
            yield return Shot("02-day-breach-preview");

            // 3. Invalid ghost keeps the committed route and explains why
            game.Player.SetTool(BuildingKind.Tower);
            game.Player.HoverOverride = new GridPos(0, 6);
            yield return Shot("03-day-invalid-ghost");
            game.Player.HoverOverride = null;
            game.Player.CancelTool();

            // 4-5. Winning layout: night combat, then victory overlay
            game.Restart();
            game.ApplyLayout(game.Content.winningLayout);
            game.StartNight();
            game.FastForward(7.0);
            yield return Shot("04-night-combat");
            while (game.Sim.State == RunState.Night) game.FastForward(1.0);
            yield return Shot("05-victory");

            // 6. Keep-only defeat
            game.Restart();
            game.StartNight();
            while (game.Sim.State == RunState.Night) game.FastForward(1.0);
            yield return Shot("06-defeat");

            // 7. Stress fixture label
            game.EnterStressMode();
            game.AutoAdvance = true;
            yield return new WaitForSeconds(4f);
            yield return Shot("07-stress-fixture");

            Application.Quit();
        }
    }
}
