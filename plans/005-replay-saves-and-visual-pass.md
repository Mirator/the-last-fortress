# Plan 005: Add replayable maps, safe saves, and connected fortress visuals

## Execution contract

Status: TODO. Priority: P1. Effort: L. Planned 2026-10-15 at `4855f69`, with expected input from completed 003 and 004. Execute after `plans/004-six-night-combat-and-perks.md`. Follow root `AGENTS.md`, the rules in `specs/archive/003-six-night-mvp.md`, and the appearance direction in `design/README.md`. This plan completes the MVP implementation, not full-release content or human playtest approval.

The owner authorized this sequence for one agent while away, bounded evidence-based tuning, minimal demonstrated test-harness fixes, a simple low-poly/HUD pass, and automatic Wall rotation/connections. Rotation here is cosmetic: it never changes grid footprints, passability, attack direction, targeting, or building identity. This deliberately adds MVP visual rotation beyond the original prototype's no-rotation scope; preserve the original prototype's controls/content.

Repository `C:/Users/Miroslav Pavelek/Documents/Games/the-last-fortress`; Unity project `game/The Last Fortress/`; editor `6000.3.16f1`. No purchases, package upgrades, publishing, commits, pushes, or cloud services are authorized by this plan.

## Baseline, drift, and scope

Run `git status --short`, inspect changes since `4855f69`, and read `plans/003-results.md` and `004-results.md`. Required input: working separate MVP scene/build, six stored authored waves, economy and multi-cell Farms, traps/reflection, four enemy types, ten perks/five reward choices, deterministic reward RNG, actual-price refunds, effective stats, and a legal recorded full-run policy. Missing or failed foundational gates are blockers, not permission to implement substitute systems.

Recognize existing source locations: `Runtime/Core/GridState.cs`, `RoutePlanner.cs`, `RunSimulation.cs`, `Entities.cs`, `WavePlan.cs`; `Runtime/Unity/PrototypeBootstrap.cs`, `WorldView.cs`, `RouteOverlay.cs`, `HudView.cs`, `PrototypeContent.cs`, `StressMeasurement.cs`, `CaptureSequence.cs`; `Editor/EntityPrefabBuilder.cs`, `PrototypeSetup.cs`, and the additive MVP setup/build created in 003. Keep those responsibilities; adapt the live implementation produced by prior plans instead of assuming names never changed.

Allowed: focused map/save/settings/summary classes under `Assets/TheLastFortress/Runtime/Core/` and `Runtime/Unity/`; MVP content/visuals/scenes/materials/prefabs/UI under the same root; additive Editor art/setup/build helpers; necessary tests in current assemblies; README; `plans/005-results.md` and evidence under `plans/evidence/`/`screenshots/`; plan index. Do not regenerate original prototype assets as an art shortcut or hand-edit Unity YAML. Generated images remain modeling references, not the world texture or animated sprites. New Unity assets retain `.meta` files.

## Steps

### 1. Generate deterministic valid terrain

Use the existing 004 xorshift32 algorithm. Seed is uint invariant decimal. Map attempt initial state = `seed XOR 0xA341316C XOR unchecked(attemptIndex * 0x9E3779B9)`, attempt index 0..19; if zero use `0x6D2B79F5`. Reward stream remains independently seeded `seed XOR 0xC8013EA4`; terrain, VFX, or camera activity cannot consume it.

Start a 20 x 20 flat land grid. Boundary ring, fixed Keep `(10,10)`, four named entry cells, and central square `8..12` in both axes remain land. Enumerate other eligible cells in `(z,x)` order. Use a partial Fisher-Yates shuffle for the first 30 cells: for index i=0..29 choose j=i+NextBounded(count-i), then swap. First ten selected become forest, next ten rock, last ten water. All three are unwalkable/unbuildable/indestructible; meshes do not add hidden colliders. Keep the shoreline outside the logical board.

Validate all four entries have an open route to a free Keep neighbor before construction; all walkable land except Keep belongs to that connected region; and at least two distinct non-overlapping buildable 2 x 2 Farm sites exist outside Keep/reserved ring. Use the actual route planner plus an ordinary flood-fill; do not invent a separate approximate path rule. Reject invalid candidates, retry deterministically up to twenty attempts, then use the authored all-land fallback. Log seed, attempt count, and fallback; fallback must not masquerade as a unique generated map.

New Run may generate a new seed or accept an explicit valid seed. Show/copy it in the menu/HUD. Same-seed Retry resets gold/perks/HP/buildings/counters but reproduces terrain and frozen authored waves under the same rules version; user choices can still change weighted offers. Store actual terrain/waves in saves so resume does not rerun an evolved generator. Keep the flat authored map selectable for recorded balance policies and debugging.

