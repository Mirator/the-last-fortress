# Feature spec 012: night pacing — call early for gold, earlier bosses

Version 0.3, 2026-10-28. Status: **IMPLEMENTED and merged into `../002-game-rules.md` v1.14 (2026-10-28); archived.** Not current: plan from `002-game-rules.md`. The owner's live check is pending. Approved by the owner on 2026-10-28:
- D1: early call;
- D2: shorter boss tails;
- D3: a faster cadence;
- D4: 1 gold per second saved, paid at dawn.

Changes `002-game-rules.md` v1.13, sections 3, 6, 9 and 13, for `release-rules-4` only. Constraint from the owner: the game must not become much more difficult.

## 1. Problem

Measured on 2026-10-28: 15 scripted `release-rules-4` runs (kill-zone, corridor and economy; seeds 1–5; active hero).

- **Early nights open with dead time.** On Night 1 the first shot comes after 26 s on average (up to 51 s), and 53% of the night is quiet, meaning no shot, hit or kill in the previous 2 s. On Nights 2–5 that is 21–29%.
- **Late nights are busy.** On Nights 8–12, 6–9% is quiet.
- **Boss nights end with a wait.** They are the longest of their stretch (Night 6 averages 94 s, Night 5 67 s), because the boss spawns at 30 s and walks at 0.15 tiles/s.
- **Little is at stake.** Kill-zone and economy layouts take no Keep damage on almost every night. Corridor loses 2 of 5 seeds. Players have no way to trade safety for reward during a night.

## 2. Decisions (owner, 2026-10-28)

- **D1: Call early for gold.** During a night, the player may call the rest of tonight's enemies early, for bonus gold. It is opt-in, never forced.
- **D2: Shorter boss tails.** The boss spawns at 15 s instead of 30 s. This was accepted as slightly harder on boss nights.
- **D3: Faster cadence.** A call makes every not-yet-due spawn come 4× sooner, counted from the moment of the call, with the boss included. They do not all arrive at once.
- **D4: The bonus.** 1 gold per whole second by which the night's last spawn moved earlier, rounded down. It is paid at the next dawn as its own income line, and lost if the Keep falls.

Rejected for now: automatically skipping quiet stretches; threat feedback (both may return later).

## 3. Rules (`release-rules-4`; legacy profiles unchanged)

1. **When it is available.** The early call can be made once per night, while at least one spawn of tonight is still pending. That is from the start of Night until the last spawn has been released. It works paused or unpaused and from either view. The HUD button and the C key make the call.
2. **The effect.** A spawn originally due at tick `t`, at or after the call tick `c`, is now due at `c + ceil((t − c) / 4)`. Spawns already due are unchanged. Order, entries and kinds are unchanged, and the living cap still applies.
3. **The bonus.** `floor((t_last − t'_last) × 0.05)` gold [P], where `t_last` is the original tick of the night's last spawn and `t'_last` is its compressed tick. It shows on the button before the call ("Call early +N").
   - It is recorded in the night recap and paid at the next dawn as a separate income line, "Early call", after the perk choice, with the rest of the income.
   - It is not paid after the final night or a defeat.
   - It is never part of the income forecast.
4. **Boss time.** A boss night's boss spawns at 15 s [P] (was 30 s), after the ordinary spawns due at the same time, as before.
5. **Unchanged.**
   - Nights are not checkpointed, so a reload replays the night from its Day checkpoint, and the player may call again.
   - Stored plans in existing saves keep their boss times.
   - Night speed rules are unchanged (speed never changes outcomes).

## 4. Acceptance

- **The rule itself.** An EditMode test calls early at a tick chosen so that rounding matters, and checks:
  - the compressed spawn ticks;
  - the bonus value, paid once at the next dawn as its own line;
  - that no call is possible after the last spawn, or a second time;
  - that legacy rules refuse the call.
- **Difficulty.** The recorded policy runs are re-run with the boss at 15 s and no calls. Recorded wins must hold. Keep damage and night lengths are compared with the 2026-10-28 baseline. "Not much more difficult" means no recorded win becomes a loss.
- **Presentation.** The HUD shows the button with its value, the C key works, and the recap and dawn income show the bonus. This is checked in a capture and by the owner.

## 5. As built (2026-10-28)

- **Boss time.**
  - With the boss at 15 s, 1 of the 15 probe runs (active hero, seeds 1-5 × 3 strategies) turned from a win into a loss: corridor seed 1, now lost on Night 9. That makes 3 losses instead of 2.
  - Economy seed 5 now takes 70 Keep damage.
  - A 20 s boss was worse (4 losses), so outcomes are sensitive to timing, not monotonic.
  - The owner kept 15 s (2026-10-28). The recorded-win test still passes.
- **The call while paused.** It is refused only while the menu, a confirmation or focus loss suspends the run. A paused night may be called.
