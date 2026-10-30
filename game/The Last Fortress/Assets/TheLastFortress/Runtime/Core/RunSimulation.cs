using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TheLastFortress
{
    /// <summary>Stable IDs: append only.</summary>
    public enum RunState
    {
        Day,
        Night,
        Victory,
        Defeat,
        Reward,
    }

    public sealed class RunConfig
    {
        public PrototypeDefinitions Definitions = PrototypeDefinitions.CreateDefault();
        public WavePlan Wave = WavePlan.CreateMainWave();

        /// <summary>Indestructible, unwalkable terrain (test-only maps). Ignored when <see cref="Terrain"/> is set.</summary>
        public IReadOnlyList<GridPos> ImpassableCells;

        /// <summary>MVP terrain (generated, fallback or flat authored). Its non-land cells are impassable and unbuildable.</summary>
        public TerrainMap Terrain;

        /// <summary>Stress fixture: Keep and Walls ignore combat damage; killed enemies respawn. Never set for the main wave.</summary>
        public bool StressMode;

        /// <summary>
        /// Stress fixture only (plan 019): a hero of this rules version on the fixture board, so performance is measured with
        /// the hero, its motor and its attacks in play. Null: no hero, as before. Ignored outside stress mode.
        /// </summary>
        public string StressHeroRules;

        /// <summary>Optional multi-night MVP run. Null keeps the single-wave prototype; invalid together with stress.</summary>
        public CampaignConfig Campaign;

        /// <summary>Run seed (invariant unsigned decimal in UI). Seeds the reward stream; 005 also derives terrain from it.</summary>
        public uint Seed;

        public static RunConfig CreateMain() => new RunConfig();

        public static RunConfig CreateStress() => new RunConfig { Wave = WavePlan.CreateStressWave(), StressMode = true };
    }

    /// <summary>Run summary counters. Preview/debug actions never count.</summary>
    public sealed class RunStats
    {
        public int NightsCleared;
        public int EnemiesDefeated;
        public int BuildingsPurchased;

        /// <summary>Gold received after the starting budget: daily income plus Recycling refunds (sale refunds excluded).</summary>
        public int GoldEarned;

        /// <summary>Enemies killed by the hero (also counted in <see cref="EnemiesDefeated"/>).</summary>
        public int HeroKills;

        /// <summary>Unique Farm-House / Wall-Trap pairs that were ever active this run.</summary>
        public readonly HashSet<string> SynergiesEver = new HashSet<string>();
    }

    /// <summary>
    /// What one night cost and achieved, counted in the authoritative kill/damage/destruction paths. Reset when a Night
    /// starts and only written during that Night, so after it ends the values stay frozen through Reward, perks and Day
    /// repairs. Transient by design: a resumed save replays the night from its Day checkpoint and recounts it once.
    /// </summary>
    public sealed class NightRecap
    {
        public int Night;
        public int EnemiesDefeated;

        /// <summary>HP actually removed from the Keep (capped at its HP before each hit; no overkill, no invulnerable hits).</summary>
        public int KeepDamage;

        /// <summary>Non-Keep buildings destroyed in combat (sales, scripted removal and resets never count).</summary>
        public int BuildingsLost;

        /// <summary>Enemies the hero killed this night (also in <see cref="EnemiesDefeated"/>).</summary>
        public int HeroKills;

        /// <summary>Spec 012: gold earned by calling this night early, paid at the next dawn (0 when not called).</summary>
        public int EarlyCallGold;

        /// <summary>Spec 015: half the income of tier III Farms (Granary) destroyed this night, paid at the next dawn.</summary>
        public int GranaryGold;
    }

    /// <summary>
    /// Authoritative run: placement, routing, fixed-step combat, phases, restart; optionally a multi-night campaign with
    /// income, rewards and perks. Step order: due spawns; movement and Trap entry; Tower attacks/burn applications; due burn
    /// ticks; enemy attacks with immediate non-recursive reflection; dead cleanup; defeat before a cleared Night.
    /// </summary>
    public sealed class RunSimulation
    {
        public const long StepNanoseconds = 50_000_000;

        readonly RunConfig config;
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly Dictionary<int, Enemy> enemiesById = new Dictionary<int, Enemy>();
        readonly List<Building> towers = new List<Building>();
        readonly Dictionary<int, Building> buildingsById = new Dictionary<int, Building>();
        readonly List<SimEvent> events = new List<SimEvent>();
        readonly List<string> diagnostics = new List<string>();
        readonly HashSet<GridPos> previewScratch = new HashSet<GridPos>();
        readonly int[] activePerEntry = new int[8];
        readonly List<string> ownedPerks = new List<string>();
        readonly List<RewardOffer> offerHistory = new List<RewardOffer>();
        readonly Dictionary<int, int> buffedTowers = new Dictionary<int, int>(); // tower ID -> tier of the Watchtower buffing it
        readonly List<Building> buildingScratch = new List<Building>();
        readonly HashSet<int> provokedArchers = new HashSet<int>();
        int buffKey = -1;
        Hero hero;

        /// <summary>Bombers light at the first solid building beside them (<see cref="ReleaseCampaign.BombersLightOnContact"/>).</summary>
        bool bombersLightOnContact;

        /// <summary>Spec 016 (release-rules-4 campaigns): raid time in steps; 0 = no raids.</summary>
        int raidTicks;
        long heroDayAccumulatorNs;

        GridState grid;
        PlacementService placement;
        RunRandom rewardRng;
        int nextSpawnIndex;
        int nextBuildingId;
        int nextEnemyId;
        long accumulatorNs;

        public RunSimulation(RunConfig config = null)
        {
            this.config = config ?? RunConfig.CreateMain();
            if (this.config.Campaign != null && this.config.StressMode)
                throw new ArgumentException("A campaign run cannot also be the stress fixture.");
            if (this.config.Campaign != null && (this.config.Campaign.Nights == null || this.config.Campaign.Nights.Count == 0))
                throw new ArgumentException("A campaign needs at least one night.");
            Planner = new RoutePlanner();
            Restart();
        }

        public RunConfig Config => config;
        public PrototypeDefinitions Definitions => config.Definitions;
        public WavePlan Wave => config.Campaign != null ? config.Campaign.Nights[NightNumber - 1] : config.Wave;
        public bool StressMode => config.StressMode;
        public CampaignConfig Campaign => config.Campaign;
        public bool IsCampaign => config.Campaign != null;
        public uint Seed => config.Seed;

        /// <summary>Current Day/Night number (Day n precedes Night n). Always 1 in the prototype.</summary>
        public int NightNumber { get; private set; } = 1;

        public int TotalNights => config.Campaign?.TotalNights ?? 1;

        /// <summary>Last Day whose income has been paid (1 = none yet; Day 1 never pays).</summary>
        public int LastPaidDay { get; private set; }

        /// <summary>Breakdown of the most recent income payment.</summary>
        public IncomeBreakdown LastIncome { get; private set; }

        public GridState Grid => grid;
        public RoutePlanner Planner { get; }
        public RunState State { get; private set; }
        public bool Paused { get; private set; }

        /// <summary>Night elapsed time in fixed steps.</summary>
        public long Tick { get; private set; }

        public double NightElapsedSeconds => Tick * PrototypeDefinitions.StepSeconds;
        public int Gold => placement.Gold;
        public Building Keep => grid.Keep;
        public IReadOnlyList<Enemy> Enemies => enemies;
        public IReadOnlyList<Building> Towers => towers;
        public int AliveEnemyCount => enemies.Count;
        public int ScheduledRemaining => Wave.Spawns.Count - nextSpawnIndex;
        public int SpawnedCount => nextSpawnIndex;
        public IReadOnlyList<string> Diagnostics => diagnostics;
        public int ResetCount { get; private set; }
        public bool IsTerminal => State == RunState.Victory || State == RunState.Defeat;
        public RunStats Stats { get; private set; }

        /// <summary>The current or most recently finished night's recap; null before the first Night of this run.</summary>
        public NightRecap LastNight { get; private set; }
        public IReadOnlyList<string> OwnedPerks => ownedPerks;
        public IReadOnlyList<RewardOffer> OfferHistory => offerHistory;

        /// <summary>The frozen offer while in Reward (null otherwise).</summary>
        public RewardOffer CurrentOffer { get; private set; }

        /// <summary>Offer frozen before a reload (checkpoint ledger); reused instead of drawing when this Night is cleared.</summary>
        public RewardOffer PendingOffer { get; private set; }

        public uint RewardRngState => rewardRng.State;
        public int NextBuildingId => nextBuildingId;
        public int NextEnemyId => nextEnemyId;

        /// <summary>Fraction of a step accumulated since the last fixed step, for render interpolation.</summary>
        public double InterpolationAlpha => (double)accumulatorNs / StepNanoseconds;

        /// <summary>The player hero (release-rules-2 runs only; null otherwise). Authoritative; views only present it.</summary>
        public Hero Hero => hero;

        public bool HasHero => hero != null;

        /// <summary>Interpolation fraction for the hero: its own Day clock during Day, the Night clock otherwise.</summary>
        public double HeroInterpolationAlpha => State == RunState.Day ? (double)heroDayAccumulatorNs / StepNanoseconds : InterpolationAlpha;

        public bool HasPerk(string id) => ownedPerks.Contains(id);

        /// <summary>Perk IDs this run can be offered: the campaign's pool (MVP: the ten MVP perks).</summary>
        public IReadOnlyList<string> PerkPool => config.Campaign?.PerkPool ?? PerkRules.AllIds;

        /// <summary>Restores the exact initial state: Day 1, starting gold, Keep only, no perks/offers, fresh reward stream.</summary>
        public void Restart()
        {
            enemies.Clear();
            enemiesById.Clear();
            towers.Clear();
            buildingsById.Clear();
            events.Clear();
            diagnostics.Clear();
            ownedPerks.Clear();
            offerHistory.Clear();
            provokedArchers.Clear();
            buffKey = -1;
            heroDayAccumulatorNs = 0;
            CurrentOffer = null;
            PendingOffer = null;
            Planner.ClearCache();
            grid = new GridState(config.Terrain != null ? config.Terrain.ImpassableCells() : config.ImpassableCells);
            placement = new PlacementService(grid, Definitions, Definitions.StartingGold) { Terrain = config.Terrain };
            rewardRng = RunRandom.ForRewards(config.Seed);
            Stats = new RunStats();
            LastNight = null;
            nextSpawnIndex = 0;
            earlyCallTick = -1;
            spawnWaitTicks = 0;
            nextBuildingId = 1;
            nextEnemyId = 1;
            accumulatorNs = 0;
            Tick = 0;
            Paused = false;
            NightNumber = 1;
            LastPaidDay = 1;
            LastIncome = default;
            State = RunState.Day;
            var keep = new Building(nextBuildingId++, Definitions.Keep, GridState.KeepCell, 0);
            grid.Install(keep);
            buildingsById[keep.Id] = keep;
            bombersLightOnContact = config.Campaign != null && ReleaseCampaign.BombersLightOnContact(config.Campaign.RulesVersion);
            raidTicks = config.Campaign != null && config.Campaign.RulesVersion == ReleaseCampaign.RulesV4 ? PrototypeDefinitions.ToTicks(ReleaseCampaign.RaidSeconds) : 0;
            var heroRules = config.Campaign != null && config.Campaign.HasHero ? config.Campaign.RulesVersion
                : config.StressMode && ReleaseCampaign.HasHero(config.StressHeroRules) ? config.StressHeroRules : null;
            hero = heroRules != null ? new Hero { MoveUnits = ReleaseCampaign.HeroMoveUnits(heroRules), Profile = HeroProfile.For(heroRules) } : null;
            Planner.TurnLate = hero != null && hero.Profile.TurnLateRoutes;
            Planner.ClearCache();
            ResetHero();
            ResetCount++;
            events.Add(new SimEvent(SimEventType.RunReset, 0));
        }

        // ---------- Day: placement ----------

        public PlacementRejection CheckPlacement(BuildingKind kind, GridPos cell) =>
            State != RunState.Day ? PlacementRejection.NotDay : placement.Check(kind, cell);

        public PlacementRejection TryPlace(BuildingKind kind, GridPos cell, out Building building) => TryPlace(kind, cell, 0, out building);

        /// <summary>Places with a cosmetic facing (Tower/House/Farm only; ignored for other kinds).</summary>
        public PlacementRejection TryPlace(BuildingKind kind, GridPos cell, int orientation, out Building building)
        {
            building = null;
            if (State != RunState.Day) return PlacementRejection.NotDay;
            var result = placement.TryPlace(kind, cell, nextBuildingId, out building);
            if (result != PlacementRejection.None) return result;
            nextBuildingId++;
            building.MaxHp = building.Hp = EffectiveMaxHp(building.Definition);
            if (IsRotatable(kind)) building.Orientation = orientation & 3;
            Register(building);
            Stats.BuildingsPurchased++;
            events.Add(new SimEvent(SimEventType.BuildingPlaced, Tick, building.Id, cell: cell));
            RecordSynergies();
            StepHeroAside();
            return result;
        }

        public SellRejection TrySell(Building building)
        {
            if (State != RunState.Day) return SellRejection.NotDay;
            var result = placement.TrySell(building, out var refund);
            if (result != SellRejection.None) return result;
            Unregister(building);
            events.Add(new SimEvent(SimEventType.BuildingSold, Tick, building.Id, refund, building.Cell));
            return result;
        }

        /// <summary>Current price for a purchasable kind (after ThriftyConstruction), or -1.</summary>
        public int PriceFor(BuildingKind kind) => placement.PriceFor(kind);

        /// <summary>Exact quote shown before repairing: HP restored and gold charged (zero when nothing can be repaired).</summary>
        public void RepairQuote(Building building, out int restored, out int cost)
        {
            restored = cost = 0;
            if (building == null || !building.Definition.HasHp || !building.IsAlive) return;
            EconomyRules.RepairQuote(building.MaxHp - building.Hp, Gold, out restored, out cost);
        }

        public RepairRejection TryRepair(Building building)
        {
            if (State != RunState.Day) return RepairRejection.NotDay;
            if (building != null && GetBuilding(building.Id) != building) return RepairRejection.NotRepairable;
            var result = placement.TryRepair(building, out var restored, out _);
            if (result != RepairRejection.None) return result;
            events.Add(new SimEvent(SimEventType.BuildingRepaired, Tick, building.Id, restored, building.Cell));
            return result;
        }

        /// <summary>Cosmetic: rotates a Tower/House/Farm 90 degrees clockwise during Day at no cost. Never changes cells.</summary>
        public bool TryRotate(Building building)
        {
            if (State != RunState.Day || building == null || GetBuilding(building.Id) != building) return false;
            if (!IsRotatable(building.Kind)) return false;
            building.Orientation = (building.Orientation + 1) & 3;
            events.Add(new SimEvent(SimEventType.BuildingRotated, Tick, building.Id, building.Orientation, building.Cell));
            return true;
        }

        /// <summary>
        /// Spec 015: why <paramref name="building"/> cannot be upgraded one tier now (None when it can). Phase first, then the
        /// building, the tier and the gold.
        /// </summary>
        public UpgradeRejection CheckUpgrade(Building building)
        {
            if (building == null) return UpgradeRejection.NothingSelected;
            if (GetBuilding(building.Id) != building || !building.IsAlive || building.Definition.Tiers == null) return UpgradeRejection.NotUpgradable;
            if (building.Tier >= BuildingUpgrades.MaxTier) return UpgradeRejection.MaxTier;
            if (State != RunState.Day || StressMode) return UpgradeRejection.NotDay;
            return Gold < placement.UpgradePriceFor(building) ? UpgradeRejection.InsufficientGold : UpgradeRejection.None;
        }

        /// <summary>Price of the next tier for this building (after ThriftyConstruction), or -1 when it has none.</summary>
        public int UpgradePrice(Building building) => placement.UpgradePriceFor(building);

        /// <summary>
        /// Spec 015: one tier up, atomically (one charge, one tier). The building keeps its share of HP,
        /// <c>ceil(hp x max' / max)</c>; footprint, routes, targets and cooldowns never change.
        /// </summary>
        public UpgradeRejection TryUpgrade(Building building)
        {
            var result = CheckUpgrade(building);
            if (result != UpgradeRejection.None) return result;
            var price = placement.UpgradePriceFor(building);
            if (!placement.TrySpend(price)) return UpgradeRejection.InsufficientGold;
            building.TotalPaid += price;
            building.Tier++;
            if (building.Definition.HasHp)
            {
                var max = EffectiveMaxHp(building);
                if (max != building.MaxHp) building.Hp = (int)(((long)building.Hp * max + building.MaxHp - 1) / building.MaxHp);
                building.MaxHp = max;
            }

            buffKey = -1; // a Watchtower's reach and bonus follow its tier
            events.Add(new SimEvent(SimEventType.BuildingUpgraded, Tick, building.Id, building.Tier, building.Cell));
            return result;
        }

        public static bool IsRotatable(BuildingKind kind) =>
            kind == BuildingKind.Tower || kind == BuildingKind.House || kind == BuildingKind.Farm ||
            kind == BuildingKind.Cannon || kind == BuildingKind.Watchtower || kind == BuildingKind.RepairStation;

        /// <summary>Income the next Day would pay with the current fortress and perks (same calculation as the payment).</summary>
        public IncomeBreakdown IncomeForecast() =>
            EconomyRules.DailyIncome(grid, Definitions, FarmBonusPercent(HasPerk(PerkRules.EfficientFarming)), HouseLinkBonus(HasPerk(PerkRules.MarketSquare)));

        /// <summary>
        /// Read-only preview of the next payment if <paramref name="perkId"/> were owned (Reward cards). Never installs the
        /// perk, draws rewards, pays gold or touches the fortress; equals <see cref="IncomeForecast"/> for perks that do not
        /// change income.
        /// </summary>
        public IncomeBreakdown IncomeForecastWith(string perkId) =>
            EconomyRules.DailyIncome(grid, Definitions, FarmBonusPercent(HasPerk(PerkRules.EfficientFarming) || perkId == PerkRules.EfficientFarming),
                HouseLinkBonus(HasPerk(PerkRules.MarketSquare) || perkId == PerkRules.MarketSquare));

        /// <summary>Effective daily income of one House/Farm with current perks (same rule as the payment).</summary>
        public int BuildingIncome(Building b) =>
            EconomyRules.BuildingIncome(grid, b, FarmBonusPercent(HasPerk(PerkRules.EfficientFarming)), HouseLinkBonus(HasPerk(PerkRules.MarketSquare)));

        static int FarmBonusPercent(bool efficientFarming) => efficientFarming ? PerkRules.EfficientFarmingPercent : 0;

        static int HouseLinkBonus(bool marketSquare) => marketSquare ? PerkRules.MarketSquareBonus : 0;

        /// <summary>Applies placements in order. Normal fixtures pay costs; stress fixtures bypass costs only.</summary>
        public List<string> ApplyLayout(LayoutFixture layout, bool bypassCost)
        {
            var failures = new List<string>();
            foreach (var p in layout.Placements)
            {
                if (!bypassCost)
                {
                    var r = TryPlace(p.Kind, p.Cell, out _);
                    if (r != PlacementRejection.None) failures.Add($"{p}: {r}");
                    continue;
                }

                var check = CheckPlacement(p.Kind, p.Cell);
                if (check != PlacementRejection.None && check != PlacementRejection.InsufficientGold)
                {
                    failures.Add($"{p}: {check}");
                    continue;
                }

                var b = new Building(nextBuildingId++, Definitions.For(p.Kind), p.Cell, 0);
                grid.Install(b);
                Register(b);
                events.Add(new SimEvent(SimEventType.BuildingPlaced, Tick, b.Id, cell: p.Cell));
                StepHeroAside();
            }

            return failures;
        }

        public Building GetBuilding(int id) => buildingsById.TryGetValue(id, out var b) ? b : null;
        public Enemy GetEnemy(int id) => enemiesById.TryGetValue(id, out var e) ? e : null;

        // ---------- Effective stats (shared definitions + tier + run perks; definitions are never mutated) ----------

        /// <summary>Tier I (ghosts, cards): the definition's maximum HP with perks.</summary>
        public int EffectiveMaxHp(BuildingDefinition def) => EffectiveMaxHp(def, 1);

        public int EffectiveMaxHp(Building b) => EffectiveMaxHp(b.Definition, b.Tier);

        /// <summary>Definition, then tier (+% of the definition, floor), then perks.</summary>
        public int EffectiveMaxHp(BuildingDefinition def, int tier)
        {
            if (!def.HasHp) return 0;
            var hp = def.MaxHp * (100 + BuildingUpgrades.For(def, tier).HpPercent) / 100;
            if (def.Kind == BuildingKind.Wall && HasPerk(PerkRules.ReinforcedTimber))
                return hp * (100 + PerkRules.ReinforcedTimberPercent) / 100;
            if (def.Kind == BuildingKind.Keep && HasPerk(PerkRules.FortressHeart)) return hp + PerkRules.FortressHeartHp;
            return hp;
        }

        static BuildingTier TierOf(Building b) => BuildingUpgrades.For(b.Definition, b.Tier);

        /// <summary>Spec 016 Stockade (tier III Mine) [P]: its raider's melee hits are halved.</summary>
        public const int StockadeReductionPercent = 50;

        /// <summary>
        /// Range of an Archer Tower or Cannon: base + tier, x1.1 with ExpandedVision (Archer Towers only), then +the buffing
        /// Watchtower's bonus once (floating point).
        /// </summary>
        public double TowerRange(Building tower)
        {
            var range = TowerRange(tower.Definition, TierOf(tower).RangeBonus);
            var watchtower = BuffingWatchtowerTier(tower);
            if (watchtower > 0) range += WatchtowerRangeBonus(watchtower);
            return range;
        }

        /// <summary>Range without position-dependent bonuses (cards, ghosts before placement).</summary>
        public double TowerRange(BuildingDefinition tower) => TowerRange(tower, 0);

        double TowerRange(BuildingDefinition tower, double tierBonus) =>
            (tower.Range + tierBonus) * (tower.Kind == BuildingKind.Tower && HasPerk(PerkRules.ExpandedVision) ? PerkRules.ExpandedVisionFactor : 1.0);

        /// <summary>Attack interval in whole steps: the tier's interval when it has one, else the definition's.</summary>
        public static int AttackTicks(Building tower)
        {
            var tier = TierOf(tower);
            return PrototypeDefinitions.ToTicks(tier.IntervalSeconds > 0 ? tier.IntervalSeconds : tower.Definition.AttackIntervalSeconds);
        }

        public static double AttackIntervalSeconds(Building tower) => AttackTicks(tower) * PrototypeDefinitions.StepSeconds;

        /// <summary>Range bonus a Watchtower of this tier gives (tier I: the definition's).</summary>
        public double WatchtowerRangeBonus(int tier)
        {
            var t = BuildingUpgrades.For(Definitions.Watchtower, tier);
            return t.WatchRangeBonus > 0 ? t.WatchRangeBonus : Definitions.Watchtower.RangeBonus;
        }

        /// <summary>Spotter damage bonus (percent) a Watchtower of this tier gives to the towers it buffs.</summary>
        public int SpotterPercent(int tier) => tier < 1 ? 0 : BuildingUpgrades.For(Definitions.Watchtower, tier).SpotterPercent;

        /// <summary>
        /// Whether a living Watchtower currently boosts this Archer Tower/Cannon (edge-adjacent, or within Chebyshev 2 with
        /// SignalFires or at tier III, 3 with both). Non-stacking: the highest-tier Watchtower in reach applies. Cached per
        /// solid-occupancy revision, so construction, sale, destruction and upgrades recompute it.
        /// </summary>
        public bool IsWatchtowerBuffed(Building tower) => BuffingWatchtowerTier(tower) > 0;

        /// <summary>Tier of the Watchtower buffing this tower (0 when none).</summary>
        public int BuffingWatchtowerTier(Building tower)
        {
            if (Definitions.Watchtower == null || !tower.Definition.CanAttack) return 0;
            var key = grid.Revision * 2 + (HasPerk(PerkRules.SignalFires) ? 1 : 0);
            if (key != buffKey)
            {
                buffKey = key;
                RefreshWatchtowerBuffs();
            }

            return buffedTowers.TryGetValue(tower.Id, out var tier) ? tier : 0;
        }

        /// <summary>Attackers a Watchtower at <paramref name="anchor"/> of this tier would cover (placement preview, selection).</summary>
        public List<Building> TowersBuffedBy(GridPos anchor, int tier = 1)
        {
            var list = new List<Building>();
            if (Definitions.Watchtower == null) return list;
            var ghost = new Building(-1, Definitions.Watchtower, anchor, 0) { Tier = tier };
            foreach (var t in towers)
                if (t.IsAlive && WatchtowerReaches(ghost, t)) list.Add(t);
            return list;
        }

        /// <summary>Chebyshev reach of this Watchtower (0 = edge-adjacent only): 2 with SignalFires or at tier III, 3 with both.</summary>
        public int WatchtowerReach(Building watchtower)
        {
            var reach = TierOf(watchtower).WatchReach;
            if (!HasPerk(PerkRules.SignalFires)) return reach;
            return reach > 0 ? reach + 1 : PerkRules.SignalFiresReach;
        }

        bool WatchtowerReaches(Building watchtower, Building tower)
        {
            var reach = WatchtowerReach(watchtower);
            return reach > 0 ? tower.ChebyshevTo(watchtower) <= reach : tower.SharesEdgeWith(watchtower);
        }

        void RefreshWatchtowerBuffs()
        {
            buffedTowers.Clear();
            foreach (var w in grid.AllBuildings())
            {
                if (w.Kind != BuildingKind.Watchtower || !w.IsAlive) continue;
                foreach (var t in towers)
                    if (t.IsAlive && WatchtowerReaches(w, t) && (!buffedTowers.TryGetValue(t.Id, out var best) || w.Tier > best))
                        buffedTowers[t.Id] = w.Tier;
            }
        }

        /// <summary>LastStand condition: Keep HP strictly below 30% of its current maximum.</summary>
        public bool LastStandActive => HasPerk(PerkRules.LastStand) && Keep.Hp * 100 < Keep.MaxHp * PerkRules.LastStandThresholdPercent;

        /// <summary>A tower's attack damage at its tier before percentages: definition + tier% (floor).</summary>
        public static int BaseDamage(Building tower) => tower.Definition.AttackDamage * (100 + TierOf(tower).DamagePercent) / 100;

        /// <summary>Direct damage without target-dependent bonuses: tier base, then perk and Spotter percentages add, floor once.</summary>
        public int TowerDamage(Building tower) => BaseDamage(tower) * (100 + DamagePercent(tower, null)) / 100;

        /// <summary>Damage without target-dependent bonuses. Archer Tower perks never apply to the Cannon.</summary>
        public int TowerDamage(BuildingDefinition tower)
        {
            if (tower.Kind != BuildingKind.Tower) return tower.AttackDamage;
            var percent = 0;
            if (HasPerk(PerkRules.Sharpshooters)) percent += PerkRules.SharpshootersPercent;
            if (LastStandActive) percent += PerkRules.LastStandPercent;
            return tower.AttackDamage * (100 + percent) / 100;
        }

        /// <summary>Damage of one shot against this enemy: adds KillZone (Archer Tower) or HeavyShot (Cannon) to the sum, floors once.</summary>
        public int DamageAgainst(Building tower, Enemy target) => BaseDamage(tower) * (100 + DamagePercent(tower, target)) / 100;

        /// <summary>
        /// The percentages that add against a tower's tier base: Sharpshooters, Last Stand and Kill Zone (Archer Towers), Heavy
        /// Shot (Cannons), and the buffing Watchtower's Spotter (both). Target-dependent ones only with a target.
        /// </summary>
        int DamagePercent(Building tower, Enemy target)
        {
            var percent = SpotterPercent(BuffingWatchtowerTier(tower));
            if (tower.Kind == BuildingKind.Tower)
            {
                if (HasPerk(PerkRules.Sharpshooters)) percent += PerkRules.SharpshootersPercent;
                if (LastStandActive) percent += PerkRules.LastStandPercent;
                if (target != null && HasPerk(PerkRules.KillZone) && (grid.HasTrap(target.Cell) || grid.HasAdjacentTrap(target.Cell))) percent += PerkRules.KillZonePercent;
            }
            else if (target != null && tower.Kind == BuildingKind.Cannon && HasPerk(PerkRules.HeavyShot) && (target.Kind == EnemyKind.Brute || target.Definition.IsBoss))
                percent += PerkRules.HeavyShotPercent;

            return percent;
        }

        /// <summary>Cannon splash radius: base + tier, +0.5 with Shrapnel.</summary>
        public double SplashRadius(Building cannon) =>
            cannon.Definition.SplashRadius + TierOf(cannon).SplashBonus + (HasPerk(PerkRules.Shrapnel) ? PerkRules.ShrapnelRadius : 0);

        /// <summary>Spike Trap damage: the tier's (else the definition's), +4 per edge-adjacent Spike Trap with SpikedCorridor (max +8).</summary>
        public int TrapDamage(Building trap)
        {
            var tier = TierOf(trap);
            var damage = tier.TrapDamage > 0 ? tier.TrapDamage : trap.Definition.TrapDamage;
            if (!HasPerk(PerkRules.SpikedCorridor)) return damage;
            var bonus = 0;
            foreach (var d in GridPos.CardinalOrder)
                if (grid.HasTrap(trap.Cell + d)) bonus += PerkRules.SpikedCorridorPerTrap;
            return damage + Math.Min(bonus, PerkRules.SpikedCorridorMax);
        }

        /// <summary>A positive hit after a percentage reduction: floor, at least 1.</summary>
        static int Reduce(int damage, int percent) => damage <= 0 ? damage : Math.Max(1, damage * (100 - percent) / 100);

        /// <summary>Gatehouse: a Wall sharing an edge with a living Archer Tower or Cannon.</summary>
        bool GatehouseProtects(Building target)
        {
            if (target.Kind != BuildingKind.Wall || !HasPerk(PerkRules.Gatehouse)) return false;
            foreach (var d in GridPos.CardinalOrder)
            {
                var n = grid.GetOccupant(target.Cell + d);
                if (n != null && n.IsAlive && (n.Kind == BuildingKind.Tower || n.Kind == BuildingKind.Cannon)) return true;
            }

            return false;
        }

        /// <summary>The enemy's Wall multiplier against this target: only on Walls, and never on a stone (tier III) Wall.</summary>
        static int WallMultiplier(int multiplier, Building target) =>
            target.Kind == BuildingKind.Wall && !TierOf(target).Stone ? multiplier : 1;

        /// <summary>
        /// One enemy hit on a building: base x Wall multiplier, +the Commander aura (other non-boss enemies within its radius),
        /// then reductions in order Gatehouse, HardenedRoofs (ranged hits on economy/support buildings).
        /// </summary>
        public int EnemyHitDamage(Enemy e, Building target)
        {
            var damage = e.Definition.AttackDamage * WallMultiplier(e.Definition.WallDamageMultiplier, target);
            var aura = CommanderAuraPercent(e);
            if (aura > 0) damage = damage * (100 + aura) / 100;
            if (GatehouseProtects(target)) damage = Reduce(damage, PerkRules.GatehouseReductionPercent);
            if (e.RaidBuildingId == target.Id && TierOf(target).Stockade) damage = Reduce(damage, StockadeReductionPercent);
            if (e.Definition.IsRanged && HasPerk(PerkRules.HardenedRoofs) &&
                (target.Kind == BuildingKind.House || target.Kind == BuildingKind.Farm || target.Kind == BuildingKind.Mine || target.Kind == BuildingKind.Watchtower || target.Kind == BuildingKind.RepairStation))
                damage = Reduce(damage, PerkRules.HardenedRoofsReductionPercent);
            return damage;
        }

        /// <summary>Siege Commander aura bonus for this attacker now (0 for bosses or when none is in range). Non-stacking.</summary>
        public int CommanderAuraPercent(Enemy e)
        {
            if (e.Definition.IsBoss) return 0;
            foreach (var c in enemies)
            {
                if (!c.Alive || c.Kind != EnemyKind.SiegeCommander || c == e) continue;
                var dx = c.X - e.X;
                var dz = c.Z - e.Z;
                var r = c.Definition.AuraRadius;
                if (dx * dx + dz * dz <= r * r + 1e-9) return c.Definition.AuraDamagePercent;
            }

            return 0;
        }

        /// <summary>Bomber blast on one building: primary x Wall multiplier or flat splash, then Gatehouse and BlastShields.</summary>
        public int BlastDamage(Enemy bomber, Building target, bool primary)
        {
            var def = bomber.Definition;
            var damage = primary ? def.BlastDamage * WallMultiplier(def.WallDamageMultiplier, target) : def.BlastSplashDamage;
            if (GatehouseProtects(target)) damage = Reduce(damage, PerkRules.GatehouseReductionPercent);
            if (HasPerk(PerkRules.BlastShields)) damage = Reduce(damage, PerkRules.BlastShieldsReductionPercent);
            return damage;
        }

        /// <summary>Reflected damage per received melee hit: 3 with any adjacent trap, +3 with BarbedDefenses, +the tier's spikes.</summary>
        public int WallReflection(Building wall)
        {
            if (wall.Kind != BuildingKind.Wall) return 0;
            var reflect = grid.HasAdjacentTrap(wall.Cell) ? EconomyRules.WallTrapReflect : 0;
            if (HasPerk(PerkRules.BarbedDefenses)) reflect += PerkRules.BarbedReflect;
            reflect += TierOf(wall).Reflect; // spikes (spec 015)
            return reflect;
        }

        // ---------- Routing ----------

        public Route GetLiveRoute(GridPos start) => Planner.GetLiveRoute(grid, start);

        /// <summary>Candidate route with a construction ghost; never mutates occupancy, revision, gold or the live cache.</summary>
        public Route PreviewRoute(GridPos start, GridPos ghostCell)
        {
            previewScratch.Clear();
            previewScratch.Add(ghostCell);
            return Planner.Plan(grid, start, previewScratch);
        }

        /// <summary>Candidate route with a complete ghost footprint. Walkable traps never change the route.</summary>
        public Route PreviewRoute(GridPos start, BuildingKind kind, GridPos anchor)
        {
            previewScratch.Clear();
            if (kind != BuildingKind.SpikeTrap)
                foreach (var c in (Definitions.For(kind)?.Footprint(anchor) ?? BuildingDefinition.FootprintCells(kind, anchor))) previewScratch.Add(c);
            return Planner.Plan(grid, start, previewScratch);
        }

        /// <summary>Errors that prevent starting Night (invalid definitions, an entry with no route).</summary>
        public List<string> ValidateMap()
        {
            var errors = Definitions.Validate();
            foreach (var s in Wave.Spawns)
                if (Definitions.EnemyFor(s.Kind) == null)
                {
                    errors.Add($"Wave uses {s.Kind}, which these definitions do not define.");
                    break;
                }

            foreach (var entry in Wave.EntryCells)
            {
                var route = Planner.Plan(grid, entry, null);
                if (!route.Found)
                    errors.Add($"Invalid map: no open or breach route from spawn {entry} to any Keep neighbor of {GridState.KeepCell}.");
            }

            return errors;
        }

        // ---------- Night ----------

        public bool StartNight()
        {
            if (State != RunState.Day) return false;
            var errors = ValidateMap();
            if (errors.Count > 0)
            {
                diagnostics.AddRange(errors);
                return false;
            }

            Tick = 0;
            accumulatorNs = 0;
            if (hero != null && hero.Profile.ContactPhase)
            {
                // release-rules-4: no attack crosses into the Night; the motor keeps its velocity so a moving start replays.
                hero.ClearAttack();
                hero.SwingReadyTick = 0;
            }

            LastNight = new NightRecap { Night = NightNumber }; // before the time-zero spawn
            earlyCallTick = -1;
            spawnWaitTicks = 0;
            SetState(RunState.Night);
            ReleaseDueSpawns(); // the time-zero spawn appears on entering Night, before any movement step
            return true;
        }

        public bool SetPaused(bool paused)
        {
            if (State != RunState.Night) return false;
            Paused = paused;
            return true;
        }

        public bool TogglePause() => SetPaused(!Paused);

        /// <summary>Consumes real elapsed seconds in whole fixed steps. Returns the number of steps run.</summary>
        public int Advance(double seconds) => Advance(seconds, HeroCommand.None);

        /// <summary>As <see cref="Advance(double)"/>, applying the same hero command to every step it runs.</summary>
        public int Advance(double seconds, HeroCommand command)
        {
            if (State != RunState.Night || Paused || !(seconds > 0)) return 0;
            accumulatorNs += (long)Math.Round(seconds * 1e9);
            var steps = 0;
            while (accumulatorNs >= StepNanoseconds && State == RunState.Night)
            {
                Step(command);
                accumulatorNs -= StepNanoseconds;
                steps++;
            }

            if (State != RunState.Night) accumulatorNs = 0;
            return steps;
        }

        /// <summary>One fixed 0.05 s step. Ignored outside an unpaused Night.</summary>
        public void Step() => Step(HeroCommand.None);

        /// <summary>One fixed step with this step's hero command (specs/003 2.3 step order).</summary>
        public void Step(HeroCommand command)
        {
            if (State != RunState.Night || Paused) return;
            ReleaseDueSpawns();
            MoveEnemies();
            HeroMovePhase(command); // 2b
            TowerAttacks();
            HeroAttackPhase(command); // 3b
            BurnTicks();
            EnemyAttacks();
            RemoveDeadEnemies();
            HeroDeathPhase(); // 6
            if (Keep.Hp > 0) RefillMaintainedEntries(); // stress keeps 100 active between steps

            if (Keep.Hp <= 0) Finish(RunState.Defeat);
            else if (!StressMode && nextSpawnIndex >= Wave.Spawns.Count && enemies.Count == 0) CompleteNight();
            Tick++;
        }

        /// <summary>A cleared Night: the final night wins; earlier campaign nights go to Reward (or straight to Day).</summary>
        void CompleteNight()
        {
            if (IsCampaign) Stats.NightsCleared = Math.Max(Stats.NightsCleared, NightNumber);
            if (!IsCampaign || NightNumber >= TotalNights)
            {
                Finish(RunState.Victory);
                return;
            }

            if (Campaign.OffersRewards) EnterReward();
            else EnterDay(NightNumber + 1);
        }

        void EnterReward()
        {
            accumulatorNs = 0;
            Paused = false;
            SetState(RunState.Reward);
            GetOrCreateOffer();
        }

        /// <summary>Freezes one offer per cleared night; repeated calls return the same offer and consume no RNG.</summary>
        public RewardOffer GetOrCreateOffer()
        {
            if (State != RunState.Reward) return null;
            if (CurrentOffer != null && CurrentOffer.ClearedNight == NightNumber) return CurrentOffer;
            if (PendingOffer != null && PendingOffer.ClearedNight == NightNumber)
            {
                // Replay after reload: reuse the frozen offer and advance the stream to its stored after-state once.
                CurrentOffer = PendingOffer;
                PendingOffer = null;
                rewardRng = new RunRandom(CurrentOffer.RngAfter);
                events.Add(new SimEvent(SimEventType.OfferCreated, Tick, NightNumber));
                return CurrentOffer;
            }

            var before = rewardRng.State;
            var ids = PerkRules.DrawOffer(rewardRng, ownedPerks, OwnsAliveKind, PerkPool);
            CurrentOffer = new RewardOffer
            {
                ClearedNight = NightNumber,
                OfferId = $"run{config.Seed}-night{NightNumber}",
                PerkIds = ids,
                RngBefore = before,
                RngAfter = rewardRng.State,
            };
            events.Add(new SimEvent(SimEventType.OfferCreated, Tick, NightNumber));
            return CurrentOffer;
        }

        bool OwnsAliveKind(BuildingKind kind)
        {
            foreach (var b in grid.AllBuildings())
                if (b.Kind == kind && b.IsAlive) return true;
            return false;
        }

        /// <summary>
        /// Commits one perk from the frozen offer, enters the next Day and pays its income once. Stale offer IDs, unknown
        /// perks or a second submission are rejected without mutation.
        /// </summary>
        public bool ChoosePerk(string offerId, string perkId)
        {
            if (State != RunState.Reward || CurrentOffer == null || CurrentOffer.OfferId != offerId) return false;
            if (Array.IndexOf(CurrentOffer.PerkIds, perkId) < 0 || HasPerk(perkId)) return false;
            CurrentOffer.ChosenPerk = perkId;
            offerHistory.Add(CurrentOffer);
            CurrentOffer = null;
            AcquirePerk(perkId);
            EnterDay(NightNumber + 1);
            return true;
        }

        /// <summary>DEBUG ONLY (no-perk baseline): leaves Reward without a perk. Never offered as a normal choice.</summary>
        public bool DebugSkipReward()
        {
            if (State != RunState.Reward) return false;
            if (CurrentOffer != null) offerHistory.Add(CurrentOffer);
            CurrentOffer = null;
            EnterDay(NightNumber + 1);
            return true;
        }

        /// <summary>
        /// Installs a validated checkpoint into this freshly constructed Day-1 simulation (see <see cref="CheckpointCodec"/>).
        /// Returns an error instead of installing anything inconsistent; the caller discards this object on error.
        /// </summary>
        internal string ApplyCheckpoint(RunCheckpoint cp, string[] perks, RewardOffer pending, List<RewardOffer> history, uint rngState)
        {
            ownedPerks.AddRange(perks); // before buildings: HP legality uses the effective maximum
            grid = new GridState(config.Terrain != null ? config.Terrain.ImpassableCells() : config.ImpassableCells);
            buffKey = -1;
            buildingsById.Clear();
            towers.Clear();
            Planner.ClearCache();
            if (cp.nextBuildingId < 2 || cp.nextEnemyId < 1) return "Entity ID counters invalid.";
            var keepSeen = false;
            foreach (var bd in cp.buildings ?? new BuildingData[0])
            {
                if (bd == null || bd.kind < 0 || bd.kind > (int)BuildingKind.Mine) return "Unknown building type.";
                var kind = (BuildingKind)bd.kind;
                var def = Definitions.For(kind);
                if (def == null) return $"{kind} is not defined.";
                if (bd.id < 1 || bd.id >= cp.nextBuildingId || buildingsById.ContainsKey(bd.id)) return $"Building ID {bd.id} invalid or duplicated.";
                if (bd.price < 0 || bd.orientation < 0 || bd.orientation > 3 || (!IsRotatable(kind) && bd.orientation != 0)) return $"Building {bd.id} has invalid price/orientation.";
                var cell = new GridPos(bd.x, bd.z);
                if (kind == BuildingKind.Keep)
                {
                    if (keepSeen || cell != GridState.KeepCell || bd.price != 0) return "Keep record invalid.";
                    keepSeen = true;
                }

                foreach (var c in def.Footprint(cell))
                {
                    if (!GridCoordinates.IsInBounds(c) || grid.IsImpassableTerrain(c)) return $"Building {bd.id} lies outside buildable land.";
                    if (kind != BuildingKind.Keep && GridCoordinates.IsReservedRing(c)) return $"Building {bd.id} lies on the reserved ring.";
                    if (grid.IsSolid(c) || grid.HasTrap(c)) return $"Building {bd.id} overlaps another structure.";
                    if (kind == BuildingKind.Mine && (config.Terrain == null || !config.Terrain.IsVein(c))) return $"Mine {bd.id} is not on a gold vein.";
                }

                // Spec 015: a missing tier is tier I and a missing total the purchase price (saves from before upgrades).
                var tier = bd.tier == 0 ? 1 : bd.tier;
                var paid = bd.paid == 0 ? bd.price : bd.paid;
                if (tier < 1 || tier > BuildingUpgrades.MaxTier || (tier > 1 && def.Tiers == null)) return $"Building {bd.id} has invalid tier {bd.tier}.";
                if (paid < bd.price || (tier == 1 && paid != bd.price)) return $"Building {bd.id} has invalid total paid {bd.paid}.";
                var b = new Building(bd.id, def, cell, bd.price) { Orientation = bd.orientation, Tier = tier, TotalPaid = paid };
                b.MaxHp = EffectiveMaxHp(b);
                if (def.HasHp && (bd.hp < 1 || bd.hp > b.MaxHp)) return $"Building {bd.id} HP {bd.hp} outside 1..{b.MaxHp}.";
                b.Hp = def.HasHp ? bd.hp : 0;
                grid.Install(b);
                Register(b);
            }

            if (!keepSeen) return "Keep missing.";
            placement = new PlacementService(grid, Definitions, cp.gold) { Thrifty = HasPerk(PerkRules.ThriftyConstruction), Terrain = config.Terrain };
            NightNumber = cp.checkpointNight;
            LastPaidDay = cp.lastPaidDay;
            nextBuildingId = cp.nextBuildingId;
            nextEnemyId = cp.nextEnemyId;
            rewardRng = new RunRandom(rngState);
            PendingOffer = pending;
            offerHistory.AddRange(history);
            var s = cp.stats ?? new StatsData();
            Stats = new RunStats
            {
                NightsCleared = s.nightsCleared, EnemiesDefeated = s.enemiesDefeated, BuildingsPurchased = s.buildingsPurchased, GoldEarned = s.goldEarned,
                HeroKills = s.heroKills,
            };
            foreach (var key in s.synergiesEver ?? new string[0]) Stats.SynergiesEver.Add(key);
            ResetHero(); // the hero is never stored except its Night-start position
            if (hero != null && cp.heroAtNight && !RestoreHeroPosition(cp.heroX, cp.heroZ)) return "Saved hero position is not walkable.";
            if (hero != null && cp.heroAtNight && hero.Profile.ContactPhase && !RestoreHeroVelocity(cp.heroVx, cp.heroVz)) return "Saved hero velocity is out of range.";
            events.Clear();
            events.Add(new SimEvent(SimEventType.RunReset, 0));
            return null;
        }

        /// <summary>Adds a perk and applies one-time current-HP grants. Later recomputation never grants HP again.</summary>
        void AcquirePerk(string id)
        {
            ownedPerks.Add(id);
            switch (id)
            {
                case PerkRules.ReinforcedTimber:
                case PerkRules.FortressHeart:
                    foreach (var b in grid.AllBuildings())
                    {
                        var max = EffectiveMaxHp(b);
                        var delta = max - b.MaxHp;
                        if (delta <= 0) continue;
                        b.MaxHp = max;
                        b.Hp += delta;
                    }

                    break;
                case PerkRules.ThriftyConstruction:
                    placement.Thrifty = true;
                    break;
            }

            events.Add(new SimEvent(SimEventType.PerkAcquired, Tick, IndexInPool(id)));
        }

        int IndexInPool(string id)
        {
            var pool = PerkPool;
            for (var i = 0; i < pool.Count; i++)
                if (pool[i] == id) return i;
            return -1;
        }

        /// <summary>
        /// Enters Day <paramref name="day"/> once: resets per-wave spawning/cooldowns, keeps gold/HP/buildings, then pays
        /// that Day's income exactly once (guarded by <see cref="LastPaidDay"/>).
        /// </summary>
        void EnterDay(int day)
        {
            if ((State != RunState.Night && State != RunState.Reward) || day != NightNumber + 1) return;
            NightNumber = day;
            nextSpawnIndex = 0;
            accumulatorNs = 0;
            Tick = 0;
            Paused = false;
            foreach (var t in towers)
            {
                t.TargetEnemyId = -1;
                t.AttackDueTick = 0;
            }

            SetState(RunState.Day);
            ResetHero();
            DawnRepair();
            PayIncome(day);
            RecordSynergies();
        }

        /// <summary>
        /// Repair Stations at dawn (game rules section 7, Repair Station), in ascending station ID: each spends its budget on eligible buildings
        /// within reach, lowest HP fraction first (ties lowest ID). Runs once per Day entry, never on Day 1 or a reload.
        /// </summary>
        void DawnRepair()
        {
            var def = Definitions.RepairStation;
            if (def == null) return;
            var stations = new List<Building>();
            foreach (var b in grid.AllBuildings())
                if (b.Kind == BuildingKind.RepairStation && b.IsAlive) stations.Add(b);
            stations.Sort((a, b) => a.Id.CompareTo(b.Id));
            var engineers = HasPerk(PerkRules.FieldEngineers);
            foreach (var station in stations)
            {
                buildingScratch.Clear();
                var reach = RepairReach(station);
                var keepCare = engineers || TierOf(station).KeepCare;
                foreach (var b in grid.AllBuildings())
                {
                    if (b == station || !b.IsAlive || !b.Definition.HasHp || b.Hp >= b.MaxHp) continue;
                    if (b.Kind == BuildingKind.Keep && !keepCare) continue;
                    if (station.ChebyshevTo(b) <= reach) buildingScratch.Add(b);
                }

                buildingScratch.Sort((a, b) =>
                {
                    var c = ((long)a.Hp * b.MaxHp).CompareTo((long)b.Hp * a.MaxHp);
                    return c != 0 ? c : a.Id.CompareTo(b.Id);
                });
                var budget = RepairBudget(station);
                var total = 0;
                foreach (var b in buildingScratch)
                {
                    if (budget <= 0) break;
                    var amount = Math.Min(b.MaxHp - b.Hp, budget);
                    b.Hp += amount;
                    budget -= amount;
                    total += amount;
                    events.Add(new SimEvent(SimEventType.BuildingRepaired, Tick, b.Id, amount, b.Cell));
                }

                if (total > 0) events.Add(new SimEvent(SimEventType.DawnRepair, Tick, station.Id, total, station.Cell));
            }
        }

        /// <summary>
        /// A Repair Station's dawn budget: its tier's (else the definition's), +30 with FieldEngineers, and +30 more when the
        /// perk meets tier III Keep care (spec 015: the perk extends the trait).
        /// </summary>
        public int RepairBudget(Building station)
        {
            var tier = TierOf(station);
            var budget = tier.RepairBudget > 0 ? tier.RepairBudget : station.Definition.RepairBudget;
            if (!HasPerk(PerkRules.FieldEngineers)) return budget;
            return budget + PerkRules.FieldEngineersBudget * (tier.KeepCare ? 2 : 1);
        }

        /// <summary>A Repair Station's Chebyshev reach: its tier's, else the definition's.</summary>
        public static int RepairReach(Building station)
        {
            var tier = TierOf(station);
            return tier.RepairReach > 0 ? tier.RepairReach : station.Definition.RepairReach;
        }

        /// <summary>Whether this Repair Station's dawn repair reaches the Keep (FieldEngineers or tier III Keep care).</summary>
        public bool RepairsKeep(Building station) => HasPerk(PerkRules.FieldEngineers) || TierOf(station).KeepCare;

        void PayIncome(int day)
        {
            if (day <= LastPaidDay || day > TotalNights) return;
            LastPaidDay = day;
            var income = IncomeForecast();
            if (HasPerk(PerkRules.WarBonds) && LastNight != null && LastNight.Night == day - 1 && LastNight.KeepDamage == 0)
                income.Bonus = PerkRules.WarBondsGold;
            if (LastNight != null && LastNight.Night == day - 1)
            {
                income.EarlyCall = LastNight.EarlyCallGold;
                income.Granary = LastNight.GranaryGold;
            }
            LastIncome = income;
            placement.Earn(LastIncome.Total);
            Stats.GoldEarned += LastIncome.Total;
            events.Add(new SimEvent(SimEventType.IncomePaid, Tick, LastIncome.Total, day));
        }

        /// <summary>Currently active unique synergy pairs (Farm-House, Wall-Trap).</summary>
        public List<string> ActiveSynergies()
        {
            var list = new List<string>();
            foreach (var (farm, house) in EconomyRules.FarmHousePairs(grid)) list.Add($"F{farm.Id}-H{house.Id}");
            foreach (var b in grid.AllBuildings())
            {
                if (b.Kind != BuildingKind.Wall || !b.IsAlive) continue;
                foreach (var d in GridPos.CardinalOrder)
                {
                    var trap = grid.GetTrap(b.Cell + d);
                    if (trap != null) list.Add($"W{b.Id}-T{trap.Id}");
                }
            }

            return list;
        }

        void RecordSynergies()
        {
            if (!IsCampaign) return;
            foreach (var key in ActiveSynergies()) Stats.SynergiesEver.Add(key);
        }

        /// <summary>
        /// Stress-fixture event: removes a structure outside combat (bypasses invulnerability, no refund).
        /// Reports whether any living enemy was actively attacking it immediately before removal.
        /// </summary>
        public bool ScriptedRemove(Building building, out bool wasUnderActiveAttack)
        {
            wasUnderActiveAttack = false;
            if (building == null || !building.IsDestructible || !building.IsAlive) return false;
            foreach (var e in enemies)
                if (e.Alive && e.TargetBuildingId == building.Id) wasUnderActiveAttack = true;
            if (!grid.Release(building)) return false;
            Unregister(building);
            events.Add(new SimEvent(SimEventType.BuildingRemovedScripted, Tick, building.Id, cell: building.Cell));
            return true;
        }

        public int CountAttackers(Building building)
        {
            var n = 0;
            foreach (var e in enemies)
                if (e.Alive && e.TargetBuildingId == building.Id) n++;
            return n;
        }

        public void DrainEvents(List<SimEvent> into)
        {
            into.AddRange(events);
            events.Clear();
        }

        // ---------- early call (spec 012) ----------

        /// <summary>Spawns not yet due when the player calls come this many times sooner, counted from the call.</summary>
        public const int EarlyCallSpeedup = 4;

        /// <summary>Bonus gold per whole second by which the night's last spawn moves earlier [P].</summary>
        public const int EarlyCallGoldPerSecond = 1;

        /// <summary>Tick of tonight's early call, or -1.</summary>
        long earlyCallTick = -1;

        public bool EarlyCalled => earlyCallTick >= 0;

        /// <summary>When a stored spawn is due: unchanged before the call, then compressed toward the call tick.</summary>
        long DueTick(SpawnEntry s)
        {
            if (earlyCallTick < 0 || s.Tick <= earlyCallTick) return s.Tick;
            return earlyCallTick + (s.Tick - earlyCallTick + EarlyCallSpeedup - 1) / EarlyCallSpeedup;
        }

        /// <summary>release-rules-4 campaign Night with spawns still pending and no call yet (paused or not).</summary>
        public bool CanCallEarly => State == RunState.Night && !StressMode && IsCampaign && config.Campaign.RulesVersion == ReleaseCampaign.RulesV4
                                    && earlyCallTick < 0 && nextSpawnIndex < Wave.Spawns.Count;

        /// <summary>Gold a call now would pay at the next dawn: whole seconds by which the last spawn would move earlier.</summary>
        public int EarlyCallGoldNow
        {
            get
            {
                if (!CanCallEarly) return 0;
                var last = Wave.Spawns[Wave.Spawns.Count - 1].Tick;
                if (last <= Tick) return 0;
                var compressed = Tick + (last - Tick + EarlyCallSpeedup - 1) / EarlyCallSpeedup;
                return (int)((last - compressed) * PrototypeDefinitions.StepSeconds) * EarlyCallGoldPerSecond;
            }
        }

        /// <summary>Spec 012: calls the rest of tonight's spawns early; the bonus is paid at the next dawn. Once per night.</summary>
        public bool CallEarly()
        {
            if (!CanCallEarly) return false;
            var gold = EarlyCallGoldNow;
            earlyCallTick = Tick;
            LastNight.EarlyCallGold = gold;
            events.Add(new SimEvent(SimEventType.EarlyCalled, Tick, -1, gold)); // compressed spawns release in the next steps
            return true;
        }

        // ---------- step phases ----------

        void ReleaseDueSpawns()
        {
            // FIFO: a due spawn waits while the living cap is reached; pending spawns still block completion.
            while (nextSpawnIndex < Wave.Spawns.Count && DueTick(Wave.Spawns[nextSpawnIndex]) <= Tick
                   && enemies.Count < MvpCampaign.MaxLiveEnemies)
            {
                var next = Wave.Spawns[nextSpawnIndex];
                // Spec 014: a body waits while its entry point is taken (FIFO), at most UnitBodies.WaitTicks. The stress
                // fixture does not wait: it is a fixed 100-enemy load for performance checks.
                if (!StressMode && EntryTaken(next) && spawnWaitTicks++ < UnitBodies.WaitTicks) break;
                spawnWaitTicks = 0;
                nextSpawnIndex++;
                Spawn(next.Cell, next.EntryIndex, next.Kind);
            }

            RefillMaintainedEntries();
        }

        /// <summary>Stress fixture only: replace killed enemies at their original entry once the initial wave is out.</summary>
        void RefillMaintainedEntries()
        {
            if (Wave.MaintainActivePerEntry <= 0 || nextSpawnIndex < Wave.Spawns.Count) return;
            if (Wave.MaintainMix != null)
            {
                // Mixed fixture: each (entry, archetype) deficit respawns that same archetype at that entry.
                foreach (var (kind, count) in Wave.MaintainMix)
                    for (var i = 0; i < Wave.EntryCells.Count; i++)
                    {
                        var alive = 0;
                        foreach (var e in enemies)
                            if (e.EntryIndex == i && e.Kind == kind) alive++;
                        for (var n = alive; n < count; n++) Spawn(Wave.EntryCells[i], i, kind);
                    }

                return;
            }

            Array.Clear(activePerEntry, 0, activePerEntry.Length);
            foreach (var e in enemies) activePerEntry[e.EntryIndex]++;
            for (var i = 0; i < Wave.EntryCells.Count; i++)
                for (var n = activePerEntry[i]; n < Wave.MaintainActivePerEntry; n++)
                    Spawn(Wave.EntryCells[i], i, EnemyKind.Grunt);
        }

        int spawnWaitTicks;

        bool EntryTaken(SpawnEntry s)
        {
            var r = Definitions.EnemyFor(s.Kind)?.BodyRadius ?? 0;
            if (r <= 0) return false;
            GridCoordinates.TileCenter(s.Cell, out var x, out var z);
            foreach (var o in enemies)
            {
                if (!o.Alive || o.Definition.BodyRadius <= 0) continue;
                double dx = o.X - x, dz = o.Z - z, sum = r + o.Definition.BodyRadius;
                if (dx * dx + dz * dz < sum * sum - 1e-9) return true;
            }

            return false;
        }

        void Spawn(GridPos cell, int entryIndex, EnemyKind kind)
        {
            var def = Definitions.EnemyFor(kind) ?? throw new InvalidOperationException($"No definition for {kind}.");
            var e = new Enemy(nextEnemyId++, entryIndex, def, cell);
            enemies.Add(e);
            enemiesById[e.Id] = e;
            events.Add(new SimEvent(SimEventType.EnemySpawned, Tick, e.Id, (int)kind, cell));
        }

        void MoveEnemies()
        {
            foreach (var e in enemies)
            {
                e.PrevX = e.X;
                e.PrevZ = e.Z;
                if (!e.Alive) continue;
                if (e.Pursuing || e.Returning)
                {
                    PursueStep(e);
                    continue;
                }

                if (e.Moving)
                {
                    AdvanceSegment(e);
                    // Leftover distance continues through the center this same step (after honoring route changes);
                    // with no leftover (Grunt: exactly 20 steps per tile) the decision waits for the next step, as in 002.
                    if (!e.Moving && e.Alive && e.CarryUnits > 0) DecideAtCenter(e, false);
                    continue;
                }

                DecideAtCenter(e, true);
            }
        }

        /// <summary>
        /// At a cell center: honor route invalidation, then attack (only on a fresh step) or start the next segment.
        /// </summary>
        void DecideAtCenter(Enemy e, bool freshStep)
        {
            if (e.FuseLit)
            {
                e.CarryUnits = 0; // a lit Bomber is committed: it never moves or retargets again
                return;
            }

            var targetDead = e.TargetBuildingId >= 0 && !(GetBuilding(e.TargetBuildingId)?.IsAlive ?? false);
            if (targetDead) e.TargetBuildingId = -1;
            if (e.Route == null || e.Route.Revision != grid.Revision || targetDead)
            {
                e.Route = Planner.GetLiveRoute(grid, e.Cell);
                e.RouteIndex = 0;
            }

            if (!e.Route.Found)
            {
                e.CarryUnits = 0;
                if (e.TargetBuildingId >= 0) e.TargetBuildingId = -1;
                var message = $"Enemy {e.Id} at {e.Cell} has no route to the Keep.";
                if (!diagnostics.Contains(message))
                {
                    diagnostics.Add(message);
                    events.Add(new SimEvent(SimEventType.RouteMissing, Tick, e.Id, cell: e.Cell));
                }

                return;
            }

            if (e.Definition.IsRanged && RangedHold(e, freshStep)) return;
            if (hero != null && hero.Profile.PursuitTicks > 0 ? StartsPursuit(e, freshStep) : MeleeHoldsHero(e, freshStep)) return;
            if (e.Definition.IsBomber && bombersLightOnContact && LightOnContact(e, freshStep)) return;
            if (raidTicks > 0 && RaidOnContact(e, freshStep)) return;

            if (e.RouteIndex >= e.Route.ReachableEndIndex)
            {
                e.CarryUnits = 0; // residual movement is discarded at the final attack cell
                if (!freshStep) return; // the attack is acquired on the next simulation step
                var attackCell = e.Route.IsBreach ? e.Route.Blocker : GridState.KeepCell;
                var target = grid.GetOccupant(attackCell);
                if (target == null || !target.IsAlive || !e.Cell.IsCardinalNeighbor(attackCell))
                    throw new InvalidOperationException($"Enemy {e.Id} at {e.Cell} has invalid attack cell {attackCell}.");
                if (e.TargetBuildingId != target.Id)
                {
                    e.TargetBuildingId = target.Id;
                    e.AttackDueTick = Tick + e.AttackTicks;
                    if (e.Definition.IsBomber)
                    {
                        e.FuseLit = true;
                        e.DetonateTick = Tick + e.FuseTicks;
                        events.Add(new SimEvent(SimEventType.FuseLit, Tick, e.Id, target.Id, e.Cell));
                    }
                }

                return;
            }

            e.TargetBuildingId = -1;
            e.NextCell = e.Route.Cells[e.RouteIndex + 1];
            e.Moving = true;
            e.MoveTicks = 0;
            e.ProgressUnits = e.CarryUnits; // distance left over from the previous segment is not lost
            e.CarryUnits = 0;
            if (freshStep) AdvanceSegment(e);
            else SetSegmentPosition(e);
        }

        /// <summary>
        /// Bomber at a cell centre with a living solid building on a cardinal neighbour (release-rules-3): it stops and lights
        /// its fuse there (on a fresh step, like any attack). Target: the Keep, then a Wall (the Breacher's ×2), then lowest ID.
        /// </summary>
        bool LightOnContact(Enemy e, bool freshStep)
        {
            Building best = null;
            foreach (var d in GridPos.CardinalOrder)
            {
                var b = grid.GetOccupant(e.Cell + d);
                if (b == null || !b.IsAlive || !b.Definition.HasHp) continue;
                if (best == null || ContactRank(b) < ContactRank(best) || (ContactRank(b) == ContactRank(best) && b.Id < best.Id)) best = b;
            }

            if (best == null) return false;
            e.CarryUnits = 0;
            if (!freshStep) return true; // the fuse is lit on the next simulation step
            e.TargetBuildingId = best.Id;
            e.AttackDueTick = Tick + e.AttackTicks;
            e.FuseLit = true;
            e.DetonateTick = Tick + e.FuseTicks;
            events.Add(new SimEvent(SimEventType.FuseLit, Tick, e.Id, best.Id, e.Cell));
            return true;
        }

        static int ContactRank(Building b) => b.Kind == BuildingKind.Keep ? 0 : b.Kind == BuildingKind.Wall ? 1 : 2;

        /// <summary>Spec 016: whether enemies raid in this run (release-rules-4 campaigns).</summary>
        public bool RaidsEnabled => raidTicks > 0;

        /// <summary>
        /// Spec 016 Day preview: living Houses, Farms and Mines with a cardinal neighbour on one of tonight's current routes,
        /// so a passing raider would stop at them (lowest ID first per cell, as the raid picks).
        /// </summary>
        public List<Building> RaidExposed()
        {
            var list = new List<Building>();
            if (!RaidsEnabled || Wave == null) return list;
            var cells = new HashSet<GridPos>();
            foreach (var entry in Wave.EntryCells)
            {
                var route = GetLiveRoute(entry);
                if (route.Found) cells.UnionWith(route.Cells);
            }

            foreach (var b in grid.AllBuildings())
            {
                if (!b.IsAlive || !IsEconomy(b.Kind)) continue;
                var exposed = false;
                foreach (var c in b.Cells)
                foreach (var d in GridPos.CardinalOrder)
                    if (cells.Contains(c + d)) exposed = true;
                if (exposed) list.Add(b);
            }

            return list;
        }

        static bool IsEconomy(BuildingKind kind) => kind == BuildingKind.House || kind == BuildingKind.Farm || kind == BuildingKind.Mine;

        /// <summary>
        /// Spec 016 raid: a Grunt, Runner or Brute at a cell centre with a living House, Farm or Mine on a cardinal neighbour
        /// stops there and attacks it (lowest ID first; the first hit one interval later), once per night. It walks on when
        /// the target falls, the raid time runs out, or the hero draws it away (a chase or fighting back ends the raid).
        /// The route is never changed; the enemy only waits on it.
        /// </summary>
        bool RaidOnContact(Enemy e, bool freshStep)
        {
            if (e.RaidBuildingId >= 0)
            {
                var target = GetBuilding(e.RaidBuildingId);
                if (target != null && target.IsAlive && Tick < e.RaidUntilTick && e.TargetBuildingId == e.RaidBuildingId && !e.TargetsHero)
                {
                    e.CarryUnits = 0;
                    return true;
                }

                EndRaid(e);
                return false;
            }

            if (e.Raided || e.Definition.IsRanged || e.Definition.IsBomber || e.Definition.IsBoss) return false;
            Building best = null;
            foreach (var d in GridPos.CardinalOrder)
            {
                var b = grid.GetOccupant(e.Cell + d);
                if (b != null && b.IsAlive && IsEconomy(b.Kind) && (best == null || b.Id < best.Id)) best = b;
            }

            if (best == null) return false;
            e.CarryUnits = 0;
            if (!freshStep) return true; // the raid starts on the next simulation step, like any attack
            e.Raided = true;
            e.RaidBuildingId = best.Id;
            e.RaidUntilTick = Tick + raidTicks;
            e.TargetBuildingId = best.Id;
            e.AttackDueTick = Tick + e.AttackTicks;
            events.Add(new SimEvent(SimEventType.RaidStarted, Tick, e.Id, best.Id, e.Cell));
            return true;
        }

        void EndRaid(Enemy e)
        {
            if (e.RaidBuildingId < 0) return;
            events.Add(new SimEvent(SimEventType.RaidEnded, Tick, e.Id, e.RaidBuildingId, e.Cell));
            if (e.TargetBuildingId == e.RaidBuildingId) e.TargetBuildingId = -1;
            if (e.ResumeBuildingId == e.RaidBuildingId) e.ResumeBuildingId = -1;
            e.RaidBuildingId = -1;
        }

        /// <summary>
        /// Raider Archer at a cell centre of its own route (game rules section 8, Raider Archer): keeps a living target in range; otherwise picks
        /// one by class (economy, support, towers, Walls, Keep), then nearest footprint cell, then lowest ID. With a target
        /// it stays on this cell. A new target is taken on a fresh step with a full interval (cooldown reset on retarget).
        /// </summary>
        bool RangedHold(Enemy e, bool freshStep)
        {
            if (hero != null)
            {
                var heroInRange = hero.Alive && HeroRangedDistanceSq(e) <= RangeSq(e);
                if (provokedArchers.Contains(e.Id) && !heroInRange) provokedArchers.Remove(e.Id);
                if (e.TargetsHero)
                {
                    if (heroInRange)
                    {
                        e.CarryUnits = 0; // sticky
                        return true;
                    }

                    e.TargetsHero = false;
                }
                else if (provokedArchers.Contains(e.Id))
                {
                    // Provoked (specs/003 2.4): the hero hit this archer, so it takes the hero over any building.
                    e.CarryUnits = 0;
                    if (!freshStep) return true;
                    e.TargetBuildingId = -1;
                    e.TargetsHero = true;
                    e.AttackDueTick = Tick + e.AttackTicks;
                    return true;
                }
            }

            if (e.TargetBuildingId >= 0)
            {
                var current = GetBuilding(e.TargetBuildingId);
                if (current != null && current.IsAlive && RangedDistanceSq(e, current) <= RangeSq(e)) // sticky target
                {
                    e.CarryUnits = 0;
                    return true;
                }

                e.TargetBuildingId = -1;
            }

            var target = AcquireRangedTarget(e);
            var takeHero = HeroIsBetterRangedTarget(e, target);
            if (target == null && !takeHero) return false;
            e.CarryUnits = 0;
            if (!freshStep) return true; // as for melee: the target is taken on the next simulation step
            if (takeHero)
            {
                e.TargetsHero = true;
                e.AttackDueTick = Tick + e.AttackTicks;
                return true;
            }

            e.TargetBuildingId = target.Id;
            e.AttackDueTick = Tick + e.AttackTicks;
            return true;
        }

        static double RangeSq(Enemy e) => e.Definition.AttackRange * e.Definition.AttackRange + 1e-9;

        /// <summary>Squared distance from the enemy's cell centre to the nearest footprint cell centre of the building.</summary>
        static double RangedDistanceSq(Enemy e, Building b)
        {
            var best = double.MaxValue;
            foreach (var c in b.Cells)
            {
                double dx = c.X - e.Cell.X, dz = c.Z - e.Cell.Z;
                var d = dx * dx + dz * dz;
                if (d < best) best = d;
            }

            return best;
        }

        /// <summary>Raider Archer target class: lower is preferred.</summary>
        public static int RangedClass(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.House:
                case BuildingKind.Farm:
                case BuildingKind.Mine: return 0;
                case BuildingKind.Watchtower:
                case BuildingKind.RepairStation: return 1;
                case BuildingKind.Tower:
                case BuildingKind.Cannon: return 2;
                case BuildingKind.Wall: return 3;
                default: return 4; // Keep
            }
        }

        Building AcquireRangedTarget(Enemy e)
        {
            Building best = null;
            var bestClass = int.MaxValue;
            var bestDistance = double.MaxValue;
            var limit = RangeSq(e);
            foreach (var b in grid.AllBuildings())
            {
                if (!b.IsSolid || !b.IsAlive) continue;
                var d = RangedDistanceSq(e, b);
                if (d > limit) continue;
                var cls = RangedClass(b.Kind);
                var better = cls < bestClass ||
                             (cls == bestClass && (d < bestDistance - 1e-9 || (Math.Abs(d - bestDistance) <= 1e-9 && b.Id < best.Id)));
                if (!better) continue;
                best = b;
                bestClass = cls;
                bestDistance = d;
            }

            return best;
        }

        /// <summary>
        /// Fixed-point movement: speed * 60 units per step, 1200 units per tile; arrival keeps the remainder. With a body
        /// (spec 014) the step is taken only when it brings the body no closer to another body ahead than their radii.
        /// </summary>
        void AdvanceSegment(Enemy e)
        {
            var progress = e.ProgressUnits + MoveUnits(e);
            double laneX = e.LaneX, laneZ = e.LaneZ;
            if (e.Definition.BodyRadius > 0)
            {
                EaseLane(e, MoveUnits(e), ref laneX, ref laneZ);
                if (!BodyMayStep(e, progress, laneX, laneZ)) return;
            }

            e.LaneX = laneX;
            e.LaneZ = laneZ;
            e.MoveTicks++;
            e.ProgressUnits = progress;
            if (e.ProgressUnits >= EnemyDefinition.UnitsPerTile)
            {
                e.CarryUnits = e.ProgressUnits - EnemyDefinition.UnitsPerTile;
                e.ProgressUnits = 0;
                e.Cell = e.NextCell;
                e.Moving = false;
                e.MoveTicks = 0;
                e.RouteIndex++;
                GridCoordinates.TileCenter(e.Cell, out e.X, out e.Z);
                e.X += e.LaneX;
                e.Z += e.LaneZ;
                TriggerTrap(e);
                return;
            }

            SetSegmentPosition(e);
        }

        static void SetSegmentPosition(Enemy e)
        {
            GridCoordinates.TileCenter(e.Cell, out var fx, out var fz);
            GridCoordinates.TileCenter(e.NextCell, out var tx, out var tz);
            var t = (double)e.ProgressUnits / EnemyDefinition.UnitsPerTile;
            e.X = fx + (tx - fx) * t + e.LaneX;
            e.Z = fz + (tz - fz) * t + e.LaneZ;
        }

        /// <summary>Spec 014: the lane offset turns with the route at half the walking speed, so corners stay smooth.</summary>
        static void EaseLane(Enemy e, int units, ref double laneX, ref double laneZ)
        {
            double dx = e.NextCell.X - e.Cell.X, dz = e.NextCell.Z - e.Cell.Z; // cardinal unit step
            double tx = dz * e.Lane, tz = -dx * e.Lane; // to the right of travel
            var max = units * 0.5 / EnemyDefinition.UnitsPerTile;
            double ex = tx - laneX, ez = tz - laneZ;
            var len = Math.Sqrt(ex * ex + ez * ez);
            if (len <= max) { laneX = tx; laneZ = tz; }
            else { laneX += ex / len * max; laneZ += ez / len * max; }
        }

        /// <summary>
        /// Spec 014: whether a body may take this step. Blocked when the new position comes within the sum of radii of a body
        /// ahead (in front along the direction of travel) and closer than now; pursuing and returning enemies never block.
        /// After UnitBodies.WaitTicks blocked, a body not queued behind an engaged line walks through bodies for
        /// UnitBodies.PassUnits, so crossings and head-on meetings always resolve.
        /// </summary>
        bool BodyMayStep(Enemy e, int progress, double laneX, double laneZ)
        {
            if (e.PassUnits > 0)
            {
                e.PassUnits = Math.Max(0, e.PassUnits - MoveUnits(e));
                e.BlockedTicks = 0;
                return true;
            }

            GridCoordinates.TileCenter(e.Cell, out var fx, out var fz);
            GridCoordinates.TileCenter(e.NextCell, out var tx, out var tz);
            var t = Math.Min(1.0, (double)progress / EnemyDefinition.UnitsPerTile);
            double nx = fx + (tx - fx) * t + laneX, nz = fz + (tz - fz) * t + laneZ;
            double dirX = tx - fx, dirZ = tz - fz;
            Enemy blocker = null;
            foreach (var o in enemies)
            {
                if (o == e || !o.Alive || o.Definition.BodyRadius <= 0 || o.Pursuing || o.Returning) continue;
                var r = e.Definition.BodyRadius + o.Definition.BodyRadius;
                double ax = o.X - nx, az = o.Z - nz;
                var after = ax * ax + az * az;
                if (after >= r * r - 1e-9) continue;
                double cx = o.X - e.X, cz = o.Z - e.Z;
                if (cx * dirX + cz * dirZ <= 1e-9) continue; // not ahead
                if (after >= cx * cx + cz * cz - 1e-12) continue; // moving away or alongside
                blocker = o;
                break;
            }

            if (blocker == null)
            {
                e.BlockedTicks = 0;
                e.BlockerId = -1;
                return true;
            }

            e.BlockerId = blocker.Id;
            if (++e.BlockedTicks < UnitBodies.WaitTicks || QueuedBehindEngaged(blocker)) return false;
            e.BlockedTicks = 0;
            e.PassUnits = UnitBodies.PassUnits;
            return true;
        }

        /// <summary>
        /// Whether the chain of bodies waiting behind each other, starting at <paramref name="o"/>, ends at an enemy that is
        /// attacking (a building or the hero) or has lit its fuse. A circle of waiting bodies with no attacker is not engaged,
        /// so it resolves through the pass rule.
        /// </summary>
        bool QueuedBehindEngaged(Enemy o)
        {
            for (var i = 0; i < 16 && o != null && o.Alive; i++)
            {
                if (o.TargetBuildingId >= 0 || o.TargetsHero || o.FuseLit) return true;
                if (o.BlockerId < 0) return false;
                enemiesById.TryGetValue(o.BlockerId, out o);
            }

            return false;
        }

        /// <summary>Spike Trap: fixed damage once when an enemy completes entry into the trap's cell.</summary>
        void TriggerTrap(Enemy e)
        {
            var trap = grid.GetTrap(e.Cell);
            if (trap == null || !e.Alive) return;
            events.Add(new SimEvent(SimEventType.TrapTriggered, Tick, trap.Id, e.Id, e.Cell));
            DamageEnemy(e, TrapDamage(trap), trap.Id);
            var tier = TierOf(trap);
            ApplySlow(e, tier.SlowPercent, tier.SlowSeconds); // barbs (spec 015)
        }

        /// <summary>Applies damage and records the kill exactly once.</summary>
        void DamageEnemy(Enemy e, int amount, int sourceId)
        {
            if (!e.Alive) return;
            e.Hp = Math.Max(0, e.Hp - amount);
            if (e.Hp > 0) return;
            e.Alive = false;
            e.Burning = false;
            Stats.EnemiesDefeated++;
            if (LastNight != null) LastNight.EnemiesDefeated++;
            if (sourceId == HeroRules.SourceId)
            {
                Stats.HeroKills++;
                if (LastNight != null) LastNight.HeroKills++;
            }

            events.Add(new SimEvent(SimEventType.EnemyKilled, Tick, e.Id, sourceId, e.Cell, e.X, e.Z, e.X, e.Z));
        }

        /// <summary>Base-range check (specification 002); live targeting uses <see cref="TowerRange"/>.</summary>
        public static bool InRange(Building tower, double x, double z) => InRange(tower, x, z, tower.Definition.Range);

        public static bool InRange(Building tower, double x, double z, double range)
        {
            var dx = x - tower.CenterX;
            var dz = z - tower.CenterZ;
            return dx * dx + dz * dz <= range * range + 1e-9;
        }

        void TowerAttacks()
        {
            foreach (var t in towers)
            {
                if (!t.IsAlive) continue;
                var range = TowerRange(t);
                var ticks = AttackTicks(t);
                var target = t.TargetEnemyId >= 0 ? GetEnemy(t.TargetEnemyId) : null;
                if (target == null || !target.Alive || !InRange(t, target.X, target.Z, range))
                {
                    target = AcquireTarget(t, range);
                    t.TargetEnemyId = target?.Id ?? -1;
                    if (target != null) t.AttackDueTick = Tick + (HasPerk(PerkRules.Overwatch) ? (ticks + 1) / 2 : ticks);
                }

                if (target == null || Tick < t.AttackDueTick) continue;
                t.AttackDueTick += ticks;
                events.Add(new SimEvent(SimEventType.TowerShot, Tick, t.Id, target.Id, target.Cell, t.CenterX, t.CenterZ, target.X, target.Z));
                if (t.Kind == BuildingKind.Cannon)
                {
                    CannonSplash(t, target);
                    if (!target.Alive) t.TargetEnemyId = -1;
                    continue;
                }

                var second = SecondArrowTarget(t, range, target); // chosen before the first arrow lands
                ArrowHit(t, target, 100);
                if (!target.Alive) t.TargetEnemyId = -1;
                if (second == null) continue;
                events.Add(new SimEvent(SimEventType.TowerShot, Tick, t.Id, second.Id, second.Cell, t.CenterX, t.CenterZ, second.X, second.Z));
                ArrowHit(t, second, TierOf(t).SecondArrowPercent);
            }
        }

        /// <summary>One Archer Tower hit at this percentage of its damage (floor): a hit like any other, so FlamingArrows burns.</summary>
        void ArrowHit(Building tower, Enemy target, int percent)
        {
            DamageEnemy(target, DamageAgainst(tower, target) * percent / 100, tower.Id);
            if (target.Alive && HasPerk(PerkRules.FlamingArrows)) ApplyBurn(target);
        }

        /// <summary>
        /// Spec 015 second arrow (Archer Tower tier II+): another living enemy in range, nearest to the primary target's position,
        /// ties to the lowest ID; null without the trait or another enemy in range. Never changes the tower's held target.
        /// </summary>
        Enemy SecondArrowTarget(Building tower, double range, Enemy primary)
        {
            if (TierOf(tower).SecondArrowPercent <= 0) return null;
            Enemy best = null;
            var bestDistance = double.MaxValue;
            foreach (var e in enemies) // ascending spawn ID; the strict comparison keeps the lowest ID on ties
            {
                if (e == primary || !e.Alive || !InRange(tower, e.X, e.Z, range)) continue;
                double dx = e.X - primary.X, dz = e.Z - primary.Z, d = dx * dx + dz * dz;
                if (d >= bestDistance - 1e-9) continue;
                best = e;
                bestDistance = d;
            }

            return best;
        }

        /// <summary>
        /// Spec 015 slow: the stronger of the current (if still running) and the new percentage, until the later expiry; never
        /// stacks. Bosses take half (floor). Movement only: attack timing never changes.
        /// </summary>
        void ApplySlow(Enemy e, int percent, double seconds)
        {
            if (!e.Alive || percent <= 0) return;
            if (e.Definition.IsBoss) percent /= 2;
            var active = Tick < e.SlowUntilTick;
            e.SlowPercent = active ? Math.Max(e.SlowPercent, percent) : percent;
            e.SlowUntilTick = Math.Max(active ? e.SlowUntilTick : 0, Tick + PrototypeDefinitions.ToTicks(seconds));
        }

        /// <summary>Movement units this enemy walks this step: (100 - slow)% of its speed, floor, at least 1.</summary>
        public int MoveUnits(Enemy e) =>
            Tick < e.SlowUntilTick && e.SlowPercent > 0 ? Math.Max(1, e.MoveUnitsPerStep * (100 - e.SlowPercent) / 100) : e.MoveUnitsPerStep;

        /// <summary>Whether a slow currently holds this enemy back (presentation).</summary>
        public bool IsSlowed(Enemy e) => Tick < e.SlowUntilTick && e.SlowPercent > 0;

        /// <summary>
        /// FlamingArrows: a fresh burn ticks 20 and 40 and 60 steps after application; a refresh extends expiry to now+60
        /// but keeps the cadence; no concurrent stacks.
        /// </summary>
        void ApplyBurn(Enemy e)
        {
            if (!e.Burning)
            {
                e.Burning = true;
                e.BurnNextTick = Tick + PerkRules.BurnIntervalTicks;
            }

            e.BurnExpiryTick = Tick + PerkRules.BurnDurationTicks;
        }

        void BurnTicks()
        {
            foreach (var e in enemies)
            {
                if (!e.Alive || !e.Burning || Tick < e.BurnNextTick) continue;
                e.BurnNextTick += PerkRules.BurnIntervalTicks;
                if (e.BurnNextTick > e.BurnExpiryTick) e.Burning = false; // the tick at expiry is the last one
                events.Add(new SimEvent(SimEventType.BurnTick, Tick, -1, e.Id, e.Cell));
                DamageEnemy(e, PerkRules.BurnDamage, -1);
            }
        }

        /// <summary>
        /// Cannon shot (game rules section 7, Cannon): the primary target and every other living enemy within the splash radius of the
        /// primary's position take the shot's damage once, in ascending enemy ID.
        /// </summary>
        void CannonSplash(Building cannon, Enemy primary)
        {
            var r = SplashRadius(cannon);
            var tier = TierOf(cannon);
            double px = primary.X, pz = primary.Z;
            foreach (var e in enemies)
            {
                if (!e.Alive) continue;
                if (e != primary)
                {
                    var dx = e.X - px;
                    var dz = e.Z - pz;
                    if (dx * dx + dz * dz > r * r + 1e-9) continue;
                    events.Add(new SimEvent(SimEventType.SplashHit, Tick, cannon.Id, e.Id, e.Cell));
                }

                DamageEnemy(e, DamageAgainst(cannon, e), cannon.Id);
                ApplySlow(e, tier.SlowPercent, tier.SlowSeconds); // concussion (spec 015)
            }
        }

        /// <summary>
        /// Nearest enemy in range (ties: lowest ID). CounterBattery puts Raider Archers and Bombers first; FocusedFire (Archer
        /// Towers) prefers the lowest current HP, then nearest. Without those perks this is the 002 rule unchanged.
        /// </summary>
        Enemy AcquireTarget(Building tower, double range)
        {
            double cx = tower.CenterX, cz = tower.CenterZ;
            var counter = HasPerk(PerkRules.CounterBattery);
            var focused = tower.Kind == BuildingKind.Tower && HasPerk(PerkRules.FocusedFire);
            Enemy best = null;
            var bestClass = int.MaxValue;
            var bestHp = int.MaxValue;
            var bestDistance = double.MaxValue;
            foreach (var e in enemies) // ascending spawn ID; strict comparisons keep the lowest ID on ties
            {
                if (!e.Alive || !InRange(tower, e.X, e.Z, range)) continue;
                var cls = counter && (e.Kind == EnemyKind.Archer || e.Kind == EnemyKind.Bomber) ? 0 : 1;
                var dx = e.X - cx;
                var dz = e.Z - cz;
                var d = dx * dx + dz * dz;
                bool better;
                if (cls != bestClass) better = cls < bestClass;
                else if (focused && e.Hp != bestHp) better = e.Hp < bestHp;
                else better = d < bestDistance - 1e-9;
                if (!better) continue;
                best = e;
                bestClass = cls;
                bestHp = e.Hp;
                bestDistance = d;
            }

            return best;
        }

        void EnemyAttacks()
        {
            foreach (var e in enemies)
            {
                if (!e.Alive) continue;
                if (e.FuseLit)
                {
                    if (Tick >= e.DetonateTick) Detonate(e);
                    continue;
                }

                if (e.TargetsHero)
                {
                    HitHero(e);
                    continue;
                }

                if (e.TargetBuildingId < 0) continue;
                var target = GetBuilding(e.TargetBuildingId);
                if (target == null || !target.IsAlive)
                {
                    e.TargetBuildingId = -1;
                    continue;
                }

                if (Tick < e.AttackDueTick) continue;
                e.AttackDueTick += e.AttackTicks;
                var ranged = e.Definition.IsRanged;
                // Reflection is decided from the Wall's cell before the hit, so a lethal hit still reflects once. Melee only.
                var reflect = ranged ? 0 : WallReflection(target);
                var damage = EnemyHitDamage(e, target);
                if (ranged) events.Add(new SimEvent(SimEventType.RangedShot, Tick, e.Id, target.Id, target.Cell, e.X, e.Z, target.CenterX, target.CenterZ));
                Damage(target, damage, e.Id);
                if (reflect <= 0) continue;
                events.Add(new SimEvent(SimEventType.DamageReflected, Tick, target.Id, e.Id, e.Cell));
                DamageEnemy(e, reflect, target.Id); // non-recursive: reflected damage never reflects
            }
        }

        /// <summary>
        /// Bomber detonation (game rules section 8, Bomber): primary blast on its target if still alive, then splash on every other living
        /// building with HP within Chebyshev 1 of its cell, ascending ID. The Bomber dies without counting as defeated.
        /// </summary>
        void Detonate(Enemy e)
        {
            e.FuseLit = false;
            e.Alive = false;
            e.Burning = false;
            events.Add(new SimEvent(SimEventType.BomberDetonated, Tick, e.Id, e.TargetBuildingId, e.Cell, e.X, e.Z, e.X, e.Z));
            var target = GetBuilding(e.TargetBuildingId);
            buildingScratch.Clear();
            foreach (var b in grid.AllBuildings())
                if (b != target && b.IsAlive && b.Definition.HasHp && b.ChebyshevTo(e.Cell) <= 1) buildingScratch.Add(b);
            buildingScratch.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (target != null && target.IsAlive) Damage(target, BlastDamage(e, target, true), e.Id);
            foreach (var b in buildingScratch)
                if (b.IsAlive) Damage(b, BlastDamage(e, b, false), e.Id);
        }

        bool IsInvulnerable(Building b) => StressMode && (b.Kind == BuildingKind.Keep || b.Kind == BuildingKind.Wall);

        void Damage(Building target, int amount, int sourceId)
        {
            events.Add(new SimEvent(SimEventType.EnemyAttacked, Tick, sourceId, target.Id, target.Cell));
            if (IsInvulnerable(target)) return;
            var before = target.Hp;
            target.Hp = Math.Max(0, Math.Min(target.MaxHp, target.Hp - amount));
            if (target.Kind == BuildingKind.Keep && LastNight != null) LastNight.KeepDamage += Math.Max(0, before - target.Hp);
            events.Add(new SimEvent(SimEventType.BuildingDamaged, Tick, sourceId, target.Id, target.Cell));
            if (target.Hp > 0) return;
            if (target.Kind == BuildingKind.Keep) return; // defeat is evaluated at the end of the step
            // Granary (spec 015): half the income it would have paid, valued now, while it still stands on the grid.
            if (TierOf(target).Granary && LastNight != null && !target.Removed)
                LastNight.GranaryGold += EconomyRules.IncomeOf(grid, target, FarmBonusPercent(HasPerk(PerkRules.EfficientFarming))) / 2;
            if (!grid.Release(target)) return; // occupancy is released exactly once, immediately
            Unregister(target);
            if (LastNight != null) LastNight.BuildingsLost++;
            var half = target.Definition.FootprintSize / 2.0;
            events.Add(new SimEvent(SimEventType.BuildingDestroyed, Tick, target.Id, sourceId, target.Cell,
                target.Cell.X + half, target.Cell.Z + half, target.Cell.X + half, target.Cell.Z + half));
            if (!HasPerk(PerkRules.Recycling)) return;
            var refund = target.TotalPaid / 4;
            placement.Earn(refund);
            Stats.GoldEarned += refund;
        }

        void RemoveDeadEnemies()
        {
            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i].Alive) continue;
                enemiesById.Remove(enemies[i].Id);
                enemies.RemoveAt(i);
            }
        }

        // ---------- Hero (specs/003; release-rules-2 runs only) ----------

        /// <summary>
        /// Moves the hero during Day on its own fixed-step clock (specs/003 2.1): only hero movement advances, nothing is
        /// fought and no other state changes. Returns the number of steps run.
        /// </summary>
        public int AdvanceDay(double seconds, HeroCommand command)
        {
            if (hero == null || State != RunState.Day || !(seconds > 0)) return 0;
            heroDayAccumulatorNs += (long)Math.Round(seconds * 1e9);
            var steps = 0;
            while (heroDayAccumulatorNs >= StepNanoseconds)
            {
                MoveHero(command);
                if (hero.Profile.DayAttacks) ContactAttackPhase(command); // release-rules-4: a swing by Day hits nothing but is real
                heroDayTick++;
                heroDayAccumulatorNs -= StepNanoseconds;
                steps++;
            }

            return steps;
        }

        /// <summary>
        /// Spawn rule (specs/003 2.1): the walkable cell without a solid building nearest <paramref name="from"/> by
        /// Manhattan distance; ties by direction from it (N, E, S, W by dominant axis), then (z, x) ascending.
        /// </summary>
        public GridPos HeroSpawnCell(GridPos from)
        {
            var best = from;
            var bestKey = (int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
            for (var z = 0; z < GridCoordinates.Size; z++)
            for (var x = 0; x < GridCoordinates.Size; x++)
            {
                var c = new GridPos(x, z);
                if (IsHeroBlocked(c)) continue;
                var key = (c.ManhattanDistance(from), DirectionRank(c.X - from.X, c.Z - from.Z), z, x);
                if (key.CompareTo(bestKey) >= 0) continue;
                best = c;
                bestKey = key;
            }

            return best;
        }

        static int DirectionRank(int dx, int dz)
        {
            if (Math.Abs(dz) >= Math.Abs(dx)) return dz > 0 ? 0 : 2; // N, S
            return dx > 0 ? 1 : 3; // E, W
        }

        /// <summary>Cells the hero's body may not overlap: off the board, non-walkable terrain, solid buildings.</summary>
        bool IsHeroBlocked(GridPos c) => !GridCoordinates.IsInBounds(c) || grid.IsImpassableTerrain(c) || grid.IsSolid(c);

        /// <summary>Whether a hero body centred at these units would overlap a blocked cell (touching is allowed).</summary>
        public bool HeroOverlapsBlocked(int ux, int uz)
        {
            if (hero != null && hero.Profile.ContactPhase) return HeroOverlapsDrawn(ux, uz);
            const int u = HeroRules.UnitsPerTile;
            var r = hero != null ? hero.Profile.RadiusUnits : HeroRules.RadiusUnits;
            var x0 = FloorDiv(ux - r, u);
            var x1 = FloorDiv(ux + r, u);
            var z0 = FloorDiv(uz - r, u);
            var z1 = FloorDiv(uz + r, u);
            for (var z = z0; z <= z1; z++)
            for (var x = x0; x <= x1; x++)
            {
                if (!IsHeroBlocked(new GridPos(x, z))) continue;
                long cx = Math.Max(x * u, Math.Min(ux, (x + 1) * u));
                long cz = Math.Max(z * u, Math.Min(uz, (z + 1) * u));
                var dx = ux - cx;
                var dz = uz - cz;
                if (dx * dx + dz * dz < (long)r * r) return true;
            }

            return false;
        }

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : (a - b + 1) / b;

        /// <summary>
        /// release-rules-4 hero collision: off-board and impassable terrain cells block whole; a solid building blocks its
        /// footprint shrunk by <see cref="HeroProfile.BuildingInsetUnits"/> on every side, matching the drawn model, so the
        /// hero fits through gaps the art shows. Enemies and routes still use whole cells.
        /// </summary>
        bool HeroOverlapsDrawn(int ux, int uz)
        {
            const int u = HeroRules.UnitsPerTile;
            var p = hero.Profile;
            var r = p.RadiusUnits;
            var x0 = FloorDiv(ux - r, u);
            var x1 = FloorDiv(ux + r, u);
            var z0 = FloorDiv(uz - r, u);
            var z1 = FloorDiv(uz + r, u);
            for (var z = z0; z <= z1; z++)
            for (var x = x0; x <= x1; x++)
            {
                var c = new GridPos(x, z);
                if (!IsHeroBlocked(c)) continue;
                if (TerrainPropsOverlap(c, ux, uz, r, out var drawnOnly))
                    return true;
                if (drawnOnly) continue;
                long minX = x * u, minZ = z * u, maxX = (x + 1) * u, maxZ = (z + 1) * u;
                var b = GridCoordinates.IsInBounds(c) && !grid.IsImpassableTerrain(c) ? grid.GetOccupant(c) : null;
                if (b != null && b.Kind == BuildingKind.Wall)
                {
                    if (WallOverlaps(b.Cell, ux, uz, r)) return true;
                    continue;
                }

                if (b != null && b.Kind == BuildingKind.Cannon && b.Definition.FootprintSize == 1)
                {
                    if (FieldGunOverlaps(b, ux, uz, r)) return true;
                    continue;
                }

                if (b == null && GridCoordinates.IsInBounds(c) && config.Terrain != null && config.Terrain.Cells[c.Z * GridCoordinates.Size + c.X] == TerrainType.Water)
                {
                    if (WaterOverlaps(c, ux, uz, r)) return true;
                    continue;
                }

                if (b != null)
                {
                    var inset = p.BuildingInsetUnits(b.Kind);
                    var size = b.Definition.FootprintSize * u;
                    int insetX = inset, insetZ = inset;
                    if (b.Kind == BuildingKind.House)
                    {
                        // 0.86 wide and 0.76 deep, turned in 90 degree steps.
                        if ((b.Orientation & 1) == 0) insetZ = HeroProfile.HouseDepthInsetUnits;
                        else insetX = HeroProfile.HouseDepthInsetUnits;
                    }

                    minX = b.Cell.X * u + insetX;
                    minZ = b.Cell.Z * u + insetZ;
                    maxX = b.Cell.X * u + size - insetX;
                    maxZ = b.Cell.Z * u + size - insetZ;
                }

                if (BoxOverlaps(minX, minZ, maxX, maxZ, ux, uz, r)) return true;
            }

            return false;
        }

        static bool BoxOverlaps(long minX, long minZ, long maxX, long maxZ, int ux, int uz, int r)
        {
            long cx = Math.Max(minX, Math.Min(ux, maxX));
            long cz = Math.Max(minZ, Math.Min(uz, maxZ));
            var dx = ux - cx;
            var dz = uz - cz;
            return dx * dx + dz * dz < (long)r * r;
        }

        /// <summary>
        /// release-rules-4 Wall as drawn: the centre post plus an arm to the edge of each side with a neighbouring Wall (the
        /// same connection mask the view builds its shape from), each <see cref="HeroProfile.WallHalfUnits"/> either side of
        /// its line; a lone Wall is a ring of logs.
        /// </summary>
        bool WallOverlaps(GridPos cell, int ux, int uz, int r)
        {
            const int u = HeroRules.UnitsPerTile;
            long cx = cell.X * u + u / 2, cz = cell.Z * u + u / 2;
            var mask = WallTopology.Mask(grid, cell);
            if (mask == 0) return BoxOverlaps(cx - HeroProfile.LoneWallHalfUnits, cz - HeroProfile.LoneWallHalfUnits, cx + HeroProfile.LoneWallHalfUnits, cz + HeroProfile.LoneWallHalfUnits, ux, uz, r);
            const int w = HeroProfile.WallHalfUnits;
            if (BoxOverlaps(cx - w, cz - w, cx + w, cz + w, ux, uz, r)) return true;
            if ((mask & 1) != 0 && BoxOverlaps(cx - w, cz, cx + w, cz + u / 2, ux, uz, r)) return true; // north
            if ((mask & 2) != 0 && BoxOverlaps(cx, cz - w, cx + u / 2, cz + w, ux, uz, r)) return true; // east
            if ((mask & 4) != 0 && BoxOverlaps(cx - w, cz - u / 2, cx + w, cz, ux, uz, r)) return true; // south
            return (mask & 8) != 0 && BoxOverlaps(cx - u / 2, cz - w, cx, cz + w, ux, uz, r); // west
        }

        /// <summary>
        /// release-rules-4 1 x 1 Cannon as drawn (owner playtest 2026-10-21: the hero fits between the gun and its corner
        /// props): the gun a disc of <see cref="HeroProfile.FieldGunRadiusUnits"/> at the cell centre, and the shot pile and
        /// powder kegs a disc each in two opposite corners, the diagonal turning with the Cannon.
        /// </summary>
        static bool FieldGunOverlaps(Building b, int ux, int uz, int r)
        {
            const int u = HeroRules.UnitsPerTile, o = HeroProfile.FieldGunPropOffsetUnits;
            long cx = b.Cell.X * u + u / 2, cz = b.Cell.Z * u + u / 2;
            if (DiscOverlaps(cx, cz, HeroProfile.FieldGunRadiusUnits, ux, uz, r)) return true;
            var sz = (b.Orientation & 1) == 0 ? 1 : -1;
            return DiscOverlaps(cx + o, cz + sz * o, HeroProfile.FieldGunPropRadiusUnits, ux, uz, r) ||
                   DiscOverlaps(cx - o, cz - sz * o, HeroProfile.FieldGunPropRadiusUnits, ux, uz, r);
        }

        static bool DiscOverlaps(long cx, long cz, int radius, int ux, int uz, int r)
        {
            var dx = ux - cx;
            var dz = uz - cz;
            long reach = radius + r;
            return dx * dx + dz * dz < reach * reach;
        }

        HashSet<GridPos> riverCrossings;

        /// <summary>
        /// release-rules-4 water cell: the hero may stand on its drawn bank, within <see cref="HeroProfile.BankUnits"/> of a side
        /// (or a corner) whose neighbour is land; elsewhere the cell is water. The body is tested at its centre and eight
        /// points on its rim, in integers.
        /// </summary>
        bool WaterOverlaps(GridPos cell, int ux, int uz, int r)
        {
            riverCrossings ??= new HashSet<GridPos>(MapProfiles.RiverCrossings(config.Terrain));
            bool Land(int x, int z)
            {
                var n = new GridPos(x, z);
                return GridCoordinates.IsInBounds(n) && config.Terrain.Cells[z * GridCoordinates.Size + x] != TerrainType.Water && !riverCrossings.Contains(n);
            }

            const int u = HeroRules.UnitsPerTile, bank = HeroProfile.BankUnits;
            bool W = Land(cell.X - 1, cell.Z), E = Land(cell.X + 1, cell.Z), S = Land(cell.X, cell.Z - 1), N = Land(cell.X, cell.Z + 1);
            var d = r * 707 / 1000;
            int[] ox = { 0, r, -r, 0, 0, d, d, -d, -d }, oz = { 0, 0, 0, r, -r, d, -d, d, -d };
            for (var i = 0; i < ox.Length; i++)
            {
                int fx = ux + ox[i] - cell.X * u, fz = uz + oz[i] - cell.Z * u;
                if (fx < 0 || fx > u || fz < 0 || fz > u) continue; // this point is not in the water cell
                var dry = W && fx <= bank || E && u - fx <= bank || S && fz <= bank || N && u - fz <= bank;
                if (!dry)
                {
                    bool Corner(bool a, bool b2, int dx, int dz, long px, long pz) =>
                        !a && !b2 && Land(cell.X + dx, cell.Z + dz) && px * px + pz * pz <= (long)bank * bank;
                    dry = Corner(W, S, -1, -1, fx, fz) || Corner(E, S, 1, -1, u - fx, fz) || Corner(W, N, -1, 1, fx, u - fz) || Corner(E, N, 1, 1, u - fx, u - fz);
                }

                if (!dry) return true;
            }

            return false;
        }

        /// <summary>
        /// release-rules-4: a forest or rock cell of a generated map blocks the hero only where its props stand (owner playtest
        /// 2026-10-21): two tree trunks (radius 48 units at fixed spots) and a bush (radius 144, corner chosen by the cell's
        /// stable hash) on forest; one boulder disc (radius about 0.31-0.35 tile, scaled by the same hash) on rock. This
        /// mirrors the art layout in WorldView.PlaceArtCell; change both together. Water and test-only impassable cells
        /// (no terrain map) stay whole: <paramref name="drawnOnly"/> is false for them.
        /// </summary>
        bool TerrainPropsOverlap(GridPos c, int ux, int uz, int r, out bool drawnOnly)
        {
            drawnOnly = false;
            var map = config.Terrain;
            if (map == null || !GridCoordinates.IsInBounds(c) || grid.GetOccupant(c) != null) return false;
            var type = map.Cells[c.Z * GridCoordinates.Size + c.X];
            if (type != TerrainType.Forest && type != TerrainType.Rock) return false;
            drawnOnly = true;
            const int u = HeroRules.UnitsPerTile;
            int cx = c.X * u + u / 2, cz = c.Z * u + u / 2;
            var h7 = (c.X * 73 + c.Z * 151) % 7; // WorldView's per-cell variation h = h7 / 7
            bool Disc(int ox, int oz, int radius)
            {
                long dx = ux - (cx + ox), dz = uz - (cz + oz), reach = r + radius;
                return dx * dx + dz * dz < reach * reach;
            }

            if (type == TerrainType.Rock) return Disc(0, 0, 376 + 59 * h7 / 7);
            var bush = h7 >= 4 ? 264 : -264;
            return Disc(-216, 180, 48) || Disc(240, -204, 48) || Disc(bush, bush, 144);
        }

        long heroDayTick;

        /// <summary>
        /// The hero's own clock: the Night step during Night, its Day clock during Day (release-rules-4 swings by Day too).
        /// Attack ticks are on this clock; Night start clears them.
        /// </summary>
        public long HeroClock => State == RunState.Day ? heroDayTick : Tick;

        void ResetHero()
        {
            if (hero == null) return;
            hero.Hp = HeroRules.MaxHp;
            hero.Alive = true;
            hero.SwingReadyTick = 0;
            hero.RespawnTick = 0;
            hero.Facing = 0;
            hero.VelX = hero.VelZ = 0;
            hero.ClearAttack();
            hero.PlaceAt(HeroSpawnCell(GridState.KeepCell));
            provokedArchers.Clear();
            heroDayAccumulatorNs = 0;
            heroDayTick = 0;
        }

        /// <summary>A building placed over the hero's body succeeds; the hero moves to the nearest free cell (specs/003 2.1).</summary>
        void StepHeroAside()
        {
            if (hero == null || !HeroOverlapsBlocked(hero.UnitsX, hero.UnitsZ)) return;
            hero.VelX = hero.VelZ = 0;
            hero.ClearAttack();
            hero.PlaceAt(HeroSpawnCell(hero.Cell));
            events.Add(new SimEvent(SimEventType.HeroRelocated, Tick, HeroRules.SourceId, -1, hero.Cell, hero.X, hero.Z, hero.X, hero.Z));
        }

        /// <summary>Restores the hero position saved at Start Night (release-rules-2 checkpoints); false if it is not legal.</summary>
        internal bool RestoreHeroPosition(int ux, int uz)
        {
            if (hero == null || HeroOverlapsBlocked(ux, uz)) return false;
            hero.UnitsX = hero.PrevUnitsX = ux;
            hero.UnitsZ = hero.PrevUnitsZ = uz;
            return true;
        }

        /// <summary>
        /// Restores the release-rules-4 velocity saved at Start Night after the position (a moving start replays); false if
        /// it exceeds the profile's top speed, which only a damaged or edited save can hold.
        /// </summary>
        internal bool RestoreHeroVelocity(int vx, int vz)
        {
            if (hero == null || (long)vx * vx + (long)vz * vz > (long)hero.Profile.MaxSpeedUnits * hero.Profile.MaxSpeedUnits) return false;
            hero.VelX = vx;
            hero.VelZ = vz;
            return true;
        }

        /// <summary>Step phase 2b: respawn when due, then movement from the command.</summary>
        void HeroMovePhase(HeroCommand command)
        {
            if (hero == null) return;
            if (!hero.Alive && Tick >= hero.RespawnTick)
            {
                hero.Alive = true;
                hero.Hp = HeroRules.MaxHp;
                hero.SwingReadyTick = Tick;
                hero.VelX = hero.VelZ = 0;
                hero.ClearAttack();
                hero.PlaceAt(HeroSpawnCell(GridState.KeepCell));
                provokedArchers.Clear();
                events.Add(new SimEvent(SimEventType.HeroRespawned, Tick, HeroRules.SourceId, -1, hero.Cell, hero.X, hero.Z, hero.X, hero.Z));
            }

            MoveHero(command);
        }

        void MoveHero(HeroCommand command)
        {
            hero.PrevUnitsX = hero.UnitsX;
            hero.PrevUnitsZ = hero.UnitsZ;
            if (!hero.Alive)
            {
                hero.VelX = hero.VelZ = 0;
                return;
            }

            // Legacy rules: an uncontrolled hero stands still. release-rules-4: it brakes to rest with the motor.
            PushHeroOutOfBodies();
            if (!command.Controlled && !hero.Profile.ContactPhase) return;
            if (command.Controlled) hero.Facing = command.Facing;
            HeroMotion.Step(ref hero.UnitsX, ref hero.UnitsZ, ref hero.VelX, ref hero.VelZ, command, hero.Attacking(HeroClock), hero.Profile, HeroOverlaps);
        }

        HeroMotion.Overlaps heroOverlaps;
        HeroMotion.Overlaps HeroOverlaps => heroOverlaps ?? (heroOverlaps = HeroOverlapsAnything);

        /// <summary>
        /// Buildings, terrain and the edge, then (spec 014, at Night) enemy bodies the hero does not already overlap: the
        /// hero slides along them but can always step out of a body it is inside.
        /// </summary>
        bool HeroOverlapsAnything(int ux, int uz)
        {
            if (HeroOverlapsBlocked(ux, uz)) return true;
            if (State != RunState.Night || hero == null || !hero.Profile.ContactPhase) return false;
            foreach (var o in enemies)
                if (o.Alive && o.Definition.BodyRadius > 0 && BodyOverlapsHeroAt(o, ux, uz) && !BodyOverlapsHeroAt(o, hero.UnitsX, hero.UnitsZ)) return true;
            return false;
        }

        bool BodyOverlapsHeroAt(Enemy o, long ux, long uz)
        {
            long ox = (long)Math.Round(o.X * HeroRules.UnitsPerTile), oz = (long)Math.Round(o.Z * HeroRules.UnitsPerTile);
            var r = hero.Profile.RadiusUnits + (long)Math.Round(o.Definition.BodyRadius * HeroRules.UnitsPerTile);
            long dx = ux - ox, dz = uz - oz;
            return dx * dx + dz * dz < r * r;
        }

        /// <summary>
        /// Spec 014: enemies push the hero. After the enemies moved, the hero leaves each body it overlaps (ID order) along
        /// the line between their centres, by at most UnitBodies.HeroPushUnits in total, never into a building, terrain or
        /// the edge; with no room it stays overlapped (enemies are never stopped by the hero).
        /// </summary>
        void PushHeroOutOfBodies()
        {
            if (State != RunState.Night || !hero.Profile.ContactPhase) return;
            var budget = (long)UnitBodies.HeroPushUnits;
            foreach (var o in enemies)
            {
                if (budget <= 0) break;
                if (!o.Alive || o.Definition.BodyRadius <= 0 || !BodyOverlapsHeroAt(o, hero.UnitsX, hero.UnitsZ)) continue;
                long ox = (long)Math.Round(o.X * HeroRules.UnitsPerTile), oz = (long)Math.Round(o.Z * HeroRules.UnitsPerTile);
                long dx = hero.UnitsX - ox, dz = hero.UnitsZ - oz;
                var r = hero.Profile.RadiusUnits + (long)Math.Round(o.Definition.BodyRadius * HeroRules.UnitsPerTile);
                var length = HeroMotion.IsqrtCeil(dx * dx + dz * dz);
                if (length == 0) { dx = 1; dz = 0; length = 1; } // exactly on top: out to the east
                var push = Math.Min(budget, r - length + 1);
                if (push <= 0) continue;
                var nx = (int)(hero.UnitsX + dx * push / length);
                var nz = (int)(hero.UnitsZ + dz * push / length);
                if (HeroOverlapsBlocked(nx, nz)) continue;
                hero.UnitsX = nx;
                hero.UnitsZ = nz;
                budget -= push;
            }
        }

        /// <summary>
        /// Read-only prediction for the view: where the next fixed step would put the hero with this command, computed by the
        /// same movement code on copies of its position and velocity. Never changes the hero, the grid or the clocks.
        /// </summary>
        public void PredictHeroStep(HeroCommand command, out int ux, out int uz)
        {
            ux = hero?.UnitsX ?? 0;
            uz = hero?.UnitsZ ?? 0;
            if (hero == null || !hero.Alive || (!command.Controlled && !hero.Profile.ContactPhase)) return;
            int vx = hero.VelX, vz = hero.VelZ;
            HeroMotion.Step(ref ux, ref uz, ref vx, ref vz, command, hero.Attacking(HeroClock), hero.Profile, HeroOverlaps);
        }

        /// <summary>Step phase 3b: a player swing, or the guard's automatic swing at the nearest enemy in reach.</summary>
        void HeroAttackPhase(HeroCommand command)
        {
            if (hero != null && hero.Profile.ContactPhase)
            {
                ContactAttackPhase(command);
                return;
            }

            if (hero == null || !hero.Alive || Tick < hero.SwingReadyTick) return;
            if (command.Controlled)
            {
                if (command.Attack) Swing();
                return;
            }

            Enemy nearest = null;
            var best = double.MaxValue;
            foreach (var e in enemies) // ascending ID: strict comparison keeps the lowest on ties
            {
                if (!e.Alive) continue;
                var dx = e.X - hero.X;
                var dz = e.Z - hero.Z;
                var d = dx * dx + dz * dz;
                if (d > HeroRules.SwingReach * HeroRules.SwingReach + 1e-9 || d >= best - 1e-12) continue;
                nearest = e;
                best = d;
            }

            if (nearest == null) return;
            hero.Facing = HeroRules.FacingToward(nearest.X - hero.X, nearest.Z - hero.Z);
            Swing();
        }

        void Swing()
        {
            hero.SwingReadyTick = Tick + HeroRules.SwingIntervalTicks;
            var f = hero.Facing;
            events.Add(new SimEvent(SimEventType.HeroAttacked, Tick, HeroRules.SourceId, f, hero.Cell, hero.X, hero.Z,
                hero.X + HeroRules.FacingX[f] / 1024.0 * HeroRules.SwingReach, hero.Z + HeroRules.FacingZ[f] / 1024.0 * HeroRules.SwingReach));
            foreach (var e in enemies)
            {
                if (!e.Alive || !HeroRules.InSwing(f, e.X - hero.X, e.Z - hero.Z)) continue;
                if (e.Kind == EnemyKind.Archer) provokedArchers.Add(e.Id);
                events.Add(new SimEvent(SimEventType.HeroHit, Tick, HeroRules.SourceId, e.Id, e.Cell, e.X, e.Z, e.X, e.Z));
                DamageEnemy(e, HeroRules.SwingDamage, HeroRules.SourceId);
            }
        }

        /// <summary>
        /// release-rules-4 phase 3b (specs/003 section 4): first a due contact of an attack accepted on an earlier step, then,
        /// when ready, a new acceptance: the player's attack with the aim committed now, or the guard's at the nearest enemy
        /// it can strike. Contact follows WindUpTicks later against whoever is in reach and arc of the hero's position then.
        /// </summary>
        void ContactAttackPhase(HeroCommand command)
        {
            if (!hero.Alive) return;
            var now = HeroClock;
            if (hero.AttackPending && now >= hero.ContactTick) ResolveContact();
            if (now < hero.SwingReadyTick) return;
            int facing;
            if (command.Controlled)
            {
                if (!command.Attack) return;
                facing = command.Facing;
            }
            else
            {
                var p = hero.Profile;
                Enemy nearest = null;
                var best = double.MaxValue;
                foreach (var e in enemies) // ascending ID: strict comparison keeps the lowest on ties
                {
                    if (!e.Alive) continue;
                    var dx = e.X - hero.X;
                    var dz = e.Z - hero.Z;
                    var d = dx * dx + dz * dz;
                    var reach = p.SwingReach + UnitBodies.ExtraReach(e.Definition);
                    if (d > reach * reach + 1e-9 || d >= best - 1e-12 || !StrikeClear(e)) continue;
                    nearest = e;
                    best = d;
                }

                if (nearest == null) return;
                facing = HeroRules.FacingToward(nearest.X - hero.X, nearest.Z - hero.Z);
                hero.Facing = facing;
            }

            var profile = hero.Profile;
            hero.AttackPending = true;
            hero.AttackStartTick = now;
            hero.ContactTick = now + profile.WindUpTicks;
            hero.SwingReadyTick = now + profile.SwingIntervalTicks;
            hero.AttackFacing = facing;
            events.Add(new SimEvent(SimEventType.HeroAttacked, now, HeroRules.SourceId, facing, hero.Cell, hero.X, hero.Z,
                hero.X + HeroRules.FacingX[facing] / 1024.0 * profile.SwingReach, hero.Z + HeroRules.FacingZ[facing] / 1024.0 * profile.SwingReach));
        }

        readonly List<Enemy> contactVictims = new List<Enemy>();

        /// <summary>The single contact of an accepted attack: committed facing, the hero's position now, living enemies now.</summary>
        void ResolveContact()
        {
            var p = hero.Profile;
            var f = hero.AttackFacing;
            hero.AttackPending = false;
            contactVictims.Clear();
            foreach (var e in enemies)
                if (e.Alive && p.InSwing(f, e.X - hero.X, e.Z - hero.Z, UnitBodies.ExtraReach(e.Definition)) && StrikeClear(e)) contactVictims.Add(e);
            events.Add(new SimEvent(SimEventType.HeroContact, HeroClock, HeroRules.SourceId, contactVictims.Count, hero.Cell, hero.X, hero.Z,
                hero.X + HeroRules.FacingX[f] / 1024.0 * p.SwingReach, hero.Z + HeroRules.FacingZ[f] / 1024.0 * p.SwingReach));
            foreach (var e in contactVictims)
            {
                if (e.Kind == EnemyKind.Archer) provokedArchers.Add(e.Id);
                else e.ProvokedUntilTick = Tick + p.ProvokeTicks; // a struck melee enemy turns on the hero at its next decision
                events.Add(new SimEvent(SimEventType.HeroHit, Tick, HeroRules.SourceId, e.Id, e.Cell, e.X, e.Z, e.X, e.Z));
                DamageEnemy(e, p.SwingDamage, HeroRules.SourceId);
            }

            contactVictims.Clear();
        }

        HeroCombatRules.CellBlocked strikeBlocked;

        /// <summary>release-rules-4: no solid building or impassable terrain touches the line from the hero to the enemy.</summary>
        bool StrikeClear(Enemy e) => HeroCombatRules.StrikeClear(hero.UnitsX, hero.UnitsZ,
            (long)Math.Round(e.X * HeroRules.UnitsPerTile), (long)Math.Round(e.Z * HeroRules.UnitsPerTile),
            strikeBlocked ?? (strikeBlocked = IsHeroBlocked));

        /// <summary>
        /// Melee engagement (specs/003 2.4) at a cell-centre decision point: Grunt, Runner and Brute stop for a hero within
        /// 1.0 and keep it within 1.5. Never at the route end and never while attacking a building. Bombers and bosses ignore it.
        /// </summary>
        bool MeleeHoldsHero(Enemy e, bool freshStep)
        {
            if (e.TargetsHero)
            {
                if (hero != null && hero.Alive && HeroDistanceSq(e) <= hero.Profile.StickyRange * hero.Profile.StickyRange + 1e-9)
                {
                    e.CarryUnits = 0;
                    return true;
                }

                e.TargetsHero = false; // resume the route at this decision point
            }

            if (hero == null || !hero.Alive || e.Definition.IsRanged || e.Definition.IsBomber || e.Definition.IsBoss) return false;
            if (e.TargetBuildingId >= 0 || e.RouteIndex >= e.Route.ReachableEndIndex) return false;
            if (HeroDistanceSq(e) > hero.Profile.EngageRange * hero.Profile.EngageRange + 1e-9) return false;
            e.CarryUnits = 0;
            if (!freshStep) return true; // as for buildings: the target is taken on the next simulation step
            e.TargetsHero = true;
            e.AttackDueTick = Tick + e.AttackTicks;
            return true;
        }

        /// <summary>
        /// release-rules-4 threat at a cell-centre decision point: a Grunt, Runner or Brute struck by the hero (even one
        /// hitting a building), or one not attacking a building with the hero within the start distance and its cooldown
        /// over, starts a bounded chase when nothing solid lies between them. One chase per enemy (and so per night); after
        /// it the enemy only fights back in place. Bombers, bosses and archers never chase.
        /// </summary>
        bool StartsPursuit(Enemy e, bool freshStep)
        {
            if (e.Chased) return FightsBackInPlace(e, freshStep);
            var p = hero.Profile;
            if (!hero.Alive || e.Definition.IsRanged || e.Definition.IsBomber || e.Definition.IsBoss) return false;
            var provoked = Tick < e.ProvokedUntilTick;
            var near = e.TargetBuildingId < 0 && Tick >= e.PursueCooldownTick && HeroDistanceSq(e) <= p.PursuitStart * p.PursuitStart + 1e-9;
            if (!provoked && !near) return false;
            if (!PursuitLineClear(e)) return false;
            e.CarryUnits = 0;
            if (!freshStep) return true; // as for buildings: the chase starts on the next simulation step
            EndRaid(e);
            e.Pursuing = true;
            e.Chased = true;
            e.PursueUntilTick = Tick + p.PursuitTicks;
            e.ProvokedUntilTick = 0;
            e.TargetBuildingId = -1;
            e.TargetsHero = false;
            e.OffUnitsX = (int)Math.Round(e.X * HeroRules.UnitsPerTile);
            e.OffUnitsZ = (int)Math.Round(e.Z * HeroRules.UnitsPerTile);
            e.LaneX = e.LaneZ = 0; // off the route the body position is its own; the lane eases back in afterwards
            return true;
        }

        /// <summary>
        /// release-rules-4, after an enemy's one chase: it never leaves its route for the hero again, but attacks the hero
        /// in place while the hero is within its hit reach. It starts where it would otherwise move on (never at its route
        /// end) or, struck within the provocation time, also at its route end or while attacking a building. A building it
        /// turned from is resumed with the same attack timing, so a hit already due lands at once.
        /// </summary>
        bool FightsBackInPlace(Enemy e, bool freshStep)
        {
            var p = hero.Profile;
            var inReach = hero.Alive && HeroDistanceSq(e) <= p.EnemyHitReach * p.EnemyHitReach + 1e-9;
            if (e.TargetsHero)
            {
                if (inReach)
                {
                    e.CarryUnits = 0;
                    return true;
                }

                e.TargetsHero = false;
            }

            if (e.ResumeBuildingId >= 0)
            {
                if (GetBuilding(e.ResumeBuildingId)?.IsAlive ?? false) e.TargetBuildingId = e.ResumeBuildingId; // its timing is kept
                e.ResumeBuildingId = -1;
                return false;
            }

            if (!inReach || e.Definition.IsRanged || e.Definition.IsBomber || e.Definition.IsBoss) return false;
            if (Tick >= e.ProvokedUntilTick && (e.TargetBuildingId >= 0 || e.RouteIndex >= e.Route.ReachableEndIndex)) return false;
            e.CarryUnits = 0;
            if (!freshStep) return true; // as for buildings: the target is taken on the next simulation step
            EndRaid(e); // spec 016: the hero ends a raid; it is never resumed
            if (e.TargetBuildingId >= 0) e.ResumeBuildingId = e.TargetBuildingId;
            else e.AttackDueTick = Tick + e.AttackTicks;
            e.TargetBuildingId = -1;
            e.TargetsHero = true;
            e.ProvokedUntilTick = 0;
            return true;
        }

        bool PursuitLineClear(Enemy e) => HeroCombatRules.StrikeClear((long)Math.Round(e.X * HeroRules.UnitsPerTile), (long)Math.Round(e.Z * HeroRules.UnitsPerTile),
            hero.UnitsX, hero.UnitsZ, strikeBlocked ?? (strikeBlocked = IsHeroBlocked));

        /// <summary>
        /// One step of a chase (straight at the hero, at the enemy's own speed, stopping just inside its hit reach and then
        /// holding the hero as its target) or of the walk back to its cell centre afterwards. The line to the hero is clear of
        /// solid cells, so every point on it lies on walkable ground. Spike Traps trigger on entering their cell, as on a route.
        /// </summary>
        void PursueStep(Enemy e)
        {
            var p = hero?.Profile;
            if (e.Pursuing)
            {
                var keep = hero != null && hero.Alive && Tick < e.PursueUntilTick && HeroDistanceSq(e) <= p.PursuitLeash * p.PursuitLeash + 1e-9 && PursuitLineClear(e);
                if (!keep)
                {
                    e.Pursuing = false;
                    e.Returning = true;
                    e.TargetsHero = false;
                    e.PursueCooldownTick = Tick + (p?.PursuitCooldownTicks ?? 0);
                }
                else
                {
                    long dx = hero.UnitsX - e.OffUnitsX, dz = hero.UnitsZ - e.OffUnitsZ;
                    var length = HeroMotion.IsqrtCeil(dx * dx + dz * dz);
                    var reachTiles = p.EnemyHitReach + UnitBodies.ExtraReach(e.Definition);
                    var stopAt = (long)(reachTiles * HeroRules.UnitsPerTile * 0.75);
                    if (length <= (long)(reachTiles * HeroRules.UnitsPerTile))
                    {
                        if (!e.TargetsHero)
                        {
                            e.TargetsHero = true;
                            e.AttackDueTick = Tick + e.AttackTicks;
                        }
                    }

                    if (length > stopAt) MoveOff(e, dx, dz, Math.Min(MoveUnits(e), length - stopAt), length);
                    return;
                }
            }

            GridCoordinates.TileCenter(e.Cell, out var cx, out var cz);
            long bx = (long)Math.Round(cx * HeroRules.UnitsPerTile) - e.OffUnitsX, bz = (long)Math.Round(cz * HeroRules.UnitsPerTile) - e.OffUnitsZ;
            var back = HeroMotion.IsqrtCeil(bx * bx + bz * bz);
            if (back > MoveUnits(e))
            {
                MoveOff(e, bx, bz, MoveUnits(e), back);
                return;
            }

            e.Returning = false; // back on its cell centre: plan again from here, as after any route change
            e.X = cx;
            e.Z = cz;
            e.Route = null;
            e.RouteIndex = 0;
            e.CarryUnits = 0;
        }

        /// <summary>Moves an off-route enemy <paramref name="step"/> units along (dx, dz) of length <paramref name="length"/>.</summary>
        void MoveOff(Enemy e, long dx, long dz, long step, long length)
        {
            if (length <= 0 || step <= 0) return;
            e.OffUnitsX += (int)(dx * step / length);
            e.OffUnitsZ += (int)(dz * step / length);
            e.X = e.OffUnitsX / (double)HeroRules.UnitsPerTile;
            e.Z = e.OffUnitsZ / (double)HeroRules.UnitsPerTile;
            var cell = new GridPos((int)Math.Floor(e.X), (int)Math.Floor(e.Z));
            if (cell == e.Cell) return;
            e.Cell = e.NextCell = cell;
            TriggerTrap(e);
        }

        double HeroDistanceSq(Enemy e)
        {
            var dx = e.X - hero.X;
            var dz = e.Z - hero.Z;
            return dx * dx + dz * dz;
        }

        /// <summary>Raider Archer range test to the hero: from its cell centre, like buildings.</summary>
        double HeroRangedDistanceSq(Enemy e)
        {
            GridCoordinates.TileCenter(e.Cell, out var cx, out var cz);
            var dx = hero.X - cx;
            var dz = hero.Z - cz;
            return dx * dx + dz * dz;
        }

        /// <summary>The hero beats the best building when it is in range and has a lower class, or the same class and is nearer.</summary>
        bool HeroIsBetterRangedTarget(Enemy e, Building best)
        {
            if (hero == null || !hero.Alive) return false;
            var d = HeroRangedDistanceSq(e);
            if (d > RangeSq(e)) return false;
            if (best == null) return true;
            var cls = RangedClass(best.Kind);
            if (HeroRules.RangedClass != cls) return HeroRules.RangedClass < cls;
            return d <= RangedDistanceSq(e, best) + 1e-9; // equal distance: the hero counts as the lowest ID
        }

        /// <summary>An enemy's due hit on the hero: normal damage plus the Commander aura; no Wall multiplier or reflection.</summary>
        void HitHero(Enemy e)
        {
            if (hero == null || !hero.Alive)
            {
                e.TargetsHero = false;
                return;
            }

            if (Tick < e.AttackDueTick) return;
            // Melee reach is the hero's own: a hero that steps back out of reach is not hit (the due hit waits, so it lands
            // as soon as the hero is in reach again). Hit-and-run is a real move. Ranged hits use the archer's range.
            var hitReach = hero.Profile.EnemyHitReach + UnitBodies.ExtraReach(e.Definition);
            if (!e.Definition.IsRanged && HeroDistanceSq(e) > hitReach * hitReach + 1e-9) return;
            e.AttackDueTick += e.AttackTicks;
            var damage = e.Definition.AttackDamage;
            var aura = CommanderAuraPercent(e);
            if (aura > 0) damage = damage * (100 + aura) / 100;
            hero.Hp = Math.Max(0, hero.Hp - damage);
            events.Add(new SimEvent(SimEventType.HeroDamaged, Tick, e.Id, damage, hero.Cell, e.X, e.Z, hero.X, hero.Z));
            if (hero.Hp > 0) return;
            hero.Alive = false; // no longer a target; the rest is resolved in phase 6
            hero.RespawnTick = long.MaxValue;
            hero.VelX = hero.VelZ = 0;
            hero.ClearAttack(); // an unlanded release-rules-4 attack dies with the hero
        }

        /// <summary>Step phase 6: a downed hero drops every enemy's hold on it and schedules its respawn. Never Defeat.</summary>
        void HeroDeathPhase()
        {
            if (hero == null || hero.Alive || hero.RespawnTick != long.MaxValue) return;
            hero.RespawnTick = Tick + HeroRules.RespawnTicks;
            foreach (var e in enemies) e.TargetsHero = false;
            provokedArchers.Clear();
            events.Add(new SimEvent(SimEventType.HeroDowned, Tick, HeroRules.SourceId, -1, hero.Cell, hero.X, hero.Z, hero.X, hero.Z));
        }

        List<int> SortedProvoked()
        {
            var list = new List<int>(provokedArchers);
            list.Sort();
            return list;
        }

        void Finish(RunState terminal)
        {
            nextSpawnIndex = Wave.Spawns.Count; // cancel pending spawns
            Paused = false;
            accumulatorNs = 0;
            SetState(terminal);
        }

        void SetState(RunState state)
        {
            State = state;
            events.Add(new SimEvent(SimEventType.StateChanged, Tick, (int)state));
        }

        void Register(Building b)
        {
            buildingsById[b.Id] = b;
            if (b.Kind == BuildingKind.Tower || b.Kind == BuildingKind.Cannon) towers.Add(b);
        }

        void Unregister(Building b)
        {
            buildingsById.Remove(b.Id);
            towers.Remove(b);
        }

        /// <summary>Canonical text dump of every gameplay-relevant value, for determinism and restart checks.</summary>
        public string CaptureSnapshot()
        {
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            sb.Append("state=").Append(State).Append(" night=").Append(NightNumber).Append(" paid=").Append(LastPaidDay)
                .Append(" paused=").Append(Paused).Append(" tick=").Append(Tick)
                .Append(" acc=").Append(accumulatorNs).Append(" gold=").Append(Gold).Append(" rev=").Append(grid.Revision)
                .Append(" nextSpawn=").Append(nextSpawnIndex).Append(earlyCallTick >= 0 ? " called=" + earlyCallTick.ToString(inv) : "").Append(" nextB=").Append(nextBuildingId)
                .Append(" nextE=").Append(nextEnemyId).Append(" diag=").Append(diagnostics.Count)
                .Append(" rng=").Append(rewardRng.State).Append(" perks=").Append(string.Join(",", ownedPerks))
                .Append(" offers=").Append(offerHistory.Count).Append(CurrentOffer != null ? "+current" : "")
                .Append(" stats=").Append(Stats.NightsCleared).Append('/').Append(Stats.EnemiesDefeated).Append('/')
                .Append(Stats.BuildingsPurchased).Append('/').Append(Stats.GoldEarned).Append('/').Append(Stats.SynergiesEver.Count)
                .AppendLine();
            if (hero != null)
                sb.Append("H pos=").Append(hero.UnitsX).Append(',').Append(hero.UnitsZ).Append(" hp=").Append(hero.Hp)
                    .Append(" alive=").Append(hero.Alive).Append(" face=").Append(hero.Facing).Append(" swing=").Append(hero.SwingReadyTick)
                    .Append(" respawn=").Append(hero.RespawnTick).Append(" kills=").Append(Stats.HeroKills)
                    .Append(" provoked=").Append(string.Join(",", SortedProvoked()))
                    .Append(hero.Profile.ContactPhase ? $" v={hero.VelX},{hero.VelZ} atk={hero.AttackPending}:{hero.AttackStartTick}:{hero.ContactTick}:{hero.AttackFacing}" : "")
                    .AppendLine();
            var buildings = new List<Building>(buildingsById.Values);
            buildings.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (var b in buildings)
                sb.Append("B").Append(b.Id).Append(' ').Append(b.Kind).Append(b.Cell).Append(" hp=").Append(b.Hp).Append('/').Append(b.MaxHp)
                    .Append(" price=").Append(b.PurchasePrice).Append(" rot=").Append(b.Orientation)
                    .Append(" tgt=").Append(b.TargetEnemyId).Append(" due=").Append(b.AttackDueTick)
                    .Append(b.Tier > 1 ? $" tier={b.Tier} paid={b.TotalPaid}" : "").AppendLine(); // spec 015: only when upgraded, so older fingerprints hold
            foreach (var e in enemies)
                sb.Append("E").Append(e.Id).Append(' ').Append(e.Kind).Append(" entry=").Append(e.EntryIndex).Append(" hp=").Append(e.Hp)
                    .Append(" cell=").Append(e.Cell).Append(" next=").Append(e.NextCell).Append(" mv=").Append(e.Moving)
                    .Append(':').Append(e.MoveTicks).Append(':').Append(e.ProgressUnits).Append('+').Append(e.CarryUnits)
                    .Append(" pos=").Append(e.X.ToString("R", inv)).Append(',')
                    .Append(e.Z.ToString("R", inv)).Append(" ri=").Append(e.RouteIndex).Append(" tgt=")
                    .Append(e.TargetBuildingId).Append(e.TargetsHero ? "+hero" : "").Append(" due=").Append(e.AttackDueTick)
                    .Append(" burn=").Append(e.Burning).Append(':').Append(e.BurnNextTick).Append(':').Append(e.BurnExpiryTick)
                    .Append(e.FuseLit ? $" fuse={e.DetonateTick}" : "")
                    .Append(e.SlowUntilTick > 0 ? $" slow={e.SlowPercent}:{e.SlowUntilTick}" : "")
                    .Append(e.Definition.BodyRadius > 0 ? $" body={e.LaneX.ToString("R", inv)},{e.LaneZ.ToString("R", inv)}:{e.BlockedTicks}:{e.PassUnits}" : "")
                    .Append(e.Pursuing || e.Returning || e.Chased || e.PursueCooldownTick > 0 || e.ProvokedUntilTick > 0
                        ? $" chase={(e.Pursuing ? "p" : e.Returning ? "r" : "-")}{(e.Chased ? "!" : "")}:{e.PursueUntilTick}:{e.PursueCooldownTick}:{e.ProvokedUntilTick}:{e.OffUnitsX},{e.OffUnitsZ}:{e.ResumeBuildingId}" : "")
                    .AppendLine();
            return sb.ToString();
        }
    }
}
