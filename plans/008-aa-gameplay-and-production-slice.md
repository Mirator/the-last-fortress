# Plan 008 — Build the gameplay foundation and production slice for an AA game

> Executor: this is a staged design and implementation handoff. Read it fully, complete each gate, and record evidence in `plans/008-results.md`. Definition work precedes dependent implementation. Do not fill missing combat or economy contracts with guesses. Update the plan's index row when a milestone changes; do not mark the whole plan DONE for a technical-only result.

## Status and intent

- **Status:** TODO; planning does not authorize execution.
- **Priority / effort / risk:** P1 / L, multi-month / HIGH (combat, economy, content and save contracts).
- **Depends on:** plan 007 technical implementation. Close its listening, live-motion and fresh-player gates before committing to expanded content production. Design documents and isolated investigations may proceed while those checks remain pending.
- **Planned at:** `dc8b55a`, 2026-10-16, **plus the uncommitted plan 007 implementation**. HEAD alone does not identify this baseline.
- **Category:** direction; production preproduction and vertical slice.
- **Repository:** `C:/Users/Miroslav Pavelek/Documents/Games/the-last-fortress`.
- **Unity project:** `game/The Last Fortress/`, Unity `6000.3.16f1`, URP, uGUI, Input System; simulation is plain C#.

Here, AA means a focused premium PC strategy game with strong authored identity, several interacting tactical systems, sustained replayability, polished audiovisual production and professional verification. It is a product ambition, not a guaranteed budget category or a request for photorealism, multiplayer or thousands of units. Preserve the premise: build during an untimed Day, shape routes, then watch automatic defense prove or disprove the plan.

This plan delivers **one production-quality slice and a mechanically complete twelve-night campaign**, not all release content. Plan 009 handles expansion and shipping. The original GDD's 15 buildings / 10 enemies / 4 bosses / 50 perks / 4 biomes remain conditional release targets, not quotas to fill with duplicates.

## Current state and evidence

All shortened asset paths below are under `game/The Last Fortress/Assets/TheLastFortress/`.

| Existing implementation | Evidence and consequence |
|---|---|
| Six buildings and four enemies, stable enum IDs | `Runtime/Core/Definitions.cs`: `Keep=0`, `Wall=1`, `Tower=2`, `House=3`, `Farm=4`, `SpikeTrap=5`; append IDs, never reorder them. |
| Six authored nights, fixed entries | `Runtime/Core/MvpCampaign.cs`: `SixNightTable`, `BuildNight`, `MaxLiveEnemies=100`. `CampaignConfig` stores actual waves before play. The release director must preserve preview/spawn parity. |
| Immediate authoritative combat | `Runtime/Core/RunSimulation.cs`: Tower targeting/damage and `EnemyAttacks`; presentation projectiles do not determine damage. New weapons must not accidentally introduce physics-based outcomes. |
| Global fixed-size map assumptions | `GridPos.cs`, `MapGenerator.cs`, `RunCheckpoint.cs`: occupancy and saved terrain use `GridCoordinates.Size`. Retain 20×20 initially; a larger board is a separate measured migration. |
| Narrow existing save validation | `RunCheckpoint.cs`: schema 1, `CheckpointCodec.RulesVersion="mvp-rules-1"`, stored terrain/waves, rejection of unknown rules. Extending a wave list alone does not create compatible full-release saves. |
| Audio, guide and presentation now exist | Uncommitted `AudioDirector.cs`, `AudioContent.cs`, `OnboardingState.cs`, `PresentationSetup.cs`; `PrototypeBootstrap.SyncViews` distributes one event batch. Extend these contracts instead of repeating 007. |
| Technical evidence is not player validation | `plans/007-results.md`: reported 92/92 EditMode, 16/16 PlayMode, successful build, offscreen renders and bounded audio. Listening, live motion, visible-window performance and five new players remain pending. No checks were rerun for this plan. |

Drift anchors:

