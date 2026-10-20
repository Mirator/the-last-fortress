# Plan 012 results — third-person hero and switchable views

2026-10-19. All four stages implemented on `main` (uncommitted at the time of writing). Spec `specs/archive/006-third-person-hero.md` (owner decisions 1-13) is merged into `specs/002-game-rules.md` section 17 and archived. Evidence: [evidence/012](evidence/012/).

## Verification (fresh runs after the last code change)

| Check | Result |
|---|---|
| Baseline before any change | EditMode 106/106, PlayMode 20/20 + 1 skipped (motion probe, as usual) |
| EditMode, final | **115/115** (+7 hero rules, +1 pre-hero save, +1 hero balance guard; settings migration test extended) |
| PlayMode, final | **21/21** + 1 skipped (+1 view-switch scenario) |
| Release build (batchmode) | Succeeded, 0 errors; content validation 0 errors, now requires the hero prefab and its rig. Main-light shadows render in these batchmode builds (hero and tree shadows in the Day captures), unlike the plan 011 note |
| Captures | Release build, offscreen, 1280×720 and 1920×1080: hero view Day/Night on Ravine, Crossing, Meadow toward the board edge and the Keep, plus the strategic view (`evidence/012/captures`, 720p set kept). Taken one build before the final one, which only added the dimmed 2×/3× buttons, the `-hero-camera` option and the prediction/title-ring fixes below. The taller settings panel (1010 of 1080 units) has no capture yet |
| Performance, visible window, 1920×1080, paired and alternating | Strategic median 3.69 / 3.70 ms, p99 4.5 / 4.4 ms; hero camera median 3.88 / 3.90 ms, p99 5.1 / 4.5 ms. Hero ≈ 1.05× strategic (gate ≤ 1.5×), far under 16.7 ms. One 49.8 ms spike in the first hero run only. Hidden-window runs measured ~0.7 ms (no rendering) and are kept only as CPU evidence |

Tests and builds ran in batchmode with the Editor closed. Batch runs keep rewriting `ProjectSettings/ProjectSettings.asset` (drops `SENTIS_ANALYTICS_ENABLED`) and `EditorBuildSettings.asset`; those side effects were restored, not committed.

## What was built

- **Core** (`Runtime/Core/Hero.cs`, `RunSimulation.cs`): `release-rules-2` with the hero as a simulation entity; integer positions and direction tables; per-step `HeroCommand`; spawn rule, collision with corner handling, Day clock, stepping aside for a new building; swing, guard, melee engagement at decision points, provoked Raider Archers, hits with the Commander aura, death and respawn; hero-kill counts; Start Night position in the checkpoint. `release-rules-1` saves load without a hero. MVP and prototype untouched.
- **Views** (`HeroCameraRig.cs`, `HeroPresenter.cs`, `HeroSky.cs`, bootstrap, `PlayerController`): Tab switch, second perspective camera, input map, 1× lock without resuming a pause, cursor capture, first-frame prediction, sky dome, wide sea, fog toggle, health bars facing the active camera, screen anchors projected with it, Keep-hit edge tag, hero HUD, settings v3 rows.
- **Art**: `Art/Source~/models_hero.py` (Blender, as code) → `ArtHero` on the shared rig, imported by `ArtSetup.RunHero` without touching other art.

## Balance (scripted, logged; balance is not the current focus)

Hero swing tuned 15 dmg / 0.6 s → 10 / 0.75 s (owner decision 13). Hero alone loses by Night 3 on all 24 seeds (asserted). Idle hero matches the no-hero outcome on 23/24 (one economy loss became a win). Active hero share of kills on Nights 6-12: average 15% / 13% / 7% (kill-zone / corridor / economy), single-night peaks up to 41% (logged only, by decision 13). Log: `evidence/012/hero-policy-runs.txt`.

## Deviations from the plan and spec

- Leaving the hero view is instant; only entering has the ≤0.35 s camera transition.
- Camera collision is a read-only grid query by approximate heights, not presentation colliders (spec allows an equivalent query). Against tall buildings the camera first rises over the shoulder, then pulls in.
- In the hero view the route overlay (lines, chevrons and the entry gates, which belong to it) and the build cards are hidden; the coral entry tags still show. The hero panel hides while a building is selected in the strategic view (they share the corner).
- `AdvanceSimulation` returns the hero's Day steps during Day (previously always 0 by Day).

## Close-range art audit (from the 15 captures)

