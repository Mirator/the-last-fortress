using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static TheLastFortress.Tests.SimTestUtil;

namespace TheLastFortress.Tests
{
    /// <summary>
    /// Spec 016 under release-rules-4: raids on economy buildings beside a route, gold veins and the Mine. A Grunt from the
    /// West walks row 10 to the Keep on the flat board; Houses at (3,11) and (6,11) stand beside that route.
    /// </summary>
    public class RaidAndMineTests
    {
        static readonly HeroCommand Still = new HeroCommand(-1, false, 0);
        static readonly int RaidTicks = PrototypeDefinitions.ToTicks(ReleaseCampaign.RaidSeconds);

        static RunSimulation NewRaidRun(EnemyKind kind = EnemyKind.Grunt, TerrainMap terrain = null)
        {
            var wave = new WavePlan("w", new[] { new SpawnEntry(0, MvpCampaign.West, 0, kind) }, new[] { MvpCampaign.West });
            var defs = ReleaseCampaign.Contact(ReleaseDefinitions.CreateDefault());
            defs.StartingGold = 2000;
            return new RunSimulation(new RunConfig
            {
                Definitions = defs, Wave = wave, Terrain = terrain,
                Campaign = new CampaignConfig
                {
                    Label = "raid test", Nights = new[] { wave }, OffersRewards = false, RulesVersion = ReleaseCampaign.RulesV4,
                    CampaignId = ReleaseCampaign.CampaignId, PerkPool = PerkRules.ReleaseIds,
                },
            });
        }

        static TerrainMap FlatWithVeins(params (int x, int z)[] veins)
        {
            var map = TerrainMap.Flat();
            foreach (var v in veins) map.Veins.Add(new GridPos(v.x, v.z));
            return map;
        }

        /// <summary>Steps (the hero still) until an event matches; fails with the tick after <paramref name="max"/> steps.</summary>
        static SimEvent StepUntilEvent(RunSimulation sim, System.Func<SimEvent, bool> match, int max = 4000)
        {
            for (var i = 0; i < max && sim.State == RunState.Night; i++)
            {
                sim.Step(Still);
                foreach (var e in Drain(sim))
                    if (match(e)) return e;
            }

            Assert.Fail($"event not seen within {max} steps (state {sim.State}, tick {sim.Tick})");
            return default;
        }

        [Test]
        public void ARaidLastsTheRaidTimeOnceThenTheRaiderWalksOnPastTheNextHouse()
        {
            var sim = NewRaidRun();
            var first = Place(sim, BuildingKind.House, 3, 11);
            var second = Place(sim, BuildingKind.House, 6, 11);
            Assert.IsTrue(sim.StartNight());
            var grunt = sim.Enemies[0];
            var start = StepUntilEvent(sim, e => e.Type == SimEventType.RaidStarted);
            Assert.AreEqual((grunt.Id, first.Id, new GridPos(3, 10)), (start.SourceId, start.TargetId, grunt.Cell));
            var end = StepUntilEvent(sim, e => e.Type == SimEventType.RaidEnded);
            Assert.AreEqual(start.Tick + RaidTicks, end.Tick, "the raid ends when its time runs out");
            Assert.IsTrue(first.IsAlive && first.Hp < first.MaxHp, "the raided House was hit and survived");

            var raids = 0;
            for (var i = 0; i < 4000 && grunt.TargetBuildingId != sim.Keep.Id; i++)
            {
                sim.Step(Still);
                raids += Drain(sim).Count(e => e.Type == SimEventType.RaidStarted);
            }

            Assert.AreEqual(sim.Keep.Id, grunt.TargetBuildingId, "it walked on to the Keep");
            Assert.AreEqual(0, raids, "one raid per night: it passed (6,10) beside the second House");
            Assert.AreEqual(second.MaxHp, second.Hp);
        }

        [Test]
        public void ARaidEndsWhenItsTargetFalls()
        {
            var sim = NewRaidRun();
            var house = Place(sim, BuildingKind.House, 3, 11);
            house.Hp = 5; // one Grunt hit
            Assert.IsTrue(sim.StartNight());
            var start = StepUntilEvent(sim, e => e.Type == SimEventType.RaidStarted);
            var end = StepUntilEvent(sim, e => e.Type == SimEventType.RaidEnded);
            Assert.IsFalse(house.IsAlive);
            Assert.Less(end.Tick, start.Tick + RaidTicks, "it did not wait out the raid time at a ruin");
            var grunt = sim.Enemies[0];
            StepUntil(sim, () => grunt.Cell.X > 3, 200);
        }

