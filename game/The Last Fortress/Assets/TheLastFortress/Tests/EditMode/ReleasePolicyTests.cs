using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace TheLastFortress.Tests
{
    /// <summary>
    /// Plan 008 G3 viability evidence: three distinct scripted approaches play the real twelve-night campaign on their map
    /// profile with legal purchases only (no granted gold or invulnerability). Each Day: (1) defense items from the
    /// defense queue until the Day's minimum count is met, (2) economy items while the Day is within the economy window and
    /// that minimum is met,
    /// (3) more defense, then a Keep repair below 75%. An illegal cell is skipped for good; an unaffordable item waits.
    /// Every night is step-bounded. Seeds 1-8 are played; only each policy's recorded seeds must win (balance is provisional
    /// and a 100% scripted win rate is not a goal). Losses are logged, not hidden. Log:
    /// TestResults/008/release-policy-runs.txt.
    /// </summary>
    public class ReleasePolicyTests
    {
        public sealed class Policy
        {
            public string Name;
            public string Map;
            public string[] Defense;
            public string[] Economy;

            /// <summary>
            /// Defense items owned at the start of each Night (index = Day - 1; the last value repeats). Counts were scaled by
            /// 1/0.7 when buildings became 30% cheaper and weaker, so the scripts keep the same share of gold on defense.
            /// </summary>
            public int[] MinDefense;

            public int EconomyUntilDay;
            public string[] Perks;

            /// <summary>Seeds (on this policy's profile) whose victory the test protects; other seeds are logged only.</summary>
            public uint[] Recorded;

            /// <summary>
            /// Spec 017: an adaptive script reads the board as played on Day 1 and returns its defense and economy items
            /// (<see cref="AdaptivePolicies"/>); they are not turned with the symmetry. Null for the fixed scripts.
            /// </summary>
            public Func<RunSimulation, (string[] defense, string[] economy)> Build;
        }

        // Kill zone where every route converges: an Archer ring, Cannons on the diagonals boosted by Watchtowers, a sealed Keep.
        public static readonly Policy KillZone = new Policy
        {
            Name = "kill-zone", Map = MapProfiles.Crossing, Recorded = new uint[] { 2, 3, 5 }, // seed 4 lost night 10 after the cheaper-buildings retune
            Defense = new[]
            {
                "Tower 9 9", "Tower 11 11", "Tower 9 11", "Tower 11 9", "Wall 9 10", "Wall 11 10", "Wall 10 9", "Wall 10 11",
                "Tower 8 10", "Tower 12 10", // cheaper Towers: two more before saving for Cannons
                "Cannon 7 7", "Watchtower 9 8", "Cannon 12 6", "Watchtower 11 8", "Cannon 7 12", "Watchtower 8 11",
                "Cannon 13 11", "Watchtower 12 11", "Tower 9 9 2", "Tower 11 11 2", "Tower 9 11 2", "Tower 11 9 2", "Tower 10 8",
                "Tower 10 12", "Cannon 7 7 2", "Cannon 12 6 2", "Tower 9 9 3", "Tower 11 11 3",
                "RepairStation 6 9", "Cannon 4 9", "Cannon 15 9", "Cannon 9 15", "Cannon 9 4",
                "Tower 7 10", "Tower 13 10", "Tower 10 7", "Tower 10 13", "Tower 6 11", "Tower 14 9",
            },
            Economy = new[] { "House 12 8", "House 8 12", "Mine vein0", "House 13 8", "Farm 12 13", "House 7 12", "Farm 6 13", "House 13 7", "Farm 15 7" },
            MinDefense = new[] { 4, 4, 6, 7, 11, 13, 16, 17, 20, 23, 26, 29 },
            EconomyUntilDay = 6,
            Perks = new[]
            {
                PerkRules.CounterBattery, PerkRules.Shrapnel, PerkRules.HeavyShot, PerkRules.Sharpshooters, PerkRules.SignalFires,
                PerkRules.Overwatch, PerkRules.FortressHeart, PerkRules.Gatehouse, PerkRules.BlastShields, PerkRules.ExpandedVision,
                PerkRules.FlamingArrows, PerkRules.ReinforcedTimber, PerkRules.HardenedRoofs, PerkRules.ThriftyConstruction,
                PerkRules.WarBonds, PerkRules.FocusedFire, PerkRules.LastStand, PerkRules.EfficientFarming, PerkRules.MarketSquare,
                PerkRules.Recycling, PerkRules.BarbedDefenses, PerkRules.KillZone, PerkRules.SpikedCorridor, PerkRules.FieldEngineers,
            },
        };

        // The ravine channels are the corridor: Towers line both channel edges (never blocking row 10), Spike Traps fill the
        // lane under their fire, Cannons hold the channel mouths and the central N/S gap.
        public static readonly Policy Corridor = new Policy
        {
            Name = "corridor", Map = MapProfiles.Ravine, Recorded = new uint[] { 1, 2, 5 },
            Defense = new[]
            {
                "Tower 8 9", "Tower 11 11", "Tower 8 11", "Tower 11 9", "SpikeTrap 7 10", "SpikeTrap 12 10", "Tower 6 11", "Tower 13 9",
                "SpikeTrap 6 10", "SpikeTrap 13 10", "SpikeTrap 5 10", "SpikeTrap 14 10", "Cannon 6 8", "Cannon 12 11",
                "Watchtower 8 8", "Watchtower 14 12", "Tower 4 9", "Tower 15 11", "Tower 8 9 2", "Tower 11 11 2", "SpikeTrap 4 10",
                "SpikeTrap 15 10", "SpikeTrap 7 10 2", "SpikeTrap 12 10 2", "Cannon 6 8 2", "Cannon 12 11 2",
                "Tower 9 12", "Tower 11 8", "SpikeTrap 10 12", "SpikeTrap 10 8", "Cannon 6 11", "Cannon 12 8", "RepairStation 7 14",
                "Tower 3 11", "Tower 16 9", "SpikeTrap 3 10", "SpikeTrap 16 10", "Tower 9 8", "Tower 11 12", "Tower 5 11", "Tower 14 9",
                "SpikeTrap 2 10", "SpikeTrap 17 10", "Tower 2 9", "Tower 17 11",
            },
            Economy = new[] { "House 13 15", "House 14 15", "Mine vein0", "Farm 13 16", "House 6 15", "House 7 15", "Farm 6 16", "House 15 4", "Farm 13 3" },
            MinDefense = new[] { 4, 6, 6, 7, 9, 11, 14, 17, 21, 26, 30, 34 },
            EconomyUntilDay = 7,
            Perks = new[]
            {
                PerkRules.KillZone, PerkRules.SpikedCorridor, PerkRules.Sharpshooters, PerkRules.FlamingArrows, PerkRules.ExpandedVision,
                PerkRules.CounterBattery, PerkRules.Overwatch, PerkRules.FortressHeart, PerkRules.Shrapnel, PerkRules.HeavyShot,
                PerkRules.BlastShields, PerkRules.HardenedRoofs, PerkRules.FocusedFire, PerkRules.ThriftyConstruction, PerkRules.Gatehouse,
                PerkRules.ReinforcedTimber, PerkRules.LastStand, PerkRules.WarBonds, PerkRules.EfficientFarming, PerkRules.MarketSquare,
                PerkRules.SignalFires, PerkRules.BarbedDefenses, PerkRules.Recycling, PerkRules.FieldEngineers,
            },
        };

        // Economy first on open land with a minimal Keep guard, then defenses spread over the farms with Repair Stations.
        public static readonly Policy Economy = new Policy
        {
            Name = "economy", Map = MapProfiles.Meadow, Recorded = new uint[] { 4, 5, 7 },
            Defense = new[]
            {
                "Tower 9 9", "Tower 11 11", "Tower 9 11", "Tower 11 9", "Wall 9 10", "Wall 11 10", "Wall 10 9", "Wall 10 11",
                "Tower 7 11", "Tower 13 9", "RepairStation 9 14", "Watchtower 8 11", "Cannon 4 9", "Cannon 15 10", "Tower 7 10",
                "Tower 13 10", "Cannon 9 4", "Cannon 10 15", "Tower 9 9 2", "Tower 11 11 2", "Tower 9 11 2", "Tower 11 9 2",
                "RepairStation 14 8", "Watchtower 12 10", "Cannon 4 9 2", "Cannon 15 10 2", "Tower 10 7", "Tower 10 13",
                "Cannon 4 13", "Cannon 15 6", "Tower 8 10", "Tower 12 11", "Tower 6 10", "Tower 14 10", "Tower 6 8", "Tower 14 12",
            },
            Economy = new[]
            {
                "House 8 12", "Mine vein0", "Mine vein1", "House 8 13", "Mine vein2", "Farm 6 12", "House 12 8", "House 13 8", "Farm 12 6",
                "Mine vein0 2", "Mine vein1 2", "House 12 12", "House 12 13",
                "Farm 13 12", "House 7 8", "House 8 7", "Farm 6 6", "House 7 13", "House 13 7", "House 8 12 2", "House 8 13 2", "Farm 6 12 2",
            },
            MinDefense = new[] { 3, 4, 4, 6, 6, 9, 11, 14, 17, 20, 23, 26 },
            EconomyUntilDay = 8,
            Perks = new[]
            {
                PerkRules.MarketSquare, PerkRules.EfficientFarming, PerkRules.WarBonds, PerkRules.HardenedRoofs, PerkRules.Sharpshooters,
                PerkRules.FieldEngineers, PerkRules.CounterBattery, PerkRules.FlamingArrows, PerkRules.ExpandedVision, PerkRules.FortressHeart,
                PerkRules.BlastShields, PerkRules.Overwatch, PerkRules.Shrapnel, PerkRules.HeavyShot, PerkRules.ThriftyConstruction,
                PerkRules.Gatehouse, PerkRules.SignalFires, PerkRules.ReinforcedTimber, PerkRules.FocusedFire, PerkRules.LastStand,
                PerkRules.Recycling, PerkRules.BarbedDefenses, PerkRules.KillZone, PerkRules.SpikedCorridor,
            },
        };

        // Spec 016 D5: the owner's 2026-10-29 playtest layout. Houses seal the Keep and ring it, Archer Towers stand around
        // them; no Walls, traps or Cannons. Its Houses are its defense, so they sit in the defense queue.
        public static readonly Policy HouseRing = new Policy
        {
            Name = "house-ring", Map = MapProfiles.Crossing, Recorded = new uint[0],
            Defense = new[]
            {
                "House 9 10", "House 11 10", "House 10 9", "House 10 11", "Tower 9 9", "Tower 11 11", "Tower 9 11", "Tower 11 9",
                "House 8 10", "House 12 10", "House 10 8", "House 10 12", "Tower 8 9", "Tower 12 11", "Tower 9 12", "Tower 11 8",
                "Tower 8 11", "Tower 12 9", "Tower 9 8", "Tower 11 12", "Tower 9 9 2", "Tower 11 11 2", "Tower 9 11 2", "Tower 11 9 2",
                "Tower 7 10", "Tower 13 10", "Tower 10 7", "Tower 10 13", "Tower 8 9 2", "Tower 12 11 2", "Tower 9 12 2", "Tower 11 8 2",
                "Tower 7 8", "Tower 13 12", "Tower 8 13", "Tower 12 7", "Tower 9 9 3", "Tower 11 11 3", "Tower 9 11 3", "Tower 11 9 3",
            },
            Economy = new[] { "Farm 12 12", "Farm 7 6", "House 8 8", "House 12 13", "Farm 6 12", "House 13 8", "Farm 15 7", "House 9 14" },
            MinDefense = new[] { 6, 8, 9, 11, 13, 15, 17, 20, 23, 26, 29, 32 },
            EconomyUntilDay = 7,
            Perks = new[]
            {
                PerkRules.MarketSquare, PerkRules.EfficientFarming, PerkRules.WarBonds, PerkRules.Sharpshooters, PerkRules.FlamingArrows,
                PerkRules.ExpandedVision, PerkRules.FortressHeart, PerkRules.HardenedRoofs, PerkRules.Overwatch, PerkRules.FocusedFire,
                PerkRules.CounterBattery, PerkRules.ThriftyConstruction, PerkRules.LastStand, PerkRules.BlastShields, PerkRules.Recycling,
                PerkRules.KillZone, PerkRules.Gatehouse, PerkRules.ReinforcedTimber, PerkRules.SignalFires, PerkRules.Shrapnel,
                PerkRules.HeavyShot, PerkRules.BarbedDefenses, PerkRules.SpikedCorridor, PerkRules.FieldEngineers,
            },
        };

        sealed class Queue
        {
            readonly string[] items;
            int next;
            public int Bought;

            public Queue(string[] items) => this.items = items;

            public bool Exhausted => next >= items.Length;

            /// <summary>
            /// Buys the next items while affordable; skips illegal cells for good. Stops on gold or <paramref name="limit"/>.
            /// Spec 015: "Kind x y tier" raises the building there to that tier and counts as one item; it is skipped when
            /// there is none or the rules have no tiers. Spec 016: "Mine veinN [tier]" means the map's N-th gold vein in (z,x)
            /// order, skipped when the map has fewer.
            /// </summary>
            public void Buy(RunSimulation sim, List<string> log, int limit = int.MaxValue)
            {
                while (next < items.Length && Bought < limit)
                {
                    var p = VeinCell(sim, items[next].Split(' '));
                    if (p == null)
                    {
                        log.Add($"{items[next]} SKIP no such vein");
                        next++;
                        continue;
                    }

                    var kind = (BuildingKind)Enum.Parse(typeof(BuildingKind), p[0]);
                    if (p.Length > 3)
                    {
                        if (!TryUpgradeItem(sim, kind, new GridPos(int.Parse(p[1]), int.Parse(p[2])), int.Parse(p[3]), out var skipped)) return;
                        log.Add(skipped ? $"{items[next]} SKIP" : $"upgrade {items[next]}");
                        if (!skipped) Bought++;
                        next++;
                        continue;
                    }

                    var r = sim.TryPlace(kind, new GridPos(int.Parse(p[1]), int.Parse(p[2])), out _);
                    if (r == PlacementRejection.InsufficientGold) return;
                    log.Add(r == PlacementRejection.None ? items[next] : $"{items[next]} SKIP {r}");
                    if (r == PlacementRejection.None) Bought++;
                    next++;
                }
            }
        }

        /// <summary>Spec 016: "Mine veinN ..." becomes "Mine x z ..." on the N-th vein in (z,x) order; null when there is none.</summary>
        static string[] VeinCell(RunSimulation sim, string[] p)
        {
            if (p.Length < 2 || !p[1].StartsWith("vein")) return p;
            var veins = sim.Config.Terrain?.Veins.OrderBy(v => v.Z).ThenBy(v => v.X).ToList() ?? new List<GridPos>();
            var n = int.Parse(p[1].Substring(4));
            if (n >= veins.Count) return null;
            var q = new List<string> { p[0], veins[n].X.ToString(), veins[n].Z.ToString() };
            q.AddRange(p.Skip(2));
            return q.ToArray();
        }

        /// <summary>Raises the building at <paramref name="cell"/> to <paramref name="tier"/>; false while a tier is unaffordable.</summary>
        static bool TryUpgradeItem(RunSimulation sim, BuildingKind kind, GridPos cell, int tier, out bool skipped)
        {
            var b = kind == BuildingKind.SpikeTrap ? sim.Grid.GetTrap(cell) : sim.Grid.GetOccupant(cell);
            skipped = b == null || b.Kind != kind || b.Cell != cell || b.Tier >= tier;
            while (!skipped && b.Tier < tier)
            {
                var r = sim.TryUpgrade(b);
                if (r == UpgradeRejection.InsufficientGold) return false;
                skipped = r != UpgradeRejection.None; // a profile without tiers
            }

            return true;
        }

        /// <summary>How a scripted run drives the hero (release-rules-2 only; plan 012 balance guard).</summary>
        public enum HeroScript
        {
            /// <summary>Never controlled: the hero guards its spawn cell and swings at enemies in reach.</summary>
            Idle,

            /// <summary>"Fight nearest": walks to the nearest enemy within <see cref="ActiveHeroLeash"/> of the Keep and attacks.</summary>
            Active,

            /// <summary>Plan 022 exploit probe: fights like <see cref="Active"/>, but after every contact runs off across the nearest Spike Trap.</summary>
            Kite,
        }

        /// <summary>The kiting script's memory: the last contact it reacted to and the retreat in progress.</summary>
        sealed class KiteState
        {
            public long LastContact = -1;
            public int RetreatSteps, RetreatFacing;
        }

        /// <summary>
        /// Plan 022: after each resolved contact, retreat for a fixed 40 steps (about 1.3 tiles, inside the 1.5 leash) toward
        /// and past the Spike Trap nearest the struck enemy (within 3 tiles), else straight away from it; then fight again. The
        /// retreat never waits for the enemy, so the script behaves the same whether or not the enemy may chase.
        /// </summary>
        static HeroCommand KiteHeroCommand(RunSimulation sim, KiteState state)
        {
            var hero = sim.Hero;
            if (hero == null || !hero.Alive) return HeroCommand.None;
            if (hero.ContactTick >= 0 && hero.ContactTick != state.LastContact && !hero.AttackPending)
            {
                state.LastContact = hero.ContactTick;
                Enemy struck = null;
                var best = double.MaxValue;
                foreach (var e in sim.Enemies)
                {
                    if (!e.Alive) continue;
                    var d = (e.X - hero.X) * (e.X - hero.X) + (e.Z - hero.Z) * (e.Z - hero.Z);
                    if (d < best) (best, struck) = (d, e);
                }

                if (struck != null)
                {
                    double dx = hero.X - struck.X, dz = hero.Z - struck.Z, trapBest = 9.0;
                    for (var x = 0; x < GridCoordinates.Size; x++)
                    for (var z = 0; z < GridCoordinates.Size; z++)
                    {
                        if (!sim.Grid.HasTrap(new GridPos(x, z))) continue;
                        double tx = x + 0.5 - struck.X, tz = z + 0.5 - struck.Z, t = tx * tx + tz * tz;
                        if (t >= trapBest || t < 1e-9) continue;
                        (trapBest, dx, dz) = (t, tx, tz);
                    }

                    state.RetreatFacing = HeroRules.FacingToward(dx, dz);
                    state.RetreatSteps = 40;
                }
            }

            if (state.RetreatSteps <= 0) return ActiveHeroCommand(sim);
            state.RetreatSteps--;
            return HeroCommand.Fine(state.RetreatFacing, false, state.RetreatFacing);
        }

        public const double ActiveHeroLeash = 6.0;

        /// <summary>The active script's command for this step: chase and hit the nearest enemy near the Keep, else return to spawn.</summary>
        public static HeroCommand ActiveHeroCommand(RunSimulation sim)
        {
            var hero = sim.Hero;
            if (hero == null || !hero.Alive) return HeroCommand.None;
            Enemy target = null;
            var best = double.MaxValue;
            foreach (var e in sim.Enemies)
            {
                if (!e.Alive) continue;
                double kx = e.X - (GridState.KeepCell.X + 0.5), kz = e.Z - (GridState.KeepCell.Z + 0.5);
                if (kx * kx + kz * kz > ActiveHeroLeash * ActiveHeroLeash) continue;
                double dx = e.X - hero.X, dz = e.Z - hero.Z;
                var d = dx * dx + dz * dz;
                if (d >= best) continue;
                best = d;
                target = e;
            }

            var contact = hero.Profile.ContactPhase;
            if (target != null)
            {
                double dx = target.X - hero.X, dz = target.Z - hero.Z;
                var facing = HeroRules.FacingToward(dx, dz);
                if (!contact)
                {
                    var heading = best <= 0.45 * 0.45 ? -1 : HeadingToward(dx, dz);
                    return new HeroCommand(heading, true, facing);
                }

                // release-rules-4: close to three quarters of the reach (the motor brakes within about 0.02 tile), and press
                // attack only within reach, so an accepted swing can land at its contact three steps later.
                var reach = hero.Profile.SwingReach;
                var stop = best <= reach * 0.75 * (reach * 0.75);
                return HeroCommand.Fine(stop ? -1 : facing, best <= reach * reach, facing);
            }

            var home = sim.HeroSpawnCell(GridState.KeepCell);
            double hx = home.X + 0.5 - hero.X, hz = home.Z + 0.5 - hero.Z;
            if (contact)
                return hx * hx + hz * hz < 0.2 * 0.2 ? HeroCommand.Fine(-1, false, hero.Facing) : HeroCommand.Fine(HeroRules.FacingToward(hx, hz), false, HeroRules.FacingToward(hx, hz));
            return hx * hx + hz * hz < 0.2 * 0.2 ? new HeroCommand(-1, false, hero.Facing) : new HeroCommand(HeadingToward(hx, hz), false, HeroRules.FacingToward(hx, hz));
        }

        static int HeadingToward(double dx, double dz) => (HeroRules.FacingToward(dx, dz) + 2) / 4 % 16;

        /// <summary>
        /// Plays <paramref name="policy"/> on the board turned by <paramref name="symmetry"/>, entries turned to match. The
        /// scripts' coordinates are written for each profile's own orientation (symmetry 0); for other orientations they are
        /// turned about the Keep, so the Keep ring stays intact while terrain-side items may land a cell off (or be skipped).
        /// Under <c>release-rules-2</c> the hero follows <paramref name="hero"/>; <paramref name="build"/> false buys nothing.
        /// </summary>
        /// <summary>Plan 019 hero evidence collected over one scripted run.</summary>
        public sealed class HeroLog
        {
            public int Deaths, Contacts, Misses, KeepDamage;

            /// <summary>Plan 022: chases started and Spike Trap hits while off the route, per enemy ID; enemy steps on and off the route.</summary>
            public readonly Dictionary<int, int> Chases = new Dictionary<int, int>(), OffRouteTraps = new Dictionary<int, int>();

            public readonly HashSet<int> Chasing = new HashSet<int>(), OffRoute = new HashSet<int>();
            public long EnemySteps, OffRouteSteps;
        }


        public static RunSimulation Play(Policy policy, uint seed, StringBuilder log, int symmetry = 0, string rules = ReleaseCampaign.RulesV1,
            HeroScript hero = HeroScript.Idle, bool build = true, HeroLog heroLog = null)
        {
            var sim = new RunSimulation(new RunConfig
            {
                // Each rules version plays with its own profile's definitions (release-rules-3: walking speeds, more HP).
                Definitions = rules == ReleaseCampaign.RulesV4 ? ReleaseCampaign.Contact(ReleaseDefinitions.CreateDefault()) : ReleaseCampaign.WalkingEnemies(rules) ? ReleaseCampaign.Walking(ReleaseDefinitions.CreateDefault()) : ReleaseDefinitions.CreateDefault(),
                Seed = seed, Terrain = MapProfiles.Generate(policy.Map, seed, symmetry), Campaign = ReleaseCampaign.Create(seed, policy.Map, symmetry, rules),
            });
            log.AppendLine($"## {policy.Name} on {sim.Config.Terrain.Describe()} ({rules}{(sim.HasHero ? $", hero {hero}" : "")}{(build ? "" : ", no construction")})");
            var plan = policy.Build?.Invoke(sim);
            var defense = new Queue(plan?.defense ?? policy.Defense.Select(i => TurnAboutKeep(i, symmetry)).ToArray());
            var economy = new Queue(plan?.economy ?? policy.Economy.Select(i => TurnAboutKeep(i, symmetry)).ToArray());
            var combat = 0.0;
            var (fuses, blasts, batch) = (0, 0, new List<SimEvent>()); // Bomber role check (plan 015 review)
            var kite = hero == HeroScript.Kite ? new KiteState() : null;
            var upgrades = 0;
            for (var day = 1; day <= sim.TotalNights && !sim.IsTerminal; day++)
            {
                var bought = new List<string>();
                var minimum = policy.MinDefense[Math.Min(day, policy.MinDefense.Length) - 1];
                if (build)
                {
                    defense.Buy(sim, bought, minimum);
                    // Economy only once the Day's defense minimum is met: cheap Houses must not starve a pending Cannon.
                    if (day <= policy.EconomyUntilDay && (defense.Bought >= minimum || defense.Exhausted)) economy.Buy(sim, bought);
                    defense.Buy(sim, bought);
                }
                // Repair the Keep below 75% when the full repair costs at most half the gold, or always below 25%.
                sim.RepairQuote(sim.Keep, out var restored, out var cost);
                if (restored > 0 && (sim.Keep.Hp * 4 < sim.Keep.MaxHp || (sim.Keep.Hp * 4 < sim.Keep.MaxHp * 3 && restored == sim.Keep.MaxHp - sim.Keep.Hp && cost * 2 <= sim.Gold)))
                {
                    sim.TryRepair(sim.Keep);
                    bought.Add($"repair Keep +{restored}");
                }

                upgrades += bought.Count(b => b.StartsWith("upgrade "));
                log.AppendLine($"Day {day}: gold left {sim.Gold}; {string.Join("; ", bought)}");
                Assert.IsTrue(sim.StartNight(), $"{policy.Name} seed {seed}: night {day} starts: {string.Join("; ", sim.Diagnostics)}");
                var steps = 0;
                while (sim.State == RunState.Night)
                {
                    Assert.Less(++steps, 60000, $"{policy.Name} seed {seed}: night {day} exceeded the step bound");
                    sim.Step(!sim.HasHero ? HeroCommand.None : hero == HeroScript.Active ? ActiveHeroCommand(sim) : kite != null ? KiteHeroCommand(sim, kite) : HeroCommand.None);
                    batch.Clear();
                    sim.DrainEvents(batch);
                    fuses += batch.Count(e => e.Type == SimEventType.FuseLit);
                    blasts += batch.Count(e => e.Type == SimEventType.BomberDetonated);
                    if (heroLog == null) continue;
                    foreach (var t in batch)
                    {
                        if (t.Type != SimEventType.TrapTriggered) continue;
                        // A trap may kill its victim, which is then already removed: use its state at the end of the last step.
                        var victim = sim.Enemies.FirstOrDefault(x => x.Id == t.TargetId);
                        if (victim != null ? victim.Pursuing || victim.Returning : heroLog.OffRoute.Contains(t.TargetId))
                            heroLog.OffRouteTraps[t.TargetId] = heroLog.OffRouteTraps.TryGetValue(t.TargetId, out var n) ? n + 1 : 1;
                    }

                    heroLog.OffRoute.Clear();
                    foreach (var e in sim.Enemies)
                    {
                        if (!e.Alive) continue;
                        heroLog.EnemySteps++;
                        if (e.Pursuing || e.Returning)
                        {
                            heroLog.OffRouteSteps++;
                            heroLog.OffRoute.Add(e.Id);
                        }

                        if (!e.Pursuing) heroLog.Chasing.Remove(e.Id);
                        else if (heroLog.Chasing.Add(e.Id)) heroLog.Chases[e.Id] = heroLog.Chases.TryGetValue(e.Id, out var n) ? n + 1 : 1;
                    }

                    foreach (var e in batch)
                        if (e.Type == SimEventType.HeroDowned) heroLog.Deaths++;
                        else if (e.Type == SimEventType.HeroContact)
                        {
                            heroLog.Contacts++;
                            if (e.TargetId == 0) heroLog.Misses++;
                        }
                }

                if (heroLog != null) heroLog.KeepDamage += sim.LastNight.KeepDamage;

                combat += sim.NightElapsedSeconds;
                var recap = sim.LastNight;
                log.AppendLine($"  Night {day}: {sim.State} {sim.NightElapsedSeconds:0.0}s; Keep -{recap.KeepDamage} ({sim.Keep.Hp}/{sim.Keep.MaxHp}); lost {recap.BuildingsLost}; killed {recap.EnemiesDefeated}" +
                               (sim.HasHero ? $" (hero {recap.HeroKills})" : ""));
                if (sim.State != RunState.Reward) continue;
                var offer = sim.CurrentOffer;
                var pick = policy.Perks.First(id => offer.PerkIds.Contains(id));
                Assert.IsTrue(sim.ChoosePerk(offer.OfferId, pick));
                log.AppendLine($"  Reward {string.Join(",", offer.PerkIds)} -> {pick}; dawn income +{sim.LastIncome.Total}");
            }

            log.AppendLine($"Result: {sim.State}; nights {sim.Stats.NightsCleared}/12; combat {combat / 60:0.0} min; defeated {sim.Stats.EnemiesDefeated}; " +
                           $"bought {sim.Stats.BuildingsPurchased} (defense {defense.Bought}, economy {economy.Bought}); earned {sim.Stats.GoldEarned}; perks [{string.Join(",", sim.OwnedPerks)}]; Bomber fuses {fuses}, blasts {blasts}" +
                           (upgrades > 0 ? $"; upgrades {upgrades}" : ""));
            log.AppendLine();
            return sim;
        }

        /// <summary>
        /// Plan 012 balance guard, extended by plan 019 (specs/003 section 8) to compare release-rules-3 with release-rules-4 on
        /// the same seeds 1-8 and the three approaches, with an idle hero, an active hero and a hero without construction.
        /// Asserted: a hero with no construction loses every seed by Night 3 under both. Logged: release-rules-1 (control),
        /// wins, Keep damage, hero deaths, the hero's share of kills on Nights 6-12 and, for release-rules-4, contacts and
        /// misses (a script swinging out of reach would show as misses). Balance is provisional and not frozen here.
        /// Log: TestResults/019/hero-policy-runs.txt.
        /// </summary>
        [Test]
        public void AHeroAloneCannotHoldTheFortressAndItsEffectIsLogged()
        {
            var log = new StringBuilder();
            var summary = new List<string>();
            var failures = new List<string>();
            foreach (var policy in new[] { KillZone, Corridor, Economy })
            {
                var controls = new Dictionary<uint, RunSimulation>();
                foreach (var rules in new[] { ReleaseCampaign.RulesV3, ReleaseCampaign.RulesV4 })
                {
                    int matches = 0, nights = 0, idleWins = 0, activeWins = 0, aloneMax = 0;
                    double share = 0, peak = 0;
                    var idleLog = new HeroLog();
                    var activeLog = new HeroLog();
                    for (uint seed = 1; seed <= 8; seed++)
                    {
                        var symmetry = MapProfiles.SymmetryFor(seed);
                        if (!controls.TryGetValue(seed, out var control)) controls[seed] = control = Play(policy, seed, log, symmetry);
                        var idle = Play(policy, seed, log, symmetry, rules, HeroScript.Idle, heroLog: idleLog);
                        var activeText = new StringBuilder();
                        var active = Play(policy, seed, activeText, symmetry, rules, HeroScript.Active, heroLog: activeLog);
                        log.Append(activeText);
                        if ((control.State, control.NightNumber) == (idle.State, idle.NightNumber)) matches++;
                        if (idle.State == RunState.Victory) idleWins++;
                        if (active.State == RunState.Victory) activeWins++;
                        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(activeText.ToString(), @"Night (\d+): \S+ .*killed (\d+) \(hero (\d+)\)"))
                        {
                            int night = int.Parse(m.Groups[1].Value), killed = int.Parse(m.Groups[2].Value), hero = int.Parse(m.Groups[3].Value);
                            if (night < 6 || killed == 0) continue;
                            share += (double)hero / killed;
                            peak = Math.Max(peak, (double)hero / killed);
                            nights++;
                        }

                        summary.Add($"{policy.Name} {rules} seed {seed}: control {control.State}@{control.NightNumber}; idle hero {idle.State}@{idle.NightNumber} " +
                                    $"(hero {idle.Stats.HeroKills}/{idle.Stats.EnemiesDefeated}); active hero {active.State}@{active.NightNumber} (hero {active.Stats.HeroKills}/{active.Stats.EnemiesDefeated})");

                        var alone = Play(policy, seed, log, symmetry, rules, HeroScript.Active, build: false);
                        aloneMax = Math.Max(aloneMax, alone.NightNumber);
                        if (alone.State != RunState.Defeat || alone.NightNumber > 3) failures.Add($"{rules} {policy.Map} seed {seed}: hero alone reached night {alone.NightNumber} ({alone.State})");
                    }

                    summary.Add($"{policy.Name} {rules}: wins idle {idleWins}/8, active {activeWins}/8; idle matches control on {matches}/8; " +
                                $"active share of kills on Nights 6-12 average {share / Math.Max(1, nights):P0}, peak {peak:P0}; Keep damage idle {idleLog.KeepDamage}, active {activeLog.KeepDamage}; " +
                                $"hero deaths idle {idleLog.Deaths}, active {activeLog.Deaths}; contacts (misses) idle {idleLog.Contacts} ({idleLog.Misses}), active {activeLog.Contacts} ({activeLog.Misses}); " +
                                $"hero alone falls by night {aloneMax}");
                }
            }

            log.Insert(0, "# Plan 019 hero policy runs, release-rules-3 vs release-rules-4 (seeds' own orientations)\n" + string.Join("\n", summary) + "\n\n");
            Directory.CreateDirectory(Path.Combine("TestResults", "019"));
            File.WriteAllText(Path.Combine("TestResults", "019", "hero-policy-runs.txt"), log.ToString());
            TestContext.WriteLine(string.Join(Environment.NewLine, summary));
            CollectionAssert.IsEmpty(failures, "a hero without buildings must lose by Night 3");
        }

        /// <summary>
        /// Plan 022 exploit probe (specs/008 section 7): the corridor approach (Spike Traps along row 10) on release-rules-4,
        /// seeds 1-8 on their own orientations, with a hero that kites enemies across traps. Logs chases per enemy, Spike Trap
        /// hits while off the route, and the share of enemy steps spent off the route. Evidence only: nothing is asserted.
        /// Log: TestResults/022/kite-runs.txt.
        /// </summary>
        [Test]
        public void KitingEnemiesAcrossTrapsIsLogged()
        {
            var log = new StringBuilder();
            var summary = new List<string>();
            foreach (var seed in Enumerable.Range(1, 8).Select(s => (uint)s))
            {
                var heroLog = new HeroLog();
                var sim = Play(Corridor, seed, log, MapProfiles.SymmetryFor(seed), ReleaseCampaign.RulesV4, HeroScript.Kite, heroLog: heroLog);
                summary.Add($"seed {seed}: {sim.State}@{sim.NightNumber}; chases {heroLog.Chases.Values.Sum()} by {heroLog.Chases.Count} enemies, " +
                            $"most by one enemy {(heroLog.Chases.Count == 0 ? 0 : heroLog.Chases.Values.Max())}; off-route trap hits {heroLog.OffRouteTraps.Values.Sum()}, " +
                            $"most on one enemy {(heroLog.OffRouteTraps.Count == 0 ? 0 : heroLog.OffRouteTraps.Values.Max())}; enemy steps off route " +
                            $"{(double)heroLog.OffRouteSteps / Math.Max(1, heroLog.EnemySteps):P1}; hero kills {sim.Stats.HeroKills}/{sim.Stats.EnemiesDefeated}");
            }

            log.Insert(0, "# Plan 022 kiting hero, corridor, release-rules-4 (seeds' own orientations)\n" + string.Join("\n", summary) + "\n\n");
            Directory.CreateDirectory(Path.Combine("TestResults", "022"));
            File.WriteAllText(Path.Combine("TestResults", "022", "kite-runs.txt"), log.ToString());
            TestContext.WriteLine(string.Join(Environment.NewLine, summary));
        }

        /// <summary>"Kind x z" with its footprint turned about the Keep cell (mirror first, then quarter turns, as the map).</summary>
        static string TurnAboutKeep(string item, int s)
        {
            if (s == 0 || item.Contains(" vein")) return item; // spec 016: veins are already on the board as played
            var p = item.Split(' ');
            var kind = (BuildingKind)Enum.Parse(typeof(BuildingKind), p[0]);
            var k = GridState.KeepCell;
            var cells = BuildingDefinition.FootprintCells(kind, new GridPos(int.Parse(p[1]), int.Parse(p[2]))).Select(c =>
            {
                int dx = c.X - k.X, dz = c.Z - k.Z;
                if (s >= 4) dx = -dx;
                for (var i = 0; i < (s & 3); i++) (dx, dz) = (-dz, dx);
                return new GridPos(k.X + dx, k.Z + dz);
            }).ToList();
            return $"{p[0]} {cells.Min(c => c.X)} {cells.Min(c => c.Z)}" + (p.Length > 3 ? " " + p[3] : "");
        }

        [Test]
        public void ThreeDistinctApproachesCompleteTwelveNightsOnTheirProfiles()
        {
            var log = new StringBuilder();
            var outcomes = new List<string>();
            var failures = new List<string>();
            foreach (var policy in new[] { KillZone, Corridor, Economy })
            {
                for (uint seed = 1; seed <= 8; seed++)
                {
                    var sim = Play(policy, seed, log);
                    outcomes.Add($"{policy.Name} seed {seed}: {sim.State} after night {sim.NightNumber}, Keep {sim.Keep.Hp}/{sim.Keep.MaxHp}");
                    if (policy.Recorded.Contains(seed) && sim.State != RunState.Victory) failures.Add($"{policy.Name} seed {seed}");
                }
            }

            // Evidence only: the same seeds on the orientation players actually get (plan 010). Not asserted, because the
            // scripts were tuned for symmetry 0 and turning them about the Keep is approximate near terrain.
            var turned = new List<string>();
            foreach (var policy in new[] { KillZone, Corridor, Economy })
            {
                var wins = 0;
                for (uint seed = 1; seed <= 8; seed++)
                {
                    var symmetry = MapProfiles.SymmetryFor(seed);
                    var sim = Play(policy, seed, log, symmetry);
                    if (sim.State == RunState.Victory) wins++;
                    turned.Add($"{policy.Name} seed {seed} (symmetry {symmetry}): {sim.State} after night {sim.NightNumber}, Keep {sim.Keep.Hp}/{sim.Keep.MaxHp}");
                }

                turned.Add($"{policy.Name}: {wins}/8 on the seeds' own orientations");
            }

            outcomes.Add("");
            outcomes.Add("Seeds' own orientations (log only):");
            outcomes.AddRange(turned);

            log.Insert(0, "# Plan 008 release policy runs (real budget, twelve nights)\n" + string.Join("\n", outcomes) + "\n\n");
            Directory.CreateDirectory(Path.Combine("TestResults", "008"));
            File.WriteAllText(Path.Combine("TestResults", "008", "release-policy-runs.txt"), log.ToString());
            TestContext.WriteLine(string.Join(Environment.NewLine, outcomes));
            CollectionAssert.IsEmpty(failures, "recorded seeds must stay winnable by each approach");
        }
    }
}