| Finding | Status |
|---|---|
| Hero, enemies and buildings read well at eye height; no missing faces or undersides seen in the captured angles | OK (not every model from every angle) |
| Sky gradient and sea reach the horizon by Day and Night | Fixed in this plan |
| Fog does not show in the build (likely stripped shader variants; it is enabled only for the hero view) | **Pending**: check Graphics fog stripping, or drop fog |
| The board is a perfect square plateau with straight cliffs; reads artificial from eye height | Deferred: art direction decision |
| In the hero view an entry tag can sit behind the speed buttons at the screen edge | Deferred: minor HUD layout |
| Camera ended inside towers next to the hero | Fixed (rise, then pull in) |
| On-top route lines cut across buildings | Fixed (hidden in the hero view) |

## Owner feedback round (2026-10-19, after the first play)

| Feedback | Change |
|---|---|
| Mouse up/down inverted | Mouse up now raises the camera and looks down at the hero; the Invert setting flips it |
| No way to start the wave from the hero view | Enter starts the night from Day in either view; the hero-view hint shows it |
| Space should jump, not pause | Space jumps in the hero view (presentation only); P pauses there |
| Castle too small next to the player | Hero and enemies are drawn at half size in the hero view (a person ≈ a quarter of the Keep); the strategic view is unchanged |
| Hero view should be more zoomed in | Camera distance 0.9–3.5, default 1.8 (was 3–7, default 5), pivot and shoulder sized to the smaller figure |
| Attack feels weird | Body faces where it runs and turns to the aim to swing; body twist through the slash; a ground arc shows the resolved reach and angle; struck enemies flinch with sparks; reach 1.2 → 1.0 tile |

## Review round (2026-10-19): gameplay and graphics reviewers

Two read-only reviewer agents (gameplay with Core probes, graphics/smoothness from code and captures). Done in this round:

| Area | Change |
|---|---|
| Rule | Melee enemies hit the hero only within the hero's own reach (1.0); the due hit waits (EditMode test extended) |
| Camera | Lift smoothed, capped at +25°, pull-in first; rock height 0.5; wheel zoom eased both ways; Tab-in starts at a matching perspective distance instead of 40 units out |
| Motion | Shown hero rate-limited (4.5 tiles/s) toward the prediction; body faces the steered heading; calmer gait |
| Combat feel | Sparks, flinch and impact sound at the shown blade contact (0.08 s); front-loaded strike and twist; slash arc anchored where the swing was resolved; faint reach arc while steering; 0.25 s click memory |
| Controls/HUD | P pauses in both views; previous speed restored on leaving the hero view; hints mention click and guarding; hero kills in recap and summary; entry tags kept above the speed block; utility panel hidden in the hero view |
| Audio | Swing whoosh, blade impact, heavy/normal hit, fall (existing clips) |
| Fog | Build keeps linear fog variants (`GraphicsSettings` fog stripping custom, linear only); fogged hero camera warmed up on the first frame (skipped without a GPU) |

Verified: EditMode 115/115, PlayMode 21/21 + 1 skipped, release build, 720p hero captures (`TestResults/012/captures-r2`). Fog effect not confirmed in stills.

Not done, for the owner to decide: melee enemies fighting back while hitting buildings (owner: keep melee as is); 64-direction movement (a rules change); a hero-view minimap or route overlay; a hero alone clears Night 1 on 18/24 seeds (onboarding/pillar risk); half-size enemies are small at mid range.

## Owner feedback round 3 (2026-10-19)

| Feedback | Change |
|---|---|
| Mouse still inverted | After trying both, the owner chose: by default (Invert look off) mouse forward looks down; Invert look on makes it look up. A PlayMode assertion locks the default |
| Buildings not scaled properly | Face-to-face captures (`-capture-set scale`, before/after in `TestResults/012/scale-*`): the hero was as tall as the palisade and a third of the Keep. Hero-view figures now 0.27 of strategic size: hero ≈ 0.22 tile, Keep ≈ 7× its height, palisade ≈ 3× |
| Player too fast | 3.0 → 1.5 tiles/s; reach 0.6, engage/hold 0.6/0.9, camera pivot, distance (0.5–2.2, default 1.0), jump and glide speed scaled to match; camera lift capped at 15° |

Afterwards: buildings no longer bounce (squash on a hit, rise when a tower fires) and Keep-hit camera shake is off by default (owner feedback).

New at this scale: ground tufts and flower clumps read knee-to-waist high (art pass); half-size enemies became quarter-size, so mid-range readability needs a look in play.

## Needs a human

Live feel of steering, swinging and the camera (mouse look, transitions); whether the hero makes nights more fun without making placement matter less; HUD clutter in motion; motion sickness at the default FOV. None of these are established by tests or captures.
