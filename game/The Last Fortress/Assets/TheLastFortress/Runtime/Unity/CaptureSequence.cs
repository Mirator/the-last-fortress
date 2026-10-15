using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
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
                yield return RunPlan006();
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

        IEnumerator WaitFor(System.Func<bool> condition, float seconds, string what, StringBuilder log)
        {
            var end = Time.unscaledTime + seconds;
            while (!condition() && Time.unscaledTime < end) yield return null;
            if (!condition()) log.AppendLine("TIMEOUT waiting for " + what);
        }

        /// <summary>
        /// Plan 006 rendered checks: edge tooltips, live arrows and kill cues, pause/menu freeze, guard dialogs, reward
        /// forecast and recap, effective selection, Keep-hit cue with shake off and the Keep off-screen, Defeat recap.
        /// Values seen at each shot go to a capture log next to the screenshots.
        /// </summary>
        IEnumerator RunPlan006()
        {
            var log = new StringBuilder();
            game.QuitApplication = () => log.AppendLine("quit requested (suppressed during capture)");

            // Edge tooltips: zoomed in so board cells reach every screen corner; the Farm tooltip is the longest.
            game.NewRun(1, flat: true);
            game.CameraRig.SetView(CameraRig.DefaultFocus, 4f);
            yield return null;
            game.Player.SetTool(BuildingKind.Farm);
            var corners = new[] { ("bl", new Vector2(0, 0)), ("br", new Vector2(Screen.width, 0)), ("tl", new Vector2(0, Screen.height)), ("tr", new Vector2(Screen.width, Screen.height)) };
            foreach (var (name, corner) in corners)
            {
                GridPos? best = null;
                var bestD = float.MaxValue;
                for (var x = 0; x < GridCoordinates.Size; x++)
                for (var z = 0; z < GridCoordinates.Size; z++)
                {
                    var p = game.CameraRig.CellToScreen(new GridPos(x, z));
                    if (p.x < 0 || p.y < 0 || p.x > Screen.width || p.y > Screen.height) continue;
                    var d = (p - corner).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = new GridPos(x, z); }
                }

                game.Player.HoverOverride = best;
                yield return Shot("006-01-tooltip-" + name);
                log.AppendLine($"tooltip {name}: cell {best} shown={game.Hud.TooltipText != null} rect={game.Hud.TooltipCanvasRect} canvas={game.Hud.CanvasSize}");
            }

            game.Player.HoverOverride = null;
            game.Player.CancelTool();
            game.CameraRig.ResetView();

            // Night 1 in real time: live arrows and kill cues, then pause and menu freeze, then the Night quit guard.
            PlayDay(1);
            game.StartNight();
            game.AutoAdvance = true;
            // A Tower kill cue appears as its arrow lands, so arrows and kills are captured separately.
            yield return WaitFor(() => game.World.VisibleCues().Any(c => c.kind == CueKind.Kill), 60f, "kill cue", log);
            yield return Shot("006-02-night1-kill-cue");
            log.AppendLine($"night1 kill: state={game.Sim.State} arrows={game.World.ActiveArrowCount} cues={string.Join(",", game.World.VisibleCues().Select(c => c.kind))}");
            yield return WaitFor(() => game.World.ActiveArrowCount > 0 || game.Sim.State != RunState.Night, 60f, "arrow in flight", log);
            game.TogglePause(); // pause with the arrow in flight
            yield return Shot("006-02b-night1-arrow-in-flight-paused");
            var before = string.Join(";", game.World.ArrowPositions());
            var cuesBefore = game.World.VisibleCues().Count();
            yield return new WaitForSeconds(0.6f);
            yield return Shot("006-03-night1-paused-frozen");
            log.AppendLine($"pause: state={game.Sim.State} paused={game.Sim.Paused} arrows={game.World.ActiveArrowCount} unchanged={before == string.Join(";", game.World.ArrowPositions())} cues {cuesBefore}->{game.World.VisibleCues().Count()}");
            game.TogglePause();

            game.OpenMenu();
            before = string.Join(";", game.World.ArrowPositions());
            yield return new WaitForSeconds(0.6f);
            yield return Shot("006-04-night1-menu-frozen");
            log.AppendLine($"menu: arrows unchanged={before == string.Join(";", game.World.ArrowPositions())}");
            game.RequestQuit();
            yield return Shot("006-05-quit-guard-night");
            log.AppendLine("quit guard (night): " + string.Join(" | ", game.Hud.ConfirmOptions().Select(o => o.label)));
            game.CancelConfirm();
            game.CloseMenu();

            yield return WaitFor(() => game.Sim.State != RunState.Night, 180f, "night 1 end", log);
            yield return Shot("006-06-night1-last-kill-into-reward");
            log.AppendLine($"after night: state={game.Sim.State} cues={game.World.VisibleCues().Count()} arrows={game.World.ActiveArrowCount}");
            game.AutoAdvance = false;
            yield return new WaitForSeconds(1f);
            yield return Shot("006-07-reward-forecast-and-recap");
            log.AppendLine("reward header: " + game.Hud.RewardHeaderText);
            log.AppendLine("reward recap: " + game.Hud.RewardRecapText);

            // Prefer a Tower perk so the selection shows base vs effective values.
            var offer = game.Sim.CurrentOffer;
            if (offer != null)
            {
                foreach (var id in offer.PerkIds) log.AppendLine($"offer {id}: next dawn +{game.Sim.IncomeForecastWith(id).Total}");
                var pick = offer.PerkIds.FirstOrDefault(id => id == "Sharpshooters" || id == "FlamingArrows" || id == "ExpandedVision") ?? offer.PerkIds[0];
                game.ChoosePerk(offer.OfferId, pick);
                log.AppendLine("chose " + pick);
            }

            var tower = game.Sim.Grid.GetOccupant(new GridPos(9, 9));
            var farm = game.Sim.Grid.GetOccupant(new GridPos(4, 13));
            game.Player.Select(tower);
            yield return Shot("006-08-selected-tower-effective");
            if (farm != null) farm.Hp = Mathf.Max(1, farm.Hp - 33); // damaged: repair, refund, income and rotation all shown
            game.Player.Select(farm);
            yield return Shot("006-09-selected-farm-income-long");
            game.Player.Select(null);

            // Day 2 with an unsaved edit: Quit and Restart guards.
            game.Sim.TryPlace(BuildingKind.Wall, new GridPos(9, 10), out _);
            game.OpenMenu();
            game.RequestQuit();
            yield return Shot("006-10-quit-guard-unsaved-day");
            log.AppendLine("quit guard (day): " + string.Join(" | ", game.Hud.ConfirmOptions().Select(o => o.label)));
            game.CancelConfirm();
            game.CloseMenu();
            game.RequestRestart();
            yield return Shot("006-11-restart-guard");
            log.AppendLine("restart guard: " + string.Join(" | ", game.Hud.ConfirmOptions().Select(o => o.label)));
            game.CancelConfirm();

            // Undefended run: Keep-hit HUD cue with camera shake off and the Keep off-screen, then the Defeat recap.
            var shake = game.Settings.cameraShake;
            game.Settings.cameraShake = false;
            game.NewRun(1, flat: true);
            game.CameraRig.SetView(new Vector3(2f, 0f, 2f), 4f);
            game.StartNight();
            game.AutoAdvance = true;
            yield return WaitFor(() => game.Hud.KeepHitShown > 0, 120f, "Keep hit", log);
            yield return Shot("006-12-keep-hit-offscreen-no-shake");
            log.AppendLine($"keep hit shown={game.Hud.KeepHitShown} keepHp={game.Sim.Keep.Hp}");
            game.AutoAdvance = false;
            game.CameraRig.ResetView();
            for (var night = 0; night < 6 && !game.Sim.IsTerminal; night++)
            {
                if (game.Sim.State == RunState.Day) game.StartNight();
                while (game.Sim.State == RunState.Night) game.FastForward(1.0);
                if (game.Sim.State == RunState.Reward) ChooseFirstPerk();
            }

            yield return Shot("006-13-outcome-" + game.Sim.State + "-recap");
            log.AppendLine("outcome: " + game.Sim.State);
            game.Settings.cameraShake = shake;

            File.WriteAllText(Path.Combine(dir, $"{Screen.width}x{Screen.height}-006-capture-log.txt"), log.ToString());
        }
    }
}
