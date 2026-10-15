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
            if (game.IsMvp)
            {
                yield return RunMvp();
                Application.Quit();
                yield break;
            }

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

        /// <summary>Economy-first Day scripts (same as the recorded EditMode policy) used to drive the MVP capture.</summary>
        static readonly string[][] CaptureDays =
        {
            new[] { "Tower 9 9", "Tower 11 11", "Farm 4 13", "House 6 13" },
            new[] { "Farm 4 11", "House 6 12" },
            new[] { "Tower 9 11" },
            new[] { "Tower 11 9", "Wall 9 10" },
            new[] { "Wall 11 10", "Wall 10 9", "Wall 10 11", "Tower 8 10", "Tower 12 10" },
            new[] { "Tower 10 8", "Tower 10 12", "Wall 7 10", "Wall 13 10", "Wall 10 7", "Wall 10 13" },
        };

        void PlayDay(int day)
        {
            foreach (var a in CaptureDays[day - 1])
            {
                var p = a.Split(' ');
                game.Sim.TryPlace((BuildingKind)System.Enum.Parse(typeof(BuildingKind), p[0]), new GridPos(int.Parse(p[1]), int.Parse(p[2])), out _);
            }

            game.Sim.TryRepair(game.Sim.Keep);
        }

        void ChooseFirstPerk()
        {
            var offer = game.Sim.CurrentOffer;
            if (offer != null) game.ChoosePerk(offer.OfferId, offer.PerkIds[0]);
        }

        /// <summary>
        /// MVP evidence (run with -saves &lt;temp dir&gt;): menu, footprint ghost, connected Walls, rotation, reward, Night, boss,
        /// Save and Quit + Continue, outcome, and the terrain of seeds 0-9.
        /// </summary>
        IEnumerator RunMvp()
        {
            yield return null;
            game.AutoAdvance = false;
            game.QuitToMenuWithoutSaving();
            yield return Shot("mvp-00-menu");

            // Connected Walls showcase on its own run: corner, T and cross bought with the normal 250 gold.
            game.NewRun(2, flat: true);
            var walls = new[]
            {
                new GridPos(14, 5), new GridPos(15, 5), new GridPos(14, 6),
                new GridPos(4, 5), new GridPos(5, 5), new GridPos(6, 5), new GridPos(5, 6),
                new GridPos(14, 13), new GridPos(13, 14), new GridPos(14, 14), new GridPos(15, 14), new GridPos(14, 15),
            };
            foreach (var c in walls) game.Sim.TryPlace(BuildingKind.Wall, c, out _);
            yield return Shot("mvp-01-connected-walls-corner-t-cross");
            game.Player.SetTool(BuildingKind.Wall);
            game.Player.HoverOverride = new GridPos(15, 6);
            yield return Shot("mvp-02-wall-ghost-connects");
            game.Player.HoverOverride = null;
            game.Player.CancelTool();

            // Main run: the recorded economy-first script.
            game.NewRun(1, flat: true);
            var sim = game.Sim;
            sim.TryPlace(BuildingKind.Tower, new GridPos(9, 9), out var tower);
            sim.TryPlace(BuildingKind.Tower, new GridPos(11, 11), out _);
            sim.TryPlace(BuildingKind.House, new GridPos(6, 13), out _);
            game.Player.SetTool(BuildingKind.Farm);
            game.Player.HoverOverride = new GridPos(4, 13);
            yield return Shot("mvp-03-farm-ghost-day1");
            game.Player.HoverOverride = null;
            game.Player.CancelTool();
            sim.TryPlace(BuildingKind.Farm, new GridPos(4, 13), 1, out var farm);
            sim.TryRotate(tower);
            game.Player.Select(farm);
            yield return Shot("mvp-04-rotated-farm-and-tower");
            game.Player.Select(null);

            for (var day = 1; day <= 6 && !game.Sim.IsTerminal; day++)
            {
                if (day > 1) PlayDay(day);
                if (day == 2)
                {
                    game.SaveAndQuit();
                    yield return Shot("mvp-06-menu-continue");
                    game.Continue();
                    game.CameraRig.SetView(new Vector3(4f, 0f, 9f), 5f); // zoomed in on the west: the new East side must still be signposted
                    yield return Shot("mvp-06b-zoomed-new-side-marker");
                    game.CameraRig.ResetView();
                }

                game.StartNight();
                if (day == 4)
                {
                    game.FastForward(14.0);
                    yield return Shot("mvp-07-night4-combat");
                }

                if (day == 6)
                {
                    game.FastForward(34.0);
                    yield return Shot("mvp-08-night6-boss");
                }

                while (game.Sim.State == RunState.Night) game.FastForward(1.0);
                if (game.Sim.State != RunState.Reward) break;
                if (day == 1) yield return Shot("mvp-05-reward");
                ChooseFirstPerk();
            }

            yield return Shot("mvp-09-outcome-" + game.Sim.State);

            // Terrain of seeds 0-9 (generated maps), zoomed out for the contact sheet.
            for (uint seed = 0; seed < 10; seed++)
            {
                game.NewRun(seed, flat: false);
                game.CameraRig.SetView(CameraRig.DefaultFocus, 12.5f);
                yield return Shot($"seed-{seed:00}");
            }

            game.CameraRig.ResetView();
        }
    }
}
