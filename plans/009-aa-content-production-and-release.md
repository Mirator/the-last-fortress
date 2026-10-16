# Plan 009 — Produce and ship the focused AA edition

> Executor: read this entire handoff before implementation. Establish the actual plan 008 baseline; its promised output is not evidence that it exists. Work through production, alpha, beta and release-candidate gates. Record results and the last verified action in `plans/009-results.md`, and update the index. Shipping and external publication require the owner's explicit instruction.

## Status and intended product

- **Status:** TODO; planning only. This document does not start implementation or publishing.
- **Priority / effort / risk:** P1 after 008 / L, multi-month / HIGH (production capacity, integration, saves and release quality).
- **Dependency:** a passed plan 008 production slice, mechanically complete twelve-night campaign, rules/catalog, compatibility fixtures, observed player results and measured asset throughput. Close plan 007's pending audio/live/player checks as part of that dependency.
- **Planned at:** commit `dc8b55a`, 2026-10-16, plus the uncommitted 007 implementation. Plan 008 is a future dependency, not implemented at this baseline.
- **Repository:** `C:/Users/Miroslav Pavelek/Documents/Games/the-last-fortress`; Unity project `game/The Last Fortress/`, Unity `6000.3.16f1`, URP, uGUI and Input System.
- **Category:** direction; full production, quality and release.

The intended AA edition is a polished premium Windows strategy game about shaping a fortress and reading the consequences. It has a distinctive authored world, a twelve-night arc, several viable approaches, meaningful map variation, professional art/audio, accessible controls and settings, reliable saves, tested performance and a complete release/support package. “AA” is an ambition for scope and quality, not an externally certified label. Keep preparation central; do not turn this into an action game, MMO or cinematic story campaign.

This plan completes content around validated systems. It does not authorize replacing the engine, rebuilding the simulation, or increasing every count to look larger. Scope is frozen from demonstrated player value and production throughput before content multiplication.

## Evidence and baseline to verify

Current repository facts, independently useful to a future executor:

- `specs/001-game-design.md`, sections 15–18, proposes four biomes, 15 buildings including Keep, ten enemies including four bosses, 50 perks, twelve nights, four difficulties and 20+ challenges. These were direction targets, explicitly not implementation acceptance fixtures.
- `Runtime/Core/Definitions.cs` currently has six stable building IDs; `MvpCampaign.cs` is the six-night table with 100 live enemies. `MapGenerator.cs` and checkpoint terrain depend on a fixed 20×20 board. Paths in this document beginning `Runtime/`, `Editor/`, `Tests/` or `Data/` are under `game/The Last Fortress/Assets/TheLastFortress/`.
- `Runtime/Core/RunCheckpoint.cs` uses schema 1 and `CheckpointCodec.RulesVersion="mvp-rules-1"`; stores actual terrain and waves and rejects incompatible rules. `Runtime/Unity/SaveStore.cs` now uses GameSettings v2 and a separate onboarding profile. Profile progression is not a run checkpoint.
- Plan 007 introduced bounded audio, shared event distribution, contextual help and presentation. `Audio/Source~/synth.py` supplies synthesized clips; `Editor/PresentationSetup.cs` links generated content. Final custom art/audio ownership must not be overwritten by setup regeneration.
- `Editor/BuildMvp.cs` currently builds `Scenes/Mvp.unity` only, Windows x64, non-development, into `Builds/Mvp/`. It writes a report and throws on failure. There is no release build/validation entry point at the baseline inspected for this plan and no `.github` CI directory. Do not invoke proposed automation as though it already exists.
- `plans/007-results.md` reports 92/92 EditMode, 16/16 PlayMode and a passing MVP build. Its captures are offscreen and performance run used a hidden window. Listening, live motion, visible-window performance and new-player sessions are still pending. No tests, build, dependency review or live review were rerun for this planning task.

Relevant source anchors:

```csharp
// Editor/BuildMvp.cs
public const string OutputDirectory = "Builds/Mvp";
public const string ExecutablePath = OutputDirectory + "/TheLastFortress.exe";
// BuildWindows constructs BuildPlayerOptions with:
// scenes = new[] { MvpSetup.ScenePath }, options = BuildOptions.None
```

```csharp
// Runtime/Core/RunCheckpoint.cs — baseline, not a future-release promise
public const int CurrentSchema = 1;
// CheckpointCodec
public const string RulesVersion = "mvp-rules-1";
```

