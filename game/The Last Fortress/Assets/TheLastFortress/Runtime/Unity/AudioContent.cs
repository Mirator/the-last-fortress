using UnityEngine;

namespace TheLastFortress
{
    /// <summary>Every sound the game plays. One-shots are routed through <see cref="AudioDirector"/>'s voice limits.</summary>
    public enum SoundId
    {
        UiClick,
        UiConfirm,
        UiReject,
        Build,
        Sell,
        Repair,
        Reward,
        Shot,
        Impact,
        Trap,
        HeavyStrike,
        Destroy,
        KeepHit,
        NightStart,
        Dawn,
        GolemArrival,
        Victory,
        Defeat,
        CannonShot,
        RaiderShot,
        FuseLit,
        BomberBlast,
        CommanderArrival,

        // Plan 019: the release-rules-4 hero's own sounds (played in the world near the hero camera).
        HeroSwing,
        HeroImpact,
        HeroHurt,
        HeroDown,
        Footstep,
    }

    /// <summary>
    /// Clip references for <see cref="AudioDirector"/> (Audio/AudioContent.asset, filled by the setup menu from
    /// Audio/Clips). Any missing clip is simply silent; the game stays fully readable without sound.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Fortress/Audio Content", fileName = "AudioContent")]
    public sealed class AudioContent : ScriptableObject
    {
        [Header("Interface and construction")]
        public AudioClip uiClick;
        public AudioClip uiConfirm;
        public AudioClip uiReject;
        public AudioClip build;
        public AudioClip sell;
        public AudioClip repair;
        public AudioClip reward;

        [Header("Combat (variants picked in turn)")]
        public AudioClip[] shots;
        public AudioClip[] impacts;
        public AudioClip trap;
        public AudioClip heavyStrike;
        public AudioClip destroy;
        public AudioClip keepHit;

        [Header("Release threats and towers")]
        public AudioClip cannonShot;
        public AudioClip raiderShot;
        public AudioClip fuseLit;
        public AudioClip bomberBlast;
        public AudioClip commanderArrival;

        [Header("Release-rules-4 hero (variants picked in turn)")]
        public AudioClip[] heroSwings;
        public AudioClip[] heroImpacts;
        public AudioClip heroHurt;
        public AudioClip heroDown;
        public AudioClip[] footsteps;

        [Header("Phases")]
        public AudioClip nightStart;
        public AudioClip dawn;
        public AudioClip golemArrival;
        public AudioClip victory;
        public AudioClip defeat;

        [Header("Loops")]
        public AudioClip ambienceDay;
        public AudioClip ambienceNight;
        public AudioClip musicPlanning;
        public AudioClip musicCombat;
        public AudioClip musicFinale;
        public AudioClip musicConclusion;

        public AudioClip Clip(SoundId id, int variant)
        {
            switch (id)
            {
                case SoundId.UiClick: return uiClick;
                case SoundId.UiConfirm: return uiConfirm;
                case SoundId.UiReject: return uiReject;
                case SoundId.Build: return build;
                case SoundId.Sell: return sell;
                case SoundId.Repair: return repair;
                case SoundId.Reward: return reward;
                case SoundId.Shot: return Pick(shots, variant);
                case SoundId.Impact: return Pick(impacts, variant);
                case SoundId.Trap: return trap;
                case SoundId.HeavyStrike: return heavyStrike;
                case SoundId.Destroy: return destroy;
                case SoundId.KeepHit: return keepHit;
                case SoundId.NightStart: return nightStart;
                case SoundId.Dawn: return dawn;
                case SoundId.GolemArrival: return golemArrival;
                case SoundId.Victory: return victory;
                case SoundId.Defeat: return defeat;
                case SoundId.CannonShot: return cannonShot;
                case SoundId.RaiderShot: return raiderShot;
                case SoundId.FuseLit: return fuseLit;
                case SoundId.BomberBlast: return bomberBlast;
                case SoundId.CommanderArrival: return commanderArrival;
                case SoundId.HeroSwing: return Pick(heroSwings, variant);
                case SoundId.HeroImpact: return Pick(heroImpacts, variant);
                case SoundId.HeroHurt: return heroHurt;
                case SoundId.HeroDown: return heroDown;
                case SoundId.Footstep: return Pick(footsteps, variant);
                default: return null;
            }
        }

        static AudioClip Pick(AudioClip[] clips, int variant) => clips == null || clips.Length == 0 ? null : clips[(variant % clips.Length + clips.Length) % clips.Length];
    }
}
