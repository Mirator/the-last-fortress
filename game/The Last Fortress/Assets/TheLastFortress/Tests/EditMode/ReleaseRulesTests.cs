using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static TheLastFortress.Tests.SimTestUtil;

namespace TheLastFortress.Tests
{
    /// <summary>
    /// Plan 008 G2: the exact failure modes named by the gate for the release additions (008-rules.md sections 1-2). Each
    /// test pins one ordering or invariant that a plausible implementation could get wrong; balance values are not frozen.
    /// </summary>
    public class ReleaseMechanicsTests
    {
        static RunSimulation NewRelease(WavePlan wave, PrototypeDefinitions defs = null) =>
            new RunSimulation(new RunConfig { Definitions = defs ?? ReleaseDefinitions.CreateDefault(), Wave = wave });

        static WavePlan Wave(params (long tick, EnemyKind kind)[] spawns) =>
            new WavePlan("w", spawns.Select(s => new SpawnEntry(s.tick, MvpCampaign.West, 0, s.kind)).ToArray(), new[] { MvpCampaign.West });

        static List<SimEvent> StepCollect(RunSimulation sim, System.Func<List<SimEvent>, bool> until, int max = 4000)
        {
            var all = new List<SimEvent>();
            for (var i = 0; i < max && sim.State == RunState.Night; i++)
            {
                sim.Step();
                var batch = Drain(sim);
                all.AddRange(batch);
                if (until(batch)) return all;
            }

            Assert.Fail($"condition not reached within {max} steps (state {sim.State}, tick {sim.Tick})");
            return all;
        }

        [Test]
        public void CannonSplashHitsEveryEnemyInRadiusOnceInIdOrderMeasuredFromTheFootprintCentre()
        {
            // Three Grunts stacked at one spawn, a fourth two tiles (2.0 > 1.25) behind them.
            var sim = NewRelease(Wave((0, EnemyKind.Grunt), (0, EnemyKind.Grunt), (0, EnemyKind.Grunt), (40, EnemyKind.Grunt)));
            var cannon = Place(sim, BuildingKind.Cannon, 3, 11);
            Assert.AreEqual((4.0, 12.0), (cannon.CenterX, cannon.CenterZ), "2x2 centre is the shared corner of its cells");
            Assert.IsTrue(RunSimulation.InRange(cannon, cannon.CenterX + 4.5, cannon.CenterZ, 4.5), "range boundary is inclusive");
            Assert.IsFalse(RunSimulation.InRange(cannon, cannon.CenterX + 4.5001, cannon.CenterZ, 4.5));

            Assert.IsTrue(sim.StartNight());
            var events = StepCollect(sim, b => b.Any(e => e.Type == SimEventType.TowerShot && e.SourceId == cannon.Id));
            var shotTick = events.Single(e => e.Type == SimEventType.TowerShot).Tick;
            var splash = events.Where(e => e.Type == SimEventType.SplashHit && e.Tick == shotTick).Select(e => e.TargetId).ToList();
            var primary = events.Single(e => e.Type == SimEventType.TowerShot).TargetId;
            var cluster = sim.Enemies.Where(e => e.Id <= 3).ToList();
            CollectionAssert.AreEqual(cluster.Select(e => e.Id).Where(id => id != primary), splash, "each other clustered enemy once, ascending ID");
            Assert.IsTrue(cluster.All(e => e.Hp == 10), "every clustered Grunt took exactly one 20-damage hit");
            Assert.AreEqual(30, sim.Enemies.Single(e => e.Id == 4).Hp, "outside the splash radius: untouched");
        }

