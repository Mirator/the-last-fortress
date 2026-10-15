# Improvements from agent playtests and focused reviews

2026-10-15. Status: proposed follow-up specification; no gameplay changes implemented by this task.

## Purpose and evidence

Make the existing six-night MVP's decisions easier to evaluate, then investigate the difference between economic and defensive openings. Preserve the rules in [003-six-night-mvp.md](003-six-night-mvp.md) except where a future, recorded balance decision explicitly changes them.

Two GPT-6 Luna subagents performed separate background simulation passes after the owner requested no desktop interaction. The drivers compile the actual `Runtime/Core/*.cs` from commit `cea7c4f` with .NET SDK 10.0.300. Default tuning was checked against `Data/MvpDefinitions.asset`; these are source-level gameplay simulations, not runs of the rendered standalone build.

- [Playtester A: mistakes, recovery, perks and checkpoint continuation](../plans/evidence/006-playtest-a/report.md), [seed-1 transcript](../plans/evidence/006-playtest-a/scenario-run.txt), [seed-2 transcript](../plans/evidence/006-playtest-a/scenario-run-seed2.txt), [driver](../plans/evidence/006-playtest-a/scenario/Program.cs).
- [Playtester B: strategy comparison](../plans/evidence/006-playtest-b/report.md), [eight fresh policy runs](../plans/evidence/006-playtest-b/policy-runs.txt), [driver](../plans/evidence/006-playtest-b/harness/Program.cs).

The earlier standalone capture attempt failed before gameplay input; its tooling timeout is not a game defect. After the owner's change of direction, neither agent used the desktop. Background console processes ran locally. No production code, tests, tuning, or user saves were changed.

No screenshots, visual inspection, mouse/keyboard usability findings, wall-clock session times, or human fun/balance validation resulted from these simulations. Existing saved test/performance reports are historical evidence and were not rerun here. This work does not satisfy the five-fresh-player acceptance gate in plan 005.

Three additional GPT-6 Luna subagents subsequently reviewed gameplay immersion, graphics/motion and UI without launching the game or controlling the desktop. These were source and saved-evidence reviews, not three further hands-on playtests. Graphics/UI reviews inspected historical plan-005 screenshots; the later `feedback-*` Day captures show the owner-feedback fixes, while older menu/combat/reward captures may predate them. Motion, current runtime layout and player comprehension remain unverified.

- [Immersion review](../plans/evidence/007-immersion/report.md): damage/loss feedback and the connection between nights and subsequent decisions.
- [Graphics and motion review](../plans/evidence/007-graphics-motion/report.md): shot-event lifecycle, movement, hit feedback, pause clocks and lighting transitions.
- [UI review](../plans/evidence/007-ui/report.md): restart/quit semantics, tooltip bounds, control discovery and selection layout.

The requirements below consolidate overlapping recommendations. Historical income-breakdown wrapping, missing building descriptions, missing new-side markers and coplanar water were addressed by the owner's earlier feedback fixes; they are not new unresolved findings here. The compact current income forecast/receipt should be retained.

## What the simulations established

| Scenario | Fresh observation | Interpretation |
|---|---|---|
| Economy-first, generated seeds 1–3, normal rewards | 3 victories; final Keep HP 500/500, 650/650, 650/650 | This fixed opening is forgiving in this small sample. |
| Defense-first, same seeds, normal rewards | Seeds 1–2 win at 150/650 HP; seed 3 loses on Night 5 | This fixed defensive policy has less margin and is sensitive to seed/reward differences. |
| Seed 1, rewards skipped through a debug API | Economy wins at 472/500 HP; defense loses on Night 5 | A diagnostic comparison, not a player-accessible mode or proof that perks are unnecessary in general. |
| Generated seed 3, Farm at `(4,13)` | Placement rejected as impassable; the economy policy still wins | Terrain can require moving an opening. Rejection itself is correct. |
| Flat seed 1, third Tower then unaffordable Farm | Farm rejected without spending; selling the 70-gold Tower refunds 35; Farm purchase succeeds | Economic mistakes are recoverable, with a real resale loss. |
| Flat seed 1, Sharpshooters selected after Night 1 | Tower damage changes from 10 to 12 | Effective stats matter when evaluating rewards. |
| Flat seed 2, Efficient Farming selected after Night 1 | Pre-choice forecast 45; post-choice forecast and payment 52 | Reward choice can alter the immediately upcoming income payment. |
| Flat seeds 1–2, Day 2 checkpoint captured/restored in memory, then Night 2 stepped on both simulations | Matching gameplay snapshots and reward-phase outcomes, Keep 500/500 | These Core continuation scenarios worked. File persistence and Continue UI were not exercised. |