At entry run `git status --short` and `git diff --stat dc8b55a..HEAD -- "game/The Last Fortress/Assets/TheLastFortress"`; inspect uncommitted and untracked work as well. Read `plans/008-results.md`, `008-rules.md`, `008-content-matrix.md` and `008-art-and-audio-bible.md`. If they are missing or gates are unmet, perform production planning only and mark dependent content work PENDING. Do not fabricate the release systems or rewrite another task's working files.

## 1. Freeze the production edition before scaling it

Create `plans/009-production-contract.md` with named owners, costs/effort, dependencies, asset throughput, risks, capacity and contingency. Financial figures must come from the owner or actual quotes, not invented industry rates. No procurement or contractor contact is authorized by this plan.

Use the following **proposed ceiling and launch candidate**. Totals include existing content; bosses count within enemies, and upgrade tiers/cosmetic variants do not inflate building/enemy totals.

| Area | Expected 008 handoff | 009 launch candidate | Ceiling / admission rule |
|---|---|---|---|
| Building kinds | 9 including Keep | 12 including Keep | Up to 15 only when each adds a distinct decision and passes throughput/counterplay review. |
| Enemy kinds | 7 including 2 bosses | 10 including 4 bosses | No additional ordinary enemy required beyond the ten total. |
| Biomes | 1 | 3 | Fourth only if it changes tactical conditions and fits the asset budget. |
| Map profiles | 3 | At least 2 distinct profiles per biome | Palette swaps do not count as tactical profiles. |
| Perks | 24 | 40 | Up to 50 after relevance and combination review; no filler percentage duplicates. |
| Nights | 12 | 12 | No longer campaign without renewed pacing validation. |
| Difficulties | 1 | 3 named settings | Fourth only for a distinct validated audience; always preserve one standard baseline. |
| Challenges | 0 | 12 authored scenarios | Up to 20 when individually tested and meaningfully different. |

This deliberately narrows the original GDD while preserving its direction. At the production gate, record either this candidate, the larger ceiling with a funded schedule, or explicit cuts. Update the GDD to reflect the chosen edition; do not maintain contradictory promises. A smaller coherent release may pass; an unfilled target table may not be silently described as complete.

Proposed content briefs to validate, not ready-to-code mechanics:

- **Three additional structures:** a slowing field/trap that buys firing time, a costly limited-capacity reinforcement that changes breach planning, and a specialist long-range weapon with a minimum-range weakness. Check overlap with Archer, Cannon, Watchtower and Walls before accepting them. They require footprint, targeting, status/stacking, economy, save-ID and preview contracts first. No implicit Barracks/soldier system.
- **Additional ordinary enemy:** with 008's five ordinary enemies and two bosses, add one ordinary shielded unit; a directional or timed protection rule should reward crossfire rather than immunity to a mandatory weapon. Define facing, mitigation and telegraph before implementation. Two additional boss identities bring the total to ten enemies/four bosses.
- **Two new bosses:** one tests split-front preparation, one tests reserve capacity and recovery. Use bounded authored attack/escort patterns and visible warnings. Do not require reflex input, new resource bars or unannounced invulnerability. Every boss needs more than inflated health.
- **New biomes:** a wooded river frontier and an exposed rocky coast, alongside the existing grassland fortress. Terrain constraints and approach geometry change viable placements; no fog, height-based damage or weather simulation is implied. Environmental effects remain cosmetic unless separately specified and previewed.

Create `plans/009-content-contracts.md` with the same exact rule fields used for 008 additions. Freeze an item before implementation; reject a redundant role instead of filling the slot. Unknown final prices, DPS, radius or mitigation are design-stage outputs, never permission for an executor to improvise.

**Production gate:** contracts, launch scope, workload and capacity are recorded; every accepted item has a player decision, counterplay, teaching point, final asset list and owner. If estimated effort exceeds funded capacity plus an explicit contingency, cut optional content before starting it. Keep the approved slice quality bar intact.

## 2. Build a repeatable content and audiovisual pipeline

### P1 — Turn the slice into a production standard

1. Promote the 008 bible into an asset acceptance checklist covering scale/pivots, footprints, silhouette, material conventions, texture/LOD/import settings, animation states/events, VFX pools, audio family limits, UI icon readability, licensing and provenance. Assign an authoritative source for every asset: generated or authored.
2. Create a small in-project review scene for all building/enemy states at actual gameplay camera scale, Day/Night lighting, damaged states and crowded combat. It uses real catalog references and is excluded from release scenes. Avoid an elaborate generic asset management framework.
3. Finish one representative new biome/structure/enemy/boss first, measure iteration cost, then schedule batches. Integrate art with gameplay each batch; do not defer all silhouettes, impact cues or mix work to the end.
4. Produce final music/sound with deliberate motifs for planning, pressure, bosses, dawn and endings. Retain priority mixing, mute and pause semantics; improve the content through listening, not by making it louder. Add accessible non-audio equivalents for every gameplay signal.

