using UnityEngine;

namespace TheLastFortress
{
    /// <summary>
    /// Single owner of run-save writes (serialized, synchronous). Checkpoint policy from plan 005:
    /// Day 1 after creation; each new Day after its perk/income transaction; immediately before Night; Save and Quit during
    /// Day. On first Reward entry the frozen offer is attached to the last pre-combat Day checkpoint before a choice is
    /// allowed. Victory/Defeat persist a terminal record. A failed write keeps the exact snapshot pending for Retry and
    /// blocks progress; the transition that produced it is never re-run.
    /// </summary>
    public sealed class RunPersistence
    {
        public SaveStore Store { get; }
        public int Slot { get; private set; } = -1;

        /// <summary>Last committed Day checkpoint (pre-combat state of the current night).</summary>
        public RunCheckpoint LastDay { get; private set; }

        /// <summary>Serialized record awaiting a successful write; null when everything is durable.</summary>
        public RunCheckpoint Pending { get; private set; }

        public string LastError { get; private set; }
        public bool SavePending => Pending != null;
        public bool HasRun => Slot >= 0;
        public int WriteCount { get; private set; }

        public RunPersistence(SaveStore store)
        {
            Store = store;
        }

        /// <summary>
        /// Picks the slot for a new run: an ended (or absent) latest run's slot is reused; an active or damaged one is never
        /// overwritten, so the new run goes to the next slot file.
        /// </summary>
        public void BeginNewRun(PrototypeDefinitions defs) => BeginNewRun(new RulesCatalog(RulesProfile.SixNights(defs)));

        /// <summary>As above with the build's profiles: a save this build cannot read is never reused or overwritten.</summary>
        public void BeginNewRun(RulesCatalog catalog)
        {
            var latest = Store.LatestSlot();
            if (latest < 0) Slot = 0;
            else
            {
                var existing = Store.Read(latest, catalog);
                Slot = existing.Status == SaveStatus.Terminal || existing.Status == SaveStatus.None ? latest : latest + 1;
            }

            LastDay = null;
            Pending = null;
            LastError = null;
        }

        /// <summary>Adopts a loaded save as the active run.</summary>
        public void Adopt(SaveLoad load)
        {
            Slot = load.Slot;
            LastDay = load.Checkpoint;
            Pending = null;
            LastError = null;
        }

        public void Detach()
        {
            Slot = -1;
            LastDay = null;
            Pending = null;
            LastError = null;
        }

        /// <summary>
        /// Whether the live Day differs from the last durable checkpoint in saved player progress: gold, buildings
        /// (type/anchor/HP/facing/price), perks or Day number. Evaluated on request only; ignores view state, grid revision
        /// and offer/run identifiers.
        /// </summary>
        public bool HasUnsavedDayEdits(RunSimulation sim)
        {
            if (!HasRun || sim.State != RunState.Day) return false;
            if (LastDay == null) return true;
            var now = CheckpointCodec.Capture(sim);
            if (now.gold != LastDay.gold || now.checkpointNight != LastDay.checkpointNight) return true;
            if (now.perks.Length != LastDay.perks.Length || now.buildings.Length != LastDay.buildings.Length) return true;
            for (var i = 0; i < now.perks.Length; i++)
                if (now.perks[i] != LastDay.perks[i]) return true;
            for (var i = 0; i < now.buildings.Length; i++)
            {
                BuildingData a = now.buildings[i], b = LastDay.buildings[i];
                if (a.id != b.id || a.kind != b.kind || a.x != b.x || a.z != b.z || a.hp != b.hp || a.orientation != b.orientation || a.price != b.price)
                    return true;
            }

            return false;
        }

        /// <summary>Captures and commits the current Day. Returns false (and keeps it pending) when the write fails.</summary>
        public bool CommitDay(RunSimulation sim)
        {
            if (!HasRun || sim.State != RunState.Day) return false;
            var cp = CheckpointCodec.Capture(sim);
            // A replayed night keeps its frozen offer attached until that night is cleared again.
            if (LastDay != null && LastDay.hasPendingOffer && LastDay.checkpointNight == cp.checkpointNight)
            {
                cp.hasPendingOffer = true;
                cp.pendingOffer = LastDay.pendingOffer;
            }

            if (!Write(cp)) return false;
            LastDay = cp;
            return true;
        }

        /// <summary>Attaches the frozen offer (IDs + RNG before/after) to the pre-combat checkpoint before any choice.</summary>
        public bool CommitRewardLedger(RunSimulation sim)
        {
            if (!HasRun || LastDay == null || sim.State != RunState.Reward || sim.CurrentOffer == null) return false;
            var cp = Clone(LastDay);
            cp.hasPendingOffer = true;
            cp.pendingOffer = CheckpointCodec.Offer(sim.CurrentOffer);
            if (!Write(cp)) return false;
            LastDay = cp;
            return true;
        }

        /// <summary>Victory/Defeat: the run can no longer be continued; the summary is kept.</summary>
        public bool CommitTerminal(RunSimulation sim)
        {
            if (!HasRun || LastDay == null) return false;
            var cp = Clone(LastDay);
            cp.status = sim.State == RunState.Victory ? "Victory" : "Defeat";
            cp.finalStats = CheckpointCodec.Stats(sim.Stats);
            return Write(cp);
        }

        /// <summary>Retries the exact pending snapshot.</summary>
        public bool Retry()
        {
            if (Pending == null) return true;
            var cp = Pending;
            if (!Write(cp)) return false;
            if (cp.status == "Active") LastDay = cp;
            return true;
        }

        bool Write(RunCheckpoint cp)
        {
            if (Store.WriteRun(Slot, cp, out var error))
            {
                Pending = null;
                LastError = null;
                WriteCount++;
                return true;
            }

            Pending = cp;
            LastError = error;
            Debug.LogWarning(error);
            return false;
        }

        static RunCheckpoint Clone(RunCheckpoint cp) => JsonUtility.FromJson<RunCheckpoint>(JsonUtility.ToJson(cp));
    }
}
