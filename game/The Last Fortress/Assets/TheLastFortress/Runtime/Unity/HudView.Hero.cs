using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastFortress
{
    /// <summary>
    /// Plan 012 hero HUD (specs/003 4.5): hero health, the respawn countdown and the view hint, bottom-left so the build
    /// cards keep their place. Shown for runs with a hero only.
    /// </summary>
    public sealed partial class HudView
    {
        GameObject heroPanel, buildCards;
        RectTransform heroFill;
        Text heroText, heroHint, keepDirection;
        GameObject crosshair;

        /// <summary>The hero panel shows its key hints through this night, then only when the mouse must be recaptured.</summary>
        public const int HintNights = 3;

        /// <summary>Whether the hero view's aiming crosshair is shown (tests read it).</summary>
        public bool CrosshairShown => crosshair != null && crosshair.activeSelf;

        /// <summary>Text of the off-screen Keep-hit marker while shown, else "" (tests read it).</summary>
        public string KeepDirectionShown => keepDirection != null && keepDirection.gameObject.activeSelf ? keepDirection.text : "";

        /// <summary>Last text shown on the hero panel (tests read it).</summary>
        public string HeroStatusShown => heroText != null && heroPanel.activeSelf ? heroText.text : "";

        void BuildHeroPanel(RectTransform root)
        {
            var panel = Panel(root, "HeroPanel", new Vector2(0, 0), new Vector2(24, 24), new Vector2(380, 104));
            heroPanel = panel.gameObject;
            heroText = Label(panel, "Text", 22, TextAnchor.UpperLeft, new Vector2(18, -12), new Vector2(310, 30), Palette.HudText);
            heroText.fontStyle = FontStyle.Bold;
            var back = Rect(panel, "Bar", new Vector2(0, 1), new Vector2(18, -46), new Vector2(304, 14));
            back.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            heroFill = Rect(back, "Fill", Vector2.zero, Vector2.zero, Vector2.zero);
            heroFill.anchorMin = Vector2.zero;
            heroFill.anchorMax = Vector2.one;
            heroFill.pivot = new Vector2(0, 0.5f);
            heroFill.offsetMin = heroFill.offsetMax = Vector2.zero;
            heroFill.gameObject.AddComponent<Image>().color = Palette.RouteCyan;
            // 20 pt and brighter: at 18 pt and 75% it was about 12 px at 1280x720 (capture review 2026-10-28).
            heroHint = Label(panel, "Hint", 20, TextAnchor.UpperLeft, new Vector2(18, -68), new Vector2(350, 30), Palette.HudText.WithAlpha(0.9f));
            heroPanel.SetActive(false);

            // Hero view: when the Keep is hit while it is behind or beside the camera, a coral tag at the screen edge points to it.
            keepDirection = Label(panel.parent, "KeepDirection", 26, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(220, 44), Palette.WarningCoral, true);
            keepDirection.fontStyle = FontStyle.Bold;
            var kr = keepDirection.rectTransform;
            kr.anchorMin = kr.anchorMax = kr.pivot = new Vector2(0.5f, 0.5f);
            keepDirection.gameObject.SetActive(false);

            // Hero view aiming crosshair at the screen centre (owner, 2026-10-20: ready for a future bow): a dot and four
            // short ticks with a dark outline, so it reads on bright sky and dark ground alike.
            var cross = Rect(panel.parent, "Crosshair", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
            cross.anchorMin = cross.anchorMax = cross.pivot = new Vector2(0.5f, 0.5f);
            cross.anchoredPosition = Vector2.zero;
            void Tick(Vector2 at, Vector2 size)
            {
                foreach (var (grow, color) in new[] { (2f, new Color(0f, 0f, 0f, 0.55f)), (0f, new Color(1f, 1f, 1f, 0.9f)) })
                {
                    var r = Rect(cross, "Tick", new Vector2(0.5f, 0.5f), Vector2.zero, size + Vector2.one * grow);
                    r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                    r.anchoredPosition = at;
                    var image = r.gameObject.AddComponent<Image>();
                    image.color = color;
                    image.raycastTarget = false;
                }
            }

            Tick(Vector2.zero, new Vector2(3, 3));
            Tick(new Vector2(0, 10), new Vector2(2, 8));
            Tick(new Vector2(0, -10), new Vector2(2, 8));
            Tick(new Vector2(10, 0), new Vector2(8, 2));
            Tick(new Vector2(-10, 0), new Vector2(8, 2));
            crosshair = cross.gameObject;
            crosshair.SetActive(false);
        }

        /// <summary>
        /// Places the Keep-hit marker at the screen edge toward the Keep while a hit cue runs in the hero view and the Keep
        /// is off screen (behind the camera counts). The top-left Keep bar flashes in both views as before.
        /// </summary>
        void RefreshKeepDirection(RunSimulation sim)
        {
            if (keepDirection == null) return;
            var show = game.HeroViewActive && KeepHitShown > 0;
            var size = canvasRect.rect.size;
            Vector2 at = default;
            var arrow = "";
            if (show)
            {
                var keep = Prims.CellCenter(GridState.KeepCell, 1.5f);
                var sp = ProjectToScreen(keep);
                var c = ScreenToCanvas(new Vector2(sp.x, sp.y));
                var behind = sp.z < 0f;
                if (behind) c = size - c; // mirrored: a point behind projects through the opposite side
                var inside = !behind && c.x > 0 && c.x < size.x && c.y > 0 && c.y < size.y;
                show = !inside;
                var dir = c - size * 0.5f;
                if (behind && dir.sqrMagnitude < 1f) dir = Vector2.down;
                var scale = Mathf.Min((size.x * 0.5f - 130f) / Mathf.Max(1e-3f, Mathf.Abs(dir.x)), (size.y * 0.5f - 60f) / Mathf.Max(1e-3f, Mathf.Abs(dir.y)));
                at = size * 0.5f + dir * Mathf.Min(1f, scale);
                arrow = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? (dir.x < 0 ? "< " : "> ") : dir.y < 0 ? "v " : "^ ";
            }

            if (keepDirection.gameObject.activeSelf != show) keepDirection.gameObject.SetActive(show);
            if (!show) return;
            Set(keepDirection, $"{arrow}KEEP -{KeepHitShown}");
            keepDirection.rectTransform.anchoredPosition = at - size * 0.5f;
        }

        void RefreshHero(RunSimulation sim)
        {
            if (heroPanel == null) return;
            var hero = sim.Hero;
            RefreshKeepDirection(sim);
            var aim = game.HeroViewActive && game.CursorLocked && hero != null && hero.Alive;
            if (crosshair != null && crosshair.activeSelf != aim) crosshair.SetActive(aim);
            // The hero view keeps the screen clear: build cards return with the strategic view (1-8 switches back).
            if (buildCards == null) buildCards = canvasRect.Find("Gameplay/BuildCards")?.gameObject;
            if (buildCards != null && buildCards.activeSelf == game.HeroViewActive) buildCards.SetActive(!game.HeroViewActive);
            var show = hero != null && !game.TitleScreen && !selectionPanel.activeSelf &&
                       (sim.State == RunState.Day || sim.State == RunState.Night || sim.State == RunState.Reward);
            if (heroPanel.activeSelf != show) heroPanel.SetActive(show);
            if (!show) return;
            if (hero.Alive) Set(heroText, $"Hero  {hero.Hp} / {HeroRules.MaxHp}");
            else
            {
                var seconds = Mathf.Max(0f, (hero.RespawnTick - sim.Tick) * (float)PrototypeDefinitions.StepSeconds);
                Set(heroText, $"Hero down · back in {seconds.ToString("0", CultureInfo.InvariantCulture)} s");
            }

            heroFill.anchorMax = new Vector2(hero.Alive ? Mathf.Clamp01((float)hero.Hp / HeroRules.MaxHp) : 0f, 1);
            // Key hints are for learning: from Night HintNights + 1 on only the mouse-recapture prompt (it is needed) stays.
            var learning = sim.NightNumber <= HintNights;
            Set(heroHint, game.HeroViewActive && game.AwaitingRecapture ? "Click to take the mouse back · Tab strategy"
                : !learning ? ""
                : game.HeroViewActive
                    ? sim.State == RunState.Night ? "Click attack · Tab strategy · P pause" : "Tab strategy · Enter night · 1-8 build"
                    : sim.State == RunState.Day ? "Guarding · Tab hero · Enter night" : "Guarding · Tab hero · H find");
            var panelH = heroHint.text.Length > 0 ? 104f : 74f;
            var panelRect = (RectTransform)heroPanel.transform;
            if (panelRect.sizeDelta.y != panelH) panelRect.sizeDelta = new Vector2(380, panelH);
            var menuPanel = canvasRect.Find("Gameplay/Menu");
            if (menuPanel != null && menuPanel.gameObject.activeSelf == game.HeroViewActive) menuPanel.gameObject.SetActive(!game.HeroViewActive);
        }
    }
}
