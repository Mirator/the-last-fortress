# Plan 023: class markers and a zoom cap for strategic readability

Status: **DONE** (executed 2026-10-28; see [023-results.md](023-results.md); owner accepted D1-a, D2-a, D3-a). Owner review of the captures pending. Original planning status: TODO, pending owner confirmation of spec 009 (D1–D3). None of them changes the shape of the code: D1 is a fade-in threshold, D2 an always-on flag, D3 the `DefaultSize` constant. Stages 0–2 may start on the recommended defaults (D1-a, D2-a, D3-a = 4.5); confirm all three before stage 4's owner review. Priority P1 (the owner's own playtest complaint). Effort M.

Implements **`specs/archive/009-strategic-figure-readability.md` v0.3** (approved 2026-10-28; implemented): option C (class markers) + D (zoom cap), with F (closer default zoom) per D3. Current rules: `specs/002-game-rules.md` v1.8, sections 13 and 17. Baseline commit: `93c83d1`.

Spec section 5 is the contract, and this plan does not restate it. Values marked [P] there are set from captures in stage 4, not guessed in code review.

## 1. Pre-check (done while planning, 2026-10-28)

Paths below are under `game/The Last Fortress/Assets/TheLastFortress/Runtime/Unity/`.

- **The enemy view has the right shape for a marker.** `WorldView.CreateEnemyView` (`WorldView.cs:1593`) already builds the per-enemy children: HP bar, Burn, Fuse and Aura. The marker is one more child of `Root`.
  - Two scales sit on `Root`: `content.enemyScale` (1.35) on `Root` itself and `CharacterScale` on `Model`. The marker's world size must divide by the `Root` scale.
- **The hide rule exists.** The Burn marker hides with `!HeroCameraShown` (`WorldView.cs:1320`); the marker uses the same gate.
- **Death and fade are handled.** `StartDying(..., v.HpBar, v.Burn, v.Fuse, v.Aura)` (`:1363`) hides the listed children; add the marker to that list.
- **The lane offset is on `Root`.** `t.position = pos + view.Offset * view.OffsetIn` (`:1289`), so a child marker follows the shown position as rule 1 requires.
- **No orthographic size term exists yet.** `BarScale` (`:1772`) returns the constant `strategic` for an orthographic camera; only the perspective branch keeps bars a constant size on screen. Add an orthographic term (camera half-height ÷ `CameraRig.DefaultSize`), shared by markers and enemy HP bars (rule 5).
- **Hero ring.** `HeroPresenter.cs:125/337` is world-fixed (about 1.2 tiles). No change; it is only checked in the captures (rule 7).
- **Zoom cap.** `CameraRig.cs:19` `MaxSize = 14f`. Change it to `WholeBoardSize` (10.5).
  - `SetView` clamps (`:73`), so `CaptureSequence.cs:334`'s `SetView(DefaultFocus, 12.5f)` would quietly become 10.5. Change it to `WholeBoardSize` and note it in the results, since old capture sets at 12.5 are no longer reproducible.
  - `PrototypeBootstrap.TitleSize` is 7, which is unaffected.
- **Reserved colours, from `Palette.cs`:** RouteCyan `79C7C7`, WarningCoral `E67961`, Amber `E9B45F`, and the burn orange (`1, 0.55, 0.12`).
  - The class cloth tints are in `Editor/ArtSetup.cs:150–168`.
  - The Runner's cloth (`D39A4C`) sits close to Amber and the Grunt's (`AE5747`) close to Coral. Both need lightening or a hue shift for their markers. This is why colours are [P] and shape carries the class.
- **No simulation, save or settings change.** EditMode tests are unaffected, and the Core is not touched.

## 2. Stages

### Stage 0 — baseline

1. Record `git rev-parse HEAD`. Build the release player (`plans/evidence/022/build.sh`, copied to `plans/evidence/023/`).
2. **"Before" captures.** Add `-capture-set 023` to `CaptureSequence.cs`, in the style of the existing sets. Scenes:
   - a Night with Grunts, Runners and Brutes on Meadow;
   - the `-stress` fixture (100 enemies).

   Each at half-heights 5.5 and 10.5, day and night, at 1280×720 and 1920×1080.

   Take them from the baseline build before any presentation change. Only the capture-set code may differ from the baseline, and it changes nothing that is drawn.
3. **Perf baseline.** Two paired stress runs, strategic view, 1080p (`-stress -stress-hero`, as in `plans/evidence/019/after-and-perf.sh`). Section 14's numbers predate the threat change and plan 022, so compare against this baseline, not against plan 019.

### Stage 1 — zoom cap (rule 6)

1. Set `MaxSize = WholeBoardSize` and update `CaptureSequence.cs:334`. Set `DefaultSize` to the D3 value (5.5 if D3-c). Captures and tests that call `SetView(DefaultFocus, DefaultSize)` follow it; fixed sizes (4–5.5) in `CaptureSequence` are unaffected.
2. Check the wheel at the cap, `Z` (whole board) and Reset Camera by hand in the Editor.

