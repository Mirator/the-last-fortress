# Specifications

What the game is and should become. Specs never refer to implementation plans; plans refer to the spec they implement. Read the relevant specs before writing or executing any plan (see root `AGENTS.md`).

| Spec | Role | Status (2026-10-28) |
|---|---|---|
| [001-vision.md](001-vision.md) | Vision, pillars, core loop, direction, risks, success criteria. No rules. | Living |
| [002-game-rules.md](002-game-rules.md) | The complete current rules of the game (release profile with the hero, plus how the legacy profiles differ) | v1.11 (`release-rules-4`, 2026-10-28); implemented; the single source of current rules |
| [archive/](archive/README.md) | Earlier milestone specs, kept as history | Not current; do not plan from them |

How specs change:

- `002-game-rules.md` always describes the game as it is. A rule change that is already implemented is edited there directly.
- A new feature gets its own numbered feature spec that states what it changes in the game rules. Once implemented, its rules are merged into `002-game-rules.md` and the feature spec moves to `archive/`.
- When a spec's status changes, update its status line and this table in the same change.