```csharp
// Runtime/Core/MvpCampaign.cs
public sealed class CampaignConfig
{
    public string Label;
    public IReadOnlyList<WavePlan> Nights;
    public bool OffersRewards;
    public int TotalNights => Nights.Count;
}
// Runtime/Core/RunCheckpoint.cs
public const int CurrentSchema = 1;
// CheckpointCodec
public const string RulesVersion = "mvp-rules-1";
```

Before work: run `git status --short` and `git diff --stat dc8b55a..HEAD -- "game/The Last Fortress/Assets/TheLastFortress"`, inspect the working diff and untracked files too, and record a baseline file inventory in `plans/evidence/008/baseline.md`. Preserve all existing plan 007 work and project-settings edits. If another task is editing the same files, agree ownership before modifying them; do not reset the checkout. No commit, push or publishing is authorized.

## Product contract and scope ceiling

The player should face three distinct viable fortress approaches: long controlled corridors with traps, concentrated ranged kill zones, and distributed defenses protecting income. Their costs and risks should differ; equal end-of-run HP is not required. No offer, premium unlock or mandatory permanent stat bonus may be necessary to win.

Target one 25–40-minute twelve-night run, measured in wall-clock play including construction and reward decisions. Treat it as a hypothesis; remove repetition before increasing speed or padding nights. Final-night defeat must still override a same-tick final kill. Preserve open-route-first breaching, guaranteed Keep income, deterministic tie-breaking and pause semantics.

**008 content ceiling:** nine building kinds including Keep, seven enemy kinds including two bosses, 24 perks, three tactical map profiles in one biome, one twelve-night campaign and one baseline difficulty. Existing content counts toward these totals. No soldiers, heroes, active abilities, multiplayer, elevation, new currency, forced grind or campaign metagame. Keep the existing 100-live-enemy cap until evidence justifies changing it.

Proposed additions and their required tactical roles:

| Addition | Locked role and initial contract | Must be frozen before coding |
|---|---|---|
| Cannon Tower | 2×2 solid building; slow deterministic area damage against ground units; no friendly fire; existing Archer remains the reliable single-target option. | Price/HP/range/cadence, integer damage and radius, center/range metric, targeting tie-break, splash application order, perk eligibility. |
| Watchtower | 1×1 solid support; orthogonally adjacent Archer/Cannon receives one range bonus; multiple Watchtowers do not stack. No fog-of-war system. | Bonus magnitude, multi-cell adjacency rule, whether newly buffed targets change cooldown, preview/range-ring parity. |
| Repair Station | 2×2 solid support; repairs nearby non-Keep buildings once at dawn within a fixed total HP budget; no combat healing or income. Manual paid repair remains available. | Radius metric, budget, deterministic allocation order, repair timing relative to income/rewards, interaction with destroyed/sold buildings. |
| Enemy Archer | Ground attacker; stops at ranged attack distance to a deterministic valid building target. Can pressure a poorly screened economy without ignoring obstacles. | Target priority, firing position search, range/line-of-sight rule, blocked-route behavior, cooldown retarget/reset and death timing. |
| Bomber | Ground breacher with a visible fuse; attacks a blocking structure, dies on detonation and damages a bounded area. Killing it before detonation cancels the blast. | Fuse ticks, targets, splash footprint rule, simultaneous deaths, trap/reflection interaction, event order and telegraph. |
| Siege Commander (second boss) | Slow ground support boss; a bounded non-stacking aura improves nearby ground enemies. Killing it removes the aura; defenses remain preparation-based. | Aura effect/radius and update tick, whether self affected, overlap rule, entry schedule, escort budget and cue. |

These are proposed product decisions for the 008 specification stage, not claims that their full mechanics already exist. G0 must resolve the right-hand column into explicit tables. If playtests invalidate a role, document a replacement and its impact before coding; do not silently substitute a different system. Flyers are deferred because they introduce a second movement/targeting layer; they are not a cheap variant of Grunts.

## Milestones and execution steps

### G0 — Close the gameplay question and freeze rules

