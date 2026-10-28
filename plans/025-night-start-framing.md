# Plan 025: frame tonight's entries at Night start

Status: **DONE** (executed 2026-10-28; see [025-results.md](025-results.md)). Owner live check pending. Priority P1: the top finding of the 2026-10-28 capture review, and a direct cost of plan 023's closer default zoom. Effort S.

Implements **`specs/archive/010-night-start-framing.md` v0.3** (implemented; was `specs/010-night-start-framing.md` v0.2 when planned) (approved 2026-10-28: D1 ease out to frame, D2 always reframe). Rules before: `specs/002-game-rules.md` v1.10, section 13 (now v1.11). Baseline commit: `2a71fb6`.

## 1. Pre-check (done while planning)

Paths below are under `game/The Last Fortress/Assets/TheLastFortress/Runtime/Unity/`.

- **Where the night starts.** `PrototypeBootstrap.StartNight` (`:971`) is the single place a night starts from player input, and already handles `heroViewAtNight`. The reframe goes there, after `Sim.StartNight()` succeeds.
- **Tonight's entries.** `Sim.Wave.EntryCells` (already used by the HUD tags).
- **Camera.**
  - `CameraRig.SetView` is the path for every explicit view: Reset Camera, Z, H, and the capture scripts.
  - `Tick` reads pan keys, right-drag and the wheel, so that is where player input during the ease is detected.
  - Rotation is a fixed `Euler(Pitch 40, Yaw 45, 0)`. For ground points, the screen X is `dot(p, right)` and the screen Y is `dot(p, up)`, and `dot(horizontal forward, up) = sin 40°`. So the focus for a projected centre `(cx, cy)` is `right·cx + forward_h·cy / sin 40°`.
  - `ClampFocus` keeps the focus within −2…22, which every framing satisfies.
- **No Core, save or settings change.** EditMode tests are unaffected.

## 2. Stages

1. **`CameraRig.FrameNight(points, eased)`.**
   - Computes the framed focus and half-height (spec rules 2–3).
   - With `eased`, it eases over 0.6 s in `Tick`; otherwise it sets the view at once.
   - `SetView` and any player input in `Tick` cancel the ease.
   - `Easing` is exposed for tests.
2. **`PrototypeBootstrap.StartNight`** calls it with the Keep and `Sim.Wave.EntryCells`, eased unless the hero view is entered at night start (rule 5).
3. **Test.** Extend one existing release scene test, not a new file. After `StartNight` and the ease:
   - every point projects inside the safe area, or inside the screen at the cap;
   - `ResetView` during an ease ends at the default view.

   Mutation-check it.
4. **Captures.** Add a night-start frame to `-capture-set 023`: a two-sided dense night and a one-entry night, after Start Night and the ease. Run them at 1280×720 and 1920×1080, and review them against the old frames.
5. **Close.**
   - Full PlayMode run and a Release build.
   - Merge the rules into `002-game-rules.md` §13 (v1.11), archive spec 010, and update both READMEs and the results file.

## 3. Risks

- **Fighting the player's camera.** D2 accepts this by design. Cancel-on-input keeps it to 0.6 s at most.
- **Capture scripts that call `StartNight` then `SetView`** still work, because `SetView` cancels the ease. Scripts that capture right after `StartNight` without a `SetView` will now show the framed view. That is intended, but older capture sets are no longer pixel-identical.
