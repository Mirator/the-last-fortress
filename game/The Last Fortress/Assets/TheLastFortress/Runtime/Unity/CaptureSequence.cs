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
            var cam = game.CameraRig.Camera;
            var canvas = game.Hud.Canvas;
            int w = Screen.width, h = Screen.height;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var mode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.5f;
            Canvas.ForceUpdateCanvases();
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
            CampaignConfig Custom(params WavePlan[] nights) => new CampaignConfig
            {
                Label = ReleaseCampaign.Label, Nights = nights, OffersRewards = true, RulesVersion = ReleaseCampaign.RulesVersion,
                CampaignId = ReleaseCampaign.CampaignId, PerkPool = PerkRules.ReleaseIds, MapProfile = MapProfiles.Meadow,
            };
            var setup = new RunSimulation(new RunConfig { Definitions = game.NewRunDefinitions, Seed = 7, Terrain = MapProfiles.Generate(MapProfiles.Meadow, 7), Campaign = Custom(wave, calm) });
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