**Gate:** one bounded batch test validates seeds 0..999, reports failing seed/attempt and fallback rate, and checks repeatability plus reward-stream independence on a small representative subset. Do not create 1000 separate discovered tests, golden image snapshots, or a property-testing framework. Manually inspect seeds 0,1,2,3,4,5,6,7,8,9 for visible terrain/build-route clarity and record screenshots/contact sheet.

### 2. Define and persist checkpoints without reward rerolls

Use a versioned plain-data record in `Application.persistentDataPath/Mvp/`; keep settings separate from the current run, and keep the original prototype untouched. Stable definition IDs replace Unity object references. No mid-combat save.

Save schema v1 includes rules/tuning version, original seed, selected profile, actual terrain cells and all wave plans, checkpoint Day/night number, gold, buildings (ID/type/anchor/orientation/current HP/actual paid price), next entity-ID counters needed for determinism, owned perks, reward RNG state, income transaction ID, run-summary counters, and frozen reward-offer metadata. HP uses effective perk maximum; multi-cell occupancy is reconstructed from one entity record. Validate bounds, footprint conflicts, known IDs, legal HP/prices/gold, wave data, unique IDs/perks, and checkpoint phase before installing loaded state. Do not deserialize directly into the active world or mutate shared tuning.

Checkpoint policy:

1. Save initial Day 1 after map/wave creation, without income. Save after each new Day's perk/income transaction, and immediately before starting Night. During Day, Save and Quit stores current construction/HP/gold state.
2. Quitting/crashing during Night resumes its prior Day checkpoint. Do not persist live enemy/burn/cooldown state as though it were a Day snapshot. UI explains `Continue returns to the start of your current night`.
3. On first entering Reward, freeze the offer and persist its IDs and RNG-before/after in a ledger attached to the last Day checkpoint BEFORE allowing a choice. The fortress snapshot remains pre-combat. Replaying that Night reuses its frozen offer, even if buildings differ, and advances reward RNG to the stored after-state once. It cannot reroll or consume extra RNG. After choosing, save the next Day's full state/owned perk/income transaction and fold the resolved ledger entry into history.
4. Quitting during Reward therefore replays combat from the prior Day but retains that offer. This is intentional and matches the checkpoint design. Selecting twice cannot apply a perk or pay income twice.
5. Victory/Defeat end Continue for that run, persist terminal status/summary, and offer fresh same/new-seed runs. Replay counters and gold start fresh; no meta-unlock grind.

Use synchronous small-file writes with a temporary file in the same directory, flush/close it, then atomic replace (or atomic move for first creation). Preserve a last-known-good backup; do not delete the active save before replacement. Serialize saves through one owner to avoid concurrent writes. A failed write shows an actionable error and Retry; do not start Night or advertise a successful save before commit. If a reward has been applied in memory when persistence fails, enter a save-pending UI state: block further gameplay, retry the same snapshot/transaction without applying the perk/income again, and retain the prior durable checkpoint. Menu exit must clearly say the latest progress is not saved.

Corrupt/unknown-schema/rules-incompatible saves stay untouched. Show the problem, allow explicit backup recovery or New Run in a separate slot; never silently reset or overwrite the failing file. No automatic unsafe schema conversion. Error paths must work without cloud/network access.

**Gate:** one checkpoint fixture round-trips a damaged Farm/Keep, discounted actual purchase price, orientation, perks, RNG and gold; another replays a Reward checkpoint without reroll or double income. Parameterize corrupt/unknown-version and injected failed-write cases in the same storage fixture, using a temporary test directory, never the user's real saves. Prove that the old durable file survives failure and that failed load leaves the live run unchanged.

### 3. Add the menu, summary, and meaningful settings

Menu: New Run with optional seed, Continue when a validated active save exists, same-seed Retry from outcome, Save and Quit during Day, clear Night/Reward checkpoint explanation, and settings. Show six-night progress and exact next wave as before; do not add feature-selection complexity.

Summary: nights cleared 0..6, enemies defeated, buildings purchased, gold earned excluding initial 250, and unique currently/ever activated synergy counts. Count a synergy by unique owning pair/type (Farm-House pair or Wall-Trap pair), not shared cell edges or render refreshes. Do not invent a `strongest synergy` score. Terminal save/summary writes must have explicit error feedback and never restore an ended run invisibly.

Settings: camera sensitivity, camera-shake toggle, window mode/resolution, and master/music/effects values. Apply supported settings using the current Unity APIs verified locally; preserve defaults/restore on load. If no audio exists, hide or disable the unused volume categories with a clear label instead of adding an unsolicited soundtrack or fake working controls. Save settings independently; a corrupt run save must not reset them.

