using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static TheLastFortress.Tests.SimTestUtil;

namespace TheLastFortress.Tests
{
    /// <summary>Plan 004: enemy kinds and fixed-point movement, authored waves, reward RNG, perks, burn ordering, Reward phase.</summary>
    public class EnemyMovementTests
    {
        static WavePlan One(EnemyKind kind, GridPos? at = null) =>
            new WavePlan("one", new[] { new SpawnEntry(0, at ?? MvpCampaign.West, 0, kind) }, new[] { at ?? MvpCampaign.West });

        // 9 tiles from (0,10) to the Keep's west neighbour = 10800 units at speed*60 units per step.
        [TestCase(EnemyKind.Grunt, 180)]
        [TestCase(EnemyKind.Runner, 103)] // ceil(10800 / 105): no time lost at the eight intermediate centers
        [TestCase(EnemyKind.Brute, 300)]
        [TestCase(EnemyKind.SiegeGolem, 450)]
        public void TravelTimeIsExactForEachSpeed(EnemyKind kind, int expectedSteps)
        {
            var sim = NewMvpRun(One(kind));
            Assert.IsTrue(sim.StartNight());
            var e = sim.Enemies[0];
            var maxStep = e.MoveUnitsPerStep / 1200.0 + 1e-9;
            double px = e.X, pz = e.Z;
            var steps = 0;
            while (!(e.Cell == new GridPos(9, 10) && !e.Moving) && steps < 1000)
            {
                sim.Step();
                steps++;
                Assert.LessOrEqual(Math.Abs(e.X - px) + Math.Abs(e.Z - pz), maxStep, "no teleport or diagonal");
                Assert.AreEqual(10.5, e.Z, 1e-9, "stays on the row");
                px = e.X;
                pz = e.Z;
            }

            Assert.AreEqual(expectedSteps, steps);
            sim.Step();
            Assert.AreEqual(sim.Keep.Id, e.TargetBuildingId, "attack acquired on the next step");
            Assert.AreEqual(0, e.CarryUnits, "residual movement discarded at the attack cell");
        }

        [Test]
        public void RunnerStateIsIndependentOfFrameChunking()
        {
            var wave = new WavePlan("mix", new[]
            {
                new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.Runner),
                new SpawnEntry(7, MvpCampaign.West, 0, EnemyKind.Brute),
            }, new[] { MvpCampaign.West });
            var a = NewMvpRun(wave);
            var b = NewMvpRun(wave);
            Place(a, BuildingKind.Wall, 5, 10);
            Place(b, BuildingKind.Wall, 5, 10);
            a.StartNight();
            b.StartNight();
            a.Advance(6.35);
            for (var i = 0; i < 127; i++) b.Advance(0.05);
            Assert.AreEqual(a.CaptureSnapshot(), b.CaptureSnapshot());
        }

