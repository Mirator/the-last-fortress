using System;
using System.Collections.Generic;
using System.Globalization;

namespace TheLastFortress
{
    /// <summary>
    /// Versioned plain-data Day checkpoint. Schema 1 is the six-night MVP (unchanged since plan 005); schema 2 (plan 008)
    /// adds the rules profile: campaign, content version, map profile and difficulty. Stable definition IDs only; uint
    /// values are invariant decimal strings.
    /// Holds the actual terrain and wave plans so resume never reruns an evolved generator. Never deserialized into the
    /// live world: <see cref="CheckpointCodec.Restore"/> validates everything and builds a fresh simulation.
    /// </summary>
    [Serializable]
    public sealed class RunCheckpoint
    {
        /// <summary>Newest schema this build writes (release profiles). The MVP keeps writing <see cref="MvpSchema"/>.</summary>
        public const int CurrentSchema = 2;

        public const int MvpSchema = 1;

        // Defaults are deliberately invalid so an empty or truncated JSON object is rejected, not read as version 1.
        public int schemaVersion;
        public string rulesVersion = "";
        public string seed = "0";
        public string profile = "";

        // Schema 2 (plan 008): which rules profile the run plays. Absent (empty) in schema 1, which implies the six-night MVP.
        public string campaignId = "";
        public int contentVersion;
        public string mapProfile = "";
        public string difficulty = "";
        public bool flatMap;
        public bool mapFallback;
        public int mapAttempt = -1;
        public int[] terrain = new int[0];

        /// <summary>Spec 016: gold vein cells as z * 20 + x (optional; older saves have none).</summary>
        public int[] veins = new int[0];

        /// <summary>Spec 017: fertile cells as z * 20 + x. Optional: without <see cref="fertileListed"/> (older saves) every free land cell is fertile.</summary>
        public int[] fertile = new int[0];

        public bool fertileListed;

        /// <summary>Spec 017: the map's Keep cell (z * 20 + x) and entries in index order. Optional: -1 / none = the centre Keep and the nights' own entries.</summary>
        public int keepCell = -1;

        public int[] entries = new int[0];

        /// <summary>Spec 017 River Valley: river crossings as z * 20 + x (optional, presentation).</summary>
        public int[] fords = new int[0];
        public NightData[] nights = new NightData[0];

        /// <summary>Active, Victory or Defeat. Terminal records keep the summary but cannot be continued.</summary>
        public string status = "Active";

        /// <summary>Day number this checkpoint resumes (Day n precedes Night n).</summary>
        public int checkpointNight = 1;

        public int gold;
        public int lastPaidDay = 1;
        public BuildingData[] buildings = new BuildingData[0];
        public int nextBuildingId = 1;
        public int nextEnemyId = 1;
        public string[] perks = new string[0];
        public string rewardRngState = "0";

        /// <summary>Frozen reward offer of this Day's Night, persisted on first Reward entry (replays reuse it, no reroll).</summary>
        public bool hasPendingOffer;

        public OfferData pendingOffer = new OfferData();
        public OfferData[] offerHistory = new OfferData[0];
        public StatsData stats = new StatsData();

        /// <summary>Stats at the terminal moment (status Victory/Defeat), for the summary screen.</summary>
        public StatsData finalStats = new StatsData();

        // release-rules-2 (plan 012): where the hero stood when the Night started, so Continue replays it from there.
        // Written only by the Start Night checkpoint; absent (false) otherwise, which means the spawn cell.
        public bool heroAtNight;

        public int heroX, heroZ;

        // release-rules-4 (plan 019): the motor velocity at Start Night, so a moving start replays. Absent (0) means rest.
        public int heroVx, heroVz;
    }

    [Serializable]
    public sealed class NightData
    {
        public string name = "";
        public int[] entryX = new int[0];
        public int[] entryZ = new int[0];
        public SpawnData[] spawns = new SpawnData[0];
    }

