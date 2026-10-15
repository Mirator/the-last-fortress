using UnityEngine;
using UnityEngine.UI;

namespace TheLastFortress
{
    /// <summary>uGUI HUD built in code (legacy UI.Text with the built-in font; no TMP resources required).</summary>
    public sealed class HudView : MonoBehaviour
    {
        PrototypeBootstrap game;
        Font font;
        RectTransform canvasRect;

        Text goldText, keepText, stateText, infoText, routeText, tooltipText, breachText, selectionTitle, selectionBody,
            terminalTitle, terminalBody, diagnosticText, primaryLabel, stressText;
        RectTransform keepFill, tooltipRect, breachRect;
        GameObject selectionPanel, tooltipPanel, breachLabel, terminalOverlay, stressBanner, diagnosticPanel;
        Image wallCardImage, towerCardImage, primaryImage;

        public Button WallCard { get; private set; }
        public Button TowerCard { get; private set; }
        public Button PrimaryButton { get; private set; }
        public Button MenuRestartButton { get; private set; }
        public Button ResetCameraButton { get; private set; }
        public Button SellButton { get; private set; }
        public Button TerminalRestartButton { get; private set; }
        public GameObject TerminalOverlay => terminalOverlay;
        public string TerminalTitle => terminalTitle.text;
        public GameObject StressBanner => stressBanner;
        public GameObject BreachLabel => breachLabel;
        public string StateText => stateText.text;
        public string TooltipText => tooltipPanel.activeSelf ? tooltipText.text : null;
        public string PrimaryButtonLabel => primaryLabel.text;
        public RectTransform CanvasRect => canvasRect;

        static readonly Color CardIdle = Palette.HudSurface.WithAlpha(0.94f);
        static readonly Color CardActive = Palette.Hex(0x35525C);

        public void Build(PrototypeBootstrap game)
        {
            this.game = game;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
            var resources = Panel(root, "Resources", new Vector2(0, 1), new Vector2(24, -24), new Vector2(380, 132));
            Swatch(resources, new Vector2(20, -22), Palette.Amber, true);
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

            // Top-center: phase and enemies
            var status = Panel(root, "Status", new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(760, 150));
            stateText = Label(status, "State", 34, TextAnchor.UpperCenter, new Vector2(0, -10), new Vector2(740, 44), Palette.Amber, true);
            infoText = Label(status, "Info", 22, TextAnchor.UpperCenter, new Vector2(0, -58), new Vector2(740, 32), Palette.HudText, true);
            routeText = Label(status, "Route", 21, TextAnchor.UpperCenter, new Vector2(0, -98), new Vector2(740, 44), Palette.RouteCyan, true);

            // Top-right: menu
            var menu = Panel(root, "Menu", new Vector2(1, 1), new Vector2(-24, -24), new Vector2(430, 76));
            MenuRestartButton = MakeButton(menu, "Restart", new Vector2(0, 1), new Vector2(12, -12), new Vector2(196, 52), game.Restart, out _);
            ResetCameraButton = MakeButton(menu, "Reset Camera", new Vector2(0, 1), new Vector2(220, -12), new Vector2(198, 52), game.ResetCamera, out _);

            // Bottom-center: build cards
            var cards = Rect(root, "BuildCards", new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(500, 120));
            WallCard = Card(cards, "Wall", game.Definitions.Wall.Cost, "1", new Vector2(0, 0), () => game.Player.SetTool(BuildingKind.Wall), out wallCardImage);
            TowerCard = Card(cards, "Archer Tower", game.Definitions.Tower.Cost, "2", new Vector2(260, 0), () => game.Player.SetTool(BuildingKind.Tower), out towerCardImage);

            // Bottom-right: primary action
            var actions = Rect(root, "Actions", new Vector2(1, 0), new Vector2(-24, 24), new Vector2(300, 100));
            PrimaryButton = MakeButton(actions, "START NIGHT", new Vector2(0, 0), Vector2.zero, new Vector2(300, 100), game.PrimaryAction, out primaryLabel);
            primaryImage = PrimaryButton.GetComponent<Image>();
            primaryLabel.fontSize = 30;

            // Bottom-left: selection
            selectionPanel = Panel(root, "Selection", new Vector2(0, 0), new Vector2(24, 24), new Vector2(380, 190)).gameObject;
            selectionTitle = Label(selectionPanel.transform, "Name", 26, TextAnchor.UpperLeft, new Vector2(18, -12), new Vector2(350, 36), Palette.Amber);
            selectionBody = Label(selectionPanel.transform, "Body", 21, TextAnchor.UpperLeft, new Vector2(18, -52), new Vector2(350, 70), Palette.HudText);
            SellButton = MakeButton(selectionPanel.transform, "Sell", new Vector2(0, 1), new Vector2(18, -124), new Vector2(200, 52), game.SellSelected, out _);
            selectionPanel.SetActive(false);

            // Controls hint
            Label(root, "Hint", 18, TextAnchor.LowerLeft, new Vector2(24, 230), new Vector2(1100, 30), Palette.HudText.WithAlpha(0.8f))
                .text = "1 Wall · 2 Tower · LMB place/select · RMB/Esc cancel · WASD/arrows pan · wheel zoom · Space pause";
            var hintRect = root.Find("Hint") as RectTransform;
            hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = Vector2.zero;
            hintRect.anchoredPosition = new Vector2(24, 226);

            // Cursor tooltip and breach label (never block pointer)
            tooltipPanel = Panel(root, "Tooltip", new Vector2(0, 0), Vector2.zero, new Vector2(330, 74)).gameObject;
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();
            tooltipRect.pivot = new Vector2(0, 1);
            tooltipPanel.GetComponent<Image>().raycastTarget = false;
            tooltipText = Label(tooltipPanel.transform, "Text", 21, TextAnchor.MiddleLeft, new Vector2(12, -6), new Vector2(310, 62), Palette.HudText);
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
        }

