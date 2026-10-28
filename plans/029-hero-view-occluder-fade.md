# Plan 029: hero view fades what hides the hero

Status: **DONE** (2026-10-28; results in section 4). Owner live check pending. Priority P1. Effort M. Implements **`specs/archive/011-hero-view-occluder-fade.md` v0.3** (was `specs/011-...` v0.2 when planned) (approved 2026-10-28: D1 buildings and trees, D2 Walls let the camera through, D3 see-through at 30%). Current rules: `specs/002-game-rules.md` v1.12, section 18. Baseline commit: `780515e`.

## 1. Pre-check

Paths are under `game/The Last Fortress/Assets/TheLastFortress/`.

- **Candidates.** `WorldView.CollectCameraBlockers` already walks exactly the candidate renderers (building views without HP bar, smoke or glow, and forest/rock cells), and `CameraBlockerVersion` changes when they do. A sibling method returns the renderers themselves.
- **Materials.** Buildings and props use URP Lit (opaque, `_BaseMap`, `_BaseColor`). A transparent URP Lit variant is not used by any material, so build stripping could drop it. Instead, a small shader `TheLastFortress/HeroFade` is added, with these properties:
  - URP forward pass, transparent;
  - base map × base colour, lit by the main light and SH ambient, with fog;
  - `_Alpha`.

  It reaches the build through a material referenced by the release content (`content.heroFade`). That reference is assigned by a narrow editor step, `ArtSetup.RunHeroFade`, like `RunCompactCannon`.
- **Where it runs.** `PrototypeBootstrap.SyncViews` already handles the hero camera each frame, after `HeroRig.Follow`. The fade runs there, with `heroCameraShown`.
- **Walls.** `CollectCameraBlockers` skips building views of kind Wall (rule 4). Hero collision is in Core and stays unchanged.

## 2. Stages

1. **Shader, material and content field.** Run `ArtSetup.RunHeroFade` in batch mode.
2. **`HeroOccluderFade`:**
   - per-renderer alpha, eased over 0.15 s;
   - per-source-material fade copies (base map and colour copied), with originals restored at alpha 1 and on leaving the hero view;
   - `FadedCount` for tests.
3. **Walls stop being camera blockers.**
4. **Test.** Extend `TheHeroCameraKeepsItsNearPlaneOutOfBuildingsAndSettles`:
   - with the hero's back to a Wall, the camera stays at least 0.5 behind and a mesh is faded;
   - after Tab, nothing is faded.

   Mutation-check it.
5. **Captures.** Run the hero-camera set, 013 and 023 (Wall corner, by the Keep, west of the Tower), and compare them with the 2026-10-28 frames.
6. **Close.**
   - Full PlayMode run and a Release build.
   - Merge the rules into `002-game-rules.md` §18 (v1.13), archive the spec, and update the indexes and the results.

## 3. Risks

- **Transparent sorting.** Two faded meshes, or a faded mesh in front of the sky dome, may sort wrongly at 30%. This is accepted under D3 and is checked in the captures.
- **Night lighting.** The faded ghost ignores torch and window point lights. At 30% this is expected to read fine; checked in the night frames.
- **Cost.** Per frame, about a few hundred box tests against two segments, only in the hero view. Measured with the stress hero view if the captures show a hitch.

## 4. Results (fresh, 2026-10-28)

| Check | Result |
|---|---|
| Camera test (`evidence/029/after/Camera.xml`). New case: the hero just outside a Wall's drawn box, looking away from it | Passed |
| Mutation: Walls back as camera blockers (`mutation-walls/`) | Failed as intended: "the Wall pulled the camera in" (distance 0.02). Restored. |
| Mutation: nothing ever fades (`mutation-fade/`) | Failed as intended: "the Wall hiding the hero is not faded". Restored. |
| Full PlayMode (`after/PlayMode.xml`) | 24 passed, 1 skipped (same skip as before) |
| Release build (`release-build.log`); fade material setup (`fade-setup.log`) | Succeeded, 0 errors |
| Hero-camera capture set, 279 frames (`captures/camera-log-after.txt`) | 0 fallbacks, 0 poses that are not clear |

The first version of the Wall case put the hero inside the Wall's box. Both mutations still failed, but the Walls one failed only at a later check, because the camera escaped sideways. The case now measures the drawn palisade and stands the hero 0.08 tile outside its thin side, so each mutation fails at its own assertion.

**Frames** (`evidence/029/captures/`, before vs after, offscreen at 1440x900):
- **By the Keep, Day and Night:** the camera keeps its distance, the palisade beside it is see-through, and the hero is shown.
- **The plan 024 Wall corner** (back to the Wall): the hero is shown and the Wall is faded. Before, the camera was at the hero's shoulder with no hero.
- **Back to a Tower** (`tower-back-unchanged.jpg`): unchanged, as decided under D2.

Not measured: performance in the hero view (box tests against about 50-300 meshes per frame, hero view only). The stress hero view was not re-run.
