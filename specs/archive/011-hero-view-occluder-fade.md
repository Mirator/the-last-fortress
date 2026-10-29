# Feature spec 011: the hero view fades what hides the hero

Version 0.3, 2026-10-28. Status: **IMPLEMENTED and merged into `../002-game-rules.md` v1.13 section 18 (2026-10-28); archived.** Not current: plan from `002-game-rules.md`. The owner's live check is pending. Approved; the owner decided on 2026-10-28:
- buildings and trees fade;
- Walls let the camera through;
- the fade is see-through at 30%.

Changes `002-game-rules.md` v1.12, section 18 (Camera and focus). Presentation only.

## 1. Problem

The 2026-10-28 capture reviews found two problems in the hero view near the player's own fortress:

- **Palisades fill the view.** Next to the Keep, palisade stakes fill a third of the screen.
- **The hero can vanish.** With the hero's back to a Tower or Wall, there is no room for the camera behind them. Since the pivot fix of rules v1.10 the camera then shows from the shoulder at distance 0, and the hero is drawn shadow-only, so the player sees no hero.

## 2. Decisions (owner, 2026-10-28)

- **D1: What fades.** Buildings, trees and boulders fade while they hide the hero. The alternative, buildings only, was rejected.
- **D2: Back to a wall.** Walls are no longer camera blockers. The camera keeps its distance behind the hero, and the Wall it is in or behind fades. Towers, the Keep, Houses and the other buildings still pull the camera in, as now. The alternatives were rejected: letting the camera through every building, and fading without changing the camera.
- **D3: Look.** The faded object is see-through at 30% opacity. The alternative, a dither pattern, was rejected.

## 3. Rules

1. **Only in the hero view.** Fading applies only while the hero camera renders, including the entry and exit transitions. The strategic view never shows a faded object. Leaving the hero view restores everything at once.
2. **What fades.**
   - **Candidates:** each drawn mesh of a building (not its HP bar, smoke or glow), and each tree, bush or boulder mesh of a forest or rock cell.
   - **The rule:** a candidate fades while either holds:
     - its bounding box crosses the line from the camera to the hero's chest or head;
     - its box holds the camera's near-plane volume.
   - **Never faded:** units (enemies and the hero), effects, overlays and the ground.
3. **The fade.**
   - **Opacity:** a faded mesh is drawn see-through at 30% [P], lit by the main light and ambient light only.
   - **Timing:** it eases to 30% and back over 0.15 s [P], on real time, so it eases while paused too.
   - **Shadows:** a faded mesh casts no shadow while faded.
4. **Walls and the camera.** Walls are not camera blockers (§18): the camera neither pulls in nor lifts for them. Every other blocker is unchanged. Hero collision is unchanged.
5. **No other change.** The simulation, saves, settings and the strategic view are unchanged.

## 4. Acceptance

- **Back to a Wall:** with the hero's back to a Wall and looking away from it, the camera stays at its set distance behind the hero, the hero is visible, and the Wall is faded.
- **Between camera and hero:** a Tower or tree between the camera and the hero is faded, and the hero is visible through it.
- **Leaving the hero view:** after leaving, no mesh is faded.
- **Captures:** the existing capture frames of the hero by the Keep and at the Wall corner show the hero, before and after. The owner's look is the acceptance of record.

## 5. As built (2026-10-28)

- **Back to a Tower.** With the hero's back to a Tower, the Keep or a House, the hero is still not shown. Under D2 those buildings still pull the camera in, and the camera shows from the shoulder (`013-dense-h-east`). D2 accepted this; a later change would have to let those buildings through as well.
- **Rendering.** Faded meshes use run-time copies of `Materials/HeroFade.mat` (shader `TheLastFortress/HeroFade`), referenced by the release content so the shader reaches the player build.
