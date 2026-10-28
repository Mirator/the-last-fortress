# Strategic figure readability — feature specification

Version **0.3**, 2026-10-28. Status: **IMPLEMENTED and merged into `../002-game-rules.md` v1.9 section 13 (2026-10-28); archived.** Not current: plan from `002-game-rules.md`. The owner's review of the captures is pending. Approved 2026-10-28. The option was chosen by the agent at the owner's request ("choose the best option"); the owner accepted it as recommended: D1-a, D2-a, D3-a (default half-height 4.5).

This document extends `002-game-rules.md` v1.8 and respects `001-vision.md`. It changes the "Board readability" bullet and the camera zoom range (and possibly the default zoom) in section 13, and the figures paragraph in section 17. It is presentation only: no simulation, save or settings change.

**As built (v0.3), where it differs from the text below:**
- the Runner's marker is a triangle that always points up the screen, not along its heading;
- enemy HP bars keep the on-screen size they have at half-height 5.5 when zoomed further out, rather than a fixed pixel size;
- the provisional values chosen from the captures are 18 px, 0.12 tile minimum and 0.42 tile maximum;
- after the owner's first look (2026-10-28), markers are drawn under the enemy figures (only terrain, buildings and trees are ignored), because close up a marker drawn on top hid the figure and a Grunt read as a bomb; they also fade out between half-height 4 and 3;
- the fill colours are those listed in `002-game-rules.md` section 13.

## 1. Problem

The owner's screenshot (release build, 2026-10-28, Night 1, a window about 930 px tall) shows that the hero and enemies are too small to read. Enemies are dark specks of a few pixels, and their class cannot be told.

- **This is at about the default zoom, not zoomed out.** The Keep (about 19 m, 2.4 tiles) is about 170 px tall in the frame. At the 40° pitch that gives about 90 px per tile, so a half-height of about 5. The problem is therefore the default view, not only the far zoom.

- **Why this happens.** Figures are drawn at true scale in both views (owner decision 2026-10-19, section 17): a person is about 0.22 tile tall. The strategic camera is orthographic, with a half-height from 2.5 to 14 tiles. At 1080p a tile is 540 / half-height pixels, so about 98 px at the default 5.5 and 39 px at 14. Plan 014 measured a person at about 10–14 px at the default zoom and left open "class markers vs closer zoom".
- **The vision is not met.** `001-vision.md` asks that "enemy classes are distinguishable at low zoom", and pillar 3 asks for readable battles.
- **What is already there.**
  - **Hero:** a cyan ground ring of fixed world size, about 1.2 tiles across, so about 46 px wide at half-height 14. The hero can already be found; the figure inside the ring is what is small.
  - **Enemies:** a cloth colour per class, with mild emission. At a few pixels this does not survive, least of all at night.
  - **HP bars:** shown only when the enemy is damaged, at a fixed world size in the strategic view (half the old width). They shrink with zoom like everything else. Only the hero (perspective) view keeps bars at a constant size on screen.
  - **Tonight preview:** the Day HUD already lists tonight's classes per entry (`Tonight: West 6 Grunts + 2 Runners · …`). The entry tags only show a count (`NORTH 10`).
- **No size compensation in the strategic view.** Nothing on the board keeps a constant on-screen size in the orthographic strategic view today; this spec introduces it.

## 2. Goals and boundaries

Goals:
- Each enemy's position and class can be read in the strategic view at every allowed zoom, by day and by night, at 1280×720 and 1920×1080.
- Keep true-scale figures and the rule that the two views never differ in proportions.
- Keep crowds readable as crowds. A hundred enemies must not turn into a carpet of overlapping symbols.

Out of scope:
- the hero view (its figures are already large on screen);
- figure models and animation;
- the entry tags' text (section 7);
- any simulation, balance or save change.

## 3. Options

