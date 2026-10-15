using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using static TheLastFortress.Tests.SimTestUtil;

namespace TheLastFortress.Tests
{
    /// <summary>Plan 005: seeded terrain, wall masks and cosmetic rotation, checkpoints and storage failure handling.</summary>
    public class TerrainGenerationTests
    {
        [Test]
        public void Seeds0To999AreValidRepeatableAndRewardIndependent()
        {
            var fallbacks = new List<uint>();
            var attempts = new int[MapGenerator.MaxAttempts];
            for (uint seed = 0; seed < 1000; seed++)
            {
                var map = MapGenerator.Generate(seed);
                if (map.Fallback)
                {
                    fallbacks.Add(seed);
                    Assert.IsTrue(map.Cells.All(c => c == TerrainType.Land), $"seed {seed}: fallback is the authored all-land map");
                    continue;
                }

                attempts[map.Attempt]++;
                Assert.IsNull(MapGenerator.Validate(map), $"seed {seed} attempt {map.Attempt}");
                foreach (var t in new[] { TerrainType.Forest, TerrainType.Rock, TerrainType.Water })
                    Assert.AreEqual(10, map.Cells.Count(c => c == t), $"seed {seed}: ten {t}");
                for (var i = 0; i < map.Cells.Length; i++)
                {
                    var c = new GridPos(i % 20, i / 20);
                    if (!MapGenerator.IsEligible(c)) Assert.AreEqual(TerrainType.Land, map.Cells[i], $"seed {seed}: fixed cell {c} stays land");
                }
            }

            for (uint seed = 0; seed < 10; seed++)
                CollectionAssert.AreEqual(MapGenerator.Generate(seed).Cells, MapGenerator.Generate(seed).Cells, $"seed {seed} repeatable");
            Assert.IsFalse(MapGenerator.Generate(1).Cells.SequenceEqual(MapGenerator.Generate(2).Cells), "different seeds differ");

            // The reward stream is seeded independently: terrain generation consumes none of it.
            foreach (uint seed in new uint[] { 0, 1, 7, 999 })
            {
                string[] FirstOffer(TerrainMap terrain)
                {
                    var sim = new RunSimulation(new RunConfig
                    {
                        Definitions = MvpDefinitions.CreateDefault(), Seed = seed, Terrain = terrain,
                        Campaign = new CampaignConfig { Label = "t", Nights = new[] { RewardAndPerkTests.Empty(), RewardAndPerkTests.Empty() }, OffersRewards = true },
                    });
                    sim.StartNight();
                    sim.Step();
                    return sim.CurrentOffer.PerkIds;
                }

                CollectionAssert.AreEqual(FirstOffer(TerrainMap.Flat()), FirstOffer(MapGenerator.Generate(seed)), $"seed {seed}");
            }

            var report = $"Seeds 0..999: {1000 - fallbacks.Count} generated, {fallbacks.Count} fallback ({fallbacks.Count / 10.0:0.0}%)" +
                         $"{(fallbacks.Count > 0 ? " [" + string.Join(",", fallbacks) + "]" : "")}; accepted attempt histogram: {string.Join(" ", attempts.Select((n, i) => $"{i}:{n}").Where(s => !s.EndsWith(":0")))}";
            Directory.CreateDirectory("TestResults");
            File.WriteAllText(Path.Combine("TestResults", "005-map-seeds.txt"), report);
            TestContext.WriteLine(report);
            Assert.Less(fallbacks.Count, 50, "fallback must stay rare (<5%)");
        }
    }

