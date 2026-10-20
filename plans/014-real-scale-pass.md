# Plan 014: real-scale consistency pass (DRAFT, owner decisions open)

Status: **IMPLEMENTED (agent), owner look PENDING.** 2026-10-19. The owner then said to ignore the earlier rules constraints and the open decisions: make everything real-ish, then use a closer default zoom. Results are at the end; the sections below are the original measurement and plan. Trigger: owner screenshot (hero beside the Keep at night, current plan 013 build): "Is this the correct scale of buildings vs enemies and player? Looks weird." Presentation only: footprints, ranges, reach, speeds and every other rule stay as in `specs/002-game-rules.md` v1.2. It changes §17's scale sentence ("Keep about seven times their height, palisade about three") and the height bands in `plans/008-art-and-audio-bible.md`, so the spec is edited in the same change once decisions below are made.

## How it was measured

Every FBX in `Art/Models` imported in Blender 5.2 and its world bounds read (script: `Art/Source~/measure.py`). Feature sizes (doors, rungs, logs, crops) are from the source scripts; texture module sizes from each material's tiling and the procedural texture layouts. Figures are shown at model × enemy scale 1.35 × figure scale 0.27 = **0.3645** in both views.

**Yardstick:** the hero is 1.75 m. Its head top is about 0.205–0.22 tile shown, so **1 tile ≈ 8 m** (±5%). The board is ~160 m across; a 1×1 building plot is 8×8 m, which suits a cottage or a small tower keep.

## Measurements vs real reference

Real references are typical medieval/real-world values (approximate). Ratio = game ÷ real midpoint.

### Figures (shown size)

| Model | Game | Real reference | Ratio | Verdict |
|---|---|---|---|---|
| Hero | 1.75 m (yardstick) | 1.75 m | 1.0 | OK |
| Grunt | 1.6 m | 1.7 m | 0.95 | OK |
| Runner | 1.5 m (leaning) | 1.7 m | 0.9 | OK |
| Raider Archer | 1.7 m incl. hood point | 1.7 m | 1.0 | OK |
| Bomber | 1.45 m (leaning) | 1.7 m | 0.85 | OK |
| Brute | ~2.1 m | big man 1.9–2.1 m | 1.0 | OK |
| Siege Commander | ~3.3 m (body), banner to 4.6 m | if human, 1.9–2.1 m | **1.6** | Reads as a giant, not a commander: owner decision |
| Siege Golem | 3.4 m | fantasy | — | OK |
| Movement (rules) | hero 12 m/s, Grunt 8, Runner 14, Brute 4.9 | jog 3–4, sprint ≤10 | 2–3.5× | Too fast for true scale; rules, kept by owner. Animation already compensates with running strides |
| Hero reach / enemy engage (rules) | 4.9 m / 4.9 m | sword ~1.5 m | ~3× | Rules, kept; the step-in covers it |

### Buildings (overall)

| Model | Game | Real reference | Ratio | Verdict |
|---|---|---|---|---|
| Keep | 7.0 × 7.0 m, 14 m tall | small tower keep 8–12 m wide, 15–20 m tall | 0.8 | Plausible but **lower than the Archer Tower and Watchtower**: wrong hierarchy |
| Archer Tower | 7.8 m wide, 17.7 m tall, platform at 12 m | timber archer tower 8–12 m, platform 6–8 m | **1.7** | Too tall |
| Watchtower | 22 m tall | lookout 10–15 m | **1.7** | Too tall |
| Palisade (Walls) | 5.7–6.8 m tall | 3–4.5 m | **1.6** | Too tall |
| House | 7 × 6.3 m, 9.3 m incl. chimney | cottage, ridge 6–8 m | 1.3 | Slightly tall |
| Farm | 14.6 m square plot, shed 4.5 m | field plot, shed 3–4 m | 1.1 | Plot fine |
| Cannon pit | 14 m square, 8 m incl. gun | emplacement 8–15 m | 1.0 | Pit fine; the gun is not (below) |
| Repair Station | 14 m yard, crane 11.9 m | treadwheel crane 8–12 m | 1.1 | OK |
| Spike Trap | 6.4 m pit, spikes 1.1 m | stakes 0.5–1 m | 1.4 | Slightly big |
| Entry gate | 11 m wide, 14 m tall | war gate 4–6 m tall | **2.6** | Far too big |

