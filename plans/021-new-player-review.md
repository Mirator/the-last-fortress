# New-player review: both views (2026-10-21)

Three reviewers looked at the `release-rules-4` build after the owner's playtest changes (touch hits, bounded pursuit, routes that turn late):

- a tower-defense veteran;
- a third-person action player;
- a first-time casual player.

None of them played live. They only read files and looked at fresh captured frames (`game/The Last Fortress/TestResults/019/review/`), the owner's two screenshots, the specs and the scripted balance logs. Motion judgements are inferred from 1/30 s still frames and per-frame logs. Each item below says whether I checked it in the code.

## Highest-impact findings

| # | View | Finding | Evidence | Checked | Suggested change |
|---|---|---|---|---|---|
| 1 | Hero | **Enemies are drawn up to ±0.12 tile from where the simulation has them** (a fixed per-ID lane offset so columns read as several bodies, applied even while fighting the hero). With a 0.225 reach, what you see is not what you can hit. This likely explains the owner's "I couldn't hit it" better than the aim arc does. | `WorldView.cs` (offset ±0.12 per axis, applied at `t.position = pos + view.Offset`) | Yes, code | Ease the offset to zero when an enemy is engaged with, chasing, or within about 0.5 tile of the hero. Presentation only. |
| 2 | Hero | **The shown body and aim turn in 5.6° steps.** They follow the command's 64-way facing, not the camera's continuous yaw. Each step makes a one-frame turn, and the turn rate also twitches the legs. This is the strongest code-level cause of the "feels like 20 fps" impression. | `HeroPresenter.cs` (`aim = c.Facing * 360/64`); `HeroAnimationRig` gait uses `TurnRate`; hero log frames 000–029 pulse with each facing step | Yes, code | Drive the shown aim and body yaw from `HeroRig.Yaw`; keep the 64 headings for the simulation only. Use the turn rate in the gait only when not moving and above a threshold. |
| 3 | Both | **Chase exploit.** The hero runs at 0.7 tiles/s; Grunts move at 0.3 and Brutes at 0.2. A chase lasts 4 s, with only a 2 s cooldown, and Spike Traps fire both while chasing and on the walk back. The hero can kite enemies and pull them back and forth over traps. This conflicts with "routing never loops to farm trap damage" (rules §7), and chased enemies leave the route the towers were placed for. | `RunSimulation.StartsPursuit/PursueStep/MoveOff`, rules §18 | Yes, code; not observed in play | Limit chases to one per enemy per night (or a short total). Stop the walk back from triggering traps. Consider letting proximity chases start only when the enemy is near its route, not anywhere. Re-run the policy check afterwards. |
| 4 | Strategic | **At default zoom, enemies are nearly invisible at Night.** True-scale dark figures on dark ground: four alive, none readable. This breaks the vision goal "enemy classes distinguishable at low zoom". | `012-*-5-strategic-night.png`, older `016` night captures | Frames | Add class-coloured ground markers or rings for enemies in the strategic view only, keep route lines at Night, and optionally show a count per entry. |
| 5 | Hero | **The struck enemy is hidden behind the hero's body at contact.** Enemies hold about 0.15 tile away and the camera sits directly behind the hero, so the strike pose and its target are hidden. | Frames 236, 237, 250, 261; owner screenshot 1 | Frames | A wider shoulder offset (hero at about 40% of screen width), or a slight camera yaw toward the committed attack during wind-up. Optionally let enemies stand nearer 0.20 tile. |
| 6 | Hero | **Hits have little weight.** No hitstop, no camera impulse, no victim recoil; sparks are small; contrast is low at night. A Grunt takes 5 swings (45 HP / 10 damage). | Frames 237, 248–260; `PrototypeBootstrap` shakes only on Keep hits | Partly | Presentation only: a 40–60 ms hitstop on hero and victim, a small camera impulse in the hero view, a ~0.03 tile cosmetic recoil, a brighter hit flash. Leave damage and HP to the balance pass. |
| 7 | Hero | **The camera distance snaps in hard steps.** Examples: 2.20 → 0.71 in one frame at the Keep; 0.605 → 0.358 at the forest, where the feet leave the frame. The entry transition passes the Keep with the view filled by it. | Camera log frames 007→008, 143–147, 211→212, 270–273 | Log | Keep the immediate pull-in only when the near plane would clip; otherwise pull in over about 0.1 s. Raise the pivot or pitch at short distances so the feet stay in frame. Route the entry transition over obstacles. |
| 8 | Hero | **No orientation aids.** There are no route lines, no Keep marker and no hit-direction warning: one hit came from behind the camera with no cue. Entry tags look like Keep-hit tags. | `012-*-3/4`, frame 312, owner screenshot 1 | Frames | Faint route chevrons, a Keep marker or compass, a hit-direction indicator, and tags in the form "West: 3". |
| 9 | Both | **The hero is never introduced.** All five guide steps are strategic. "Guarding · Tab hero · H find" is unexplained. Space pauses in one view and jumps in the other. In Reward the hero hint still says "Enter night · 1-8 build". | `OnboardingState.cs` steps; `HudView.Hero.cs` hint logic; frame 560 | Yes, code | Add one guide step for Tab and the hero, make P the pause key in both views, give Reward its own hint (or none), and reword "Guarding". |
| 10 | Strategic | **Build cards cover the board.** The eight cards take about 45% of the width at bottom centre, over the Keep and the West route. The hero panel overlaps an entry tag. Text uses jargon ("(0.0–13.5 s)", "60, 2x to Walls"). | Owner screenshot 2, `016/after-crossing-s4` | Frames | A compact single-row bar, or cards that collapse while a tool is held; plain-language threat text; keep world tags clear of HUD panels. |

