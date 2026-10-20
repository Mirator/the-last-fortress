# The Last Fortress — vision

Version 2.1, 2026-10-20. Single-player strategy / roguelite / base defense for PC (Steam, Windows), premium. Unity 6.3 LTS (`6000.3.16f1`), URP, project in `game/The Last Fortress/`.

This document says what the game is for and what it must protect. It contains no rules or numbers: the current game is described in [002-game-rules.md](002-game-rules.md), and planned changes in their own feature specs (see [README.md](README.md)). Where this document and a rules spec disagree on a concrete rule, the rules spec wins.

## Core concept

Build a fortress during the day, defend it at night, and survive until the final invasion is defeated. Unlike a traditional tower defense, every structure changes the battlefield: Walls redirect enemies, buildings create chokepoints, and where the economy stands decides how the defense works.

*Build your fortress. Shape the battlefield. Survive the night.*

## Design pillars

1. **Every building is a tactical decision.** Placement matters as much as what a building does.
2. **Simple controls, deep consequences.** The basics are learned in two minutes; new strategies keep appearing after hours.
3. **Readable battles.** Many enemies stay understandable through clear silhouettes and restrained effects.
4. **Meaningful risk and reward.** Investing in economy makes later nights easier but leaves the fortress weaker now.
5. **Short, replayable runs.** Seeded maps, wave variation, perks and building synergies make each run different.

The central design question: **can a player build a clever layout, watch enemies interact with it, and feel that their planning, not random upgrades or raw statistics, won the battle?**

## Core loop

- **Day:** untimed. Receive income, build, sell, repair, rotate, inspect the next attack and its routes.
- **Night:** start the wave manually. Buildings fight automatically; enemies try to reach and destroy the Keep. No construction during combat, even while paused.
- **Reward:** after each non-final night, choose one of three perks.

The loop repeats until the final night is cleared (victory) or the Keep falls (defeat). A run ends with a summary.

## The central mechanic: spatial consequences

Buildings do not just occupy space; they change enemy navigation, affect neighbouring structures and create new tactical situations. Enemies always prefer any open route to the Keep and break through only when sealed in, so the player shapes routes deliberately. Routes are previewed with the same rules enemies use, so the outcome is predictable from the layout.

Economic and defensive openings should both be viable, depending on the map and the coming threats. Different maps must change which strategies work; random terrain alone is not variation.

## Experience goals

- A full run of 25-40 minutes; Day has no countdown, so the player plans at their own pace.
- Rewards encourage builds: no perk is required to win, some perks enable distinctive strategies rather than bigger numbers, and offers favour perks relevant to what the player owns.
- Information that decides play (routes, tower ranges, synergies, the next attack) is understandable without reading detailed tooltips. Building statistics appear on selection or hover.
- Mouse and keyboard first. Threats always have visual cues; audio supports but is never required.

## Visual and audio direction

Minimalist, colourful, readable low-poly 3D; the world beyond the board is lost in mist, and enemies come out of the mist along four roads. Day is warm and inviting; night is dark with strong contrast and warm torchlight. Buildings are identifiable by shape alone, enemy classes are distinguishable at low zoom, and effects emphasise meaningful attacks rather than filling the screen. Simple rigs or procedural motion. Mood references are in `design/README.md`.

Music moves between calm day themes and tense night combat. Sound effects make combat legible (arrows, explosions, wall damage, destruction, placement, incoming attack warnings), and a large approaching threat is audible without watching every edge.

## Direction beyond the current game

Candidates, none committed until a feature spec exists:

- A player-controlled hero with a switchable third-person view: implemented (game rules section 17); hero progression is a later candidate.
- Soldiers and Barracks with simple orders (Hold, Move, Attack Target).
- More biomes and maps, a larger board, flying enemies, more bosses, building upgrade tiers, difficulty modes, challenges and daily seeds.
- Meta-progression that unlocks strategic options, not large unconditional stat increases; a skilled player wins with the starting set.

Out of scope for version 1.0: multiplayer (co-op or competitive) and story campaigns.

## Audience and risks

Players who enjoy accessible strategy, tower defense, roguelite progression and compact city builders in replayable sessions.

- **Genre saturation:** a medieval defense game has many competitors; the battlefield manipulation must be obvious in trailers and screenshots.
- **Limited depth:** if Wall-and-Tower placement becomes formulaic, replayability suffers.
- **Over-scoping:** adding buildings, enemies or systems before the core loop is validated extends development without making it better.
- **Weak variation:** maps that do not change viable strategies add little.

## What success looks like

Before investing in more content, playtests should show that players:

1. understand how buildings change enemy paths;
2. face meaningful decisions in placement and building choice;
3. can beat the same map with more than one strategy;
4. feel responsible for their own victories and defeats;
5. keep enjoying repeated runs and restart voluntarily after losing.