        [Test]
        public void BruteDoublesDamageOnlyAgainstWalls()
        {
            var sim = NewMvpRun(One(EnemyKind.Brute));
            EnclosureWalls(sim);
            var west = sim.Grid.GetOccupant(new GridPos(9, 10));
            Assert.IsTrue(sim.StartNight());
            StepUntil(sim, () => west.Hp < 100, 2000);
            Assert.AreEqual(60, west.Hp, "20 x2 against a Wall");
            StepUntil(sim, () => sim.Keep.Hp < 500, 2000);
            Assert.AreEqual(480, sim.Keep.Hp, "no multiplier against the Keep");
        }
    }

    public class SixNightWaveTests
    {
        [Test]
        public void AuthoredNightsMatchTableAndExecuteAsPreviewed()
        {
            var campaign = MvpCampaign.CreateSixNights();
            Assert.AreEqual(6, campaign.TotalNights);
            Assert.IsTrue(campaign.OffersRewards);
            var expected = new[] { (10, 0, 0, 0, 1, 1.5), (12, 4, 0, 0, 2, 1.5), (14, 4, 2, 0, 2, 1.25), (18, 8, 3, 0, 3, 1.0), (22, 10, 4, 0, 3, 1.0), (24, 12, 4, 1, 4, 1.0) };
            var entries = new[] { MvpCampaign.West, MvpCampaign.East, MvpCampaign.North, MvpCampaign.South };
            for (var n = 0; n < 6; n++)
            {
                var plan = campaign.Nights[n];
                var (g, r, b, golem, entryCount, cadence) = expected[n];
                Assert.AreEqual(g, plan.Spawns.Count(s => s.Kind == EnemyKind.Grunt), $"night {n + 1} grunts");
                Assert.AreEqual(r, plan.Spawns.Count(s => s.Kind == EnemyKind.Runner));
                Assert.AreEqual(b, plan.Spawns.Count(s => s.Kind == EnemyKind.Brute));
                Assert.AreEqual(golem, plan.Spawns.Count(s => s.Kind == EnemyKind.SiegeGolem));
                CollectionAssert.AreEqual(entries.Take(entryCount), plan.EntryCells);
                var normal = plan.Spawns.Where(s => s.Kind != EnemyKind.SiegeGolem).ToList();
                for (var i = 0; i < normal.Count; i++)
                {
                    Assert.AreEqual(i * PrototypeDefinitions.ToTicks(cadence), normal[i].Tick, $"night {n + 1} spawn {i} time");
                    Assert.AreEqual(i % entryCount, normal[i].EntryIndex, "round-robin entries");
                }
            }

            // Cycle order on Night 3: G R B G R B G R G R G R G G ...
            var n3 = campaign.Nights[2].Spawns.Take(9).Select(s => s.Kind.ToString()[0]);
            Assert.AreEqual("GRBGRBGRG", string.Concat(n3));

            var n6 = campaign.Nights[5].Spawns;
            var bossIndex = n6.ToList().FindIndex(s => s.Kind == EnemyKind.SiegeGolem);
            Assert.AreEqual(600, n6[bossIndex].Tick, "Golem at 30 s");
            Assert.AreEqual(MvpCampaign.West, n6[bossIndex].Cell);
            Assert.AreEqual(600, n6[bossIndex - 1].Tick, "the equal-time non-boss spawn comes first");

            // Execution uses the same stored plan the Day preview shows.
            var sim = NewMvpRun(campaign: campaign);
            Assert.AreSame(campaign.Nights[0], sim.Wave);
            StringAssert.Contains("10 Grunts from West", WaveText.Describe(sim.Wave, 1));
            StringAssert.Contains("BOSS", WaveText.Describe(campaign.Nights[5], 6));
        }

        [Test]
        public void DeadBossWithSurvivingSupportDoesNotWin()
        {
            var wave = new WavePlan("final", new[]
            {
                new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.SiegeGolem),
                new SpawnEntry(0, MvpCampaign.East, 1, EnemyKind.Grunt),
            }, new[] { MvpCampaign.West, MvpCampaign.East });
            var sim = NewMvpRun(wave);
            Place(sim, BuildingKind.Tower, 3, 9); // covers the west approach only
            Assert.IsTrue(sim.StartNight());
            var golem = sim.Enemies.Single(e => e.Kind == EnemyKind.SiegeGolem);
            var grunt = sim.Enemies.Single(e => e.Kind == EnemyKind.Grunt);
            golem.Hp = 10; // fixture shortcut: the boss falls to the first arrow
            StepUntil(sim, () => !golem.Alive, 400);
            Assert.IsTrue(grunt.Alive);
            for (var i = 0; i < 100; i++) sim.Step();
            Assert.AreEqual(RunState.Night, sim.State, "boss death alone does not clear the night");
        }

        [Test]
        public void LivingCapDelaysDueSpawnsFifo()
        {
            var spawns = Enumerable.Range(0, 103).Select(i => new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.Grunt)).ToList();
            var sim = NewMvpRun(new WavePlan("cap", spawns, new[] { MvpCampaign.West }));
            Place(sim, BuildingKind.Tower, 3, 9);
            Assert.IsTrue(sim.StartNight());
            Assert.AreEqual(100, sim.AliveEnemyCount);
            Assert.AreEqual(3, sim.ScheduledRemaining, "delayed, not dropped");
            StepUntil(sim, () => sim.ScheduledRemaining == 0, 2000);
            Assert.LessOrEqual(sim.AliveEnemyCount, 100);
            Assert.AreEqual(RunState.Night, sim.State);
        }
    }

    public class RewardAndPerkTests
    {
        internal static WavePlan Empty() => new WavePlan("empty", new SpawnEntry[0], new[] { MvpCampaign.West });

        /// <summary>
        /// Real reward path: empty nights clear immediately into Reward; the first seed whose offers contain the wanted
        /// perks (in order) is used. Ends in the Day after the last choice, whose stored wave is <paramref name="final"/>.
        /// </summary>
        internal static RunSimulation WithPerks(Action<RunSimulation> day1, WavePlan final, params string[] perks)
        {
            for (uint seed = 0; seed < 5000; seed++)
            {
                var nights = Enumerable.Repeat(0, perks.Length).Select(_ => Empty()).Append(final ?? Empty()).ToArray();
                var sim = new RunSimulation(new RunConfig
                {
                    Definitions = MvpDefinitions.CreateDefault(), Seed = seed,
                    Campaign = new CampaignConfig { Label = "perks", Nights = nights, OffersRewards = true },
                });
                day1?.Invoke(sim);
                var ok = true;
                foreach (var perk in perks)
                {
                    Assert.IsTrue(sim.StartNight());
                    sim.Step();
                    Assert.AreEqual(RunState.Reward, sim.State);
                    if (!sim.CurrentOffer.PerkIds.Contains(perk))
                    {
                        ok = false;
                        break;
                    }

                    Assert.IsTrue(sim.ChoosePerk(sim.CurrentOffer.OfferId, perk));
                }

                if (ok) return sim;
            }

            Assert.Fail("no seed offered " + string.Join(",", perks));
            return null;
        }

        [Test]
        public void Xorshift32MatchesLockedVectorAndRejectionBound()
        {
            var r = new RunRandom(1);
            CollectionAssert.AreEqual(new uint[] { 270369, 67634689, 2647435461 }, new[] { r.Next(), r.Next(), r.Next() });
            Assert.AreEqual(RunRandom.ZeroFallback, new RunRandom(0).State);
            Assert.AreEqual(RunRandom.ZeroFallback, RunRandom.ForRewards(RunRandom.RewardSalt).State, "zero state replaced");
            var b = new RunRandom(12345);
            for (var i = 0; i < 1000; i++) Assert.Less(b.NextBounded(7), 7u);
        }

        [Test]
        public void OffersAreSeededDistinctUnownedWeightedAndFrozen()
        {
            Assert.AreEqual(PerkRules.AllIds.OrderBy(x => x, StringComparer.Ordinal), PerkRules.AllIds);
            Func<BuildingKind, bool> towersOnly = k => k == BuildingKind.Tower;
            Assert.AreEqual(3, PerkRules.Weight(PerkRules.Sharpshooters, towersOnly));
            Assert.AreEqual(1, PerkRules.Weight(PerkRules.EfficientFarming, towersOnly), "no Farm owned");
            Assert.AreEqual(3, PerkRules.Weight(PerkRules.FortressHeart, towersOnly), "Keep/general always 3");

            RewardOffer First(uint seed)
            {
                var sim = new RunSimulation(new RunConfig
                {
                    Definitions = MvpDefinitions.CreateDefault(), Seed = seed,
                    Campaign = new CampaignConfig { Label = "t", Nights = new[] { Empty(), Empty() }, OffersRewards = true },
                });
                sim.StartNight();
                sim.Step();
                var offer = sim.GetOrCreateOffer();
                var rng = sim.RewardRngState;
                Assert.AreSame(offer, sim.GetOrCreateOffer(), "repeated requests never regenerate");
                Assert.AreEqual(rng, sim.RewardRngState, "and consume no RNG");
                Assert.AreEqual(offer.RngAfter, rng);
                Assert.AreEqual(3, offer.PerkIds.Distinct().Count());
                Assert.IsFalse(sim.ChoosePerk("stale", offer.PerkIds[0]), "stale offer ID mutates nothing");
                Assert.IsFalse(sim.ChoosePerk(offer.OfferId, PerkRules.AllIds.First(id => !offer.PerkIds.Contains(id))), "perk outside the offer");
                Assert.AreEqual(RunState.Reward, sim.State);
                return offer;
            }

            var a = First(42);
            var b = First(42);
            CollectionAssert.AreEqual(a.PerkIds, b.PerkIds, "same seed, same decisions, same offer");
            Assert.AreEqual(a.RngBefore, RunRandom.ForRewards(42).State);
            var differs = Enumerable.Range(0, 20).Any(s => !First((uint)s).PerkIds.SequenceEqual(a.PerkIds));
            Assert.IsTrue(differs, "offers depend on the seed");
        }

        [Test]
        public void TowerDamageAddsPercentsThenFloorsAtExactLastStandBoundary()
        {
            Building tower = null;
            var sim = WithPerks(s => tower = Place(s, BuildingKind.Tower, 3, 3), null, PerkRules.Sharpshooters, PerkRules.LastStand);
            sim.Keep.Hp = 150; // exactly 30% of 500: not strictly below
            Assert.IsFalse(sim.LastStandActive);
            Assert.AreEqual(12, sim.TowerDamage(tower), "Sharpshooters only: floor(10 * 1.2)");
            sim.Keep.Hp = 149;
            Assert.IsTrue(sim.LastStandActive);
            Assert.AreEqual(16, sim.TowerDamage(tower), "floor(10 * (1 + 0.2 + 0.4)), not staged rounding");
        }

        [Test]
        public void HpPerksGrantCurrentHpOnceAndUseEffectiveMax()
        {
            Building wall = null;
            var sim = WithPerks(s =>
            {
                wall = Place(s, BuildingKind.Wall, 3, 3);
                wall.Hp = 60;
            }, null, PerkRules.ReinforcedTimber, PerkRules.FortressHeart);
            Assert.AreEqual((90, 130), (wall.Hp, wall.MaxHp), "max +30 and the same delta once; FortressHeart did not re-grant");
            Assert.AreEqual((650, 650), (sim.Keep.Hp, sim.Keep.MaxHp));
            var fresh = Place(sim, BuildingKind.Wall, 4, 4);
            Assert.AreEqual((130, 130), (fresh.Hp, fresh.MaxHp));
            sim.RepairQuote(wall, out var restored, out var cost);
            Assert.AreEqual((40, 8), (restored, cost), "repairs validate against the effective maximum");
            Assert.AreEqual(100, MvpDefinitions.CreateDefault().Wall.MaxHp, "shared definitions untouched");
        }

        [Test]
        public void DiscountedPriceDrivesSaleAndOneRecyclingRefund()
        {
            var final = new WavePlan("brute", new[] { new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.Brute) }, new[] { MvpCampaign.West });
            var sim = WithPerks(null, final, PerkRules.ThriftyConstruction, PerkRules.Recycling);
            Assert.AreEqual(18, sim.PriceFor(BuildingKind.Wall), "max(1, floor(20 * 0.9))");
            var sold = Place(sim, BuildingKind.Wall, 3, 3);
            var gold = sim.Gold;
            Assert.AreEqual(SellRejection.None, sim.TrySell(sold));
            Assert.AreEqual(gold + 9, sim.Gold, "sale refunds floor(18 / 2), no Recycling bonus");

            EnclosureWalls(sim);
            var west = sim.Grid.GetOccupant(new GridPos(9, 10));
            Assert.AreEqual(18, west.PurchasePrice);
            gold = sim.Gold;
            Assert.IsTrue(sim.StartNight());
            StepUntil(sim, () => west.Removed, 3000);
            for (var i = 0; i < 40; i++) sim.Step();
            Assert.AreEqual(gold + 4, sim.Gold, "floor(18 * 0.25) once on combat destruction");
        }

        [Test]
        public void EfficientFarmingForecastEqualsPayment()
        {
            var sim = WithPerks(s =>
            {
                Place(s, BuildingKind.Farm, 5, 5);
                Place(s, BuildingKind.House, 7, 5);
                Place(s, BuildingKind.Farm, 12, 5);
            }, Empty(), PerkRules.EfficientFarming);
            var forecast = sim.IncomeForecast();
            Assert.AreEqual(39 + 32, forecast.Farms, "floor((25+5)*1.3) + floor(25*1.3)");
            var gold = sim.Gold;
            var paidDay = sim.LastPaidDay;
            Assert.IsTrue(sim.StartNight());
            sim.Step();
            Assert.AreEqual(RunState.Victory, sim.State, "final night: no income");
            Assert.AreEqual(gold, sim.Gold);
            Assert.AreEqual(paidDay, sim.LastPaidDay);
            Assert.AreEqual(20 + 15 + 71, sim.LastIncome.Total, "Day 2 payment used the same perk-aware calculation");
        }

        [Test]
        public void OfferedIncomePerkPreviewMatchesPaymentWithoutMutatingTheRun()
        {
            // Linked Farm + House opening; the first seed whose real Night 1 offer contains Efficient Farming.
            for (uint seed = 0; seed < 5000; seed++)
            {
                var sim = new RunSimulation(new RunConfig
                {
                    Definitions = MvpDefinitions.CreateDefault(), Seed = seed,
                    Campaign = new CampaignConfig { Label = "t", Nights = new[] { Empty(), Empty() }, OffersRewards = true },
                });
                Place(sim, BuildingKind.Farm, 5, 5);
                Place(sim, BuildingKind.House, 7, 5);
                sim.StartNight();
                sim.Step();
                var offer = sim.CurrentOffer;
                if (!offer.PerkIds.Contains(PerkRules.EfficientFarming)) continue;

                var before = sim.CaptureSnapshot();
                Assert.AreEqual(65, sim.IncomeForecast().Total, "current forecast: Keep 20 + House 15 + linked Farm 30");
                Assert.AreEqual(74, sim.IncomeForecastWith(PerkRules.EfficientFarming).Total, "20 + 15 + floor(30 * 1.3)");
                Assert.AreEqual(65, sim.IncomeForecastWith(PerkRules.FortressHeart).Total, "a Keep perk implies no income change");
                sim.IncomeForecastWith(PerkRules.EfficientFarming);
                Assert.AreEqual(before, sim.CaptureSnapshot(), "previewing never changes gold, perks, RNG or the offer");
                Assert.AreSame(offer, sim.GetOrCreateOffer());

                var gold = sim.Gold;
                Assert.IsTrue(sim.ChoosePerk(offer.OfferId, PerkRules.EfficientFarming));
                Assert.AreEqual(gold + 74, sim.Gold, "the payment equals the preview");
                Assert.AreEqual(39, sim.BuildingIncome(sim.Grid.GetOccupant(new GridPos(5, 5))), "selected Farm uses the same rule");
                return;
            }

            Assert.Fail("no seed offered Efficient Farming after Night 1");
        }

        [Test]
        public void NightRecapCountsOnlyThatNightsAppliedLosses()
        {
            WavePlan OneGrunt() => new WavePlan("g", new[] { new SpawnEntry(0, MvpCampaign.West, 0) }, new[] { MvpCampaign.West });
            RunSimulation Run(params WavePlan[] nights) => new RunSimulation(new RunConfig
            {
                Definitions = MvpDefinitions.CreateDefault(), Campaign = new CampaignConfig { Label = "t", Nights = nights },
            });

            // Clean night: the Tower kills the Grunt before it reaches anything.
            var clean = Run(OneGrunt(), OneGrunt());
            Place(clean, BuildingKind.Tower, 8, 9);
            Assert.IsNull(clean.LastNight);
            clean.StartNight();
            RunToEnd(clean);
            Assert.AreEqual((1, 1, 0, 0), (clean.LastNight.Night, clean.LastNight.EnemiesDefeated, clean.LastNight.KeepDamage, clean.LastNight.BuildingsLost));

            // Building-loss night: the House sealing the Keep is destroyed in combat; the Keep takes some hits.
            var loss = Run(OneGrunt(), OneGrunt());
            PlaceWalls(loss, (11, 10), (10, 9), (10, 11));
            var house = Place(loss, BuildingKind.House, 9, 10);
            Place(loss, BuildingKind.Farm, 8, 11);
            Place(loss, BuildingKind.Tower, 11, 13);
            loss.StartNight();
            RunToEnd(loss);
            Assert.IsTrue(house.Removed);
            var recap = loss.LastNight;
            Assert.AreEqual((1, 1), (recap.EnemiesDefeated, recap.BuildingsLost));
            Assert.AreEqual(500 - loss.Keep.Hp, recap.KeepDamage, "HP actually removed from the Keep");
            var keepDamage = recap.KeepDamage;
            loss.TryRepair(loss.Keep);
            Assert.AreEqual(SellRejection.None, loss.TrySell(loss.Grid.GetOccupant(new GridPos(8, 11))));
            Assert.AreEqual((keepDamage, 1), (recap.KeepDamage, recap.BuildingsLost), "repairs and sales never rewrite the finished night");
            loss.StartNight();
            Assert.AreNotSame(recap, loss.LastNight);
            Assert.AreEqual((2, 0, 0, 0), (loss.LastNight.Night, loss.LastNight.EnemiesDefeated, loss.LastNight.KeepDamage, loss.LastNight.BuildingsLost), "reset at the next Night start");

            // Lethal night: overkill is capped at the HP the Keep had.
            var defeat = Run(MvpCampaign.CreateFoundation().Nights[0]);
            defeat.StartNight();
            RunToEnd(defeat);
            Assert.AreEqual(RunState.Defeat, defeat.State);
            Assert.AreEqual(500, defeat.LastNight.KeepDamage);
            Assert.AreEqual(0, defeat.LastNight.BuildingsLost, "the Keep itself is not a lost building");
        }

        [Test]
        public void BurnKeepsCadenceOnRefreshExpiresInclusiveAndFreezesWhilePaused()
        {
            var final = new WavePlan("brute", new[] { new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.Brute) }, new[] { MvpCampaign.West });
            Building tower = null;
            var sim = WithPerks(s => tower = Place(s, BuildingKind.Tower, 4, 7), final, PerkRules.FlamingArrows);
            Assert.IsTrue(sim.StartNight());
            var brute = sim.Enemies[0];
            var shots = new List<long>();
            var burns = new List<long>();
            var events = new List<SimEvent>();
            for (var i = 0; i < 900 && brute.Alive; i++)
            {
                if (i == 200)
                {
                    sim.SetPaused(true);
                    var frozen = sim.CaptureSnapshot();
                    for (var k = 0; k < 50; k++) sim.Step();
                    Assert.AreEqual(frozen, sim.CaptureSnapshot(), "paused: no burn or attack timer advances");
                    sim.SetPaused(false);
                }

                sim.Step();
                events.Clear();
                sim.DrainEvents(events);
                shots.AddRange(events.Where(e => e.Type == SimEventType.TowerShot && e.TargetId == brute.Id).Select(e => e.Tick));
                burns.AddRange(events.Where(e => e.Type == SimEventType.BurnTick && e.TargetId == brute.Id).Select(e => e.Tick));
                if (shots.Count > 0 && !RunSimulation.InRange(tower, brute.X, brute.Z) && sim.Tick > shots.Last() + 80) break;
            }

            Assert.Greater(shots.Count, 3, "several refreshing hits");
            Assert.IsTrue(brute.Alive, "fixture keeps the Brute alive to observe expiry");
            var expected = new List<long>();
            for (var t = shots[0] + 20; t <= shots.Last() + 60; t += 20) expected.Add(t);
            CollectionAssert.AreEqual(expected, burns, "1 s cadence from the first hit; refresh extends to last hit + 3 s inclusive");
            Assert.AreEqual(150 - 10 * shots.Count - 2 * burns.Count, brute.Hp, "burn and direct damage, no stacking");
        }

        [Test]
        public void BurningEnemyKilledByTowerNeverTicksAgain()
        {
            var final = new WavePlan("g", new[] { new SpawnEntry(0, MvpCampaign.West, 0, EnemyKind.Grunt) }, new[] { MvpCampaign.West });
            var sim = WithPerks(s => Place(s, BuildingKind.Tower, 8, 9), final, PerkRules.FlamingArrows);
            Assert.IsTrue(sim.StartNight());
            var events = new List<SimEvent>();
            for (var i = 0; i < 600 && sim.State == RunState.Night; i++)
            {
                sim.Step();
                sim.DrainEvents(events);
            }

            var kill = events.Single(e => e.Type == SimEventType.EnemyKilled);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.BurnTick), "it was burning");
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.BurnTick && e.Tick > kill.Tick));
            Assert.AreEqual(1, sim.Stats.EnemiesDefeated);
        }

        [Test]
        public void RewardChoiceCommitsOncePaysOnceAndRestartIsFresh()
        {
            var config = new RunConfig { Definitions = MvpDefinitions.CreateDefault(), Seed = 7, Campaign = MvpCampaign.CreateSixNights() };
            var sim = new RunSimulation(config);
            var fresh = sim.CaptureSnapshot();
            Place(sim, BuildingKind.Tower, 8, 9);
            Place(sim, BuildingKind.Tower, 8, 11);
            Place(sim, BuildingKind.House, 12, 12);
            Assert.IsTrue(sim.StartNight());
            var events = new List<SimEvent>();
            for (var i = 0; i < 6000 && sim.State == RunState.Night; i++)
            {
                sim.Step();
                sim.DrainEvents(events);
            }

            Assert.AreEqual(RunState.Reward, sim.State);
            Assert.AreEqual(PlacementRejection.NotDay, sim.CheckPlacement(BuildingKind.Wall, new GridPos(3, 3)));
            Assert.IsFalse(sim.StartNight());
            var offer = sim.CurrentOffer;
            var gold = sim.Gold;
            Assert.IsTrue(sim.ChoosePerk(offer.OfferId, offer.PerkIds[1]));
            Assert.IsFalse(sim.ChoosePerk(offer.OfferId, offer.PerkIds[1]), "double submit");
            Assert.IsFalse(sim.ChoosePerk(offer.OfferId, offer.PerkIds[2]));
            sim.DrainEvents(events);
            Assert.AreEqual(RunState.Day, sim.State);
            Assert.AreEqual(2, sim.NightNumber);
            CollectionAssert.AreEqual(new[] { offer.PerkIds[1] }, sim.OwnedPerks);
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.IncomePaid));
            Assert.AreEqual(gold + 35, sim.Gold);
            Assert.AreEqual(1, sim.Stats.NightsCleared);
            Assert.AreEqual(10, sim.Stats.EnemiesDefeated);
            Assert.AreEqual(3, sim.Stats.BuildingsPurchased);
            Assert.AreEqual(35, sim.Stats.GoldEarned);

            sim.Restart();
            Assert.AreEqual(fresh, sim.CaptureSnapshot(), "perks, offers, RNG, stats cleared; same seed's fresh state");
            Assert.AreEqual(0, sim.OwnedPerks.Count);
        }
    }
}