    [Serializable]
    public sealed class SpawnData
    {
        public long tick;
        public int x, z, entry, kind;
    }

    [Serializable]
    public sealed class BuildingData
    {
        public int id, kind, x, z, orientation, hp, price;

        /// <summary>Spec 015: upgrade tier and total price paid; 0 (absent in older saves) means tier I and the purchase price.</summary>
        public int tier, paid;
    }

    [Serializable]
    public sealed class OfferData
    {
        public int clearedNight;
        public string offerId = "";
        public string[] perks = new string[0];
        public string rngBefore = "0";
        public string rngAfter = "0";
        public string chosen = "";
    }

    [Serializable]
    public sealed class StatsData
    {
        public int nightsCleared, enemiesDefeated, buildingsPurchased, goldEarned;

        /// <summary>Enemies killed by the hero (release-rules-2; 0 in older saves).</summary>
        public int heroKills;
        public string[] synergiesEver = new string[0];
    }

    public enum CheckpointProblem
    {
        None,
        UnknownSchema,
        IncompatibleRules,
        Invalid,
        Terminal,
    }

    /// <summary>Conversion between a live Day simulation and <see cref="RunCheckpoint"/>, with full validation on load.</summary>
    public static class CheckpointCodec
    {
        /// <summary>The six-night MVP rules. Bump a profile's version when its semantics change; old saves are then rejected, never converted.</summary>
        public const string RulesVersion = MvpCampaign.RulesVersion;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string U(uint v) => v.ToString(Inv);

        public static bool TryParseU(string s, out uint v) => uint.TryParse(s, NumberStyles.None, Inv, out v);

