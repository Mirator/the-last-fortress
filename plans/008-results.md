# Plan 008 results — gameplay foundation and production slice

Status: **TECHNICAL DONE; human, art/audio and visible-window gates PENDING.** The rules contract, release profile, six new mechanics, twelve-night campaign, three map profiles, 24-perk pool, legacy-safe saves, release content validator and release build are implemented and verified by fresh automated runs. Nobody has played, watched live or listened to any of it. This is not an AA-ready slice and must not be called one.

Last verified step (2026-10-16, after the last code change): EditMode **105/105**, PlayMode **18/18**, `ReleaseContentValidation.ValidateAll` **0 errors**, release build and MVP build **Succeeded** (0 errors, 486 warnings each, same as 007), offscreen release captures at 1280×720 and 1920×1080, MVP 007 capture set as a regression run. Next action: G0/G4 human sessions with `Builds/Release/` (see "Remaining").

## Environment and baseline

Baseline `dc8b55a` + uncommitted 007 work, recorded in [`evidence/008/baseline.md`](evidence/008/baseline.md): fresh EditMode 92/92 before any change. No Editor was open; every check used closed-project batch commands; standalone captures ran with a hidden window and offscreen rendering. Nothing is committed. 007's work and project-settings edits were preserved (Unity's define-symbol reorder in `ProjectSettings.asset` was restored after the last run; 7 MVP material files that the release setup rewrote were restored from git — only `GolemStone.mat`'s unused legacy `_Color` had changed).

## Gate results

