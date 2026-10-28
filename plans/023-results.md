# Plan 023 results: class markers and a zoom cap for strategic readability

Executed 2026-10-28 against `93c83d1`. Implements `specs/archive/009-strategic-figure-readability.md` (v0.3; approved 2026-10-28 as recommended: D1-a, D2-a, D3-a), now merged into `specs/002-game-rules.md` v1.9 section 13. Presentation only: no simulation, save or settings change. Nothing is committed.

## What changed

All paths are under `game/The Last Fortress/Assets/TheLastFortress/`.

- **`Runtime/Unity/WorldView.cs`**
  - Each enemy view gets a `Marker` child: a flat class shape on the ground.
    - It is drawn with the existing `OverlayOnTop` shader: unlit, with no depth test, at queue 4001 so it draws after the route lines.
    - There is one shared material. The shape and colour are baked into a per-kind mesh (vertex colours, converted to linear because the project renders in linear colour space).
  - `PlaceMarker` sizes the marker each frame as `clamp(18 px × 2 × half-height ÷ camera pixel height, 0.18, 0.42)` tiles, ×1.5 for bosses. It is turned to the camera yaw and stretched by 1 / sin 40° so the shapes read true on screen.
  - Markers hide while the hero camera shows, and on death through `StartDying`.
  - `VisibleMarkerCount` reports the markers shown in the last sync.
  - Enemy HP bars only: `BarScale(…, keepOnZoomOut: true)` keeps the on-screen size they have at half-height 5.5 when zoomed further out. Building bars are unchanged.
- **`Runtime/Unity/CameraRig.cs`:** `DefaultSize` 5.5 → 4.5 and `MaxSize` 14 → `WholeBoardSize` (10.5).
- **`Runtime/Unity/CaptureSequence.cs`**
  - New `-capture-set 023`: Day board, the dense mixed fight at 4.0 / 4.5 / 5.5 / 10.5, an option A frame, a hero-view frame, attackers at the Walls, every kind side by side, and the 100-enemy stress fixture.
  - The old `SetView(DefaultFocus, 12.5f)` now uses `WholeBoardSize`, since 12.5 is out of range. Older capture sets at 12.5 are therefore no longer reproducible exactly.
- **`Tests/PlayMode/ReleaseSceneTests.cs`:** `SwitchingViewsNeverChangesTheNightAndTheHeroMovesWithInput` now also checks, on every frame of its view-switching night:
  - the marker count equals the living enemies while the strategic camera shows, and is 0 through the hero camera;
  - both cases were exercised with enemies alive.

### Deviations from the spec text (recorded in the archived spec's as-built note)

- **Runner marker.** It is a triangle that always points up the screen, not along the heading. That is simpler, and the class still reads by shape.
- **Enemy HP bars.** They keep their half-height 5.5 size rather than a fixed pixel size. This meets "never smaller than today", without a second pixel rule.
- **No §16 tuning-history row.** That table records [P] changes backed by policy runs; this change is presentation only and is recorded in the v1.9 version line instead.

## Values chosen from the captures (stage 4)

- **Round 1** (15 px, 0.15 to 0.40 tile): the classes were readable at 10.5, but the fills came out pastel. The vertex colours were not converted to linear, and the outline came out grey. The markers were also small at 4.5.
- **Round 2** (18 px, 0.18 to 0.42 tile, linear colours), accepted:
  - every class can be named at 4.5 and 10.5, at 1280×720 and 1920×1080;
  - crowds at a Wall and columns at the entries read as groups;
  - no enemy is hidden behind a tree or building.

  This is the agent's reading of the frames; the owner's look is the acceptance of record.

Before/after sheets are in `plans/evidence/023/captures/`:
- the Walls at 4.5;
- the lineup of all kinds at 4.5 (720p);
- the dense fight at 4.5, 5.5 and 10.5;
- the stress fixture at 10.5;
- option A (figures ×2, no markers) next to markers at 4.5.

The full frames are in `game/The Last Fortress/TestResults/023/{before,after1,after2}/` (gitignored). The scripted states are identical before and after: the capture logs match line for line.

## Verification (fresh, 2026-10-28)

| Check | Result |
|---|---|
| PlayMode `ReleaseSceneTests` (`after/Scene.xml`) | 8/8 passed |
| Mutation: markers left on in the hero view (`mutation/Scene.xml`) | Test failed as intended: "class markers (hero camera True, frame 0)". Code reverted. |
| Full PlayMode (`after/PlayMode.xml`) | 24 passed, 1 skipped (same skip as before) |
| Full EditMode (`after/EditMode.xml`) | 129/129 passed |
| Release builds (`before-build.log`, `after-build-2.log`, `release-build.log`) | Succeeded, 0 errors; built to `Builds/Plan023-before` and `Builds/Plan023-after`, then the final code to `Builds/Release` for the owner |
| Hero view unchanged | Before vs after, pixels differing by more than 8: 28k (720p) and 53k (1080p). Two identical after-builds differ by 34k and 118k (animated grass and flames), so the difference is noise. |

**Performance:** paired and alternating, stress fixture with the hero, strategic view, 1920×1080, Editor closed (`perf/`).

| Run | Build | Median | p95 | p99 | Max |
|---|---|---:|---:|---:|---:|
| 1 | before | 3.449 | 4.170 | 4.545 | 10.1 |
| 2 | after | 3.624 | 4.359 | 4.696 | 7.9 |
| 3 | before | 3.423 | 4.139 | 4.762 | 41.4 (one hitch) |
| 4 | after | 3.523 | 4.117 | 4.351 | 5.2 |

- After is 0.1–0.2 ms slower at the median, inside the machine's ±0.4 ms noise, and every run passes the section 14 gate.
- The strategic stress view uses the default zoom, so this compares what ships: the 4.5 view with markers against the 5.5 view without. It is not the cost of the markers alone.
- Run 3's 41 ms hitch was in the baseline build; its cause was not investigated (compare plan 019's 66.8 ms).

## Not done / pending

- **Owner review of the captures and the live feel. This is the acceptance of record.** In particular:
  - the 4.5 default framing;
  - whether 18 px markers are big enough;
  - the colours. Grunt red and the Runner's yellow sit near the coral and amber UI hues, though the shapes differ.
- **Plan stage 1.2 was not done.** Nobody checked the mouse wheel at the cap, `Z` (whole board) or Reset Camera by hand in the Editor. The evidence is indirect: `SetView` clamps, and the full PlayMode suite passes.
- **Capture-only artefact.** Offscreen captures draw the HUD with the camera, so a marker (queue 4001, no depth test) can draw over a HUD panel in those frames, as route lines always could. None was seen in the reviewed frames. The real HUD is a screen-space overlay drawn after the camera.
- **Day captures of enemies don't exist,** because enemies only appear at night. The Day board frames check framing only.
- **The hero's ring was not changed.** At 10.5 it stays about 60 px wide; this was checked in the frames.
- **Not re-run:** the scripted policy runs, because nothing in the simulation changed.
