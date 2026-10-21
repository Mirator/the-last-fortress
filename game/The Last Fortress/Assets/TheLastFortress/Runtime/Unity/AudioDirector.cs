using System.Collections.Generic;
using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Plays the game's sound from the same event batch the world view receives (the bootstrap drains the simulation
    /// once per frame and hands that batch to both). Never drains events itself, never decides gameplay and never uses
    /// gameplay RNG.
    /// <para>Limits: at most <see cref="MaxVoices"/> one-shots, <see cref="MaxPerFamily"/> of one sound at a time
    /// (phase stingers one), an important sound may displace a less important one; at most two music tracks (during a
    /// crossfade). Combat sounds follow the presentation clock: they pause with Night pause/menu/guard dialogs, and an
    /// arrow's impact is scheduled for when the drawn arrow lands. Menu UI sounds stay live; music and ambience duck
    /// while play is suspended. A reset or loaded run clears pending sounds and never replays its event history.</para>
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public const int MaxVoices = 24;
        public const int MaxPerFamily = 4;
        public const int MaxMusicTracks = 2;
        public const int MaxPending = 48;
        public const float MusicFadeSeconds = 1.8f;
        const float MusicGain = 0.7f, AmbienceGain = 0.55f, SuspendedDuck = 0.45f;

        sealed class Voice
        {
            public AudioSource Source;
            public SoundId Id;
            public int Priority;
            public bool Combat;
            public bool Paused;
            public float Remaining;
            public long Order;
            public bool Busy => Remaining > 0f;
        }

        struct Pending
        {
            public SoundId Id;
            public float Delay;
        }

        sealed class Loop
        {
            public readonly AudioSource[] Sources = new AudioSource[2];
            public readonly float[] Level = new float[2];
            public readonly bool[] Active = new bool[2];
            public int Current;
            public float Gain;

            public AudioClip Clip => Active[Current] ? Sources[Current].clip : null;

            public int ActiveCount => (Active[0] ? 1 : 0) + (Active[1] ? 1 : 0);
        }

        AudioContent content;
        readonly Voice[] voices = new Voice[MaxVoices];
        readonly List<Pending> pending = new List<Pending>();
        readonly Loop music = new Loop { Gain = MusicGain };
        readonly Loop ambience = new Loop { Gain = AmbienceGain };
        readonly int[] playCounts = new int[System.Enum.GetValues(typeof(SoundId)).Length];
        readonly System.Random variation = new System.Random(7007); // presentation-only, never the simulation's RNG
        long order;
        int variant;
        float musicVolume = GameSettings.DefaultMusicVolume, sfxVolume = GameSettings.DefaultSfxVolume, masterVolume = GameSettings.DefaultMasterVolume;
        float duck = 1f;
        float lastKeepHit = -10f, combatClock;
        bool skipNextBatch, combatFrozen;

        // ---------- diagnostics (tests, capture log) ----------

        public int BatchesConsumed { get; private set; }
        public int EventsConsumed { get; private set; }
        public int PendingCount => pending.Count;
        public int PlayCount(SoundId id) => playCounts[(int)id];
        public AudioClip MusicClip => music.Clip;
        public AudioClip AmbienceClip => ambience.Clip;
        public int MusicTracks => music.ActiveCount;
        public bool CombatPaused => combatFrozen;

        public int ActiveVoices
        {
            get
            {
                var n = 0;
                foreach (var v in voices)
                    if (v.Busy) n++;
                return n;
            }
        }

        public int ActiveVoicesOf(SoundId id)
        {
            var n = 0;
            foreach (var v in voices)
                if (v.Busy && v.Id == id) n++;
            return n;
        }

        /// <summary>What a one-shot or the music actually plays at, including the master volume (0 = silence).</summary>
        public float EffectiveSfxVolume => AudioListener.volume * sfxVolume;
        public float EffectiveMusicVolume => AudioListener.volume * musicVolume * MusicGain;

        public void Initialize(AudioContent audioContent)
        {
            content = audioContent;
            for (var i = 0; i < MaxVoices; i++)
                voices[i] = new Voice { Source = NewSource($"Voice{i}", false) };
            for (var i = 0; i < 2; i++)
            {
                music.Sources[i] = NewSource($"Music{i}", true);
                ambience.Sources[i] = NewSource($"Ambience{i}", true);
            }
        }

        AudioSource NewSource(string sourceName, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        public void ApplyVolumes(float master, float musicLevel, float sfx)
        {
            masterVolume = Mathf.Clamp01(master);
            musicVolume = Mathf.Clamp01(musicLevel);
            sfxVolume = Mathf.Clamp01(sfx);
            AudioListener.volume = masterVolume;
            foreach (var v in voices)
                if (v.Busy) v.Source.volume = Gain(v.Id) * sfxVolume;
            UpdateLoopVolumes();
        }

        // ---------- one-shots ----------

        static int Priority(SoundId id)
        {
            switch (id)
            {
                case SoundId.KeepHit: return 100;
                case SoundId.GolemArrival:
                case SoundId.CommanderArrival:
                case SoundId.Victory:
                case SoundId.Defeat: return 95;
                case SoundId.NightStart:
                case SoundId.Dawn: return 90;
                case SoundId.Destroy:
                case SoundId.BomberBlast: return 70;
                case SoundId.FuseLit: return 65; // a threat warning: above towers and strikes, below destruction
                case SoundId.HeroDown: return 62; // the hero falling: below every threat warning
                case SoundId.HeavyStrike:
                case SoundId.CannonShot: return 60;
                case SoundId.Reward: return 55;
                case SoundId.Trap: return 50;
                case SoundId.UiReject: return 45;
                case SoundId.UiConfirm:
                case SoundId.Build:
                case SoundId.Sell:
                case SoundId.Repair: return 40;
                case SoundId.UiClick: return 30;
                case SoundId.HeroHurt: return 35;
                case SoundId.HeroImpact: return 25;
                case SoundId.Impact: return 20;
                case SoundId.HeroSwing: return 18;
                case SoundId.Footstep: return 5; // dropped first when voices run out
                default: return 15; // Shot, RaiderShot
            }
        }

        static bool IsCombat(SoundId id) =>
            id == SoundId.Shot || id == SoundId.Impact || id == SoundId.Trap || id == SoundId.HeavyStrike || id == SoundId.Destroy ||
            id == SoundId.KeepHit || id == SoundId.GolemArrival || id == SoundId.CannonShot || id == SoundId.RaiderShot || id == SoundId.FuseLit ||
            id == SoundId.BomberBlast || id == SoundId.CommanderArrival || id == SoundId.HeroSwing || id == SoundId.HeroImpact ||
            id == SoundId.HeroHurt || id == SoundId.HeroDown || id == SoundId.Footstep;

        static int FamilyCap(SoundId id)
        {
            switch (id)
            {
                case SoundId.NightStart:
                case SoundId.Dawn:
                case SoundId.GolemArrival:
                case SoundId.CommanderArrival:
                case SoundId.Victory:
                case SoundId.Defeat:
                case SoundId.Reward: return 1;
                case SoundId.KeepHit: return 2;
                case SoundId.HeroSwing:
                case SoundId.HeroImpact:
                case SoundId.HeroDown:
                case SoundId.Footstep: return 2;
                default: return MaxPerFamily;
            }
        }

        static float Gain(SoundId id)
        {
            switch (id)
            {
                case SoundId.Shot:
                case SoundId.RaiderShot: return 0.35f;
                case SoundId.Impact: return 0.45f;
                case SoundId.UiClick: return 0.6f;
                case SoundId.Trap: return 0.7f;
                case SoundId.Build:
                case SoundId.Sell:
                case SoundId.Repair: return 0.75f;
                case SoundId.Footstep: return 0.4f;
                case SoundId.HeroSwing: return 0.55f;
                case SoundId.HeroImpact: return 0.75f;
                case SoundId.HeroHurt: return 0.8f;
                default: return 1f;
            }
        }

        /// <summary>
        /// The hero camera is the listener (hero view): the hero's sounds then play in the world at their position with a
        /// short falloff. Otherwise everything stays 2D, as before; UI, music, stingers and every other sound are never spatial.
        /// </summary>
        public bool HeroListener { get; set; }

        /// <summary>Plays a one-shot within the voice limits; returns false when it was dropped.</summary>
        public bool Play(SoundId id) => Play(id, null);

        /// <summary>A hero sound at a world position (spatial only while <see cref="HeroListener"/>).</summary>
        public bool Play(SoundId id, Vector3? at)
        {
            var clip = content != null ? content.Clip(id, variant++) : null;
            var priority = Priority(id);
            var same = 0;
            Voice free = null, victim = null, oldestSame = null;
            foreach (var v in voices)
            {
                if (!v.Busy)
                {
                    free ??= v;
                    continue;
                }

                if (v.Id == id)
                {
                    same++;
                    if (oldestSame == null || v.Order < oldestSame.Order) oldestSame = v;
                }

                if (v.Priority < priority && (victim == null || v.Priority < victim.Priority || v.Priority == victim.Priority && v.Order < victim.Order)) victim = v;
            }

            var cap = FamilyCap(id);
            if (same >= cap)
            {
                if (cap > 1) return false; // never a flood of one sample: an ordinary sound at its cap is dropped
                free = oldestSame;         // a one-off stinger restarts instead of being lost
            }

            var voice = free ?? victim;
            if (voice == null) return false;
            if (voice.Busy) voice.Source.Stop();

            var combat = IsCombat(id);
            voice.Id = id;
            voice.Priority = priority;
            voice.Combat = combat;
            voice.Order = ++order;
            voice.Paused = false;
            var pitch = id == SoundId.Shot || id == SoundId.RaiderShot || id == SoundId.Impact ? 0.94f + 0.12f * (float)variation.NextDouble() : 1f;
            voice.Remaining = clip != null ? clip.length / pitch : 0.25f; // a missing clip still holds its voice briefly (limits stay testable)
            playCounts[(int)id]++;
            if (clip != null)
            {
                var spatial = at.HasValue && HeroListener;
                voice.Source.spatialBlend = spatial ? 1f : 0f;
                if (spatial)
                {
                    voice.Source.transform.position = at.Value;
                    voice.Source.rolloffMode = AudioRolloffMode.Linear;
                    voice.Source.minDistance = 0.4f; // tiles: full level within about 3 m, gone by about 50 m
                    voice.Source.maxDistance = 6f;
                    voice.Source.dopplerLevel = 0f;
                }

                voice.Source.clip = clip;
                voice.Source.pitch = pitch;
                voice.Source.volume = Gain(id) * sfxVolume;
                voice.Source.Play();
                if (combat && combatFrozen)
                {
                    voice.Source.Pause();
                    voice.Paused = true;
                }
            }

            return true;
        }

        long lastHeroImpactTick = -1;

        /// <summary>A planted foot of the shown release-rules-4 hero (its animation's contact, never a timer).</summary>
        public void Footfall(Vector3 at)
        {
            if (!combatFrozen) Play(SoundId.Footstep, at);
        }

        static Vector3 At(double x, double z) => new Vector3((float)x, 0.12f, (float)z);

        /// <summary>Actual Keep HP loss (the same observation that drives the HUD cue); rate-limited, highest priority.</summary>
        public void KeepHit()
        {
            if (combatClock - lastKeepHit < 0.22f) return;
            lastKeepHit = combatClock;
            Play(SoundId.KeepHit);
        }

        // ---------- per frame ----------

        /// <summary>
        /// Loaded or restarted run: stop combat sounds and pending impacts, and skip the batch that carries the load's own
        /// events (placements of restored buildings, reset), so nothing from its history is replayed.
        /// </summary>
        public void ResetRun(RunSimulation sim)
        {
            pending.Clear();
            foreach (var v in voices)
            {
                if (!v.Combat || !v.Busy) continue;
                v.Source.Stop();
                v.Remaining = 0f;
            }

            skipNextBatch = true;
        }

        /// <param name="events">This frame's batch, already drained by the bootstrap (shared with the world view).</param>
        /// <param name="fxDt">Presentation delta: zero while combat presentation is frozen.</param>
        /// <param name="frozen">Night paused, menu or guard dialog open: combat one-shots pause.</param>
        /// <param name="suspended">Menu or guard dialog open: music and ambience duck.</param>
        public void Consume(RunSimulation sim, IReadOnlyList<SimEvent> events, float fxDt, float realDt, bool frozen, bool suspended, bool titleScreen)
        {
            BatchesConsumed++;
            EventsConsumed += events.Count;
            SetCombatFrozen(frozen);
            combatClock += fxDt;

            if (skipNextBatch)
            {
                skipNextBatch = false;
            }
            else
            {
                foreach (var e in events) Handle(sim, e);
            }

            // Scheduled impacts follow the presentation clock: frozen with the arrows they belong to.
            for (var i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                p.Delay -= fxDt;
                if (p.Delay > 0f)
                {
                    pending[i] = p;
                    continue;
                }

                pending.RemoveAt(i);
                Play(p.Id);
            }

            foreach (var v in voices)
            {
                if (!v.Busy) continue;
                v.Remaining -= v.Combat ? fxDt : realDt;
                if (v.Remaining <= 0f) v.Source.Stop();
            }

            SetLoop(music, TargetMusic(sim, titleScreen));
            SetLoop(ambience, sim.State == RunState.Night || sim.State == RunState.Defeat || sim.StressMode ? content?.ambienceNight : content?.ambienceDay);
            duck = Mathf.MoveTowards(duck, suspended ? SuspendedDuck : 1f, realDt * 2f);
            FadeLoop(music, realDt);
            FadeLoop(ambience, realDt);
            UpdateLoopVolumes();
        }

        static bool ContactHero(RunSimulation sim) => sim.Hero != null && sim.Hero.Profile.ContactPhase;

        void SetCombatFrozen(bool frozen)
        {
            if (combatFrozen == frozen) return;
            combatFrozen = frozen;
            foreach (var v in voices)
            {
                if (!v.Combat || !v.Busy || v.Source.clip == null) continue;
                if (frozen && !v.Paused)
                {
                    v.Source.Pause();
                    v.Paused = true;
                }
                else if (!frozen && v.Paused)
                {
                    v.Source.UnPause();
                    v.Paused = false;
                }
            }
        }

        void Handle(RunSimulation sim, SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.RunReset:
                    pending.Clear();
                    break;
                case SimEventType.StateChanged:
                    // Stingers once per actual transition (the event is drained once), never on refresh, menu or load.
                    if (sim.StressMode) break;
                    var state = (RunState)e.SourceId;
                    if (state == RunState.Night) Play(SoundId.NightStart);
                    else if (state == RunState.Reward) Play(SoundId.Dawn);
                    else if (state == RunState.Victory) Play(SoundId.Victory);
                    else if (state == RunState.Defeat) Play(SoundId.Defeat);
                    break;
                case SimEventType.TowerShot:
                    // A Cannon booms; its shell lands with the drawn shell like an arrow, never before it.
                    Play(sim.GetBuilding(e.SourceId) is Building shooter && shooter.Kind == BuildingKind.Cannon ? SoundId.CannonShot : SoundId.Shot);
                    Schedule(SoundId.Impact, sim.GetBuilding(e.SourceId) is Building gun && gun.Kind == BuildingKind.Cannon ? WorldView.ShellSeconds : WorldView.ArrowSeconds);
                    break;
                case SimEventType.RangedShot:
                    Play(SoundId.RaiderShot);
                    break;
                case SimEventType.HeroAttacked:
                    if (!ContactHero(sim)) Play(SoundId.Shot); // the blade's whoosh (legacy swing)
                    break;
                case SimEventType.HeroContact:
                    Play(SoundId.HeroSwing, At(e.FromX, e.FromZ)); // release-rules-4: the whoosh with the blade's sweep, also on a miss
                    break;
                case SimEventType.HeroHit:
                    if (!ContactHero(sim)) Schedule(SoundId.Impact, WorldView.HeroBladeDelay); // with the shown contact
                    else if (e.Tick != lastHeroImpactTick)
                    {
                        lastHeroImpactTick = e.Tick; // one bite per contact, however many it struck
                        Play(SoundId.HeroImpact, At(e.ToX, e.ToZ));
                    }

                    break;
                case SimEventType.HeroDamaged:
                    var heavyHit = sim.GetEnemy(e.SourceId) is Enemy heavy && (heavy.Kind == EnemyKind.Brute || heavy.Definition.IsBoss);
                    if (!ContactHero(sim)) Play(heavyHit ? SoundId.HeavyStrike : SoundId.Impact);
                    else
                    {
                        if (heavyHit) Play(SoundId.HeavyStrike, At(e.FromX, e.FromZ));
                        Play(SoundId.HeroHurt, At(e.ToX, e.ToZ));
                    }

                    break;
                case SimEventType.HeroDowned:
                    if (ContactHero(sim)) Play(SoundId.HeroDown, At(e.FromX, e.FromZ));
                    else Play(SoundId.KeepHit);
                    break;
                case SimEventType.FuseLit:
                    Play(SoundId.FuseLit);
                    break;
                case SimEventType.BomberDetonated:
                    Play(SoundId.BomberBlast);
                    break;
                case SimEventType.TrapTriggered:
                    Play(SoundId.Trap);
                    break;
                case SimEventType.EnemyAttacked:
                    var attacker = sim.GetEnemy(e.SourceId);
                    var keep = sim.Keep != null && e.TargetId == sim.Keep.Id; // Keep damage has its own sound
                    if (attacker != null && !keep)
                        Play(attacker.Kind == EnemyKind.Brute || attacker.Definition.IsBoss ? SoundId.HeavyStrike : SoundId.Impact);
                    break;
                case SimEventType.BuildingDestroyed:
                    Play(SoundId.Destroy);
                    break;
                case SimEventType.EnemySpawned:
                    var kind = (EnemyKind)e.TargetId;
                    if (kind == EnemyKind.SiegeCommander) Play(SoundId.CommanderArrival);
                    else if (EnemyDefinition.IsBossKind(kind)) Play(SoundId.GolemArrival);
                    break;
                case SimEventType.BuildingPlaced:
                    if (sim.State == RunState.Day && !sim.StressMode) Play(SoundId.Build);
                    break;
                case SimEventType.BuildingSold:
                    Play(SoundId.Sell);
                    break;
                case SimEventType.BuildingRepaired:
                    Play(SoundId.Repair);
                    break;
                case SimEventType.PerkAcquired:
                    Play(SoundId.Reward);
                    break;
            }
        }

        void Schedule(SoundId id, float delay)
        {
            if (pending.Count >= MaxPending) pending.RemoveAt(0);
            pending.Add(new Pending { Id = id, Delay = delay });
        }

        AudioClip TargetMusic(RunSimulation sim, bool titleScreen)
        {
            if (content == null) return null;
            if (titleScreen) return content.musicPlanning;
            if (sim.StressMode) return content.musicCombat;
            switch (sim.State)
            {
                case RunState.Night: return sim.IsCampaign && sim.NightNumber >= sim.TotalNights ? content.musicFinale : content.musicCombat;
                case RunState.Victory:
                case RunState.Defeat: return content.musicConclusion;
                default: return content.musicPlanning;
            }
        }

        /// <summary>Crossfades to a new loop; the same clip never restarts or stacks. A third request ends the oldest fade.</summary>
        static void SetLoop(Loop loop, AudioClip clip)
        {
            if (loop.Clip == clip) return;
            var next = 1 - loop.Current;
            if (loop.Active[next])
            {
                loop.Sources[next].Stop();
                loop.Active[next] = false;
            }

            loop.Current = next;
            if (clip == null) return;
            var s = loop.Sources[next];
            s.clip = clip;
            s.volume = 0f;
            s.Play();
            loop.Level[next] = 0f;
            loop.Active[next] = true;
        }

        static void FadeLoop(Loop loop, float dt)
        {
            for (var i = 0; i < 2; i++)
            {
                if (!loop.Active[i]) continue;
                var target = i == loop.Current ? 1f : 0f;
                loop.Level[i] = Mathf.MoveTowards(loop.Level[i], target, dt / MusicFadeSeconds);
                if (target == 0f && loop.Level[i] <= 0f)
                {
                    loop.Sources[i].Stop();
                    loop.Active[i] = false;
                }
            }
        }

        void UpdateLoopVolumes()
        {
            for (var i = 0; i < 2; i++)
            {
                music.Sources[i].volume = music.Level[i] * music.Gain * musicVolume * duck;
                ambience.Sources[i].volume = ambience.Level[i] * ambience.Gain * sfxVolume * duck;
            }
        }
    }
}
