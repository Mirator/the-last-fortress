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

        /// <summary>-offscreen: render camera and HUD into a texture, so a hidden or minimized window still yields real frames.</summary>
        bool offscreen;

        /// <summary>-capture-set 007: only the plan 007 sequence (faster iteration); default runs every MVP sequence.</summary>
        string captureSet;

        public void Begin(PrototypeBootstrap game, string directory)
        {
            this.game = game;
            dir = directory;
            var args = System.Environment.GetCommandLineArgs();
            offscreen = System.Array.IndexOf(args, "-offscreen") >= 0;
            var setIndex = System.Array.IndexOf(args, "-capture-set");
            captureSet = setIndex >= 0 && setIndex + 1 < args.Length ? args[setIndex + 1] : null;
            Application.runInBackground = true; // an unfocused window must keep rendering
            game.FocusGateEnabled = false; // and keep playing: captures run unattended
            Directory.CreateDirectory(dir);
            StartCoroutine(Run());
        }

        IEnumerator Shot(string name)
        {
            game.SyncViews();
            yield return null;
            // Scripted jumps (FastForward, perk choice) skip the frames real play would have: let presentation catch up —
            // the light blend, arrows and cues in flight, the Keep-hit cue after a night — bounded, before the frame is taken.
            var settleUntil = Time.unscaledTime + 1.5f;
            while (Time.unscaledTime < settleUntil && !Settled()) yield return null;
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(dir, $"{Screen.width}x{Screen.height}-{name}.png");
            if (offscreen) CaptureOffscreen(path);
            else ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
        }

        bool Settled()
        {
            var night = game.Sim.State == RunState.Night || game.Sim.State == RunState.Reward || game.Sim.State == RunState.Defeat || game.Sim.StressMode;
            var lightDone = Mathf.Approximately(game.World.NightBlend, night ? 1f : 0f) || game.CombatFrozen;
            var combatQuiet = game.Sim.State == RunState.Night || game.CombatFrozen || game.World.ActiveArrowCount == 0 && game.Hud.KeepHitShown == 0;
            return lightDone && combatQuiet;
        }

        /// <summary>One camera render with the HUD temporarily drawn by that camera, read back to a PNG.</summary>
        void CaptureOffscreen(string path)
        {
            var cam = game.ActiveCamera; // strategic or hero view, whichever is rendering
            var canvas = game.Hud.Canvas;
            int w = Screen.width, h = Screen.height;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var mode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.005f; // in front of everything: ground and grass never cover the HUD
            Canvas.ForceUpdateCanvases();
            // Instanced draws (hero-view grass) are queued per frame and were consumed by the frame's own render.
            if (game.HeroViewActive) game.World.DrawGrass(cam, game.Sim);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            canvas.renderMode = mode;
            canvas.worldCamera = null;
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            RenderTexture.ReleaseTemporary(rt);
        }

        IEnumerator Run()
        {
            if (game.IsRelease && (captureSet == "motion" || captureSet == "fx"))
            {
                yield return RunMotionFrames(captureSet == "fx");
                Application.Quit();
                yield break;
            }

            if (game.IsRelease && captureSet == "hero-motion")
            {
                yield return RunHeroMotion();
                Application.Quit();
                yield break;
            }

            if (game.IsRelease && captureSet == "fire")
            {
                yield return RunFire();
                Application.Quit();
                yield break;
            }

            if (game.IsRelease && captureSet == "hero-camera")
            {
                yield return RunHeroCamera();
                Application.Quit();
                yield break;
            }

            if (game.IsRelease && captureSet == "scale")
            {
                yield return RunScaleLineup();
                Application.Quit();
                yield break;
            }

            if (game.IsRelease && captureSet == "023")
            {
                yield return RunPlan023();
                Application.Quit();
                yield break;
            }

            if (game.IsRelease && captureSet == "013")
            {
                yield return RunPlan013();
                Application.Quit();
                yield break;
            }

            if (game.IsRelease && captureSet == "012")
            {
                yield return RunPlan012();
                Application.Quit();
                yield break;
            }

            if (game.IsRelease && captureSet == "011")
            {
                yield return RunPlan011();
                Application.Quit();
                yield break;
            }

            if (game.IsRelease)
            {
                yield return RunPlan008();
                Application.Quit();
                yield break;
            }

            if (game.IsMvp)
            {
                if (captureSet != "007")
                {
                    yield return RunMvp();
                    yield return RunPlan006();
                }

                yield return RunPlan007();
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
                game.CameraRig.SetView(CameraRig.DefaultFocus, CameraRig.WholeBoardSize); // was 12.5 before plan 023 capped the zoom
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
            // Burn marker size: flag one enemy as burning for a paused frame only, then restore it.
            var burning = game.Sim.Enemies.FirstOrDefault(en => !en.Burning);
            if (burning != null)
            {
                burning.Burning = true;
                game.SyncViews(0.05f);
                yield return Shot("006-03b-burn-marker");
                burning.Burning = false;
            }

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

        /// <summary>
        /// Plan 007 rendered checks: title, first-run guide on a generated map, Help, Credits, Settings with volumes,
        /// escalation announcements (Days 2, 3, 6), a damaged building, Night lighting, the Golem banner, and Nights 4
        /// and 6 at real speed (1x, simulation not skipped) with audio voice statistics sampled every frame.
        /// </summary>
        /// <summary>Writes a Day checkpoint for <paramref name="sim"/> and continues it through the menu, as a relaunch would.</summary>
        void ContinueSaved(RunSimulation sim, StringBuilder log)
        {
            // A new slot after every earlier run (New Run never reuses an active slot), so Continue picks exactly this one.
            if (!game.Persistence.Store.WriteRun(game.Persistence.Store.LatestSlot() + 1, CheckpointCodec.Capture(sim), out var error)) log.AppendLine("save failed: " + error);
            game.OpenMenu();
            game.Continue();
        }

        /// <summary>Steps the paused-to-us simulation until the condition holds (bounded), then freezes it for the frame.</summary>
        bool StepUntil(System.Func<bool> condition, int maxSteps)
        {
            for (var i = 0; i < maxSteps && game.Sim.State == RunState.Night; i++)
            {
                if (condition()) break;
                game.Sim.Step();
                game.SyncViews(0.05f);
            }

            var ok = condition();
            if (game.Sim.State == RunState.Night) game.Sim.SetPaused(true); // presentation frozen: the cue is still there
            return ok;
        }

        GridPos? FirstLegal(BuildingKind kind, int x0, int z0, int x1, int z1)
        {
            for (var x = x0; x <= x1; x++)
            for (var z = z0; z <= z1; z++)
                if (game.Sim.CheckPlacement(kind, new GridPos(x, z)) == PlacementRejection.None) return new GridPos(x, z);
            return null;
        }

        /// <summary>
        /// Plan 008 release evidence: title with the map selector, Day 1 on each map profile with the two-row build cards,
        /// the Cannon/Watchtower/Repair Station previews, every new threat in combat (Cannon splash, Bomber fuse, Raider
        /// Archer, Siege Commander aura and bar), the final-siege announcement and the release reward cards.
        /// </summary>
        IEnumerator RunPlan008()
        {
            var log = new StringBuilder();
            game.QuitApplication = () => log.AppendLine("quit requested (suppressed during capture)");
            game.AutoAdvance = false;
            game.SkipOnboarding();
            game.QuitToMenuWithoutSaving();
            yield return Shot("008-01-title-ravine");
            game.Hud.MapButton.onClick.Invoke();
            yield return Shot("008-01b-title-crossing");
            log.AppendLine($"title: map={game.MapProfile}");

            var n = 2;
            foreach (var id in MapProfiles.All)
            {
                game.SetMapProfile(id);
                game.NewRun(7, false);
                yield return Shot($"008-0{n++}-day1-{id}");
                log.AppendLine($"day 1 {id}: {game.Sim.Config.Terrain.Describe()} info=[{game.Hud.InfoText.Replace("\n", " | ")}]");
            }

            // Ravine previews: Cannon range, Watchtower boost, Repair Station reach.
            game.SetMapProfile(MapProfiles.Ravine);
            game.NewRun(7, false);
            game.Sim.TryPlace(BuildingKind.Tower, new GridPos(8, 9), out _);
            game.Player.SetTool(BuildingKind.Cannon);
            game.Player.HoverOverride = FirstLegal(BuildingKind.Cannon, 5, 8, 7, 11);
            yield return Shot("008-05-cannon-ghost-range");
            log.AppendLine($"cannon ghost: ring {game.Overlay.RangeRingRadius:0.00} tooltip=[{game.Hud.TooltipText}]");
            game.Player.SetTool(BuildingKind.Watchtower);
            game.Player.HoverOverride = new GridPos(8, 8);
            yield return Shot("008-06-watchtower-ghost-boost");
            log.AppendLine($"watchtower ghost: marks {game.Overlay.BuffMarkCount} tooltip=[{game.Hud.TooltipText}]");
            game.Player.SetTool(BuildingKind.RepairStation);
            game.Player.HoverOverride = FirstLegal(BuildingKind.RepairStation, 11, 11, 14, 14);
            yield return Shot("008-07-repair-station-reach");
            log.AppendLine($"repair ghost: reach visible {game.Overlay.ReachVisible} tooltip=[{game.Hud.TooltipText}]");
            game.Player.HoverOverride = null;
            game.Player.CancelTool();

            // Every new threat on one stored night, loaded through the real save path.
            var wave = new WavePlan("threats", new[]
            {
                new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.Grunt), new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.Grunt),
                new SpawnEntry(4, MvpCampaign.West, 0, EnemyKind.Grunt), new SpawnEntry(4, MvpCampaign.West, 0, EnemyKind.Runner),
                new SpawnEntry(10, MvpCampaign.East, 1, EnemyKind.Bomber), new SpawnEntry(10, MvpCampaign.West, 0, EnemyKind.Archer),
                new SpawnEntry(30, MvpCampaign.West, 0, EnemyKind.SiegeCommander), new SpawnEntry(40, MvpCampaign.West, 0, EnemyKind.Grunt),
                new SpawnEntry(44, MvpCampaign.West, 0, EnemyKind.Grunt), new SpawnEntry(48, MvpCampaign.West, 0, EnemyKind.Brute),
            }, new[] { MvpCampaign.West, MvpCampaign.East });
            var calm = new WavePlan("calm", new SpawnEntry[0], new[] { MvpCampaign.West });
            // The showcase plays under release-rules-2 (sprint speeds): its small fortress and step budgets were written for
            // them, and under release-rules-3 the Keep fell before the Archer, Bomber and Commander showed. Presentation is the same.
            CampaignConfig Custom(params WavePlan[] nights) => new CampaignConfig
            {
                Label = ReleaseCampaign.Label, Nights = nights, OffersRewards = true, RulesVersion = ReleaseCampaign.RulesV2,
                CampaignId = ReleaseCampaign.CampaignId, PerkPool = PerkRules.ReleaseIds, MapProfile = MapProfiles.Meadow,
            };
            var sprintDefinitions = game.Catalog.Find(ReleaseCampaign.RulesV2, ReleaseCampaign.CampaignId).Definitions;
            var setup = new RunSimulation(new RunConfig { Definitions = sprintDefinitions, Seed = 7, Terrain = MapProfiles.Generate(MapProfiles.Meadow, 7), Campaign = Custom(wave, calm) });
            ContinueSaved(setup, log);
            foreach (var (kind, cell) in new[] { (BuildingKind.Cannon, new GridPos(6, 8)), (BuildingKind.Watchtower, new GridPos(8, 9)), (BuildingKind.House, new GridPos(7, 12)) })
                log.AppendLine($"place {kind} {cell}: {game.Sim.TryPlace(kind, cell, out _)}");
            game.StartNight();
            for (var i = 0; i < 60; i++) yield return null; // light blend to night at real frames, simulation not advancing
            log.AppendLine($"cannon splash: {StepUntil(() => game.World.VisibleCues().Any(c => c.kind == CueKind.Blast), 800)}");
            yield return Shot("008-08-cannon-splash");
            game.Sim.SetPaused(false);
            log.AppendLine($"raider archer: {StepUntil(() => game.Sim.Enemies.Any(e => e.Kind == EnemyKind.Archer && e.TargetBuildingId >= 0) && game.World.ActiveArrowCount > 0, 1200)}");
            yield return Shot("008-09-raider-archer-shooting");
            game.Sim.SetPaused(false);
            log.AppendLine($"bomber fuse: {StepUntil(() => game.Sim.Enemies.Any(e => e.FuseLit), 1200)}");
            yield return Shot("008-10-bomber-fuse");
            game.Sim.SetPaused(false);
            log.AppendLine($"bomber blast: {StepUntil(() => game.World.VisibleCues().Any(c => c.kind == CueKind.Blast && c.position.x > 9f), 200)}");
            yield return Shot("008-11-bomber-blast");
            game.Sim.SetPaused(false);
            log.AppendLine($"commander: {StepUntil(() => game.Sim.Enemies.Any(e => e.Kind == EnemyKind.SiegeCommander && e.X > 4.0), 1500)} banner=[{game.Hud.GolemBannerText}]");
            yield return Shot("008-12-commander-aura-boss-bar");
            game.Sim.SetPaused(false);
            StepUntil(() => game.Sim.State != RunState.Night, 6000);
            log.AppendLine($"threat night: {game.Sim.State} keep {game.Sim.Keep.Hp}/{game.Sim.Keep.MaxHp}");

            // The real Night 12 after a calm night: release reward cards, then the final-siege announcement.
            var real = ReleaseCampaign.Create(7, MapProfiles.Meadow);
            setup = new RunSimulation(new RunConfig { Definitions = game.NewRunDefinitions, Seed = 7, Terrain = MapProfiles.Generate(MapProfiles.Meadow, 7), Campaign = Custom(calm, real.Nights[11]) });
            ContinueSaved(setup, log);
            game.StartNight();
            game.Sim.Step();
            yield return Shot("008-13-reward-release-perks");
            log.AppendLine($"reward: {string.Join(",", game.Sim.CurrentOffer?.PerkIds ?? new string[0])}");
            if (game.Sim.CurrentOffer != null) game.ChoosePerk(game.Sim.CurrentOffer.OfferId, game.Sim.CurrentOffer.PerkIds[0]);
            yield return Shot("008-14-final-siege-announcement");
            log.AppendLine($"announcement: [{game.Hud.AnnouncementText}] info=[{game.Hud.InfoText.Replace("\n", " | ")}]");

            game.Hud.HelpButton.onClick.Invoke();
            yield return Shot("008-15-help");
            game.Hud.HelpPanel.SetActive(false);

            File.WriteAllText(Path.Combine(dir, $"{Screen.width}x{Screen.height}-008-capture-log.txt"), log.ToString());
        }

        /// <summary>The same small fortress on every map: an L of Walls west and north of the Keep, two Archer Towers, a House, a Farm, a trap.</summary>
        /// <summary>
        /// -capture-set 012 (release build): the hero view by Day and Night on each map profile, looking out to the board
        /// edge and back at the Keep, plus the strategic view with the hero's ring, for the close-range art audit.
        /// </summary>
        IEnumerator RunPlan012()
        {
            var log = new StringBuilder();
            game.QuitApplication = () => log.AppendLine("quit requested (suppressed during capture)");
            game.AutoAdvance = false;
            game.SkipOnboarding();
            foreach (var id in MapProfiles.All)
            {
                game.SetMapProfile(id);
                game.NewRun(7, false);
                foreach (var (kind, x, z) in Plan011Layout) game.Sim.TryPlace(kind, new GridPos(x, z), out _);
                HeroWalk(0.65f, new HeroCommand(0, false, 0)); // north of the Keep, then west onto open ground
                HeroWalk(1.0f, new HeroCommand(12, false, 48));
                log.AppendLine($"{id}: {game.Sim.Config.Terrain.Describe()} hero at {game.Sim.Hero.X:0.00},{game.Sim.Hero.Z:0.00}");
                game.SetHeroView(true);
                game.HeroRig.SkipTransition();
                game.HeroRig.SetOrbit(270f, 14f);
                yield return Shot($"012-{id}-1-day-edge");
                game.HeroRig.SetOrbit(90f, 18f);
                yield return Shot($"012-{id}-2-day-keep");
                game.StartNight();
                for (var i = 0; i < 60; i++) yield return null; // night light at real frames
                log.AppendLine($"{id} night: {StepUntil(() => game.Sim.AliveEnemyCount >= 4, 1600)} alive={game.Sim.AliveEnemyCount}");
                game.HeroRig.SetOrbit(270f, 14f);
                yield return Shot($"012-{id}-3-night-edge");
                game.HeroRig.SetOrbit(90f, 18f);
                yield return Shot($"012-{id}-4-night-keep");
                game.SetHeroView(false);
                yield return Shot($"012-{id}-5-strategic-night");
                game.Sim.SetPaused(false);
            }

            File.WriteAllText(Path.Combine(dir, $"{Screen.width}x{Screen.height}-012-capture-log.txt"), log.ToString());
        }

        /// <summary>
        /// -capture-set scale: the hero face to face with the Keep, a Wall, an Archer Tower and a House, seen from the side at
        /// eye height in the hero view, for judging relative scale.
        /// </summary>
        IEnumerator RunScaleLineup()
        {
            game.AutoAdvance = false;
            game.SkipOnboarding();
            game.SetMapProfile(MapProfiles.Meadow);
            game.NewRun(7, false);
            var sim = game.Sim;
            foreach (var (kind, x, z) in new[] { (BuildingKind.Wall, 9, 12), (BuildingKind.Tower, 11, 12), (BuildingKind.House, 12, 11) })
                sim.TryPlace(kind, new GridPos(x, z), out _);
            sim.AdvanceDay(0.6f, new HeroCommand(0, false, 0)); // north of the Keep, between Wall and Tower
            game.SetHeroView(true);
            game.HeroRig.SkipTransition();
            foreach (var (name, yaw, pitch) in new[] { ("side", 270f, 4f), ("front", 180f, 6f), ("back", 0f, 6f) })
            {
                game.HeroRig.SetOrbit(yaw, pitch);
                yield return Shot($"scale-{name}");
            }
        }

        /// <summary>
        /// -capture-set 013 (release build): matched before/after views for the second visual pass, through both cameras.
        /// Per map profile at seed 7: the plan 011 fortress by Day, a placement ghost and Night 1 in the strategic view, and
        /// the hero view toward the board edge and the Keep by Day and Night, the whole board by Day and Night, enemies walking in. Then the slice up close (low and high hero angles, a
        /// tower right behind the hero camera, a swing at an engaged Grunt), the dense fight in both views, every building
        /// kind in a row, and every enemy kind passing the hero. Simulation-driven and deterministic; only presentation differs.
        /// </summary>
        IEnumerator RunPlan013()
        {
            var log = new StringBuilder();
            game.QuitApplication = () => log.AppendLine("quit requested (suppressed during capture)");
            game.AutoAdvance = false;
            game.SkipOnboarding();
            foreach (var id in MapProfiles.All)
            {
                game.SetMapProfile(id);
                game.NewRun(7, false);
                foreach (var (kind, x, z) in Plan011Layout) game.Sim.TryPlace(kind, new GridPos(x, z), out _);
                HeroWalk(0.65f, new HeroCommand(0, false, 0)); // north of the Keep, then west onto open ground
                HeroWalk(1.0f, new HeroCommand(12, false, 48));
                log.AppendLine($"{id}: {game.Sim.Config.Terrain.Describe()} hero at {game.Sim.Hero.X:0.00},{game.Sim.Hero.Z:0.00}");
                game.CameraRig.ResetView();
                yield return Shot($"013-{id}-s1-day");
                game.CameraRig.ShowWholeBoard();
                yield return Shot($"013-{id}-s4-whole-day"); // plan 016: the board's edge, the roads and the mist around it
                game.CameraRig.ResetView();
                game.Player.SetTool(BuildingKind.Tower);
                game.Player.HoverOverride = FirstLegal(BuildingKind.Tower, 6, 6, 9, 9);
                yield return Shot($"013-{id}-s2-placement");
                game.Player.HoverOverride = null;
                game.Player.CancelTool();
                yield return HeroShot($"013-{id}-h1-day-edge", 270f, 6f);
                yield return HeroShot($"013-{id}-h2-day-keep", 90f, 12f);
                game.StartNight();
                for (var i = 0; i < 60; i++) yield return null; // night light at real frames; the simulation is not advancing
                // Plan 016: enemies walking in out of the mist along the first entry's road, close up.
                log.AppendLine($"{id} arrivals: {StepUntil(() => game.Sim.SpawnedCount >= 2, 400)} walking={game.World.ArrivalCount}");
                var firstEntry = game.Sim.Wave.EntryCells[0];
                game.CameraRig.SetView(Prims.CellCenter(firstEntry) + RouteOverlay.Outward(firstEntry) * 2.5f, 3.2f);
                yield return Shot($"013-{id}-s6-arrivals-night");
                game.CameraRig.ResetView();
                log.AppendLine($"{id} night:{StepUntil(() => game.Sim.AliveEnemyCount >= 4 && game.World.ActiveArrowCount > 0, 1600)} alive={game.Sim.AliveEnemyCount}");
                yield return Shot($"013-{id}-s3-night");
                game.CameraRig.ShowWholeBoard();
                yield return Shot($"013-{id}-s5-whole-night");
                game.CameraRig.ResetView();
                yield return HeroShot($"013-{id}-h3-night-keep", 90f, 12f);
                yield return HeroShot($"013-{id}-h4-night-edge", 270f, 6f);
                game.Sim.SetPaused(false);
            }

            // The slice up close on Crossing: strategic close views, the hero view low and high, and a tower behind the camera.
            game.SetMapProfile(MapProfiles.Crossing);
            game.NewRun(7, false);
            foreach (var (kind, x, z) in Plan011Layout) game.Sim.TryPlace(kind, new GridPos(x, z), out _);
            HeroWalk(0.65f, new HeroCommand(0, false, 0));
            HeroWalk(1.0f, new HeroCommand(12, false, 48));
            game.CameraRig.SetView(new Vector3(10f, 0f, 10.5f), 4.5f);
            yield return Shot("013-slice-s-day-close");
            game.CameraRig.ResetView();
            yield return HeroShot("013-slice-h-day-low", 60f, 2f);
            yield return HeroShot("013-slice-h-day-high", 135f, 35f);
            var hero = game.Sim.Hero;
            var behind = Mathf.Atan2((float)hero.X - 9.5f, (float)hero.Z - 11.5f) * Mathf.Rad2Deg; // Tower (9,11) behind the camera
            yield return HeroShot("013-slice-h-obstruction", behind, 10f);
            log.AppendLine($"slice: hero at {hero.X:0.00},{hero.Z:0.00}, obstruction yaw {behind:0}");

            // Night on the slice: Grunts from the West walk into the hero on their row; it swings at the first that engages.
            // The frame shows the blade one step after the swing resolved, at its contact.
            var grunts = new System.Collections.Generic.List<SpawnEntry>();
            for (var i = 0; i < 6; i++) grunts.Add(new SpawnEntry(i * 30, MvpCampaign.West, 0, EnemyKind.Grunt));
            ContinueSaved(Plan013Setup(MapProfiles.Crossing, new WavePlan("slice", grunts, new[] { MvpCampaign.West }), true), log);
            hero = game.Sim.Hero;
            HeroWalk(1.4f, new HeroCommand(0, false, 0)); // around the north Walls into the west entry on row 10
            HeroWalk(2.0f, new HeroCommand(12, false, 48));
            HeroWalk(2.07f, new HeroCommand(8, false, 32));
            game.StartNight();
            for (var i = 0; i < 60; i++) yield return null;
            log.AppendLine($"slice night: hero at {hero.X:0.00},{hero.Z:0.00} engage {StepUntil(() => NearestEnemyTo(hero, out var d) != null && d < 0.55, 2400)}");
            game.Sim.SetPaused(false);
            var swung = false;
            for (var i = 0; i < 40 && game.Sim.State == RunState.Night && !swung; i++)
            {
                var target = NearestEnemyTo(hero, out _);
                var facing = target == null ? hero.Facing : HeroRules.FacingToward(target.X - hero.X, target.Z - hero.Z);
                var ready = hero.SwingReadyTick <= game.Sim.Tick;
                game.Sim.Step(new HeroCommand(-1, true, facing));
                swung = ready && hero.SwingReadyTick > game.Sim.Tick;
                game.SyncViews(0.05f);
            }

            game.Sim.Step(new HeroCommand(-1, false, hero.Facing)); // one step on: the shown blade is at its contact
            game.SyncViews(0.05f);
            if (game.Sim.State == RunState.Night) game.Sim.SetPaused(true);
            log.AppendLine($"slice swing: {swung} hero hp={hero.Hp}");
            var foe = NearestEnemyTo(hero, out _);
            var aim = foe == null ? 270f : Mathf.Atan2((float)(foe.X - hero.X), (float)(foe.Z - hero.Z)) * Mathf.Rad2Deg;
            yield return HeroShot("013-slice-h-night-swing", aim + 35f, 8f);
            log.AppendLine("slice cues: " + string.Join(" ", game.World.VisibleCues().Select(c => $"{c.kind}@{c.position.x:0.00},{c.position.y:0.00},{c.position.z:0.00}")) +
                           $" particles={game.World.ParticleCount} grass={game.World.GrassDrawn}");
            yield return HeroShot("013-slice-h-night-keep", 90f, 14f);
            game.CameraRig.SetView(new Vector3(10f, 0f, 10.5f), 4.5f);
            yield return Shot("013-slice-s-night-close");
            game.CameraRig.ResetView();
            game.Sim.SetPaused(false);

            // Dense fight: 40 mixed attackers from two sides against the same fortress plus a Cannon, in both views.
            var spawns = new System.Collections.Generic.List<SpawnEntry>();
            var mix = new[] { EnemyKind.Grunt, EnemyKind.Grunt, EnemyKind.Runner, EnemyKind.Grunt, EnemyKind.Archer, EnemyKind.Brute, EnemyKind.Bomber, EnemyKind.Grunt };
            for (var i = 0; i < 40; i++) spawns.Add(new SpawnEntry(i * 3, i % 2 == 0 ? MvpCampaign.West : MvpCampaign.East, i % 2, mix[i % mix.Length]));
            ContinueSaved(Plan013Setup(MapProfiles.Meadow, new WavePlan("dense", spawns, new[] { MvpCampaign.West, MvpCampaign.East }), true), log);
            HeroWalk(1.2f, new HeroCommand(12, false, 48));
            game.StartNight();
            for (var i = 0; i < 60; i++) yield return null;
            log.AppendLine($"dense: {StepUntil(() => game.Sim.AliveEnemyCount >= 24, 2400)} alive={game.Sim.AliveEnemyCount}");
            yield return Shot("013-dense-s");
            game.CameraRig.SetView(new Vector3(8f, 0f, 10f), 5f);
            yield return Shot("013-dense-s-close");
            game.CameraRig.ResetView();
            yield return HeroShot("013-dense-h-west", 270f, 10f);
            yield return HeroShot("013-dense-h-east", 90f, 14f);
            game.Sim.SetPaused(false);

            // Every building kind in one row north of the Keep (Meadow), strategic and hero view.
            var rowSim = Plan013Setup(MapProfiles.Meadow, new WavePlan("calm", new SpawnEntry[0], new[] { MvpCampaign.West }), false, 1000);
            var row = new StringBuilder();
            foreach (var (kind, x, z) in Plan013Row)
            {
                GridPos? at = null;
                for (var dx = 0; dx <= 2 && at == null; dx++)
                for (var dz = -1; dz <= 2 && at == null; dz++)
                    if (rowSim.CheckPlacement(kind, new GridPos(x + dx, z + dz)) == PlacementRejection.None) at = new GridPos(x + dx, z + dz);
                row.Append($"{kind}{at}={(at.HasValue ? rowSim.TryPlace(kind, at.Value, out _).ToString() : "none")} ");
            }

            ContinueSaved(rowSim, log);
            log.AppendLine("row: " + row);
            HeroWalk(0.9f, new HeroCommand(0, false, 0));
            game.CameraRig.SetView(new Vector3(10f, 0f, 13f), 5.5f);
            yield return Shot("013-row-s-day");
            game.CameraRig.ResetView();
            yield return HeroShot("013-row-h-day", 0f, 8f);
            yield return HeroShot("013-row-h-day-back", 180f, 8f); // the far side of the Keep and its neighbours

            // Every enemy kind passing the hero west of the Keep: slowest first so the group arrives together.
            var lineup = new[] { EnemyKind.SiegeCommander, EnemyKind.SiegeGolem, EnemyKind.Brute, EnemyKind.Archer, EnemyKind.Grunt, EnemyKind.Bomber, EnemyKind.Runner };
            var lineupSpawns = new System.Collections.Generic.List<SpawnEntry>();
            for (var i = 0; i < lineup.Length; i++) lineupSpawns.Add(new SpawnEntry(i * 22, MvpCampaign.West, 0, lineup[i]));
            ContinueSaved(Plan013Setup(MapProfiles.Meadow, new WavePlan("lineup", lineupSpawns, new[] { MvpCampaign.West }), false), log);
            HeroWalk(0.9f, new HeroCommand(0, false, 0));
            HeroWalk(3.2f, new HeroCommand(12, false, 48));
            game.StartNight();
            for (var i = 0; i < 60; i++) yield return null;
            log.AppendLine($"lineup: {StepUntil(() => game.Sim.Enemies.Count(e => e.Alive) == lineup.Length && game.Sim.Enemies.All(e => !e.Alive || e.X > 2.6), 1600)} " +
                           string.Join(" ", game.Sim.Enemies.Where(e => e.Alive).Select(e => $"{e.Kind}@{e.X:0.0},{e.Z:0.0}")));
            game.CameraRig.SetView(new Vector3(4f, 0f, 10.5f), 3.2f);
            yield return Shot("013-lineup-s");
            game.CameraRig.ResetView();
            var cx = (float)game.Sim.Enemies.Where(e => e.Alive).DefaultIfEmpty().Average(e => e == null ? 4.0 : e.X);
            var cz = (float)game.Sim.Enemies.Where(e => e.Alive).DefaultIfEmpty().Average(e => e == null ? 10.5 : e.Z);
            yield return HeroShot("013-lineup-h", Mathf.Atan2(cx - (float)game.Sim.Hero.X, cz - (float)game.Sim.Hero.Z) * Mathf.Rad2Deg, 6f);
            game.Sim.SetPaused(false);
            File.WriteAllText(Path.Combine(dir, $"{Screen.width}x{Screen.height}-013-capture-log.txt"), log.ToString());
        }

        /// <summary>
        /// -capture-set 023 (release build): strategic readability before/after (spec 009). The dense mixed fight at half-heights
        /// 4.0, 4.5, 5.5 and 10.5, later with the attackers at the Walls, once with figures drawn twice as large (option A, for
        /// comparison only), and once through the hero camera (must look unchanged); every enemy kind side by side at 4.5 and
        /// 10.5; the Day board at 4.5 and 10.5; the 100-enemy stress fixture at 5.5 and 10.5. Explicit sizes, so the frames
        /// match across builds whatever the default zoom is.
        /// </summary>
        IEnumerator RunPlan023()
        {
            var log = new StringBuilder();
            game.QuitApplication = () => log.AppendLine("quit requested (suppressed during capture)");
            game.AutoAdvance = false;
            game.SkipOnboarding();

            // Plan 025 (spec 010): Night start frames the Keep and tonight's entries (Night 1 of seed 7: the North only).
            IEnumerator NightFraming()
            {
                var until = Time.realtimeSinceStartup + 3f;
                while (game.CameraRig.Easing && Time.realtimeSinceStartup < until) yield return null;
                log.AppendLine($"framing: focus={game.CameraRig.Focus:F2} size={game.CameraRig.Size:F2} easing={game.CameraRig.Easing}");
            }

            game.NewRun(7, false);
            yield return null;
            game.CameraRig.ResetView();
            game.StartNight();
            yield return NightFraming();
            StepUntil(() => game.Sim.AliveEnemyCount >= 3, 600);
            yield return Shot("025-night1-start");

            var spawns = new System.Collections.Generic.List<SpawnEntry>();
            var mix = new[] { EnemyKind.Grunt, EnemyKind.Grunt, EnemyKind.Runner, EnemyKind.Grunt, EnemyKind.Archer, EnemyKind.Brute, EnemyKind.Bomber, EnemyKind.Grunt };
            for (var i = 0; i < 40; i++) spawns.Add(new SpawnEntry(i * 3, i % 2 == 0 ? MvpCampaign.West : MvpCampaign.East, i % 2, mix[i % mix.Length]));
            ContinueSaved(Plan013Setup(MapProfiles.Meadow, new WavePlan("dense", spawns, new[] { MvpCampaign.West, MvpCampaign.East }), true), log);
            HeroWalk(1.2f, new HeroCommand(12, false, 48));
            game.CameraRig.SetView(CameraRig.DefaultFocus, 4.5f);
            yield return Shot("023-board-day-4.5");
            game.CameraRig.SetView(CameraRig.DefaultFocus, 10.5f);
            yield return Shot("023-board-day-10.5");
            game.StartNight();
            yield return NightFraming();
            log.AppendLine($"dense: {StepUntil(() => game.Sim.AliveEnemyCount >= 24, 2400)} alive={game.Sim.AliveEnemyCount}");
            yield return Shot("025-dense-night-start");
            foreach (var size in new[] { 4.0f, 4.5f, 5.5f, 10.5f })
            {
                game.CameraRig.SetView(CameraRig.DefaultFocus, size);
                yield return Shot("023-dense-" + size.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            }

            // Option A for comparison: every shown enemy drawn twice as large (presentation only, this frame only).
            var enemies = GameObject.Find("Enemies");
            if (enemies != null)
            {
                foreach (Transform t in enemies.transform) t.localScale *= 2f;
                game.CameraRig.SetView(CameraRig.DefaultFocus, 4.5f);
                yield return Shot("023-dense-4.5-optionA");
                foreach (Transform t in enemies.transform) t.localScale /= 2f;
            }

            yield return HeroShot("023-dense-h-west", 270f, 10f);
            game.Sim.SetPaused(false);
            log.AppendLine($"walls: {StepUntil(() => game.Sim.Enemies.Count(e => e.Alive && e.TargetBuildingId >= 0) >= 8, 2400)} " +
                           $"alive={game.Sim.AliveEnemyCount} at buildings={game.Sim.Enemies.Count(e => e.Alive && e.TargetBuildingId >= 0)}");
            game.CameraRig.SetView(new Vector3(8f, 0f, 10f), 4.5f);
            yield return Shot("023-walls-4.5");
            game.CameraRig.SetView(new Vector3(8f, 0f, 10f), 3.5f);
            yield return Shot("023-walls-3.5");
            game.CameraRig.SetView(CameraRig.DefaultFocus, 10.5f);
            yield return Shot("023-walls-10.5");
            game.Sim.SetPaused(false);

            // Every enemy kind side by side, slowest first so the group arrives together.
            var lineup = new[] { EnemyKind.SiegeCommander, EnemyKind.SiegeGolem, EnemyKind.Brute, EnemyKind.Archer, EnemyKind.Grunt, EnemyKind.Bomber, EnemyKind.Runner };
            var lineupSpawns = new System.Collections.Generic.List<SpawnEntry>();
            for (var i = 0; i < lineup.Length; i++) lineupSpawns.Add(new SpawnEntry(i * 22, MvpCampaign.West, 0, lineup[i]));
            ContinueSaved(Plan013Setup(MapProfiles.Meadow, new WavePlan("lineup", lineupSpawns, new[] { MvpCampaign.West }), false), log);
            game.StartNight();
            for (var i = 0; i < 60; i++) yield return null;
            log.AppendLine($"lineup: {StepUntil(() => game.Sim.Enemies.Count(e => e.Alive) == lineup.Length && game.Sim.Enemies.All(e => !e.Alive || e.X > 2.6), 1600)} " +
                           string.Join(" ", game.Sim.Enemies.Where(e => e.Alive).Select(e => $"{e.Kind}@{e.X:0.0},{e.Z:0.0}")));
            var cx = (float)game.Sim.Enemies.Where(e => e.Alive).DefaultIfEmpty().Average(e => e == null ? 4.0 : e.X);
            var cz = (float)game.Sim.Enemies.Where(e => e.Alive).DefaultIfEmpty().Average(e => e == null ? 10.5 : e.Z);
            game.CameraRig.SetView(new Vector3(cx, 0f, cz), 4.5f);
            yield return Shot("023-lineup-4.5");
            game.CameraRig.SetView(new Vector3(cx, 0f, cz), 3.5f);
            yield return Shot("023-lineup-3.5");
            game.CameraRig.SetView(CameraRig.DefaultFocus, 10.5f);
            yield return Shot("023-lineup-10.5");
            game.Sim.SetPaused(false);

            game.EnterStressMode();
            game.AutoAdvance = true;
            yield return new WaitForSeconds(6f);
            log.AppendLine($"stress: alive={game.Sim.AliveEnemyCount}");
            game.CameraRig.SetView(CameraRig.DefaultFocus, 5.5f);
            yield return Shot("023-stress-5.5");
            game.CameraRig.SetView(CameraRig.DefaultFocus, 10.5f);
            yield return Shot("023-stress-10.5");
            File.WriteAllText(Path.Combine(dir, $"{Screen.width}x{Screen.height}-023-capture-log.txt"), log.ToString());
        }

        static readonly (BuildingKind kind, int x, int z)[] Plan013Row =
        {
            (BuildingKind.Wall, 4, 13), (BuildingKind.Wall, 5, 13), (BuildingKind.Tower, 7, 13), (BuildingKind.House, 9, 13), (BuildingKind.Farm, 11, 13),
            (BuildingKind.SpikeTrap, 14, 13), (BuildingKind.Cannon, 15, 13), (BuildingKind.Watchtower, 6, 15), (BuildingKind.RepairStation, 9, 16),
        };

        /// <summary>A release run on <paramref name="profile"/> at seed 7 whose first night is <paramref name="night"/>; with the plan 011 fortress (+ Cannon) or empty.</summary>
        RunSimulation Plan013Setup(string profile, WavePlan night, bool fortress, int gold = 0)
        {
            var definitions = game.NewRunDefinitions;
            var startingGold = definitions.StartingGold;
            if (gold > 0) definitions.StartingGold = gold; // read once by the constructor; restored right after
            var setup = new RunSimulation(new RunConfig
            {
                Definitions = definitions, Seed = 7, Terrain = MapProfiles.Generate(profile, 7),
                Campaign = new CampaignConfig
                {
                    Label = ReleaseCampaign.Label, Nights = new[] { night, new WavePlan("calm", new SpawnEntry[0], new[] { MvpCampaign.West }) },
                    OffersRewards = true, RulesVersion = ReleaseCampaign.RulesVersion, CampaignId = ReleaseCampaign.CampaignId,
                    PerkPool = PerkRules.ReleaseIds, MapProfile = profile,
                },
            });
            definitions.StartingGold = startingGold;
            if (!fortress) return setup;
            foreach (var (kind, x, z) in Plan011Layout) setup.TryPlace(kind, new GridPos(x, z), out _);
            setup.TryPlace(BuildingKind.Cannon, new GridPos(13, 10), out _);
            return setup;
        }

        /// <summary>
        /// Walks the hero by Day for the time the scripted paths were written for (release-rules-2, 1.5 tiles/s), stretched to
        /// the run's own hero speed so it reaches the same places.
        /// </summary>
        void HeroWalk(float seconds, HeroCommand command) =>
            game.Sim.AdvanceDay(seconds * HeroRules.MoveUnitsPerStep / Mathf.Max(1, game.Sim.Hero.MoveUnits), command);

        /// <summary>The hero view at a fixed orbit (no transition), then back to the strategic view.</summary>
        IEnumerator HeroShot(string name, float yaw, float pitch)
        {
            if (!game.SetHeroView(true) && !game.HeroViewActive) yield break;
            game.HeroRig.SkipTransition();
            game.HeroRig.SetOrbit(yaw, pitch);
            for (var i = 0; i < 20; i++) yield return null; // the camera's obstacle response eases in over real frames
            yield return Shot(name);
            game.SetHeroView(false);
        }

        Enemy NearestEnemyTo(Hero hero, out double distance)
        {
            Enemy best = null;
            distance = double.MaxValue;
            foreach (var e in game.Sim.Enemies)
            {
                if (!e.Alive) continue;
                var d = System.Math.Sqrt((e.X - hero.X) * (e.X - hero.X) + (e.Z - hero.Z) * (e.Z - hero.Z));
                if (d < distance) (best, distance) = (e, d);
            }

            return best;
        }

        static readonly (BuildingKind kind, int x, int z)[] Plan011Layout =
        {
            (BuildingKind.Wall, 8, 8), (BuildingKind.Wall, 8, 9), (BuildingKind.Wall, 8, 11), (BuildingKind.Wall, 8, 12), (BuildingKind.Wall, 9, 12),
            (BuildingKind.Wall, 11, 12), (BuildingKind.Wall, 12, 12), (BuildingKind.Tower, 9, 11), (BuildingKind.Tower, 11, 9),
            (BuildingKind.House, 12, 8), (BuildingKind.Farm, 13, 14), (BuildingKind.SpikeTrap, 6, 10),
        };

        /// <summary>
        /// -capture-set 011 (release build): matched before/after views for the visual upgrade. Per map profile at seed 7 and
        /// default zoom: Day with the same fortress, an Archer Tower placement ghost, and Night 1 in combat. Then a close view
        /// of the Keep/Wall/Tower slice by Day and Night, and a dense mixed fight. Simulation-driven and deterministic, so the
        /// same build inputs give the same game states; only presentation differs between builds.
        /// </summary>
        IEnumerator RunPlan011()
        {
            var log = new StringBuilder();
            game.QuitApplication = () => log.AppendLine("quit requested (suppressed during capture)");
            game.AutoAdvance = false;
            game.SkipOnboarding();
            foreach (var id in MapProfiles.All)
            {
                game.SetMapProfile(id);
                game.NewRun(7, false);
                var placed = new StringBuilder();
                foreach (var (kind, x, z) in Plan011Layout)
                    placed.Append($"{kind}({x},{z})={game.Sim.TryPlace(kind, new GridPos(x, z), out _)} ");
                log.AppendLine($"{id}: {game.Sim.Config.Terrain.Describe()} {placed}");
                game.CameraRig.ResetView();
                yield return Shot($"011-{id}-1-day");
                game.Player.SetTool(BuildingKind.Tower);
                game.Player.HoverOverride = FirstLegal(BuildingKind.Tower, 6, 6, 9, 9);
                yield return Shot($"011-{id}-2-placement");
                game.Player.HoverOverride = null;
                game.Player.CancelTool();
                game.StartNight();
                for (var i = 0; i < 60; i++) yield return null; // night light at real frames; the simulation is not advancing
                log.AppendLine($"{id} night: {StepUntil(() => game.Sim.AliveEnemyCount >= 3 && game.World.ActiveArrowCount > 0, 1600)} alive={game.Sim.AliveEnemyCount}");
                yield return Shot($"011-{id}-3-night");
                if (id == MapProfiles.Crossing)
                {
                    game.CameraRig.SetView(new Vector3(10f, 0f, 10.5f), 4.5f);
                    yield return Shot("011-slice-night-close");
                    game.CameraRig.ResetView();
                }

                game.Sim.SetPaused(false);
            }

            // The slice by Day, close: Keep, connected Walls and Archer Towers on Crossing.
            game.SetMapProfile(MapProfiles.Crossing);
            game.NewRun(7, false);
            foreach (var (kind, x, z) in Plan011Layout) game.Sim.TryPlace(kind, new GridPos(x, z), out _);
            game.CameraRig.SetView(new Vector3(10f, 0f, 10.5f), 4.5f);
            yield return Shot("011-slice-day-close");
            game.CameraRig.ResetView();

            // Dense fight: 40 mixed attackers from two sides against the same fortress plus a Cannon.
            var spawns = new System.Collections.Generic.List<SpawnEntry>();
            var mix = new[] { EnemyKind.Grunt, EnemyKind.Grunt, EnemyKind.Runner, EnemyKind.Grunt, EnemyKind.Archer, EnemyKind.Brute, EnemyKind.Bomber, EnemyKind.Grunt };
            for (var i = 0; i < 40; i++) spawns.Add(new SpawnEntry(i * 3, i % 2 == 0 ? MvpCampaign.West : MvpCampaign.East, i % 2, mix[i % mix.Length]));
            var dense = new WavePlan("dense", spawns, new[] { MvpCampaign.West, MvpCampaign.East });
            var calm = new WavePlan("calm", new SpawnEntry[0], new[] { MvpCampaign.West });
            var setup = new RunSimulation(new RunConfig
            {
                Definitions = game.NewRunDefinitions, Seed = 7, Terrain = MapProfiles.Generate(MapProfiles.Meadow, 7),
                Campaign = new CampaignConfig
                {
                    Label = ReleaseCampaign.Label, Nights = new[] { dense, calm }, OffersRewards = true, RulesVersion = ReleaseCampaign.RulesVersion,
                    CampaignId = ReleaseCampaign.CampaignId, PerkPool = PerkRules.ReleaseIds, MapProfile = MapProfiles.Meadow,
                },
            });
            foreach (var (kind, x, z) in Plan011Layout) setup.TryPlace(kind, new GridPos(x, z), out _);
            setup.TryPlace(BuildingKind.Cannon, new GridPos(13, 10), out _);
            ContinueSaved(setup, log);
            game.StartNight();
            for (var i = 0; i < 60; i++) yield return null;
            log.AppendLine($"dense: {StepUntil(() => game.Sim.AliveEnemyCount >= 24, 2400)} alive={game.Sim.AliveEnemyCount}");
            yield return Shot("011-dense-combat");
            game.CameraRig.SetView(new Vector3(8f, 0f, 10f), 5f);
            yield return Shot("011-dense-combat-close");
            game.CameraRig.ResetView();
            game.Sim.SetPaused(false);
            File.WriteAllText(Path.Combine(dir, $"{Screen.width}x{Screen.height}-011-capture-log.txt"), log.ToString());
        }

        /// <summary>
        /// -capture-set motion (release build): consecutive frames at a fixed 1/30 s through a busy night on a close camera,
        /// with a per-frame log of what happened, so motion (falls, strikes, collapses, shells) can be reviewed as strips.
        /// </summary>
        /// <param name="fx">-capture-set fx: Bombers reach a Wall unopposed (fuse, blast, scorch), a House burns at low HP,
        /// a Cannon fires — the particle effects rather than the gait.</param>
        IEnumerator RunMotionFrames(bool fx = false)
        {
            var log = new StringBuilder();
            game.QuitApplication = () => log.AppendLine("quit requested (suppressed during capture)");
            game.AutoAdvance = false;
            game.SkipOnboarding();
            var kinds = fx
                ? new[] { EnemyKind.Bomber, EnemyKind.Grunt, EnemyKind.Bomber, EnemyKind.Grunt, EnemyKind.Bomber }
                : new[] { EnemyKind.Grunt, EnemyKind.Runner, EnemyKind.Brute, EnemyKind.Bomber, EnemyKind.Archer, EnemyKind.Grunt, EnemyKind.SiegeCommander };
            var spawns = new System.Collections.Generic.List<SpawnEntry>();
            for (var i = 0; i < kinds.Length; i++) spawns.Add(new SpawnEntry(i * 16, MvpCampaign.West, 0, kinds[i]));
            var wave = new WavePlan("motion", spawns, new[] { MvpCampaign.West });
            var calm = new WavePlan("calm", new SpawnEntry[0], new[] { MvpCampaign.West });
            var setup = new RunSimulation(new RunConfig
            {
                Definitions = game.NewRunDefinitions, Seed = 3, Terrain = TerrainMap.Flat(),
                Campaign = new CampaignConfig
                {
                    Label = ReleaseCampaign.Label, Nights = new[] { wave, calm }, OffersRewards = true, RulesVersion = ReleaseCampaign.RulesVersion,
                    CampaignId = ReleaseCampaign.CampaignId, PerkPool = PerkRules.ReleaseIds, MapProfile = MapProfiles.Meadow,
                },
            });
            if (fx)
            {
                // Walls across the west lane (one gap the route takes), no Tower in reach: the Bombers light their fuses.
                foreach (var z in new[] { 8, 9, 11, 12 }) setup.TryPlace(BuildingKind.Wall, new GridPos(4, z), out _);
                setup.TryPlace(BuildingKind.Wall, new GridPos(6, 10), out _);
                setup.TryPlace(BuildingKind.House, new GridPos(7, 12), out var house);
                if (house != null) house.Hp = house.MaxHp / 5; // below a third: on fire
                setup.TryPlace(BuildingKind.Cannon, new GridPos(8, 7), out _);
            }
            else
            {
                setup.TryPlace(BuildingKind.Tower, new GridPos(5, 8), out _);
                setup.TryPlace(BuildingKind.Cannon, new GridPos(5, 11), out _);
                setup.TryPlace(BuildingKind.Wall, new GridPos(8, 10), out _); // a breach point near the camera
            }

            ContinueSaved(setup, log);
            game.CameraRig.SetView(fx ? new Vector3(6f, 0f, 10f) : new Vector3(6.5f, 0f, 10.5f), 3.2f);
            game.StartNight();
            for (var i = 0; i < 60; i++) yield return null; // night lighting at real frames; the simulation is not advancing
            game.ExternalViewClock = true; // from here every frame advances presentation exactly once, by 1/30 s
            var defeated = game.Sim.Stats.EnemiesDefeated;
            var lost = 0;
            for (var frame = 0; frame < (fx ? 660 : 330) && game.Sim.State == RunState.Night; frame++)
            {
                var keep = game.Sim.Keep.Hp;
                var buildings = game.World.BuildingViewCount;
                game.Sim.Advance(1.0 / 30.0);
                game.SyncViews(1f / 30f);
                yield return new WaitForEndOfFrame();
                CaptureOffscreen(Path.Combine(dir, $"{Screen.width}x{Screen.height}-motion-{frame:000}.png"));
                var notes = new System.Collections.Generic.List<string>();
                if (game.Sim.Stats.EnemiesDefeated > defeated) notes.Add($"kill x{game.Sim.Stats.EnemiesDefeated - defeated}");
                if (game.World.BuildingViewCount < buildings) notes.Add("building destroyed");
                if (game.Sim.Keep.Hp < keep) notes.Add($"keep -{keep - game.Sim.Keep.Hp}");
                if (game.World.ActiveShellCount > 0) notes.Add("shell");
                if (game.Sim.Enemies.Any(e => e.FuseLit)) notes.Add("fuse");
                if (game.World.DyingCount > 0) notes.Add($"dying {game.World.DyingCount}");
                defeated = game.Sim.Stats.EnemiesDefeated;
                log.AppendLine($"{frame:000} t={game.Sim.NightElapsedSeconds:0.00}s {string.Join(", ", notes)}");
            }

            File.WriteAllText(Path.Combine(dir, $"{Screen.width}x{Screen.height}-motion-log.txt"), log.ToString());
        }

        /// <summary>
        /// -capture-set hero-motion (release build, plan 019): consecutive hero-view frames at a fixed 1/30 s with the same
        /// scripted camera-relative input under any rules version, so before/after hero motion can be compared as clips. Day:
        /// the camera behind the hero over the Keep, start, stop, backpedal, strafes into Walls, a moving 180 degree turn, a
        /// diagonal slide and a building placed over the hero. Night: approach and attack a Grunt, a pause during the swing,
        /// strafe and backpedal while holding attack, a fall and the rise after respawn, and leaving and re-entering the
        /// view. A per-frame log records the authoritative hero state. Writes into the save folder like the other sets.
        /// </summary>
        IEnumerator RunHeroMotion()
        {
            var log = new StringBuilder();
            game.QuitApplication = () => log.AppendLine("quit requested (suppressed during capture)");
            game.AutoAdvance = false;
            game.SkipOnboarding();
            var west = MvpCampaign.West;
            var wave = new WavePlan("hero-motion", new[]
            {
                new SpawnEntry(0, west, 0, EnemyKind.Grunt), new SpawnEntry(40, west, 0, EnemyKind.Grunt), new SpawnEntry(80, west, 0, EnemyKind.Grunt),
            }, new[] { west });
            var calm = new WavePlan("calm", new SpawnEntry[0], new[] { west });
            var definitions = game.NewRunDefinitions;
            var startingGold = definitions.StartingGold;
            definitions.StartingGold = 1000; // read once by the constructor; restored right after
            var setup = new RunSimulation(new RunConfig
            {
                Definitions = definitions, Seed = 7, Terrain = MapProfiles.Generate(MapProfiles.Meadow, 7),
                Campaign = new CampaignConfig
                {
                    Label = ReleaseCampaign.Label, Nights = new[] { wave, calm }, OffersRewards = true, RulesVersion = ReleaseCampaign.RulesVersion,
                    CampaignId = ReleaseCampaign.CampaignId, PerkPool = PerkRules.ReleaseIds, MapProfile = MapProfiles.Meadow,
                },
            });
            definitions.StartingGold = startingGold;
            foreach (var (kind, x, z) in Plan011Layout) setup.TryPlace(kind, new GridPos(x, z), out _);
            ContinueSaved(setup, log);
            var sim = game.Sim;
            var hero = sim.Hero;
            var fine = hero.Profile.ContactPhase;
            log.AppendLine($"rules={sim.Campaign.RulesVersion} reach={hero.Profile.SwingReach} speed={hero.Profile.MaxSpeedUnits} radius={hero.Profile.RadiusUnits} {sim.Config.Terrain.Describe()}");
            game.SetHeroView(true);
            game.HeroRig.SkipTransition();
            game.HeroRig.SetOrbit(0f, 14f);
            game.ExternalViewClock = true;
            var frame = 0;
            const float dt = 1f / 30f;

            IEnumerator Frames(string label, int count, System.Func<int, (Vector2 move, bool attack, float? yaw)> input, System.Func<bool> stop = null)
            {
                for (var i = 0; i < count && (stop == null || !stop()); i++)
                {
                    var (move, attack, yaw) = input(i);
                    if (yaw.HasValue) game.HeroRig.SetOrbit(yaw.Value, game.HeroRig.Pitch);
                    var command = PlayerController.SteerCommand(move, game.HeroRig.Yaw, attack, fine);
                    game.Player.HeroInputOverride = command;
                    yield return null; // the player samples the override in Update
                    if (game.HeroViewActive)
                    {
                        if (sim.State == RunState.Day) sim.AdvanceDay(dt, command);
                        else sim.Advance(dt, command);
                    }
                    else if (sim.State == RunState.Night) sim.Advance(dt);

                    game.SyncViews(dt);
                    yield return new WaitForEndOfFrame();
                    CaptureOffscreen(Path.Combine(dir, $"{Screen.width}x{Screen.height}-hero-{frame:000}.png"));
                    var near = NearestEnemyTo(hero, out var d);
                    log.AppendLine($"{frame:000} {label} {sim.State} tick={sim.Tick} hero={hero.UnitsX},{hero.UnitsZ} v={hero.VelX},{hero.VelZ} hp={hero.Hp} alive={hero.Alive} " +
                                   $"cmd={command} paused={sim.Paused} shown={game.HeroView.ShownPosition.x:0.000},{game.HeroView.ShownPosition.z:0.000} " +
                                   $"cam={game.HeroRig.transform.position.x:0.00},{game.HeroRig.transform.position.y:0.00},{game.HeroRig.transform.position.z:0.00}" +
                                   (near != null ? $" enemy{near.Id}={d:0.000} hp{near.Hp}" : "") +
                                   (fine ? $" phase={game.HeroView.ShownAttackPhase:0.000} pending={hero.AttackPending}" +
                                           (game.HeroView.AnimationRig != null ? $" gait={game.HeroView.AnimationRig.Gait:0.00} foot={game.HeroView.AnimationRig.FootfallCount}" : "") : ""));
                    frame++;
                }
            }

            (Vector2, bool, float?) Hold(Vector2 move, bool attack = false, float? yaw = null) => (move, attack, yaw);

            // Day.
            yield return Frames("obstruct-keep", 30, i => Hold(Vector2.zero, false, i * 1.5f)); // camera over the Keep behind the hero
            yield return Frames("start", 24, i => Hold(Vector2.up, false, 0f));
            yield return Frames("stop", 12, i => Hold(Vector2.zero));
            yield return Frames("backpedal", 18, i => Hold(Vector2.down));
            yield return Frames("strafe-right", 18, i => Hold(Vector2.right));
            yield return Frames("strafe-left", 24, i => Hold(Vector2.left));
            yield return Frames("turn-180", 24, i => Hold(Vector2.up, false, Mathf.Min(180f, i * 15f)));
            yield return Frames("diagonal-slide", 24, i => Hold(new Vector2(1f, 1f)));
            sim.TryPlace(BuildingKind.Wall, hero.Cell, out var placed);
            log.AppendLine($"wall over the hero at {hero.Cell}: {(placed != null ? "placed" : "rejected")}");
            yield return Frames("relocated", 12, i => Hold(Vector2.zero));

            // To the West lane, unrecorded: just off the (6,10) centre, where the first Grunt engages.
            foreach (var (tx, tz) in new[] { (10.5, 13.5), (6.5, 13.5), (6.5, 10.62) })
                for (var i = 0; i < 1200; i++)
                {
                    double dx = tx - hero.X, dz = tz - hero.Z;
                    if (dx * dx + dz * dz < 0.03 * 0.03) break;
                    var h = HeroRules.FacingToward(dx, dz);
                    sim.AdvanceDay(0.05, fine ? HeroCommand.Fine(dx * dx + dz * dz < 0.06 * 0.06 ? -1 : h, false, h) : new HeroCommand((h + 2) / 4 % 16, false, h));
                }

            log.AppendLine($"lane: hero {hero.X:0.000},{hero.Z:0.000}");
            game.StartNight();
            for (var i = 0; i < 3000 && sim.State == RunState.Night; i++)
            {
                if (NearestEnemyTo(hero, out var d) != null && d < 1.3) break;
                sim.Step(fine ? HeroCommand.Fine(-1, false, 48) : new HeroCommand(-1, false, 48));
            }

            game.SyncViews(0f);
            float Aim()
            {
                var e = NearestEnemyTo(hero, out _);
                return e == null ? game.HeroRig.Yaw : Mathf.Atan2((float)(e.X - hero.X), (float)(e.Z - hero.Z)) * Mathf.Rad2Deg;
            }

            bool InReach(double scale)
            {
                var e = NearestEnemyTo(hero, out var d);
                return e != null && d <= hero.Profile.SwingReach * scale;
            }

            yield return Frames("engage", 75, i => Hold(InReach(0.75) ? Vector2.zero : Vector2.up, InReach(1.0), Aim()));
            yield return Frames("swing", 2, i => Hold(Vector2.zero, true, Aim()));
            sim.SetPaused(true);
            yield return Frames("paused", 12, i => Hold(Vector2.zero, true));
            sim.SetPaused(false);
            yield return Frames("resume", 15, i => Hold(Vector2.zero, true, Aim()));
            yield return Frames("strafe-attack", 30, i => Hold(Vector2.right, true, Aim()));
            yield return Frames("backpedal-attack", 20, i => Hold(Vector2.down, true, Aim()));
            yield return Frames("return", 30, i => Hold(InReach(0.75) ? Vector2.zero : Vector2.up, InReach(1.0), Aim()));
            // Unrecorded: back onto the lane just off the (7,10) centre, ahead of the remaining Grunts, which engage there.
            for (var i = 0; i < 600 && hero.Alive && sim.State == RunState.Night; i++)
            {
                double dx = 7.5 - hero.X, dz = 10.62 - hero.Z;
                if (dx * dx + dz * dz < 0.03 * 0.03) break;
                var h = HeroRules.FacingToward(dx, dz);
                sim.Step(fine ? HeroCommand.Fine(dx * dx + dz * dz < 0.06 * 0.06 ? -1 : h, false, h) : new HeroCommand((h + 2) / 4 % 16, false, h));
            }

            game.SyncViews(0f);
            hero.Hp = 1; // capture fixture only: the next enemy hit fells the hero
            yield return Frames("fall", 150, i => Hold(Vector2.zero, false, Aim()), () => !hero.Alive);
            yield return Frames("down", 30, i => Hold(Vector2.zero));
            for (var i = 0; i < 260 && !hero.Alive && sim.State == RunState.Night; i++) sim.Step();
            yield return Frames("rise", 30, i => Hold(Vector2.zero));
            game.SetHeroView(false);
            yield return Frames("strategic", 8, i => Hold(Vector2.zero));
            game.SetHeroView(true); // the entry transition, not skipped
            yield return Frames("enter", 15, i => Hold(Vector2.zero));
            game.Player.HeroInputOverride = null;
            game.ExternalViewClock = false;
            File.WriteAllText(Path.Combine(dir, $"{Screen.width}x{Screen.height}-hero-log.txt"), log.ToString());
        }

        /// <summary>
        /// -capture-set hero-camera (release build, plan 019): the hero camera against obstacles, 1/30 s frames: orbiting
        /// behind the hero over the Keep at the field-of-view and distance extremes, pressed into a Wall corner, beside a
        /// forest cell, and the eased entry from the strategic view there. Logs the shown distance, lift and fallbacks.
        /// </summary>
        IEnumerator RunHeroCamera()
        {
            var log = new StringBuilder();
            game.QuitApplication = () => log.AppendLine("quit requested (suppressed during capture)");
            game.AutoAdvance = false;
            game.SkipOnboarding();
            game.SetMapProfile(MapProfiles.Meadow);
            game.NewRun(7, false);
            var sim = game.Sim;
            var hero = sim.Hero;
            var fine = hero.Profile.ContactPhase;
            var startingGold = sim.Gold;
            foreach (var (kind, x, z) in Plan011Layout) sim.TryPlace(kind, new GridPos(x, z), out _);
            log.AppendLine($"rules={sim.Campaign.RulesVersion} gold {startingGold} -> {sim.Gold} {sim.Config.Terrain.Describe()}");
            game.SetHeroView(true);
            game.HeroRig.SkipTransition();
            game.ExternalViewClock = true;
            var frame = 0;
            const float dt = 1f / 30f;

            IEnumerator Frames(string label, int count, System.Func<int, float> yaw)
            {
                for (var i = 0; i < count; i++)
                {
                    game.HeroRig.SetOrbit(yaw(i), 16f);
                    game.Player.HeroInputOverride = PlayerController.SteerCommand(Vector2.zero, game.HeroRig.Yaw, false, fine);
                    yield return null;
                    game.SyncViews(dt);
                    yield return new WaitForEndOfFrame();
                    CaptureOffscreen(Path.Combine(dir, $"{Screen.width}x{Screen.height}-camera-{frame:000}.png"));
                    var rig = game.HeroRig;
                    log.AppendLine($"{frame:000} {label} fov={rig.FieldOfView:0} zoom={rig.Distance:0.00} shown={rig.ShownDistance:0.000} lift={rig.ShownLift:0.0} " +
                                   $"clear={rig.ShownPoseClear()} fallback={rig.FallbackFrames} cuts={rig.TransitionCuts} transition={rig.InTransition}");
                    frame++;
                }
            }

            void WalkTo(double tx, double tz)
            {
                for (var i = 0; i < 1200; i++)
                {
                    double dx = tx - hero.X, dz = tz - hero.Z;
                    if (dx * dx + dz * dz < 0.03 * 0.03) break;
                    var h = HeroRules.FacingToward(dx, dz);
                    sim.AdvanceDay(0.05, fine ? HeroCommand.Fine(dx * dx + dz * dz < 0.06 * 0.06 ? -1 : h, false, h) : new HeroCommand((h + 2) / 4 % 16, false, h));
                }

                game.SyncViews(0f);
            }

            foreach (var (fov, zoom) in new[] { (50f, 2.2f), (90f, 0.5f), (90f, 2.2f) })
            {
                game.HeroRig.FieldOfView = fov;
                game.HeroRig.SetDefaultDistance(zoom);
                yield return Frames($"keep-fov{fov:0}-zoom{zoom:0.0}", 48, i => -60f + i * 2.5f); // sweeps across the Keep behind
            }

            game.HeroRig.FieldOfView = 60f;
            game.HeroRig.SetDefaultDistance(1f);
            // Pressed into the outside corner of the Wall at (9,12): from the spawn cell north-west.
            for (var i = 0; i < 40; i++) sim.AdvanceDay(0.05, fine ? HeroCommand.Fine(56, false, 56) : new HeroCommand(14, false, 56));
            game.SyncViews(0f);
            yield return Frames("wall-corner", 60, i => 90f + i * 3f);

            // Beside the forest cell nearest the Keep, the trees behind the camera.
            GridPos? forest = null;
            var best = int.MaxValue;
            for (var i = 0; i < sim.Config.Terrain.Cells.Length; i++)
            {
                if (sim.Config.Terrain.Cells[i] != TerrainType.Forest) continue;
                var c = new GridPos(i % GridCoordinates.Size, i / GridCoordinates.Size);
                var d = c.ManhattanDistance(GridState.KeepCell);
                if (d < best) (best, forest) = (d, c);
            }

            if (forest.HasValue)
            {
                var f = forest.Value;
                var toKeep = new Vector2(GridState.KeepCell.X - f.X, GridState.KeepCell.Z - f.Z);
                var side = Mathf.Abs(toKeep.x) >= Mathf.Abs(toKeep.y) ? new Vector2(Mathf.Sign(toKeep.x), 0f) : new Vector2(0f, Mathf.Sign(toKeep.y));
                WalkTo(f.X + 0.5 + side.x * 0.6, f.Z + 0.5 + side.y * 0.6);
                var away = Mathf.Atan2(side.x, side.y) * Mathf.Rad2Deg; // facing away from the trees: they are behind the camera
                log.AppendLine($"forest {f}: hero {hero.X:0.00},{hero.Z:0.00}");
                yield return Frames("forest", 60, i => away - 45f + i * 1.5f);
                game.SetHeroView(false);
                game.SyncViews(0f);
                game.SetHeroView(true); // the eased entry next to the trees
                yield return Frames("enter", 15, i => game.HeroRig.Yaw);
            }

            game.Player.HeroInputOverride = null;
            game.ExternalViewClock = false;
            File.WriteAllText(Path.Combine(dir, $"{Screen.width}x{Screen.height}-camera-log.txt"), log.ToString());
        }

        /// <summary>
        /// -capture-set fire (release build, 2026-10-21): burning enemies (Flaming Arrows) close up at Night, through the hero
        /// camera and the strategic camera, a few frames apart so flames read as motion. Capture fixture only: the enemies are
        /// set burning for the whole capture without taking burn damage.
        /// </summary>
        IEnumerator RunFire()
        {
            game.QuitApplication = () => { };
            game.AutoAdvance = false;
            game.SkipOnboarding();
            var west = MvpCampaign.West;
            var wave = new WavePlan("fire", new[]
            {
                new SpawnEntry(0, west, 0, EnemyKind.Grunt), new SpawnEntry(10, west, 0, EnemyKind.Brute), new SpawnEntry(20, west, 0, EnemyKind.Grunt),
            }, new[] { west });
            var setup = new RunSimulation(new RunConfig
            {
                Definitions = game.NewRunDefinitions, Seed = 7, Terrain = TerrainMap.Flat(),
                Campaign = new CampaignConfig
                {
                    Label = ReleaseCampaign.Label, Nights = new[] { wave, new WavePlan("calm", new SpawnEntry[0], new[] { west }) }, OffersRewards = true,
                    RulesVersion = ReleaseCampaign.RulesVersion, CampaignId = ReleaseCampaign.CampaignId, PerkPool = PerkRules.ReleaseIds, MapProfile = MapProfiles.Meadow,
                },
            });
            ContinueSaved(setup, new StringBuilder());
            var sim = game.Sim;
            game.StartNight();
            for (var i = 0; i < 260 && sim.State == RunState.Night; i++) sim.Step(); // the three walk in to about x = 4
            foreach (var e in sim.Enemies)
            {
                e.Burning = true;
                e.BurnNextTick = e.BurnExpiryTick = long.MaxValue;
            }

            game.ExternalViewClock = true;
            IEnumerator Frames(string name, int count)
            {
                for (var i = 0; i < count; i++)
                {
                    sim.Advance(1.0 / 30.0);
                    foreach (var e in sim.Enemies) e.BurnNextTick = e.BurnExpiryTick = long.MaxValue;
                    game.SyncViews(1f / 30f);
                    yield return new WaitForEndOfFrame();
                    if (i % 6 == 5) CaptureOffscreen(Path.Combine(dir, $"{Screen.width}x{Screen.height}-fire-{name}-{i / 6}.png"));
                }
            }

            var lead = sim.Enemies.Count > 0 ? sim.Enemies[0] : null;
            var focus = lead != null ? new Vector3((float)lead.X, 0f, (float)lead.Z) : new Vector3(4f, 0f, 10.5f);
            game.CameraRig.SetView(focus, 2.5f);
            yield return Frames("strategic", 24);
            if (lead != null && sim.Hero != null)
            {
                // Capture fixture only: the hero stands just east of the burning group, looking at it.
                sim.Hero.UnitsX = sim.Hero.PrevUnitsX = (int)System.Math.Round((lead.X + 0.7) * HeroRules.UnitsPerTile);
                sim.Hero.UnitsZ = sim.Hero.PrevUnitsZ = (int)System.Math.Round(lead.Z * HeroRules.UnitsPerTile);
            }

            game.SetHeroView(true);
            game.HeroRig.SkipTransition();
            game.HeroRig.SetOrbit(270f, 8f);
            game.HeroRig.SetDefaultDistance(0.6f);
            game.Player.HeroInputOverride = HeroCommand.Fine(-1, false, 48);
            yield return Frames("hero", 24);
            game.Player.HeroInputOverride = null;
            game.ExternalViewClock = false;
        }

        IEnumerator RunPlan007()
        {
            var log = new StringBuilder();
            game.QuitApplication = () => log.AppendLine("quit requested (suppressed during capture)");
            game.AutoAdvance = false;

            game.QuitToMenuWithoutSaving();
            yield return Shot("007-01-title");
            log.AppendLine($"title: gameplay layer visible={game.Hud.GameplayLayer.activeSelf} music={game.Audio.MusicClip?.name}");
            game.Hud.CreditsButton.onClick.Invoke();
            yield return Shot("007-02-credits");
            game.Hud.CreditsPanel.SetActive(false);
            game.Hud.SettingsButton.onClick.Invoke();
            yield return Shot("007-03-settings-audio");
            game.Hud.SettingsPanel.SetActive(false);

            // A first run on an ordinary generated map: guide step 1, then the guide after a Tower, then Help.
            game.ReopenOnboarding();
            game.NewRun(3, flat: false);
            yield return Shot("007-04-generated-day1-guide");
            log.AppendLine($"generated day 1: {game.Sim.Config.Terrain.Describe()} guide=[{game.Hud.GuideText}]");
            var placed = false;
            for (var x = 5; x < 9 && !placed; x++)
            for (var z = 8; z < 12 && !placed; z++)
                placed = game.Sim.TryPlace(BuildingKind.Tower, new GridPos(x, z), out _) == PlacementRejection.None;
            game.Player.SetTool(BuildingKind.Wall);
            game.Player.HoverOverride = new GridPos(1, 1); // invalid edge placement next to the guide
            yield return Shot("007-05-guide-step2-invalid-edge");
            log.AppendLine($"after tower: guide=[{game.Hud.GuideText}] tooltip=[{game.Hud.TooltipText}]");
            game.Player.SetTool(BuildingKind.House);
            GridPos? houseCell = null;
            for (var x = 4; x < 16 && houseCell == null; x++)
            for (var z = 4; z < 16 && houseCell == null; z++)
                if (game.Sim.CheckPlacement(BuildingKind.House, new GridPos(x, z)) == PlacementRejection.None) houseCell = new GridPos(x, z);
            game.Player.HoverOverride = houseCell;
            yield return Shot("007-05b-house-payback-tooltip");
            log.AppendLine($"house tooltip: [{game.Hud.TooltipText}]");
            game.Player.HoverOverride = null;
            game.Player.CancelTool();
            game.Hud.HelpButton.onClick.Invoke();
            yield return Shot("007-06-help");
            game.Hud.HelpPanel.SetActive(false);

            // Escalation on the recorded economy script (flat fixture): Runner on Day 2, Brute on Day 3, final siege on Day 6.
            game.NewRun(1, flat: true);
            var audio = game.Audio;
            for (var day = 1; day <= 6 && !game.Sim.IsTerminal; day++)
            {
                PlayDay(day);
                if (day == 2 || day == 3 || day == 6)
                {
                    yield return Shot($"007-07-day{day}-announcement");
                    log.AppendLine($"day {day} announcement: [{game.Hud.AnnouncementText}]");
                }

                if (day == 3)
                {
                    var house = game.Sim.Grid.GetOccupant(new GridPos(6, 13));
                    if (house != null)
                    {
                        var hp = house.Hp;
                        house.Hp = Mathf.Max(1, house.MaxHp / 4);
                        game.CameraRig.SetView(new Vector3(6f, 0f, 12f), 4.5f);
                        game.SyncViews(0.4f);
                        yield return Shot("007-08-damaged-house-smoke");
                        log.AppendLine($"damaged house: smoke={game.World.IsShowingDamage(house.Id)}");
                        house.Hp = hp;
                        game.CameraRig.ResetView();
                    }
                }

                game.StartNight();
                if (day == 4 || day == 6) yield return RealTimeNight(day, log);
                while (game.Sim.State == RunState.Night) game.FastForward(1.0);
                log.AppendLine($"night {day}: {game.Sim.State} keep={game.Sim.Keep.Hp}");
                if (game.Sim.State != RunState.Reward) break;
                ChooseFirstPerk();
            }

            yield return Shot("007-12-outcome-" + game.Sim.State);
            log.AppendLine($"outcome: {game.Sim.State} music={audio.MusicClip?.name} victory={audio.PlayCount(SoundId.Victory)} defeat={audio.PlayCount(SoundId.Defeat)}");

            // Five Menu -> New Run -> end cycles (an undefended Keep falls on Night 1): nothing may accumulate.
            for (var cycle = 1; cycle <= 5; cycle++)
            {
                game.QuitToMenuWithoutSaving();
                yield return null;
                game.NewRun((uint)(100 + cycle), flat: false);
                game.StartNight();
                game.AutoAdvance = true;
                var until = Time.unscaledTime + 8f;
                while (game.Sim.State == RunState.Night && Time.unscaledTime < until) yield return null; // a few real-time seconds of combat
                game.AutoAdvance = false;
                while (game.Sim.State == RunState.Night) game.FastForward(1.0);
                var settle = Time.unscaledTime + 3f;
                while (Time.unscaledTime < settle) yield return null;
                log.AppendLine($"cycle {cycle}: {game.Sim.State} voices={audio.ActiveVoices} pending={audio.PendingCount} musicTracks={audio.MusicTracks} " +
                               $"arrows={game.World.ActiveArrowCount} cues={game.World.PendingCueCount} enemyViews={game.World.EnemyViewCount} " +
                               $"audioSources={audio.GetComponentsInChildren<AudioSource>().Length} objects={Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length}");
            }

            File.WriteAllText(Path.Combine(dir, $"{Screen.width}x{Screen.height}-007-capture-log.txt"), log.ToString());
        }

        /// <summary>
        /// Night at normal speed: the simulation advances by real frame time (AutoAdvance), shots every few seconds, and the
        /// audio director's voice counts are sampled every frame to show the limits hold in the densest fights.
        /// </summary>
        IEnumerator RealTimeNight(int night, StringBuilder log)
        {
            var audio = game.Audio;
            var start = new int[System.Enum.GetValues(typeof(SoundId)).Length];
            for (var i = 0; i < start.Length; i++) start[i] = audio.PlayCount((SoundId)i);
            int maxVoices = 0, maxImpacts = 0, maxShots = 0, frames = 0;
            var maxTracks = 0;
            var nextShot = 4f;
            var shot = 0;
            var began = Time.unscaledTime;
            game.AutoAdvance = true;
            var golemShot = false;
            while (game.Sim.State == RunState.Night && Time.unscaledTime - began < 90f)
            {
                frames++;
                maxVoices = Mathf.Max(maxVoices, audio.ActiveVoices);
                maxImpacts = Mathf.Max(maxImpacts, audio.ActiveVoicesOf(SoundId.Impact));
                maxShots = Mathf.Max(maxShots, audio.ActiveVoicesOf(SoundId.Shot));
                maxTracks = Mathf.Max(maxTracks, audio.MusicTracks);
                if (!golemShot && game.Hud.GolemBannerText != null)
                {
                    golemShot = true;
                    yield return Shot($"007-10-night{night}-golem-banner");
                    log.AppendLine($"golem banner: [{game.Hud.GolemBannerText}] at {game.Sim.NightElapsedSeconds:0.0}s");
                    continue;
                }

                if (Time.unscaledTime - began >= nextShot && shot < 4)
                {
                    nextShot += 8f;
                    yield return Shot($"007-{(night == 4 ? "09" : "11")}-night{night}-1x-{shot++}");
                    continue;
                }

                yield return null;
            }

            game.AutoAdvance = false;
            var played = new StringBuilder();
            for (var i = 0; i < start.Length; i++)
            {
                var n = audio.PlayCount((SoundId)i) - start[i];
                if (n > 0) played.Append($"{(SoundId)i}={n} ");
            }

            log.AppendLine($"night {night} at 1x: {Time.unscaledTime - began:0.0}s wall clock, {frames} frames, sim {game.Sim.NightElapsedSeconds:0.0}s, " +
                           $"max voices {maxVoices}/{AudioDirector.MaxVoices}, max impacts {maxImpacts}, max shots {maxShots}/{AudioDirector.MaxPerFamily}, " +
                           $"max music tracks {maxTracks}, lighting blend {game.World.NightBlend:0.00}; played {played}");
        }

    }
}
