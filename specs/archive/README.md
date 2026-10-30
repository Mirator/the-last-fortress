# Archived specifications

Historical milestone specs, kept unchanged for reference. They are **not** current: the game is described in [../002-game-rules.md](../002-game-rules.md) and its direction in [../001-vision.md](../001-vision.md). Do not plan from these files.

| File | Was |
|---|---|
| 001-game-design.md | Original game design document (GDD v1.1) |
| 002-navigation-prototype.md | One-wave navigation prototype; still the rules of the prototype test scene |
| 003-six-night-mvp.md | Six-night MVP (`mvp-rules-1`) |
| 004-agent-playtest-improvements.md | Playtest-driven improvements to the MVP |
| 005-release-rules.md | Frozen `release-rules-1` contract, before consolidation into 002-game-rules |
| 006-third-person-hero.md | Hero and switchable views feature spec with the owner's decisions; implemented and merged into 002-game-rules section 17 |
| 007-third-person-production-slice.md | `release-rules-4` production-slice hero (motor, contact melee, camera safety, animation, sound) v0.1; implemented and merged into 002-game-rules section 18; owner quality review pending |
| 008-bounded-chase-limits.md | One hero chase per enemy per night, then fighting back in place; enemy hit reach 0.225; attack timing kept on a switch (v0.3); implemented and merged into 002-game-rules section 18 (v1.8); owner live check pending |
| 009-strategic-figure-readability.md | Strategic class markers under enemies, enemy HP bars that keep their size when zoomed out, default zoom 4.5, zoom-out capped at 10.5 (v0.3); implemented and merged into 002-game-rules section 13 (v1.9); owner review of the captures pending |
| 010-night-start-framing.md | At Night start the strategic camera eases out to frame the Keep, tonight's entries and their road mouths (v0.3); implemented and merged into 002-game-rules section 13 (v1.11); owner live check pending |
| 011-hero-view-occluder-fade.md | In the hero view, meshes that hide the hero turn see-through and Walls no longer pull the camera in (v0.3); implemented and merged into 002-game-rules section 18 (v1.13); owner live check pending |
| 012-night-pacing.md | Early call for gold (once a night, remaining spawns 4x sooner, 1 gold per second saved at dawn) and release-rules-4 bosses at 15 s (v0.3); implemented and merged into 002-game-rules sections 3, 6, 9, 13 and 16 (v1.14); owner live check pending |
| 013-buildings-let-the-camera-through.md | No building pulls the hero camera in; buildings fade instead (v0.3); implemented and merged into 002-game-rules section 18 (v1.15); owner live check pending |
| 014-unit-collision.md | Enemy bodies, lanes and queues; hero blocked and pushed by enemies (v0.3); implemented and merged into 002-game-rules sections 2, 8, 16, 17 and 18 (v1.16); owner live check pending |
| 015-building-upgrades.md | Upgrade tiers II and III for every building but the Keep: a stat raise and one trait each, a slow status, sale from the total paid, Upgrade (U) and tier pips (v0.2); implemented and merged into 002-game-rules sections 6, 7, 10, 12, 13 and 16 (v1.18); owner live check pending |
| 016-break-the-turtle.md | Raids on economy buildings beside a route, gold veins and the Mine, per-night spawn limit 150 (v0.3); implemented and merged into 002-game-rules sections 2, 6-13 and 16 (v1.21); its pressure gate was not reached and is superseded by spec 017 D10; owner live check pending |
| 017-land-economy-and-maps.md | House and Farm income from free fertile land, four map recipes with a Keep and 3-5 entries per map, the income preview, wave tuning (v0.5); implemented and merged into 002-game-rules sections 2, 6-13 and 16 (v1.22); its balance gate was loosened by the owner (a Wall-sealed Keep still wins); owner live check pending |