### Stage 2 — markers (rules 1–4)

1. **Meshes.** Seven flat marker meshes (disc, chevron, square, diamond, hollow ring, hexagon), built once in code like `ReservedRingMesh`, and shared by all views of a kind.
2. **Material.** An unlit material with no depth test (ZTest Always, ZWrite Off), drawn after opaque geometry and before HP bars. One material per class colour, created once, so the SRP batcher keeps the cost flat. If no existing shader in the project can do this, add the smallest URP unlit shader with these two states and say so in the results.
3. **The marker.** `EnemyView.Marker`, a child of `Root` at ground height (0.02).
   - Its size is computed each frame as in rule 3: `clamp(px × 2 × half-height ÷ Screen.height, footprint, cap)`, then divided by the `Root` scale. Read the camera from the active `CameraRig`, not `Camera.main`.
   - The Runner's chevron turns with the figure's yaw.
   - Its visibility is `!HeroCameraShown`; add the D1-b fade only if the owner picks it.
   - Add the marker to `StartDying`'s hide list.
4. **Debug counter.** Add `VisibleMarkerCount` on `WorldView`, in the style of `RouteOverlay.VisibleEntryCount`, for the test.

### Stage 3 — HP bars (rule 5)

1. In `BarScale`, the orthographic branch keeps the bar at a constant pixel width with the same formula as the markers (pixels → world via `2 × half-height ÷ Screen.height`), never smaller than today's `strategic` scale.
   - Buildings use `BarScale` too (`:1200`). Apply the new term to enemy bars only: building bars already read at the building's size. Pass a flag rather than changing the shared default.
2. Bars stay damaged-only unless the owner picks D2-b.

### Stage 4 — tune from captures (the [P] values)

1. Capture the `-capture-set 023` "after" frames, plus the default view at half-heights 5.5, 4.5 and 4.0 (for D3) and one option A frame (`enemyScale` × 2) for comparison.
2. **Set px, footprint, cap and colours** by checking the spec's acceptance items in the frames:
   - no speck test;
   - crowd test;
   - every class nameable at 10.5, at both 1280×720 and 1920×1080;
   - reserved hues not confused.

   Iterate at most three rounds. Record each round's values and frames in `plans/evidence/023/`.
3. **Owner review.** Send the owner a before/after sheet. The owner's look is the acceptance of record; the agent's frame review is not.

### Stage 5 — verification

1. **PlayMode.** Extend `ReleaseSceneTests.SwitchingViewsNeverChangesTheNightAndTheHeroMovesWithInput`, which already switches views during a Night:
   - in the strategic view, `VisibleMarkerCount` equals the number of living enemies;
   - in the hero view it is 0;
   - back in the strategic view it equals the living enemies again.

   This would catch a marker leaking into the hero view, or missing for a newly spawned or dead enemy.

   Do not add tests for sizes or colours: they are provisional and judged by eye.
2. **Test runs.**
   - PlayMode `ReleaseSceneTests` (the affected suite) first, then the full PlayMode suite, since `WorldView` is shared.
   - EditMode once, as a regression check.
3. **Release build, then perf.** Two paired stress runs against the stage 0 baseline, with the section 14 gate. Up to 100 extra unlit quads should be negligible; report the measured difference, not the expectation.
4. Check by eye that the hero view looks exactly as before (stage 0 versus stage 4 hero-view frames).

## 3. Spec and docs (same change)

- `specs/002-game-rules.md` → v1.9:
  - section 13: the camera zoom range becomes 2.5–10.5, and the "Board readability" bullet gains class markers;
  - section 17: the figures paragraph allows strategic markers as detail;
  - a section 16 row for the zoom cap, as a presentation change with its reason.
- Spec 009 → IMPLEMENTED, moved to `specs/archive/`, with both spec indexes updated.
- This plan → DONE, plus `plans/023-results.md` and a `plans/README.md` row.
- Evidence goes in `plans/evidence/023/`: XML results, txt files, capture sheets and scripts. The `.log` files are not committed, following the existing convention.

## 4. Risks

- **Clutter at Walls.** Twenty or more enemies at a Wall make a pile of symbols. The cap bounds it; if the capture still reads as noise, the fallback is to lower the alpha of markers that overlap another one of the same class. That is a contained change in stage 4, not a new rule.
- **No depth test reads as an x-ray.** Markers show through the Keep and trees. That is the intent (rule 4). If it looks wrong next to the Keep, the fallback is a dimmed copy behind occluders, which needs a second pass.
- **The capture tooling is fragile.** Plan 019 needed several runs. A locked desktop stalls the player (see memory); run the captures with the desktop unlocked.
- **The owner may prefer option A on seeing the comparison frame.** Then stop after stage 4 and re-plan; do not ship both.
