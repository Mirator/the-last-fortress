# Plan 024 results: hero camera pivot steps off a touched Wall or Tower

Fixed 2026-10-28 against `6420c18`. This is a bug fix of the hero camera described in `specs/002-game-rules.md` §18 (camera paragraph); with the fix, that paragraph is v1.10. Presentation only: no simulation, save or settings change.

## Bug

Found by the agent's capture review of the Release build on 2026-10-28 (`-capture-set 023` and `hero-camera`):

- **Night hero view facing a Tower** (`023-dense-h-west`). The camera sat inside the Tower's base and the hero wasn't visible. Plan 015's capture of the same shot (`evidence/015/captures/dense-h-west.jpg`) showed the hero in front of the Tower.
- **Wall corner** (hero-camera frames 170–203). The camera distance dropped to 0.003, and the fallback (hold the last clear pose) ran every frame: 34 fallback frames. Plan 019's log for the same pose shows a distance of 0.5–1.0 and no fallbacks.

**Cause, measured with temporary logging (since removed):**
- The hero's camera pivot points must each keep the near-plane padding from every blocker box. That padding is 0.064 tile at a field of view of 60, and about 0.084 at 90.
- Since the owner's playtest round 2 on 2026-10-21 (plan 019), hero collision follows the drawn models, so the hero can stand 0.06 tile from a Tower's box and 0.047 from a Wall's.
- A hero touching either one therefore had no clear pivot. With no last pose (a fresh hero view), the camera went into the hero's head; otherwise it held a stale pose.
- The camera code itself had not changed since `702bd71`. The same broken frame is in the plan 023 before-capture, so it predates plans 022 and 023.

## Change

- `HeroCameraObstacles.StepOut`: moves a point sideways (X and Z) out of every box it is within the padding of, taking the shortest way out of each box. It fails if the result is not clear or the step is longer than the limit.
- `HeroCameraRig.Pivot`: a pivot candidate that is blocked now tries a step-out of at most 0.1 tile (`MaxPivotStep`) before moving on to the next candidate. The line from the head to the pivot is still checked, so a pivot never ends up on the far side of a surface.
- Rules v1.10 §18 records the step-out.

## Verification (fresh, 2026-10-28)

| Check | Result |
|---|---|
| Camera test with the new case: hero touching a Tower's east side, looking at it (`after/Camera.xml`) | Passed |
| Mutation: step-out disabled (`mutation/Camera.xml`) | Failed as intended: "against the Tower the camera fell back to a held or in-head pose". Fix restored. |
| Full PlayMode (`after/PlayMode.xml`) | 24 passed, 1 skipped (same skip as before) |
| Release build (`release-build.log`) | Succeeded, 0 errors, to `Builds/Release` |
| Hero-camera capture set, 280 frames (`captures/after-camera-log.txt`) | 0 fallbacks and 0 poses that are not clear; before: 34 fallbacks (`before-camera-log.txt`) |
| `023-dense-h-west` (`captures/before-` and `after-dense-h-west.jpg`) | The hero now stands in front of the Tower, framed as in plan 015 |

EditMode was not re-run, because nothing in Core changed.

## Limits

- **Hero with its back to a Wall.** When the camera looks away from a Wall the hero stands against, the space behind the hero is the Wall itself. The camera then shows from the shoulder, at a distance of 0, and the hero is drawn shadow-only. That is the documented close-camera behaviour, so it is no longer a fallback. It still doesn't show the hero (`after-wall-corner-175.jpg`). Showing the hero there would need a lift above 15° or seeing through the Wall; both are spec changes and were not made.
- **Coverage.** Only a Tower and a Wall were checked in captures. Houses, Farms, Watchtowers and Cannons use the same box rule and the same step-out, but were not filmed.
- **Owner live check is pending.**
