# Plan 007 results: complete game experience

Status: **technical milestone implemented; listening, live review and player validation PENDING.** Stages A–D and F are implemented and verified in a fresh run on 2026-10-16. Stage E has had no participants. Nobody has listened to the audio. The visual checks used offscreen renders, not a live window. "Tests pass" and "feels like a complete game" are separate claims. Only the first is supported here.

Last verified step: final suites, MVP build, full capture at 1280×720 and 1920×1080, five-cycle leak check and stress run, all fresh and after the last code change. Next action: the listening pass and the five-person sessions in [`evidence/007-experience/playtest.md`](evidence/007-experience/playtest.md).

## Environment

- Baseline HEAD `dc8b55a`. Nothing is committed. The pre-existing `EditorSettings.asset` / `EditorBuildSettings.asset` edits and the `007-*` report folders are untouched. A Unity-rewritten define-symbol order in `ProjectSettings.asset` was restored.
- No Editor was open. Every check used closed-project batch commands and the standalone build launched with `-WindowStyle Hidden`. No window was shown or controlled.
- Hidden windows render black through `ScreenCapture`: the Stage A baseline came out black. Plan 007 adds a `-offscreen` capture mode (camera + HUD rendered to a texture). All 007 images use it, and 0 of 118 final frames are black.

## Stage results

| Stage | Status | What changed |
|---|---|---|
| A Baseline | Done | [`evidence/007-experience/baseline.md`](evidence/007-experience/baseline.md). Fresh HEAD: EditMode 91/91, PlayMode **14/15**. The pan test failed on a harness timing defect, now fixed (see Tests). Build succeeded. |
| B Events and audio | Done technically; **listening PENDING** | `PrototypeBootstrap.SyncViews` is now the only `DrainEvents` call. `WorldView.Sync(sim, batch, dt)` and the new `AudioDirector` receive the same read-only batch. Limits: 24 voices; at most 4 of one ordinary sound, where the new one is dropped; stingers restart rather than stack; priority displacement (Keep hit > phase > destruction > … > shots). Two music tracks during a 1.8 s crossfade, and the same clip never restarts. Combat one-shots pause and resume with Night pause, menu and guard dialogs. Arrow impacts are scheduled on the presentation clock for when the drawn arrow lands. Load and restart clear pending sounds and skip the load's own events. Stingers come from `StateChanged` events, so each plays once per real transition. 28 clips are synthesized by `Audio/Source~/synth.py` (numpy, original; provenance in `Audio/credits.md`). `GameSettings` v2 adds master/music/effects volume with an explicit v1 migration and a 0–1 / NaN sanitize. Settings preview the volume at once, Back reverts it, and 0 = `AudioListener.volume` 0. The "no audio" row is removed. |
| C Entry and UI | Done; **player check PENDING** | Title: menu column over a staged, never-saved settlement on a generated island, no gameplay HUD or route lines, and the promise "Build by day. Hold until dawn." The menu has New Run (seed behind **Seed…**), Continue, Recover, Settings, Help, Credits, Quit and version/build id. One button state language (hover, press, focus, disabled) and click sounds on buttons, never on hover. The five-step first-run guide (`OnboardingState`) observes successful actions only. It recognizes steps done in any order, starting Night passes the optional steps, and it never blocks Start Night. Hide / Skip guide; Skip and progress persist in `profile.json`. Continue never restarts the guide, and an existing player with run saves starts with it skipped. Help (goal, rules, controls, Show guide) works in the run and from the title. Credits list sound, icons, font and engine. Card icons for all five buildings plus gold (`UI/Source~/icons.py`, same line style as `design/icons`). The status panel sizes to its text, and the boss bar, announcement, Golem banner and entry markers stack under it. The resources panel grows with perks. Outcome actions: Retry same seed / New Run / Main menu. |
| D World and climax | Done; **live motion PENDING** | 0.8 s day/night light blend on the presentation clock. Night starts immediately, and a load snaps to its phase. Tower firing flash and recoil. Smoke on buildings below 50% HP. House windows glow at night through an unlit pane, with no new lights. Brute/Golem lean back 0.35 s before a due strike, using the simulation's `AttackDueTick` with no timing change. Day announcements read from the stored campaign: the first Runner (Day 2), the first Brute (Day 3), and the final siege plus Golem (Day 6), with values taken from the definitions. A non-blocking 3.5 s Golem arrival banner, alongside the existing boss bar. MVP island finish: beach ring, 42 shore stones/reeds outside the board, 6 lit low-contrast ground patches; 48 static pieces in total. Ownership mode: generated. Prefabs are unchanged, decoration is runtime code, and clips/icons come from their scripts via **Link Audio and Icons**. |
| E Players | **PENDING** | No participants. Protocol, criteria and the listening checklist are in [`playtest.md`](evidence/007-experience/playtest.md). The content-scope decision (3.6) is not made. |
| F Stabilization | Done for automatable checks | See below. |

