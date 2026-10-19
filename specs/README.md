# Specifications

What the game should do. Plans in `../plans/` describe how to build it and point back to a spec. Read the relevant specs before writing or executing any plan (see root `AGENTS.md`).

| Spec | Scope | Status (2026-10-19) |
|---|---|---|
| [001](001-game-design.md) | Game design document: vision, pillars, full-release direction | Living vision document; milestone specs and `plans/008-rules.md` take precedence for concrete rules |
| [002](002-navigation-prototype.md) | One-wave navigation prototype | Implemented (plan 001) |
| [003](003-six-night-mvp.md) | Six-night MVP, profile `mvp-rules-1` | Implemented (plans 003–005); profile frozen |
| [004](004-agent-playtest-improvements.md) | Playtest-driven improvements to the MVP | Implemented (plan 006); human checks pending |
| [005](005-third-person-hero.md) | Player hero, third-person view, switchable views, `release-rules-2` | **Current**; owner decisions pending; next plan 012 |

Release profile rules (`release-rules-1`, twelve nights) were frozen as [plans/008-rules.md](../plans/008-rules.md) rather than as a spec. Treat that file as the current rules baseline until a spec replaces it.

When a spec is implemented or superseded, update its status line and this table in the same change.