| | Change | Readable at far zoom | Keeps true scale | Cost / risk |
|---|---|---|---|---|
| A | Enlarge figures 1.5–2.5× in both views (`FigureScale` / `enemyScale`) | Partly. Still small at half-height 14. | No: reverses the 2026-10-19 decision | One constant, but the hero view then shows people as tall as doors, and contact distances (0.225 tile) would look like overlap. Useful only as an A/B capture for the owner. |
| B | Enlarge figures in the strategic view only, growing with zoom | Yes | No: the views differ in proportions | Figures clip into Walls and each other in crowds; the figure no longer shows where the enemy really is. |
| **C** | **Class markers.** A flat class-shaped marker under each enemy in the strategic view. Its world size grows with zoom, so its size on screen stays about the same. | **Yes** | **Yes**: a marker is presentation detail, like the hero's ring | Small to medium: one marker per enemy view, a size rule tied to the camera. Risk: clutter in crowds, which the size cap (section 5) bounds. |
| **D** | **Cap the zoom-out** at the existing `WholeBoardSize` (half-height 10.5) instead of 14 | Removes the worst case | Yes | Trivial. Above 10.5 the view shows mostly mist beyond the board. |
| **F** | **Closer default zoom.** Default half-height 5.5 → 4.0–4.5, which plan 014 already named as an option. | Only at the default; zooming out still shows specks | Yes | Trivial. The figures are about 25–35% larger on screen at the default, but each screen shows less of the board; `Z` (the whole board) and the wheel are unchanged. This is the only option here that makes figures **bigger** at the zoom of the complaint without changing their scale. |
| E | Constant on-screen HP bars in the strategic view | Only damaged enemies | Yes | Small, but does not show class or undamaged enemies. Folded into C (section 5, item 5). |

## 4. Recommendation

**C + D, with F as an owner decision (D3, recommended yes at 4.5).**

- **C is the only option that meets both goals.** It is how true-scale strategy games do it: Total War's unit banners and Supreme Commander's strategic icons. It also follows this game's existing pattern: the hero's ring and the burn and fuse markers are presentation detail added for readability.
- **C makes figures readable, not bigger.** At the default zoom the marker is only slightly wider than the feet. It adds shape and colour, so class and position read, but the figure itself stays small. The owner asked for "bigger", which is why F is offered as D3.
- **D is housekeeping.** The camera already treats 10.5 as "the whole board", and zooming further only adds mist. It is not where the complaint was made.
- **A is the fallback** if the owner later prefers the genre's oversized look. Because it is one constant, it can be captured side by side with C for comparison without further work.
- **Not B:** it breaks the proportion rule and the reliability of where a figure stands.

## 5. Rules (the contract for the plan)

Replaces the end of the section 13 "Board readability" bullet and adds to section 17's figures paragraph. All values marked [P] are provisional presentation values, set in the capture review (section 7); the rest are the rule.

