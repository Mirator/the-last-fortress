"""
Plan 012 hero, superseded in plan 013: the hero is now built by characters.py with every other figure (same rig, knees
added). This entry point is kept so existing instructions still produce ArtHero.fbx:

    blender -b --factory-startup -P models_hero.py -- <output folder>
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import characters  # noqa: E402

if __name__ == "__main__":
    characters.figure("Hero", **characters.HERO)