**Verification:** catalog/asset validator passes, no missing/pink materials or broken references, no setup regeneration overwrites authored assets, real-time visual and listening approval against the slice. Record review decisions and actual throughput in `plans/evidence/009/production.md`.

### P2 — Deliver content in playable biome batches

For each batch, ship its maps, encounters, buildings/perks if applicable, art/audio, tutorial explanations and save fixtures together. Keep authored wave recipes and deterministic maps visible to previews. Validate the real map constraints including multi-cell placements and fallback behavior. Keep the live-enemy cap at the validated 008 setting; raising it is a performance/design change, not routine content authoring.

Expose map and difficulty selection with expected run length and rule differences. Make challenges local and reproducible, storing a stable challenge ID/version, seed, rules and completion status. No server, global leaderboard or daily live service is required.

Progression is horizontal: recognize mastery through optional challenge rewards, cosmetic identifiers and newly available choices. The initial loadout must remain winnable. Unlock conditions have deterministic single-award behavior; replaying or loading a terminal run cannot grant rewards repeatedly. Profile corruption/recovery must not destroy active run files. Avoid introducing grind solely to stretch playtime.

**Verification:** batch-specific scenarios pass; two viable approaches remain on a representative shared map, and all three 008 approaches retain useful matchups across the release. Validate challenge objectives against actual outcomes with bounded simulation; use a few end-to-end runs rather than one campaign test per content item. Collect observed failures, weak perks and dominant builds for targeted balancing.

## 3. Complete the product experience

### P3 — Accessibility, input, localization and settings

- Finish mouse/keyboard remapping with conflict handling, reset and persistence; remapping must not trigger world actions while typing in seed/settings fields. Preserve cancel/pause behavior and UI focus. Controller support is a separate scope decision, not silently implied by AA or Steam.
- Provide independent text/UI scale where feasible, visible focus, reduced motion, camera-shake toggle and readable alert alternatives. All informational colors retain text/shape distinctions. Explicitly test 1280×720, 1920×1080, 2560×1440 and one ultrawide viewport, plus the supported UI-scale extremes. Critical actions remain on screen.
- Externalize player-facing text with stable keys and parameterized numbers, pluralization and wrapping. Keep English as source; choose translated languages from a funded owner decision. Test expanded pseudolocalized text and missing-key fallback before commissioning translation. Do not automatically migrate every UI component merely to externalize strings.
- Finish display/audio/settings behavior, safe defaults and revertable display changes; confirm changes or roll back after a bounded timeout using a clock that still runs while gameplay is paused. Settings corruption uses clear recovery without destroying valid run/profile records.

**Verification:** focused UI integration protects actual changed risks (remap conflicts, input-field isolation, scale/focus, confirmation timeout and migration). Inspect real screens and text, including long perk stacks, localized boss warnings and save failures. Do not assert cosmetic pixel equality or mirror implementation helpers.

### P4 — Release architecture, diagnostics and optional platform integration

1. Preserve 008's explicit rules/content IDs and legacy save support. Maintain fixtures from MVP, 008 and each supported production schema. Define support/migration policy before removing a legacy reader. Unknown newer files are preserved. Atomic writes retain backups; conflicting files are never silently overwritten.
2. Produce deterministic release artifacts with application/build/content version, scene list, source baseline manifest, package manifest/lock references and known issues. “Same executable name” is insufficient to identify a candidate. Freeze dependency versions at RC; any upgrade gets isolated verification. Do not upgrade Unity simply to appear current.
3. Add one documented local verification entry point using existing Unity commands, release validation and report checks. Reuse a runner only after the local flow is reliable. If CI is introduced, document Unity licensing, cache, secrets and Windows build requirements; never store credentials in the repository. CI is not an excuse to rewrite tests.
4. Provide useful local diagnostics: build ID, seed, profile, rules version, last phase, errors and performance settings, excluding personal data or secret values. Crash/telemetry upload is optional and requires a separate privacy/consent decision; offline play cannot depend on it.
5. Steam achievements/cloud integration may follow only when authorized and accounts/app identifiers exist. During implementation verify current official platform documentation and policies; this plan does not assert SDK or store requirements. Local saves and play work without a platform client. Define conflict, offline and backup behavior before enabling cloud synchronization; do not equate syncing files with safe save merging.

**Verification:** clean-machine installation and offline play, old-save continuation, failed writes, controlled process interruption during saves, settings/profile recovery, repeatable content validation and traceable build reports. Test interrupt/recovery in temporary save folders only. Platform tests remain explicitly N/A if integration is excluded, or PENDING if it is promised but unavailable.

