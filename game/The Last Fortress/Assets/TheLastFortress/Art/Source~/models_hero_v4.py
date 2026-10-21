"""
Plan 019: the release-rules-4 hero (specs/003 section 7), the plan 013 hero with articulated elbows, wrists and ankles,
human arm length and a slightly longer sword, exported as ArtHeroV4.fbx. ArtHero.fbx (models_hero.py / characters.py)
stays the legacy figure for older saves. Run headless:

    blender -b --factory-startup -P models_hero_v4.py -- <output folder>

Rig (pivot in brackets): as characters.py, plus ArmL/ArmR [shoulder] > ForearmL/ForearmR [elbow] > HandL/HandR [wrist] >
Weapon / OffHand [grip], and ShinL/ShinR [knee] > FootL/FootR [ankle]. Exported flat; Editor/ArtSetup.cs (RunHeroV4)
rebuilds the joints and adds HeroAnimationRig.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import characters  # noqa: E402

if __name__ == "__main__":
    characters.figure("HeroV4", **characters.HERO_V4)