## What already works (all three agreed)

- **Day threat information and the route preview.** The coral entry pad, cyan line and chevrons make "buildings change the path" obvious; the Day banner and build cards are clear.
- **Map identity.** Crossing's rivers and fords and Ravine's ridges change where you build.
- **The hero helps without taking over.** It takes 4–8% of kills on Nights 6–12, and a hero alone falls by Night 3. Movement is smooth and responsive in the logs (about 0.1 s to start, through rest on reversal), the strike pose lands on the contact frame, and pause freezes it.

## Notes and limits

- One reviewer noted that the scripted "control" baseline is `release-rules-1`, whose enemy speeds and HP differ from release-rules-3/4. So control-vs-idle gaps (for example corridor 7/8 vs 3/8) don't measure the hero. The V3-vs-V4 comparison is the meaningful one.
- Not reviewable from stills: real-time frame pacing, sound, cursor capture, how switching views feels.
- Two reviewers said `specs/002-game-rules.md` §18 was out of date. They read an older copy: §18 already has reach 0.225, touch hits, pursuit and turn-late routes.

## Suggested order

1. Items 1 and 2: presentation-only, small, likely fixes for the owner's two complaints.
2. Item 3, the chase-exploit limits: a rules change; needs the owner's OK and a policy re-run.
3. Items 4, 5 and 7: visibility and camera.
4. Items 6, 8, 9 and 10: feel and onboarding polish.

## Done after the review (2026-10-21)

- Item 1: an enemy that is fighting or chasing the hero, or is within 0.5 tile of it, eases its drawn lane offset to zero, so it is shown at its true position (`WorldView`).
- Item 2: the hero's shown aim and body follow the camera's continuous yaw; the 64-way facing is only what the simulation reads. Turning on the spot moves the feet only above 150°/s while standing (`HeroPresenter`, `HeroAnimationRig`, `PrototypeBootstrap`).
- Fresh: scene tests 8/8 (`evidence/019/review-fix/Scene.xml`); build Succeeded, 0 errors, 2026-10-21 08:57. Not yet checked live by the owner.