1. **One marker per living enemy, strategic view only.** It is a flat shape on the ground under the enemy, drawn at the enemy's shown position (including its lane offset). It hides in the hero view, as the burn marker already does, and goes with the figure when the enemy dies or leaves.
2. **Shape is the class; colour supports it.** Classes must be distinguishable by shape alone (colour-blind safe, the project's "shape, not colour alone" convention):
   - Grunt: disc;
   - Runner: chevron pointing along its shown heading;
   - Brute: square;
   - Raider Archer: diamond;
   - Bomber: hollow ring (the lit-fuse spark still shows);
   - Siege Golem and Siege Commander: a larger hexagon (they also have the boss bar).

   Each fill colour is the class's cloth tint lightened, with a dark outline so it reads on grass by day and by night [P]. Fills must stay clearly apart from the reserved hues: cyan (hero, routes), coral (entries, breach, Commander aura), amber (fuse, torches), orange (burn).
3. **Constant size on screen, with limits.** The marker's world diameter is `clamp(px × 2 × half-height ÷ screen height, footprint, cap)`, recomputed each frame. This keeps the same pixel size at 1280×720 and 1920×1080; a factor fixed in world terms would not, because the orthographic view scales with the screen height.
   - `px` is about 14–16 px [P];
   - `footprint` is about 0.15 tile [P], a little wider than the figure's feet, so close up the marker never pokes far out from under them;
   - `cap` is about 0.4 tile [P], which bounds overlap in crowds. Enemies in one lane are offset by up to ±0.12 tile, so a column at full zoom-out reads as a dense strip of the same shape. That is intended: the front marker still shows the class.
   - Bosses use 1.5× the cap [P].
4. **Never hidden by the board.** Markers draw over terrain, buildings and trees (no depth test), below HP bars and the HUD, and unlit, so night lighting does not darken them. This prevents an enemy behind a tree or the Keep from vanishing at the fixed 40° camera.
5. **HP bars follow the same size rule in the strategic view.** Bars stay damaged-only (see D2), but their scale uses the marker's camera term, so a damaged enemy's bar is readable at every zoom.
6. **Camera.** The strategic zoom range becomes half-height 2.5–10.5 (`WholeBoardSize`), instead of 2.5–14. The default is 5.5, or the D3 value.
7. **Hero.** No new marker. The cyan ring stays; at about 1.2 tiles it is already about 60 px wide at half-height 10.5 (1080p). A check at 10.5 by night is part of the acceptance captures.

## 6. Decisions for the owner

**D1 — markers at the default zoom.** At half-height 5.5 a person is still small (about 10–14 px).

| | Rule | Effect |
|---|---|---|
| **D1-a (recommended)** | Always shown in the strategic view | One consistent read at every zoom. At 5.5 the marker is barely larger than the feet, so it adds class colour and shape without covering the figure. |
| D1-b | Shown only from half-height ≥ about 7, fading in | The default view stays as now. Two looks to learn, and night readability at the default zoom is unchanged. |

**D2 — HP bars for undamaged enemies.**

| | Rule | Effect |
|---|---|---|
| **D2-a (recommended)** | Damaged-only, as now (with rule 5's size) | The marker already shows presence. Bars still mean "being hurt", so the board stays calm. |
| D2-b | Always on in the strategic view | Every enemy carries a bar. Adds about 100 bars of clutter in the stress fixture for little extra information. |

**D3 — default zoom (option F).**

| | Rule | Effect |
|---|---|---|
| **D3-a (recommended)** | Default half-height 4.5 | Figures about 22% larger on screen at the default; less of the board around the Keep. `Z` still shows the whole board. |
| D3-b | Default 4.0 | About 38% larger, noticeably tighter framing; entries may start off screen on larger maps. |
| D3-c | Keep 5.5 | No change; markers alone carry readability. |

The stage 4 captures include the default view at 5.5, 4.5 and 4.0, so this can be decided from frames.

## 7. Acceptance

- **Captures for owner review.** A night with Brutes, Runners and Grunts, and the 100-enemy stress fixture:
  - before and after, at half-heights 5.5 and 10.5, by day and by night, at 1280×720 and 1920×1080;
  - plus one option A frame (`enemyScale` × 2) for comparison.

  The provisional values (k, cap, colours) are fixed from these captures.
- **No speck test.** At half-height 10.5, at both resolutions, every class can be named from its marker in the capture, and no enemy is hidden behind a tree or building.
- **Crowd test.** In the stress fixture, the lanes still read as lanes, and a group at a Wall still reads as a group of identifiable classes.
- **Performance.** The paired stress run still meets the section 14 gate. Section 14's figures predate the threat change and plan 022, so take a fresh baseline in the same session.
- **Not in scope of acceptance:** the hero view. It must look exactly as before.

## 8. Not changed / later

- **Entry tags** keep `NORTH 10`. A per-class breakdown (`NORTH 6 Grunt · 3 Runner · 1 Brute`) would duplicate the Day `Tonight` line. It would also widen tags whose width drives the low-screen layout clamps (`HudView.Mvp.cs`, `RefreshEntryMarkers`). Revisit only if a playtest asks for it.
- **No setting to turn markers off**, so settings stay at version 3. Add one only if players ask for it.