The policy drivers use fixed purchase coordinates and perk preferences, skip rejected purchases, and repair the Keep when possible. They do not search for optimal strategies, relocate every blocked purchase, or represent novice decision-making. Seed changes affect both terrain and reward offers. Do not attribute a difference to terrain or a specific perk without isolating that variable.

## P1 — Show effective selected-building values

**Problem:** `HudView.Refresh` in `Runtime/Unity/HudView.cs` constructs selected Tower damage/range from `sel.Definition`, while combat uses `RunSimulation.TowerDamage` and `TowerRange`. Farm selection similarly starts from base daily income and adds the House-link text without showing the final perk-adjusted value. A's fresh run demonstrates the underlying 10 → 12 damage change; the display mismatch is confirmed by source inspection, not a screenshot.

**Required behavior:**

1. Selected Tower details show current effective direct damage, attack interval and effective range from the authoritative simulation. Last Stand's conditional bonus updates as the Keep crosses its threshold. Burn remains separately described; it must not be included in the direct-hit number.
2. A selected Farm shows its final daily income using the same rounding, adjacency and perk rules as the income transaction. A House shows its own income. Keep the optional breakdown concise, for example `Income +39/day · House link · Efficient Farming`.
3. Build-card effect lines that show numerical damage, range or income must state whether a value is base or effective. Prefer effective values when they can be calculated for the candidate; Farm adjacency depends on placement, so do not advertise an unearned adjacency bonus.
4. Range overlays continue to agree with effective attack range. Preserve the existing damage, income, rounding and targeting rules; this is a presentation correction.

**Acceptance:** with an owned Tower, select Sharpshooters and see 12 direct damage rather than 10; Expanded Vision shows range 4.4 rather than 4.0. Exercise Last Stand across its actual threshold. For an adjacent House/Farm with Efficient Farming, selection and forecast agree on the Farm's income: `floor((25 + 5) * 1.3) = 39`. Verify rendering later at 1280×720 and 1920×1080 without clipping. Do not create separate tests for every label or provisional balance constant; extend the nearest meaningful HUD integration scenario and retain Core rule coverage.

## P1 — Explain how a reward changes the imminent income payment

**Problem:** `HudView.RefreshReward` says `Dawn ... pays +N gold after your choice`, using the forecast before a choice. Efficient Farming is applied before dawn income is calculated. A's seed-2 scenario directly recorded a 45-gold forecast before choosing the perk and a 52-gold payment at dawn. B's linked-Farm opening similarly pays 74 rather than its pre-choice 65. The payments are correct; the header's unconditional promise is misleading. The header behavior was source-inspected, not visually played.

**Required behavior:**

1. The reward header labels the displayed amount as the current forecast, or explicitly says income-changing rewards can alter it.
2. For an offered income-changing reward, show the actual prospective next payment for the current surviving fortress. Example: `Next dawn: +65 → +74 gold` for one linked Farm, one House and the Keep. Calculate using authoritative rules without consuming RNG, granting perks, paying income, modifying the fortress, or changing the frozen offer.
3. Choosing that reward applies its effect and pays income once. Do not change checkpoint/offer-ledger behavior to implement the preview.

**Acceptance:** reproduce the linked-Farm seed-2 opening, record the forecast, offer-specific preview and received payment, and verify they agree. A full-health Keep perk must not imply an income change. Previewing or redrawing cards must not alter the offer, RNG or gold. Extend an existing reward/HUD case only where needed to catch this concrete discrepancy.

