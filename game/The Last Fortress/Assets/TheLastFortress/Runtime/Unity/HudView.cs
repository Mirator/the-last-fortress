using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastFortress
{
    /// <summary>uGUI HUD built in code (legacy UI.Text with the built-in font; no TMP resources required).</summary>
    public sealed partial class HudView : MonoBehaviour
    {
        PrototypeBootstrap game;
        Font font, headingFont;
        RectTransform canvasRect;

        sealed class BuildCard
        {
            public BuildingKind Kind;
            public Button Button;
            public Image Image;
            public Text Cost;
            public Text Effect;
        }

        Text goldText, keepText, incomeText, stateText, infoText, routeText, tooltipText, breachText, selectionTitle, selectionBody,
            terminalTitle, terminalBody, diagnosticText, primaryLabel, stressText, repairLabel, upgradeLabel;
        RectTransform keepFill, tooltipRect, breachRect, statusRect, resourcesRect, topMenuRect;
        GameObject selectionPanel, tooltipPanel, breachLabel, terminalOverlay, stressBanner, diagnosticPanel, rewardOverlay, bossPanel;
        Image primaryImage;
        readonly List<BuildCard> cards = new List<BuildCard>();
        readonly Button[] rewardButtons = new Button[PerkRules.OfferSize];
        readonly Text[] rewardTitles = new Text[PerkRules.OfferSize];
        readonly Text[] rewardBodies = new Text[PerkRules.OfferSize];
        Text rewardHeader, rewardRecap, perksText, bossText;
        RectTransform bossFill;
        string shownOfferId;
        string[] shownPerks;

        public GameObject RewardOverlay => rewardOverlay;
        public Button RewardButton(int i) => rewardButtons[i];
        public string RewardCardText(int i) => rewardTitles[i].text + "\n" + rewardBodies[i].text;
        public string RewardHeaderText => rewardHeader.text;
        public string RewardRecapText => rewardRecap.text;
        public GameObject BossPanel => bossPanel;

        public Button WallCard => CardFor(BuildingKind.Wall);
        public Button TowerCard => CardFor(BuildingKind.Tower);
        public Button RepairButton { get; private set; }
        public string InfoText => infoText.text;
        public string RouteText => routeText.text;
        public string IncomeText => incomeText.text;
        public string SelectionText => selectionPanel.activeSelf ? selectionBody.text : null;

        public Button CardFor(BuildingKind kind)
        {
            foreach (var c in cards)
                if (c.Kind == kind) return c.Button;
            return null;
        }
        public Button PrimaryButton { get; private set; }

        /// <summary>Speed row above the primary action (MVP/release): index 0 pauses, 1..3 pick 1×, 2×, 3×.</summary>
        public Button SpeedButton(int i) => speedButtons[i];

        readonly Button[] speedButtons = new Button[4];

        /// <summary>Spec 012: "Call early +N" above the speed buttons while a call is possible.</summary>
        public Button CallEarlyButton { get; private set; }

        Text callEarlyLabel;
        readonly Text[] speedLabels = new Text[4];
        public Button MenuRestartButton { get; private set; }
        public Button ResetCameraButton { get; private set; }
        public Button SellButton { get; private set; }

        /// <summary>Spec 015: one tier up for the selection (U); disabled with its reason by Night, at tier III or without the gold.</summary>
        public Button UpgradeButton { get; private set; }

        public string UpgradeLabel => upgradeLabel != null ? upgradeLabel.text : "";
        public Button TerminalRestartButton { get; private set; }
        public GameObject TerminalOverlay => terminalOverlay;
        public string TerminalTitle => terminalTitle.text;
        public GameObject StressBanner => stressBanner;
        public GameObject BreachLabel => breachLabel;
        public string StateText => stateText.text;
        public string TooltipText => tooltipPanel.activeSelf ? tooltipText.text : null;
        public string PrimaryButtonLabel => primaryLabel.text;
        public RectTransform CanvasRect => canvasRect;
        public Canvas Canvas => canvasRect.GetComponent<Canvas>();

        static readonly Color CardIdle = Palette.HudSurface.WithAlpha(0.94f);
        static readonly Color CardActive = Palette.Hex(0x35525C);

        public void Build(PrototypeBootstrap game)
        {
            this.game = game;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            headingFont = HeadingFont() ?? font;
            if (panelSprite == null) panelSprite = SlicedSprite("HudPanel", 0.86f, 0.72f, 0.95f);
            if (buttonSprite == null) buttonSprite = SlicedSprite("HudButton", 1f, 0.8f, 1f);

            var canvasGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = canvasGo.GetComponent<RectTransform>();
            var root = canvasRect;

            // Top-left: gold and Keep health
            var resources = Panel(root, "Resources", new Vector2(0, 1), new Vector2(24, -24), new Vector2(380, game.IsMvp ? 196 : 132));
            resourcesRect = resources;
            if (game.Content.goldIcon != null) Icon(resources, game.Content.goldIcon, new Vector2(18, -20), 32f, Palette.Amber);
            else Swatch(resources, new Vector2(20, -22), Palette.Amber, true);
            goldText = Label(resources, "Gold", 28, TextAnchor.UpperLeft, new Vector2(62, -14), new Vector2(300, 40), Palette.HudText);
            Swatch(resources, new Vector2(20, -70), Palette.WarningCoral, false);
            keepText = Label(resources, "Keep", 24, TextAnchor.UpperLeft, new Vector2(62, -60), new Vector2(300, 36), Palette.HudText);
            var barBack = Rect(resources, "KeepBar", new Vector2(0, 1), new Vector2(62, -100), new Vector2(296, 14));
            barBack.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
            keepFill = Rect(barBack, "Fill", Vector2.zero, Vector2.zero, Vector2.zero);
            keepFill.anchorMin = Vector2.zero;
            keepFill.anchorMax = Vector2.one;
            keepFill.pivot = new Vector2(0, 0.5f);
            keepFill.offsetMin = keepFill.offsetMax = Vector2.zero;
            keepFill.gameObject.AddComponent<Image>().color = Palette.WarningCoral;
            incomeText = Label(resources, "Income", 21, TextAnchor.UpperLeft, new Vector2(20, -122), new Vector2(350, 50), Palette.HudText);

            // Top-center: phase and enemies
            var mvp = game.IsMvp;
            var status = Panel(root, "Status", new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(mvp ? 860 : 760, mvp ? 158 : 150));
            statusRect = status;
            stateText = Label(status, "State", 34, TextAnchor.UpperCenter, new Vector2(0, -10), new Vector2(840, 44), Palette.Amber, true);
            infoText = Label(status, "Info", mvp ? 22 : 21, TextAnchor.UpperCenter, new Vector2(0, -56), new Vector2(840, mvp ? 58 : 32), Palette.HudText, true);
            routeText = Label(status, "Route", 21, TextAnchor.UpperCenter, new Vector2(0, mvp ? -116 : -98), new Vector2(840, 36), Palette.RouteCyan, true);
            perksText = Label(resources, "Perks", 18, TextAnchor.UpperLeft, new Vector2(20, -170), new Vector2(350, 24), Palette.Amber);

            // Boss health (only while a Siege Golem is alive)
            bossPanel = Panel(root, "BossBar", new Vector2(0.5f, 1), new Vector2(0, mvp ? -192 : -186), new Vector2(620, 54)).gameObject;
            bossText = Label(bossPanel.transform, "Text", 22, TextAnchor.UpperCenter, new Vector2(0, -4), new Vector2(600, 28), Palette.WarningCoral, true);
            var bossBack = Rect(bossPanel.transform, "Bar", new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(580, 12));
            bossBack.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
            bossFill = Rect(bossBack, "Fill", Vector2.zero, Vector2.zero, Vector2.zero);
            bossFill.anchorMin = Vector2.zero;
            bossFill.anchorMax = Vector2.one;
            bossFill.offsetMin = bossFill.offsetMax = Vector2.zero;
            bossFill.gameObject.AddComponent<Image>().color = Palette.WarningCoral;
            if (mvp)
            {
                // The MVP draws the boss line inside the status panel (LayoutTop grows it), so no panel of its own.
                bossPanel.GetComponent<Image>().enabled = false;
                bossPanel.transform.Find("Accent").gameObject.SetActive(false);
            }

            bossPanel.SetActive(false);

            // Top-right: menu
            var mvpMenu = game.IsMvp;
            var menu = Panel(root, "Menu", new Vector2(1, 1), new Vector2(-24, -24), new Vector2(mvpMenu ? 500 : 430, mvpMenu ? 112 : 76));
            topMenuRect = menu;
            MenuRestartButton = MakeButton(menu, "Restart", new Vector2(0, 1), new Vector2(12, -12), new Vector2(mvpMenu ? 116 : 196, 52), game.RequestRestart, out var restartTop);
            ResetCameraButton = MakeButton(menu, "Reset Camera", new Vector2(0, 1), new Vector2(mvpMenu ? 136 : 220, -12), new Vector2(mvpMenu ? 140 : 198, 52), game.ResetCamera, out var resetTop);
            if (mvpMenu) restartTop.fontSize = resetTop.fontSize = 21;
            if (game.IsMvp) BuildRunMenuRow(menu);

            // Bottom-center: build cards (eight kinds wrap into two rows of four, release-rules-4's nine with the Mine into
            // two narrower rows of five; row 0 is the lower one).
            var kinds = game.PurchasableKinds;
            var cardWidth = kinds.Count > 8 ? 178f : kinds.Count > 2 ? 204f : 240f;
            var gap = kinds.Count > 2 ? 10f : 20f;
            var perRow = kinds.Count > 8 ? 5 : kinds.Count > 5 ? 4 : kinds.Count;
            var rows = (kinds.Count + perRow - 1) / perRow;
            cardBarTop = 24f + rows * 120f + (rows - 1) * gap + 8f;
            var cardRow = Rect(root, "BuildCards", new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(perRow * (cardWidth + gap) - gap, rows * 120 + (rows - 1) * gap));
            cardGroup = cardRow.gameObject.AddComponent<CanvasGroup>();
            for (var i = 0; i < kinds.Count; i++)
            {
                var kind = kinds[i];
                var def = game.Definitions.For(kind);
                var buildCard = new BuildCard { Kind = kind };
                var row = rows - 1 - i / perRow; // the first row on top, read left to right like the hotkeys
                buildCard.Button = Card(cardRow, def.DisplayName, def.Cost, (i + 1).ToString(), new Vector2(i % perRow * (cardWidth + gap), row * (120 + gap)), cardWidth,
                    () => game.Player.SetTool(kind), out buildCard.Image, out buildCard.Cost, game.IsMvp ? CardEffect(def) : null, game.Content.IconFor(kind));
                if (game.IsMvp) buildCard.Effect = buildCard.Button.transform.Find("Effect").GetComponent<Text>();
                cards.Add(buildCard);
            }

            // Bottom-right: primary action
            var actions = Rect(root, "Actions", new Vector2(1, 0), new Vector2(-24, 24), new Vector2(300, 100));
            PrimaryButton = MakeButton(actions, "START NIGHT", new Vector2(0, 0), Vector2.zero, new Vector2(300, 100), game.PrimaryAction, out primaryLabel);
            primaryImage = PrimaryButton.GetComponent<Image>();
            primaryLabel.fontSize = 30;
            if (game.IsMvp)
            {
                var speed = Rect(root, "Speed", new Vector2(1, 0), new Vector2(-24, 136), new Vector2(300, 56));
                for (var i = 0; i < speedButtons.Length; i++)
                {
                    var index = i;
                    speedButtons[i] = MakeButton(speed, i == 0 ? "II" : $"{PrototypeBootstrap.GameSpeeds[i - 1]}×", Vector2.zero, new Vector2(i * 77f, 0), new Vector2(69, 56),
                        () => { if (index == 0) game.Sim.SetPaused(!game.Sim.Paused); else game.SetGameSpeed(PrototypeBootstrap.GameSpeeds[index - 1]); }, out speedLabels[i]);
                }

                // Night only, so it sits on the speed row's line, left of it (the build cards are hidden at Night).
                CallEarlyButton = MakeButton(root, "CALL EARLY", new Vector2(1, 0), new Vector2(-336, 24), new Vector2(300, 56), () => game.CallEarly(), out callEarlyLabel);
                callEarlyLabel.fontSize = 22;
                CallEarlyButton.gameObject.SetActive(false);
            }

            // Bottom-left: selection
            selectionPanel = Panel(root, "Selection", new Vector2(0, 0), new Vector2(24, 24), new Vector2(380, game.IsMvp ? 248 : 190)).gameObject;
            selectionTitle = Label(selectionPanel.transform, "Name", 26, TextAnchor.UpperLeft, new Vector2(18, -12), new Vector2(350, 36), Palette.Amber);
            selectionBody = Label(selectionPanel.transform, "Body", 21, TextAnchor.UpperLeft, new Vector2(18, -52), new Vector2(350, 70), Palette.HudText);
            SellButton = MakeButton(selectionPanel.transform, "Sell", new Vector2(0, 1), new Vector2(18, -124), new Vector2(160, 52), game.SellSelected, out _);
            RepairButton = MakeButton(selectionPanel.transform, "Repair", new Vector2(0, 1), new Vector2(190, -124), new Vector2(172, 52), game.RepairSelected, out repairLabel);
            repairLabel.fontSize = 20;
            RepairButton.gameObject.SetActive(false);
            if (game.IsMvp)
            {
                RotateButton = MakeButton(selectionPanel.transform, "Rotate (R)", new Vector2(0, 1), new Vector2(18, -184), new Vector2(160, 50), game.RotateSelected, out _);
                RotateButton.gameObject.SetActive(false);
                UpgradeButton = MakeButton(selectionPanel.transform, "Upgrade (U)", new Vector2(0, 1), new Vector2(190, -184), new Vector2(172, 50), game.UpgradeSelected, out upgradeLabel);
                upgradeLabel.fontSize = 20;
                UpgradeButton.gameObject.SetActive(false);
            }
            selectionPanel.SetActive(false);

            // Controls hint
            Label(root, "Hint", 18, TextAnchor.LowerLeft, new Vector2(24, 230), new Vector2(1100, 30), Palette.HudText.WithAlpha(0.8f))
                .text = (game.IsMvp ? "1 Wall · 2 Tower · 3 House · 4 Farm · 5 Trap" : "1 Wall · 2 Tower")
                        + " · LMB place/select · RMB/Esc cancel · WASD/arrows pan · wheel zoom · Space pause";
            var hintRect = root.Find("Hint") as RectTransform;
            hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = Vector2.zero;
            hintRect.anchoredPosition = new Vector2(24, 226);
            if (game.IsMvp) hintRect.gameObject.SetActive(false); // the MVP lists controls in its menu instead

            // Cursor tooltip and breach label (never block pointer)
            var tipWidth = game.IsMvp ? TooltipWidth : 330f;
            tooltipPanel = Panel(root, "Tooltip", new Vector2(0, 0), Vector2.zero, new Vector2(tipWidth, 74)).gameObject;
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();
            tooltipRect.pivot = new Vector2(0, 1);
            tooltipPanel.GetComponent<Image>().raycastTarget = false;
            tooltipText = Label(tooltipPanel.transform, "Text", 21, game.IsMvp ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, new Vector2(12, game.IsMvp ? -8 : -6), new Vector2(tipWidth - 20, 62), Palette.HudText);
            tooltipPanel.SetActive(false);

            breachLabel = Panel(root, "BreachLabel", new Vector2(0, 0), Vector2.zero, new Vector2(150, 40)).gameObject;
            breachRect = breachLabel.GetComponent<RectTransform>();
            breachRect.pivot = new Vector2(0.5f, 0);
            var breachImage = breachLabel.GetComponent<Image>();
            breachImage.color = Palette.WarningCoral;
            breachImage.raycastTarget = false;
            breachText = Label(breachLabel.transform, "Text", 24, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(150, 40), Palette.HudSurface, true);
            breachText.text = "BREACH";
            breachText.fontStyle = FontStyle.Bold;
            breachLabel.SetActive(false);

            raidTagRoot = root;

            // Stress banner
            stressBanner = Panel(root, "StressBanner", new Vector2(0.5f, 1), new Vector2(0, -186), new Vector2(900, 56)).gameObject;
            stressBanner.GetComponent<Image>().color = Palette.WarningCoral;
            stressText = Label(stressBanner.transform, "Text", 26, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(900, 56), Palette.HudSurface, true);
            stressText.fontStyle = FontStyle.Bold;
            stressText.text = "STRESS FIXTURE — DEBUG MODE, NOT THE MAIN WAVE";
            stressBanner.SetActive(false);

            diagnosticPanel = Panel(root, "Diagnostic", new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(900, 80)).gameObject;
            diagnosticPanel.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            diagnosticText = Label(diagnosticPanel.transform, "Text", 22, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(880, 80), Palette.WarningCoral, true);
            diagnosticPanel.SetActive(false);

            // Terminal overlay (blocks the world)
            var overlay = Rect(root, "TerminalOverlay", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            overlay.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.09f, 0.6f);
            terminalOverlay = overlay.gameObject;
            var card = Panel(overlay, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 320));
            card.pivot = new Vector2(0.5f, 0.5f);
            terminalTitle = Label(card, "Title", 64, TextAnchor.UpperCenter, new Vector2(0, -28), new Vector2(620, 80), Palette.Amber, true);
            terminalTitle.fontStyle = FontStyle.Bold;
            terminalBody = Label(card, "Body", 24, TextAnchor.UpperCenter, new Vector2(0, -118), new Vector2(600, 80), Palette.HudText, true);
            TerminalRestartButton = MakeButton(card, "Restart", new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(260, 70), game.Restart, out var restartLabel);
            restartLabel.fontSize = 28;
            terminalOverlay.SetActive(false);

            // Reward choice (blocks the world; combat and building are stopped in Reward)
            var reward = Rect(root, "RewardOverlay", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            reward.anchorMin = Vector2.zero;
            reward.anchorMax = Vector2.one;
            reward.offsetMin = reward.offsetMax = Vector2.zero;
            reward.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.09f, 0.55f);
            rewardOverlay = reward.gameObject;
            rewardHeader = Label(reward, "Header", 40, TextAnchor.MiddleCenter, new Vector2(0, -250), new Vector2(1200, 110), Palette.Amber, true);
            rewardRecap = Label(reward, "Recap", 24, TextAnchor.MiddleCenter, new Vector2(0, -362), new Vector2(1200, 36), Palette.HudText, true);
            for (var i = 0; i < PerkRules.OfferSize; i++)
            {
                var index = i;
                var rt = Rect(reward, $"RewardCard{i}", new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 420, -30), new Vector2(390, 330));
                rt.pivot = new Vector2(0.5f, 0.5f);
                var img = rt.gameObject.AddComponent<Image>();
                img.color = Palette.HudSurface.WithAlpha(0.97f);
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = img;
                var colors = button.colors;
                colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
                // During the 0.8 s reward lockout the card dims but stays opaque (Unity's default halves its alpha, so the
                // board showed through the text).
                colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                button.colors = colors;
                button.onClick.AddListener(() => Choose(index));
                rewardTitles[i] = Label(rt, "Title", 30, TextAnchor.UpperCenter, new Vector2(0, -24), new Vector2(360, 44), Palette.Amber, true);
                rewardBodies[i] = Label(rt, "Body", 22, TextAnchor.UpperCenter, new Vector2(0, -84), new Vector2(350, 230), Palette.HudText, true);
                rewardButtons[i] = button;
            }

            rewardOverlay.SetActive(false);
            if (game.IsMvp) BuildMvpPanels(root);
        }

        /// <summary>
        /// MVP: every in-run panel moves under one layer that the menus hide as a whole, so the title screen shows only
        /// the world and its menu. Order (and so layering) is preserved; dialogs, save failure and menus stay on the root.
        /// </summary>
        RectTransform BuildGameplayLayer(RectTransform root, params string[] names)
        {
            var layer = FullScreenRect(root, "Gameplay");
            layer.SetAsFirstSibling();
            foreach (var n in names)
            {
                var child = root.Find(n);
                if (child != null) child.SetParent(layer, false);
            }

            return layer;
        }


        /// <summary>
        /// The reward cards ignore pointer clicks for a moment after they appear and until the button that was held when they
        /// appeared is released, so an attack click at the end of a fight never picks a perk (owner, 2026-10-21).
        /// </summary>
        public const float RewardLockSeconds = 0.8f;

        string rewardLockFor;
        float rewardShownAt;
        bool rewardReleased;

        public bool RewardLocked => rewardLockFor != null && (Time.unscaledTime < rewardShownAt + RewardLockSeconds || !rewardReleased);

        void Choose(int index)
        {
            if (shownOfferId == null || shownPerks == null || index >= shownPerks.Length) return;
            game.ChoosePerk(shownOfferId, shownPerks[index]); // stale or repeated clicks are rejected by the simulation
        }

        void RefreshReward(RunSimulation sim)
        {
            var offer = sim.State == RunState.Reward ? sim.CurrentOffer : null;
            if (rewardOverlay.activeSelf != (offer != null)) rewardOverlay.SetActive(offer != null);
            if (offer == null)
            {
                shownOfferId = null;
                shownPerks = null;
                return;
            }

            if (rewardLockFor != offer.OfferId)
            {
                rewardLockFor = offer.OfferId;
                rewardShownAt = Time.unscaledTime;
                rewardReleased = false;
            }

            var mouse = game.Player.Mouse;
            if (mouse == null || !mouse.leftButton.isPressed) rewardReleased = true;
            shownOfferId = offer.OfferId;
            shownPerks = offer.PerkIds;
            Set(rewardRecap, sim.LastNight != null && sim.LastNight.Night == offer.ClearedNight ? $"Night {offer.ClearedNight}: {RecapLine(sim.LastNight)}" : "");
            Set(rewardHeader, $"Night {offer.ClearedNight} survived — choose one reward\nCurrent forecast for dawn of Day {offer.ClearedNight + 1}: +{sim.IncomeForecast().Total} gold");
            for (var i = 0; i < rewardButtons.Length; i++)
            {
                var has = i < offer.PerkIds.Length;
                if (rewardButtons[i].gameObject.activeSelf != has) rewardButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                var id = offer.PerkIds[i];
                Set(rewardTitles[i], PerkRules.Title(id));
                Set(rewardBodies[i], PerkRules.Effect(id, sim.Definitions.House?.LandIncome > 0) + "\n\n" + Relevance(sim, id) + Condition(sim, id) + IncomePreview(sim, id));
            }
        }

        /// <summary>For income-changing rewards: the actual next payment with this choice (read-only preview).</summary>
        static string IncomePreview(RunSimulation sim, string id)
        {
            var now = sim.IncomeForecast().Total;
            var with = sim.IncomeForecastWith(id).Total;
            return with == now ? "" : $"\nNext dawn: +{now} → +{with} gold";
        }

        /// <summary>Effective direct damage/interval/range; burn and Last Stand state named separately.</summary>
        static string TowerDetails(RunSimulation sim, Building tower)
        {
            var damage = sim.TowerDamage(tower);
            var baseDamage = RunSimulation.BaseDamage(tower);
            var range = sim.TowerRange(tower);
            var text = $"Damage {damage}{(damage != baseDamage ? $" (base {baseDamage})" : "")} every {RunSimulation.AttackIntervalSeconds(tower).ToString("0.##", CultureInfo.InvariantCulture)} s · range {range.ToString("0.0#", CultureInfo.InvariantCulture)}";
            var watchtower = sim.BuffingWatchtowerTier(tower);
            if (watchtower > 0) text += " (Watchtower +" + sim.WatchtowerRangeBonus(watchtower).ToString("0.#", CultureInfo.InvariantCulture) + ")";
            if (tower.Kind == BuildingKind.Cannon)
                return text + $"\nHits every enemy within {sim.SplashRadius(tower).ToString("0.0#", CultureInfo.InvariantCulture)} tiles of its target";
            if (sim.HasPerk(PerkRules.LastStand)) text += sim.LastStandActive ? " · Last Stand active" : " · Last Stand below 30% Keep";
            if (sim.HasPerk(PerkRules.FlamingArrows)) text += $"\n+ burn {PerkRules.BurnDamage}/s for 3 s";
            return text;
        }

        static string WatchtowerDetails(RunSimulation sim, Building watchtower)
        {
            var n = sim.TowersBuffedBy(watchtower.Cell, watchtower.Tier).Count;
            var reachTiles = sim.WatchtowerReach(watchtower);
            var reach = reachTiles > 0 ? $"within {reachTiles} tiles" : "touching it";
            var spotter = sim.SpotterPercent(watchtower.Tier);
            return $"+{sim.WatchtowerRangeBonus(watchtower.Tier).ToString("0.#", CultureInfo.InvariantCulture)} range{(spotter > 0 ? $" and +{spotter}% damage" : "")} to Archer Towers and Cannons {reach} (never stacks) · boosting {n}";
        }

        static string RepairStationDetails(RunSimulation sim, Building station)
        {
            var keep = sim.RepairsKeep(station) ? ", the Keep too" : "";
            return $"At dawn restores up to {sim.RepairBudget(station)} HP to damaged buildings within {RunSimulation.RepairReach(station)} tiles{keep}, weakest first";
        }

        /// <summary>Spec 015: what a tier gives against tier I (its stats and trait), for the selection panel.</summary>
        static string TierText(BuildingDefinition def, int tier)
        {
            var t = BuildingUpgrades.For(def, tier);
            var inv = CultureInfo.InvariantCulture;
            var parts = new List<string>();
            if (t.HpPercent > 0) parts.Add($"HP {def.MaxHp * (100 + t.HpPercent) / 100}");
            if (t.DamagePercent > 0) parts.Add($"damage {def.AttackDamage * (100 + t.DamagePercent) / 100}");
            if (t.IntervalSeconds > 0) parts.Add($"every {t.IntervalSeconds.ToString("0.##", inv)} s");
            if (t.RangeBonus > 0) parts.Add($"range +{t.RangeBonus.ToString("0.##", inv)}");
            if (t.SplashBonus > 0) parts.Add($"splash +{t.SplashBonus.ToString("0.##", inv)}");
            if (t.IncomeBonus > 0) parts.Add($"+{t.IncomeBonus} gold/day");
            if (t.TrapDamage > 0) parts.Add($"{t.TrapDamage} damage");
            if (t.Reflect > 0) parts.Add($"spikes reflect {t.Reflect}");
            if (t.Stone) parts.Add("stone: no double damage from Brutes, Bombers or bosses");
            if (t.SecondArrowPercent > 0) parts.Add(t.SecondArrowPercent >= 100 ? "second arrow at full damage" : $"second arrow at {t.SecondArrowPercent}%");
            if (t.SlowPercent > 0) parts.Add($"slows {t.SlowPercent}% for {t.SlowSeconds.ToString("0.#", inv)} s");
            if (t.MarketHallBonus > 0) parts.Add($"Market hall: touching Farms +{t.MarketHallBonus}");
            if (t.Granary) parts.Add("Granary: half its income if destroyed");
            if (t.Stockade) parts.Add("Stockade: raids do half damage");
            if (t.WatchRangeBonus > 0) parts.Add($"+{t.WatchRangeBonus.ToString("0.#", inv)} range");
            if (t.SpotterPercent > 0) parts.Add($"+{t.SpotterPercent}% damage");
            if (t.WatchReach > 0) parts.Add($"reach {t.WatchReach} tiles");
            if (t.RepairBudget > 0) parts.Add($"{t.RepairBudget} HP at dawn");
            if (t.RepairReach > 0) parts.Add($"within {t.RepairReach} tiles");
            if (t.KeepCare) parts.Add("the Keep too");
            return string.Join(", ", parts);
        }

        /// <summary>A House/Farm's own effective daily income with the reasons it differs from base.</summary>
        static string IncomeDetails(RunSimulation sim, Building b)
        {
            var text = $"Income +{sim.BuildingIncome(b)}/day";
            if (b.Definition.LandIncome > 0)
            {
                var fields = EconomyRules.Fields(sim.Grid, b);
                text += $" · {fields} field{(fields == 1 ? "" : "s")}"; // spec 017 D4: the free fertile cells it counts
            }

            if (b.Kind == BuildingKind.Farm && EconomyRules.FarmHasAdjacentHouse(sim.Grid, b)) text += " · House link";
            if (b.Kind == BuildingKind.Farm && sim.HasPerk(PerkRules.EfficientFarming)) text += " · Efficient Farming";
            return text;
        }

        /// <summary>Conditional perks: whether the condition holds now (never a prediction of later nights).</summary>
        static string Condition(RunSimulation sim, string id)
        {
            if (id != PerkRules.LastStand) return "";
            var pct = sim.Keep.MaxHp > 0 ? sim.Keep.Hp * 100 / sim.Keep.MaxHp : 0;
            return pct < PerkRules.LastStandThresholdPercent ? "\nActive now: Keep at " + pct + "%." : $"\nNot active now: Keep at {pct}% (needs below {PerkRules.LastStandThresholdPercent}%).";
        }

        static string Relevance(RunSimulation sim, string id)
        {
            var kind = PerkRules.AffectedKind(id);
            if (kind == null) return "Applies to your whole fortress.";
            var n = 0;
            foreach (var b in sim.Grid.AllBuildings())
                if (b.Kind == kind.Value && b.IsAlive) n++;
            var name = sim.Definitions.For(kind.Value).DisplayName;
            return n > 0 ? $"You own {n} {name}{(n == 1 ? "" : "s")}." : $"You own no {name} yet (future ones benefit).";
        }

        // ---------- refresh ----------

        /// <summary>Shown by Day (to preset the night's speed) and Night; the lit button is the one in effect.</summary>
        void RefreshSpeed(RunSimulation sim)
        {
            if (speedButtons[0] == null) return;
            if (CallEarlyButton != null)
            {
                var call = sim.CanCallEarly && !game.TitleScreen && !game.MenuOpen;
                if (CallEarlyButton.gameObject.activeSelf != call) CallEarlyButton.gameObject.SetActive(call);
                if (call) Set(callEarlyLabel, $"CALL EARLY  +{sim.EarlyCallGoldNow} gold  (C)");
            }

            var row = speedButtons[0].transform.parent.gameObject;
            var show = (sim.State == RunState.Day || sim.State == RunState.Night) && !sim.StressMode && !game.TitleScreen;
            if (row.activeSelf != show) row.SetActive(show);
            if (!show) return;
            // At Night the row takes the PAUSE button's place (II pauses and resumes); by Day it sits above START NIGHT.
            var rowRect = (RectTransform)row.transform;
            var rowY = sim.State == RunState.Night ? 24f : 136f;
            if (rowRect.anchoredPosition.y != rowY) rowRect.anchoredPosition = new Vector2(-24, rowY);
            var paused = sim.State == RunState.Night && sim.Paused;
            speedButtons[0].interactable = sim.State == RunState.Night;
            for (var i = 2; i < speedButtons.Length; i++) speedButtons[i].interactable = !game.HeroViewActive; // 1x only in the hero view
            for (var i = 0; i < speedButtons.Length; i++)
            {
                var lit = i == 0 ? paused : !paused && game.GameSpeed == PrototypeBootstrap.GameSpeeds[i - 1];
                speedButtons[i].image.color = (lit ? Palette.Amber : Palette.Hex(0x35525C)).WithAlpha(speedButtons[i].interactable ? 1f : 0.35f);
                speedLabels[i].color = lit ? Palette.HudSurface : Palette.HudText;
            }
        }

        public void Refresh(RunSimulation sim, PlayerController player, RouteOverlay overlay, CameraRig rig)
        {
            var defs = sim.Definitions;
            Set(goldText, $"Gold  {sim.Gold}");
            Set(keepText, $"Keep  {sim.Keep.Hp} / {sim.Keep.MaxHp}");
            keepFill.anchorMax = new Vector2(Mathf.Clamp01((float)sim.Keep.Hp / sim.Keep.MaxHp), 1);
            // At Night the income forecast steps back (it cannot change until dawn); gold, Keep HP, the night and the threat
            // lines stay.
            Set(incomeText, sim.IsCampaign && sim.State != RunState.Night ? IncomeLine(sim) : "");

            var wave = sim.Wave;
            var campaign = sim.IsCampaign;
            var nightTag = campaign ? $" {sim.NightNumber} of {sim.TotalNights}" : "";
            switch (sim.State)
            {
                case RunState.Day:
                    if (!campaign)
                    {
                        Set(stateText, "DAY — build your defenses");
                        Set(infoText, $"Upcoming: {wave.Spawns.Count} Grunts from the west edge {WavePlan.MainEntry}, one every 1.5 s");
                    }
                    else
                    {
                        Set(stateText, $"DAY{nightTag}");
                        Set(infoText, WaveText.Tonight(wave));
                    }

                    break;
                case RunState.Night:
                    var clock = sim.NightElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture);
                    Set(stateText, sim.Paused ? $"NIGHT{nightTag}  {clock}s — PAUSED" : $"NIGHT{nightTag}  {clock}s{(game.GameSpeed > 1 ? $"  ·  {game.GameSpeed}×" : "")}");
                    Set(infoText, $"Enemies alive {sim.AliveEnemyCount} · still to come {sim.ScheduledRemaining}");
                    break;
                case RunState.Reward:
                    Set(stateText, $"NIGHT {sim.NightNumber} CLEARED");
                    Set(infoText, "Choose one reward to continue. Building is closed until the next Day.");
                    break;
                default:
                    Set(stateText, sim.State == RunState.Victory ? "VICTORY" : "DEFEAT");
                    Set(infoText, $"Enemies alive {sim.AliveEnemyCount} · still to come {sim.ScheduledRemaining}");
                    break;
            }

            if (sim.StressMode) Set(infoText, $"{sim.AliveEnemyCount} enemies active across 4 entries · Keep/Walls invulnerable · {sim.NightElapsedSeconds:0}s");

            // Route summary (Day only)
            var route = overlay.PrimaryRoute;
            var newSides = campaign && sim.State == RunState.Day ? WaveText.NewEntries(sim.Campaign, sim.NightNumber) : null;
            if (route == null) Set(routeText, sim.State == RunState.Night && !campaign ? "Towers fire automatically. Enemies follow open routes first." : "");
            else if (!route.Found) Set(routeText, "NO ROUTE — map invalid");
            else if (newSides != null && overlay.CandidateRoute == null && !route.IsBreach)
            {
                Set(routeText, $"NEW: enemies also attack from {newSides}");
                routeText.color = Palette.WarningCoral;
            }
            else if (!route.IsBreach)
            {
                Set(routeText, campaign ? "" : $"{(overlay.CandidateRoute != null ? "If built: " : "Route: ")}open path, {route.Cells.Count - 1} tiles to the Keep");
                routeText.color = Palette.RouteCyan;
            }
            else
            {
                var blocker = sim.Grid.GetOccupant(route.Blocker);
                var name = blocker != null ? blocker.Definition.DisplayName : player.Tool.HasValue ? defs.For(player.Tool.Value).DisplayName : "structure";
                if (sim.Wave.EntryCells.Count > 1) name += $" (from {WaveText.EntryName(sim.Wave.EntryCells[0])})";
                Set(routeText, $"{(overlay.CandidateRoute != null ? "If built: " : "")}SEALED — enemies will BREACH {name} at {route.Blocker}");
                routeText.color = Palette.WarningCoral;
            }

            // Build cards
            var canBuild = sim.State == RunState.Day && !sim.StressMode;
            // Building is closed at Night, during the reward choice and after the run ends: the cards fade out and click through,
            // so they never cover the fight at the close default zoom (plan 015 review).
            var nightCards = game.IsMvp && (sim.State == RunState.Night || sim.State == RunState.Reward || sim.IsTerminal);
            cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, nightCards ? NightCardAlpha : 1f, Time.unscaledDeltaTime * 4f);
            cardGroup.blocksRaycasts = !nightCards;
            foreach (var card in cards)
            {
                var available = defs.For(card.Kind) != null; // a legacy six-night run has no release kinds
                if (card.Button.gameObject.activeSelf != available) card.Button.gameObject.SetActive(available);
                if (!available) continue;
                card.Button.interactable = canBuild;
                card.Image.color = player.Tool == card.Kind ? CardActive : CardIdle;
                var price = sim.PriceFor(card.Kind);
                Set(card.Cost, $"{price} gold");
                if (card.Effect != null) Set(card.Effect, CardEffect(defs.For(card.Kind), sim));
                card.Cost.color = price <= sim.Gold ? Palette.Amber : Palette.WarningCoral;
            }

            // Primary action
            if (sim.State == RunState.Day)
            {
                Set(primaryLabel, "START NIGHT");
                PrimaryButton.interactable = !sim.StressMode;
                primaryImage.color = Palette.Amber;
                primaryLabel.color = Palette.HudSurface;
            }
            else if (sim.State == RunState.Night)
            {
                Set(primaryLabel, sim.Paused ? "RESUME" : "PAUSE");
                PrimaryButton.interactable = true;
                primaryImage.color = Palette.HudSurface;
                primaryLabel.color = Palette.HudText;
            }
            else
            {
                Set(primaryLabel, sim.State == RunState.Reward ? "CHOOSE REWARD" : "—");
                PrimaryButton.interactable = false;
            }

            // The outcome card has its own actions; at Night the speed row's II is the pause (one row instead of three).
            var showPrimary = !sim.IsTerminal && !(speedButtons[0] != null && sim.State == RunState.Night && !sim.StressMode);
            if (PrimaryButton.gameObject.activeSelf != showPrimary) PrimaryButton.gameObject.SetActive(showPrimary);
            RefreshSpeed(sim);

            // Selection panel
            var sel = player.Selected;
            if (selectionPanel.activeSelf != (sel != null)) selectionPanel.SetActive(sel != null);
            if (sel != null)
            {
                var tiers = campaign && sel.Definition.Tiers != null;
                Set(selectionTitle, tiers ? $"{sel.Definition.DisplayName} {BuildingUpgrades.Numeral(sel.Tier)}" : sel.Definition.DisplayName);
                var body = sel.Definition.HasHp ? $"HP {sel.Hp} / {sel.MaxHp}   at {sel.Cell}" : $"{sim.TrapDamage(sel)} damage on entry · walkable   at {sel.Cell}";
                if (sel.Kind == BuildingKind.Tower || sel.Kind == BuildingKind.Cannon) body += "\n" + TowerDetails(sim, sel);
                if (sel.Kind == BuildingKind.Watchtower) body += "\n" + WatchtowerDetails(sim, sel);
                if (sel.Kind == BuildingKind.RepairStation) body += "\n" + RepairStationDetails(sim, sel);
                if (tiers && sel.Tier > 1) body += $"\nTier {BuildingUpgrades.Numeral(sel.Tier)}: {TierText(sel.Definition, sel.Tier)}";
                if (tiers && sel.Tier < BuildingUpgrades.MaxTier)
                    body += $"\nNext, tier {BuildingUpgrades.Numeral(sel.Tier + 1)} for {sim.UpgradePrice(sel)} gold: {TierText(sel.Definition, sel.Tier + 1)}";
                if (sel.Definition.DailyIncome > 0 && campaign) body += "\n" + IncomeDetails(sim, sel);
                if (sel.Kind == BuildingKind.Wall && sim.WallReflection(sel) > 0) body += $"\nReflects {sim.WallReflection(sel)} per melee hit";
                var day = sim.State == RunState.Day && !sim.StressMode;
                var canSell = day && sel.IsDestructible;
                if (canSell) body += $"\nSell refund: {PlacementService.RefundFor(sel)} gold";
                else if (!sel.IsDestructible) body += "\nThe Keep cannot be sold";
                Set(selectionBody, body);
                if (SellButton.gameObject.activeSelf != canSell) SellButton.gameObject.SetActive(canSell);
                var canRepair = day && campaign && sel.Definition.HasHp;
                if (RepairButton.gameObject.activeSelf != canRepair) RepairButton.gameObject.SetActive(canRepair);
                if (canRepair)
                {
                    sim.RepairQuote(sel, out var restored, out var cost);
                    RepairButton.interactable = restored > 0;
                    Set(repairLabel, sel.Hp >= sel.MaxHp ? "Full health" : restored > 0 ? $"Repair +{restored} HP\nfor {cost} gold" : "No gold");
                }

                var showUpgrade = UpgradeButton != null && tiers && !sim.StressMode;
                if (UpgradeButton != null && UpgradeButton.gameObject.activeSelf != showUpgrade) UpgradeButton.gameObject.SetActive(showUpgrade);
                if (showUpgrade)
                {
                    var check = sim.CheckUpgrade(sel);
                    UpgradeButton.interactable = check == UpgradeRejection.None;
                    Set(upgradeLabel, check == UpgradeRejection.None ? $"Upgrade (U)\n{sim.UpgradePrice(sel)} gold"
                        : check == UpgradeRejection.InsufficientGold ? $"Upgrade {sim.UpgradePrice(sel)} gold\nNot enough gold"
                        : check == UpgradeRejection.NotDay ? "Upgrade\nonly by day" : BuildingUpgrades.Describe(check));
                }

                if (game.IsMvp) LayoutSelection(day && RunSimulation.IsRotatable(sel.Kind), showUpgrade);
            }

            // Ghost tooltip
            var showTip = overlay.GhostVisible && player.Tool.HasValue;
            if (tooltipPanel.activeSelf != showTip) tooltipPanel.SetActive(showTip);
            if (showTip)
            {
                var def = defs.For(player.Tool.Value);
                var size = def.FootprintSize > 1 ? $" ({def.FootprintSize}x{def.FootprintSize})" : "";
                var price = sim.PriceFor(player.Tool.Value);
                var extra = !campaign ? "" : player.Tool == BuildingKind.Wall ? " · Automatic connections" : RunSimulation.IsRotatable(player.Tool.Value) ? " · R rotates" : "";
                var detail = campaign ? "\n" + BuildDetail(sim, def) : "";
                if (campaign && overlay.GhostValid && def.LandIncome > 0 && player.HoverCell.HasValue)
                {
                    // Spec 017 D4: what it would earn here, from the free fertile cells marked under the ghost.
                    var income = sim.PreviewIncome(def.Kind, player.HoverCell.Value, out var fields);
                    detail += $"\nHere: +{income}/day · {fields.Count} field{(fields.Count == 1 ? "" : "s")}";
                }
                Set(tooltipText, overlay.GhostValid
                    ? $"{def.DisplayName}{size} — {price} gold\nOK: can build here{extra}{detail}"
                    : $"{def.DisplayName}{size} — {price} gold\nCan't build: {PlacementText.Describe(overlay.GhostRejection)}");
                tooltipText.color = overlay.GhostValid ? Palette.HudText : Palette.WarningCoral;
                var anchor = player.HoverOverride.HasValue ? (Vector2)ProjectToScreen(Prims.CellCenter(player.HoverOverride.Value)) : player.PointerPosition;
                if (game.IsMvp) PlaceTooltip(ScreenToCanvas(anchor));
                else tooltipRect.anchoredPosition = ScreenToCanvas(anchor) + new Vector2(28, -12);
            }

            // Breach label
            var breach = overlay.BreachMarkerVisible ? ProjectToScreen(overlay.BreachMarkerPosition + Vector3.up * 1.3f) : Vector3.back;
            var showBreach = overlay.BreachMarkerVisible && breach.z > 0f; // never pointing at something behind the camera
            if (breachLabel.activeSelf != showBreach) breachLabel.SetActive(showBreach);
            if (showBreach) breachRect.anchoredPosition = ScreenToCanvas(new Vector2(breach.x, breach.y));
            SyncRaidTags(sim);

            if (stressBanner.activeSelf != sim.StressMode) stressBanner.SetActive(sim.StressMode);

            // Perks and boss
            // At Night the list is planning detail: only its count stays.
            Set(perksText, sim.OwnedPerks.Count == 0 ? "" : sim.State == RunState.Night ? $"Perks: {sim.OwnedPerks.Count} (listed by Day)"
                : "Perks: " + string.Join(", ", PerkTitles(sim)));
            Enemy boss = null;
            foreach (var e in sim.Enemies)
                if (e.Alive && e.Definition.IsBoss) boss = e;
            var showBoss = boss != null && sim.State == RunState.Night;
            if (bossPanel.activeSelf != showBoss) bossPanel.SetActive(showBoss);
            if (showBoss)
            {
                Set(bossText, $"BOSS — {boss.Definition.DisplayName}  {boss.Hp} / {boss.Definition.MaxHp}{(boss.Burning ? "  (burning)" : "")}");
                bossFill.anchorMax = new Vector2(Mathf.Clamp01((float)boss.Hp / boss.Definition.MaxHp), 1);
            }

            RefreshReward(sim);
            if (game.IsMvp) RefreshMvpPanels(sim, player);

            var diag = sim.State == RunState.Day && sim.Diagnostics.Count > 0 ? sim.Diagnostics[sim.Diagnostics.Count - 1] : null;
            if (diagnosticPanel.activeSelf != (diag != null)) diagnosticPanel.SetActive(diag != null);
            if (diag != null) Set(diagnosticText, diag);

            // Terminal overlay
            var terminal = sim.IsTerminal;
            if (terminalOverlay.activeSelf != terminal) terminalOverlay.SetActive(terminal);
            if (terminal)
            {
                var won = sim.State == RunState.Victory;
                Set(terminalTitle, won ? "VICTORY" : "DEFEAT");
                terminalTitle.color = won ? Palette.Amber : Palette.WarningCoral;
                if (campaign) Set(terminalBody, SummaryText(sim));
                else
                    Set(terminalBody, won
                        ? $"All {wave.Spawns.Count} Grunts defeated.\nKeep held with {sim.Keep.Hp} / {defs.Keep.MaxHp} HP after {sim.NightElapsedSeconds:0.0}s."
                        : $"The Keep fell after {sim.NightElapsedSeconds:0.0}s.\n{sim.AliveEnemyCount} Grunts were still attacking.");
            }
        }

        static IEnumerable<string> PerkTitles(RunSimulation sim)
        {
            foreach (var id in sim.OwnedPerks) yield return PerkRules.Title(id);
        }

        static string IncomeLine(RunSimulation sim)
        {
            var received = sim.State == RunState.Day && sim.NightNumber > 1 && sim.LastPaidDay == sim.NightNumber && sim.LastIncome.Total > 0
                ? $"\nReceived +{sim.LastIncome.Total} at dawn{(sim.LastIncome.EarlyCall > 0 ? $" (early call +{sim.LastIncome.EarlyCall})" : "")}" : "";
            if (sim.NightNumber >= sim.TotalNights) return "No income after the last night" + received;
            return $"Income +{sim.IncomeForecast().Total} per day" + received;
        }

        const float TooltipWidth = 390f, TooltipMargin = 8f;

        /// <summary>Canvas height of the build cards' top edge plus a small gap (two rows in the release).</summary>
        float cardBarTop = 24f + 120f + 8f;

        CanvasGroup cardGroup;

        /// <summary>Build-card opacity during Night (MVP scenes).</summary>
        public const float NightCardAlpha = 0f;

        public float CardBarAlpha => cardGroup.alpha;

        /// <summary>
        /// Ghost tooltip details: the numbers a placement decision needs, from effective values. Income buildings say how
        /// many dawns are left to pay them back (no income after the last night).
        /// </summary>
        static string BuildDetail(RunSimulation sim, BuildingDefinition d)
        {
            switch (d.Kind)
            {
                case BuildingKind.Tower:
                    return $"{sim.TowerDamage(d)} damage every {d.AttackIntervalSeconds.ToString("0.##", CultureInfo.InvariantCulture)} s · range {sim.TowerRange(d).ToString("0.0#", CultureInfo.InvariantCulture)}";
                case BuildingKind.SpikeTrap:
                    return $"{d.TrapDamage} damage to each enemy that steps on it · walkable";
                case BuildingKind.Wall:
                    var heavy = sim.Definitions.Brute != null ? sim.Definitions.Brute.WallDamageMultiplier : 1;
                    var bombers = sim.Definitions.Bomber != null ? " · Bombers blow up what blocks them" : "";
                    return heavy > 1 ? $"Seal every route and enemies break through · Brutes/Golems hit Walls {heavy}x{bombers}" : "Seal every route and enemies break through";
                case BuildingKind.Cannon:
                    return $"{d.AttackDamage} damage to every enemy within {d.SplashRadius.ToString("0.0#", CultureInfo.InvariantCulture)} tiles of its target every {d.AttackIntervalSeconds.ToString("0.##", CultureInfo.InvariantCulture)} s · range {d.Range.ToString("0.0#", CultureInfo.InvariantCulture)} · 2x2";
                case BuildingKind.Watchtower:
                    return $"+{d.RangeBonus.ToString("0.#", CultureInfo.InvariantCulture)} range to Archer Towers and Cannons touching it (never stacks)";
                case BuildingKind.RepairStation:
                    return RepairStationDetails(sim, new Building(-1, d, default, 0)) + " · 2x2";
                case BuildingKind.Mine:
                    return "Only on a gold vein · enemies passing beside it stop to raid it";
                case BuildingKind.House:
                case BuildingKind.Farm:
                    var dawns = Mathf.Max(0, sim.TotalNights - sim.NightNumber);
                    var perDay = d.Kind == BuildingKind.Farm && sim.HasPerk(PerkRules.EfficientFarming) ? d.DailyIncome * (100 + PerkRules.EfficientFarmingPercent) / 100 : d.DailyIncome;
                    if (dawns == 0) return "No dawns left: it would never pay";
                    var more = d.LandIncome > 0 ? $" plus {d.LandIncome} a day per free fertile field beside it" : d.Kind == BuildingKind.Farm ? " (more next to a House)" : "";
                    return $"Pays at {dawns} more dawn{(dawns == 1 ? "" : "s")}: +{perDay * dawns} gold{more}";
                default:
                    return "";
            }
        }

        /// <summary>The tooltip's current canvas-local rectangle (x, y = bottom-left).</summary>
        public Rect TooltipCanvasRect
        {
            get
            {
                var p = tooltipRect.anchoredPosition;
                var s = tooltipRect.sizeDelta;
                return new Rect(p.x, p.y - s.y, s.x, s.y);
            }
        }

        public Vector2 CanvasSize => canvasRect.rect.size;

        /// <summary>
        /// Sizes the tooltip to its text, places it below-right of the pointer and flips/clamps it so the whole box
        /// (price, footprint, reason, rotation hint) stays inside the canvas at any edge, pan or zoom.
        /// </summary>
        void PlaceTooltip(Vector2 pointer)
        {
            var textHeight = Mathf.Ceil(tooltipText.preferredHeight);
            tooltipText.rectTransform.sizeDelta = new Vector2(TooltipWidth - 24, textHeight);
            var size = new Vector2(TooltipWidth, textHeight + 16);
            tooltipRect.sizeDelta = size;
            var canvas = canvasRect.rect.size;
            var x = pointer.x + 28f;
            var top = pointer.y - 12f;
            if (x + size.x > canvas.x - TooltipMargin) x = pointer.x - 28f - size.x; // flip left
            var floor = game.IsMvp ? cardBarTop : TooltipMargin; // MVP: stay clear of the build cards
            if (top - size.y < floor) top = pointer.y + 12f + size.y; // flip above
            x = Mathf.Clamp(x, TooltipMargin, Mathf.Max(TooltipMargin, canvas.x - size.x - TooltipMargin));
            top = Mathf.Clamp(top, size.y + TooltipMargin, Mathf.Max(size.y + TooltipMargin, canvas.y - TooltipMargin));
            tooltipRect.anchoredPosition = new Vector2(x, top);
        }

        /// <summary>
        /// Selection panel grows upward to fit its text; action rows sit below the text, so details, refund and repair
        /// quote never overlap the Sell/Repair/Rotate hit targets.
        /// </summary>
        void LayoutSelection(bool rotateRow, bool upgrade)
        {
            var bodyHeight = Mathf.Max(28f, Mathf.Ceil(selectionBody.preferredHeight));
            selectionBody.rectTransform.sizeDelta = new Vector2(350, bodyHeight);
            var rowY = -(52f + bodyHeight + 10f);
            ((RectTransform)SellButton.transform).anchoredPosition = new Vector2(18, rowY);
            ((RectTransform)RepairButton.transform).anchoredPosition = new Vector2(190, rowY);
            var anyAction = SellButton.gameObject.activeSelf || RepairButton.gameObject.activeSelf;
            var height = 52f + bodyHeight + 10f + (anyAction ? 52f + 8f : 0f);
            if (RotateButton != null)
            {
                var rotateY = anyAction ? rowY - 58f : rowY;
                ((RectTransform)RotateButton.transform).anchoredPosition = new Vector2(18, rotateY);
                // Upgrade shares the second row: beside Rotate, or in its place when the building cannot rotate.
                if (UpgradeButton != null) ((RectTransform)UpgradeButton.transform).anchoredPosition = new Vector2(rotateRow ? 190 : 18, rotateY);
                if (rotateRow || upgrade) height = -rotateY + 50f + 8f;
            }

            ((RectTransform)selectionPanel.transform).sizeDelta = new Vector2(380, height + 8f);
        }

        /// <summary>Selection panel and its action buttons (canvas-local), for layout checks.</summary>
        public RectTransform SelectionPanelRect => (RectTransform)selectionPanel.transform;
        public RectTransform SelectionBodyRect => selectionBody.rectTransform;

        Vector2 ScreenToCanvas(Vector2 screen)
        {
            var scale = canvasRect.lossyScale.x;
            return scale > 0 ? screen / scale : screen;
        }

        /// <summary>World point to screen with the camera that is rendering (strategic or hero view); z below 0 is behind it.</summary>
        Transform raidTagRoot;
        readonly List<RectTransform> raidTags = new List<RectTransform>();
        List<Building> raidExposed = new List<Building>();
        (int revision, int night) raidKey = (-1, -1);

        /// <summary>
        /// Spec 016: by Day a small amber RAID tag over each House, Farm or Mine beside one of tonight's routes (a passing
        /// Grunt, Runner or Brute would stop to raid it). Recomputed only when the board or the night changes.
        /// </summary>
        void SyncRaidTags(RunSimulation sim)
        {
            var show = sim.RaidsEnabled && sim.State == RunState.Day && !game.HeroViewActive;
            if (show && raidKey != (sim.Grid.Revision, sim.NightNumber))
            {
                raidKey = (sim.Grid.Revision, sim.NightNumber);
                raidExposed = sim.RaidExposed();
            }

            var shown = 0;
            if (show)
                foreach (var b in raidExposed)
                {
                    if (!b.IsAlive) continue;
                    var p = ProjectToScreen(new Vector3((float)b.CenterX, 1.25f, (float)b.CenterZ));
                    if (p.z <= 0f) continue;
                    if (shown == raidTags.Count)
                    {
                        var tag = Panel(raidTagRoot, "RaidTag", Vector2.zero, Vector2.zero, new Vector2(78, 28));
                        tag.pivot = new Vector2(0.5f, 0);
                        var image = tag.GetComponent<Image>();
                        image.color = Palette.Amber;
                        image.raycastTarget = false;
                        var text = Label(tag, "Text", 18, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(78, 28), Palette.HudSurface, true);
                        text.text = "RAID";
                        text.fontStyle = FontStyle.Bold;
                        tag.SetSiblingIndex(breachLabel.transform.GetSiblingIndex());
                        raidTags.Add(tag);
                    }

                    var t = raidTags[shown++];
                    if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                    t.anchoredPosition = ScreenToCanvas(new Vector2(p.x, p.y));
                }

            for (var i = shown; i < raidTags.Count; i++)
                if (raidTags[i].gameObject.activeSelf) raidTags[i].gameObject.SetActive(false);
        }

        public int VisibleRaidTagCount => raidTags.FindAll(t => t.gameObject.activeSelf).Count;

        Vector3 ProjectToScreen(Vector3 world) => (game != null ? game.ActiveCamera : Camera.main).WorldToScreenPoint(world);

        static void Set(Text t, string value)
        {
            if (t.text != value) t.text = value;
        }

        // ---------- builders ----------

        static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = Rect(parent, name, anchor, position, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Palette.HudSurface.WithAlpha(0.94f);
            Sliced(img, panelSprite);
            // Restrained accent: a thin amber rule inset along the top edge, the one ornament every panel shares.
            var accent = new GameObject("Accent", typeof(RectTransform)).GetComponent<RectTransform>();
            accent.SetParent(rt, false);
            accent.anchorMin = new Vector2(0f, 1f);
            accent.anchorMax = new Vector2(1f, 1f);
            accent.pivot = new Vector2(0.5f, 1f);
            accent.offsetMin = new Vector2(14f, -5f);
            accent.offsetMax = new Vector2(-14f, -3f);
            var line = accent.gameObject.AddComponent<Image>();
            line.color = Palette.Amber.WithAlpha(0.45f);
            line.raycastTarget = false;
            foreach (var end in new[] { 0f, 1f })
            {
                // Small amber diamonds cap the rule: the one heraldic touch, kept tiny so it never competes with content.
                var cap = new GameObject("AccentCap", typeof(RectTransform)).GetComponent<RectTransform>();
                cap.SetParent(accent, false);
                cap.anchorMin = cap.anchorMax = new Vector2(end, 0.5f);
                cap.sizeDelta = new Vector2(6f, 6f);
                cap.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var capImage = cap.gameObject.AddComponent<Image>();
                capImage.color = Palette.Amber.WithAlpha(0.7f);
                capImage.raycastTarget = false;
            }

            return rt;
        }

        /// <summary>HUD surface sprites, generated once (no texture assets): rounded corners, a lit 1 px rim and a soft top-to-bottom shade.</summary>
        static Sprite panelSprite, buttonSprite;

        static void Sliced(Image img, Sprite sprite)
        {
            if (sprite == null) return;
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
        }

        static Sprite SlicedSprite(string name, float top, float bottom, float rim)
        {
            const int size = 48, radius = 9;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // Signed distance to the rounded rectangle (negative inside).
                var qx = Mathf.Max(Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius), 0f);
                var qy = Mathf.Max(Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius), 0f);
                var d = Mathf.Sqrt(qx * qx + qy * qy) - radius;
                var alpha = Mathf.Clamp01(0.5f - d);
                var shade = Mathf.Lerp(bottom, top, y / (size - 1f));
                var edge = Mathf.Clamp01(1.5f + d); // 1 on the outer 1.5 px
                var v = Mathf.Lerp(shade, rim, edge * (y > size / 2 ? 1f : 0.55f)); // the rim is brighter on top, like light from above
                pixels[y * size + x] = new Color(v, v, v, alpha);
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius + 3, radius + 3, radius + 3, radius + 3));
        }

        /// <summary>
        /// Plan 013 typography: large headings (30 px and up) use a book serif already installed with Windows, for a restrained
        /// medieval accent; nothing is shipped or downloaded. Body text keeps the engine's bundled font, so layouts are unchanged.
        /// </summary>
        static Font HeadingFont()
        {
            var installed = new System.Collections.Generic.HashSet<string>(Font.GetOSInstalledFontNames());
            foreach (var name in new[] { "Palatino Linotype", "Book Antiqua", "Georgia" })
                if (installed.Contains(name)) return Font.CreateDynamicFontFromOSFont(name, 32);
            return null;
        }

        Text Label(Transform parent, string name, int size, TextAnchor align, Vector2 position, Vector2 box, Color color, bool centered = false)
        {
            var rt = Rect(parent, name, centered ? new Vector2(0.5f, 1) : new Vector2(0, 1), position, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = size >= 30 ? headingFont : font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (color.grayscale > 0.5f)
            {
                // Light text gets a soft drop shadow so it stays legible over the board and bright panels.
                var shadow = rt.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
                shadow.effectDistance = new Vector2(1.5f, -1.5f);
            }

            return t;
        }

        static void Swatch(Transform parent, Vector2 position, Color color, bool round)
        {
            var rt = Rect(parent, "Icon", new Vector2(0, 1), position, new Vector2(28, 28));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (!round) rt.localRotation = Quaternion.Euler(0, 0, 45); // diamond for health, square for gold
        }

        Button MakeButton(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction onClick, out Text text)
        {
            var rt = Rect(parent, label.Replace(" ", "") + "Button", anchor, position, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Palette.Hex(0x3A5963);
            Sliced(img, buttonSprite);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            ApplyStates(button);
            button.onClick.AddListener(onClick);
            button.onClick.AddListener(ClickSound);
            text = Label(rt, "Label", 24, TextAnchor.MiddleCenter, Vector2.zero, size, Palette.HudText, true);
            text.text = label;
            return button;
        }

        /// <summary>One state language for every button and card: hover brightens, press darkens, focus stays visible, disabled fades.</summary>
        static void ApplyStates(Selectable selectable)
        {
            var colors = selectable.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.22f, 1.22f, 1.22f, 1f);
            colors.selectedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.62f, 0.62f, 0.62f, 1f); // dimmed but opaque: the board never shows through a card
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            selectable.colors = colors;
        }

        /// <summary>Short tactile click (hover never makes a sound).</summary>
        void ClickSound() => game.Audio?.Play(SoundId.UiClick);

        static RawImage Icon(Transform parent, Texture2D texture, Vector2 position, float size, Color tint)
        {
            var rt = Rect(parent, "Icon", new Vector2(0, 1), position, new Vector2(size, size));
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = texture;
            img.color = tint;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// One short line on each MVP card saying what the building does. With a run, Tower/Farm show effective values
        /// (perks); the Farm line never includes the placement-dependent House bonus.
        /// </summary>
        static string CardEffect(BuildingDefinition d, RunSimulation sim = null)
        {
            switch (d.Kind)
            {
                case BuildingKind.Wall: return "Blocks enemy paths";
                case BuildingKind.Tower: return sim == null ? $"Shoots · range {d.Range:0}" : $"Shoots {sim.TowerDamage(d)} · range {sim.TowerRange(d).ToString("0.#", CultureInfo.InvariantCulture)}";
                case BuildingKind.House: return d.LandIncome > 0 ? $"+{d.DailyIncome}/day, +{d.LandIncome} per field" : $"+{d.DailyIncome} gold per day";
                case BuildingKind.Farm:
                    if (d.LandIncome > 0) return $"+{d.DailyIncome}/day, +{d.LandIncome} per field · 2x2";
                    var farm = sim != null && sim.HasPerk(PerkRules.EfficientFarming) ? d.DailyIncome * (100 + PerkRules.EfficientFarmingPercent) / 100 : d.DailyIncome;
                    return $"+{farm}/day · 2x2 tiles";
                case BuildingKind.SpikeTrap: return "Hurts enemies on it";
                case BuildingKind.Cannon: return d.FootprintSize > 1 ? "Splash · slow · 2x2" : "Splash · slow";
                case BuildingKind.Watchtower: return $"+{d.RangeBonus.ToString("0.#", CultureInfo.InvariantCulture)} range nearby";
                case BuildingKind.RepairStation: return "Repairs at dawn · 2x2";
                case BuildingKind.Mine: return $"+{d.DailyIncome}/day · gold vein";
                default: return "";
            }
        }

        Button Card(Transform parent, string title, int cost, string hotkey, Vector2 position, float width,
            UnityEngine.Events.UnityAction onClick, out Image image, out Text costText, string effect = null, Texture2D icon = null)
        {
            var rt = Rect(parent, title.Replace(" ", "") + "Card", new Vector2(0, 0), position, new Vector2(width, 120));
            image = rt.gameObject.AddComponent<Image>();
            image.color = CardIdle;
            Sliced(image, buttonSprite);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ApplyStates(button);
            button.onClick.AddListener(onClick);
            button.onClick.AddListener(ClickSound);
            var compact = effect != null;
            var titleX = icon != null ? 52f : 16f;
            if (icon != null) Icon(rt, icon, new Vector2(12, -8), 34f, Palette.HudText);
            var titleText = Label(rt, "Title", width < 240 ? 22 : 26, TextAnchor.UpperLeft, new Vector2(titleX, compact ? -10 : -12), new Vector2(width - titleX - 10, 34), Palette.HudText);
            titleText.text = title;
            Text effectText = null;
            if (compact)
            {
                effectText = Label(rt, "Effect", 18, TextAnchor.UpperLeft, new Vector2(16, -38), new Vector2(width - 24, 24), Palette.HudText.WithAlpha(0.75f));
                effectText.text = effect;
            }

            // Two rows of five narrower cards (release-rules-4 with the Mine): long titles and effects shrink to stay on one line.
            foreach (var t in new[] { titleText, effectText })
            {
                if (t == null || width >= 200) continue;
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = 14;
                t.resizeTextMaxSize = t.fontSize;
                t.verticalOverflow = VerticalWrapMode.Truncate;
            }
            costText = Label(rt, "Cost", compact ? 25 : 28, TextAnchor.UpperLeft, new Vector2(16, compact ? -70 : -52), new Vector2(width - 90, 40), Palette.Amber);
            costText.text = $"{cost} gold";
            var keyX = width - 62;
            var keyY = compact ? -70f : -64f;
            var key = Label(rt, "Hotkey", 22, TextAnchor.MiddleCenter, new Vector2(keyX, keyY), new Vector2(46, 40), Palette.HudSurface);
            key.text = hotkey;
            var keyBack = Rect(rt, "HotkeyBack", new Vector2(0, 1), new Vector2(keyX, keyY), new Vector2(46, 40));
            var kb = keyBack.gameObject.AddComponent<Image>();
            kb.color = Palette.HudText;
            Sliced(kb, buttonSprite);
            kb.raycastTarget = false;
            keyBack.SetSiblingIndex(key.transform.GetSiblingIndex());
            return button;
        }
    }
}