## Fresh verification (final run, after the last change)

| Check | Result |
|---|---|
| EditMode full suite | **92/92 passed** (`evidence/007-experience/final-EditMode.xml`) |
| PlayMode full suite | **16/16 passed** (`final-PlayMode.xml`) |
| MVP build | `result=Succeeded`, `errors=0`, warnings 486 (baseline 486) (`build-report.txt`) |
| Rendered capture, 720p + 1080p, offscreen | 59 frames per size; all MVP, 006 and 007 sequences ran. The 006 log matches the baseline except two cue/arrow counts. That difference is deliberate: shots now wait (bounded, ≤ 1.5 s) for presentation to settle, so arrows and cues in flight land before the frame is taken |
| Night 4 at 1× (real-time stepping, no skipped frames) | 36.2 s; max 14/24 voices, impacts ≤ 4, shots ≤ 4, 1 music track; NightStart 1, Dawn 1, KeepHit 3 |
| Night 6 at 1× | 62.2 s; max 8–21/24 voices across runs, shots/impacts ≤ 4, 2 music tracks only during the crossfade; NightStart 1, GolemArrival 1, Victory 1; banner at 30.1 s |
| Five Menu → New Run → end cycles | Identical after every cycle: 28 audio sources, 481 transforms, 0 pending sounds, 0 arrows, 0 cues, 1 voice (the Defeat stinger still sounding). No growth |
| Stress fixture, 1920×1080, Ryzen 5 3600 / GTX 1660 SUPER, D3D12, vsync off | median 0.32 ms, p95 0.47 ms, p99 0.68 ms, max 15.0 ms, `gate_pass=True` (`rendered/1920x1080-stress.txt`). **The window was hidden, so nothing was presented**; these figures say nothing about frame time on a visible window |

Images and capture logs: [`evidence/007-experience/rendered/`](evidence/007-experience/rendered/) (title, credits, settings, generated Day 1 with the guide, Help, Day 2/3/6 announcements, damaged House, Night 4/6 at 1×, Golem banner, Victory).

Audio measurements without listening (`synth.py` output): no clipped samples, peaks −0.5 to −11 dBFS, Keep hit and Golem the loudest one-shots (RMS −8 dB), ambience loops seamless at the seam (jump equal to the median sample step). This is an objective check only. Character, mix balance and fatigue are unjudged.

## Tests changed or added (AGENTS.md)

- `MvpReplayAndSaveTests.SettingsFromBeforeAudioMigrateAndVolumesStayInRange`: a literal v1 settings file loads with its camera choices and default volumes, instead of being rejected as "unknown version". Out-of-range volumes clamp, and saved volumes round-trip. It catches the naive version bump that would reset every existing player's settings.
- `MvpSceneTests.LethalShot…`, extended: an in-flight arrow's impact stays pending and silent through pause and menu, and sounds when it lands. World and audio received identical event and batch counts. Impact voices stay within the family cap.
- `MvpSceneTests.MenuSeeded…`, extended: Master volume 0 previews as real silence and persists.
- `MvpSceneTests.GuideAdvancesOnRealActionsSkipPersistsAndContinueLeavesCheckpointUntouched` (new): the title hides the gameplay HUD. A rejected placement does not advance the guide; a real Tower and a Tower inspection do. Skip persists. The guide never blocks Start Night. Reopening the menu never stacks music. Continue shows no guide and leaves the checkpoint bytes unchanged.
- `PrototypeSceneTests.PausedNightBlocks…`: harness fix only. It now waits up to 600 frames for the pan instead of exactly 10, because pan speed follows real frame time and batch frames are about 0.5 ms. The assertion is unchanged.

