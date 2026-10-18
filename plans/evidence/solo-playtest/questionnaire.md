# Solo playtest — owner questionnaire

Build: `game/The Last Fortress/Builds/Release/` (copy the whole folder). Version/build id (bottom of title menu): ________  Date: ________

## What a solo test can and cannot tell you

You know the rules, the code and the intent, so you **cannot** judge whether the game is understandable to a newcomer (onboarding, HUD clarity, "how do enemies route?"). Leave those to the 007 protocol with fresh players.

You **can** judge: whether the decisions are interesting, pacing and boredom, whether one strategy dominates, bugs, audio, live motion and frame rate. That is what this sheet covers.

Rules for yourself:
- Fill each run's section **right after** the run (or during Day phases). Memory rewrites itself within an hour.
- Write what happened, not what you plan to fix. Fix ideas go in the last section.
- Use a fresh save folder per session so nothing leaks between runs: `TheLastFortress.exe -saves C:\temp\lf-solo-1`
- Sound on, normal speed, default zoom. Turn on a frame-rate overlay (Windows: `Win+G` → Performance).
- Note the seed of every run so any bug or odd result can be replayed.

## Session plan (4 runs, roughly 3–4 hours total; split across days if needed)

| Run | Map | How to play | What it answers |
|---|---|---|---|
| 1 | Ravine | However you naturally want to | Baseline fun, pacing, run length |
| 2 | Crossing | **Forbidden:** Towers within 3 cells of the Keep. Defend at the routes | Is a non-ring strategy viable and fun? |
| 3 | Meadow | Pure Keep ring, minimal thought | Is the ring dominant / boring? |
| 4 | Any | Bug hunt (checklist below), no need to win | Stability, saves, edge cases |

---

## Per-run sheet (copy once per run)

**Run #:** ___  **Map:** ________  **Seed:** ________  **Start time:** ____  **End time:** ____
**Outcome:** Victory / Defeat on Night ___  **Total wall-clock minutes:** ___

### Night log

Fill one row per Night. *Day min* = time spent building; *Idle?* = any stretch over ~20 s where you had nothing to decide or watch.

| Night | Day min | Night min | Keep HP after | Idle? (where) | Tension 1–5 | Notes (surprises, close calls, confusion) |
|---|---|---|---|---|---|---|
| 1 | | | | | | |
| 2 | | | | | | |
| 3 | | | | | | |
| 4 | | | | | | |
| 5 | | | | | | |
| 6 | | | | | | |
| 7 | | | | | | |
| 8 | | | | | | |
| 9 | | | | | | |
| 10 | | | | | | |
| 11 | | | | | | |
| 12 | | | | | | |

### Ratings (1 = strongly disagree, 5 = strongly agree)

| # | Statement | 1–5 | Why (one line) |
|---|---|---|---|
| R1 | Most Days, I had a real choice between at least two good options | | |
| R2 | When I lost HP or buildings, I could tell exactly which placement caused it | | |
| R3 | Nights were fun to **watch**, not just wait through | | |
| R4 | New enemies/bosses forced me to change my defense | | |
| R5 | At least one perk changed how I played (not just a stat bump) | | |
| R6 | The map shape mattered to my plan | | |
| R7 | Difficulty curve felt right (no flat stretch, no sudden wall) | | |
| R8 | The run length felt right for the amount of decisions in it | | |
| R9 | I wanted to start another run when it ended | | |

### Open questions

1. The single most interesting decision I made this run: 
2. The most boring stretch (which Night/Day, and why): 
3. Something I did that felt like an exploit or "too easy": 
4. Something that felt unfair or that I could not have reacted to: 
5. Building/enemy/perk I never used or never noticed — and why: 
6. Moment I was confused (even knowing the rules): 
7. If I could change one thing before the next run: 

### Economy check

- Gold felt: too tight / about right / too loose — at Nights: ________
- Buildings I bought most: ________  never bought: ________
- Did I ever have gold with nothing worth buying? When? ________

---

## Strategy comparison (after runs 2 and 3)

| Question | Answer |
|---|---|
| Did the route-defense run (2) feel **harder**, **easier** or **the same** as the ring (3)? | |
| Which was more **fun**? Why? | |
| In run 3, did I ever have to think hard? On which Night? | |
| Would a new player find the ring on their own within one run? | |
| Verdict: ring is **fine** / **dominant but fun** / **dominant and boring** | |

---

## Audio pass (do once, ~15 min, during any run)

| Check | OK? | Notes |
|---|---|---|
| Keep taking damage is audible over everything else on a dense Night (8+) | | |
| No clipping/distortion when many enemies die at once | | |
| No flood of the same sample (e.g. 4 identical arrow hits in a row sounding robotic) | | |
| Music doesn't drown out threat sounds | | |
| Music/ambience loops without an audible seam | | |
| Day→Night and Night→Day transitions sound intentional | | |
| Pause with an arrow in flight: impact plays only after resume | | |
| Open/close the menu 5× quickly: music does not stack | | |
| Master volume 0 = true silence; volumes survive a restart | | |
| Overall: would I keep the sound on? (1–5) | | |

## Visual / motion pass (live, default zoom)

| Check | OK? | Notes |
|---|---|---|
| Enemy walking looks natural at 1× (no sliding, jitter, popping) | | |
| Can I tell enemy types apart at a glance at default zoom? | | |
| Projectiles visibly connect with targets | | |
| Damage/destruction is readable (smoke, HP bars, deaths) | | |
| Day/night lighting change looks good, not jarring | | |
| HUD never covers something I needed to see | | |
| Lowest FPS seen: ____ on Night ____ (map ____) with roughly ____ enemies | | |
| Any hitch/stutter? When? | | |

## Bug hunt checklist (run 4)

| Action | Expected | Result |
|---|---|---|
| Save and Quit on a Day, then Continue | Exact same Day state | |
| Quit the game (Alt+F4) mid-Night, then Continue | Night replays from its Day checkpoint | |
| Quit during perk choice, then Continue | Night replays from its Day checkpoint; same perk offer after it (no free reroll) | |
| Restart mid-run | Confirm dialog, exact Day 1 of same seed | |
| Sell a building, check refund | floor(price / 2) | |
| Try to fully block all routes with Walls | Enemies break through Walls (open-route-first breaching), never get stuck | |
| Rotate/place 2×2 buildings at map edges and near water | Only valid cells accepted | |
| Spam-click Start Night / perk cards | No double start, no double perk | |
| Press F9 during a normal run | (Known issue: drops the run into stress mode) | |
| Alt-Tab out mid-Night and back | Game state intact, audio OK | |
| Change resolution / window mode (if Settings offers it) | HUD stays usable | |
| Any error popup, freeze, or crash | — | |

Bug log (one line each: seed, Night, what you did, what happened, can you repeat it?):

1. 
2. 
3. 

---

## Final summary (after all runs)

1. In one sentence, is the core loop fun **right now**? 
2. Top 3 problems, ranked by how much they hurt the fun: 
   1. 
   2. 
   3. 
3. Top 3 things that already work and must not be broken: 
   1. 
   2. 
   3. 
4. Is 12 nights the right length, or would ~8 be tighter? 
5. Biggest open question that only a **new** player can answer: 
6. Next step: tune balance / fix bugs / cut content / get fresh players / start art production 