    public class WallConnectionTests
    {
        [Test]
        public void AllSixteenMasksMapToCanonicalShapeAndRotation()
        {
            var expected = new Dictionary<int, (WallShape, int)>
            {
                [0] = (WallShape.Isolated, 0), [1] = (WallShape.End, 0), [2] = (WallShape.End, 1), [4] = (WallShape.End, 2), [8] = (WallShape.End, 3),
                [5] = (WallShape.Straight, 0), [10] = (WallShape.Straight, 1),
                [3] = (WallShape.Corner, 0), [6] = (WallShape.Corner, 1), [12] = (WallShape.Corner, 2), [9] = (WallShape.Corner, 3),
                [7] = (WallShape.T, 0), [14] = (WallShape.T, 1), [13] = (WallShape.T, 2), [11] = (WallShape.T, 3),
                [15] = (WallShape.Cross, 0),
            };
            Assert.AreEqual(16, expected.Count);
            foreach (var pair in expected)
            {
                WallTopology.Classify(pair.Key, out var shape, out var turns);
                Assert.AreEqual(pair.Value, (shape, turns), $"mask {pair.Key}");
                var m = WallTopology.CanonicalMask(shape);
                for (var i = 0; i < turns; i++) m = WallTopology.RotateClockwise(m);
                Assert.AreEqual(pair.Key, m, "rotating the canonical mask reproduces it");
            }
        }

        [Test]
        public void ConnectionsFollowOccupancyAndRotationIsCosmeticAndSaved()
        {
            var sim = NewMvpRun(campaign: MvpCampaign.CreateSixNights());
            var corner = Place(sim, BuildingKind.Wall, 5, 5);
            var east = Place(sim, BuildingKind.Wall, 6, 5);
            Place(sim, BuildingKind.Wall, 5, 6);
            Assert.AreEqual(WallTopology.North | WallTopology.East, WallTopology.Mask(sim.Grid, corner.Cell));
            Assert.AreEqual(WallTopology.North | WallTopology.East | WallTopology.West, WallTopology.Mask(sim.Grid, corner.Cell, new GridPos(4, 5)),
                "a ghost Wall shows its hypothetical connection without mutating occupancy");
            Assert.IsNull(sim.Grid.GetOccupant(new GridPos(4, 5)));
            Place(sim, BuildingKind.Wall, 4, 5);
            Assert.AreEqual(11, WallTopology.Mask(sim.Grid, corner.Cell), "T");
            Assert.AreEqual(SellRejection.None, sim.TrySell(east));
            Assert.AreEqual(9, WallTopology.Mask(sim.Grid, corner.Cell), "dead connection removed");
            Assert.AreEqual(WallTopology.East, WallTopology.Mask(sim.Grid, new GridPos(4, 5)), "neighbor became an end");
            Assert.IsFalse(sim.TryRotate(corner), "Walls connect automatically, never rotate");

            Assert.AreEqual(PlacementRejection.None, sim.TryPlace(BuildingKind.Farm, new GridPos(12, 4), 1, out var farm));
            var cells = farm.Cells.ToArray();
            var revision = sim.Grid.Revision;
            Assert.IsTrue(sim.TryRotate(farm));
            Assert.AreEqual(2, farm.Orientation);
            CollectionAssert.AreEqual(cells, farm.Cells, "same anchor and footprint");
            Assert.AreEqual(revision, sim.Grid.Revision, "rotation never changes occupancy or routes");

            var restored = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(JsonUtility.ToJson(CheckpointCodec.Capture(sim))),
                sim.Definitions, out var problem, out var message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            Assert.AreEqual(2, restored.GetBuilding(farm.Id).Orientation);
            Assert.AreEqual(9, WallTopology.Mask(restored.Grid, corner.Cell), "loaded topology");

            Assert.IsTrue(sim.StartNight());
            Assert.IsFalse(sim.TryRotate(farm), "no rotation at Night");
        }
    }

    public class CheckpointTests
    {
        static string Normalize(string snapshot) => Regex.Replace(snapshot, @" rev=\d+", "");

        static RunSimulation RoundTrip(RunSimulation sim, out RunCheckpoint cp)
        {
            cp = CheckpointCodec.Capture(sim);
            var json = JsonUtility.ToJson(cp);
            var back = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(json), sim.Definitions, out var problem, out var message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            return back;
        }

        [Test]
        public void DayCheckpointRoundTripsAndReplaysIdentically()
        {
            var final = new WavePlan("mix", new[]
            {
                new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.Brute), new SpawnEntry(20, MvpCampaign.West, 0, EnemyKind.Runner),
            }, new[] { MvpCampaign.West });
            Building farm = null;
            var sim = RewardAndPerkTests.WithPerks(s =>
            {
                farm = Place(s, BuildingKind.Farm, 5, 5);
                s.TryRotate(farm);
                Place(s, BuildingKind.Tower, 8, 9);
            }, final, PerkRules.ThriftyConstruction, PerkRules.FortressHeart);
            var wall = Place(sim, BuildingKind.Wall, 7, 10);
            Assert.AreEqual(18, wall.PurchasePrice);
            farm.Hp = 37;
            sim.Keep.Hp = 401;

