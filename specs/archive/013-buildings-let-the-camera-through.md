# Feature spec 013: no building pulls the hero camera in

Version 0.3, 2026-10-28. Status: **IMPLEMENTED and merged into `../002-game-rules.md` v1.15 section 18 (2026-10-28); archived.** Not current: plan from `002-game-rules.md`. The owner's live check is pending. Approved. The owner asked on 2026-10-28 for "Hero behind Towers": let Towers, the Keep and Houses fade instead of pulling the hero camera in. This reverses D2 of `archive/011-hero-view-occluder-fade.md` for those buildings. Changes `002-game-rules.md` v1.14, section 18. Presentation only.

## 1. Problem

After plan 029, Walls let the camera through. With the hero's back to a Tower, the Keep or a House, the camera is still pulled in to the hero's shoulder, and the hero is drawn shadow-only, so the player sees no hero (`plans/evidence/029/captures/tower-back-unchanged.jpg`).

## 2. Decision

- **No building is a camera blocker.** This covers every kind. The owner named Towers, the Keep and Houses. Farms, Cannons, Watchtowers and Repair Stations are included for consistency, since they would otherwise be the only buildings that still hide the hero.
- **What still blocks the camera:** trees, bushes and boulders. They still pull the camera in and lift it, as now.
- **The ground** still blocks.
- **Fading is unchanged** (spec 011 rules 1–3). A building the camera sits in or looks through fades to 30%.
- **The accepted consequence.** The camera may sit inside a building's box, for example the Keep, looking out through its faded walls. Faded meshes are culled from behind, so the near walls vanish rather than fill the view.

## 3. Acceptance

- With the hero's back to a Tower or the Keep, the camera stays at its set distance, and the hero is visible through the faded building.
- With the Keep behind the hero, the camera is no longer pulled in. The PlayMode camera test is updated from "the Keep pulls the camera in" to "the Keep fades and the camera keeps its distance".
- Tree and boulder behaviour, the ground, transitions and the near-plane checks are unchanged.

## 4. As built (2026-10-28)

- **Back to a Tower** (`013-dense-h-east`): the hero now stands in view through the faded Tower. Before, the camera was at the hero's shoulder and showed no hero.
- **The Keep behind the hero at the longest zoom (2.2)** (hero-camera frame 020). The camera now stays 2.2 back, so a large faded Keep fills most of the view, and the hero is small behind it. Before, the camera was pulled in close. A lower opacity while the camera is inside a building's box would reduce this; it is not done and is the owner's call.