**Gate:** one scene-level smoke goes menu → seeded Day → checkpoint → Continue → outcome/Retry and confirms effective settings. Keep storage validation in EditMode; do not duplicate every save edge case through UI.

### 4. Add automatic connected Walls and cosmetic rotation

Wall connections use cardinal neighboring Walls only. Farm/House/Tower/Keep adjacency does not create a gate or wall socket. Represent the neighbor mask as N=1, E=2, S=4, W=8. Treat connections symmetrically and derive them from authoritative occupancy. Include the candidate ghost when drawing its own hypothetical variant, without mutating committed topology.

Provide isolated, end, straight, corner, T, and cross shapes from a small set of reusable pieces/meshes. Canonical masks: 0 isolated, 1 end-N, 5 N/S straight, 3 N/E corner, 7 N/E/S T, 15 cross. Rotate by 90-degree yaw steps to match remaining masks; the logical mask rotation is `((mask << 1) | (mask >> 3)) & 15`. Keep silhouette/joins visible from the fixed camera. Rails may meet at cell boundaries, but every Wall still blocks exactly one tile. No extra collision or navigation geometry from rail meshes.

Refresh a placed/sold/destroyed Wall and its four neighbors, plus reset/load/generation changes. Remove dead connections in the same presentation sync as occupancy changes. Destroying a straight segment must correctly turn neighbors into ends; closed loops/corners/T/crosses must connect without duplicate structures or price charges. Avoid per-frame regeneration of all Wall meshes; change only affected topology. Existing wall nodes remain the source of damage/refund identity.

Manual R rotation: during Day, rotate the active Tower/House/Farm ghost clockwise by 90 degrees and store facing on placement; an explicit selected-building Rotate button may rotate those existing structures at no cost during Day. All these footprints are square, so the anchor/occupied cells remain unchanged. No rotation during Night/Reward, including paused Night. Trap is directionless. Keep is not rotated in this scope. Walls always auto-connect; R does not override their connections, and the tooltip says `Automatic connections`. Non-Wall orientation defaults to zero, is restored on load, and never changes targeting or attack range.

**Gate:** one table-driven pure mask check covers the 16 masks and rotations; one small scene/fixture checks add/remove/load of a corner/T plus a rotated Farm retaining its footprint and save facing. Visual inspection must confirm continuous joins; no need for 16 screenshot tests or one discovered test per mesh variant.

### 5. Perform the simple low-poly and HUD pass

Follow `design/README.md`: warm timber, pale stone, muted green, cool readable night/amber accents, matte low-poly geometry. Model Keep, connected Walls, Tower, House, 2 x 2 Farm, Trap, Grunt, lean Runner, broad Brute, heavy Golem with primitives/simple meshes. Distinguish all enemy classes at default zoom without tiny details. Use a small shared material palette and simple procedural motion; no imported art dependency, complex rigging, custom texture pipeline, or additional content roster.

Use Unity Editor APIs/available Unity integration for prefabs/assets, never hand-written YAML. An additive MVP visual setup preserves tuning, original prototype references, and unsaved user scenes. Verify all new `PrefabFor`/EnemyKind dispatches explicitly rather than falling back to Tower/Grunt. Farm visual is centered over its actual 2 x 2 bounds, while its stored anchor remains the lowest cell.

Keep the grid/footprint preview unobtrusive, range/selection obvious, and the reserved ring clear during building. Make route/breach overlays readable on top of relevant geometry (current prototype report notes tall Towers occlude them); use an overlay rendering approach compatible with installed URP, verified locally, rather than a full pipeline migration. A screen-space breach marker/label is acceptable. Use pattern/icon/text in addition to color. Costs, HP, hotkeys, income and repair quotes remain real text. Use simple existing/native icons; SVGs in `design/icons/` are design sources, not a reason to install an importer. Keep uGUI unless an existing integration justifies otherwise; do not migrate the UI framework.

Night must remain readable without a torch near an enemy. Keep lights/shadows restrained and do not let art appear to block an unoccupied tile. Check the default camera for Tower occlusion of Keep, unit silhouettes, and Wall connections; retain pan/zoom/reset and fixed orientation.

**Gate:** capture Day, Night, 2 x 2 Farm ghost, connected corner/T/cross Walls, rotated building, reward, menu/Continue, and outcome at 1920 x 1080 and 1280 x 720. Record remaining visual issues rather than claiming photorealistic assets or final-release polish. No automated test for each color, mesh vertex, or HUD label.

### 6. Final build, focused regressions, and performance evidence