        [Test]
        public void WatchtowersDoNotStackAndLosingOneMidCombatKeepsTheTowerCooldown()
        {
            var sim = NewRelease(Wave((0, EnemyKind.Brute)));
            var tower = Place(sim, BuildingKind.Tower, 5, 9);
            var north = Place(sim, BuildingKind.Watchtower, 5, 8);
            var south = Place(sim, BuildingKind.Watchtower, 4, 9);
            Assert.AreEqual(5.0, sim.TowerRange(tower), 1e-9, "two adjacent Watchtowers still give +1 once");
            CollectionAssert.AreEqual(new[] { tower }, sim.TowersBuffedBy(new GridPos(6, 9)), "ghost preview reports the tower it would reach");
            Assert.AreEqual(SellRejection.None, sim.TrySell(north));
            Assert.AreEqual(5.0, sim.TowerRange(tower), 1e-9, "the remaining Watchtower keeps the bonus");

            Assert.IsTrue(sim.StartNight());
            StepUntil(sim, () => tower.TargetEnemyId >= 0 && sim.Tick + 5 < tower.AttackDueTick
                                 && RunSimulation.InRange(tower, sim.Enemies[0].X, sim.Enemies[0].Z, 3.5));
            var (target, due) = (tower.TargetEnemyId, tower.AttackDueTick);
            Assert.IsTrue(sim.ScriptedRemove(south, out _));
            Assert.AreEqual(4.0, sim.TowerRange(tower), 1e-9, "destruction recomputes the buff");
            sim.Step();
            Assert.AreEqual((target, due), (tower.TargetEnemyId, tower.AttackDueTick), "a buff change never resets the cooldown or a target still in range");
        }

        [Test]
        public void RepairStationRestoresOnceAtDawnLowestHpFractionFirstWithinReach()
        {
            var sim = new RunSimulation(new RunConfig
            {
                Definitions = ReleaseDefinitions.CreateDefault(), Seed = 1,
                Campaign = new CampaignConfig { Label = "t", Nights = new[] { RewardAndPerkTests.Empty(), RewardAndPerkTests.Empty() }, OffersRewards = true },
            });
            Place(sim, BuildingKind.RepairStation, 4, 4); // cells 4..5, reach 2 → 2..7
            var wall = Place(sim, BuildingKind.Wall, 7, 4); // 70/100 = 0.70
            var house = Place(sim, BuildingKind.House, 3, 7); // 40/80 = 0.50
            var tower = Place(sim, BuildingKind.Tower, 6, 2); // 95/100
            var far = Place(sim, BuildingKind.Wall, 9, 4); // Chebyshev 4: out of reach
            (wall.Hp, house.Hp, tower.Hp, far.Hp, sim.Keep.Hp) = (70, 40, 95, 10, 300);

            Assert.IsTrue(sim.StartNight());
            sim.Step();
            Assert.IsTrue(sim.DebugSkipReward());
            var events = Drain(sim);
            Assert.AreEqual((80, 80, 95, 10, 300), (wall.Hp, house.Hp, tower.Hp, far.Hp, sim.Keep.Hp),
                "budget 50: House +40 (lowest fraction), Wall +10; nothing beyond reach; the Keep needs Field Engineers");
            Assert.AreEqual(50, events.Single(e => e.Type == SimEventType.DawnRepair).TargetId);

            var restored = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(JsonUtility.ToJson(CheckpointCodec.Capture(sim))),
                new RulesCatalog(RulesProfile.SixNights(sim.Definitions)), out var problem, out var message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            Assert.AreEqual((80, 80), (restored.GetBuilding(wall.Id).Hp, restored.GetBuilding(house.Id).Hp), "loading the Day never repairs again");
        }

        static PrototypeDefinitions ToughBomber()
        {
            var d = ReleaseDefinitions.CreateDefault();
            d.Bomber.MaxHp = 1000; // survives the approach so the kill lands inside the fuse
            return d;
        }

