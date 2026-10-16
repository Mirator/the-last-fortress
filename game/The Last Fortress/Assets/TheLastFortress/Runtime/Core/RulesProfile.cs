using System.Collections.Generic;

namespace TheLastFortress
{
    /// <summary>
    /// One playable rules profile (plan 008): rules version, campaign, the definitions and perk pool it plays with, what can
    /// be built and which checkpoint schema it writes. Saves name their profile; a build restores only profiles it ships.
    /// </summary>
    public sealed class RulesProfile
    {
        public string RulesVersion;
        public string CampaignId;
        public string Label;
        public int ContentVersion;

        /// <summary>Checkpoint schema written for runs of this profile (MVP: 1, unchanged; release: 2).</summary>
        public int Schema;

        public PrototypeDefinitions Definitions;
        public IReadOnlyList<string> PerkPool;
        public IReadOnlyList<BuildingKind> PurchasableKinds;

        public bool IsRelease => RulesVersion == ReleaseCampaign.RulesVersion;

        public static readonly BuildingKind[] MvpKinds =
            { BuildingKind.Wall, BuildingKind.Tower, BuildingKind.House, BuildingKind.Farm, BuildingKind.SpikeTrap };

        public static readonly BuildingKind[] ReleaseKinds =
        {
            BuildingKind.Wall, BuildingKind.Tower, BuildingKind.House, BuildingKind.Farm, BuildingKind.SpikeTrap,
            BuildingKind.Cannon, BuildingKind.Watchtower, BuildingKind.RepairStation,
        };

        /// <summary>The six-night MVP (also the legacy profile every release build keeps for old saves).</summary>
        public static RulesProfile SixNights(PrototypeDefinitions defs) => new RulesProfile
        {
            RulesVersion = MvpCampaign.RulesVersion, CampaignId = MvpCampaign.CampaignId, Label = "Six nights", ContentVersion = 0,
            Schema = RunCheckpoint.MvpSchema, Definitions = defs, PerkPool = PerkRules.AllIds, PurchasableKinds = MvpKinds,
        };

        public static RulesProfile TwelveNights(PrototypeDefinitions defs) => new RulesProfile
        {
            RulesVersion = ReleaseCampaign.RulesVersion, CampaignId = ReleaseCampaign.CampaignId, Label = ReleaseCampaign.Label,
            ContentVersion = ReleaseCampaign.ContentVersion, Schema = RunCheckpoint.CurrentSchema, Definitions = defs,
            PerkPool = PerkRules.ReleaseIds, PurchasableKinds = ReleaseKinds,
        };
    }

    /// <summary>The profiles a build can restore. The first is the build's default for new runs.</summary>
    public sealed class RulesCatalog
    {
        readonly List<RulesProfile> profiles = new List<RulesProfile>();

        public RulesCatalog(params RulesProfile[] items)
        {
            profiles.AddRange(items);
        }

        public IReadOnlyList<RulesProfile> Profiles => profiles;
        public RulesProfile Default => profiles.Count > 0 ? profiles[0] : null;

        public RulesProfile Find(string rulesVersion, string campaignId)
        {
            foreach (var p in profiles)
                if (p.RulesVersion == rulesVersion && p.CampaignId == campaignId) return p;
            return null;
        }

        /// <summary>The profile a live run plays under (by its campaign's rules and campaign IDs).</summary>
        public RulesProfile For(CampaignConfig campaign) =>
            campaign == null ? null : Find(campaign.RulesVersion, campaign.CampaignId);
    }
}
