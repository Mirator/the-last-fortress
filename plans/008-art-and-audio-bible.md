# Plan 008 art and audio bible

The standard every 008/009 asset is held to. It describes the current generated family (blockouts and synthesized clips) and the rules a final asset must keep so it drops into the same code paths. **Nothing here is approved final art or audio**: there has been no listening pass, live review or art-director sign-off (G4 PENDING). Reference captures below are candidates for that review, not approvals.

## 1. Camera, scale and readability

- Fixed isometric orthographic camera (`CameraRig.Pitch/Yaw`), default size shows the whole 20×20 board at 1280×720 with the HUD. Every gameplay object must read **at default zoom on the 720p frame**; detail that only shows when zoomed is decoration.
- One tile = one world unit. Building pivots: tile centre on the ground for 1×1, footprint centre (shared corner) for 2×2. Bases stay inside 0.9 × 0.9 per tile so neighbours never touch.
- Height bands (keep the ladder; it is how a player tells roles apart): floor traps ≤ 0.15 · Walls 0.6–0.8 · Farm 0.35 · Houses ~1.1 · Cannon ~1.1 (wide, low) · Repair Station ~1.4 · Keep 1.6 · Archer Tower 2.0 · Watchtower 2.5 (thin). Enemies: Grunt/Runner/Archer/Bomber 0.55–0.65 · Brute 0.85 · bosses 1.5–2.0 (Commander's banner tallest).
- Silhouette first, colour second: every kind must be identifiable in greyscale. Current tells: Cannon = squat square platform + barrel; Watchtower = stilts + pennant; Repair Station = workshop + crane; Raider Archer = vertical bow stave; Bomber = round keg on the back; Commander = banner pole + cape. Enemy roles also differ in gait (stride/bob/sway table in `WorldView.AnimateModel`).

## 2. Palette and materials

- Palette is code (`Palette.cs`): grass `#87935D`, timber `#996D43`/shadow `#634830`, limestone `#D8CCAE`, enemy body `#303538`, enemy cloth coral-brown `#AE5747`, amber `#E9B45F` (player fire, torches), route cyan `#79C7C7` (player information), warning coral `#E67961` (enemy threat, damage, invalid).
- Colour semantics are fixed: **cyan = your plan/information, amber = your fire/economy highlights, coral = threat**. New enemy cloth stays in desaturated mid-tones (olive `#6E7F3A` archer, slate-violet `#3E3A52` bomber, violet `#6B3F8C` commander) so coral remains reserved for warnings.
- Materials: URP Lit, flat colour, no textures in the blockout set; emission only for torches, eyes, fuse and glow. Final art may add textures but must keep the flat-shaded value structure and must not rely on colour alone (shape + motion for every signal).

## 3. Animation and presentation events

All presentation is driven by the single Core event batch (`PrototypeBootstrap.SyncViews`), on the presentation clock (frozen while paused or in the menu). Final animation rigs must hook the same moments:

| Moment | Event | Current presentation | Final asset must provide |
|---|---|---|---|
| Archer Tower / Cannon fires | `TowerShot` | arrow or shell flight 0.18 s, muzzle flash, recoil | firing pose; the hit lands when the projectile lands |
| Cannon splash | `TowerShot` + `SplashHit` | amber ground ring of the real splash radius | impact VFX sized from Core radius (never fixed art size) |
| Raider Archer shot | `RangedShot` | coral arrow from archer to building | draw/release; stays stopped on its cell |
| Fuse lit | `FuseLit` + `Enemy.FuseLit` | flickering amber spark on the keg | fuse visible for the whole 1.5 s |
| Bomber blast | `BomberDetonated` | 1.5-tile ring + debris | blast covering the Chebyshev-1 square it damages |
| Commander aura | Commander alive | faint coral disc of the real radius | aura readable at default zoom, never hiding enemies |
| Brute/boss strike | `AttackDueTick` | 0.35 s lean-back wind-up | same timing; no animation may delay damage |
| Dawn repair | `BuildingRepaired` / `DawnRepair` | rising cyan sparks | brief, bounded (≤16 at once) |
| Destruction | `BuildingDestroyed` | debris cue | collapse; occupancy is already released |

Caps (pooled, oldest recycled): hit 40, kill 30, collapse 12, fire 24, blast 12, repair 16 cues; arrows/shells pooled. New VFX must declare a cap.

## 4. Audio

- 24 one-shot voices, at most 4 of one sound, priority displacement (Keep hit > phase > destruction > … > shots), stingers restart rather than stack, two music tracks only during a 1.8 s crossfade (`AudioDirector`). New sounds must fit this budget, not raise it.
- 008 events have their own synthesized clips (`synth.py`, own seeds; the 007 clips are byte-identical): Cannon = `cannon_shot` (muzzle crack, deep thump, iron ring, smoke tail; priority 60 like a heavy strike), Raider Archer = `raider_shot` (lower, slacker bow with string rattle; shot priority and pitch spread), fuse = `fuse_lit` (struck spark, hiss, crackle; priority 65, above towers and strikes so the warning survives a full mix), Bomber blast = `bomber_blast` (detonation crack, boom, gravel rain; destruction priority 70), Commander arrival = `commander_arrival` (rising war-horn fifth over a quickening drum roll; stinger priority 95, one at a time, distinct from the Golem's slow low tread). Peaks −0.5 to −4 dBFS, no clipped samples. **Nobody has listened to them**: whether each threat is recognisable by ear is the open G4 listening check.
- Mix intent: threat cues (fuse, boss arrival, Keep hit) must be audible over combat beds; economy/dawn cues soft. Every gameplay sound has a non-audio equivalent (the visual table above, HUD text, banners).

## 5. Naming, import and ownership

- Prefabs: `Prefabs/<Kind>.prefab` for MVP kinds, `Prefabs/Release<Kind>.prefab` for 008 kinds. Icons: `UI/Icons/icon_<kind>.png`, 128 px white line art on transparent, 3.5/64 stroke. Clips: `Audio/Clips/<name>.wav` (mono, PCM for one-shots, Vorbis streaming for loops).
- **Ownership:** everything currently shipped is *generated*: blockouts by `EntityPrefabBuilder`, icons by `UI/Source~/icons.py`, clips by `Audio/Source~/synth.py`. `ReleaseSetup` regenerates a `Release*` prefab only while the content asset still points at the generated file; assigning an authored prefab in `ReleaseContent.asset` stops regeneration for that kind. `PresentationSetup` rewrites import settings and references only, never the source files. Before final art lands, extend this rule to icons and clips (an authored file must not be overwritten by a generator).
- LOD: not needed for the blockouts; a final asset over ~2k triangles must ship LOD1 at ≤50%.

## 6. Reference captures (candidates, not approved)

`evidence/008/rendered/` at 1280×720 and 1920×1080, offscreen (hidden window): title with map selector; Day 1 on Ravine/Crossing/Meadow with the two-row build cards; Cannon range, Watchtower boost and Repair reach previews; Cannon splash, Raider Archer, Bomber fuse and blast, Commander aura with boss bar and banner; release reward cards; final-siege announcement. These were produced and checked by the agent only.

## 7. Production throughput (for plan 009)

Measured this session, agent-generated blockouts only: three buildings, three enemies, three icons and their full integration (prefab code, setup, views, overlay, HUD text, audio mapping, validation, PlayMode coverage) in one working session. **This is not final-art throughput** and must not be used to schedule 009: no finished AA asset was produced, iterated or approved. The first real measurement is the time to take one building, one enemy and one encounter from this blockout to an approved final asset (G4).
