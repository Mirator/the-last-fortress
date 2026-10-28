# Plan 032: no building pulls the hero camera in

Status: **DONE** (2026-10-28; results below). Owner live check pending. Priority P1. Effort S. Implements **`specs/archive/013-buildings-let-the-camera-through.md` v0.3** (was `specs/013-...` v0.2 when planned) (approved 2026-10-28). Current rules: `specs/002-game-rules.md` v1.14, section 18. Baseline commit: `fe3a58d`.

## Steps

1. **`WorldView.CollectCameraBlockers`:** collect terrain props only. Building views are skipped, as Walls already are. `CollectFadeCandidates` is unchanged.
2. **Test.** `TheHeroCameraKeepsItsNearPlaneOutOfBuildingsAndSettles`.
   - The Keep assertion becomes: the camera keeps its distance, and the Keep fades.
   - The plan 024 Tower case gets a back-to-the-Tower check: the camera keeps its distance.

   Mutation-check it by restoring building blockers.
3. **Captures:** the hero-camera set and 013 (the night shot facing east was hidden behind the Tower). Then a full PlayMode run and a Release build.
4. **Close:** rules v1.15 §18, archive the spec, update the indexes and the results.

## Results (fresh, 2026-10-28)

| Check | Result |
|---|---|
| Camera test, updated (`evidence/032/after/Camera.xml`): the Keep behind the hero fades and the camera keeps its 2.2 distance; with its back to the Tower the camera stays over 0.9 back and the Tower fades | Passed |
| Mutation: buildings restored as camera blockers (the plan 029 code) (`mutation/`) | Failed as intended: "the Keep behind pulled the camera in", 0.45 instead of at least 2.15. Restored. |
| Full PlayMode (`after/PlayMode.xml`) | 24 passed, 1 skipped (same skip as before) |
| Release build (`release-build.log`) | Succeeded, 0 errors |
| Hero-camera set (279 frames) and 013 | 0 fallbacks, 0 poses that are not clear. Back to the Tower: the hero is visible. Palisades by the Keep: unchanged from plan 029. |

The test's old assertion, "the Keep behind pulled the camera in", described the rule this plan removes, so it was replaced, not weakened. The mutation shows the new assertion detects the old behaviour.

**Trade-off** (`captures/keep-behind-zoom22-before-after.jpg`): with the Keep behind the hero at the longest zoom, a faded Keep fills the view and the hero is small. This is recorded in the archived spec's as-built note.