Build the normal six-night MVP with the additive `TheLastFortress.Editor.BuildMvp.BuildWindows` from 003 to `Builds/Mvp/TheLastFortress.exe`; retain original prototype executable/output separately. Normal launch opens the MVP menu; no hidden free resources or stress/debug flags by default. Document command-line or menu access for flat authored map and explicitly labeled stress measurement.

Keep the original reference stress fixture measurable, and add an MVP-content measurement using the current MVP visuals/effect/view path: same 30-Wall/10-Tower board and four entries; 100 maintained enemies split per entry as 15 Grunts, 5 Runners, 4 Brutes, 1 Golem (four Golems total is debug-only), respawning the same archetype/entry after death. Keep/Walls invulnerable only for the fixture. This preserves 100 active while stressing all silhouettes/motion/effects; labeled debug placement may include an additional House, Farm, and Trap in validated free cells if needed to exercise their views. Record the complete serialized fixture. Do not pretend authored six-night waves normally spawn four bosses.

Use reference Ryzen 5 3600, GTX 1660 SUPER, 32 GiB; Windows x64 non-development standalone, 1080p, VSync off/no frame cap, quality recorded. Warm up 10 s, capture 60 s, remove an actively attacked first west Wall blocker at 30 measured seconds, validate 100 maintained enemies and route invalidation. Gate median <=16.7 ms, p95 <=20 ms, p99 <=33.3 ms; percentile sorted index ceil(p*n)-1. Original Grunt-only gate must also remain valid for comparison. Record event validity/frame, sample count, min/max enemies, build/rules version, hardware and all percentiles. If mixed archetypes make the selected event target invalid, repair fixture design, not gameplay routing. A measurement on other hardware cannot claim the reference-machine gate passed.

Profile a failed gate before optimizing; no architecture rewrite for speculative scale. Apply only MVP tuning already authorized in 003/004: at most three documented passes per stage, ±20% total from original specification values, permitted costs/House-Farm outputs/enemy HP/Tower damage/non-boss counts only, no change to starting budget/base Keep HP/income or gameplay rules. Prefer no tuning in this art/save stage unless a reproduced viability issue warrants it. Re-run existing recorded full-run policies after actual tuning changes.

## Verification interface

Run affected checks first, then relevant suites once after integration. Use the actual open project's Editor Test Runner/Unity MCP, or sequential batch commands when it is closed:

```powershell
$fortressEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
$fortressProject = (Resolve-Path 'game/The Last Fortress').Path
$fortressResults = Join-Path $fortressProject 'TestResults'
New-Item -ItemType Directory -Force -Path $fortressResults | Out-Null
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform EditMode -testResults "$fortressResults/005-EditMode.xml" -logFile "$fortressResults/005-EditMode.log"
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform PlayMode -testResults "$fortressResults/005-PlayMode.xml" -logFile "$fortressResults/005-PlayMode.log"
& $fortressEditor -batchmode -nographics -quit -projectPath $fortressProject -executeMethod TheLastFortress.Editor.BuildMvp.BuildWindows -logFile "$fortressResults/005-Build.log"
```

Wait for each command; use quoted `Start-Process -WindowStyle Hidden -Wait -PassThru` if shell returns early. No `-quit` on tests and no second Editor for an open project. Check fresh nonzero Passed XML, zero failures/unexpected skips, no compile errors, and successful BuildReport. Live export was used in the earlier review; batch commands were not rerun during planning. Performance/screenshot verification requires rendered standalone execution, not `-nographics`.

Only necessary new coverage is required: map validity/repeatability batch, checkpoint/reward integrity/failure behavior, wall masks/orientation, and one representative UI integration path. Existing test corrections need demonstrated harness defects; preserve original behavioral scenarios. No coverage quota, full-run test multiplication, golden screenshot suite, or extra tooling package.

## Done, evidence, and stop conditions

Write `plans/005-results.md` with build/test results, map seed/fallback evidence, checkpoint/error-path evidence, screenshot paths, connected-wall/rotation examples, full-run policy outcomes, performance report, and remaining human actions. Update plan index. Clearly distinguish implementation DONE from fresh-player validation pending; do not mark the overall game released or commercially validated.

Human acceptance still needed: at least five fresh players, route/placement understanding, explanation of outcomes, two genuinely viable strategies, restart interest, and real six-night session duration. Agent simulations/screenshots cannot substitute. Leave a concise playtest checklist and a playable build for the owner; do not stop otherwise-complete technical work merely because the owner is away.

Stop dependent mutations and report if loading/writing risks user save loss, core routing/occupancy regresses, a verification failure persists after two focused fixes, or implementation requires new external accounts/assets/packages or broader game mechanics. Preserve the last playable stage and user files. Minor art issues can be recorded as known limitations; missing persistence integrity or a failed reference performance gate cannot be labeled passed.