### The details a person stands next to (the "weird" in the screenshot)

| Feature | Game | Real reference | Ratio | Where |
|---|---|---|---|---|
| Stone course height (Keep, footings, sills) | 1.0 m, blocks 1–2 m long | 0.3–0.45 m, blocks 0.4–0.9 m | **2.7** | texture 4 rows × tiling 2 |
| Timber plank / board width | 1.0 m | 0.2–0.3 m | **4** | texture 4 planks × tiling 2 |
| Shingle row | 0.63 m | 0.15–0.25 m | **3** | texture 8 rows × tiling 1.6 |
| Palisade log diameter | 0.85 m (joint post 1.1 m) | 0.25–0.35 m | **2.8** | `models.py` log radius 0.052 |
| Keep door | 3.2 m tall, threshold **1.6 m above ground**, no stair | 2.2–2.6 m, at ground or with a stair | 1.3 + floating | `models.py` keep(), plinth 0.2 |
| House door | 2.2 m, threshold 0.8 m up, no step | 2.0 m, one step | floating | `models_ext.py` house() |
| Wall torches (Keep, towers) | 1.2 m long, mounted at 3.4 m, flame 0.5 m (bloom makes it larger) | 0.5–0.7 m at ~2 m | **2** | `wall_torch` |
| Archer Tower ladder | rungs every 1.26 m, 1.1 m wide | rungs 0.3 m, 0.45 m wide | **4** | `tower()` |
| Tower timber posts | 0.6 m square | 0.2–0.3 m | **2.4** | `tower()`, `watchtower()` |
| Cannon barrel / wheels | 7 m long / 2.7 m wheel | 2–3 m / 1.2–1.5 m | **2.5** | `cannon()` |
| Crops | cones 1–1.6 m tall, 0.8 m wide | wheat 0.8–1.2 m, thin stalks | **width 5×** | `farm()` |
| Farm fence posts | 1.8 m | 1.0–1.3 m | 1.5 | `farm()` |

### Nature

| Model | Game | Real reference | Ratio | Verdict |
|---|---|---|---|---|
| Pine | 5–6.7 m | 15–25 m | **0.3** | Far too small: shorter than the House |
| Broadleaf | 5.2–6.5 m | 10–20 m | **0.4** | Too small |
| Bush | 1.1 m | 1–2 m | OK | |
| Large boulders | 5.5 m wide, 1.7 m tall | 1–3 m | 1.0–2 | Acceptable |
| Reeds | 2.2 m | 1.5–2.5 m | OK | |
| Grass (hero view) | 13–24 cm | 10–40 cm | OK | |
| Ford stones | 1.2 m | 0.5–1 m | 1.3 | OK-ish |

## Assessment

The hero, the ordinary enemies and the building **plots** are mutually consistent: at 1 tile ≈ 8 m a person standing at a 7 m Keep looks right. What makes the screenshot feel wrong is four things the eye reads against the person:

1. **Detail modules are 2.5–4× real size.** Stone courses, planks, shingles, logs, ladder rungs, posts and torches were sized so they would read at ~45 px per tile in the strategic view (plan 011). Next to a 1.75 m person they turn the Keep into a giant's tower: half-person-high stones, a 3 m door floating at chest height with no stair, torches as long as a person. This is the main cause.
2. **Height hierarchy is inverted.** The Archer Tower (17.7 m) and Watchtower (22 m) dwarf the Keep (14 m), and the palisade (6–7 m) is nearly half the Keep. The Keep should be the tallest building.
3. **Nature is undersized.** Trees are 5–7 m, shorter than a cottage, while towers are 18–22 m. The island looks like a model railway.
4. **A few oversized props**: the entry gate (14 m), the cannon (7 m barrel), and fat crop cones.