        // ---------- refresh ----------

        public void Refresh(RunSimulation sim, PlayerController player, RouteOverlay overlay, CameraRig rig)
        {
            var defs = sim.Definitions;
            Set(goldText, $"Gold  {sim.Gold}");
            Set(keepText, $"Keep  {sim.Keep.Hp} / {defs.Keep.MaxHp}");
            keepFill.anchorMax = new Vector2(Mathf.Clamp01((float)sim.Keep.Hp / defs.Keep.MaxHp), 1);

            var wave = sim.Wave;
            switch (sim.State)
            {
                case RunState.Day:
                    Set(stateText, "DAY — build your defenses");
                    Set(infoText, $"Upcoming: {wave.Spawns.Count} Grunts from the west edge {WavePlan.MainEntry}, one every 1.5 s");
                    break;
                case RunState.Night:
                    Set(stateText, sim.Paused ? "NIGHT — PAUSED" : $"NIGHT  {sim.NightElapsedSeconds:0.0}s");
                    Set(infoText, $"Enemies alive {sim.AliveEnemyCount} · still to come {sim.ScheduledRemaining}");
                    break;
                default:
                    Set(stateText, sim.State == RunState.Victory ? "VICTORY" : "DEFEAT");
                    Set(infoText, $"Enemies alive {sim.AliveEnemyCount} · still to come {sim.ScheduledRemaining}");
                    break;
            }

            if (sim.StressMode) Set(infoText, $"{sim.AliveEnemyCount} Grunts active across 4 entries · Keep/Walls invulnerable · {sim.NightElapsedSeconds:0}s");

            // Route summary (Day only)
            var route = overlay.PrimaryRoute;
            if (route == null) Set(routeText, sim.State == RunState.Night ? "Towers fire automatically. Enemies follow open routes first." : "");
            else if (!route.Found) Set(routeText, "NO ROUTE — map invalid");
            else if (!route.IsBreach)
            {
                Set(routeText, $"{(overlay.CandidateRoute != null ? "If built: " : "Route: ")}open path, {route.Cells.Count - 1} tiles to the Keep");
                routeText.color = Palette.RouteCyan;
            }
            else
            {
                var blocker = sim.Grid.GetOccupant(route.Blocker);
                var name = blocker != null ? blocker.Definition.DisplayName : player.Tool.HasValue ? defs.For(player.Tool.Value).DisplayName : "structure";
                Set(routeText, $"{(overlay.CandidateRoute != null ? "If built: " : "")}SEALED — enemies will BREACH {name} at {route.Blocker}");
                routeText.color = Palette.WarningCoral;
            }

            // Build cards
            var canBuild = sim.State == RunState.Day && !sim.StressMode;
            WallCard.interactable = canBuild;
            TowerCard.interactable = canBuild;
            wallCardImage.color = player.Tool == BuildingKind.Wall ? CardActive : CardIdle;
            towerCardImage.color = player.Tool == BuildingKind.Tower ? CardActive : CardIdle;

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
                Set(primaryLabel, "—");
                PrimaryButton.interactable = false;
            }

