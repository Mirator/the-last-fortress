# Plan 005 results — replayable maps, saves and connected fortress visuals

Status: DONE (implementation and mechanical gates). Fresh-player validation is still PENDING; nothing here claims fun, balance for humans, or release readiness. Executed 2026-10-15 after plans 003/004 in the same live Editor session (Unity 6000.3.16f1, Unity MCP pinned to `The Last Fortress@ffd3a3a9`). Nothing committed, pushed or published; no packages added.

**Playable build:** `game/The Last Fortress/Builds/Mvp/TheLastFortress.exe` (opens the MVP menu). The original prototype build was rebuilt with its unchanged command to `game/The Last Fortress/Builds/NavigationPrototype/TheLastFortress.exe`.

## Implementation

| Step | Result |
|---|---|
| 1 Terrain | `MapGenerator`: xorshift32 per attempt `seed ^ 0xA341316C ^ (attempt * 0x9E3779B9)`, partial Fisher-Yates over eligible cells in (z,x) order, 10 forest / 10 rock / 10 water; ring, Keep, entries and the central 8..12 square stay land. Validation uses the real `RoutePlanner` (all four entries need an open route) plus a flood fill (no isolated walkable pocket) and the two-disjoint-Farm-sites rule; up to 20 attempts, then the authored all-land fallback, which is labeled as such (`TerrainMap.Describe`, HUD "FALLBACK flat map"). Note: the central square always contains two disjoint 2 x 2 sites, so in practice that rule never rejects; connectivity and open entry routes are the real filters. Terrain is unwalkable/unbuildable (existing impassable-cell path), rendered as trees/boulders/pools without colliders. New Run seeds come from the wall clock; all gameplay streams derive from the seed via xorshift32 (reward stream untouched by terrain). |
| 2 Checkpoints | `RunCheckpoint` schema v1 (core, plain data, invariant-decimal strings for uint) with rules version `mvp-rules-1`, seed, profile, actual terrain cells and all six wave plans, checkpoint Day, gold, buildings (ID/type/anchor/orientation/HP/actual price), ID counters, perks, reward RNG state, income transaction (last paid Day), stats, offer history and a frozen pending offer. `CheckpointCodec.Restore` validates everything (schema, rules, bounds, footprints/overlaps, known IDs, HP 1..effective max with perks installed first, prices, gold, wave data, unique perks, phase) and builds a fresh simulation; the live run is never touched. Policy: Day 1 after creation; each new Day after perk + income; immediately before Night (Night starts only after the write commits); Save and Quit in Day; on first Reward entry the offer (IDs, RNG before/after) is attached to the pre-combat checkpoint before a choice is allowed; replay reuses it and sets the stream to the stored after-state; Victory/Defeat write a terminal record (no Continue). `SaveStore`: temp file in the same folder, `Flush(true)`, `File.Replace` with `.bak` (or move on first write); damaged/unknown/incompatible files are never modified; a New Run never overwrites an active or damaged save (it uses the next slot file; only an ended run's slot is reused), and New Run is hidden in the in-run menu (Save and Quit first); explicit backup recovery. `RunPersistence` is the single writer; a failed write keeps the exact snapshot pending, blocks play (Retry / Quit without saving) and never re-runs the transition. |
| 3 Menu/summary/settings | Menu: New Run with optional seed, Continue ("returns to the start of your current night"), Recover backup when damaged, in-run Resume / Save and Quit (Day only) / Settings, Quit. Outcome screen: nights cleared, enemies defeated, buildings purchased, gold earned (start excluded), synergies active/ever (unique pairs), seed; Retry same seed, New seed, Main menu. Seed display + copy in the HUD. Settings (separate `settings.json`, atomic, unaffected by run-save corruption): camera sensitivity (pan/zoom), camera shake on Keep hits (a small bounded shake added so the toggle is real), window mode, resolution; volume categories shown disabled with "this build has no audio". |
| 4 Walls/rotation | `WallTopology`: N=1 E=2 S=4 W=8 from authoritative occupancy, canonical masks 0/1/5/3/7/15, rotation `((m<<1)|(m>>3))&15` = +90° yaw. Six reusable piece prefabs (arms end at the tile edge). Views rebuild only Walls whose mask changed on a new occupancy revision (`WallRebuildCount`), never every frame; the Wall ghost shows its own hypothetical variant without touching committed topology; tooltip "Automatic connections". `R`/Rotate button rotate Tower/House/Farm ghosts or selected buildings during Day only; facing is saved/restored and never changes cells, routes or combat. Prototype scene controls/visuals unchanged (R is MVP-only). |
| 5 Visuals/HUD | Additive MVP setup only (prototype prefabs untouched): wall pieces, Runner/Brute/Golem silhouettes, lighter Golem stone with glow, enemy view scale 1.35, foliage/rock materials, terrain decorations, and an unlit `TheLastFortress/OverlayOnTop` shader (ZTest Always, SRPDefaultUnlit) so route lines, breach markers, selection/range rings and synergy edges stay visible through tall Towers. uGUI kept. |
| 6 Final | MVP stress fixture `WavePlan.CreateMvpStressWave` (per entry 15 Grunt, 5 Runner, 4 Brute, 1 Golem; deficits respawn the same archetype at the same entry) with labeled debug House/Farm/Trap; `StressMeasurement` now records the full fixture. Both builds succeed. |

## Verification

New/extended tests: `Tests/EditMode/MvpReplayAndSaveTests.cs` — seeds 0..999 batch (validity, exact obstacle counts, fixed cells, repeatability, reward-stream independence, fallback rate); 16-mask table; corner/T add/remove/sell/load plus rotated Farm keeping its footprint and saved facing (and no Night rotation); Day checkpoint round-trip (damaged Farm/Keep, discounted price, orientation, perks, RNG, gold, waves, multi-cell occupancy) followed by identical resumed combat; Reward ledger replay with a different build → same offer, RNG after-state, one income, ledger folded into history; storage faults (corrupt, unknown schema, injected write failure via a directory at the temp path) proving the durable file survives, failed loads leave the live run unchanged, settings survive, backup recovery works, New Run uses a separate slot, and Retry writes the pending snapshot. `Tests/PlayMode/MvpSceneTests.cs` — menu → seeded generated run → Save and Quit → Continue (stored terrain + construction) → Night → Defeat summary + terminal save → same-seed Retry → settings applied and persisted; existing MVP smoke now starts from the menu.

| Evidence | Result |
|---|---|
| `plans/evidence/005-EditMode-20261015-131901.xml` | Passed, 89/89 (fresh, final code) |
| `plans/evidence/005-PlayMode-20261015-131920.xml` | Passed, 13/13 (fresh, final code) |
| `plans/evidence/005-map-seeds.txt` | 1000/1000 generated, 0 fallback (0.0%); accepted at attempt 0: 988, attempt 1: 12 |
| `plans/evidence/005-mvp-build-report.txt` / `005-prototype-build-report.txt` | Both Succeeded, 0 errors (MVP rebuilt after the final fixes; screenshots and stress runs were taken on the build just before them, which differed only in menu/settings/slot handling) |
| `plans/evidence/005-policy-runs-rerun.txt` | Recorded policies rerun after 005 (no tuning changed): economy-first wins seeds 1–3 with and without perks; defense-first wins with perks, loses without |
| `plans/screenshots/005-mvp/` | 1920 x 1080 and 1280 x 720: menu, connected corner/T/cross Walls, Wall ghost variant, 2 x 2 Farm ghost, rotated Farm and Tower, Reward, menu Continue, Night 4 combat, Night 6 boss, Victory; seeds 0–9 at 720p plus `seeds-0-9-contact-sheet.png` |

Visual inspection: Wall joins are continuous for corner/T/cross; seeds 0–9 each show the open route and readable obstacles; the boss and Brutes read at default zoom.

## Performance (reference machine)

Ryzen 5 3600, GTX 1660 SUPER, 32 GiB (verified via `Win32_Processor`/`Win32_VideoController`), Windows 10, non-development x64 standalone, 1920 x 1080 windowed, quality `PC`, vSync 0, no frame cap, Direct3D12; 10 s warmup, 60 s capture, west blocker removed at 30 s.

| Fixture | Samples | Median | p95 | p99 | Max | Enemies min/max | Event |
|---|---:|---:|---:|---:|---:|---|---|
| MVP content (`005-stress-mvp-1920x1080.txt`) | 23996 | 2.532 ms | 2.966 ms | 3.188 ms | 5.236 ms | 100/100 | Wall (6,9), 24 attackers, revision 43→44, valid |
| Original Grunt-only (`005-stress-prototype-1920x1080.txt`) | 25894 | 2.343 ms | 2.789 ms | 3.018 ms | 5.562 ms | 100/100 | Wall (6,9), 21 attackers, revision 41→42, valid |

Gate (median ≤ 16.7, p95 ≤ 20, p99 ≤ 33.3 ms, valid event): **passed** for both. No optimization or tuning was needed.

## Remaining issues (known limitations)

- Window mode/resolution are applied with `Screen.SetResolution` in standalone builds (window mode now applies on its own); their in-build effect was not visually verified — the scene smoke verified sensitivity and shake application and settings persistence only.
- Restart (top-right) is an immediate same-seed retry without a confirmation prompt; the previous run's checkpoint stays in its own slot file. Older slot files are not listed in the menu (Continue opens the latest).
- F9 (stress fixture) leaves the current run without saving and returns to the menu when toggled off.
- `PrototypeSetup.Run` was refactored (shared helpers) but not re-run, to avoid rewriting prototype prefabs; the prototype scene/build still load and pass.

- Water cells use the same color as the off-board sea and read weaker than forest/rock; the income line wraps at 1080p with two economy types; the main menu leaves an empty band where hidden buttons sit; the night timer uses the OS decimal separator.
- A Tower diagonal to the Keep can hide part of the Keep from the fixed camera (overlays stay visible on top).
- The Editor rewrites `ProjectSettings/ProjectSettings.asset` (`preloadedAssets`) and `EditorSettings.asset` during PlayMode test runs; both were restored to their committed values after the final run. The only intended ProjectSettings change is the `Mvp.unity` entry in `EditorBuildSettings.asset`.
- Balance: economy-first wins comfortably even without perks; no tuning was applied (not authorized without a viability failure).

## Human playtest checklist (still required)

1. Five fresh players, no coaching, Windows build above. Time to understanding placement, Start Night and the route/BREACH warning (target: 4 of 5 within two minutes).
2. After the first run ends, ask why it ended (target: 3 of 5 explain correctly). Note whether they restart voluntarily (same or new seed).
3. Record full six-night session length with construction time separated from combat time (combat alone is about 3.5 min in the scripted runs).
4. Look for genuinely different winning approaches (economy vs early defense) and whether perks feel optional.
5. Check readability: enemy types at default zoom, water vs sea, Farm/House and Wall/Trap links, reward card text, menu Continue explanation.
6. Exercise Save and Quit, quit during Night or Reward then Continue (same offer, no reroll), and settings persistence.

## Owner playtest feedback fixes (2026-10-15, after first hands-on run)

| Feedback | Change |
|---|---|
| Buildings' purpose unclear | Each MVP build card now has one effect line from the live definitions (Wall "Blocks enemy paths", Tower "Shoots · range 4", House "+15 gold per day", Farm "+25/day · 2x2 tiles", Spike Trap "Damages enemies on it"). |
| New attack side missed while zoomed in | On-map entry markers for tonight ("WEST 10"), pinned to the screen edge with a direction mark when off-screen, tagged NEW for a side not used the previous night; status line "NEW: enemies also attack from East". |
| Water flickering while panning | Z-fighting: pool tops sat exactly on the ground plane. Pools are now raised slightly, inset on a pale rim and tinted darker than the sea (also fixes water vs sea readability). |
| Too much text | MVP status shows "DAY n of 6" plus one compact "Tonight: …" line (boss line on Night 6); route length line and "Six nights" removed (breach/NEW warnings only); income is one line ("Income +65 per day", plus "Received +65 at dawn"). |
| Controls hint weird/misplaced | Floating controls line removed in the MVP; controls listed in the main/in-run menu. Prototype unchanged. |

Verification on the final code: MVP build succeeded; EditMode 89/89 (`TestResults/EditMode`, 18:20) and PlayMode 13/13 (fresh run after the last tweak); the MVP smoke now also checks the "Tonight" preview, the WEST marker, the NEW East callout/marker and the dawn-income line. Screenshots: `screenshots/005-mvp/1280x720-feedback-zoomed-new-side-marker.png`, `1280x720-feedback-day1-cards-water.png`. Flicker was fixed by geometry (no coplanar faces); still images cannot show flicker, so confirm in motion. ProjectSettings drift from the PlayMode run restored again.