## 4. Quality milestones and measurable release gates

### Alpha — Feature and content complete

All frozen launch content works end to end with no placeholder mechanics, missing definition IDs, unimplemented challenge objectives or missing mandatory UI. All required art/audio has an accepted production owner; alpha may track remaining polish in an explicit finite list. At least one complete run per map-profile family and difficulty is observed; automated seeds supplement human review. No compile failure, known save loss or blocker prevents completion.

Freeze feature additions at alpha. New requests replace equivalent work or become post-release candidates. If the product still depends on a missing major mechanic, it is not alpha.

### Beta — Content and systems stable

1. Use a declared hardware matrix: minimum, recommended and a higher-tier machine, recording CPU/GPU/RAM/OS, resolution and quality preset. Proposed baseline to evaluate: Ryzen 5 3600 / GTX 1660 SUPER / 16 GB at 1080p. This is a test candidate drawn from 007's reported machine, not published minimum hardware. Confirm it with visible-window evidence.
2. On approved recommended hardware at the selected preset target 60 FPS, p95 frame time ≤16.7 ms and p99 ≤33.3 ms during representative worst-case **visible** combat. Record CPU/GPU timing, memory, active units/effects/voices, 10-second warmup and at least 120-second samples where scenarios permit. For short encounters use a declared repeatable sequence, not invented duration. Hidden-window measurements cannot pass this gate.
3. Proposed practical budgets: cold launch to interactive menu ≤15 s and new-run load ≤8 s on the declared reference SSD; no loading-related save stalls above 100 ms during normal play. Record measurements and revise budgets before acceptance if the agreed hardware changes.
4. Five-hour soak or an equivalent documented repeated-run sequence shows no unbounded retained objects, sources or memory. Compare settled states after warmup and garbage collection, not transient allocation peaks. Test pause, minimize/focus return, display changes and audio-device changes with known, recoverable behavior.
5. Run two observed playtest rounds including new and returning players. A proposed minimum is 12 participants across the rounds; record experience level and repeated participants. Check onboarding, threat comprehension, map differences, perk relevance, defeat attribution and voluntary replay. Small-sample observations guide fixes; do not invent retention/market-success claims.

**Beta gate:** the frozen content matrix is complete, performance matrix meets agreed budgets, human/art/audio/accessibility review is signed off, and no open blocker, crash, save-loss or reproducible major combat exploit remains. Triage remaining issues by impact and owner. Do not silently waive a failed criterion; record an explicit scope/target decision.

### Release candidate — Ready to distribute, publication separate

Build from an identified frozen source state. Run the full suites, content/asset validation and clean-machine smoke on the actual packaged artifact. Complete two consecutive candidate-verification passes without a blocker and without new candidate-changing code between a pass and its artifact; a relevant fix produces a new candidate and reruns affected checks. Never repeat passing expensive checks without a candidate change or unresolved concern.

Deliver the complete executable/data package, version manifest, controls, Credits/licenses, support/troubleshooting notes, known issues, migration policy, local diagnostic instructions and archive/rollback procedure. Use actual gameplay captures from the release candidate for approved marketing assets. Check rights for art, music, fonts and middleware; identify unresolved rights as blockers, not presumed permission.

The release checklist records store/platform verification separately. Preparing screenshots, a trailer brief, store copy and a package is allowed within future authorized implementation; sending them, creating external posts, uploading or publishing requires direct authorization. Budget, price and release date are owner decisions. A green technical checklist does not authorize release.

## 5. Verification commands and testing boundaries

Use the existing commands below from the repository root for regression. They were inspected, not executed for this plan. Use the already-open Editor's tools if applicable; never open a second instance on the same project. Follow current restrictions on windows and desktop control. Standalone measurement requires a permitted visible environment; otherwise leave that gate PENDING.

```powershell
$fortressEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
$fortressProject = (Resolve-Path 'game/The Last Fortress').Path
$fortressResults = Join-Path $fortressProject 'TestResults/009'
New-Item -ItemType Directory -Force -Path $fortressResults | Out-Null
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform EditMode -testResults "$fortressResults/EditMode.xml" -logFile "$fortressResults/EditMode.log"
& $fortressEditor -batchmode -nographics -projectPath $fortressProject -runTests -testPlatform PlayMode -testResults "$fortressResults/PlayMode.xml" -logFile "$fortressResults/PlayMode.log"
& $fortressEditor -batchmode -nographics -quit -projectPath $fortressProject -executeMethod TheLastFortress.Editor.BuildMvp.BuildWindows -logFile "$fortressResults/BuildMvp.log"
```