        [Test]
        public void BomberKilledDuringItsFuseNeverDetonates()
        {
            var sim = NewRelease(Wave((0, EnemyKind.Bomber)), ToughBomber());
            EnclosureWalls(sim);
            Place(sim, BuildingKind.Tower, 8, 8);
            var wall = sim.Grid.GetOccupant(new GridPos(9, 10));
            Assert.IsTrue(sim.StartNight());
            var bomber = sim.Enemies[0];
            StepCollect(sim, b => b.Any(e => e.Type == SimEventType.FuseLit));
            Assert.IsTrue(bomber.FuseLit);
            bomber.Hp = 1; // the next Archer Tower shot (every 0.75 s) lands within the 1.5 s fuse
            var events = StepCollect(sim, b => b.Any(e => e.Type == SimEventType.EnemyKilled && e.SourceId == bomber.Id));
            Assert.Less(sim.Tick, bomber.DetonateTick);
            RunToEnd(sim);
            Assert.IsFalse(events.Concat(Drain(sim)).Any(e => e.Type == SimEventType.BomberDetonated));
            Assert.AreEqual(100, wall.Hp, "no blast");
            Assert.AreEqual(1, sim.Stats.EnemiesDefeated);
        }

        [Test]
        public void BomberBlastBreaksItsWallSplashesNeighboursAndIsNotCountedAsDefeated()
        {
            var sim = NewRelease(Wave((0, EnemyKind.Bomber), (400, EnemyKind.Grunt)));
            EnclosureWalls(sim);
            var wall = sim.Grid.GetOccupant(new GridPos(9, 10));
            var house = Place(sim, BuildingKind.House, 8, 11); // Chebyshev 1 from the attack cell (8,10)
            var other = sim.Grid.GetOccupant(new GridPos(10, 9)); // Chebyshev 2: outside the splash
            Assert.IsTrue(sim.StartNight());
            var events = StepCollect(sim, b => b.Any(e => e.Type == SimEventType.BomberDetonated));
            var lit = events.Single(e => e.Type == SimEventType.FuseLit).Tick;
            Assert.AreEqual(lit + 30, events.Single(e => e.Type == SimEventType.BomberDetonated).Tick, "1.5 s fuse");
            Assert.IsFalse(wall.IsAlive, "60 x2 against a Wall");
            Assert.AreEqual((60, 100), (house.Hp, other.Hp));
            Assert.AreEqual(0, sim.Stats.EnemiesDefeated);
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.EnemyKilled));
        }

        [Test]
        public void BlastThatEmptiesTheKeepOnTheLastEnemysStepIsDefeat()
        {
            var sim = NewRelease(Wave((0, EnemyKind.Bomber)));
            sim.Keep.Hp = 60;
            Assert.IsTrue(sim.StartNight());
            RunToEnd(sim);
            Assert.AreEqual(0, sim.AliveEnemyCount, "the Bomber died in the same step");
            Assert.AreEqual(RunState.Defeat, sim.State, "Keep at 0 is evaluated before the cleared-night check");
        }

        [Test]
        public void RaiderArcherStopsOnItsRouteKeepsItsTargetAndResetsTheCooldownOnRetarget()
        {
            var defs = ReleaseDefinitions.CreateDefault();
            defs.RaiderArcher.AttackRange = 3.0; // the geometry below is laid out for this range, not the current tuning
            var sim = NewRelease(Wave((0, EnemyKind.Archer)), defs);
            var house = Place(sim, BuildingKind.House, 6, 12);
            var wall = Place(sim, BuildingKind.Wall, 6, 9);
            Place(sim, BuildingKind.SpikeTrap, 7, 9); // makes the Wall reflect melee hits
            Assert.IsTrue(sim.StartNight());
            var archer = sim.Enemies[0];
            StepUntil(sim, () => archer.TargetBuildingId >= 0);
            Assert.AreEqual(house.Id, archer.TargetBuildingId, "economy before Walls although the Wall is nearer");
            Assert.AreEqual(new GridPos(4, 10), archer.Cell, "stops on the first route cell within 3 tiles");
            Assert.AreEqual(sim.Tick + archer.AttackTicks - 1, archer.AttackDueTick, "first hit one full interval after acquisition");
            for (var i = 0; i < 100; i++) sim.Step();
            Assert.AreEqual((new GridPos(4, 10), false), (archer.Cell, archer.Moving), "holds its cell while the target lives");
            Assert.Less(house.Hp, 80);
            Assert.AreEqual(100, wall.Hp, "sticky target: the Wall is not hit");

            Assert.IsTrue(sim.ScriptedRemove(house, out var attacked));
            Assert.IsTrue(attacked);
            StepUntil(sim, () => archer.TargetBuildingId == wall.Id);
            Assert.AreEqual(sim.Tick + archer.AttackTicks - 1, archer.AttackDueTick, "retarget restarts the full interval");
            Assert.AreEqual(new GridPos(4, 10), archer.Cell);
            var reflect = sim.WallReflection(wall);
            StepUntil(sim, () => wall.Hp < 100);
            Assert.AreEqual((94, 35), (wall.Hp, archer.Hp), "6 damage, and a ranged hit is never reflected");
            Assert.AreEqual(3, reflect, "a melee hit on this Wall would have reflected 3");
        }

        [Test]
        public void CommanderAuraAddsHalfToNearbyHitsAndEndsWhenItDies()
        {
            var defs = ReleaseDefinitions.CreateDefault();
            defs.Grunt.MaxHp = 100000; // the Tower can only ever kill the Commander
            var sim = NewRelease(Wave((0, EnemyKind.Grunt), (0, EnemyKind.SiegeCommander)), defs);
            Place(sim, BuildingKind.Tower, 4, 8);
            Assert.IsTrue(sim.StartNight());
            var grunt = sim.Enemies[0];
            var commander = sim.Enemies[1];
            // Both spawn on one cell; wait until the Grunt is hitting the Keep and the slower Commander has caught up.
            StepUntil(sim, () => grunt.TargetBuildingId == sim.Keep.Id && sim.CommanderAuraPercent(grunt) > 0, 6000);
            var keep = sim.Keep.Hp;
            StepUntil(sim, () => sim.Keep.Hp < keep);
            Assert.AreEqual(15, keep - sim.Keep.Hp, "10 +50% inside the aura");
            Assert.AreEqual(0, sim.CommanderAuraPercent(commander), "never buffs itself");

            commander.Hp = 1;
            StepUntil(sim, () => !commander.Alive);
            Assert.AreEqual(0, sim.CommanderAuraPercent(grunt), "removed with the Commander");
            keep = sim.Keep.Hp;
            StepUntil(sim, () => sim.Keep.Hp < keep);
            Assert.AreEqual(10, keep - sim.Keep.Hp);
        }
    }

    /// <summary>Plan 008 G1: profile-tagged saves, legacy six-night continuation and rejection of unknown profiles.</summary>
    public class ReleaseSaveTests
    {
        static readonly RulesCatalog ReleaseBuild = new RulesCatalog(
            RulesProfile.TwelveNights(ReleaseDefinitions.CreateDefault()), RulesProfile.SixNights(MvpDefinitions.CreateDefault()));

        static RunSimulation NewReleaseRun(uint seed, string profile) => new RunSimulation(new RunConfig
        {
            Definitions = ReleaseDefinitions.CreateDefault(), Seed = seed, Terrain = MapProfiles.Generate(profile, seed),
            Campaign = ReleaseCampaign.Create(seed, profile),
        });

        [Test]
        public void SixNightSaveWithoutProfileFieldsContinuesAsSixNightsUnderMvpRules()
        {
            var mvp = new RunSimulation(new RunConfig { Definitions = MvpDefinitions.CreateDefault(), Seed = 9, Campaign = MvpCampaign.CreateSixNights(), Terrain = MapGenerator.Generate(9) });
            Place(mvp, BuildingKind.Farm, 4, 13);
            var json = JsonUtility.ToJson(CheckpointCodec.Capture(mvp));
            StringAssert.Contains("\"schemaVersion\":1", json, "the MVP keeps writing schema 1");
            foreach (var field in new[] { "campaignId", "contentVersion", "mapProfile", "difficulty" }) // as written before plan 008
                json = System.Text.RegularExpressions.Regex.Replace(json, $"\"{field}\":(\"[^\"]*\"|\\d+),", "");
            StringAssert.DoesNotContain("campaignId", json);

            var back = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(json), ReleaseBuild, out var problem, out var message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            Assert.AreEqual((6, MvpCampaign.RulesVersion), (back.TotalNights, back.Campaign.RulesVersion), "never converted to twelve nights");
            Assert.AreSame(PerkRules.AllIds, back.PerkPool, "MVP draws keep the ten-perk pool");
            Assert.IsNull(back.Definitions.Cannon);
            Assert.AreEqual(PlacementRejection.NotPurchasable, back.CheckPlacement(BuildingKind.Cannon, new GridPos(14, 14)));
            Assert.AreEqual(RunCheckpoint.MvpSchema, CheckpointCodec.Capture(back).schemaVersion, "and it keeps saving as schema 1");
        }

        [Test]
        public void ReleaseSaveRoundTripsProfileMapWavesAndNewBuildingsAndUnknownProfilesAreRejected()
        {
            var sim = NewReleaseRun(21, MapProfiles.Crossing);
            var cannon = Place(sim, BuildingKind.Cannon, 11, 11); // central cells are always land
            Place(sim, BuildingKind.Watchtower, 10, 12);
            cannon.Hp = 77;
            var cp = CheckpointCodec.Capture(sim);
            Assert.AreEqual((2, ReleaseCampaign.RulesVersion, ReleaseCampaign.CampaignId, MapProfiles.Crossing, "standard"),
                (cp.schemaVersion, cp.rulesVersion, cp.campaignId, cp.mapProfile, cp.difficulty));
            var json = JsonUtility.ToJson(cp);

            var back = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(json), ReleaseBuild, out var problem, out var message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            Assert.AreEqual(sim.CaptureSnapshot().Replace(" rev=" + sim.Grid.Revision, ""), back.CaptureSnapshot().Replace(" rev=" + back.Grid.Revision, ""));
            Assert.AreEqual((12, MapProfiles.Crossing), (back.TotalNights, back.Campaign.MapProfile));
            CollectionAssert.AreEqual(sim.Config.Terrain.Cells, back.Config.Terrain.Cells, "the accepted terrain, not a regenerated one");
            for (var n = 0; n < 12; n++)
                CollectionAssert.AreEqual(sim.Campaign.Nights[n].Spawns.Select(s => (s.Tick, s.Cell, s.Kind)), back.Campaign.Nights[n].Spawns.Select(s => (s.Tick, s.Cell, s.Kind)));
            Assert.AreEqual(5.5, back.TowerRange(back.GetBuilding(cannon.Id)), 1e-9, "Watchtower adjacency rebuilt from occupancy");

            CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(json), new RulesCatalog(RulesProfile.SixNights(MvpDefinitions.CreateDefault())), out problem, out message);
            Assert.AreEqual(CheckpointProblem.IncompatibleRules, problem, "an MVP-only build never opens a twelve-night save");
            CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(json.Replace("\"schemaVersion\":2", "\"schemaVersion\":3")), ReleaseBuild, out problem, out message);
            Assert.AreEqual(CheckpointProblem.UnknownSchema, problem);
            StringAssert.Contains("newer version", message);
            CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(json.Replace("\"contentVersion\":1", "\"contentVersion\":2")), ReleaseBuild, out problem, out _);
            Assert.AreEqual(CheckpointProblem.IncompatibleRules, problem);
            var badPerk = JsonUtility.FromJson<RunCheckpoint>(json);
            (badPerk.checkpointNight, badPerk.lastPaidDay, badPerk.perks) = (2, 2, new[] { "NotAPerk" });
            Assert.IsNull(CheckpointCodec.Restore(badPerk, ReleaseBuild, out problem, out _));
            Assert.AreEqual(CheckpointProblem.Invalid, problem);
        }
    }

    /// <summary>Plan 008 G3: the twelve stored nights and the three tactical map profiles.</summary>
    public class ReleaseContentTests
    {
        [Test]
        public void TwelveNightsFollowTheTableWithSeededEntriesAndPreviewWhatSpawns()
        {
            var defs = ReleaseDefinitions.CreateDefault();
            var table = ReleaseCampaign.Table();
            var rotations = new HashSet<string>();
            for (uint seed = 0; seed < 50; seed++)
            {
                var campaign = ReleaseCampaign.Create(seed, MapProfiles.Meadow);
                CollectionAssert.IsEmpty(ReleaseCampaign.Validate(campaign, defs), $"seed {seed}");
                Assert.AreEqual(12, campaign.TotalNights);
                for (var n = 0; n < 12; n++)
                {
                    var spawns = campaign.Nights[n].Spawns;
                    var r = table[n];
                    Assert.AreEqual((r.Grunts, r.Runners, r.Brutes, r.Bombers, r.Archers),
                        (spawns.Count(s => s.Kind == EnemyKind.Grunt), spawns.Count(s => s.Kind == EnemyKind.Runner), spawns.Count(s => s.Kind == EnemyKind.Brute),
                            spawns.Count(s => s.Kind == EnemyKind.Bomber), spawns.Count(s => s.Kind == EnemyKind.Archer)), $"seed {seed} night {n + 1}");
                    Assert.AreEqual(r.Boss, spawns.Where(s => EnemyDefinition.IsBossKind(s.Kind)).Select(s => (EnemyKind?)s.Kind).SingleOrDefault());
                }

                rotations.Add(string.Join(",", campaign.Nights.Select(w => w.Spawns[0].EntryIndex)));
                CollectionAssert.AreEqual(campaign.Nights[11].Spawns, ReleaseCampaign.Create(seed, MapProfiles.Meadow).Nights[11].Spawns, "repeatable");
            }

            Assert.Greater(rotations.Count, 10, "the seed varies which entry leads each night");
            var finale = ReleaseCampaign.Create(4, MapProfiles.Ravine).Nights[11];
            StringAssert.Contains("BOSS: Siege Commander from", WaveText.Tonight(finale));
            var text = WaveText.Describe(finale, 12);
            StringAssert.Contains("Bombers", text);
            StringAssert.Contains("Raider Archers", text);
        }

        [Test]
        public void MapProfilesMeetTheirNamedConstraintsOnASeedCorpusAndFitTwoByTwoBuildings()
        {
            foreach (var id in MapProfiles.All)
            {
                var profile = MapProfiles.Get(id);
                Assert.IsNull(MapProfiles.Validate(profile, MapProfiles.Skeleton(profile, 0)), $"{id}: the fallback skeleton is itself valid");
                var fallbacks = 0;
                for (uint seed = 0; seed < 200; seed++)
                {
                    var map = MapProfiles.Generate(id, seed);
                    Assert.AreEqual(id, map.Profile);
                    if (map.Fallback) fallbacks++;
                    else Assert.IsNull(MapProfiles.Validate(profile, map), $"{id} seed {seed}");
                    if (seed >= 20) continue;
                    CollectionAssert.AreEqual(map.Cells, MapProfiles.Generate(id, seed).Cells, $"{id} seed {seed} repeatable");
                    var sim = new RunSimulation(new RunConfig
                    {
                        Definitions = ReleaseDefinitions.CreateDefault(), Seed = seed, Terrain = map, Campaign = ReleaseCampaign.Create(seed, id),
                    });
                    CollectionAssert.IsEmpty(sim.ValidateMap(), $"{id} seed {seed}: every entry routes on Day 1");
                    foreach (var kind in new[] { BuildingKind.Cannon, BuildingKind.RepairStation, BuildingKind.Farm })
                        Assert.IsTrue(Enumerable.Range(0, 400).Any(i => sim.CheckPlacement(kind, new GridPos(i % 20, i / 20)) == PlacementRejection.None),
                            $"{id} seed {seed}: a legal, affordable {kind} site on Day 1");
                }

                Assert.Less(fallbacks, 10, $"{id}: fallback must stay rare (<5%)");
            }
        }
    }
}
