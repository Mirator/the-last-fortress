namespace TheLastFortress
{
    /// <summary>First-run guide steps, in the order they are suggested.</summary>
    public enum OnboardingStep
    {
        PlaceTower = 0,
        InspectRange = 1,
        Economy = 2,
        StartNight = 3,
        ChooseReward = 4,
    }

    /// <summary>
    /// Progressive, skippable first-run guide. It only observes the run (buildings that exist, the selection, the
    /// phase, owned perks) and never changes simulation, gold, offers or waves. A step completes from a successful
    /// action, never from a click or timeout alone: an invalid placement places nothing, so it completes nothing.
    /// Steps already done in any order are recognized; starting Night passes the optional earlier steps.
    /// </summary>
    public sealed class OnboardingState
    {
        public const int StepCount = 5;

        readonly OnboardingProfile profile;

        public OnboardingState(OnboardingProfile profile)
        {
            this.profile = profile ?? new OnboardingProfile();
        }

        public OnboardingProfile Profile => profile;

        /// <summary>The guide follows this run (New Run while the guide is unfinished); false for Continue.</summary>
        public bool Active { get; private set; }

        /// <summary>Hidden by the player for now; Help shows it again.</summary>
        public bool Hidden { get; set; }

        public bool Visible => Active && !Hidden && !profile.Finished;

        /// <summary>First step not yet done, or null when all are.</summary>
        public OnboardingStep? Current
        {
            get
            {
                for (var i = 0; i < StepCount; i++)
                    if (!IsDone((OnboardingStep)i)) return (OnboardingStep)i;
                return null;
            }
        }

        /// <summary>The step to show now, or null: the reward step only appears on an actual Reward screen.</summary>
        public OnboardingStep? Shown(RunSimulation sim)
        {
            if (!Visible) return null;
            var step = Current;
            return step == OnboardingStep.ChooseReward && sim.State != RunState.Reward ? null : step;
        }

        public bool IsDone(OnboardingStep step) => (profile.stepsDone & (1 << (int)step)) != 0;

        /// <summary>Starts guiding a freshly created run, unless the player skipped or finished the guide.</summary>
        public void BeginRun()
        {
            Active = !profile.Finished;
            Hidden = false;
        }

        /// <summary>Continue/recovery: an old run never restarts the guide.</summary>
        public void EndRun() => Active = false;

        /// <summary>Help: show the guide again for this run, at its first unfinished step (from the start if it was finished).</summary>
        public void Reopen()
        {
            profile.skipped = false;
            if (profile.completed)
            {
                profile.completed = false; // finished before: start again from step 1
                profile.stepsDone = 0;
            }

            Active = true;
            Hidden = false;
        }

        public void Skip()
        {
            profile.skipped = true;
            Hidden = true;
        }

        /// <summary>Marks steps done from the current run; returns true when the profile changed (caller persists it).</summary>
        public bool Observe(RunSimulation sim, PlayerController player)
        {
            if (!Active || profile.Finished || sim == null || !sim.IsCampaign || sim.StressMode) return false;
            var before = profile.stepsDone;
            var towers = 0;
            var economy = 0;
            foreach (var b in sim.Grid.AllBuildings())
            {
                if (!b.IsAlive) continue;
                if (b.Kind == BuildingKind.Tower) towers++;
                else if (b.Kind == BuildingKind.House || b.Kind == BuildingKind.Farm) economy++;
            }

            if (towers > 0) Mark(OnboardingStep.PlaceTower);
            if (player.Selected != null && player.Selected.Kind == BuildingKind.Tower) Mark(OnboardingStep.InspectRange);
            if (economy > 0) Mark(OnboardingStep.Economy);
            if (sim.State != RunState.Day || sim.NightNumber > 1)
            {
                // The night started: the optional preparation tips are passed, not repeated.
                Mark(OnboardingStep.PlaceTower);
                Mark(OnboardingStep.InspectRange);
                Mark(OnboardingStep.Economy);
                Mark(OnboardingStep.StartNight);
            }

            if (sim.OwnedPerks.Count > 0) Mark(OnboardingStep.ChooseReward);
            if (Current == null) profile.completed = true;
            return profile.stepsDone != before;
        }

        void Mark(OnboardingStep step) => profile.stepsDone |= 1 << (int)step;

        public static string Text(OnboardingStep step)
        {
            switch (step)
            {
                case OnboardingStep.PlaceTower:
                    return "Build an Archer Tower: press 2 or pick its card, then click a free tile beside the path the enemies will take.";
                case OnboardingStep.InspectRange:
                    return "Click your Tower to see its range. The cyan line is the route enemies follow to the Keep.";
                case OnboardingStep.Economy:
                    return "Optional: a House or Farm adds gold every dawn. Invest now, or keep the gold for defense.";
                case OnboardingStep.StartNight:
                    return "Ready? Press START NIGHT. Towers fire on their own; Space pauses.";
                default:
                    return "You held the night. Pick one reward card; it lasts for the whole run.";
            }
        }
    }
}
