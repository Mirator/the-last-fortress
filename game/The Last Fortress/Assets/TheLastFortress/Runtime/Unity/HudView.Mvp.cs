using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastFortress
{
    /// <summary>
    /// MVP-only HUD: title/in-run menu, settings, help, credits, first-run guide, escalation announcements, run
    /// summary, seed display and save-failure handling.
    /// </summary>
    public sealed partial class HudView
    {
        GameObject menuOverlay, settingsPanel, saveFailedPanel, helpPanel, creditsPanel, guidePanel, announcePanel, golemBanner;
        Text menuStatus, menuContinueLabel, seedText, saveFailedText, settingsSensitivity, settingsShake, settingsWindow, settingsResolution, settingsStatus,
            settingsMaster, settingsMusic, settingsSfx, menuTitle, menuPromise, guideHeader, guideBody, announceText, golemTitle,
            settingsLook, settingsInvert, settingsFov, settingsHeroNight;
        InputField seedInput;
        GameSettings editing;
        Resolution[] resolutions;
        int resolutionIndex = -1;
        string seedError;
        Text keepHitText;
        Image keepBarBack;
        float keepHitTimer;
        int keepHitAmount;
        RectTransform gameplayLayer, menuPanel, seedRow, newRunRow;
        readonly List<(GameObject go, float height)> menuStack = new List<(GameObject, float)>();
        bool seedExpanded;
        Button showGuideButton;
        float golemBannerTimer;
        int golemBannerReset = -1, golemBannerNight = -1;
        RunSimulation golemBannerSim;

        /// <summary>How long the Golem's arrival banner stays (presentation time; it never blocks input or the outcome).</summary>
        public const float GolemBannerSeconds = 3.5f;

        /// <summary>Damage shown by the current Keep-hit cue (0 when none is showing).</summary>
        public int KeepHitShown => keepHitTimer > 0f ? keepHitAmount : 0;
        readonly GameObject[] entryMarkers = new GameObject[4];
        readonly Text[] entryMarkerTexts = new Text[4];

        /// <summary>Visible entry-marker texts (tests/diagnostics).</summary>
        public string EntryMarkerText(int i) => entryMarkers[i] != null && entryMarkers[i].activeSelf ? entryMarkerTexts[i].text : null;

        public GameObject MenuOverlay => menuOverlay;
        public GameObject SettingsPanel => settingsPanel;
        public GameObject SaveFailedPanel => saveFailedPanel;
        public GameObject HelpPanel => helpPanel;
        public GameObject CreditsPanel => creditsPanel;
        public GameObject GuidePanel => guidePanel;

        /// <summary>The in-run HUD layer; hidden as a whole while a menu is open.</summary>
        public GameObject GameplayLayer => gameplayLayer.gameObject;

        public string GuideText => guidePanel.activeSelf ? guideHeader.text + "\n" + guideBody.text : null;
        public string AnnouncementText => announcePanel.activeSelf ? announceText.text : null;
        public string GolemBannerText => golemBanner.activeSelf ? golemTitle.text : null;
        public Button NewRunButton { get; private set; }

        /// <summary>Release title only: cycles the map profile used by the next New Run.</summary>
        public Button MapButton { get; private set; }

        Text mapLabel, menuSubtitle;
        public Button ContinueButton { get; private set; }
        public Button RecoverButton { get; private set; }
        public Button ResumeButton { get; private set; }
        public Button SaveAndQuitButton { get; private set; }
        public Button SettingsButton { get; private set; }
        public Button RunMenuButton { get; private set; }

        /// <summary>In-run menu: retry the same seed from Day 1 (the top bar's Restart hides at Night).</summary>
        public Button MenuRestartRunButton { get; private set; }

        GameObject copySeedButton;
        public Button RotateButton { get; private set; }
        public Button NewSeedButton { get; private set; }
        public Button RetrySaveButton { get; private set; }
        public Button QuitButton { get; private set; }
        public Button HelpButton { get; private set; }
        public Button MenuHelpButton { get; private set; }
        public Button CreditsButton { get; private set; }
        public Button SkipGuideButton { get; private set; }
        public Button HideGuideButton { get; private set; }
        public Button ShowGuideButton => showGuideButton;
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

        /// <summary>"v0.1.0 · build 1a2b3c4d" for bug reports (the build id is absent in the Editor).</summary>
        public static string VersionText
        {
            get
            {
                var guid = Application.buildGUID;
                return string.IsNullOrEmpty(guid) ? $"Version {Application.version} (editor)" : $"Version {Application.version} · build {guid.Substring(0, Mathf.Min(8, guid.Length))}";
            }
        }

        void BuildRunMenuRow(RectTransform menu)
        {
            RunMenuButton = MakeButton(menu, "Menu", new Vector2(0, 1), new Vector2(284, -12), new Vector2(100, 52), game.OpenMenu, out var menuLabel);
            menuLabel.fontSize = 21;
            HelpButton = MakeButton(menu, "Help", new Vector2(0, 1), new Vector2(392, -12), new Vector2(96, 52), OpenHelp, out var helpLabel);
            helpLabel.fontSize = 21;
            seedText = Label(menu, "Seed", 18, TextAnchor.MiddleLeft, new Vector2(14, -68), new Vector2(360, 34), Palette.HudText);
            copySeedButton = MakeButton(menu, "Copy seed", new Vector2(0, 1), new Vector2(378, -68), new Vector2(110, 34), CopySeed, out var copyLabel).gameObject;
            copyLabel.fontSize = 18;
        }

        void CopySeed() => GUIUtility.systemCopyBuffer = game.Sim.Seed.ToString(CultureInfo.InvariantCulture);

        void BuildMvpPanels(RectTransform root)
        {
            // Terminal overlay: summary needs more room plus same-seed / new run / menu actions.
            var card = (RectTransform)terminalOverlay.transform.Find("Card");
            card.sizeDelta = new Vector2(760, 520);
            terminalBody.rectTransform.sizeDelta = new Vector2(720, 290);
            var retryLabel = (Text)TerminalRestartButton.GetComponentInChildren(typeof(Text));
            retryLabel.text = "Retry same seed";
            retryLabel.fontSize = 24;
            var restartRect = (RectTransform)TerminalRestartButton.transform;
            restartRect.anchoredPosition = new Vector2(-240, 30);
            restartRect.sizeDelta = new Vector2(224, 70);
            retryLabel.rectTransform.sizeDelta = restartRect.sizeDelta;
            NewSeedButton = MakeButton(card, "New seed", new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(224, 70), () => game.NewRun(PrototypeBootstrap.NewSeed(), game.FlatMap), out var newSeedLabel);
            newSeedLabel.text = "New Run";
            MakeButton(card, "Main menu", new Vector2(0.5f, 0), new Vector2(240, 30), new Vector2(224, 70), game.QuitToMenuWithoutSaving, out _);

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
                img.sprite = null; // a flat, full-strength coral tag: the panel shading and amber rule are for dark panels
                Destroy(m.Find("Accent").gameObject);
                entryMarkerTexts[i] = Label(m, "Text", 20, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(150, 44), Palette.HudSurface, true);
                entryMarkerTexts[i].rectTransform.anchorMin = entryMarkerTexts[i].rectTransform.anchorMax = entryMarkerTexts[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                entryMarkerTexts[i].fontStyle = FontStyle.Bold;
                entryMarkers[i] = m.gameObject;
                m.gameObject.SetActive(false);
            }

            // Day announcement under the status panel (the boss bar's slot, which only shows at Night): a new enemy
            // type or the final siege, read from the stored wave.
            announcePanel = Panel(root, "Announcement", new Vector2(0.5f, 1), new Vector2(0, -192), new Vector2(860, 64)).gameObject;
            // A click shows the full briefing again after it collapsed (or collapses it); UI clicks never reach the world.
            announcePanel.AddComponent<Button>().onClick.AddListener(() => announceExpanded = !announceExpanded);
            var stripe = Rect(announcePanel.transform, "Stripe", new Vector2(0, 0.5f), Vector2.zero, new Vector2(8, 64));
            stripe.anchorMin = new Vector2(0, 0);
            stripe.anchorMax = new Vector2(0, 1);
            stripe.offsetMin = stripe.offsetMax = Vector2.zero;
            stripe.sizeDelta = new Vector2(8, 0);
            var stripeImg = stripe.gameObject.AddComponent<Image>();
            stripeImg.color = Palette.WarningCoral;
            stripeImg.raycastTarget = false;
            announceText = Label(announcePanel.transform, "Text", 21, TextAnchor.UpperCenter, new Vector2(0, -8), new Vector2(820, 52), Palette.HudText, true);
            announcePanel.SetActive(false);

            // Boss arrival: a short, non-blocking title (the boss line in the status panel keeps its health afterwards; its
            // numbers are on the Day briefing and the Help).
            golemBanner = Panel(root, "GolemBanner", new Vector2(0.5f, 1), new Vector2(0, -262), new Vector2(760, 58)).gameObject;
            var gImg = golemBanner.GetComponent<Image>();
            gImg.color = Palette.HudSurface.WithAlpha(0.88f);
            gImg.raycastTarget = false;
            golemTitle = Label(golemBanner.transform, "Title", 34, TextAnchor.UpperCenter, new Vector2(0, -8), new Vector2(740, 44), Palette.WarningCoral, true);
            golemTitle.fontStyle = FontStyle.Bold;
            golemBanner.SetActive(false);

            // First-run guide: top-right under the run menu; never blocks Start Night.
            guidePanel = Panel(root, "Guide", new Vector2(1, 1), new Vector2(-24, -146), new Vector2(500, 150)).gameObject;
            guideHeader = Label(guidePanel.transform, "Header", 18, TextAnchor.UpperLeft, new Vector2(18, -10), new Vector2(470, 26), Palette.Amber);
            guideHeader.fontStyle = FontStyle.Bold;
            guideBody = Label(guidePanel.transform, "Body", 20, TextAnchor.UpperLeft, new Vector2(18, -38), new Vector2(464, 60), Palette.HudText);
            HideGuideButton = MakeButton(guidePanel.transform, "Hide", new Vector2(1, 0), new Vector2(-152, 10), new Vector2(90, 38), () => game.Onboarding.Hidden = true, out var hideLabel);
            SkipGuideButton = MakeButton(guidePanel.transform, "Skip guide", new Vector2(1, 0), new Vector2(-12, 10), new Vector2(132, 38), game.SkipOnboarding, out var skipLabel);
            hideLabel.fontSize = skipLabel.fontSize = 18;
            guidePanel.SetActive(false);

            if (game.IsRelease) BuildHeroPanel(root);
            gameplayLayer = BuildGameplayLayer(root, "EntryMarkers", "Resources", "Status", "BossBar", "Menu", "BuildCards", "Actions", "Speed", "Selection", "Hint",
                "Tooltip", "BreachLabel", "StressBanner", "Diagnostic", "Announcement", "GolemBanner", "HeroPanel", "KeepDirection", "TerminalOverlay", "RewardOverlay", "Guide");

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

            BuildMenu(root);
            BuildSettings(root);
            BuildHelp(root);
            BuildCredits(root);
        }

        /// <summary>
        /// Title and in-run menu: a column on the left with the world visible beside it. Buttons stack by visibility, so
        /// the title (New Run, Continue, …) and the in-run menu (Resume, Save and Quit, …) never leave gaps.
        /// </summary>
        void BuildMenu(RectTransform root)
        {
            menuOverlay = FullScreen(root, "MainMenu", 0.3f);
            menuPanel = Panel(menuOverlay.transform, "Panel", new Vector2(0, 0.5f), new Vector2(96, 0), new Vector2(700, 940));
            menuPanel.GetComponent<Image>().color = Palette.HudSurface.WithAlpha(0.95f);
            menuTitle = Label(menuPanel, "Title", 56, TextAnchor.UpperCenter, new Vector2(0, -40), new Vector2(680, 72), Palette.Amber, true);
            menuTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            menuTitle.text = "THE LAST FORTRESS";
            menuTitle.fontStyle = FontStyle.Bold;
            menuPromise = Label(menuPanel, "Promise", 30, TextAnchor.UpperCenter, new Vector2(0, -116), new Vector2(660, 40), Palette.HudText, true);
            menuPromise.text = "Build by day. Hold until dawn.";
            menuSubtitle = Label(menuPanel, "Subtitle", 20, TextAnchor.UpperCenter, new Vector2(0, -160), new Vector2(660, 30), Palette.HudText.WithAlpha(0.75f), true);
            menuSubtitle.text = "Protect the Keep through six nights and a final siege.";

            newRunRow = Rect(menuPanel, "NewRunRow", new Vector2(0.5f, 1), Vector2.zero, new Vector2(560, 64));
            var release = game.IsRelease;
            NewRunButton = MakeButton(newRunRow, "New Run", new Vector2(0, 1), Vector2.zero, new Vector2(release ? 290 : 440, 64), StartNewRunFromMenu, out var newLabel);
            if (release)
            {
                // Map profile for the next New Run: cycles Ravine → Crossing → Meadow; its one-line summary is the subtitle.
                MapButton = MakeButton(newRunRow, "Map", new Vector2(0, 1), new Vector2(300, 0), new Vector2(144, 64), game.CycleMapProfile, out mapLabel);
                mapLabel.fontSize = 19;
            }
            newLabel.fontSize = 28;
            NewRunButton.GetComponent<Image>().color = Palette.Amber;
            newLabel.color = Palette.HudSurface;
            MakeButton(newRunRow, "Seed…", new Vector2(0, 1), new Vector2(452, 0), new Vector2(108, 64), () => seedExpanded = !seedExpanded, out var seedToggle);
            seedToggle.fontSize = 20;

            seedRow = Rect(menuPanel, "SeedRow", new Vector2(0.5f, 1), Vector2.zero, new Vector2(560, 50));
            Label(seedRow, "SeedLabel", 20, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(200, 50), Palette.HudText).text = "Seed (optional)";
            seedInput = MakeInput(seedRow, new Vector2(200, 0), new Vector2(360, 50), "random");

            ContinueButton = MakeButton(menuPanel, "Continue", new Vector2(0.5f, 1), Vector2.zero, new Vector2(560, 64), game.Continue, out menuContinueLabel);
            menuContinueLabel.fontSize = 22;
            RecoverButton = MakeButton(menuPanel, "Recover backup", new Vector2(0.5f, 1), Vector2.zero, new Vector2(560, 56), () => game.RecoverBackup(), out _);
            ResumeButton = MakeButton(menuPanel, "Resume", new Vector2(0.5f, 1), Vector2.zero, new Vector2(560, 64), game.CloseMenu, out _);
            MenuRestartRunButton = MakeButton(menuPanel, "Restart (same seed, Day 1)", new Vector2(0.5f, 1), Vector2.zero, new Vector2(560, 56), () =>
            {
                game.CloseMenu();
                game.RequestRestart(); // a progressed run asks first; the dialog holds the world like the menu did
            }, out _);
            SaveAndQuitButton = MakeButton(menuPanel, "Save and Quit", new Vector2(0.5f, 1), Vector2.zero, new Vector2(560, 56), () => game.SaveAndQuit(), out _);
            SettingsButton = MakeButton(menuPanel, "Settings", new Vector2(0.5f, 1), Vector2.zero, new Vector2(560, 56), OpenSettings, out _);
            MenuHelpButton = MakeButton(menuPanel, "Help", new Vector2(0.5f, 1), Vector2.zero, new Vector2(560, 56), OpenHelp, out _);
            CreditsButton = MakeButton(menuPanel, "Credits", new Vector2(0.5f, 1), Vector2.zero, new Vector2(560, 56), () => creditsPanel.SetActive(true), out _);
            QuitButton = MakeButton(menuPanel, "Quit game", new Vector2(0.5f, 1), Vector2.zero, new Vector2(560, 56), game.RequestQuit, out _);
            menuStack.Add((newRunRow.gameObject, 64));
            menuStack.Add((seedRow.gameObject, 50));
            menuStack.Add((ContinueButton.gameObject, 64));
            menuStack.Add((RecoverButton.gameObject, 56));
            menuStack.Add((ResumeButton.gameObject, 64));
            menuStack.Add((MenuRestartRunButton.gameObject, 56));
            menuStack.Add((SaveAndQuitButton.gameObject, 56));
            menuStack.Add((SettingsButton.gameObject, 56));
            menuStack.Add((MenuHelpButton.gameObject, 56));
            menuStack.Add((CreditsButton.gameObject, 56));
            menuStack.Add((QuitButton.gameObject, 56));
            menuStatus = Label(menuPanel, "Status", 19, TextAnchor.UpperCenter, Vector2.zero, new Vector2(640, 110), Palette.HudText, true);
            Label(menuPanel, "Version", 18, TextAnchor.LowerCenter, new Vector2(0, -904), new Vector2(660, 26), Palette.HudText.WithAlpha(0.7f), true).text = VersionText;
            menuOverlay.SetActive(false);
        }

        void BuildSettings(RectTransform root)
        {
            settingsPanel = FullScreen(root, "Settings", 0.4f);
            var hero = game.IsRelease;
            var sp = Panel(settingsPanel.transform, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, hero ? 1010 : 780));
            Opaque(sp);
            sp.pivot = new Vector2(0.5f, 0.5f);
            Label(sp, "Title", 44, TextAnchor.UpperCenter, new Vector2(0, -24), new Vector2(780, 56), Palette.Amber, true).text = "SETTINGS";
            settingsSensitivity = SettingRow(sp, -110, "Camera sensitivity", () => StepSensitivity(-0.25f), () => StepSensitivity(0.25f));
            settingsShake = SettingRow(sp, -175, "Camera shake on Keep hits", ToggleShake, ToggleShake);
            settingsWindow = SettingRow(sp, -240, "Window mode", ToggleWindow, ToggleWindow);
            settingsResolution = SettingRow(sp, -305, "Resolution", () => StepResolution(-1), () => StepResolution(1));
            settingsMaster = SettingRow(sp, -385, "Master volume", () => StepVolume(0, -0.1f), () => StepVolume(0, 0.1f));
            settingsMusic = SettingRow(sp, -450, "Music volume", () => StepVolume(1, -0.1f), () => StepVolume(1, 0.1f));
            settingsSfx = SettingRow(sp, -515, "Effects volume", () => StepVolume(2, -0.1f), () => StepVolume(2, 0.1f));
            if (hero)
            {
                // Hero view camera (plan 012, settings version 3).
                settingsLook = SettingRow(sp, -595, "Hero look sensitivity", () => StepLook(-0.25f), () => StepLook(0.25f));
                settingsInvert = SettingRow(sp, -660, "Invert look up/down", ToggleInvert, ToggleInvert);
                settingsFov = SettingRow(sp, -725, "Hero field of view", () => StepFov(-5f), () => StepFov(5f));
                settingsHeroNight = SettingRow(sp, -790, "Hero view when night starts", ToggleHeroNight, ToggleHeroNight);
            }

            var noteY = hero ? -805f : -575f;
            Label(sp, "AudioNote", 18, TextAnchor.UpperCenter, new Vector2(0, noteY - 50), new Vector2(760, 30), Palette.HudText.WithAlpha(0.6f), true).text =
                "Changes preview immediately. Save settings keeps them; Back discards them.";
            MakeButton(sp, "Save settings", new Vector2(0.5f, 0), new Vector2(-160, 30), new Vector2(280, 60), SaveSettings, out _);
            MakeButton(sp, "Back", new Vector2(0.5f, 0), new Vector2(160, 30), new Vector2(280, 60), CloseSettings, out _);
            settingsStatus = Label(sp, "Status", 19, TextAnchor.UpperCenter, new Vector2(0, noteY - 87), new Vector2(760, 50), Palette.WarningCoral, true);
            settingsPanel.SetActive(false);
        }

        void BuildHelp(RectTransform root)
        {
            helpPanel = FullScreen(root, "Help", 0.5f);
            var hp = Panel(helpPanel.transform, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 660));
            Opaque(hp);
            hp.pivot = new Vector2(0.5f, 0.5f);
            Label(hp, "Title", 44, TextAnchor.UpperCenter, new Vector2(0, -24), new Vector2(940, 56), Palette.Amber, true).text = "HELP";
            Label(hp, "Goal", 22, TextAnchor.UpperLeft, new Vector2(44, -96), new Vector2(900, 90), Palette.HudText).text =
                (game.IsRelease
                    ? "Goal: keep the Keep standing through twelve nights; Siege Golems come on Nights 6, 9 and 11, the Siege Commander on Night 12.\n"
                    : "Goal: keep the Keep standing through six nights; the last one is a siege with a Siege Golem.\n") +
                "Build by day, then start the night. Towers fire on their own. Enemies take the open route to the Keep; " +
                "if Walls seal it, they break through the first structure in the way.";
            Label(hp, "Controls", 21, TextAnchor.UpperLeft, new Vector2(44, -210), new Vector2(900, 330), Palette.HudText).text =
                (game.IsRelease
                    ? "1–8  choose Wall, Archer Tower, House, Farm, Spike Trap, Cannon, Watchtower, Repair Station\n"
                    : "1–5  choose Wall, Archer Tower, House, Farm, Spike Trap\n") +
                "Left click  place / select a building\n" +
                "Right click or Esc  cancel the building tool or selection\n" +
                "Hold right mouse button and drag  move around the map\n" +
                "R  rotate the ghost or the selected building (Walls connect automatically)\n" +
                (game.IsRelease ? "U or Upgrade  raise the selected building one tier by day (I → II → III; each tier adds a trait)\n" : "") +
                "WASD / arrow keys  pan · mouse wheel  zoom · Reset Camera  back to the Keep · Z  the whole board\n" +
                "Space or PAUSE  pause and resume the night (inspection still works)\n" +
                "F or II / 1× / 2× / 3×  night speed (picking a speed also resumes)\n" +
                "C or CALL EARLY  send the rest of tonight's enemies 4x sooner for gold at dawn (once a night)\n" +
                (game.IsRelease ? "Tab  hero view: WASD move · mouse look · click swing · Space jump · P pause · Enter start night · H find hero\n" : "") +
                "Menu  save and quit by day, settings, credits";
            showGuideButton = MakeButton(hp, "Show guide", new Vector2(0.5f, 0), new Vector2(-160, 30), new Vector2(280, 60), () =>
            {
                game.ReopenOnboarding();
                helpPanel.SetActive(false);
            }, out _);
            MakeButton(hp, "Close", new Vector2(0.5f, 0), new Vector2(160, 30), new Vector2(280, 60), () => helpPanel.SetActive(false), out _);
            helpPanel.SetActive(false);
        }

        void BuildCredits(RectTransform root)
        {
            creditsPanel = FullScreen(root, "Credits", 0.5f);
            var cp = Panel(creditsPanel.transform, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 640));
            Opaque(cp);
            cp.pivot = new Vector2(0.5f, 0.5f);
            Label(cp, "Title", 44, TextAnchor.UpperCenter, new Vector2(0, -24), new Vector2(940, 56), Palette.Amber, true).text = "CREDITS";
            Label(cp, "Body", 20, TextAnchor.UpperLeft, new Vector2(44, -100), new Vector2(900, 440), Palette.HudText).text =
                "The Last Fortress — design, code, models and icons made for this project.\n\n" +
                "Music and sound: synthesized for this project by its own script (Audio/Source~/synth.py); no recordings, " +
                "sample libraries or third-party sounds are used.\n\n" +
                "Icons: project line icons (design/icons) and new drawings in the same style.\n\n" +
                "Fonts: the runtime font shipped with the Unity Engine (LegacyRuntime.ttf); headings use a serif already installed with Windows (Palatino Linotype, Book Antiqua or Georgia), never shipped with the game.\n\n" +
                "Made with Unity " + Application.unityVersion + ", Universal Render Pipeline and Input System, under the Unity Engine licence.\n\n" +
                VersionText;
            MakeButton(cp, "Close", new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(280, 60), () => creditsPanel.SetActive(false), out _);
            creditsPanel.SetActive(false);
        }

        /// <summary>Dialogs drawn over the menu use an opaque surface so the text underneath never shows through.</summary>
        static void Opaque(RectTransform panel) => panel.GetComponent<Image>().color = Palette.HudSurface;

        void OpenHelp()
        {
            helpPanel.transform.SetAsLastSibling();
            helpPanel.SetActive(true);
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
            ApplyStates(field);
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
            else
            {
                seedError = "Seed must be a whole number from 0 to 4294967295.";
                seedExpanded = true;
                game.Audio?.Play(SoundId.UiReject);
            }
        }

        void OpenSettings()
        {
            editing = game.Settings.Clone();
            resolutions = Screen.resolutions;
            resolutionIndex = -1;
            for (var i = 0; i < resolutions.Length; i++)
                if (resolutions[i].width == (editing.width > 0 ? editing.width : Screen.width) && resolutions[i].height == (editing.height > 0 ? editing.height : Screen.height))
                    resolutionIndex = i;
            Set(settingsStatus, game.SettingsProblem ?? "");
            settingsPanel.transform.SetAsLastSibling();
            settingsPanel.SetActive(true);
        }

        /// <summary>Back without saving: the previewed volumes return to the saved ones.</summary>
        void CloseSettings()
        {
            var saved = game.Settings;
            game.Audio?.ApplyVolumes(saved.masterVolume, saved.musicVolume, saved.sfxVolume);
            settingsPanel.SetActive(false);
        }

        void StepLook(float delta) => editing.lookSensitivity = Mathf.Clamp(Mathf.Round((editing.lookSensitivity + delta) * 4f) / 4f, 0.25f, 3f);
        void ToggleInvert() => editing.invertLook = !editing.invertLook;
        void StepFov(float delta) => editing.fieldOfView = Mathf.Clamp(editing.fieldOfView + delta, 50f, 90f);
        void ToggleHeroNight() => editing.heroViewAtNight = !editing.heroViewAtNight;

        void StepSensitivity(float delta) => editing.cameraSensitivity = Mathf.Clamp(Mathf.Round((editing.cameraSensitivity + delta) * 4f) / 4f, 0.25f, 3f);
        void ToggleShake() => editing.cameraShake = !editing.cameraShake;
        void ToggleWindow() => editing.fullscreen = !editing.fullscreen;

        /// <summary>Steps a volume by 10% and previews it at once (effects play a sample at the new level).</summary>
        void StepVolume(int which, float delta)
        {
            float Step(float v) => Mathf.Clamp01(Mathf.Round((v + delta) * 10f) / 10f);
            if (which == 0) editing.masterVolume = Step(editing.masterVolume);
            else if (which == 1) editing.musicVolume = Step(editing.musicVolume);
            else editing.sfxVolume = Step(editing.sfxVolume);
            game.Audio?.ApplyVolumes(editing.masterVolume, editing.musicVolume, editing.sfxVolume);
            if (which != 1) game.Audio?.Play(SoundId.UiConfirm);
        }

        static string Percent(float v) => v <= 0f ? "Off" : Mathf.RoundToInt(v * 100f).ToString(CultureInfo.InvariantCulture) + "%";

        void StepResolution(int step)
        {
            if (resolutions == null || resolutions.Length == 0) return;
            resolutionIndex = (resolutionIndex + step + resolutions.Length) % resolutions.Length;
            editing.width = resolutions[resolutionIndex].width;
            editing.height = resolutions[resolutionIndex].height;
        }

        void SaveSettings()
        {
            game.ApplySettings(editing.Clone(), true);
            Set(settingsStatus, game.SettingsProblem ?? "Settings saved.");
            settingsStatus.color = game.SettingsProblem == null ? Palette.RouteCyan : Palette.WarningCoral;
        }

        /// <summary>Night-local consequences, e.g. "9 enemies defeated · Keep -40 HP · 1 building lost".</summary>
        public static string RecapLine(NightRecap r)
        {
            var keep = r.KeepDamage > 0 ? $"Keep -{r.KeepDamage} HP" : "Keep unharmed";
            var lost = r.BuildingsLost == 0 ? "no buildings lost" : r.BuildingsLost == 1 ? "1 building lost" : $"{r.BuildingsLost} buildings lost";
            var hero = r.HeroKills > 0 ? $" (hero {r.HeroKills})" : "";
            var called = r.EarlyCallGold > 0 ? $" · called early: +{r.EarlyCallGold} gold at dawn" : "";
            return $"{r.EnemiesDefeated} {(r.EnemiesDefeated == 1 ? "enemy" : "enemies")} defeated{hero} · {keep} · {lost}{called}";
        }

        static string SummaryText(RunSimulation sim)
        {
            var s = sim.Stats;
            var current = sim.ActiveSynergies().Count;
            var head = sim.State == RunState.Victory
                ? $"All {sim.TotalNights} nights survived. Keep {sim.Keep.Hp} / {sim.Keep.MaxHp} HP."
                : $"The Keep fell on Night {sim.NightNumber} of {sim.TotalNights} after {sim.NightElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s; {sim.AliveEnemyCount} {(sim.AliveEnemyCount == 1 ? "enemy was" : "enemies were")} attacking.";
            var tonight = sim.State == RunState.Defeat && sim.LastNight != null ? "\nThis night: " + RecapLine(sim.LastNight) : "";
            var heroKills = sim.HasHero ? $" (hero {s.HeroKills})" : "";
            return $"{head}{tonight}\n\nRun: nights cleared {s.NightsCleared} / {sim.TotalNights}\nEnemies defeated {s.EnemiesDefeated}{heroKills}\nBuildings purchased {s.BuildingsPurchased}\n" +
                   $"Gold earned {s.GoldEarned} (starting 250 excluded)\nSynergies active {current} · formed this run {s.SynergiesEver.Count}\nSeed {sim.Seed.ToString(CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// Day announcement from the stored campaign: the first night an enemy type appears, and the final siege. Values
        /// come from the definitions, so the text stays true if they are tuned. Null when there is nothing new.
        /// </summary>
        public static string Announcement(RunSimulation sim)
        {
            if (!sim.IsCampaign || sim.State != RunState.Day || sim.StressMode) return null;
            var tonight = sim.Campaign.Nights[sim.NightNumber - 1];
            var lines = new List<string>();
            var seenKinds = new bool[EnemyDefinition.KindCount];
            for (var n = 0; n < sim.NightNumber - 1; n++)
                foreach (var s in sim.Campaign.Nights[n].Spawns)
                    seenKinds[(int)s.Kind] = true;
            var newKinds = new bool[EnemyDefinition.KindCount];
            foreach (var s in tonight.Spawns)
                if (!seenKinds[(int)s.Kind]) newKinds[(int)s.Kind] = true;

            var final = sim.NightNumber == sim.TotalNights && sim.TotalNights > 1;
            if (final) lines.Add($"FINAL SIEGE — Night {sim.NightNumber} of {sim.TotalNights}: survive it and the run is won.");
            for (var k = 1; k < EnemyDefinition.KindCount; k++)
            {
                if (!newKinds[k]) continue;
                var d = sim.Definitions.EnemyFor((EnemyKind)k);
                var speed = d.SpeedTilesPerSecond.ToString("0.0#", CultureInfo.InvariantCulture);
                var walls = d.WallDamageMultiplier > 1 && !d.IsBomber ? $", {d.WallDamageMultiplier}x damage to Walls" : "";
                lines.Add($"NEW ENEMY: {d.DisplayName} — {Role(d)} ({d.MaxHp} HP, {speed} tiles/s{walls}).");
            }

            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        /// <summary>Which map this is: the release profile by name (and how it is turned), else the MVP generator's wording.</summary>
        static string MapLabel(TerrainMap map)
        {
            if (map == null || map.Authored) return "flat map";
            if (map.Profile != "" && MapProfiles.IsKnown(map.Profile))
            {
                var turn = MapProfiles.SymmetryNames[map.Symmetry];
                return $"{MapProfiles.Get(map.Profile).DisplayName}{(turn != "" ? " (" + turn + ")" : "")}{(map.Fallback ? " · fallback" : "")}";
            }

            return map.Fallback ? "FALLBACK flat map" : "generated map";
        }

        /// <summary>What an enemy kind does, with values from its definition so tuning keeps the text true.</summary>
        static string Role(EnemyDefinition d)
        {
            var inv = CultureInfo.InvariantCulture;
            switch (d.Kind)
            {
                case EnemyKind.Runner: return "fast and fragile";
                case EnemyKind.Brute: return "slow and heavy";
                case EnemyKind.SiegeGolem: return "a massive siege boss";
                case EnemyKind.Archer: return $"stops {d.AttackRange.ToString("0.#", inv)} tiles away and shoots, Houses and Farms first";
                case EnemyKind.Bomber: return $"blows up whatever blocks it, a Wall or the Keep, {d.FuseSeconds.ToString("0.#", inv)} s after arriving ({d.BlastDamage}, {d.WallDamageMultiplier}x to Walls) unless killed first";
                case EnemyKind.SiegeCommander: return $"the final boss: enemies within {d.AuraRadius.ToString("0.#", inv)} tiles hit {d.AuraDamagePercent}% harder";
                default: return "";
            }
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
            if (golemBanner != null && golemBannerTimer > 0f)
            {
                golemBannerTimer = Mathf.Max(0f, golemBannerTimer - dt);
                var g = golemBannerTimer > 0f && game.Sim.State == RunState.Night;
                if (golemBanner.activeSelf != g) golemBanner.SetActive(g);
            }

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
            var cam = game.ActiveCamera;
            var size = canvasRect.rect.size;
            for (var i = 0; i < entryMarkers.Length; i++)
            {
                var active = show && i < wave.EntryCells.Count;
                if (entryMarkers[i].activeSelf != active) entryMarkers[i].SetActive(active);
                if (!active) continue;
                var cell = wave.EntryCells[i];
                var count = 0;
                var pending = 0;
                for (var k = 0; k < wave.Spawns.Count; k++)
                    if (wave.Spawns[k].EntryIndex == i)
                    {
                        count++;
                        if (k >= sim.SpawnedCount) pending++;
                    }

                // At Night the pads and torches mark the entries: a tag stays only as an edge arrow, without counts, and
                // only while that entry still has enemies to send.
                var night = sim.State == RunState.Night;
                if (night && pending == 0)
                {
                    if (entryMarkers[i].activeSelf) entryMarkers[i].SetActive(false);
                    continue;
                }

                var isNew = sim.State == RunState.Day && IsNewEntry(sim, cell);

                // Project the entry's road mouth and set the tag beside it, on the side away from the screen centre: the routes
                // run toward the Keep in the middle, so neither the entry nor the route's first cells are covered. The tag
                // stays inside the free play area (below the top panels, above the cards); an arrow means the entry is off screen.
                var label0 = $"{(isNew ? "NEW " : "")}{WaveText.EntryName(cell).ToUpperInvariant()}{(night ? "" : $" {count}")}";
                var halfWidth = ((label0.Length + 2) * 12f + 26f) / 2f;
                var sp = cam.WorldToScreenPoint(RouteOverlay.RoadMouth(cell));
                var g = ScreenToCanvas(new Vector2(sp.x, sp.y));
                if (sp.z < 0) g = size - g;
                var p = new Vector2(g.x + (g.x < size.x * 0.5f ? -1f : 1f) * (halfWidth + 34f), g.y);
                var minX = halfWidth + 16f;
                var minY = game.HeroViewActive ? 300f : 170f; // the hero view keeps tags above the speed and night buttons
                var topReserve = Mathf.Max(260f, topPanelsBottom + 30f); // stay clear of status, boss bar, announcement, banner and guide
                var clamped = new Vector2(Mathf.Clamp(p.x, minX, size.x - minX), Mathf.Clamp(p.y, minY, size.y - topReserve));
                // The resources panel (top left) grows with its perks list: a tag beside it stays below it.
                if (clamped.x - halfWidth < resourcesRect.anchoredPosition.x + resourcesRect.sizeDelta.x + 10f)
                    clamped.y = Mathf.Min(clamped.y, size.y + resourcesRect.anchoredPosition.y - resourcesRect.sizeDelta.y - 32f);
                // Low on screen the corners hold the Hero panel (left) and the speed and night buttons (right): keep tags between.
                if (clamped.y < 330f) clamped.x = Mathf.Clamp(clamped.x, 420f + halfWidth, size.x - 340f - halfWidth);
                var hidden = g.x < 0f || g.x > size.x || g.y < minY || g.y > size.y - topReserve + 22f;
                var arrow = !hidden ? "" : g.x < 0f ? "< " : g.x > size.x ? "> " : g.y >= minY ? "^ " : "v ";
                // The hero view and the Night show a tag only as an edge arrow: on screen it would cover the fight.
                // At Night an entry under the top panels still has its pad and torch in view: only one truly off screen counts.
                var offScreen = sp.z < 0 || g.x < 0f || g.x > size.x || g.y < 0f || g.y > size.y;
                if ((game.HeroViewActive && !hidden) || (night && !offScreen))
                {
                    if (entryMarkers[i].activeSelf) entryMarkers[i].SetActive(false);
                    continue;
                }

                var label = arrow + label0;
                Set(entryMarkerTexts[i], label);
                var rt = (RectTransform)entryMarkers[i].transform;
                rt.sizeDelta = new Vector2(label.Length * 12f + 26f, 44f);
                entryMarkerTexts[i].rectTransform.sizeDelta = rt.sizeDelta;
                rt.anchoredPosition = clamped;
            }
        }

        internal static bool IsNewEntry(RunSimulation sim, GridPos cell)
        {
            if (sim.NightNumber < 2) return false;
            foreach (var p in sim.Campaign.Nights[sim.NightNumber - 2].EntryCells)
                if (p == cell) return false;
            return true;
        }

        void RefreshGuide(RunSimulation sim)
        {
            var guide = game.Onboarding;
            var step = guide != null && !game.MenuOpen && !sim.IsTerminal ? guide.Shown(sim) : null;
            if (guidePanel.activeSelf != step.HasValue) guidePanel.SetActive(step.HasValue);
            if (!step.HasValue) return;
            Set(guideHeader, $"GUIDE {(int)step.Value + 1} / {OnboardingState.StepCount} · Goal: hold the Keep for {sim.TotalNights} nights");
            Set(guideBody, OnboardingState.Text(step.Value));
            var h = Mathf.Max(48f, Mathf.Ceil(guideBody.preferredHeight));
            guideBody.rectTransform.sizeDelta = new Vector2(464, h);
            ((RectTransform)guidePanel.transform).sizeDelta = new Vector2(500, 38 + h + 58);
        }

        static readonly Color GuideCard = Palette.Hex(0x5C4C2E);

        /// <summary>While the guide asks for a Tower or an income building, that card is tinted amber (not color alone: the guide names it).</summary>
        void HighlightGuideCards(RunSimulation sim, PlayerController player)
        {
            var step = guidePanel.activeSelf && game.Onboarding != null ? game.Onboarding.Shown(sim) : null;
            foreach (var card in cards)
            {
                var wanted = step == OnboardingStep.PlaceTower && card.Kind == BuildingKind.Tower ||
                             step == OnboardingStep.Economy && (card.Kind == BuildingKind.House || card.Kind == BuildingKind.Farm);
                if (wanted && player.Tool != card.Kind) card.Image.color = GuideCard;
            }
        }

        /// <summary>The Day briefing shows in full this long, then collapses to its first line until clicked [P].</summary>
        public const float AnnouncementFullSeconds = 12f;

        RunSimulation announceSim;
        int announceReset = -1, announceNight = -1;
        float announceShownAt;
        bool announceExpanded;

        /// <summary>True while the Day briefing shows only its first line.</summary>
        public bool AnnouncementCollapsed { get; private set; }

        void RefreshAnnouncements(RunSimulation sim)
        {
            var text = game.MenuOpen ? null : Announcement(sim);
            if (announcePanel.activeSelf != (text != null)) announcePanel.SetActive(text != null);
            if (text != null)
            {
                // Full for the first seconds of each Day, then one line, so the briefing stops covering the board while building.
                if (announceSim != sim || announceReset != sim.ResetCount || announceNight != sim.NightNumber)
                {
                    (announceSim, announceReset, announceNight) = (sim, sim.ResetCount, sim.NightNumber);
                    announceShownAt = Time.unscaledTime;
                    announceExpanded = false;
                }

                var firstBreak = text.IndexOf('\n');
                AnnouncementCollapsed = firstBreak >= 0 && !announceExpanded && Time.unscaledTime - announceShownAt >= AnnouncementFullSeconds;
                if (AnnouncementCollapsed) text = text.Substring(0, firstBreak) + "  (click for details)";
                Set(announceText, text);
                var h = Mathf.Ceil(announceText.preferredHeight);
                announceText.rectTransform.sizeDelta = new Vector2(820, h);
                ((RectTransform)announcePanel.transform).sizeDelta = new Vector2(860, h + 18);
            }

            // Golem arrival: once per actual appearance in a run's night, from the live simulation (not on load replays).
            Enemy boss = null;
            foreach (var e in sim.Enemies)
                if (e.Alive && e.Definition.IsBoss) boss = e;
            if (boss != null && sim.State == RunState.Night && (golemBannerSim != sim || golemBannerReset != sim.ResetCount || golemBannerNight != sim.NightNumber))
            {
                golemBannerSim = sim;
                golemBannerReset = sim.ResetCount;
                golemBannerNight = sim.NightNumber;
                golemBannerTimer = GolemBannerSeconds;
                var bd = boss.Definition;
                Set(golemTitle, $"THE {bd.DisplayName.ToUpperInvariant()} HAS ARRIVED");
                golemBanner.SetActive(true);
            }
            else if (sim.State != RunState.Night && golemBanner.activeSelf)
            {
                golemBannerTimer = 0f;
                golemBanner.SetActive(false);
            }
        }

        void RefreshMvpPanels(RunSimulation sim, PlayerController player)
        {
            var map = sim.Config.Terrain;
            Set(seedText, sim.StressMode ? "Stress fixture (debug)" : $"Seed {sim.Seed.ToString(CultureInfo.InvariantCulture)} · {MapLabel(map)}");
            LayoutTopRight(sim.State == RunState.Night && !sim.StressMode);

            var sel = player.Selected;
            var canRotate = sel != null && sim.State == RunState.Day && !sim.StressMode && RunSimulation.IsRotatable(sel.Kind);
            if (RotateButton.gameObject.activeSelf != canRotate) RotateButton.gameObject.SetActive(canRotate);

            RefreshGuide(sim);
            HighlightGuideCards(sim, player);
            RefreshAnnouncements(sim);
            LayoutTop();
            RefreshEntryMarkers(sim);
            RefreshHero(sim);
            RefreshConfirm();

            var persistence = game.Persistence;
            var hasRunNow = persistence != null && persistence.HasRun;
            var pending = hasRunNow && persistence.SavePending;
            if (saveFailedPanel.activeSelf != pending) saveFailedPanel.SetActive(pending);
            if (pending)
                Set(saveFailedText, $"{persistence.LastError}\n\nNothing after your last successful save is stored yet. Gameplay is paused until the save succeeds; " +
                                    "quitting now keeps the previous checkpoint." + (game.ExitAfterSave ? " The game exits only after Retry succeeds." : ""));
            if (pending) foreach (var b in rewardButtons) b.interactable = false;
            else foreach (var b in rewardButtons) b.interactable = !RewardLocked;

            if (showGuideButton.gameObject.activeSelf != (hasRunNow && game.Onboarding != null)) showGuideButton.gameObject.SetActive(hasRunNow && game.Onboarding != null);

            // Menus replace the in-run HUD; dialogs and the save-failure overlay stay above both.
            if (gameplayLayer.gameObject.activeSelf == game.MenuOpen) gameplayLayer.gameObject.SetActive(!game.MenuOpen);
            if (menuOverlay.activeSelf != game.MenuOpen) menuOverlay.SetActive(game.MenuOpen);
            if (!game.MenuOpen)
            {
                if (settingsPanel.activeSelf) CloseSettings();
                if (creditsPanel.activeSelf) creditsPanel.SetActive(false);
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
            MenuRestartRunButton.gameObject.SetActive(hasRun && !sim.IsTerminal);
            newRunRow.gameObject.SetActive(!hasRun); // in-run: Save and Quit first, so a New Run never replaces the active run
            if (MapButton != null)
            {
                if (game.MapProfile == "")
                {
                    Set(mapLabel, "Map: by seed");
                    Set(menuSubtitle, hasRun ? "" : "Twelve nights to the final siege. The seed picks Ravine, Crossing or Meadow and turns it.");
                }
                else
                {
                    var profile = MapProfiles.Get(game.MapProfile);
                    Set(mapLabel, "Map: " + profile.DisplayName);
                    Set(menuSubtitle, hasRun ? "" : $"Twelve nights to the final siege. {profile.DisplayName}: {profile.Summary}");
                }
            }
            seedRow.gameObject.SetActive(!hasRun && (seedExpanded || seedError != null));
            SaveAndQuitButton.gameObject.SetActive(hasRun);
            SaveAndQuitButton.interactable = hasRun && sim.State == RunState.Day && !pending;
            CreditsButton.gameObject.SetActive(!hasRun);
            Set(menuPromise, hasRun ? $"Paused — {(sim.State == RunState.Day ? "Day" : sim.State == RunState.Night ? "Night" : "Reward")} {sim.NightNumber} of {sim.TotalNights}" : "Build by day. Hold until dawn.");
            LayoutMenu();

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
                Set(settingsMaster, Percent(editing.masterVolume));
                Set(settingsMusic, Percent(editing.musicVolume));
                Set(settingsSfx, Percent(editing.sfxVolume));
                if (settingsLook != null)
                {
                    Set(settingsLook, editing.lookSensitivity.ToString("0.00", CultureInfo.InvariantCulture) + "x");
                    Set(settingsInvert, editing.invertLook ? "On" : "Off");
                    Set(settingsFov, editing.fieldOfView.ToString("0", CultureInfo.InvariantCulture) + "°");
                    Set(settingsHeroNight, editing.heroViewAtNight ? "On" : "Off");
                }
            }
        }

        float topPanelsBottom;
        bool? topRightNight;

        /// <summary>
        /// At Night the top-right panel keeps only Reset Camera, Menu and Help: Restart (also in the Menu), the seed and
        /// Copy seed are Day matters.
        /// </summary>
        void LayoutTopRight(bool night)
        {
            if (topRightNight == night) return;
            topRightNight = night;
            if (MenuRestartButton.gameObject.activeSelf == night) MenuRestartButton.gameObject.SetActive(!night);
            if (seedText.gameObject.activeSelf == night) seedText.gameObject.SetActive(!night);
            if (copySeedButton.activeSelf == night) copySeedButton.SetActive(!night);
            var shift = night ? 124f : 0f;
            ((RectTransform)ResetCameraButton.transform).anchoredPosition = new Vector2(136 - shift, -12);
            ((RectTransform)RunMenuButton.transform).anchoredPosition = new Vector2(284 - shift, -12);
            ((RectTransform)HelpButton.transform).anchoredPosition = new Vector2(392 - shift, -12);
            topMenuRect.sizeDelta = new Vector2(500 - shift, night ? 76 : 112);
        }

        /// <summary>
        /// Sizes the status panel to its text (a four-entry preview with a boss line wraps to four lines), then stacks
        /// the boss bar / announcement and the Golem banner beneath it, and grows the resources panel to fit its perks.
        /// </summary>
        void LayoutTop()
        {
            var infoH = Mathf.Max(28f, Mathf.Ceil(infoText.preferredHeight));
            infoText.rectTransform.sizeDelta = new Vector2(840, infoH);
            var routeY = -56f - infoH - 6f;
            routeText.rectTransform.anchoredPosition = new Vector2(0, routeY);
            var routeH = string.IsNullOrEmpty(routeText.text) ? 0f : Mathf.Ceil(routeText.preferredHeight);
            routeText.rectTransform.sizeDelta = new Vector2(840, Mathf.Max(1f, routeH));
            var statusH = Mathf.Max(112f, -routeY + routeH + 10f);
            // The boss line and bar sit inside the status panel, under its text.
            ((RectTransform)bossPanel.transform).anchoredPosition = new Vector2(0, -(24f + statusH - 8f));
            if (bossPanel.activeSelf) statusH += 50f;
            statusRect.sizeDelta = new Vector2(860, statusH);

            var y = -(24f + statusRect.sizeDelta.y + 10f);
            ((RectTransform)announcePanel.transform).anchoredPosition = new Vector2(0, y);
            var bottom = -y - 10f;
            if (announcePanel.activeSelf) bottom = Mathf.Max(bottom, -y + ((RectTransform)announcePanel.transform).sizeDelta.y);
            ((RectTransform)golemBanner.transform).anchoredPosition = new Vector2(0, -(bottom + 12f));
            if (golemBanner.activeSelf) bottom += 12f + 58f;
            if (guidePanel.activeSelf) bottom = Mathf.Max(bottom, 146f + ((RectTransform)guidePanel.transform).sizeDelta.y);
            topPanelsBottom = bottom;

            var incomeH = Mathf.Max(24f, Mathf.Ceil(incomeText.preferredHeight));
            incomeText.rectTransform.sizeDelta = new Vector2(350, incomeH);
            var noIncome = string.IsNullOrEmpty(incomeText.text);
            var perksY = noIncome ? -122f : -122f - incomeH - 4f;
            perksText.rectTransform.anchoredPosition = new Vector2(20, perksY);
            var perksH = string.IsNullOrEmpty(perksText.text) ? 0f : Mathf.Ceil(perksText.preferredHeight);
            perksText.rectTransform.sizeDelta = new Vector2(350, Mathf.Max(1f, perksH));
            resourcesRect.sizeDelta = new Vector2(380, Mathf.Max(noIncome ? 120f : 196f, -perksY + perksH + 12f));
        }

        /// <summary>Stacks the visible menu rows under the title, then the status text.</summary>
        void LayoutMenu()
        {
            var y = -214f;
            foreach (var (go, height) in menuStack)
            {
                if (!go.activeSelf) continue;
                ((RectTransform)go.transform).anchoredPosition = new Vector2(0, y);
                y -= height + 12f;
            }

            menuStatus.rectTransform.anchoredPosition = new Vector2(0, y - 6f);
        }
    }
}
