# Plan 015: walking speeds at real scale, then a four-way review

Status: **IMPLEMENTED (agent); owner play-test PENDING.** 2026-10-20. Implements `specs/002-game-rules.md` v1.4 (sections 1, 8, 16, 17).

## Why

After plan 014 put every model at real size (1 tile ≈ 8 m), the owner saw that people sprinted: the release-rules-2 speeds meant 8 m/s for a Grunt, 14 m/s for a Runner and 12 m/s for the hero. Owner: "slow the movement"; enemies should be closer to walking.

## What changed

- New rules version **`release-rules-3`** (default for new runs). Saves made under `release-rules-2` keep their sprint speeds and HP, and `release-rules-1` saves continue without a hero, as before. A save names its rules, and the build restores each with its own profile (`PrototypeBootstrap` catalog: rules-3, rules-2, rules-1, six-night MVP).
- Enemy speeds (tiles/s, whole 1/1200 units per step): Grunt and Bomber 0.3 (2.4 m/s brisk march), Raider Archer 0.25, Brute 0.2, Runner 0.6 (a real run), Siege Golem and Siege Commander 0.15. `ReleaseCampaign.Walking`.
- Enemy HP ×1.5 (`ReleaseCampaign.WalkingHpScale`). The first guess, ×2.5, overshot: slower enemies bunch at the walls, so more of them hit at once.
- Hero 0.6 tiles/s (36 units per step; was 90; raised to 0.8 by the review below), carried on `Hero.MoveUnits` by rules version. The view's prediction uses the same value.
- Unchanged: spawn cadence, tower and building stats, economy, perks, hero swing, reach and engage distances.

## Verification

- Scripted approaches, active hero, seeds 1–8, each profile's own orientation (`evidence/015/`): release-rules-2 8/8, 8/8, 8/8; release-rules-3 with HP ×2.5 2/8, 0/8, 0/8; with HP ×1.5 **kill-zone 8/8, corridor 4/8, economy 7/8**. A hero alone still falls by Night 2 in both.
- EditMode **117/117**. Two existing tests mirrored the build's rules catalog without the new profile and were updated to the real catalog, with their assertions unchanged. One test now uses its profile's own definitions. New test `ASprintSpeedSaveKeepsItsSpeedsAndNewRunsWalk` protects save compatibility (old saves keep sprint speeds; new runs walk).
- PlayMode **21/21** + 1 skipped. In one scenario the Raider Archer now comes from the undefended east: at walking speed it died crossing the Cannon's cover before it could shoot, which is what the assertion checks.
- The capture script's scripted hero walks scale with the hero's speed, so captures reach the same places.

## Review (four read-only reviewer agents on the fresh build)

Graphics, UI/UX, animation and smoothness, and gameplay each reviewed the release-rules-3 build from captures (013 set, 008 menus/threats, 330-frame motion flipbook) and code. The gameplay reviewer also compiled Core headless and replayed the scripted approaches. Reviews are not owner approval.

### Done (fresh build, `evidence/015/captures`)

| Area | Finding (reviewers) | Change |
|---|---|---|
| Gameplay | Brute beat the hero one-on-one; Brutes and Archers caused nearly all Keep damage, so under-built defences failed in one night | Brute HP 180, Raider Archer 45 (release-rules-3 only) |
| Gameplay | Hero at 0.6 tiles/s could not catch a Runner or reach a lane in time | Hero 0.8 tiles/s (48 units) |
| Gameplay | release-rules-3 was covered by no checked-in test | The hero balance guard now plays the current rules: active hero 7/8, 5/8, 8/8; a hero alone falls by Night 3 on every seed (`hero-policy-runs-r3.txt`) |
| UI, graphics, animation | HP bars were strategic-size slabs across the hero view (all three) | Bars keep a constant on-screen size in the hero view; enemy bars at half width over true-scale figures in the strategic view |
| UI, graphics | Build cards covered the fight at Night | Cards fade out and click through at Night, Reward and the end screens |
| UI | Gate tags under the speed buttons and the Hero panel | Tags low on screen stay between the corners |
| UI | Help lacked the hero controls; "Reset Camera: whole board" was wrong | Hero controls line; Reset Camera returns to the Keep; new **Z** shows the whole board |
| UI | Hero hint wrapped out of its panel; "1 enemies"; paused status lost the clock | Shorter hints, singulars, "NIGHT n 23.7 s — PAUSED" |
| Graphics | Boulders, cliffs and far cliffs used the cut-masonry texture | Rock texture (`rock_albedo/normal`), tiling 1.3 |
| Graphics | No anti-aliasing | 4x MSAA (URP asset) |
| Graphics | Keep windows clipped to white at night | Window emission 2.4 → 1.15 |
| Graphics | Grass ignored torchlight | Grass shader takes additional lights (Forward+ loop) |
| Graphics | Arrows ~7 m, shells 2 m, smoke per frame | 1 m arrows pointing along their arc, smaller shells, smoke by time |
| Graphics | Spike Trap read as a garden bed; Repair Station as a gallows | Pale stake points and a hazard-red rim; angled crane jib with a stay |
| Graphics | Rivers went black at night | River night tint lighter than the sea |
| Animation | Enemy step-ins slid metres every blow | The engaged figure walks the last gap at its own pace (feet stepping) and leans in a fifth of its height per blow |
| Animation | Hero swing: 3.6 m teleport step, two 45° yaw pops, instant 180° turn | Step of at most 0.12 tile toward the struck enemy; swing eased in and out; 2000°/s turn |
| Animation | A shot body froze mid-stride until the arrow landed, then fell in 0.1 s and deflated | Keeps walking until the hit lands; ease-in tip over 0.45 s, lies, sinks at full size (1.15 s) |
| Animation | Hero kills fell before the blade arrived; their sparks were skipped | Kill waits for the blade's contact; sparks at the falling body |
| Animation | Whole-model bob lifted planted feet; stride assumed human legs | No model bob on rigged figures; hip bob in walk/run phase; step length from the real leg (0.39 H walk, 0.81 H run), swing 36°/50° |
| Animation | Swing and reach arcs at knee height; enemy arrows at the hero landed 3.6 m up; the cannonball pile orbited the gun | Arcs on the ground; chest height; pile stays on the deck |
| Captures | HUD overdrawn by ground in offscreen captures; threat shots showed Defeat | Canvas plane in front of everything; the threat showcase plays under release-rules-2 (its fortress and step budgets were written for it) |

