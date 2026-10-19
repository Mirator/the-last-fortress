# Plan 009 production contract — DRAFT, production gate NOT passed

Prepared 2026-10-16 under plan 009 §1. **This is a planning draft, not a frozen contract.** Plan 009's entry rule applies: plan 008's production slice is not validated (its G0 player, G3 blind, G4 art/audio/visible-FPS/participant gates are PENDING — see [008-results.md](008-results.md)), so only production planning is allowed. Nothing here authorizes procurement, contracting, hiring, spending, a schedule or implementation. Every owner, cost and capacity entry is an **owner decision** and is left open rather than estimated; no industry rates were invented.

## 1. Proposed launch scope (candidate, to be decided at the production gate)

Totals include existing content; bosses count within enemies; upgrade tiers and cosmetic variants do not count. "Have now" is the implemented 008 release profile.

| Area | Have now (008, technical) | 009 launch candidate | Ceiling / admission rule | Decision |
|---|---|---|---|---|
| Building kinds | 9 incl. Keep | 12 incl. Keep | 15 only with a distinct decision and passing throughput/counterplay review | OPEN |
| Enemy kinds | 7 incl. 2 bosses | 10 incl. 4 bosses | no ordinary enemy beyond 10 total | OPEN |
| Biomes | 1 (grassland) | 3 | a 4th only if it changes tactical conditions and fits the asset budget | OPEN |
| Map profiles | 3 | ≥ 2 distinct profiles per biome | palette swaps do not count | OPEN |
| Perks | 24 | 40 | ≤ 50 after relevance/combination review; no filler percentage duplicates | OPEN |
| Nights | 12 | 12 | no longer campaign without renewed pacing validation | OPEN |
| Difficulties | 1 (standard) | 3 named | a 4th only for a distinct validated audience; standard always kept | OPEN |
| Challenges | 0 | 12 authored | ≤ 20 when individually tested | OPEN |

Recommendation for the owner, with reasons: **do not choose the candidate yet.** The 008 slice has no human evidence that the twelve-night loop is fun, that the three approaches feel distinct, or what one finished asset costs. Choosing 12/10/3/40/12 now would commit roughly 1.5–2× the content of 008 on unmeasured throughput. The cheapest next evidence is the 008 G4 slice with real players.

## 2. Owners, effort, capacity and costs

| Role (plan 009 §6) | Owner | Capacity (FTE / months) | Cost basis | Status |
|---|---|---|---|---|
| Gameplay engineering / design | owner decision | owner decision | owner or actual quote | OPEN |
| Environment and character art | owner decision | owner decision | owner or actual quote | OPEN |
| Animation / VFX | owner decision | owner decision | owner or actual quote | OPEN |
| Audio (music, SFX, mix) | owner decision | owner decision | owner or actual quote | OPEN |
| QA / playtest coordination | owner decision | owner decision | owner or actual quote | OPEN |
| Production / localization coordination | owner decision | owner decision | owner or actual quote | OPEN |

- **Asset throughput: not measured.** 008 produced generated blockouts only ([bible §7](008-art-and-audio-bible.md)); no finished building, enemy or encounter has been taken to an approved final asset. Plan 009 must not schedule content batches until that measurement exists.
- **Engineering throughput (observed, agent-produced, technical only):** the 008 rules, six mechanics, twelve-night campaign, three map profiles, 24-perk pool, legacy-safe saves, validator, release build and tests were implemented and verified in one working session. This says little about art/audio-bound production and nothing about human iteration time.
- Provisional allowance from the plan (8–16 months after 008 with a small multidisciplinary team) is a scenario, not a funded schedule. Contingency proposal: 20–30% of capacity, to validate once capacity is known.

## 3. Dependencies (exact, in order)

1. Plan 007: listening pass, live motion review, five new players (all PENDING).
2. Plan 008 G0 player gate and G3 blind sessions: three distinct layout approaches observed across Ravine/Crossing/Meadow (PENDING).
3. Plan 008 G4: art/audio direction and budget decision; one finished building, enemy and encounter with measured time; listening approval; live 720p/1080p review; visible-window frame measurement on recorded hardware; five returning + five new participants (PENDING).
4. Owner decisions in §2 and the launch scope in §1.
5. Only then: freeze `009-content-contracts.md` items one by one, update the GDD to the chosen edition, and start P1 (pipeline sample).

## 4. Risks and mitigations

| Risk | Likelihood / impact | Mitigation |
|---|---|---|
| Converged Keep ring stays dominant ([008 tuning log](../specs/005-release-rules.md#8-tuning-log-provisional-values-changed-with-evidence)) | medium / high: variety collapses to one build | Decide with 008 G3 evidence; new content must counter it by decision (e.g. split-front boss, minimum-range weapon), not by raw numbers |
| Content multiplication before throughput is known | high / high | Gate in §3; cut optional breadth first |
| Status effects (slow), facing (shield), breach-cost changes are new rule systems | high / medium | Each is a separate contract with EditMode failure-mode tests, like 008's |
| Save compatibility across three profiles | medium / high | Keep schema 1 (MVP) and 2 (release) readers; one new rules version per frozen content change; fixtures per supported schema |
| Visible-window performance unmeasured | medium / high | 100-enemy cap stays; measure before raising counts |
| Final art/audio rights | unknown / blocker | Track provenance per asset (bible §5); unresolved rights block RC |
| Solo or part-time capacity | likely / high | Choose the smaller edition; the plan allows a smaller coherent release |

## 5. Cut order if effort exceeds capacity + contingency

1. Challenges beyond 6. 2. Fourth difficulty (already excluded). 3. Perks beyond 32. 4. Third biome. 5. Second new boss. 6. The specialist long-range structure. Never cut: the slice quality bar, save safety, accessibility basics, the standard difficulty.

## 6. Gate status

**Production gate: NOT PASSED.** Missing: every owner/cost/capacity entry, measured asset throughput, the 007/008 human gates, and the owner's launch-scope decision. No 009 content, pipeline, accessibility, release-architecture, alpha, beta or RC work may start under this draft.
