# Plan 025 results: frame tonight's entries at Night start

Executed 2026-10-28 against `2a71fb6`. Implements `specs/archive/010-night-start-framing.md` (v0.3; the owner approved D1, ease out to frame, and D2, always reframe). Now merged into `specs/002-game-rules.md` v1.11 section 13. Presentation only: no simulation, save or settings change.

## What changed

All paths are under `game/The Last Fortress/Assets/TheLastFortress/Runtime/Unity/`.

- **`CameraRig.cs`**
  - **`FrameNight(points, eased)`:**
    - projects the points onto the fixed camera axes;
    - centres them, moving the focus back onto the ground through 1 / sin 40°;
    - takes the smallest half-height that keeps them inside the safe area (margins of 18% of the height and 10% of the width), clamped to 4.5–10.5.
  - **The ease** runs in `Tick` on real time: 0.6 s, smoothstep.
  - **Cancelling it:**
    - any change the player makes to the focus or size in `Tick` (pan keys, right-drag, wheel) cancels it;
    - so does `SetView`, the path used by Reset Camera, Z, H and scripts.
  - `Easing` is public for tests and captures.
- **`PrototypeBootstrap.StartNight`:**
  - frames the Keep, each of tonight's entry cells, and a point 1.5 tiles beyond each entry along its road;
  - eases unless the hero view is entered at Night start, in which case the view is set at once.
- **`CaptureSequence.cs`:** `-capture-set 023` gains two frames, both taken after Start Night and the ease:
  - `025-night1-start`: Night 1 of seed 7, North only;
  - `025-dense-night-start`: the West + East dense night.
- **`Tests/PlayMode/ReleaseSceneTests.cs`:** `EntryMarkersStandAtTonightsEntriesAndChevronsFlowTowardTheKeep` now checks four things:
  - Start Night starts an ease, which ends within 3 s of real time;
  - the camera moves, and the Keep and the entry land inside the safe area;
  - a West + East framing zooms out and keeps all three points inside the safe area;
  - Reset Camera during an ease ends at the default view.

## Values (from the captures)

- **First pass:** only the entry cells were framed. On the two-sided night, the East column walking in sat under the top-right HUD buttons.
- **The fix:** a road point 1.5 tiles beyond each entry [P], recorded in the spec's as-built note.
- **Night 1, seed 7 (North only):** focus (10.5, 15.75), half-height 4.5. The camera pans; it doesn't zoom.
- **Dense West + East night:** focus (10.0, 10.5), half-height 7.81.
- The same values came out at 1280×720 and 1920×1080 (`captures/capture-log-1920x1080.txt`).

**Frames** (`plans/evidence/025/captures/`):
- **Before:** at half-height 4.5 the dense night shows no enemy (`before-dense-night-4.5.jpg`, from the 2026-10-28 review build).
- **After:** both columns and the Keep at both resolutions, and the North entry, its walk-in road and the Keep on Night 1.

This is the agent's reading of the frames; the owner's look is the acceptance of record.

## Verification (fresh, 2026-10-28)

| Check | Result |
|---|---|
| Framing test (`after/Entry.xml`) | Passed |
| Mutation: safe-area margins set to 0 (`mutation/Entry.xml`) | Failed as intended: "(0.50, 0.00, 10.50) framed at (0.00, …), outside the safe area". Code restored. The first version of the test, with only the one-entry night, survived this mutation, because at 4.5 the margins don't bind. That's why the two-sided case was added. |
| Full PlayMode (`after/PlayMode.xml`) | 24 passed, 1 skipped (same skip as before) |
| Release build (`release-build.log`) | Succeeded, 0 errors, to `Builds/Release` |

EditMode was not re-run, because nothing in Core changed. Performance was not re-measured; the change is a few vector operations once per night plus a 0.6 s lerp.

## Not done / pending

- **Owner live check:**
  - the 0.6 s ease;
  - whether reframing every night is welcome (D2);
  - whether 7.8 on two-sided nights is too far out.
- **Not filmed:**
  - the four-entry final siege, where the 10.5 cap binds;
  - the hero-view-at-Night-start path.

  The second is only covered by code review: the view is set at once there.
- **Older capture sets** that capture right after `StartNight` without a `SetView` now show the framed view, so they are not pixel-identical to earlier runs.