1. Complete 007's pending human/listening/live gates, or record failed criteria and fix the base experience first. The user has requested these plans; recruiting players, buying assets and committing production expenditure still require separate authorization.
2. Run a controlled diagnostic with identical seed, purchases and reward policy comparing a Tower ring, a corridor, and a trap corridor. Record route lengths, applied damage, losses and economy; change one variable at a time. This distinguishes dominant tactics from unclear information. Bound each simulated night by ticks and fail with the seed/layout on timeout.
3. Produce `specs/archive/005-release-rules.md`: exact tables for the six additions above; all twelve night compositions; all 24 perk IDs/effects/eligibility; map constraints; cost and income assumptions; save/profile behavior. Match `Definitions`, `PerkRules` and `MvpCampaign` naming. Classify each value as invariant or provisional tuning.
4. Produce `plans/008-content-matrix.md`: every new item has an encounter/decision it enables, a counter, teaching moment, audiovisual cue, author and verification. At least eight of the 24 perks must alter placement, targeting or timing choices rather than merely increase a stat. No proc-based perk until RNG/order and save rules are explicit.

**Gate:** `specs/archive/005-release-rules.md` (moved from `plans/008-rules.md`) and the matrix exist with no unresolved required contract field; the report cites completed 007 gates and the three-policy diagnostic. Human judgments are recorded as observations, not inferred from scripts. If participants are unavailable, mark G0's player gate PENDING and continue only independent specification/tool investigations.

### G1 — Establish release definitions, authoring and safe persistence

1. Add an explicit campaign/rules profile instead of editing `MvpCampaign.SixNightTable` into twelve nights. Preserve the six-night MVP as a regression/demo profile with a clearly separate label. Keep plain C# definitions authoritative; Unity assets supply validated authoring data converted at run creation.
2. Add a small release-content catalog and validator using the existing `MvpDefinitionsAsset`/`PrototypeContent` pattern. Validate stable IDs, missing references, invalid values, perk eligibility and wave entries. Validation must name the asset and field; no generic mod/plugin framework.
3. Freeze actual wave plans and map data before Day 1. Persist campaign ID, content/rules version, map profile, difficulty, stable definition IDs and all new rule-relevant building state. Presentation may change without rewriting outcomes. Cosmetics never consume map/reward RNG streams.
4. Preserve old MVP continuation through a legacy profile reader or explicit import to an equivalent legacy rules profile. Never convert an active six-night save into a twelve-night run implicitly. Unknown future formats stay untouched with an actionable message. Write upgraded copies atomically after success, retaining the original backup. Keep run/profile/settings migrations separate.
5. Use focused extraction around newly shared definition lookup if needed. Do not rewrite the entire simulation or navigation framework.

**Gate:** content validator returns no errors for release/MVP assets; a literal old save continues with old rules; new saves round-trip map, actual waves, IDs and new fields; invalid data is rejected before play without losing a durable file. Tests must cover meaningful changed compatibility risks, not catalog counts.

### G2 — Implement the tactical triangle in small increments

1. Add Cannon and its focused targeting/splash tests, then Bomber and its cancellation/simultaneous-event rules. Evaluate a compact authored encounter before adding support structures.
2. Add Watchtower and Repair Station with visible range/adjacency/repair previews. All quotes, effective values and paid results come from Core APIs, not duplicated UI arithmetic. Support effects recompute on construction, destruction and sale without per-frame object scans in presentation.
3. Add ranged Enemy Archer and Commander using the frozen target/route contracts. Do not reuse melee adjacency as an undocumented ranged pathfinding shortcut. Keep route preview honest about the ground entry routes and explain when an enemy stops to fire.
4. Extend the single Core event batch and existing audio/view consumers additively. Each new threat has a silhouette, telegraph, hit/death cue, sound and inspectable role. Pool effects with explicit caps. Preserve pause, last-kill presentation and reset cleanup.