## Not done, limits and follow-ups

- **Listening** (3.2 acceptance), **live 1× visual review / video**, and the **five new players** (3.1, 3.5) are all PENDING. The real-time frames show state at 1×; they are stills, not motion review.
- Not implemented: highlighting buildings affected by a perk on the reward screen (optional in 3.4); speed-up (needs observed waiting first); occlusion silhouette for the Keep (renders show it readable behind Towers at default zoom; recheck in play).
- Music and sound are procedural. If a listener rejects them, swap the clips in `Audio/Clips` and relink. The code does not depend on the specific files.
- Frame timing on a visible window and 60 FPS on the reference PC are unmeasured. Run `-stress -measure` with a visible window before claiming the performance target.
- The full-release scope (3.6) is untouched by design. It is a decision for after Stage E.

Deliverable: the whole `game/The Last Fortress/Builds/Mvp/` folder (data and the `.exe` together), built from the current working tree. Controls are in the in-game Help and `game/The Last Fortress/README.md`; licences are in Credits and `Audio/credits.md`.

## Review round (gameplay + UI/UX agents, 2026-10-16)

Two read-only reviewer agents (cheap model) assessed gameplay and UI/UX from code, rules, capture logs and renders. Every finding was checked against the code and the images before acting. Fresh verification after the changes: EditMode 92/92 and PlayMode 16/16 (on the final code, PlayMode was rerun after the last copy change), MVP build succeeded with 0 errors and 486 warnings, and full offscreen capture ran at both sizes. Updated frames are in `rendered/`. New frame: `007-05b-house-payback-tooltip`.

**Implemented (presentation only; no rule, price, wave or perk value changed):**
- **Placement tooltip.** It now shows the numbers behind the decision. Tower: effective damage, interval and range. Spike Trap: damage on entry. Wall: "seal every route and enemies break through · Brutes/Golems hit Walls 2x". House/Farm: how many dawns are left to pay back the cost. The capture confirms "Pays at 5 more dawns: +75 gold" for a House on Day 1. "[OK] / [X]" became "OK: can build here / Can't build: …", which keeps a text cue as well as color. The tooltip flips above the pointer instead of covering the build cards.
- **Reward cards.** Last Stand states whether its condition holds now ("Not active now: Keep at 100% (needs below 30%)"). The reviewer noted it was never picked by the scripted policies.
- **Golem banner.** It now gives the Keep threat: "hits for 40, 2x on Walls".
- **First-run guide.** The card the guide talks about is tinted (Tower for step 1, House/Farm for the economy step). Help's **Show guide** used to do nothing once the guide had been finished; it now restarts it.
- **Outcome screen.** The dead "—" primary button is hidden. "Synergies active · ever" became "Building links active · formed this run".
- **Text size.** Perks, card subtitles, seed and version text go from 16–17 to 18 px. The Spike Trap subtitle was shortened so it doesn't wrap at 720p.
- **Settings note.** It now says that Back discards previewed changes.
- **Capture.** The first-run guide was absent from the earlier final renders. The cause was capture order: the earlier sequences had already finished the guide. That is now fixed by the restartable Show guide, and both sizes show it.

**Rejected or deferred, with reason:**
- Keep the coral Keep bar. The design brief specifies a coral health heart. Restart stays in the top bar: plan 006 placed it there deliberately, behind a guard.
- A "stale unaffordable cost colors" claim was wrong: the frame shows the costs in coral.
- A "need N more" text on cards: costs are numeric next to the gold total. Low value for the space.
- Shortening the "Tonight:" wave line and laying Help out in two columns: these touch asserted strings or are cosmetic only. Revisit after the player sessions.
- Night enemy contrast: plausible, but it needs a live look. It goes on the live-review checklist rather than a blind material change.
- Payback on the build cards themselves: there's no room on the card, so it went into the tooltip instead.
- Gameplay balance (flat difficulty curve with 15 of 16 scripted wins, Spike Trap and Recycling never used, count-only escalation): these are **RULE CHANGE** items and wait for Stage E evidence (plan 3.5). Before any tuning, the recommended next diagnostic keeps purchases constant and compares a Tower ring against corridor placement on one seed, then repeats it with a chokepoint-trap variant.