        [TestCase(EnemyKind.Bomber)]
        [TestCase(EnemyKind.Archer)]
        [TestCase(EnemyKind.SiegeGolem)]
        public void BombersArchersAndBossesNeverRaid(EnemyKind kind)
        {
            var sim = NewRaidRun(kind);
            Place(sim, BuildingKind.House, 3, 11);
            Assert.IsTrue(sim.StartNight());
            var enemy = sim.Enemies[0];
            for (var i = 0; i < 2000 && sim.State == RunState.Night && enemy.Alive && enemy.Cell.X < 5; i++)
            {
                sim.Step(Still);
                Assert.IsFalse(Drain(sim).Any(e => e.Type == SimEventType.RaidStarted), $"{kind} raided at tick {sim.Tick}");
            }
        }

        /// <summary>A raid only makes the enemy wait: it walks the same cells as without the House beside its route.</summary>
        [Test]
        public void ARaidNeverChangesTheRoute()
        {
            List<GridPos> Walk(bool house)
            {
                var sim = NewRaidRun();
                if (house) Place(sim, BuildingKind.House, 3, 11);
                Assert.IsTrue(sim.StartNight());
                var grunt = sim.Enemies[0];
                var cells = new List<GridPos> { grunt.Cell };
                var raided = false;
                for (var i = 0; i < 4000 && grunt.TargetBuildingId != sim.Keep.Id; i++)
                {
                    sim.Step(Still);
                    raided |= Drain(sim).Any(e => e.Type == SimEventType.RaidStarted);
                    if (cells[cells.Count - 1] != grunt.Cell) cells.Add(grunt.Cell);
                }

                Assert.AreEqual(house, raided, "scenario: only the run with the House raids");
                return cells;
            }

            CollectionAssert.AreEqual(Walk(false), Walk(true));
        }

        /// <summary>v0.3: the hero ends a raid. Struck while raiding, the Grunt chases; back on its route it never raids again.</summary>
        [Test]
        public void TheHeroEndsARaidAndItIsNotResumed()
        {
            var sim = NewRaidRun();
            var house = Place(sim, BuildingKind.House, 3, 11);
            Assert.IsTrue(sim.StartNight());
            var grunt = sim.Enemies[0];
            var start = StepUntilEvent(sim, e => e.Type == SimEventType.RaidStarted);
            var hero = sim.Hero;
            int ux = (int)System.Math.Round(grunt.X * 1200), uz = (int)System.Math.Round(grunt.Z * 1200) - 168; // touching, south
            hero.UnitsX = hero.PrevUnitsX = ux;
            hero.UnitsZ = hero.PrevUnitsZ = uz;
            hero.VelX = hero.VelZ = 0;
            sim.Step(HeroCommand.Fine(-1, true, 0));
            var end = StepUntilEvent(sim, e => e.Type == SimEventType.RaidEnded, hero.Profile.WindUpTicks + 5);
            Assert.Less(end.Tick, start.Tick + RaidTicks, "ended by the hero, not by its time");
            Assert.AreEqual(-1, grunt.RaidBuildingId);

            var hp = house.Hp;
            var raids = 0;
            for (var i = 0; i < 6000 && grunt.Alive && grunt.TargetBuildingId != sim.Keep.Id; i++)
            {
                sim.Step(HeroCommand.Fine(0, false, 0)); // the hero runs north, out of the leash
                raids += Drain(sim).Count(e => e.Type == SimEventType.RaidStarted);
            }

            Assert.AreEqual(0, raids, "the raid is spent");
            Assert.AreEqual(hp, house.Hp, "the House is not attacked again");
        }