Tests (fresh, after the last change): EditMode **117/117**, PlayMode **21/21** + 1 skipped. One PlayMode check measured the removed whole-model bob as its evidence of a gait; it now reads the gait amount (new `WorldView.EnemyGait`), same intent. Performance, visible window 1920×1080, two alternating rounds (`evidence/015/perf`): strategic median 4.72 / 5.19 ms, p99 7.4 / 10.0; hero 6.07 / 5.59 ms (1.28× / 1.08×). MSAA costs about 1.2–1.6 ms; the gate (16.7 / 20 / 33.3, hero ≤ 1.5×) passes.

### Bomber lights on contact (owner decision, after the review)

Under release-rules-3, a Bomber lights its fuse at any cell centre beside a living solid building. It targets the Keep first, then a Wall, then the lowest ID. Before, it lit only at its route's attack cell. Spike Traps are not solid and do not trigger it, and release-rules-1 and -2 saves are unchanged (`ReleaseCampaign.BombersLightOnContact`). New EditMode case `UnderWalkingRulesABomberLightsAtTheFirstBuildingItWalksUpTo` covers both rules versions. The policy log now counts fuses and blasts.

The comparison ran in one session with the same code, with the rule switched off and then on, active hero, 8 seeds per approach (`evidence/015/hero-policy-runs-r3-bomber-before.txt` and `-bomber.txt`):

| Approach | Wins before → after | Fuses | Blasts |
|---|---|---|---|
| kill-zone | 7/8 → 8/8 | 1 → 14 | 0 → 12 |
| corridor | 5/8 → 5/8 | 4 → 4 | 3 → 3 |
| economy | 8/8 → 8/8 | 0 → 28 | 0 → 16 |

Blasts went up tenfold, but they land on outer buildings, and a Bomber that blows up there never reaches the Keep. Wins did not drop. Corridor builds kill every Bomber before it touches anything. In about 24 × 49 spawned Bombers, roughly 3% detonate, so the threat is still small.

### Open: owner decisions

1. **Strategic readability** (all reviewers): at the close default zoom enemies are still dots at night and classes do not read. Recommended: strategic-only class markers (a small kind-coloured, kind-shaped ground mark at a fixed screen size), proportions unchanged.
2. **Bombers are still a weak role.** The owner chose to have a Bomber light its fuse at the first solid building it touches; this is done (see below). Under the gameplay reviewer's count, 4 of 1,067 Bombers lit a fuse. Now about 3% detonate. Next levers: Bomber HP or speed, or tower targeting.
3. **Hero view locked to 1×** while playing the hero now decides outcomes (active vs idle: 19 vs 14 of 24 wins); consider allowing 2×.
4. **Bosses are slow HP sponges** arriving after their wave; try boss spawn at 20 s instead of 30 s.
5. Smaller ones: show the route faintly at Night; trim the new-enemy announcement; the hero camera tilts down near the Keep (lift applied as pitch); Spike Trap damage per entry fell behind towers at walking speed (needs a release-rules-3 building override).

Not done because they need a live look first: real-speed motion review of the new step-ins, gait and deaths (stills and a 1/30 s flipbook only).