        /// <summary>
        /// Snapshot of a Day (current construction/HP/gold/perks). Throws outside Day: no mid-combat saves. With
        /// <paramref name="nightStart"/> (the checkpoint written at Start Night) a hero run also stores the hero's position.
        /// </summary>
        public static RunCheckpoint Capture(RunSimulation sim, bool nightStart = false)
        {
            if (sim.State != RunState.Day) throw new InvalidOperationException("Checkpoints are Day-only.");
            if (!sim.IsCampaign) throw new InvalidOperationException("Only campaign runs are saved.");
            var cfg = sim.Config;
            var campaign = sim.Campaign;
            var release = campaign.RulesVersion != MvpCampaign.RulesVersion;
            var cp = new RunCheckpoint
            {
                schemaVersion = release ? RunCheckpoint.CurrentSchema : RunCheckpoint.MvpSchema,
                rulesVersion = campaign.RulesVersion,
                seed = U(cfg.Seed),
                profile = campaign.Label ?? "",
                campaignId = release ? campaign.CampaignId : "",
                contentVersion = release ? ReleaseCampaign.ContentVersion : 0,
                mapProfile = release ? campaign.MapProfile ?? "" : "",
                difficulty = release ? campaign.Difficulty ?? "" : "",
                flatMap = cfg.Terrain == null || cfg.Terrain.Authored,
                mapFallback = cfg.Terrain != null && cfg.Terrain.Fallback,
                mapAttempt = cfg.Terrain?.Attempt ?? -1,
                checkpointNight = sim.NightNumber,
                gold = sim.Gold,
                lastPaidDay = sim.LastPaidDay,
                nextBuildingId = sim.NextBuildingId,
                nextEnemyId = sim.NextEnemyId,
                perks = new List<string>(sim.OwnedPerks).ToArray(),
                rewardRngState = U(sim.RewardRngState),
                stats = Stats(sim.Stats),
            };

            cp.terrain = new int[GridCoordinates.Size * GridCoordinates.Size];
            if (cfg.Terrain != null)
                for (var i = 0; i < cp.terrain.Length; i++) cp.terrain[i] = (int)cfg.Terrain.Cells[i];
            cp.veins = cfg.Terrain == null ? new int[0] : cfg.Terrain.Veins.ConvertAll(v => v.Z * GridCoordinates.Size + v.X).ToArray();
            cp.fertileListed = cfg.Terrain?.Fertile != null;
            var fertile = new List<int>();
            if (cp.fertileListed)
                foreach (var c in cfg.Terrain.Fertile) fertile.Add(c.Z * GridCoordinates.Size + c.X);
            fertile.Sort();
            cp.fertile = fertile.ToArray();
            cp.keepCell = cfg.Terrain == null ? -1 : cfg.Terrain.Keep.Z * GridCoordinates.Size + cfg.Terrain.Keep.X;
            cp.entries = cfg.Terrain?.Entries == null ? new int[0] : cfg.Terrain.Entries.ConvertAll(e => e.Z * GridCoordinates.Size + e.X).ToArray();
            cp.fords = cfg.Terrain == null ? new int[0] : cfg.Terrain.Fords.ConvertAll(f => f.Z * GridCoordinates.Size + f.X).ToArray();

            var nights = new List<NightData>();
            foreach (var w in sim.Campaign.Nights)
            {
                var n = new NightData { name = w.Name, entryX = new int[w.EntryCells.Count], entryZ = new int[w.EntryCells.Count] };
                for (var i = 0; i < w.EntryCells.Count; i++)
                {
                    n.entryX[i] = w.EntryCells[i].X;
                    n.entryZ[i] = w.EntryCells[i].Z;
                }

                var spawns = new List<SpawnData>();
                foreach (var s in w.Spawns) spawns.Add(new SpawnData { tick = s.Tick, x = s.Cell.X, z = s.Cell.Z, entry = s.EntryIndex, kind = (int)s.Kind });
                n.spawns = spawns.ToArray();
                nights.Add(n);
            }

            cp.nights = nights.ToArray();

            var buildings = new List<BuildingData>();
            foreach (var b in sim.Grid.AllBuildings())
                buildings.Add(new BuildingData { id = b.Id, kind = (int)b.Kind, x = b.Cell.X, z = b.Cell.Z, orientation = b.Orientation, hp = b.Hp, price = b.PurchasePrice, tier = b.Tier, paid = b.TotalPaid });
            buildings.Sort((a, b) => a.id.CompareTo(b.id));
            cp.buildings = buildings.ToArray();

            var history = new List<OfferData>();
            foreach (var o in sim.OfferHistory) history.Add(Offer(o));
            cp.offerHistory = history.ToArray();
            if (nightStart && sim.Hero != null)
            {
                cp.heroAtNight = true;
                cp.heroX = sim.Hero.UnitsX;
                cp.heroZ = sim.Hero.UnitsZ;
                if (sim.Hero.Profile.ContactPhase)
                {
                    cp.heroVx = sim.Hero.VelX;
                    cp.heroVz = sim.Hero.VelZ;
                }
            }

            return cp;
        }

        public static OfferData Offer(RewardOffer o) => new OfferData
        {
            clearedNight = o.ClearedNight, offerId = o.OfferId ?? "", perks = (string[])o.PerkIds.Clone(),
            rngBefore = U(o.RngBefore), rngAfter = U(o.RngAfter), chosen = o.ChosenPerk ?? "",
        };

        public static StatsData Stats(RunStats s) => new StatsData
        {
            nightsCleared = s.NightsCleared, enemiesDefeated = s.EnemiesDefeated, buildingsPurchased = s.BuildingsPurchased,
            goldEarned = s.GoldEarned, heroKills = s.HeroKills, synergiesEver = new List<string>(s.SynergiesEver).ToArray(),
        };

        /// <summary>
        /// Validates the record completely, then builds and returns a fresh simulation at the checkpoint Day. On any problem
        /// returns null with a reason; nothing outside the returned object is touched.
        /// </summary>
        public static RunSimulation Restore(RunCheckpoint cp, PrototypeDefinitions defs, out CheckpointProblem problem, out string message) =>
            Restore(cp, new RulesCatalog(RulesProfile.SixNights(defs)), out problem, out message);

