# Art provenance (plan 011)

Everything in `Art/` was authored as code by the agent (Claude) for this project on 2026-10-19. No purchased, downloaded or third-party meshes, textures, fonts or plug-ins were used. **These are generated assets pending artist review, not approved final art.**

| Files | Source | Tool | Notes |
|---|---|---|---|
| `Textures/*.png` (stone, wood, shingle, grass, soil, cloth, water normal) | `Source~/textures.py` | Python 3.13, numpy 2.4, Pillow 12.1 | Tileable fBm noise, fixed seeds, deterministic |
| `Models/ArtKeep, ArtWall*, ArtArcherTower, ArtGrunt` (the visual slice) | `Source~/models.py` | Blender 5.2.1, no add-ons | Flat FBX export, world pivots; joints rebuilt by `Editor/ArtSetup.cs` |
| `Models/ArtPine, ArtBroadleaf, ArtBush, ArtRock*, ArtReeds, ArtTuft, ArtFordStone` | `Source~/environment.py` | Blender 5.2.1 | Imports the helpers from `models.py` |
| `Models/ArtHouse, ArtFarm, ArtSpikeTrap, ArtCannon, ArtWatchtower, ArtRepairStation` and the six other enemies | `Source~/models_ext.py` | Blender 5.2.1 | Enemies share the Grunt's part rig |
| `Materials/*.mat`, `Post/*.asset`, `ArtDirection.asset`, `Prefabs/*.prefab` | `Editor/ArtSetup.cs` | Unity 6000.3.16f1 | Materials, profiles and ArtDirection are created once and then owned by whoever tunes them; prefabs are rebuilt from the models |

Regenerate the models with `blender -b --factory-startup -P <script> -- <Art/Models folder>`, the textures with `python textures.py`, then **The Last Fortress > Art > Import and Assign Art Slice**.

## Plan 013 additions (2026-10-19)

Same provenance: authored as code by the agent (Claude); nothing purchased, downloaded or third-party. Generated assets pending artist review.

| Files | Source | Tool | Notes |
|---|---|---|---|
| `Models/ArtHero, ArtGrunt, ArtRunner, ArtBrute, ArtRaiderArcher, ArtBomber, ArtSiegeCommander, ArtSiegeGolem` | `Source~/characters.py` (replaces the figures in `models.py`, `models_ext.py` and `models_hero.py`; the last is now a wrapper) | Blender 5.2.1 | One articulated rig with knees (`ShinL/ShinR`), hero shield on `OffHand` |
| `Models/ArtPine, ArtBroadleaf, ArtRockLarge, ArtRockSmall`, far islands | `Source~/environment.py` | Blender 5.2.1 | Layered pine skirts, fuller crowns, flat-bottomed boulders |
| `Models/ArtRepairStation` | `Source~/models_ext.py` | Blender 5.2.1 | Crane with hoisted stone and windlass |
| `Textures/grass_detail*.png, sand_albedo.png, rock_albedo.png, rock_normal.png` | `Source~/textures.py` | Python 3.13, numpy, Pillow | `*_detail.png` is imported linear (x2 multiply map) |
| `Shaders/Ground.shader, Water.shader, Grass.shader` | hand-written HLSL | URP 17.3 | Island ground blend, sea/river water, instanced hero-view grass |
| `Materials/ArtTerrainGround, ArtRiverWater, ArtGrass` | `Editor/ArtSetup.cs` | Unity | Created once when missing, then owned by whoever tunes them |
| Island, sky, clouds, stars, grass meshes | `Runtime/Unity/WorldView.Island.cs`, `WorldView.Grass.cs`, `HeroSky.cs` | runtime | Built per map from the played layout; no assets |
| Heading font | Windows-installed Palatino Linotype / Book Antiqua / Georgia via `Font.CreateDynamicFontFromOSFont` | — | Not shipped; falls back to the bundled LegacyRuntime font |