| Gate | Status | Evidence and limits |
|---|---|---|
| **G0** rules, matrix, diagnostic | **Docs and diagnostic DONE; player gate PENDING** | [`specs/005-release-rules.md`](../specs/005-release-rules.md) freezes all six additions (every right-hand-column field), twelve nights, 24 perks (10 placement/targeting/timing ★), map constraints, economy, saves; values tagged invariant/provisional, with a tuning log. [`008-content-matrix.md`](008-content-matrix.md). Diagnostic [`tactics-diagnostic.txt`](evidence/008/tactics-diagnostic.txt): six-night MVP, flat map, seeds 1–3, same purchases and reward policy — the Tower ring won all 3; the corridor and the trap corridor (one added variable) lost on Nights 3–4. On a flat 4-entry map every route converges at the Keep and 8 Walls add only 2 route cells, so the ring's advantage is geometry, not unclear information. 007's listening/live/five-player gates are **still not done**; this session could not recruit or observe people. |
| **G1** profiles, catalog, saves, entry points | **DONE** | `RulesProfile`/`RulesCatalog`; the six-night table is untouched and remains the MVP/legacy profile. `ReleaseDefinitionsAsset` (authoring data → plain C# at run creation) and `Editor/ReleaseContentValidation.ValidateAll` (names asset and field; checks definitions, prefabs and icons for every kind, perk texts, 3 profiles × 50 seeds of maps, campaigns and Day-1 routes). Checkpoint schema 2 for release runs (`rulesVersion`, `campaignId`, `contentVersion`, `mapProfile`, `difficulty`); the MVP keeps writing schema 1. Tests: a pre-008 six-night save continues as six nights under MVP rules in a release catalog and keeps saving schema 1; a release save round-trips map, actual waves, IDs and Watchtower adjacency; MVP-only builds, newer schemas, other content versions and out-of-pool perks are rejected (codec level); in the scene, a schema-1 save continues in the release build with release cards hidden. No implicit conversion exists, so "upgraded copies" never occur; the original file is replaced only by the same profile's next checkpoint, keeping `.bak`. `Editor/BuildRelease.BuildWindows` builds only `Scenes/Release.unity` into `Builds/Release/` after validation. |
| **G2** six mechanics | **DONE (mechanics); 1× counter demos PENDING** | Cannon (2×2, splash), Watchtower (non-stacking range support), Repair Station (2×2, one dawn repair), Raider Archer (stops on its own route, sticky target), Bomber (fuse, committed blast, not counted as a kill), Siege Commander (non-stacking aura). EditMode tests pin exactly the gate's failure modes: splash once per target in ID order with the inclusive range boundary from the 2×2 centre; Watchtower non-stacking and destruction mid-combat without cooldown reset; one dawn repair, lowest HP fraction first, never again on load; fuse cancelled by a kill inside the fuse; blast breaks its Wall, splashes Chebyshev 1, no kill credit; same-step Keep defeat precedence; ranged stop, sticky target, cooldown reset on retarget, no reflection; aura +50% and its removal. PlayMode: hotkey/click placement of a 2×2 Cannon, Watchtower ghost highlighting, Core-derived range ring (5.5), and every new event presented (shell + splash ring, fuse, aura, enemy arrow, boss bar) within audio voice/family caps and cue pools. "A bounded recorded scenario demonstrates each counter at 1× with no coaching" was **not** done at 1× with a person; scripted stills exist (see captures). |
| **G3** twelve nights, maps, perks | **DONE (mechanical); blind playtests PENDING** | `ReleaseCampaign` (table + seeded entry rotation/boss entry from a separate stream, stored before Day 1), `MapProfiles` (Ravine corridor, Crossing split front, Meadow open economy; skeleton + bounded seeded variation, 20 attempts, validated fallback; 0 fallbacks over seeds 0–49 per profile, <5% asserted over 0–199), 24-perk release pool with the MVP pool unchanged (MVP seeds' offers and event payloads unchanged). Policy evidence [`release-policy-runs.txt`](evidence/008/release-policy-runs.txt): three distinct scripted approaches on their profiles, seeds 1–8, real budget: **kill-zone 6/8, corridor 5/8, economy 4/8** wins; losses recorded. The test protects 3 winning seeds per approach. Two provisional values were changed with this evidence (see the rules tuning log). Scripted combat time per full run 9–11 min; wall-clock run length with construction and decisions is unmeasured. |
| **G4** AA-quality slice | **PENDING** (technical integration only) | [`008-art-and-audio-bible.md`](008-art-and-audio-bible.md) written. All nine buildings and seven enemies have generated blockout prefabs, icons and presentation hooks; the slice uses the real campaign/save/content systems. **Not done:** final art/audio (no commissioning or purchase authorized), listening approval, live/default-zoom motion review, visible-window frame measurement, five returning + five new participants, finished-asset throughput. |
| **G5** production candidate handoff | **Technical part DONE; human sub-gates PENDING** | Full suites, release + MVP builds, old/new save scenarios, captures, validation report, this file, rules, matrix, bible. Known issues below. Playtime distributions from observed runs: none (no observed runs). |

## Fresh verification (final run, after the last code change)

| Check | Result |
|---|---|
| EditMode full suite | **105/105** (`evidence/008/final-EditMode.xml`) — 92 existing unchanged + 13 new |
| PlayMode full suite | **18/18** (`final-PlayMode.xml`) — 16 existing unchanged + 2 new |
| Release content validation | 0 errors; 3 map profiles × seeds 0–49, 0 fallbacks; 9 buildings, 7 enemies, 24 perks, 12 nights (`release-validation.txt`) |
| Release build | `Succeeded`, 0 errors, 486 warnings, `Scenes/Release.unity` only, 135 MB (`release-build-report.txt`) |
| MVP build (regression) | `Succeeded`, 0 errors, 486 warnings (`mvp-build-report.txt`) |
| Release captures | 16 frames × 2 sizes (incl. Help), offscreen, hidden window; every scripted condition reached (`rendered/*-008-capture-log.txt`) |
| MVP 007 capture set (regression) | identical to the 007 evidence log except one constant extra scene object (the Repair reach line); stable over 5 leak cycles (`rendered-mvp-regression/`) |

## Tests added (AGENTS.md)

- `ReleaseMechanicsTests` (8): one per G2 failure mode listed above. The Raider Archer test pins its own range in the fixture so it checks the rule, not the tuning.
- `ReleaseSaveTests` (2): legacy six-night continuation in a release catalog; release round trip and rejection of unknown profile/schema/content/perk.
- `ReleaseContentTests` (2): twelve nights match the table and preview what spawns, seeded variation repeatable; each map profile meets its named constraint on a seed corpus with legal 2×2 sites on Day 1.
- `ReleasePolicyTests` (1): three approaches × seeds 1–8, writes the policy log, protects 3 winning seeds each.
- `ReleaseSceneTests` (2, PlayMode): real-scene input/overlay/HUD for the new kinds plus presentation of every new threat within budgets; legacy save in the release build.
No existing test or assertion was changed.

## What was built (by area)

- **Core:** `Definitions` (3 building + 3 enemy kinds, appended IDs), `ReleaseDefinitions`, `RulesProfile`, `ReleaseCampaign`, `MapProfiles`, `PerkRules` (pool per profile, 14 new perks), `RunSimulation` (attacker list incl. Cannon, per-building cadence, splash, Watchtower cache per occupancy revision, dawn repair, ranged hold, fuse/detonation, aura, perk modifiers), `RunCheckpoint` (schema 2 + catalog restore), `WaveText` (7 kinds, any boss, compact two-line preview for release nights; MVP text unchanged), `EconomyRules` (Market Square, War Bonds bonus line).
- **Unity:** `ReleaseDefinitionsAsset`; `PrototypeContent` release references; bootstrap catalog, map-profile choice (title button, `-map`), legacy-profile loading; HUD two-row cards hidden per run profile, text for new kinds, any-boss bar/banner, release announcements, map selector; overlay range rings from Core, Watchtower boost outlines, Repair reach square; world shells/splash rings, enemy arrows, fuse, blast, aura, repair sparks; audio mapping; capture sequence `RunPlan008`.
- **Editor:** `ReleaseSetup` (idempotent; never overwrites authored prefabs), `ReleaseContentValidation`, `BuildRelease`, `TacticsDiagnostic`; prefab blockouts; 3 icons in `icons.py` (existing icons regenerate byte-identical).

## Motion and animation polish (measured, not watched)

Three passes plus an independent harsh critic, all presentation-only (no rule changed). Tools: `Tests/PlayMode/MotionProbe` (explicit diagnostic: a mixed night at a fixed 60 fps through the real scene, per-frame enemy step, turn, model and building-scale change, deaths) and the release build's `-capture-set motion` (330 consecutive frames at 1/30 s on a close camera, with a per-frame log).

| Pass | Found | Fixed |
|---|---|---|
| 0 | 90°-in-one-frame turns for every kind; slow enemies jumped up to 4× a frame's step when a night ended | rate-limited turning; always-interpolated position; 2×2 targets faced at their centre; standing spawns face the Keep |
| 1 | 4 of 5 enemies vanished while the killing arrow/shell was still flying; Brute strike jerk; destroyed buildings popped out | death fall (0.35 s) that waits for the projectile; collapse (0.45 s); wind-up released over time |
| 2 | Cannon never aimed; HP bar vanished when the shot was fired; splash disc nearly opaque | turret-only aim (base stays square); bar stays until the hit; lighter ring |
| Critic | build cards covered the fight all night; Cannon un-aimed on its kill shot and fired from the building centre; HP dropped at fire time; shells flew like arrows; splash popped off; gait vibrated (Runner ~16 steps/s); one turn rate for all; enemies stood inside each other; strike peaked after contact; **and my capture/probe advanced the effects clock twice per frame**, so earlier timing evidence was invalid | cards fade to 18% and click-through at Night; aim held 0.6 s after a shot, shell and flash from the muzzle; bar holds until impact, then drains; shells 0.42 s with a high arc and a glowing ball, impact sound timed to it; ring expands in 0.1 s then fades; gait from a per-kind cadence (1.5–4.5 steps/s); turn rate by weight (720/480/300/180 °/s); fixed per-enemy world offset; strike rises over the last 0.1 s and peaks on the hit (this fixed an off-by-one-step timing bug that was the real cause of the old Brute jerk); `ExternalViewClock` so captures/probes advance presentation exactly once |

Final probe ([`motion-after-fix.txt`](evidence/008/motion-after-fix.txt)): every kind moves exactly 1.00× its speed per frame, no jumps, max turn 3–12°/frame, no projectile-before-death pops; model step ≤ 0.02 (Brute 0.041, at the threshold). Strips: [`evidence/008/motion/`](evidence/008/motion/). Suites 105/105 and 18/18 (+1 skipped diagnostic), both builds green after the last change.

Not addressed from the critique: entry labels can sit over enemies at the spawn edge; the Commander's aura disc draws over water and shore; the Bomber blast marker is a 1.5-radius disc while its damage area is the 3×3 square; dying bodies hold their last gait pose during the kill delay; the building hit pulse is still an instant +6% (deliberate punch). None of this replaces watching it at 1×.

## Known issues and limits

- **Ring dominance is reduced, not resolved.** The converged Keep ring is still the most forgiving scripted approach (6/8). Whether players find it dominant or boring needs G3/G4 human sessions.
- New sounds are placeholders reusing 007 clips (Cannon = heavy strike, fuse = trap tick, blast = destroy). Each needs a designed sound and a listening pass.
- Commander aura disc is large at default zoom and could hide enemies in a crowd; review live.
- The 1.25-tile Cannon splash and 0.8–0.9 s late-night cadences have not been checked for readability at 1×.
- The Watchtower ghost's range ring shows a Cannon/Tower ghost's base range only; a tower ghost next to an existing Watchtower does not preview its bonus until placed (stated in code).
- The MVP stress fixture still uses MVP kinds; there is no release stress mix and no frame measurement of release combat (visible or hidden).
- Running **Setup Release Scene** (or Setup MVP Scene) calls the shared material generator, which rewrites a few MVP `.mat` files (only `GolemStone.mat` changes, in an unused legacy field). Restore them from git after a setup run; a later fix should load existing materials instead of recreating them.
- Release and MVP builds share the save folder by design (legacy continuation); an MVP build reports a release save as incompatible and never overwrites it.

## Remaining (needs people, hardware or authorization)

1. 007's listening pass, live motion review and five new players — still open and still a dependency.
2. G0 player gate and G3 blind sessions: at least three distinct layout approaches observed across the three maps; record losses.
3. G4: art/audio direction decision and budget (commissioning/purchasing needs owner authorization), final assets for the slice, listening approval, 720p/1080p live review, visible-window frame measurement on recorded hardware, five returning + five new participants, finished-asset throughput.
4. Run length: measure wall-clock 12-night runs with players against the 25–40 min hypothesis.

Deliverable: the whole `game/The Last Fortress/Builds/Release/` folder (the `.exe` and its data together), built from the current working tree. Old six-night saves continue in it. `Builds/Mvp/` remains the six-night regression build.