        /// <summary>Restores with the profiles this build ships; a save of any other profile is incompatible and untouched.</summary>
        public static RunSimulation Restore(RunCheckpoint cp, RulesCatalog catalog, out CheckpointProblem problem, out string message)
        {
            problem = CheckpointProblem.Invalid;
            message = null;
            try
            {
                return RestoreUnchecked(cp, catalog, out problem, out message);
            }
            catch (Exception e)
            {
                problem = CheckpointProblem.Invalid;
                message = "Save data is inconsistent: " + e.Message;
                return null;
            }
        }

        static RunSimulation Fail(string why, out CheckpointProblem problem, out string message, CheckpointProblem kind = CheckpointProblem.Invalid)
        {
            problem = kind;
            message = why;
            return null;
        }

        /// <summary>
        /// Resolves the save's rules profile. Schema 1 is always the six-night MVP; schema 2 names its profile; anything else
        /// is from a newer build. Never converts a save into another profile.
        /// </summary>
        static RulesProfile ResolveProfile(RunCheckpoint cp, RulesCatalog catalog, out CheckpointProblem problem, out string message)
        {
            problem = CheckpointProblem.None;
            message = null;
            RulesProfile profile;
            if (cp.schemaVersion == RunCheckpoint.MvpSchema)
            {
                profile = cp.rulesVersion == MvpCampaign.RulesVersion ? catalog.Find(MvpCampaign.RulesVersion, MvpCampaign.CampaignId) : null;
                if (profile == null)
                {
                    problem = CheckpointProblem.IncompatibleRules;
                    message = $"Save uses rules '{cp.rulesVersion}', this build does not include them. The file was left untouched.";
                }

                return profile;
            }

            if (cp.schemaVersion != RunCheckpoint.CurrentSchema)
            {
                problem = CheckpointProblem.UnknownSchema;
                message = $"Unknown save schema {cp.schemaVersion} (this build reads {RunCheckpoint.MvpSchema}-{RunCheckpoint.CurrentSchema}). " +
                          "It was probably made by a newer version; the file was left untouched — update the game to continue it.";
                return null;
            }

            profile = catalog.Find(cp.rulesVersion, cp.campaignId);
            if (profile == null || cp.contentVersion != profile.ContentVersion)
            {
                problem = CheckpointProblem.IncompatibleRules;
                message = $"Save uses rules '{cp.rulesVersion}' / campaign '{cp.campaignId}' (content {cp.contentVersion}), which this build does not include. The file was left untouched.";
                return null;
            }

            return profile;
        }

