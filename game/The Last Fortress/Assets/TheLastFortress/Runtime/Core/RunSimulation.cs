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

        GridState grid;
        PlacementService placement;
        RunRandom rewardRng;
        int nextSpawnIndex;
        int nextBuildingId;
        int nextEnemyId;
        long accumulatorNs;
        int towerAttackTicks;

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

        public bool HasPerk(string id) => ownedPerks.Contains(id);

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
            CurrentOffer = null;
            PendingOffer = null;
            Planner.ClearCache();
            grid = new GridState(config.Terrain != null ? config.Terrain.ImpassableCells() : config.ImpassableCells);
            placement = new PlacementService(grid, Definitions, Definitions.StartingGold);
            rewardRng = RunRandom.ForRewards(config.Seed);
            Stats = new RunStats();
            LastNight = null;
            nextSpawnIndex = 0;
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

        public static bool IsRotatable(BuildingKind kind) =>
            kind == BuildingKind.Tower || kind == BuildingKind.House || kind == BuildingKind.Farm;

        /// <summary>Income the next Day would pay with the current fortress and perks (same calculation as the payment).</summary>
        public IncomeBreakdown IncomeForecast() => EconomyRules.DailyIncome(grid, Definitions, FarmBonusPercent(HasPerk(PerkRules.EfficientFarming)));

        /// <summary>
        /// Read-only preview of the next payment if <paramref name="perkId"/> were owned (Reward cards). Never installs the
        /// perk, draws rewards, pays gold or touches the fortress; equals <see cref="IncomeForecast"/> for perks that do not
        /// change income.
        /// </summary>
        public IncomeBreakdown IncomeForecastWith(string perkId) =>
            EconomyRules.DailyIncome(grid, Definitions, FarmBonusPercent(HasPerk(PerkRules.EfficientFarming) || perkId == PerkRules.EfficientFarming));

        /// <summary>Effective daily income of one House/Farm with current perks (same rule as the payment).</summary>
        public int BuildingIncome(Building b) => EconomyRules.BuildingIncome(grid, b, FarmBonusPercent(HasPerk(PerkRules.EfficientFarming)));

        static int FarmBonusPercent(bool efficientFarming) => efficientFarming ? PerkRules.EfficientFarmingPercent : 0;

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
            }

            return failures;
        }

        public Building GetBuilding(int id) => buildingsById.TryGetValue(id, out var b) ? b : null;
        public Enemy GetEnemy(int id) => enemiesById.TryGetValue(id, out var e) ? e : null;

        // ---------- Effective stats (shared definitions + run perks; definitions are never mutated) ----------

        public int EffectiveMaxHp(BuildingDefinition def)
        {
            if (!def.HasHp) return 0;
            if (def.Kind == BuildingKind.Wall && HasPerk(PerkRules.ReinforcedTimber))
                return def.MaxHp * (100 + PerkRules.ReinforcedTimberPercent) / 100;
            if (def.Kind == BuildingKind.Keep && HasPerk(PerkRules.FortressHeart)) return def.MaxHp + PerkRules.FortressHeartHp;
            return def.MaxHp;
        }

        /// <summary>Tower range: base, +10% with ExpandedVision (floating point).</summary>
        public double TowerRange(Building tower) => TowerRange(tower.Definition);

        public double TowerRange(BuildingDefinition tower) =>
            tower.Range * (HasPerk(PerkRules.ExpandedVision) ? PerkRules.ExpandedVisionFactor : 1.0);

        /// <summary>LastStand condition: Keep HP strictly below 30% of its current maximum.</summary>
        public bool LastStandActive => HasPerk(PerkRules.LastStand) && Keep.Hp * 100 < Keep.MaxHp * PerkRules.LastStandThresholdPercent;

        /// <summary>Direct Tower damage: percent bonuses add against the base, then floor once (10 → 16 with both).</summary>
        public int TowerDamage(Building tower) => TowerDamage(tower.Definition);

        public int TowerDamage(BuildingDefinition tower)
        {
            var percent = 0;
            if (HasPerk(PerkRules.Sharpshooters)) percent += PerkRules.SharpshootersPercent;
            if (LastStandActive) percent += PerkRules.LastStandPercent;
            return tower.AttackDamage * (100 + percent) / 100;
        }

        /// <summary>Reflected damage per received melee hit: 3 with any adjacent trap, +3 with BarbedDefenses (max 6).</summary>
        public int WallReflection(Building wall)
        {
            if (wall.Kind != BuildingKind.Wall) return 0;
            var reflect = grid.HasAdjacentTrap(wall.Cell) ? EconomyRules.WallTrapReflect : 0;
            if (HasPerk(PerkRules.BarbedDefenses)) reflect += PerkRules.BarbedReflect;
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
                foreach (var c in BuildingDefinition.FootprintCells(kind, anchor)) previewScratch.Add(c);
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

            towerAttackTicks = PrototypeDefinitions.ToTicks(Definitions.Tower.AttackIntervalSeconds);
            Tick = 0;
            accumulatorNs = 0;
            LastNight = new NightRecap { Night = NightNumber }; // before the time-zero spawn
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
        public int Advance(double seconds)
        {
            if (State != RunState.Night || Paused || !(seconds > 0)) return 0;
            accumulatorNs += (long)Math.Round(seconds * 1e9);
            var steps = 0;
            while (accumulatorNs >= StepNanoseconds && State == RunState.Night)
            {
                Step();
                accumulatorNs -= StepNanoseconds;
                steps++;
            }

            if (State != RunState.Night) accumulatorNs = 0;
            return steps;
        }

        /// <summary>One fixed 0.05 s step. Ignored outside an unpaused Night.</summary>
        public void Step()
        {
            if (State != RunState.Night || Paused) return;
            ReleaseDueSpawns();
            MoveEnemies();
            TowerAttacks();
            BurnTicks();
            EnemyAttacks();
            RemoveDeadEnemies();
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
            var ids = PerkRules.DrawOffer(rewardRng, ownedPerks, OwnsAliveKind);
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
            buildingsById.Clear();
            towers.Clear();
            Planner.ClearCache();
            if (cp.nextBuildingId < 2 || cp.nextEnemyId < 1) return "Entity ID counters invalid.";
            var keepSeen = false;
            foreach (var bd in cp.buildings ?? new BuildingData[0])
            {
                if (bd == null || bd.kind < 0 || bd.kind > (int)BuildingKind.SpikeTrap) return "Unknown building type.";
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

                foreach (var c in BuildingDefinition.FootprintCells(kind, cell))
                {
                    if (!GridCoordinates.IsInBounds(c) || grid.IsImpassableTerrain(c)) return $"Building {bd.id} lies outside buildable land.";
                    if (kind != BuildingKind.Keep && GridCoordinates.IsReservedRing(c)) return $"Building {bd.id} lies on the reserved ring.";
                    if (grid.IsSolid(c) || grid.HasTrap(c)) return $"Building {bd.id} overlaps another structure.";
                }

                var b = new Building(bd.id, def, cell, bd.price) { Orientation = bd.orientation };
                b.MaxHp = EffectiveMaxHp(def);
                if (def.HasHp && (bd.hp < 1 || bd.hp > b.MaxHp)) return $"Building {bd.id} HP {bd.hp} outside 1..{b.MaxHp}.";
                b.Hp = def.HasHp ? bd.hp : 0;
                grid.Install(b);
                Register(b);
            }

            if (!keepSeen) return "Keep missing.";
            placement = new PlacementService(grid, Definitions, cp.gold) { Thrifty = HasPerk(PerkRules.ThriftyConstruction) };
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
            };
            foreach (var key in s.synergiesEver ?? new string[0]) Stats.SynergiesEver.Add(key);
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
                        var max = EffectiveMaxHp(b.Definition);
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

            events.Add(new SimEvent(SimEventType.PerkAcquired, Tick, Array.IndexOf(PerkRules.AllIds, id)));
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
            PayIncome(day);
            RecordSynergies();
        }

        void PayIncome(int day)
        {
            if (day <= LastPaidDay || day > TotalNights) return;
            LastPaidDay = day;
            LastIncome = IncomeForecast();
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

        // ---------- step phases ----------

        void ReleaseDueSpawns()
        {
            // FIFO: a due spawn waits while the living cap is reached; pending spawns still block completion.
            while (nextSpawnIndex < Wave.Spawns.Count && Wave.Spawns[nextSpawnIndex].Tick <= Tick
                   && enemies.Count < MvpCampaign.MaxLiveEnemies)
            {
                var s = Wave.Spawns[nextSpawnIndex++];
                Spawn(s.Cell, s.EntryIndex, s.Kind);
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

        /// <summary>Fixed-point movement: speed * 60 units per step, 1200 units per tile; arrival keeps the remainder.</summary>
        void AdvanceSegment(Enemy e)
        {
            e.MoveTicks++;
            e.ProgressUnits += e.MoveUnitsPerStep;
            if (e.ProgressUnits >= EnemyDefinition.UnitsPerTile)
            {
                e.CarryUnits = e.ProgressUnits - EnemyDefinition.UnitsPerTile;
                e.ProgressUnits = 0;
                e.Cell = e.NextCell;
                e.Moving = false;
                e.MoveTicks = 0;
                e.RouteIndex++;
                GridCoordinates.TileCenter(e.Cell, out e.X, out e.Z);
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
            e.X = fx + (tx - fx) * t;
            e.Z = fz + (tz - fz) * t;
        }

        /// <summary>Spike Trap: fixed damage once when an enemy completes entry into the trap's cell.</summary>
        void TriggerTrap(Enemy e)
        {
            var trap = grid.GetTrap(e.Cell);
            if (trap == null || !e.Alive) return;
            events.Add(new SimEvent(SimEventType.TrapTriggered, Tick, trap.Id, e.Id, e.Cell));
            DamageEnemy(e, trap.Definition.TrapDamage, trap.Id);
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
            events.Add(new SimEvent(SimEventType.EnemyKilled, Tick, e.Id, sourceId, e.Cell, e.X, e.Z, e.X, e.Z));
        }

        /// <summary>Base-range check (specification 002); live targeting uses <see cref="TowerRange"/>.</summary>
        public static bool InRange(Building tower, double x, double z) => InRange(tower, x, z, tower.Definition.Range);

        public static bool InRange(Building tower, double x, double z, double range)
        {
            GridCoordinates.TileCenter(tower.Cell, out var cx, out var cz);
            var dx = x - cx;
            var dz = z - cz;
            return dx * dx + dz * dz <= range * range + 1e-9;
        }

        void TowerAttacks()
        {
            foreach (var t in towers)
            {
                if (!t.IsAlive) continue;
                var range = TowerRange(t);
                var target = t.TargetEnemyId >= 0 ? GetEnemy(t.TargetEnemyId) : null;
                if (target == null || !target.Alive || !InRange(t, target.X, target.Z, range))
                {
                    target = AcquireNearest(t, range);
                    t.TargetEnemyId = target?.Id ?? -1;
                    if (target != null) t.AttackDueTick = Tick + towerAttackTicks;
                }

                if (target == null || Tick < t.AttackDueTick) continue;
                t.AttackDueTick += towerAttackTicks;
                GridCoordinates.TileCenter(t.Cell, out var tx, out var tz);
                events.Add(new SimEvent(SimEventType.TowerShot, Tick, t.Id, target.Id, target.Cell, tx, tz, target.X, target.Z));
                DamageEnemy(target, TowerDamage(t), t.Id);
                if (!target.Alive) t.TargetEnemyId = -1;
                else if (HasPerk(PerkRules.FlamingArrows)) ApplyBurn(target);
            }
        }

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

        Enemy AcquireNearest(Building tower, double range)
        {
            GridCoordinates.TileCenter(tower.Cell, out var cx, out var cz);
            Enemy best = null;
            var bestDistance = double.MaxValue;
            foreach (var e in enemies) // ascending spawn ID; strict comparison keeps the lowest ID on ties
            {
                if (!e.Alive || !InRange(tower, e.X, e.Z, range)) continue;
                var dx = e.X - cx;
                var dz = e.Z - cz;
                var d = dx * dx + dz * dz;
                if (d < bestDistance - 1e-9)
                {
                    best = e;
                    bestDistance = d;
                }
            }

            return best;
        }

        void EnemyAttacks()
        {
            foreach (var e in enemies)
            {
                if (!e.Alive || e.TargetBuildingId < 0) continue;
                var target = GetBuilding(e.TargetBuildingId);
                if (target == null || !target.IsAlive)
                {
                    e.TargetBuildingId = -1;
                    continue;
                }

                if (Tick < e.AttackDueTick) continue;
                e.AttackDueTick += e.AttackTicks;
                // Reflection is decided from the Wall's cell before the hit, so a lethal hit still reflects once.
                var reflect = WallReflection(target);
                var damage = e.Definition.AttackDamage * (target.Kind == BuildingKind.Wall ? e.Definition.WallDamageMultiplier : 1);
                Damage(target, damage, e.Id);
                if (reflect <= 0) continue;
                events.Add(new SimEvent(SimEventType.DamageReflected, Tick, target.Id, e.Id, e.Cell));
                DamageEnemy(e, reflect, target.Id); // non-recursive: reflected damage never reflects
            }
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
            if (!grid.Release(target)) return; // occupancy is released exactly once, immediately
            Unregister(target);
            if (LastNight != null) LastNight.BuildingsLost++;
            var half = target.Definition.FootprintSize / 2.0;
            events.Add(new SimEvent(SimEventType.BuildingDestroyed, Tick, target.Id, sourceId, target.Cell,
                target.Cell.X + half, target.Cell.Z + half, target.Cell.X + half, target.Cell.Z + half));
            if (!HasPerk(PerkRules.Recycling)) return;
            var refund = target.PurchasePrice / 4;
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
            if (b.Kind == BuildingKind.Tower) towers.Add(b);
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
                .Append(" nextSpawn=").Append(nextSpawnIndex).Append(" nextB=").Append(nextBuildingId)
                .Append(" nextE=").Append(nextEnemyId).Append(" diag=").Append(diagnostics.Count)
                .Append(" rng=").Append(rewardRng.State).Append(" perks=").Append(string.Join(",", ownedPerks))
                .Append(" offers=").Append(offerHistory.Count).Append(CurrentOffer != null ? "+current" : "")
                .Append(" stats=").Append(Stats.NightsCleared).Append('/').Append(Stats.EnemiesDefeated).Append('/')
                .Append(Stats.BuildingsPurchased).Append('/').Append(Stats.GoldEarned).Append('/').Append(Stats.SynergiesEver.Count)
                .AppendLine();
            var buildings = new List<Building>(buildingsById.Values);
            buildings.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (var b in buildings)
                sb.Append("B").Append(b.Id).Append(' ').Append(b.Kind).Append(b.Cell).Append(" hp=").Append(b.Hp).Append('/').Append(b.MaxHp)
                    .Append(" price=").Append(b.PurchasePrice).Append(" rot=").Append(b.Orientation)
                    .Append(" tgt=").Append(b.TargetEnemyId).Append(" due=").Append(b.AttackDueTick).AppendLine();
            foreach (var e in enemies)
                sb.Append("E").Append(e.Id).Append(' ').Append(e.Kind).Append(" entry=").Append(e.EntryIndex).Append(" hp=").Append(e.Hp)
                    .Append(" cell=").Append(e.Cell).Append(" next=").Append(e.NextCell).Append(" mv=").Append(e.Moving)
                    .Append(':').Append(e.MoveTicks).Append(':').Append(e.ProgressUnits).Append('+').Append(e.CarryUnits)
                    .Append(" pos=").Append(e.X.ToString("R", inv)).Append(',')
                    .Append(e.Z.ToString("R", inv)).Append(" ri=").Append(e.RouteIndex).Append(" tgt=")
                    .Append(e.TargetBuildingId).Append(" due=").Append(e.AttackDueTick)
                    .Append(" burn=").Append(e.Burning).Append(':').Append(e.BurnNextTick).Append(':').Append(e.BurnExpiryTick).AppendLine();
            return sb.ToString();
        }
    }
}