## P2 — Investigate defensive recovery before changing balance

**Problem:** the fixed defense opening repeatedly cannot afford its next Tower: it has 60 gold on Day 2 against a 70-gold cost, with only 20 recurring income. Economy-first earns 373–498 gold across the three normal runs, versus 80–100 for defense-first. This creates a narrow recovery margin in these policies, but it does not establish that all defensive strategies are invalid.

**Next decision and acceptance:**

1. Compare at least one alternative defense-led opening that adapts its purchases to income and blocked terrain on the same seeds 1–3. Use normal offered rewards and ordinary placement/repair APIs. Record purchased/skipped buildings, repair spending, surviving defenses, Keep HP and outcome by night.
2. Distinguish terrain effects from reward luck using a clearly labeled diagnostic comparison if necessary; do not secretly grant favorable rewards in a claimed normal run.
3. State whether the desired second approach is pure defense with guaranteed Keep income or defense-led with delayed economic investment. Do not silently turn the goal into identical outcomes for all openings.
4. If adaptive defensive play still lacks a reasonable recovery path, propose the smallest supported tuning change with before/after logs. Follow the existing bounded-tuning policy in plans 004/005; broader changes require an explicit new decision. No automatic starting-gold increase, kill bounty, mid-night construction, free repair, or guaranteed optimal rewards.
5. Retain a passing economy approach and document the tradeoff introduced. Simulation wins support mechanical viability only; fresh players must still demonstrate understandable, genuinely different approaches.

This requirement authorizes investigation in a future implementation task, not a balance patch based solely on the eight runs here.

## P2 — Preserve clear recovery from construction mistakes

**Evidence:** A observed reserved-ring, occupied-Keep and insufficient-gold rejections, with no spend on the checked rejected transactions. Selling one Tower allowed the Farm plan to continue, while losing half the Tower's purchase price. B encountered an impassable Farm footprint. No visual feedback failure was observed.

**Follow-up acceptance, not a confirmed UI bug:** inspect the existing ghost/rejection/sale flow when rendered testing is available. A player should be able to identify a blocked footprint, move the entire 2×2 Farm to legal land, distinguish insufficient gold from impassable terrain, and see the exact sale refund before selling. Preserve the current rejection reasons and atomic transactions. Change UI only if that inspection establishes a specific gap; do not add redundant prompts, new resource rules or duplicate tests for behavior that already works.

## P1 — Keep lethal shots visible after enemy removal

**Source-confirmed issue, not a rendered reproduction:** `RunSimulation.Step` calls Tower attacks and removes dead enemies before returning (`Runtime/Core/RunSimulation.cs:419–427`, `:995–1003`). `TowerShot` is emitted before damage, but `WorldView.Sync` drains it after stepping. `SpawnArrow` looks up the now-removed target and returns when it is absent (`Runtime/Unity/WorldView.cs:253–275`, `:445–449`). The view also removes dead enemy models in that sync. The most decisive hit can therefore lose its firing cue altogether.

**Required behavior:** preserve sufficient immutable shot/impact position data, or an equivalent small presentation snapshot, to display a lethal shot after its target is removed. The current `TowerShot` event does not supply a usable impact position; do not assume its default cell is the target. Give kills a brief bounded impact/death cue without keeping a dead enemy in simulation occupancy or delaying damage. Handle several simulation steps between view updates and reset/load cleanup. Retain nonlethal fire cues.

**Acceptance:** a lethal hit, a nonlethal hit, and a target killed before the next view sync all produce the appropriate bounded cues at the correct location. A targeted presentation integration case should catch the missing-target event failure; do not add a full-run scenario for every effect. Confirm behavior visually later, including the last kill that enters Reward/Victory.

## P2 — Make attacks and fortress losses readable

**Source-backed concern:** the current building reaction is a small 0.18-second scale pulse; building destruction removes the view. Enemy attacks and Tower fire have little distinct contact feedback beyond those cues and HP bars (`WorldView.cs:257–275`, `:314–330`, `:445–479`). Historical Night 4/6 captures show a dense Tower/Keep cluster; plan 005 records partial Keep occlusion. Static captures cannot establish whether the existing reactions are sufficient in motion.