        [Test]
        public void AMineStandsOnlyOnAGoldVeinAndEarnsByTier()
        {
            var sim = NewRaidRun(terrain: FlatWithVeins((3, 11)));
            Assert.AreEqual(PlacementRejection.NoVein, sim.TryPlace(BuildingKind.Mine, new GridPos(4, 11), out _));
            var mine = Place(sim, BuildingKind.Mine, 3, 11);
            var income = new List<int> { sim.IncomeForecast().Mines };
            Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(mine));
            income.Add(sim.IncomeForecast().Mines);
            Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(mine));
            income.Add(sim.IncomeForecast().Mines);
            var d = sim.Definitions.Mine;
            CollectionAssert.AreEqual(new[] { d.DailyIncome, d.DailyIncome + d.Tiers[0].IncomeBonus, d.DailyIncome + d.Tiers[1].IncomeBonus }, income);
        }

        /// <summary>Stockade (tier III): the raider's hits on the Mine are halved; tier II takes them in full.</summary>
        [TestCase(2, 10)]
        [TestCase(3, 5)]
        public void ATierThreeMineHalvesItsRaidersHits(int tier, int expected)
        {
            var sim = NewRaidRun(terrain: FlatWithVeins((3, 11)));
            var mine = Place(sim, BuildingKind.Mine, 3, 11);
            while (mine.Tier < tier) Assert.AreEqual(UpgradeRejection.None, sim.TryUpgrade(mine));
            Assert.AreEqual(10, sim.Definitions.Grunt.AttackDamage, "scenario: a 10-damage Grunt tells halving apart");
            Assert.IsTrue(sim.StartNight());
            StepUntilEvent(sim, e => e.Type == SimEventType.RaidStarted);
            var hp = mine.Hp;
            StepUntil(sim, () => mine.Hp < hp, 100);
            Assert.AreEqual(expected, hp - mine.Hp);
        }

        /// <summary>
        /// Veins on every profile over 50 seeds: deterministic, at most three, at least one, each on buildable land at
        /// Chebyshev 5+ from the Keep, off every open route but within 2 of one, and 3+ apart.
        /// </summary>
        [Test]
        public void GoldVeinsAreDeterministicAndKeepTheirPlacementRules()
        {
            var fewest = int.MaxValue;
            foreach (var profile in MapProfiles.All)
            for (uint seed = 1; seed <= 50; seed++)
            {
                var map = MapProfiles.Generate(profile, seed);
                CollectionAssert.AreEqual(map.Veins, MapProfiles.Generate(profile, seed).Veins, $"{profile} {seed}: deterministic");
                fewest = System.Math.Min(fewest, map.Veins.Count);
                Assert.LessOrEqual(map.Veins.Count, MapProfiles.VeinCount);
                var grid = new GridState(map.ImpassableCells());
                grid.Install(new Building(1, PrototypeDefinitions.CreateDefault().Keep, GridState.KeepCell, 0));
                var route = new HashSet<GridPos>();
                foreach (var entry in new[] { MvpCampaign.West, MvpCampaign.East, MvpCampaign.North, MvpCampaign.South })
                    route.UnionWith(new RoutePlanner().Plan(grid, entry, null).Cells);
                int Cheb(GridPos a, GridPos b) => System.Math.Max(System.Math.Abs(a.X - b.X), System.Math.Abs(a.Z - b.Z));
                foreach (var v in map.Veins)
                {
                    var at = $"{profile} seed {seed} vein {v}";
                    Assert.IsTrue(map[v] == TerrainType.Land && !GridCoordinates.IsReservedRing(v), at);
                    Assert.GreaterOrEqual(Cheb(v, GridState.KeepCell), MapProfiles.VeinKeepDistance, at);
                    Assert.IsFalse(route.Contains(v), at + " on a route");
                    Assert.IsTrue(route.Any(r => Cheb(r, v) <= MapProfiles.VeinRouteReach), at + " far from every route");
                    Assert.IsTrue(map.Veins.All(o => o == v || Cheb(o, v) >= MapProfiles.VeinSpacing), at + " too close to another");
                }
            }

            Assert.GreaterOrEqual(fewest, 1, "every map has a vein");
        }

        [Test]
        public void VeinsAndMinesRoundTripThroughSavesAndOlderSavesHaveNone()
        {
            var catalog = new RulesCatalog(RulesProfile.TwelveNightsContact(ReleaseDefinitions.CreateDefault()));
            var terrain = MapProfiles.Generate(MapProfiles.Meadow, 3);
            Assert.IsNotEmpty(terrain.Veins, "scenario: the map has a vein");
            var sim = new RunSimulation(new RunConfig
            {
                Definitions = catalog.Find(ReleaseCampaign.RulesV4, ReleaseCampaign.CampaignId).Definitions, Seed = 3,
                Terrain = terrain, Campaign = ReleaseCampaign.Create(3, MapProfiles.Meadow, ReleaseCampaign.RulesV4),
            });
            var vein = terrain.Veins[0];
            var mine = Place(sim, BuildingKind.Mine, vein.X, vein.Z);
            var json = JsonUtility.ToJson(CheckpointCodec.Capture(sim));

            var back = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(json), catalog, out var problem, out var message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            CollectionAssert.AreEqual(terrain.Veins, back.Config.Terrain.Veins);
            Assert.AreEqual(BuildingKind.Mine, back.GetBuilding(mine.Id).Kind);

            var noVeins = System.Text.RegularExpressions.Regex.Replace(json, "\"veins\":\\[[^\\]]*\\]", "\"veins\":[]");
            Assert.AreNotEqual(json, noVeins);
            CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(noVeins), catalog, out problem, out message);
            Assert.AreEqual(CheckpointProblem.Invalid, problem, "a Mine off a vein is refused");
            StringAssert.Contains("not on a gold vein", message);

            Assert.IsTrue(sim.TrySell(mine) == SellRejection.None);
            var older = System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(CheckpointCodec.Capture(sim)), ",?\"veins\":\\[[^\\]]*\\]", "");
            Assert.IsFalse(older.Contains("\"veins\""), "scenario: a save from before veins");
            back = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(older), catalog, out problem, out message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            Assert.IsEmpty(back.Config.Terrain.Veins);
        }
    }
}