Expected: nonzero totals, XML `result="Passed"`, no failures; MVP `Builds/Mvp/build-report.txt` says `Succeeded`, `errors=0`. A successful exit without results is not a pass. After 008 has created them, also invoke `TheLastFortress.Editor.ReleaseContentValidation.ValidateAll` and `TheLastFortress.Editor.BuildRelease.BuildWindows` via the same `-batchmode -nographics -quit -executeMethod` pattern, with separate logs. Expect zero validation errors and a successful `Builds/Release/build-report.txt`. Verify these symbols in the current code before invocation; they do not exist at this planning baseline. If 008 chose different documented symbols, use the actual ones and update this plan's recorded commands.

Extend validation in production to accepted asset ownership/import/reference constraints and emit a machine-readable release inventory. No new command is considered available until implemented and exercised. Put final reports under `plans/evidence/009/` with artifact IDs and timestamps; retain raw transient captures in ignored results directories when unnecessary for review.

Follow root AGENTS.md: read nearest tests, run affected cases first and expand at integration gates. Use EditMode for definitions, placement, navigation, economy, modifier composition, deterministic generation, objective evaluation and migration. PlayMode is for actual scenes, input, audio/UI lifecycle and platform-adapter integration. Use a few representative complete runs, not a combinatorial campaign suite. Test broken contracts, not item counts or provisional balance constants. Preserve existing meaningful assertions; content count targets are a production inventory check rather than gameplay unit tests.

## 6. Scope, staffing, schedule and stop conditions

**Future implementation scope:** release-relevant `Assets/TheLastFortress/` Core, Unity, Editor, Data, scenes and art/audio/UI, focused tests, project documentation, narrowly justified settings/platform adapters, packaging scripts and verification configuration, plus `plans/009-*`/evidence. Platform packages, new licenses and CI services require an explicit dependency/cost decision. Preserve MVP/prototype profiles and unrelated work; no reset, commit, push, purchase, external messaging or publication is implied.

Provisional allowance **after** the 008 slice: 8–16 months with a small multidisciplinary production team, sharing engineering/design, environment/character art, animation/VFX, audio, QA and production roles. Together, 008 and 009 suggest roughly 12–24 months, subject to scope, staffing and rework; this is a planning scenario, not a funded schedule or guaranteed AA result. Reserve an explicit 20–30% capacity contingency as a proposal to validate in the production contract. Solo work or part-time specialists changes the critical path substantially. Do not convert these estimates into a monetary budget without actual staffing/quotes.

Recommended order: production contract → pipeline sample → playable content batches → product/accessibility work → alpha freeze → beta/optimization → RC. Diagnostics and compatibility work begin early; no end-of-project rewrite. Art, engineering and QA may run concurrently only with explicit file/asset ownership and integration gates; this document does not dispatch agents or contractors.

Stop dependent work and report if: 008's slice is not validated; final content has no distinct decision/counter; required assets exceed capacity; authored work is overwritten by setup; a change breaks supported saves; visible performance cannot meet the approved target without measured scope cuts; release rights or required account access are unresolved; or the same gate fails twice after focused repairs. Continue independent safe work and identify the smallest necessary decision. Missing players/hardware/platform access remains a named PENDING gate, never a fabricated pass.

Do not add multiplayer, soldiers, flyers, huge battle counts, procedural narrative or extra biomes to rescue a failed schedule. Cut optional breadth while preserving identity, varied tactics, legibility, stability and the proven slice quality.

## Final done criteria and maintenance

- [ ] Production contract and frozen content matrix match the implemented edition and updated GDD.
- [ ] All accepted content has complete rules, presentation, teaching, rights and validation; no placeholders are described as final.
- [ ] MVP/008/release saves, settings and profiles pass the documented supported migration/recovery matrix.
- [ ] Full suites, release validation and actual release package build pass with traceable reports.
- [ ] Visible hardware/performance, live motion, listening, accessibility and observed-player gates are complete.
- [ ] No open blocker/crash/save-loss/major exploit remains; residual issues have explicit disposition.
- [ ] RC package, credits, support material, diagnostics, rollback and future patch policy are ready.
- [ ] Publication status is separately recorded; local readiness is not reported as a public release.

Write `plans/009-results.md` with passed/pending gates and artifact locations. Maintain stable content/rules versions, profile award idempotency and legacy support across patches. Review each future item as a player decision plus production cost; avoid expanding the catalog at the expense of learning, readable battles or reliable continuation. The completed result is a supported focused game, not just a larger prototype.