Rules-level mismatches (speeds 2–3.5× real, 4.9 m reach) stay; the owner kept the rules. Animation already hides most of it.

## Fix plan (staged; each stage built, captured and judged before the next)

**Stage 1: detail scale (largest effect, no change to silhouettes).** The owner allows different detail between views, never different proportions. So add real-scale surface detail that fades in close up:
- A shared building shader (URP lit, same passes as `Ground.shader`): today's albedo/normal as the macro layer at its current tiling (strategic look unchanged) plus a **real-scale detail layer** (0.35 m stone courses, 0.25 m boards, 0.2 m shingles), faded in by camera distance, so the hero view reads true module sizes and the strategic view keeps its clean read.
- Geometry fixes at real scale: the Keep door at 2.4 m with a stone stair up to it (or the door lowered to the ground with the plinth as a step); a step at the House door; torches 0.6 m mounted at 2.2 m with a smaller flame; ladder rungs every 0.3 m on a 0.45 m ladder; tower posts 0.25–0.3 m; palisade logs Ø 0.3 m (about 25 per tile edge). Gameplay footprints and pivots stay as they are; Wall log spacing is re-derived so neighbours still meet without gaps.

**Stage 2: height hierarchy.** Keep 16–18 m (≈2.0–2.2 tiles), the tallest; Archer Tower 10–11 m (≈1.3 tiles, platform at 7 m); Watchtower 13–14 m (≈1.7 tiles); palisade 3.5–4 m (≈0.45–0.5 tile); House ridge 7 m; gate 5–6 m. Keep the Keep visibly 1×1. Then update `HeroCameraRig.ObstacleHeight` (the camera's approximate height table), the arrow launch heights, torch-light positions and the selection/HP-bar heights that assume today's heights.

**Stage 3: nature and props.** Pines 12–15 m and broadleaves 10–12 m (≈1.5–1.9 tiles), slimmer trunks and higher crowns inside their cell; the cannon at real size (2.5 m barrel, 1.3 m wheels) on its pit; crops as thin stalk clumps about 1 m; fence posts 1.2 m.

**Stage 4: verification.** The existing `-capture-set scale` face-to-face lineup (side/front/back) plus 013's set at both resolutions; a strategic occlusion check (taller Keep and trees must not hide routes, gates, BREACH labels or enemies at default zoom); the measurement script rerun and its table stored with the evidence; the paired perf A/B; EditMode/PlayMode. The flat-board island test from 013 still guards footprints. Add a lean content check only if a concrete risk appears (for example, a model exceeding its cell).

Effort: stage 1 about 2–3 days, stage 2 about 1–2 days (camera table and anchors are the risk), stage 3 about 1 day, stage 4 about 1 day. All generated as code again; still not artist-approved art.

## Owner decisions needed before starting

1. **Tree height vs strategic occlusion:** real-ish trees (12–15 m) will hide more of the board behind them at the fixed 40° camera. Accept, or cap them at about 9–10 m (still taller than the House)?
2. **Siege Commander:** keep a 3.3 m giant (fantasy boss, reads strongly) or make it a 2.1 m armoured man with a big banner (consistent humans, weaker boss read)?
3. **Keep with a raised entrance and stair** (historically right, more detail) **or a ground-level door** (simpler, clearer)?
4. Still open from plan 013: how the strategic view keeps true-scale enemies readable (class markers vs closer zoom). Stage 2 lowers the palisade, which slightly helps enemy visibility at the walls.

## Results (2026-10-19)

Owner direction: real-ish everything, closer default zoom. Decisions taken: real tree heights (occlusion accepted), the Siege Commander as a 2.1 m armoured man, a raised Keep entrance with a stone stair. Movement speeds and reach are simulation values and were **not** changed (they need an explicit owner go-ahead; see below).

**Measured after** (`evidence/014/measure-after.json`, Blender bounds; figures × 0.3645; 1 tile = 8 m):

| Model | Before | After | Real reference |
|---|---|---|---|
| Keep | 14 m, 7 m wide | **19 m** incl. turret cones and finial (13 m walls), 7.8 m incl. turrets, inside its cell | small tower keep 15–20 m |
| Archer Tower | 17.7 m, platform 12 m | **11.8 m**, platform 7 m, rungs every 0.3 m, posts 0.3 m | 8–12 m |
| Watchtower | 22 m | **16.3 m** incl. pennant (lookout at 11 m) | 10–15 m |
| Palisade | 5.7–6.8 m, logs Ø 0.85 m | **3.6–4 m** logs (4.9 m joint post with cap), Ø 0.3 m | 3–4.5 m |
| House | 9.3 m, door 0.8 m above ground | **7.2 m** incl. chimney, 2 m door at ground with a step, 0.9 m windows | 6–8 m |
| Cannon | 7 m barrel, 2.7 m wheels | **2.9 m** barrel, 1.2 m wheels, 1.7 m parapet | 2–3 m, 1.2–1.5 m |
| Farm | crop cones 0.8 m wide | **thin stalk clumps ~1 m**, fence 1.2 m | wheat 0.8–1.2 m |
| Spike Trap | 1.1 m spikes, 9 | **0.6 m** stakes, 25 | 0.5–1 m |
| Entry gate | 14 m | **5.9 m** | 4–6 m |
| Pine / broadleaf | 5–7 m | **13 m / 11 m** (× placement 0.95–1.25) | 15–25 / 10–20 m |
| Siege Commander | 3.3 m body | **2.1 m** body (banner above) | 1.9–2.1 m |
| Wall torch | 1.2 m at 3.4 m | **0.6 m** at about 2.2 m | 0.5–0.7 m at ~2 m |
| Stone course / plank / shingle row (texture) | 1.0 / 1.0 / 0.63 m | **0.35 / 0.25 / 0.2 m** (material tiling 5.7–8) | 0.3–0.45 / 0.2–0.3 / 0.15–0.25 m |

Runtime anchors that assumed the old heights were updated: building HP-bar heights, the hero camera's obstacle-height table (Keep 4.6, forest 5.2, ...), arrow and shell launch heights (tower archer ~1.0, muzzle), the Keep's night light, enemy bar height and body height for the Commander. Found and fixed on the way: the tower archer had stood at ground level inside the tower legs since plan 011 (its geometry was never moved to the platform); it now stands on the platform.

**Camera:** default strategic view centred on the Keep at half-height 5.5 tiles (was 10.5), zoom range 2.5–14 (was 4–14). Spec `002-game-rules.md` v1.3 updated (§13 camera, §17 scale sentence).

**Verification (fresh, after the last change):** EditMode **116/116**, PlayMode **21/21** + 1 skipped. One PlayMode scenario (`FarmHotkeyPlacesFootprint...`) assumed the old whole-board default view (an on-map West label and a click on cell (5,5)); it now frames the whole board itself before its assertions, which are unchanged. Release build, captures at 720p/1080p (`evidence/014/captures`, 1080p). Visible-window stress, 1920×1080, two alternating rounds: strategic median 3.57 / 3.58 ms, p95 4.42 / 4.43, p99 4.85 / 4.86; hero camera 4.42 / 4.42 ms (1.24×). Gate passes.

**Not solved, needs a decision:**
- **Strategic readability:** at the new default zoom a person is about 10–14 px tall at 1080p. Enemies read as people moving along routes, not reliably by class, especially at night (`captures/dense-s.jpg`, `crossing-s3-night.jpg`). Options: a closer default (about 4: ~18 px, less of the board), or strategic-only class markers.
- **Speeds and reach stay 2–3.5× real** (hero 12 m/s, Grunt 8 m/s, reach 4.9 m). Making them real-ish slows every night by 2–3×, needs a rules version and a balance pass.
- Taller trees and the Keep hide more behind them from the fixed camera; not yet checked across many layouts.
- Repair Station, bridges, rocks and figures other than the Commander were already near real size and were not remodelled.
