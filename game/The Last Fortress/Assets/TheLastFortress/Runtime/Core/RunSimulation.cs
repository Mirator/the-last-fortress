using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TheLastFortress
{
    public enum RunState
    {
        Day,
        Night,
        Victory,
        Defeat,
    }

    public sealed class RunConfig
    {
        public PrototypeDefinitions Definitions = PrototypeDefinitions.CreateDefault();
        public WavePlan Wave = WavePlan.CreateMainWave();

        /// <summary>Test-only indestructible terrain.</summary>
        public IReadOnlyList<GridPos> ImpassableCells;

        /// <summary>Stress fixture: Keep and Walls ignore combat damage; killed Grunts respawn. Never set for the main wave.</summary>
        public bool StressMode;

        public static RunConfig CreateMain() => new RunConfig();

        public static RunConfig CreateStress() => new RunConfig { Wave = WavePlan.CreateStressWave(), StressMode = true };
    }

    /// <summary>
    /// Authoritative one-wave run: placement, routing, fixed-step combat, phases, restart.
    /// Each step: release spawns, route invalidation + movement, towers, enemy attacks, then defeat before victory.
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

        GridState grid;
        PlacementService placement;
        int nextSpawnIndex;
        int nextBuildingId;
        int nextEnemyId;
        long accumulatorNs;
        int gruntTicksPerTile;
        int gruntAttackTicks;
        int towerAttackTicks;

        public RunSimulation(RunConfig config = null)
        {
            this.config = config ?? RunConfig.CreateMain();
            Planner = new RoutePlanner();
            Restart();
        }

        public RunConfig Config => config;
        public PrototypeDefinitions Definitions => config.Definitions;
        public WavePlan Wave => config.Wave;
        public bool StressMode => config.StressMode;
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

        /// <summary>Fraction of a step accumulated since the last fixed step, for render interpolation.</summary>
        public double InterpolationAlpha => (double)accumulatorNs / StepNanoseconds;

        /// <summary>Restores the exact initial state: Day, starting gold, Keep only, no entities, timers, caches or events.</summary>
        public void Restart()
        {
            enemies.Clear();
            enemiesById.Clear();
            towers.Clear();
            buildingsById.Clear();
            events.Clear();
            diagnostics.Clear();
            Planner.ClearCache();
            grid = new GridState(config.ImpassableCells);
            placement = new PlacementService(grid, Definitions, Definitions.StartingGold);
            nextSpawnIndex = 0;
            nextBuildingId = 1;
            nextEnemyId = 1;
            accumulatorNs = 0;
            Tick = 0;
            Paused = false;
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

        public PlacementRejection TryPlace(BuildingKind kind, GridPos cell, out Building building)
        {
            building = null;
            if (State != RunState.Day) return PlacementRejection.NotDay;
            var result = placement.TryPlace(kind, cell, nextBuildingId, out building);
            if (result != PlacementRejection.None) return result;
            nextBuildingId++;
            Register(building);
            events.Add(new SimEvent(SimEventType.BuildingPlaced, Tick, building.Id, cell: cell));
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

        // ---------- Routing ----------

        public Route GetLiveRoute(GridPos start) => Planner.GetLiveRoute(grid, start);

        /// <summary>Candidate route with a construction ghost; never mutates occupancy, revision, gold or the live cache.</summary>
        public Route PreviewRoute(GridPos start, GridPos ghostCell)
        {
            previewScratch.Clear();
            previewScratch.Add(ghostCell);
            return Planner.Plan(grid, start, previewScratch);
        }

        /// <summary>Errors that prevent starting Night (invalid definitions, an entry with no route).</summary>
        public List<string> ValidateMap()
        {
            var errors = Definitions.Validate();
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

            gruntTicksPerTile = Definitions.GruntTicksPerTile;
            gruntAttackTicks = PrototypeDefinitions.ToTicks(Definitions.Grunt.AttackIntervalSeconds);
            towerAttackTicks = PrototypeDefinitions.ToTicks(Definitions.Tower.AttackIntervalSeconds);
            Tick = 0;
            accumulatorNs = 0;
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
            EnemyAttacks();
            RemoveDeadEnemies();
            if (Keep.Hp > 0) RefillMaintainedEntries(); // stress keeps 100 active between steps

            if (Keep.Hp <= 0) Finish(RunState.Defeat);
            else if (!StressMode && nextSpawnIndex >= Wave.Spawns.Count && enemies.Count == 0) Finish(RunState.Victory);
            Tick++;
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
            while (nextSpawnIndex < Wave.Spawns.Count && Wave.Spawns[nextSpawnIndex].Tick <= Tick)
            {
                var s = Wave.Spawns[nextSpawnIndex++];
                Spawn(s.Cell, s.EntryIndex);
            }

            RefillMaintainedEntries();
        }

        /// <summary>Stress fixture only: replace killed Grunts at their original entry once the initial wave is out.</summary>
        void RefillMaintainedEntries()
        {
            if (Wave.MaintainActivePerEntry <= 0 || nextSpawnIndex < Wave.Spawns.Count) return;
            Array.Clear(activePerEntry, 0, activePerEntry.Length);
            foreach (var e in enemies) activePerEntry[e.EntryIndex]++;
            for (var i = 0; i < Wave.EntryCells.Count; i++)
                for (var n = activePerEntry[i]; n < Wave.MaintainActivePerEntry; n++)
                    Spawn(Wave.EntryCells[i], i);
        }

        void Spawn(GridPos cell, int entryIndex)
        {
            var e = new Enemy(nextEnemyId++, entryIndex, Definitions.Grunt, cell);
            enemies.Add(e);
            enemiesById[e.Id] = e;
            events.Add(new SimEvent(SimEventType.EnemySpawned, Tick, e.Id, cell: cell));
        }

        void MoveEnemies()
        {
            foreach (var e in enemies)
            {
                e.PrevX = e.X;
                e.PrevZ = e.Z;
                if (e.Moving)
                {
                    AdvanceSegment(e);
                    continue;
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
                    if (e.TargetBuildingId >= 0) e.TargetBuildingId = -1;
                    var message = $"Enemy {e.Id} at {e.Cell} has no route to the Keep.";
                    if (!diagnostics.Contains(message))
                    {
                        diagnostics.Add(message);
                        events.Add(new SimEvent(SimEventType.RouteMissing, Tick, e.Id, cell: e.Cell));
                    }

                    continue;
                }

                if (e.RouteIndex >= e.Route.ReachableEndIndex)
                {
                    var attackCell = e.Route.IsBreach ? e.Route.Blocker : GridState.KeepCell;
                    var target = grid.GetOccupant(attackCell);
                    if (target == null || !target.IsAlive || !e.Cell.IsCardinalNeighbor(attackCell))
                        throw new InvalidOperationException($"Enemy {e.Id} at {e.Cell} has invalid attack cell {attackCell}.");
                    if (e.TargetBuildingId != target.Id)
                    {
                        e.TargetBuildingId = target.Id;
                        e.AttackDueTick = Tick + gruntAttackTicks;
                    }

                    continue;
                }

                e.TargetBuildingId = -1;
                e.NextCell = e.Route.Cells[e.RouteIndex + 1];
                e.Moving = true;
                e.MoveTicks = 0;
                AdvanceSegment(e);
            }
        }

        void AdvanceSegment(Enemy e)
        {
            e.MoveTicks++;
            GridCoordinates.TileCenter(e.Cell, out var fx, out var fz);
            GridCoordinates.TileCenter(e.NextCell, out var tx, out var tz);
            if (e.MoveTicks >= gruntTicksPerTile)
            {
                e.Cell = e.NextCell;
                e.Moving = false;
                e.MoveTicks = 0;
                e.RouteIndex++;
                e.X = tx;
                e.Z = tz;
                return;
            }

            var t = (double)e.MoveTicks / gruntTicksPerTile;
            e.X = fx + (tx - fx) * t;
            e.Z = fz + (tz - fz) * t;
        }

        public static bool InRange(Building tower, double x, double z)
        {
            GridCoordinates.TileCenter(tower.Cell, out var cx, out var cz);
            var dx = x - cx;
            var dz = z - cz;
            var r = tower.Definition.Range;
            return dx * dx + dz * dz <= r * r + 1e-9;
        }

        void TowerAttacks()
        {
            foreach (var t in towers)
            {
                if (!t.IsAlive) continue;
                var target = t.TargetEnemyId >= 0 ? GetEnemy(t.TargetEnemyId) : null;
                if (target == null || !target.Alive || !InRange(t, target.X, target.Z))
                {
                    target = AcquireNearest(t);
                    t.TargetEnemyId = target?.Id ?? -1;
                    if (target != null) t.AttackDueTick = Tick + towerAttackTicks;
                }

                if (target == null || Tick < t.AttackDueTick) continue;
                t.AttackDueTick += towerAttackTicks;
                target.Hp = Math.Max(0, target.Hp - t.Definition.AttackDamage);
                events.Add(new SimEvent(SimEventType.TowerShot, Tick, t.Id, target.Id));
                if (target.Hp > 0) continue;
                target.Alive = false;
                t.TargetEnemyId = -1;
                events.Add(new SimEvent(SimEventType.EnemyKilled, Tick, target.Id, t.Id, target.Cell));
            }
        }

        Enemy AcquireNearest(Building tower)
        {
            GridCoordinates.TileCenter(tower.Cell, out var cx, out var cz);
            Enemy best = null;
            var bestDistance = double.MaxValue;
            foreach (var e in enemies) // ascending spawn ID; strict comparison keeps the lowest ID on ties
            {
                if (!e.Alive || !InRange(tower, e.X, e.Z)) continue;
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
                e.AttackDueTick += gruntAttackTicks;
                Damage(target, e.Definition.AttackDamage, e.Id);
            }
        }

        bool IsInvulnerable(Building b) => StressMode && (b.Kind == BuildingKind.Keep || b.Kind == BuildingKind.Wall);

        void Damage(Building target, int amount, int sourceId)
        {
            events.Add(new SimEvent(SimEventType.EnemyAttacked, Tick, sourceId, target.Id, target.Cell));
            if (IsInvulnerable(target)) return;
            target.Hp = Math.Max(0, Math.Min(target.Definition.MaxHp, target.Hp - amount));
            events.Add(new SimEvent(SimEventType.BuildingDamaged, Tick, sourceId, target.Id, target.Cell));
            if (target.Hp > 0) return;
            if (target.Kind == BuildingKind.Keep) return; // defeat is evaluated at the end of the step
            if (!grid.Release(target)) return; // occupancy is released exactly once, immediately
            Unregister(target);
            events.Add(new SimEvent(SimEventType.BuildingDestroyed, Tick, target.Id, sourceId, target.Cell));
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
            sb.Append("state=").Append(State).Append(" paused=").Append(Paused).Append(" tick=").Append(Tick)
                .Append(" acc=").Append(accumulatorNs).Append(" gold=").Append(Gold).Append(" rev=").Append(grid.Revision)
                .Append(" nextSpawn=").Append(nextSpawnIndex).Append(" nextB=").Append(nextBuildingId)
                .Append(" nextE=").Append(nextEnemyId).Append(" diag=").Append(diagnostics.Count).AppendLine();
            var buildings = new List<Building>(buildingsById.Values);
            buildings.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (var b in buildings)
                sb.Append("B").Append(b.Id).Append(' ').Append(b.Kind).Append(b.Cell).Append(" hp=").Append(b.Hp)
                    .Append(" tgt=").Append(b.TargetEnemyId).Append(" due=").Append(b.AttackDueTick).AppendLine();
            foreach (var e in enemies)
                sb.Append("E").Append(e.Id).Append(" entry=").Append(e.EntryIndex).Append(" hp=").Append(e.Hp)
                    .Append(" cell=").Append(e.Cell).Append(" next=").Append(e.NextCell).Append(" mv=").Append(e.Moving)
                    .Append(':').Append(e.MoveTicks).Append(" pos=").Append(e.X.ToString("R", inv)).Append(',')
                    .Append(e.Z.ToString("R", inv)).Append(" ri=").Append(e.RouteIndex).Append(" tgt=")
                    .Append(e.TargetBuildingId).Append(" due=").Append(e.AttackDueTick).AppendLine();
            return sb.ToString();
        }
    }
}
