# Audio provenance and licence

All 33 clips in `Clips/` are original project material. `Source~/synth.py` generated them on 2026-10-16 using only numpy (28 for plan 007, five threat clips for plan 008). It synthesizes them with oscillators, filtered noise, Karplus-Strong strings and damped modal partials. No recordings, sample libraries, downloaded sounds or generative-AI audio were used. Nothing was bought. The clips have the same licence as the rest of the game.

Regenerate (overwrites `Clips/`; Unity re-imports, then run **The Last Fortress > Link Audio and Icons**):

```
python "Assets/TheLastFortress/Audio/Source~/synth.py" "Assets/TheLastFortress/Audio/Clips"
```

The script uses fixed seeds and a fixed generation order. The same numpy version gives the same clips. The 007 clips share one random stream, so regenerating only a subset of them changes the rest: regenerate everything. The five 008 clips each draw from their own seed (`seeded`), so they can be regenerated alone (`synth.py <dir> cannon_shot raider_shot fuse_lit bomber_blast commander_arrival`) and adding them left every 007 clip byte-identical (SHA-256 checked against a full regeneration).

| Role | Clips |
|---|---|
| Interface and construction | `ui_click`, `ui_confirm`, `ui_reject`, `build`, `sell`, `repair`, `reward` |
| Combat | `shot_0..2`, `impact_0..2`, `trap`, `heavy_strike`, `destroy`, `keep_hit` |
| Phases | `night_start`, `dawn`, `golem`, `victory`, `defeat` |
| Release threats and towers (plan 008) | `cannon_shot` (Cannon), `raider_shot` (Raider Archer), `fuse_lit` (Bomber fuse), `bomber_blast` (Bomber detonation), `commander_arrival` (Siege Commander stinger) |
| Environment (32 s loops) | `ambience_day` (wind, distant birds), `ambience_night` (low wind, crickets) |
| Music (loops) | `music_planning` (84 bpm, D dorian lute and pad), `music_combat` (108 bpm drums and ostinato), `music_finale` (126 bpm), `music_conclusion` (72 bpm pad) |

Status: these clips are technically integrated and measured (peak, RMS, DC offset, loop seams; see `plans/007-results.md`; the 008 clips: peak and clipped-sample count, see `plans/008-art-and-audio-bible.md` section 4). **Nobody has listened to them yet.** Mix quality, character and fatigue are PENDING a listening check. They are intended as final clips, not placeholder beeps, but that has to be confirmed by ear.
