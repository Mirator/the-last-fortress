using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastFortress
{
    /// <summary>MVP-only HUD: main menu, in-run menu, settings, run summary, seed display and save-failure handling.</summary>
    public sealed partial class HudView
    {
        GameObject menuOverlay, settingsPanel, saveFailedPanel;
        Text menuStatus, menuContinueLabel, seedText, saveFailedText, settingsSensitivity, settingsShake, settingsWindow, settingsResolution, settingsStatus;
        InputField seedInput;
        GameSettings editing;
        Resolution[] resolutions;
        int resolutionIndex = -1;
        string seedError;
        Text keepHitText;
        Image keepBarBack;
        float keepHitTimer;
        int keepHitAmount;

        /// <summary>Damage shown by the current Keep-hit cue (0 when none is showing).</summary>
        public int KeepHitShown => keepHitTimer > 0f ? keepHitAmount : 0;
        readonly GameObject[] entryMarkers = new GameObject[4];
        readonly Text[] entryMarkerTexts = new Text[4];

        /// <summary>Visible entry-marker texts (tests/diagnostics).</summary>
        public string EntryMarkerText(int i) => entryMarkers[i] != null && entryMarkers[i].activeSelf ? entryMarkerTexts[i].text : null;

        public GameObject MenuOverlay => menuOverlay;
        public GameObject SettingsPanel => settingsPanel;
        public GameObject SaveFailedPanel => saveFailedPanel;
        public Button NewRunButton { get; private set; }
        public Button ContinueButton { get; private set; }
        public Button RecoverButton { get; private set; }
        public Button ResumeButton { get; private set; }
        public Button SaveAndQuitButton { get; private set; }
        public Button SettingsButton { get; private set; }
        public Button RunMenuButton { get; private set; }
        public Button RotateButton { get; private set; }
        public Button NewSeedButton { get; private set; }
        public Button RetrySaveButton { get; private set; }
        public Button QuitButton { get; private set; }
        public GameObject ConfirmPanel => confirmPanel;
        public string ConfirmTitle => confirmTitle.text;
        public string ConfirmBody => confirmBody.text;

        /// <summary>Visible guard-dialog choices, in order (label, button).</summary>
        public IEnumerable<(string label, Button button)> ConfirmOptions()
        {
            for (var i = 0; i < confirmButtons.Length; i++)
                if (confirmButtons[i].gameObject.activeSelf) yield return (confirmLabels[i].text, confirmButtons[i]);
        }

        GameObject confirmPanel;
        Text confirmTitle, confirmBody;
        readonly Button[] confirmButtons = new Button[3];
        readonly Text[] confirmLabels = new Text[3];
        PrototypeBootstrap.ConfirmRequest shownConfirm;
        public InputField SeedInput => seedInput;
        public string MenuStatusText => menuStatus.text;
        public string SeedText => seedText != null ? seedText.text : null;
        public string TerminalBody => terminalBody.text;

        void BuildRunMenuRow(RectTransform menu)
        {
            RunMenuButton = MakeButton(menu, "Menu", new Vector2(0, 1), new Vector2(318, -12), new Vector2(140, 52), game.OpenMenu, out var menuLabel);
            menuLabel.fontSize = 21;
            seedText = Label(menu, "Seed", 17, TextAnchor.MiddleLeft, new Vector2(14, -68), new Vector2(330, 34), Palette.HudText);
            MakeButton(menu, "Copy seed", new Vector2(0, 1), new Vector2(348, -68), new Vector2(110, 34), CopySeed, out var copyLabel);
            copyLabel.fontSize = 18;
        }

        void CopySeed() => GUIUtility.systemCopyBuffer = game.Sim.Seed.ToString(CultureInfo.InvariantCulture);

        void BuildMvpPanels(RectTransform root)
        {
            // Terminal overlay: summary needs more room plus same-seed / new-seed / menu actions.
            var card = (RectTransform)terminalOverlay.transform.Find("Card");
            card.sizeDelta = new Vector2(760, 520);
            terminalBody.rectTransform.sizeDelta = new Vector2(720, 290);
            ((Text)TerminalRestartButton.GetComponentInChildren(typeof(Text))).text = "Retry same seed";
            var restartRect = (RectTransform)TerminalRestartButton.transform;
            restartRect.anchoredPosition = new Vector2(-250, 30);
            NewSeedButton = MakeButton(card, "New seed", new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(220, 70), () => game.NewRun(PrototypeBootstrap.NewSeed(), game.FlatMap), out _);
            MakeButton(card, "Main menu", new Vector2(0.5f, 0), new Vector2(250, 30), new Vector2(220, 70), game.QuitToMenuWithoutSaving, out _);

            // Keep-hit cue beside the Keep health bar: readable when the Keep is off-screen or camera shake is off.
            keepBarBack = keepFill.parent.GetComponent<Image>();
            keepHitText = Label(keepText.transform.parent, "KeepHit", 24, TextAnchor.UpperRight, new Vector2(250, -58), new Vector2(110, 36), Palette.WarningCoral);
            keepHitText.fontStyle = FontStyle.Bold;
            keepHitText.text = "";

            // Entry markers: where tonight's enemies come from, pinned to the screen edge when that side is off-screen.
            var markerRoot = FullScreenRect(root, "EntryMarkers");
            markerRoot.SetAsFirstSibling(); // behind every panel and overlay
            for (var i = 0; i < entryMarkers.Length; i++)
            {
                var m = Panel(markerRoot, $"Entry{i}", new Vector2(0, 0), Vector2.zero, new Vector2(150, 44));
                m.pivot = new Vector2(0.5f, 0.5f);
                var img = m.GetComponent<Image>();
                img.color = Palette.WarningCoral.WithAlpha(0.92f);
                img.raycastTarget = false;
                entryMarkerTexts[i] = Label(m, "Text", 20, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(150, 44), Palette.HudSurface, true);
                entryMarkerTexts[i].rectTransform.anchorMin = entryMarkerTexts[i].rectTransform.anchorMax = entryMarkerTexts[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                entryMarkerTexts[i].fontStyle = FontStyle.Bold;
                entryMarkers[i] = m.gameObject;
                m.gameObject.SetActive(false);
            }

            // Guard dialog (Restart/Quit): blocks the world and every panel underneath until a choice is made.
            confirmPanel = FullScreen(root, "Confirm", 0.65f);
            var cbox = Panel(confirmPanel.transform, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 330));
            cbox.pivot = new Vector2(0.5f, 0.5f);
            confirmTitle = Label(cbox, "Title", 34, TextAnchor.UpperCenter, new Vector2(0, -22), new Vector2(860, 46), Palette.Amber, true);
            confirmBody = Label(cbox, "Body", 22, TextAnchor.UpperCenter, new Vector2(0, -84), new Vector2(840, 130), Palette.HudText, true);
            for (var i = 0; i < confirmButtons.Length; i++)
            {
                var index = i;
                confirmButtons[i] = MakeButton(cbox, $"Choice{i}", new Vector2(0.5f, 0), new Vector2((i - 1) * 285, 28), new Vector2(270, 64), () => ChooseConfirm(index), out confirmLabels[i]);
                confirmLabels[i].fontSize = 21;
            }

            confirmPanel.SetActive(false);

            // Save failure: blocks play until the same snapshot is written, or the player leaves without saving.
            saveFailedPanel = FullScreen(root, "SaveFailed", 0.6f);
            var box = Panel(saveFailedPanel.transform, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 330));
            box.pivot = new Vector2(0.5f, 0.5f);
            Label(box, "Title", 40, TextAnchor.UpperCenter, new Vector2(0, -20), new Vector2(860, 50), Palette.WarningCoral, true).text = "PROGRESS NOT SAVED";
            saveFailedText = Label(box, "Text", 21, TextAnchor.UpperCenter, new Vector2(0, -80), new Vector2(860, 150), Palette.HudText, true);
            RetrySaveButton = MakeButton(box, "Retry save", new Vector2(0.5f, 0), new Vector2(-170, 28), new Vector2(300, 64), () => game.RetrySave(), out _);
            MakeButton(box, "Quit without saving", new Vector2(0.5f, 0), new Vector2(170, 28), new Vector2(300, 64), game.QuitToMenuWithoutSaving, out _);
            saveFailedPanel.SetActive(false);

            // Main / in-run menu
            menuOverlay = FullScreen(root, "MainMenu", 0.82f);
            var panel = Panel(menuOverlay.transform, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 760));
            panel.pivot = new Vector2(0.5f, 0.5f);
            var title = Label(panel, "Title", 64, TextAnchor.UpperCenter, new Vector2(0, -26), new Vector2(780, 80), Palette.Amber, true);
            title.text = "THE LAST FORTRESS";
            title.fontStyle = FontStyle.Bold;
            Label(panel, "Subtitle", 22, TextAnchor.UpperCenter, new Vector2(0, -108), new Vector2(780, 30), Palette.HudText, true).text =
                "Six nights · build by day, hold the Keep by night";

            Label(panel, "SeedLabel", 22, TextAnchor.MiddleLeft, new Vector2(70, -170), new Vector2(240, 50), Palette.HudText).text = "Seed (optional)";
            seedInput = MakeInput(panel, new Vector2(300, -170), new Vector2(450, 50), "random");
            NewRunButton = MakeButton(panel, "New Run", new Vector2(0.5f, 1), new Vector2(0, -240), new Vector2(560, 64), StartNewRunFromMenu, out var newLabel);
            newLabel.fontSize = 28;
            ContinueButton = MakeButton(panel, "Continue", new Vector2(0.5f, 1), new Vector2(0, -316), new Vector2(560, 64), game.Continue, out menuContinueLabel);
            menuContinueLabel.fontSize = 22;
            RecoverButton = MakeButton(panel, "Recover backup", new Vector2(0.5f, 1), new Vector2(0, -392), new Vector2(560, 56), () => game.RecoverBackup(), out _);
            ResumeButton = MakeButton(panel, "Resume", new Vector2(0.5f, 1), new Vector2(-145, -460), new Vector2(270, 56), game.CloseMenu, out _);
            SaveAndQuitButton = MakeButton(panel, "Save and Quit", new Vector2(0.5f, 1), new Vector2(145, -460), new Vector2(270, 56), () => game.SaveAndQuit(), out _);
            SettingsButton = MakeButton(panel, "Settings", new Vector2(0.5f, 1), new Vector2(-145, -528), new Vector2(270, 56), OpenSettings, out _);
            QuitButton = MakeButton(panel, "Quit game", new Vector2(0.5f, 1), new Vector2(145, -528), new Vector2(270, 56), game.RequestQuit, out _);
            menuStatus = Label(panel, "Status", 20, TextAnchor.UpperCenter, new Vector2(0, -594), new Vector2(760, 90), Palette.HudText, true);
            Label(panel, "Controls", 18, TextAnchor.UpperCenter, new Vector2(0, -688), new Vector2(780, 60), Palette.HudText.WithAlpha(0.7f), true).text =
                "Controls: 1–5 choose building · left click place / select · right click or Esc cancel · R rotate\n" +
                "WASD / arrows pan · mouse wheel zoom · Space pause at night";
            menuOverlay.SetActive(false);

            // Settings (on top of the menu)
            settingsPanel = FullScreen(root, "Settings", 0.4f);
            var sp = Panel(settingsPanel.transform, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 600));
            sp.pivot = new Vector2(0.5f, 0.5f);
            Label(sp, "Title", 44, TextAnchor.UpperCenter, new Vector2(0, -24), new Vector2(780, 56), Palette.Amber, true).text = "SETTINGS";
            settingsSensitivity = SettingRow(sp, -110, "Camera sensitivity", () => StepSensitivity(-0.25f), () => StepSensitivity(0.25f));
            settingsShake = SettingRow(sp, -180, "Camera shake on Keep hits", ToggleShake, ToggleShake);
            settingsWindow = SettingRow(sp, -250, "Window mode", ToggleWindow, ToggleWindow);
            settingsResolution = SettingRow(sp, -320, "Resolution", () => StepResolution(-1), () => StepResolution(1));
            var audio = Label(sp, "Audio", 21, TextAnchor.MiddleLeft, new Vector2(40, -390), new Vector2(740, 40), Palette.HudText.WithAlpha(0.55f));
            audio.text = "Master / music / effects volume: unavailable — this build has no audio.";
            MakeButton(sp, "Save settings", new Vector2(0.5f, 0), new Vector2(-160, 30), new Vector2(280, 60), SaveSettings, out _);
            MakeButton(sp, "Back", new Vector2(0.5f, 0), new Vector2(160, 30), new Vector2(280, 60), () => settingsPanel.SetActive(false), out _);
            settingsStatus = Label(sp, "Status", 19, TextAnchor.UpperCenter, new Vector2(0, -440), new Vector2(760, 50), Palette.WarningCoral, true);
            settingsPanel.SetActive(false);
        }

        RectTransform FullScreenRect(Transform parent, string name)
        {
            var rt = Rect(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        GameObject FullScreen(Transform parent, string name, float alpha)
        {
            var rt = FullScreenRect(parent, name);
            rt.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.09f, alpha);
            return rt.gameObject;
        }

        InputField MakeInput(Transform parent, Vector2 position, Vector2 size, string placeholder)
        {
            var rt = Rect(parent, "SeedInput", new Vector2(0, 1), position, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.08f, 0.1f, 0.12f, 1f);
            var field = rt.gameObject.AddComponent<InputField>();
            var text = Label(rt, "Text", 26, TextAnchor.MiddleLeft, new Vector2(14, 0), size - new Vector2(28, 0), Palette.HudText);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0, 0.5f);
            text.supportRichText = false;
            var ph = Label(rt, "Placeholder", 24, TextAnchor.MiddleLeft, new Vector2(14, 0), size - new Vector2(28, 0), Palette.HudText.WithAlpha(0.4f));
            ph.rectTransform.anchorMin = ph.rectTransform.anchorMax = ph.rectTransform.pivot = new Vector2(0, 0.5f);
            ph.text = placeholder;
            field.textComponent = text;
            field.placeholder = ph;
            field.targetGraphic = img;
            field.contentType = InputField.ContentType.IntegerNumber;
            field.characterLimit = 10;
            return field;
        }

        Text SettingRow(Transform parent, float y, string label, UnityEngine.Events.UnityAction minus, UnityEngine.Events.UnityAction plus)
        {
            Label(parent, label + "Label", 23, TextAnchor.MiddleLeft, new Vector2(40, y), new Vector2(340, 50), Palette.HudText).text = label;
            MakeButton(parent, label + " <", new Vector2(0, 1), new Vector2(400, y), new Vector2(60, 50), minus, out var l1);
            var value = Label(parent, label + "Value", 23, TextAnchor.MiddleCenter, new Vector2(470, y), new Vector2(240, 50), Palette.Amber);
            MakeButton(parent, label + " >", new Vector2(0, 1), new Vector2(720, y), new Vector2(60, 50), plus, out var l2);
            l1.text = "<";
            l2.text = ">";
            return value;
        }

        void StartNewRunFromMenu()
        {
            var text = seedInput.text.Trim();
            seedError = null;
            if (text.Length == 0)
            {
                game.NewRun(PrototypeBootstrap.NewSeed(), game.FlatMap);
                return;
            }

            if (uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var seed)) game.NewRun(seed, game.FlatMap);
            else seedError = "Seed must be a whole number from 0 to 4294967295.";
        }

        void OpenSettings()
        {
            editing = JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(game.Settings));
            resolutions = Screen.resolutions;
            resolutionIndex = -1;
            for (var i = 0; i < resolutions.Length; i++)
                if (resolutions[i].width == (editing.width > 0 ? editing.width : Screen.width) && resolutions[i].height == (editing.height > 0 ? editing.height : Screen.height))
                    resolutionIndex = i;
            Set(settingsStatus, game.SettingsProblem ?? "");
            settingsPanel.SetActive(true);
        }

        void StepSensitivity(float delta) => editing.cameraSensitivity = Mathf.Clamp(Mathf.Round((editing.cameraSensitivity + delta) * 4f) / 4f, 0.25f, 3f);
        void ToggleShake() => editing.cameraShake = !editing.cameraShake;
        void ToggleWindow() => editing.fullscreen = !editing.fullscreen;

        void StepResolution(int step)
        {
            if (resolutions == null || resolutions.Length == 0) return;
            resolutionIndex = (resolutionIndex + step + resolutions.Length) % resolutions.Length;
            editing.width = resolutions[resolutionIndex].width;
            editing.height = resolutions[resolutionIndex].height;
        }

        void SaveSettings()
        {
            game.ApplySettings(editing, true);
            Set(settingsStatus, game.SettingsProblem ?? "Settings saved.");
            settingsStatus.color = game.SettingsProblem == null ? Palette.RouteCyan : Palette.WarningCoral;
        }

        /// <summary>Night-local consequences, e.g. "9 enemies defeated · Keep -40 HP · 1 building lost".</summary>
        public static string RecapLine(NightRecap r)
        {
            var keep = r.KeepDamage > 0 ? $"Keep -{r.KeepDamage} HP" : "Keep unharmed";
            var lost = r.BuildingsLost == 0 ? "no buildings lost" : r.BuildingsLost == 1 ? "1 building lost" : $"{r.BuildingsLost} buildings lost";
            return $"{r.EnemiesDefeated} enemies defeated · {keep} · {lost}";
        }

        static string SummaryText(RunSimulation sim)
        {
            var s = sim.Stats;
            var current = sim.ActiveSynergies().Count;
            var head = sim.State == RunState.Victory
                ? $"All {sim.TotalNights} nights survived. Keep {sim.Keep.Hp} / {sim.Keep.MaxHp} HP."
                : $"The Keep fell on Night {sim.NightNumber} of {sim.TotalNights} after {sim.NightElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s; {sim.AliveEnemyCount} enemies were attacking.";
            var tonight = sim.State == RunState.Defeat && sim.LastNight != null ? "\nThis night: " + RecapLine(sim.LastNight) : "";
            return $"{head}{tonight}\n\nRun: nights cleared {s.NightsCleared} / {sim.TotalNights}\nEnemies defeated {s.EnemiesDefeated}\nBuildings purchased {s.BuildingsPurchased}\n" +
                   $"Gold earned {s.GoldEarned} (starting 250 excluded)\nSynergies active {current} · ever {s.SynergiesEver.Count}\nSeed {sim.Seed.ToString(CultureInfo.InvariantCulture)}";
        }

        /// <summary>Called by the bootstrap for actual Keep HP loss; hits within the cue window add up.</summary>
        public void KeepHit(int damage)
        {
            if (keepHitText == null || damage <= 0) return;
            keepHitAmount = keepHitTimer > 0f ? keepHitAmount + damage : damage;
            keepHitTimer = 0.9f;
            keepHitText.text = $"-{keepHitAmount}";
        }

        /// <summary>Advances HUD cues by the presentation delta (frozen while paused or in the menu).</summary>
        public void TickEffects(float dt)
        {
            if (keepHitText == null || keepHitTimer <= 0f) return;
            keepHitTimer = Mathf.Max(0f, keepHitTimer - dt);
            var t = keepHitTimer / 0.9f;
            keepHitText.color = Palette.WarningCoral.WithAlpha(t);
            keepBarBack.color = Color.Lerp(new Color(0, 0, 0, 0.5f), Palette.WarningCoral.WithAlpha(0.85f), t);
            if (keepHitTimer <= 0f)
            {
                keepHitText.text = "";
                keepHitAmount = 0;
            }
        }

        void ChooseConfirm(int index)
        {
            var request = game.PendingConfirm;
            if (request == null || request != shownConfirm || index >= request.Options.Count) return;
            request.Options[index].action();
        }

        void RefreshConfirm()
        {
            var request = game.PendingConfirm;
            if (confirmPanel.activeSelf != (request != null)) confirmPanel.SetActive(request != null);
            shownConfirm = request;
            if (request == null) return;
            confirmPanel.transform.SetAsLastSibling();
            Set(confirmTitle, request.Title);
            Set(confirmBody, request.Body);
            var count = request.Options.Count;
            for (var i = 0; i < confirmButtons.Length; i++)
            {
                var show = i < count;
                if (confirmButtons[i].gameObject.activeSelf != show) confirmButtons[i].gameObject.SetActive(show);
                if (!show) continue;
                Set(confirmLabels[i], request.Options[i].label);
                // Centre two choices, spread three.
                ((RectTransform)confirmButtons[i].transform).anchoredPosition = new Vector2(count == 2 ? (i - 0.5f) * 300 : (i - 1) * 285, 28);
            }
        }

        void RefreshEntryMarkers(RunSimulation sim)
        {
            var show = sim.IsCampaign && !game.MenuOpen && (sim.State == RunState.Day || sim.State == RunState.Night);
            var wave = sim.Wave;
            var cam = game.CameraRig.Camera;
            var size = canvasRect.rect.size;
            for (var i = 0; i < entryMarkers.Length; i++)
            {
                var active = show && i < wave.EntryCells.Count;
                if (entryMarkers[i].activeSelf != active) entryMarkers[i].SetActive(active);
                if (!active) continue;
                var cell = wave.EntryCells[i];
                var count = 0;
                foreach (var s in wave.Spawns)
                    if (s.EntryIndex == i) count++;
                var isNew = sim.State == RunState.Day && IsNewEntry(sim, cell);

                // Project the entry tile, then keep the marker inside the free play area (below the top panels, above the cards).
                var label0 = $"{(isNew ? "NEW " : "")}{WaveText.EntryName(cell).ToUpperInvariant()} {count}";
                var halfWidth = ((label0.Length + 2) * 12f + 26f) / 2f;
                var sp = cam.WorldToScreenPoint(Prims.CellCenter(cell, 0.6f));
                var p = ScreenToCanvas(new Vector2(sp.x, sp.y));
                if (sp.z < 0) p = size - p;
                var minX = halfWidth + 16f;
                const float minY = 170f, topReserve = 260f;
                var clamped = new Vector2(Mathf.Clamp(p.x, minX, size.x - minX), Mathf.Clamp(p.y, minY, size.y - topReserve));
                var off = (clamped - p).sqrMagnitude > 1f;
                var arrow = !off ? "" : p.x < minX ? "< " : p.x > size.x - minX ? "> " : p.y > size.y - topReserve ? "^ " : "v ";
                var label = arrow + label0;
                Set(entryMarkerTexts[i], label);
                var rt = (RectTransform)entryMarkers[i].transform;
                rt.sizeDelta = new Vector2(label.Length * 12f + 26f, 44f);
                entryMarkerTexts[i].rectTransform.sizeDelta = rt.sizeDelta;
                rt.anchoredPosition = clamped;
            }
        }

        static bool IsNewEntry(RunSimulation sim, GridPos cell)
        {
            if (sim.NightNumber < 2) return false;
            foreach (var p in sim.Campaign.Nights[sim.NightNumber - 2].EntryCells)
                if (p == cell) return false;
            return true;
        }

        void RefreshMvpPanels(RunSimulation sim, PlayerController player)
        {
            var map = sim.Config.Terrain;
            Set(seedText, sim.StressMode ? "Stress fixture (debug)" : $"Seed {sim.Seed.ToString(CultureInfo.InvariantCulture)} · {(map == null || map.Authored ? "flat map" : map.Fallback ? "FALLBACK flat map" : "generated map")}");

            var sel = player.Selected;
            var canRotate = sel != null && sim.State == RunState.Day && !sim.StressMode && RunSimulation.IsRotatable(sel.Kind);
            if (RotateButton.gameObject.activeSelf != canRotate) RotateButton.gameObject.SetActive(canRotate);

            RefreshEntryMarkers(sim);
            RefreshConfirm();

            var persistence = game.Persistence;
            var pending = persistence != null && persistence.HasRun && persistence.SavePending;
            if (saveFailedPanel.activeSelf != pending) saveFailedPanel.SetActive(pending);
            if (pending)
                Set(saveFailedText, $"{persistence.LastError}\n\nNothing after your last successful save is stored yet. Gameplay is paused until the save succeeds; " +
                                    "quitting now keeps the previous checkpoint." + (game.ExitAfterSave ? " The game exits only after Retry succeeds." : ""));
            if (pending) foreach (var b in rewardButtons) b.interactable = false;
            else foreach (var b in rewardButtons) b.interactable = true;

            if (menuOverlay.activeSelf != game.MenuOpen) menuOverlay.SetActive(game.MenuOpen);
            if (!game.MenuOpen)
            {
                if (settingsPanel.activeSelf) settingsPanel.SetActive(false);
                return;
            }

            var hasRun = persistence != null && persistence.HasRun;
            var save = game.LatestSave;
            var canContinue = !hasRun && save != null && save.CanContinue;
            ContinueButton.gameObject.SetActive(!hasRun);
            ContinueButton.interactable = canContinue;
            Set(menuContinueLabel, canContinue ? $"Continue — Day {save.Sim.NightNumber} of {save.Sim.TotalNights} · seed {save.Sim.Seed}" : "Continue");
            var damaged = save != null && (save.Status == SaveStatus.Corrupt || save.Status == SaveStatus.UnknownSchema || save.Status == SaveStatus.Incompatible ||
                                           save.Status == SaveStatus.Invalid || save.Status == SaveStatus.IoError);
            RecoverButton.gameObject.SetActive(!hasRun && damaged);
            ResumeButton.gameObject.SetActive(hasRun);
            NewRunButton.gameObject.SetActive(!hasRun); // in-run: Save and Quit first, so a New Run never replaces the active run
            SaveAndQuitButton.gameObject.SetActive(hasRun);
            SaveAndQuitButton.interactable = hasRun && sim.State == RunState.Day && !pending;

            string status;
            if (hasRun)
                status = sim.State == RunState.Day
                    ? "Save and Quit stores this Day exactly as it is now."
                    : "Continue returns to the start of your current night: leaving now resumes from the last Day checkpoint.";
            else if (save == null || save.Status == SaveStatus.None) status = "No saved run yet.";
            else if (save.Status == SaveStatus.Ok) status = "Continue returns to the start of your current night. A New Run is saved separately and keeps this run's file.";
            else if (save.Status == SaveStatus.Terminal) status = $"Your last run ended ({save.Checkpoint.status}). Start a New Run or reuse its seed {save.Checkpoint.seed}.";
            else status = $"The saved run cannot be continued: {save.Message}\nThe file was left untouched. Recover the previous checkpoint or start a New Run (saved separately).";
            if (game.SettingsProblem != null) status += "\n" + game.SettingsProblem;
            if (seedError != null) status = seedError + "\n" + status;
            Set(menuStatus, status);

            if (settingsPanel.activeSelf && editing != null)
            {
                Set(settingsSensitivity, editing.cameraSensitivity.ToString("0.00", CultureInfo.InvariantCulture) + "x");
                Set(settingsShake, editing.cameraShake ? "On" : "Off");
                Set(settingsWindow, editing.fullscreen ? "Fullscreen" : "Windowed");
                Set(settingsResolution, editing.width > 0 ? $"{editing.width} x {editing.height}" : $"{Screen.width} x {Screen.height} (current)");
            }
        }
    }
}