**Desired behavior:** provide short, distinct cues for a successful hit, a Keep hit and a destroyed structure. A persistent HUD Keep-health indicator should briefly signal a new hit even if the Keep is outside the camera view; on-board cues stay attached to their targets. Prefer restrained flashes, impact shapes or a short collapse/fade over constant damage-number clutter. Reuse the lethal-shot work rather than adding a second effect system. Effects must not suggest changed footprints, obstacles, range or damage timing.

**Acceptance:** inspect a clustered Night 4/6 battle at default zoom at both supported resolutions, with camera shake disabled as well as enabled. Observers should identify the first Keep hit and a frontline loss and associate visible contact cues with the correct targets. Bound simultaneous effects and their lifetimes; clear them on retry/load. Treat player comprehension as a pending usability check, not a claim established by this review.

## P2 — Connect each night to the next decision

**Experience recommendation:** existing Reward and terminal screens communicate phase/reward choice and run totals, but not a compact account of the just-finished night's losses. The immersion reviewer recommends a clearer build–defend–recover connection; no fresh player demonstrated confusion.

**Desired behavior:** add a concise recap to the existing Reward and Defeat surfaces: enemies defeated this night, Keep HP lost to damage this night, and buildings destroyed in combat this night. Keep these distinct from run totals. Define HP lost as damage actually applied to the Keep, excluding overkill, rather than net HP change across a later perk/repair. Exclude sales, scripted removals and resets from combat losses. Do not add a score, extra reward screen or new gameplay currency.

**Acceptance:** a clean night, a building-loss night and a defeat show correct night-local totals; a Keep-health reward or repair does not rewrite the preceding damage total. Check one continuation from a Day checkpoint so replay totals are not counted twice. Extend an existing rules/checkpoint fixture only if these new counters introduce that risk. Later ask fresh players to name one consequence before choosing their next construction/reward; use their responses to evaluate whether the recap helps.

## P2 — Give enemy motion and pause a consistent presentation clock

**Source findings:** enemy views interpolate position and facing but apply no gait (`WorldView.cs:334–362`). Arrows and building reactions consume view delta time even when combat is paused; burn motion uses `Time.unscaledTime`, and the bootstrap still synchronizes views when paused or in a menu. These establish current behavior, not whether players dislike it.

**Desired behavior:** use a combat-presentation clock that freezes combat arrows, attack/hit/death reactions, gait and burn motion when Night is paused or suspended by the menu. Camera pan/zoom may remain available during explicit Night pause. Add restrained procedural walking/attack motion to existing enemy models: gait runs while moving, stops while stationary, and contact motion follows actual attack events. Runner motion may be quicker and Brute/Golem motion heavier; animation cannot alter authoritative positions, attack cadence, occupancy or RNG. No rigging package or new enemy ability is required.

**Acceptance:** pause with a projectile and burning enemy visible, inspect with the camera, and resume without an FX jump or duplicate event. Open/close the menu during combat with the same result. Watch each existing archetype walk, stop and attack; the model remains grounded and readable at default zoom. For low-priority lighting polish, inspect the current immediate Day/Night cut before changing it; if distracting, add a short presentation-only blend with readable enemies throughout. Do not delay wave starts or reward availability for a lighting transition.

## P1 — Make restart and exit consequences explicit

**Source-confirmed behavior:** the top-right Restart directly resets the run; the menu's Quit game directly calls `Application.Quit`. Day construction is checkpointed by Save and Quit or before Night, not after every purchase (`HudView.cs:128`, `HudView.Mvp.cs:109–111`, `PrototypeBootstrap.cs:220–230`, `:291–300`). A prior durable slot can survive Restart, but this does not make the active run's reset or unsaved Day edits obvious; older slots are not currently listed in the menu.