        static RunSimulation RestoreUnchecked(RunCheckpoint cp, RulesCatalog catalog, out CheckpointProblem problem, out string message)
        {
            if (cp == null) return Fail("Save is empty.", out problem, out message);
            var profile = ResolveProfile(cp, catalog, out problem, out message);
            if (profile == null) return null;
            var defs = profile.Definitions;
            var release = cp.schemaVersion == RunCheckpoint.CurrentSchema;
            if (release && cp.mapProfile != "" && !MapProfiles.IsKnown(cp.mapProfile)) return Fail($"Unknown map profile '{cp.mapProfile}'.", out problem, out message);
            if (release && cp.difficulty != "standard") return Fail($"Unknown difficulty '{cp.difficulty}'.", out problem, out message);
            if (cp.status != "Active")
                return Fail($"This run already ended ({cp.status}).", out problem, out message, CheckpointProblem.Terminal);
            if (!TryParseU(cp.seed, out var seed)) return Fail("Bad seed.", out problem, out message);
            if (!TryParseU(cp.rewardRngState, out var rngState) || rngState == 0) return Fail("Bad reward RNG state.", out problem, out message);

            // Terrain
            const int cells = GridCoordinates.Size * GridCoordinates.Size;
            if (cp.terrain == null || cp.terrain.Length != cells) return Fail("Terrain size mismatch.", out problem, out message);
            var map = new TerrainMap { Seed = seed, Attempt = cp.mapAttempt, Fallback = cp.mapFallback, Authored = cp.flatMap, Profile = release ? cp.mapProfile : "" };
            if (cp.keepCell >= 0)
            {
                if (cp.keepCell >= cells) return Fail("Bad Keep cell.", out problem, out message);
                map.Keep = new GridPos(cp.keepCell % GridCoordinates.Size, cp.keepCell / GridCoordinates.Size);
                if (GridCoordinates.IsReservedRing(map.Keep)) return Fail("Bad Keep cell.", out problem, out message);
            }

            if (cp.entries != null && cp.entries.Length > 0)
            {
                map.Entries = new List<GridPos>();
                foreach (var e in cp.entries)
                {
                    var c = new GridPos(e % GridCoordinates.Size, e / GridCoordinates.Size);
                    if (e < 0 || e >= cells || !GridCoordinates.IsReservedRing(c) || map.Entries.Contains(c)) return Fail("Bad map entry.", out problem, out message);
                    map.Entries.Add(c);
                }
            }
            for (var i = 0; i < cells; i++)
            {
                if (cp.terrain[i] < 0 || cp.terrain[i] > (int)TerrainType.Water) return Fail("Unknown terrain type.", out problem, out message);
                map.Cells[i] = (TerrainType)cp.terrain[i];
                var c = new GridPos(i % GridCoordinates.Size, i / GridCoordinates.Size);
                if (map.Cells[i] != TerrainType.Land && (c == map.Keep || GridCoordinates.IsReservedRing(c)))
                    return Fail($"Terrain blocks a fixed cell {c}.", out problem, out message);
            }

            foreach (var v in cp.veins ?? new int[0])
            {
                var c = new GridPos(v % GridCoordinates.Size, v / GridCoordinates.Size);
                if (v < 0 || v >= cells || map.Cells[v] != TerrainType.Land || c == map.Keep || GridCoordinates.IsReservedRing(c) || map.IsVein(c))
                    return Fail("Bad gold vein.", out problem, out message);
                map.Veins.Add(c);
            }

            foreach (var f in cp.fords ?? new int[0])
            {
                if (f < 0 || f >= cells || map.Cells[f] != TerrainType.Land) return Fail("Bad ford.", out problem, out message);
                map.Fords.Add(new GridPos(f % GridCoordinates.Size, f / GridCoordinates.Size));
            }

            if (cp.fertileListed)
            {
                map.Fertile = new HashSet<GridPos>();
                foreach (var f in cp.fertile ?? new int[0])
                    if (f < 0 || f >= cells || map.Cells[f] != TerrainType.Land || !map.Fertile.Add(new GridPos(f % GridCoordinates.Size, f / GridCoordinates.Size)))
                        return Fail("Bad fertile cell.", out problem, out message);
            }

            // Waves
            if (cp.nights == null || cp.nights.Length == 0) return Fail("No wave plans.", out problem, out message);
            var nights = new List<WavePlan>();
            foreach (var n in cp.nights)
            {
                if (n == null || n.entryX == null || n.entryZ == null || n.entryX.Length == 0 || n.entryX.Length != n.entryZ.Length || n.spawns == null)
                    return Fail("Malformed wave plan.", out problem, out message);
                var entries = new GridPos[n.entryX.Length];
                for (var i = 0; i < entries.Length; i++)
                {
                    entries[i] = new GridPos(n.entryX[i], n.entryZ[i]);
                    if (!GridCoordinates.IsReservedRing(entries[i])) return Fail("Wave entry is not on the boundary.", out problem, out message);
                }

                var spawns = new List<SpawnEntry>();
                long last = 0;
                foreach (var s in n.spawns)
                {
                    if (s == null || s.tick < last || s.entry < 0 || s.entry >= entries.Length || s.kind < 0 || s.kind > (int)EnemyKind.SiegeCommander)
                        return Fail("Malformed spawn.", out problem, out message);
                    if (entries[s.entry] != new GridPos(s.x, s.z) || defs.EnemyFor((EnemyKind)s.kind) == null)
                        return Fail("Spawn does not match its entry or definitions.", out problem, out message);
                    last = s.tick;
                    spawns.Add(new SpawnEntry(s.tick, entries[s.entry], s.entry, (EnemyKind)s.kind));
                }

                nights.Add(new WavePlan(n.name ?? "", spawns, entries));
            }

            if (cp.checkpointNight < 1 || cp.checkpointNight > nights.Count) return Fail("Checkpoint night out of range.", out problem, out message);
            if (cp.lastPaidDay != Math.Max(1, cp.checkpointNight)) return Fail("Income transaction does not match the checkpoint Day.", out problem, out message);
            if (cp.gold < 0) return Fail("Negative gold.", out problem, out message);

            // Perks
            var perks = cp.perks ?? new string[0];
            var perkSet = new HashSet<string>();
            foreach (var p in perks)
                if (!PerkRules.InPool(profile.PerkPool, p) || !perkSet.Add(p)) return Fail($"Unknown or duplicate perk '{p}'.", out problem, out message);
            if (perks.Length > cp.checkpointNight - 1) return Fail("More perks than cleared nights.", out problem, out message);

            RewardOffer pending = null;
            if (cp.hasPendingOffer)
            {
                var o = cp.pendingOffer;
                if (o == null || o.clearedNight != cp.checkpointNight || o.perks == null || o.perks.Length == 0 || o.perks.Length > PerkRules.OfferSize)
                    return Fail("Malformed pending reward offer.", out problem, out message);
                var ids = new HashSet<string>();
                foreach (var p in o.perks)
                    if (!PerkRules.InPool(profile.PerkPool, p) || perkSet.Contains(p) || !ids.Add(p)) return Fail("Pending offer lists an invalid perk.", out problem, out message);
                if (!TryParseU(o.rngBefore, out var before) || !TryParseU(o.rngAfter, out var after) || after == 0)
                    return Fail("Pending offer RNG state invalid.", out problem, out message);
                pending = new RewardOffer { ClearedNight = o.clearedNight, OfferId = o.offerId, PerkIds = (string[])o.perks.Clone(), RngBefore = before, RngAfter = after };
            }

            var history = new List<RewardOffer>();
            foreach (var o in cp.offerHistory ?? new OfferData[0])
            {
                if (o == null || o.perks == null) return Fail("Malformed offer history.", out problem, out message);
                TryParseU(o.rngBefore, out var b);
                TryParseU(o.rngAfter, out var a);
                history.Add(new RewardOffer { ClearedNight = o.clearedNight, OfferId = o.offerId, PerkIds = (string[])o.perks.Clone(), RngBefore = b, RngAfter = a, ChosenPerk = string.IsNullOrEmpty(o.chosen) ? null : o.chosen });
            }

            var config = new RunConfig
            {
                Definitions = defs, Seed = seed, Terrain = map,
                Campaign = new CampaignConfig
                {
                    Label = cp.profile ?? "", Nights = nights, OffersRewards = true,
                    RulesVersion = profile.RulesVersion, CampaignId = profile.CampaignId, PerkPool = profile.PerkPool,
                    MapProfile = release ? cp.mapProfile : "", Difficulty = release ? cp.difficulty : "standard",
                },
            };
            var sim = new RunSimulation(config); // fresh object; the live run is untouched whatever happens below
            var error = sim.ApplyCheckpoint(cp, perks, pending, history, rngState);
            if (error != null) return Fail(error, out problem, out message);
            problem = CheckpointProblem.None;
            message = null;
            return sim;
        }
    }
}