            var back = RoundTrip(sim, out var cp);
            Assert.AreEqual(Normalize(sim.CaptureSnapshot()), Normalize(back.CaptureSnapshot()));
            Assert.AreEqual((37, 1, 18), (back.GetBuilding(farm.Id).Hp, back.GetBuilding(farm.Id).Orientation, back.GetBuilding(wall.Id).PurchasePrice));
            Assert.AreEqual((401, 650), (back.Keep.Hp, back.Keep.MaxHp), "effective max from restored perks");
            Assert.AreEqual(18, back.PriceFor(BuildingKind.Wall), "Thrifty restored");
            Assert.AreSame(back.GetBuilding(farm.Id), back.Grid.GetOccupant(new GridPos(6, 6)), "multi-cell occupancy rebuilt from one record");
            CollectionAssert.AreEqual(sim.Wave.Spawns.Select(s => (s.Tick, s.Cell, s.Kind)), back.Wave.Spawns.Select(s => (s.Tick, s.Cell, s.Kind)));

            Assert.IsTrue(sim.StartNight());
            Assert.IsTrue(back.StartNight());
            RunToEnd(sim);
            RunToEnd(back);
            Assert.AreEqual(Normalize(sim.CaptureSnapshot()), Normalize(back.CaptureSnapshot()), "resumed combat is identical");
            Assert.AreEqual((sim.LastNight.Night, sim.LastNight.EnemiesDefeated, sim.LastNight.KeepDamage, sim.LastNight.BuildingsLost),
                (back.LastNight.Night, back.LastNight.EnemiesDefeated, back.LastNight.KeepDamage, back.LastNight.BuildingsLost),
                "the replayed night's recap matches and is counted once");
        }

        [Test]
        public void RewardLedgerReplayReusesOfferWithoutRerollOrDoubleIncome()
        {
            var sim = new RunSimulation(new RunConfig { Definitions = MvpDefinitions.CreateDefault(), Seed = 11, Campaign = MvpCampaign.CreateSixNights() });
            Place(sim, BuildingKind.Tower, 8, 9);
            Place(sim, BuildingKind.Tower, 8, 11);
            var ledger = CheckpointCodec.Capture(sim); // committed immediately before Night 1
            Assert.IsTrue(sim.StartNight());
            RunToEnd(sim);
            Assert.AreEqual(RunState.Reward, sim.State);
            var offer = sim.CurrentOffer;
            ledger.hasPendingOffer = true; // attached on first Reward entry, before any choice
            ledger.pendingOffer = CheckpointCodec.Offer(offer);

            // Quit during Reward: Continue replays Night 1 from the pre-combat Day with a different build.
            var replay = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(JsonUtility.ToJson(ledger)), sim.Definitions, out var problem, out var message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            Assert.AreEqual(1, replay.NightNumber);
            Place(replay, BuildingKind.Farm, 4, 13); // changes weights; must not change the frozen offer
            Assert.IsTrue(replay.StartNight());
            RunToEnd(replay);
            Assert.AreEqual(RunState.Reward, replay.State);
            CollectionAssert.AreEqual(offer.PerkIds, replay.CurrentOffer.PerkIds, "no reroll");
            Assert.AreEqual(offer.RngAfter, replay.RewardRngState, "stream advanced to the stored after-state once");
            Assert.AreSame(replay.CurrentOffer, replay.GetOrCreateOffer());

            var gold = replay.Gold;
            Assert.IsTrue(replay.ChoosePerk(replay.CurrentOffer.OfferId, offer.PerkIds[2]));
            Assert.IsFalse(replay.ChoosePerk(offer.OfferId, offer.PerkIds[0]));
            Assert.AreEqual(gold + replay.LastIncome.Total, replay.Gold, "one income transaction");
            Assert.AreEqual(2, replay.LastPaidDay);
            var day2 = CheckpointCodec.Capture(replay);
            Assert.IsFalse(day2.hasPendingOffer);
            Assert.AreEqual(offer.PerkIds[2], day2.offerHistory.Single().chosen, "resolved ledger folded into history");
        }