**Required behavior:** distinguish `Retry same seed — start Day 1` from resuming the current checkpoint. Before replacing a progressed active run, offer Cancel and explain the reset; terminal same-seed retry can stay immediate. If Quit game would discard unsaved Day edits, offer save-and-exit, exit without those edits, and Cancel, using the existing durable save path. Do not exit after a failed save or report progress saved before commit. During Night/Reward, explain the existing return-to-prior-Day checkpoint policy rather than promising a mid-combat save. Do not introduce confirmation prompts for routine placement or every sale.

**Acceptance:** make a Day purchase/repair, then cancel Restart and Quit separately; gold, HP, buildings and checkpoint remain unchanged. Confirmed actions do exactly what their labels promise. A save-and-exit failure keeps the current run available with actionable feedback. Confirmed restart after a checkpoint still says Day 1 even when the older checkpoint remains durable in another slot. Retain the fast terminal retry path.

## P2 — Keep tooltips and selection actions readable

**Source-level layout risks:** placement tooltips have a fixed top-left pivot and pointer offset with no canvas-edge bounds check (`HudView.cs:179–184`, `:424–439`). Selection details use a fixed 350×70 body followed by fixed-position action buttons, while detail content varies by building (`:155–165`, `:403–420`). Tooltip clipping and longest-case selection overlap were not reproduced in a fresh rendered session; historical centered/Farm examples do not cover the risk.

**Required behavior:** clamp or flip the full tooltip inside the canvas, retaining price, footprint, rejection reason and applicable rotation hint. Reflow selection details/actions as necessary to accommodate the effective-stat correction above. Preserve the exact refund and repair quote before the action; keep current compact income/receipt text rather than restoring the superseded source breakdown.

**Acceptance:** at 1280×720 and 1920×1080, inspect valid/invalid ghosts near every screen edge after pan/zoom, and the longest Tower, linked Farm and reflecting Wall selection cases. All text and action hit targets remain separate and inside their panels. Include the dawn receipt case in visual checks. Do not create golden screenshot tests for cosmetic spacing.

## P3 — Check control discovery without restoring HUD clutter

**Hypothesis:** controls are listed in the menu, card badges expose building keys, and Pause/Resume is a visible button. The owner deliberately removed the misplaced floating controls sentence. Whether pan/zoom/cancel discovery still causes trouble requires fresh-player evidence.

**Follow-up:** if players cannot discover those controls, add a compact, dismissible Controls/help affordance usable during the run; retain the existing menu reference. Do not restore a permanent floating instruction paragraph or introduce a tutorial framework. Verify the help surface neither places buildings through the UI nor advances suspended combat unexpectedly. This is a conditional usability recommendation, not a missing-controls defect.

## Deferred validation

Rendered checks remain needed for camera/occlusion, water in motion, enemy and route/BREACH readability, new-side warnings, card/tooltip layout, pause, settings, menu/Continue, and actual save-file round trips. A's continuation used `CheckpointCodec` in memory, not the file store or menu. Do not record these paths as passed based on these runs.

Night 6 took 54.0–59.1 simulated seconds for the three economy victories and 67.9 seconds for the two defense victories. These are combat durations, not full-session times or evidence that the game feels slow. Gather human construction/combat timing before adding speed controls, stretching waves or targeting the full GDD's 25–40 minute session length for this MVP.

## Delivery order and scope

1. Correct effective stat information, reward-income wording/preview, lethal-shot event presentation and restart/exit communication.
2. Add concise consequence feedback/recap, establish consistent paused effects, and verify tooltip/selection layout; procedural movement is presentation polish after correctness.
3. Run the adaptive-defense comparison and make an evidence-backed balance decision.
4. Complete deferred rendered checks when desktop use is acceptable or another rendered environment is available. Use fresh-player evidence to decide the conditional help/lighting changes; keep the human acceptance gate open.

Keep the current six buildings, four enemies, six-night table, seeded maps, fixed-step simulation and save contract. No new units, upgrades, tutorial framework, dependencies, UI migration or performance capture is required by this document. Follow root `AGENTS.md`: inspect nearby tests, add only coverage tied to a concrete changed risk, and do not rerun builds/tests/performance merely for this documentation task.
