# 007 Stage A: baseline before changes

Recorded 2026-10-16 before any plan 007 edit, against HEAD `dc8b55a` ("Keep the burn marker at flame size while it flickers"). Unity `6000.3.16f1`. The project was **not** open in an Editor: no `Unity.exe` process, only `mcp-for-unity.exe` bridges. So closed-project batch commands were allowed.

## Working tree and drift

- `git diff --stat dc8b55a..HEAD -- Assets/TheLastFortress` printed nothing. There is no code drift since the plan was written.
- Uncommitted changes that were already there and are kept as they are: `ProjectSettings/EditorBuildSettings.asset` (line endings only), `ProjectSettings/EditorSettings.asset` (`m_EnterPlayModeOptions: 0 → 1`, which disables domain reload; new runtime state therefore stays instance-owned, with no static caches), `plans/README.md`, and the untracked `plans/007-complete-game-experience.md` and `plans/evidence/007-{graphics-motion,immersion,ui}/`.
- The first batch run in this session made Unity rewrite `ProjectSettings/ProjectSettings.asset`. It only reordered `scriptingDefineSymbols` (`SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY` became `APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED`). Content is unchanged.
- The rules match spec 003: six-night table in `MvpCampaign.SixNightTable`, ten perks, checkpoint schema 1 / `mvp-rules-1`. Plan 007 changes none of these.

## Fresh measurements at HEAD

| Check | Result | Evidence (`game/The Last Fortress/TestResults/007/`, not in git) |
|---|---|---|
| EditMode, full suite | **91/91 passed** | `baseline-EditMode.xml` |
| PlayMode, full suite | **14/15**: `PrototypeSceneTests.PausedNightBlocksBuildingButKeepsCameraAndInspection` failed with "camera pans while paused: expected > 0.05, was 0.047" | `baseline-PlayMode.xml` |
| MVP Windows build | Succeeded, errors 0, warnings 486, 130 MB | `baseline-Build.log`, `Builds/Mvp/build-report.txt` |
| Rendered capture at 1280×720 and 1920×1080 | The sequence ran to the end: 40 files per size, and the 006 capture log matches its 006 values (tooltips inside the canvas, pause/menu freeze, guards, recap). **Every PNG is pure black**: the build was started with a hidden window, and `ScreenCapture` reads the swap chain. No visual baseline came out of this run | `baseline-capture-*/`, `baseline-player-*.log` |

The PlayMode failure is a harness timing defect, not a gameplay regression. Camera panning scales with real frame time. The test held D for exactly 10 frames, and in `-batchmode -nographics` those frames are so short that the pan stays under the 0.05 threshold. In plan 006 the same test passed inside an open Editor, where frames are longer. The fix in this plan waits up to 600 frames for the pan instead of 10. The assertion is unchanged.

For the visual baseline, I relied on the plan 006 rendered set (`plans/evidence/006-rendered/`, 2026-10-15, flat fixture, seed 1) and the 005 menu still. The plan 007 capture adds an **offscreen** path (`-offscreen`): it renders camera and HUD into a texture, so a hidden window still produces real frames. All 007 renders use that path.

Measured vs. hypothesis: the counts above are measured. None of the following has been observed: pacing, gait at 1×, audio, or what a player understands. Those stay hypotheses until they are watched or played.

## Planned changes (B–D) and their sources

| Area | Planned | Source / budget |
|---|---|---|
| Event distribution | One `DrainEvents` per frame in `PrototypeBootstrap.SyncViews`; the same read-only batch goes to `WorldView.Sync` and a new `AudioDirector` | code only |
| Audio | 28 clips: UI/construction, combat, phase stingers, day/night ambience, four music loops. 24 voices, at most 4 of one sound, important sounds displace lesser ones, at most two music tracks during a crossfade | **Synthesized** by a project script (`Audio/Source~/synth.py`, numpy). No downloads or purchases; licence = project. About 20 MB of WAV in the repository |
| Settings | `GameSettings` v2 with master/music/effects volume; explicit v1 → v2 migration; separate `profile.json` for the guide | code only |
| Entry | Title composition without the gameplay HUD, New Run with seed details, Continue/Recover, Settings, Help, Credits, Quit, version; progressive skippable guide; Help in the run | code only |
| Icons | Card icons for all five buildings plus gold, in the `design/icons` line style | Rasterized by `UI/Source~/icons.py` (Pillow). House, Farm and Trap are new drawings |
| World | 0.8 s day/night light blend, Tower firing flash and recoil, damaged-building smoke, House window glow at night, Brute/Golem wind-up | Runtime presentation only. No new lights and no prefab regeneration |
| Escalation | Day announcements built from the stored campaign (new enemy type, final siege) and a Golem arrival banner | code only |

No art direction change, no Core/rule change, no new dependency.