            // Selection panel
            var sel = player.Selected;
            if (selectionPanel.activeSelf != (sel != null)) selectionPanel.SetActive(sel != null);
            if (sel != null)
            {
                Set(selectionTitle, sel.Definition.DisplayName);
                var body = $"HP {sel.Hp} / {sel.Definition.MaxHp}   at {sel.Cell}";
                if (sel.Kind == BuildingKind.Tower) body += $"\nDamage {sel.Definition.AttackDamage} every {sel.Definition.AttackIntervalSeconds}s, range {sel.Definition.Range}";
                var canSell = sim.State == RunState.Day && sel.IsDestructible && !sim.StressMode;
                if (canSell) body += $"\nSell refund: {PlacementService.RefundFor(sel)} gold";
                else if (!sel.IsDestructible) body += "\nThe Keep cannot be sold";
                Set(selectionBody, body);
                if (SellButton.gameObject.activeSelf != canSell) SellButton.gameObject.SetActive(canSell);
            }

            // Ghost tooltip
            var showTip = overlay.GhostVisible && player.Tool.HasValue;
            if (tooltipPanel.activeSelf != showTip) tooltipPanel.SetActive(showTip);
            if (showTip)
            {
                var def = defs.For(player.Tool.Value);
                Set(tooltipText, overlay.GhostValid
                    ? $"{def.DisplayName} — {def.Cost} gold\n[OK] Valid placement"
                    : $"{def.DisplayName} — {def.Cost} gold\n[X] {PlacementText.Describe(overlay.GhostRejection)}");
                tooltipText.color = overlay.GhostValid ? Palette.HudText : Palette.WarningCoral;
                var anchor = player.HoverOverride.HasValue ? rig.CellToScreen(player.HoverOverride.Value) : player.PointerPosition;
                tooltipRect.anchoredPosition = ScreenToCanvas(anchor) + new Vector2(28, -12);
            }

            // Breach label
            var showBreach = overlay.BreachMarkerVisible;
            if (breachLabel.activeSelf != showBreach) breachLabel.SetActive(showBreach);
            if (showBreach)
            {
                var p = rig.Camera.WorldToScreenPoint(overlay.BreachMarkerPosition + Vector3.up * 1.3f);
                breachRect.anchoredPosition = ScreenToCanvas(new Vector2(p.x, p.y));
            }

            if (stressBanner.activeSelf != sim.StressMode) stressBanner.SetActive(sim.StressMode);

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
                Set(terminalBody, won
                    ? $"All {wave.Spawns.Count} Grunts defeated.\nKeep held with {sim.Keep.Hp} / {defs.Keep.MaxHp} HP after {sim.NightElapsedSeconds:0.0}s."
                    : $"The Keep fell after {sim.NightElapsedSeconds:0.0}s.\n{sim.AliveEnemyCount} Grunts were still attacking.");
            }
        }

        Vector2 ScreenToCanvas(Vector2 screen)
        {
            var scale = canvasRect.lossyScale.x;
            return scale > 0 ? screen / scale : screen;
        }

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
            img.color = Palette.HudSurface.WithAlpha(0.92f);
            return rt;
        }

        Text Label(Transform parent, string name, int size, TextAnchor align, Vector2 position, Vector2 box, Color color, bool centered = false)
        {
            var rt = Rect(parent, name, centered ? new Vector2(0.5f, 1) : new Vector2(0, 1), position, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
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
            img.color = Palette.Hex(0x35525C);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            text = Label(rt, "Label", 24, TextAnchor.MiddleCenter, Vector2.zero, size, Palette.HudText, true);
            text.text = label;
            return button;
        }

        Button Card(Transform parent, string title, int cost, string hotkey, Vector2 position,
            UnityEngine.Events.UnityAction onClick, out Image image)
        {
            var rt = Rect(parent, title.Replace(" ", "") + "Card", new Vector2(0, 0), position, new Vector2(240, 120));
            image = rt.gameObject.AddComponent<Image>();
            image.color = CardIdle;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            Label(rt, "Title", 26, TextAnchor.UpperLeft, new Vector2(16, -12), new Vector2(210, 34), Palette.HudText).text = title;
            Label(rt, "Cost", 30, TextAnchor.UpperLeft, new Vector2(16, -52), new Vector2(150, 40), Palette.Amber).text = $"{cost} gold";
            var key = Label(rt, "Hotkey", 22, TextAnchor.MiddleCenter, new Vector2(178, -64), new Vector2(46, 40), Palette.HudSurface);
            key.text = hotkey;
            var keyBack = Rect(rt, "HotkeyBack", new Vector2(0, 1), new Vector2(178, -64), new Vector2(46, 40));
            var kb = keyBack.gameObject.AddComponent<Image>();
            kb.color = Palette.HudText;
            kb.raycastTarget = false;
            keyBack.SetSiblingIndex(key.transform.GetSiblingIndex());
            return button;
        }
    }
}