**Gate:** focused EditMode tests protect the exact new failure modes: splash once per target, boundary/range tie, non-stacking support, one dawn repair, fuse canceled by death, ranged retarget timing, aura removal, and same-step Keep defeat precedence. Add PlayMode only for actual scene/input/event presentation integration. A bounded recorded scenario demonstrates each counter at 1× with no coaching.

### G3 — Author a twelve-night arc and tactical map profiles

1. Store authored threat recipes with bounded seeded variation. Seeded choices happen before the preview is shown; no hidden director reaction to the player's current defenses. Validate counts, composition, entries and boss limits.
2. Structure the arc: Nights 1–3 teach existing defense and economy; 4–6 introduce siege/ranged pressure and the first boss; 7–9 combine split fronts and support decisions; 10–12 test the chosen build and culminate in a different boss encounter. G0 supplies the exact wave table; do not implement these labels as random count multipliers.
3. Build three 20×20 profiles: a corridor-focused map, split-front map and open-economy map. Preserve guaranteed Keep access, buildable starting space and recoverable route options. Use authored skeletons with bounded seeded terrain variation, not unrelated random obstacle scattering.
4. Define constraints and fallbacks per profile; persist the actual accepted terrain. Test generation against the real routing/placement services, including 2×2 buildings, valid economy space and starting affordability. A profile must not force one mandatory building/perk.
5. Integrate 24 perks with frozen eligibility/stacking rules. No reroll by reload, duplicate income, rewards after final victory or saved offer containing an unavailable definition.

**Gate:** validator proves all twelve nights are previewable/resolvable and generated maps satisfy their named constraints on a declared bounded seed corpus. Representative scripts protect mechanical viability; blind playtests must show at least three distinct layout approaches across maps. Record losing cases as well as wins; do not optimize for a universal 100% scripted win rate.

### G4 — Produce one AA-quality slice before multiplying content

Choose one visually finished map profile and a compact encounter sequence featuring economy growth, two defense styles, ranged pressure, a breach and a boss. It must use the real campaign/save/content systems, not a disconnected trailer scene.

Deliver a consistent asset family: all nine buildings, seven enemies, damage states, gait/attack/death, terrain and foliage, lighting, UI icons, impacts, ambient beds, musical transitions and encounter stingers. Some art can remain provisional elsewhere, but the slice's visible/audio content must meet the chosen quality bar. Commissioning/purchasing is a separate owner decision. Synthesized 007 clips are integration assets until approved by listening; they are not assumed final AA audio.

Use a short art/audio bible under `plans/008-art-and-audio-bible.md` with scale, silhouette, materials, palette, animation events, mix priorities, naming/import/LOD conventions and approved reference captures. Establish generated versus manually authored ownership per asset. Existing setup scripts must not overwrite final work. Record time to produce one finished building, enemy and encounter, including iteration; plan 009 uses this throughput.

**Gate:** live/default-zoom review at 720p/1080p, listening approval, bounded VFX/audio and visible-window frame measurements on recorded hardware. Five returning participants can explain the new threats/counters; five new participants can learn the original loop without coaching. These small samples are directional evidence, not market forecasts. At least two approaches can defeat the same representative encounter without mandatory perks, and three approaches have distinct strengths across profiles.

### G5 — Handoff the production candidate

Ship the mechanically complete twelve-night build, the finished slice, validated content catalog, rules, migration fixtures, measurement results, known issues and production throughput. Report playtime distributions from observed runs, not a single ideal script. Compare against the 25–40-minute hypothesis and revise pacing deliberately.

**Gate:** all affected tests and final full suites pass, standalone build succeeds, old/new save scenarios pass, no blocker/crash/save-loss defect remains in the slice, and G4's human/visible/audio checks are complete. If not, mark the relevant gate PENDING or BLOCKED with evidence; do not label it AA-ready.

## Commands and verification

Commands below are verified against existing entry points by source inspection, **not run during this planning task**. Run from repository root. With an open Editor use that instance's test/build tools instead of a second batch instance. Respect owner restrictions on desktop interaction. Store output under ignored TestResults; copy only useful evidence into plans.

