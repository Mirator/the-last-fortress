# Art provenance (plan 011)

Everything in `Art/` was authored as code by the agent (Claude) for this project on 2026-10-19. No purchased, downloaded or third-party meshes, textures, fonts or plug-ins were used. **These are generated assets pending artist review, not approved final art.**

| Files | Source | Tool | Notes |
|---|---|---|---|
| `Textures/*.png` (stone, wood, shingle, grass, soil, cloth, water normal) | `Source~/textures.py` | Python 3.13, numpy 2.4, Pillow 12.1 | Tileable fBm noise, fixed seeds, deterministic |
| `Models/ArtKeep, ArtWall*, ArtArcherTower, ArtGrunt` (the visual slice) | `Source~/models.py` | Blender 5.2.1, no add-ons | Flat FBX export, world pivots; joints rebuilt by `Editor/ArtSetup.cs` |
| `Models/ArtPine, ArtBroadleaf, ArtBush, ArtRock*, ArtReeds, ArtTuft, ArtFordStone, ArtFarIsland*` | `Source~/environment.py` | Blender 5.2.1 | Imports the helpers from `models.py` |
| `Models/ArtHouse, ArtFarm, ArtSpikeTrap, ArtCannon, ArtWatchtower, ArtRepairStation, ArtGate` and the six other enemies | `Source~/models_ext.py` | Blender 5.2.1 | Enemies share the Grunt's part rig |
| `Materials/*.mat`, `Post/*.asset`, `ArtDirection.asset`, `Prefabs/*.prefab` | `Editor/ArtSetup.cs` | Unity 6000.3.16f1 | Materials, profiles and ArtDirection are created once and then owned by whoever tunes them; prefabs are rebuilt from the models |

Regenerate the models with `blender -b --factory-startup -P <script> -- <Art/Models folder>`, the textures with `python textures.py`, then **The Last Fortress > Art > Import and Assign Art Slice**.