        [TestCase("corrupt")]
        [TestCase("unknown-version")]
        [TestCase("failed-write")]
        public void StorageFailuresKeepDurableFileAndLiveRun(string fault)
        {
            var dir = Path.Combine(Path.GetTempPath(), "tlf-save-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var defs = MvpDefinitions.CreateDefault();
                var store = new SaveStore(dir);
                var persistence = new RunPersistence(store);
                var sim = new RunSimulation(new RunConfig { Definitions = defs, Seed = 3, Campaign = MvpCampaign.CreateSixNights(), Terrain = MapGenerator.Generate(3) });
                persistence.BeginNewRun(defs);
                Assert.IsTrue(persistence.CommitDay(sim));
                Place(sim, BuildingKind.Tower, 9, 9);
                Assert.IsTrue(persistence.CommitDay(sim), "second write keeps a .bak");
                Assert.IsTrue(store.WriteSettings(new GameSettings { cameraSensitivity = 1.7f }, out _));
                var path = store.SlotPath(0);
                var live = sim.CaptureSnapshot();
                var other = new RunPersistence(store);
                other.BeginNewRun(defs);
                Assert.AreEqual(1, other.Slot, "a New Run never replaces an active saved run");

                switch (fault)
                {
                    case "corrupt":
                        File.WriteAllText(path, "{ \"schemaVersion\": 1, \"buildings\": [ {");
                        break;
                    case "unknown-version":
                        File.WriteAllText(path, File.ReadAllText(path).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99"));
                        break;
                    case "failed-write":
                        Directory.CreateDirectory(path + ".tmp"); // the temp file cannot be created: injected I/O failure
                        break;
                }

                var durable = File.ReadAllBytes(path);
                if (fault == "failed-write")
                {
                    Place(sim, BuildingKind.Wall, 9, 11);
                    Assert.IsFalse(persistence.CommitDay(sim));
                    Assert.IsTrue(persistence.SavePending, "progress is reported unsaved, not advertised as saved");
                    StringAssert.Contains("Retry", persistence.LastError);
                    CollectionAssert.AreEqual(durable, File.ReadAllBytes(path), "durable file survives the failed write");
                    Assert.AreEqual(SaveStatus.Ok, store.Read(0, defs).Status);
                    Directory.Delete(path + ".tmp");
                    Assert.IsTrue(persistence.Retry(), "retry writes the same pending snapshot");
                    Assert.IsFalse(persistence.SavePending);
                    var reloaded = store.Read(0, defs);
                    Assert.AreEqual(SaveStatus.Ok, reloaded.Status);
                    Assert.IsTrue(reloaded.Sim.Grid.IsSolid(new GridPos(9, 11)));
                    return;
                }

                var load = store.Read(0, defs);
                Assert.AreEqual(fault == "corrupt" ? SaveStatus.Corrupt : SaveStatus.UnknownSchema, load.Status);
                Assert.IsNull(load.Sim);
                Assert.IsNotEmpty(load.Message);
                CollectionAssert.AreEqual(durable, File.ReadAllBytes(path), "a failing save is never modified");
                Assert.AreEqual(live, sim.CaptureSnapshot(), "failed load leaves the live run unchanged");
                Assert.AreEqual(1.7f, store.ReadSettings(out _).cameraSensitivity, "settings survive a bad run save");

                var backup = store.Read(0, defs, backup: true);
                Assert.AreEqual(SaveStatus.Ok, backup.Status, "explicit backup recovery is available");
                Assert.IsFalse(backup.Sim.Grid.IsSolid(new GridPos(9, 9)), "backup is the previous checkpoint");

                persistence.BeginNewRun(defs);
                Assert.AreEqual(1, persistence.Slot, "a New Run never overwrites the damaged file");
                Assert.IsTrue(persistence.CommitDay(new RunSimulation(new RunConfig { Definitions = defs, Seed = 4, Campaign = MvpCampaign.CreateSixNights() })));
                CollectionAssert.AreEqual(durable, File.ReadAllBytes(path));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