```powershell
$fortressEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
$fortressProject = (Resolve-Path 'game/The Last Fortress').Path
$fortressResults = Join-Path $fortressProject 'TestResults/008'
New-Item -ItemType Directory -Force -Path $fortressResults | Out-Null
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform EditMode -testResults "$fortressResults/EditMode.xml" -logFile "$fortressResults/EditMode.log"
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform PlayMode -testResults "$fortressResults/PlayMode.xml" -logFile "$fortressResults/PlayMode.log"
& $fortressEditor -batchmode -nographics -quit -projectPath $fortressProject -executeMethod TheLastFortress.Editor.BuildMvp.BuildWindows -logFile "$fortressResults/BuildMvp.log"
```

Expected: nonzero test totals, `result="Passed"`, no test failures; MVP `build-report.txt` says `Succeeded`, `errors=0`. These commands preserve the existing regression profile. G1 must additionally create `Editor/BuildRelease.cs` with `TheLastFortress.Editor.BuildRelease.BuildWindows`, output `Builds/Release/`, and the release scene only. It must also create `Editor/ReleaseContentValidation.cs` with `TheLastFortress.Editor.ReleaseContentValidation.ValidateAll`, throwing on invalid content. These are **new required entry points**, not existing commands. After their implementation run both using the same batch template (`-quit -executeMethod ...`) and expect exit 0, a zero-error validation report and release `build-report.txt` success. A passing MVP build alone cannot verify the release profile.

Run affected fixtures first with their actual fully qualified `-testFilter`, check nonzero matched tests, then full suites only at integration gates. Follow root AGENTS.md. Read `MvpCombatAndPerkTests.cs`, `MvpFoundationTests.cs`, `RoutingTests.cs`, `MvpReplayAndSaveTests.cs` and `MvpSceneTests.cs` before extending the nearest scenario. Preserve old semantic assertions; use new profile fixtures for new rules. No target count, balance snapshots, giant generic framework or full-campaign replay for every low-level rule.

## Scope, estimates, stop conditions and maintenance

**In scope:** relevant `Runtime/Core/`, `Runtime/Unity/`, `Editor/`, `Data/`, new `Scenes/Release.unity`, release prefabs/materials/audio/UI, focused tests, `.meta` files, project README and `plans/008-*`/evidence. This plan authorizes neither edits now nor changes to unrelated assets, packages, project settings, prototype rules or other work. An explicit engine/package upgrade must be justified and separately planned.

Planning allowance: approximately 4–8 months for a small multidisciplinary team covering gameplay engineering, technical/art direction, 3D animation, audio and QA/design, with some roles shared. This is a provisional staffing scenario, not a promise or vendor estimate. A solo developer should expect materially longer or reduce scope. G0 produces the actual work breakdown; G4 asset throughput revises it. Do not commit spending or dates on this estimate.

Stop dependent work if: the new role requires unspecified combat semantics; save compatibility cannot be preserved; ranged pathfinding would invalidate the ground-route contract; 2×2 support introduces unsolved placement assumptions; the 100-enemy budget is exceeded without measurement; G0/G4 player findings reject the premise; or the same verification gate fails twice after targeted fixes. Keep independent work moving and report the smallest decision needed. Do not weaken tests or hide failing seeds.

Deferred deliberately: flyers, controllable soldiers, bigger maps, online services, procedural storytelling and full content multiplication. Revisit only for a named player decision and a measured cost. Reviewers should inspect stable IDs, saved actual plans, deterministic damage ordering, support invalidation and audiovisual readability at default zoom. New content should extend validated definitions and existing presentation ownership, not add parallel simulation paths.

Final handoff: `plans/008-results.md`, completed gates G0–G5, `specs/archive/005-release-rules.md`, content matrix, art/audio bible, full `Builds/Release/` package, legacy build/regression results and a scoped production recommendation for plan 009. AA remains the destination; this milestone proves the design and production method that can support it.
