using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static TheLastFortress.Tests.SimTestUtil;

namespace TheLastFortress.Tests
{
    /// <summary>
    /// Plan 012 Stage 1: the hero rules of specs/003 that a plausible implementation could get wrong. Each test names the
    /// failure it catches; hero tuning values are read from <see cref="HeroRules"/>, never frozen here.
    /// </summary>
    public class HeroRulesTests
    {
        static readonly HeroCommand Still = new HeroCommand(-1, false, 0);

        /// <summary>A one-night run on the flat board (release-rules-2 unless stated), so the hero exists and nothing else varies.</summary>
        static RunSimulation NewHeroRun(WavePlan wave, IReadOnlyList<GridPos> impassable = null, string rules = ReleaseCampaign.RulesV2) => new RunSimulation(new RunConfig
        {
            Definitions = rules == ReleaseCampaign.RulesV4 ? ReleaseCampaign.Contact(ReleaseDefinitions.CreateDefault()) : ReleaseCampaign.WalkingEnemies(rules) ? ReleaseCampaign.Walking(ReleaseDefinitions.CreateDefault()) : ReleaseDefinitions.CreateDefault(),
            Wave = wave, ImpassableCells = impassable,
            Campaign = new CampaignConfig
            {
                Label = "hero test", Nights = new[] { wave }, OffersRewards = false, RulesVersion = rules,
                CampaignId = ReleaseCampaign.CampaignId, PerkPool = PerkRules.ReleaseIds,
            },
        });

        static WavePlan Wave(params (long tick, EnemyKind kind, GridPos entry)[] spawns)
        {
            var entries = spawns.Select(s => s.entry).Distinct().ToList();
            return new WavePlan("w", spawns.Select(s => new SpawnEntry(s.tick, s.entry, entries.IndexOf(s.entry), s.kind)).ToArray(), entries);
        }

        /// <summary>Walks the hero during Day: <paramref name="steps"/> fixed steps on one heading.</summary>
        static void Walk(RunSimulation sim, int heading, int steps)
        {
            for (var i = 0; i < steps; i++) Assert.AreEqual(1, sim.AdvanceDay(0.05, new HeroCommand(heading, false, 0)));
        }

        /// <summary>Steps the night with a command function until <paramref name="until"/> holds; bounded.</summary>
        static void StepWith(RunSimulation sim, System.Func<HeroCommand> command, System.Func<bool> until, int max = 4000)
        {
            for (var i = 0; i < max; i++)
            {
                if (until()) return;
                Assert.AreEqual(RunState.Night, sim.State, $"night ended early at tick {sim.Tick}");
                sim.Step(command());
            }

            Assert.IsTrue(until(), $"condition not reached within {max} steps (tick {sim.Tick})");
        }

        [Test]
        public void MeleeEngagesOnlyAtACellCentreAndAnEnemyHittingABuildingIgnoresTheHero()
        {
            // The hero stands on the West route at (5.55, 10.45): within reach of the (5,10) centre, not of (4,10).
            var sim = NewHeroRun(Wave((0, EnemyKind.Grunt, MvpCampaign.West), (0, EnemyKind.Grunt, MvpCampaign.East)));
            Walk(sim, 12, 66); // west
            Walk(sim, 8, 14); // south
            var hero = sim.Hero;
            Assert.AreEqual((6660, 12540), (hero.UnitsX, hero.UnitsZ), "units are 1/1200 tile");
            Assert.IsTrue(sim.StartNight());
            var west = sim.Enemies.Single(e => e.EntryIndex == 0);
            var east = sim.Enemies.Single(e => e.EntryIndex == 1);
            var engagedAt = new GridPos(-1, -1);
            StepWith(sim, () => Still, () =>
            {
                if (west.TargetsHero && engagedAt.X < 0)
                {
                    Assert.IsFalse(west.Moving, "engaged mid-segment");
                    Assert.AreEqual((5.5, 10.5), (west.X, west.Z), "engages standing on a cell centre");
                    engagedAt = west.Cell;
                }

                return hero.Hp < HeroRules.MaxHp;
            });
            Assert.AreEqual(new GridPos(5, 10), engagedAt, "the first centre within engage range, never (4,10) at 1.05");
            Assert.AreEqual(HeroRules.MaxHp - west.Definition.AttackDamage, hero.Hp, "one normal hit, no Wall multiplier");
            // Stepping back beyond the hero's reach but inside the hold: the Grunt keeps the hero but cannot hit it.
            for (var i = 0; i < 9; i++) sim.Step(new HeroCommand(4, false, 48)); // east, to 0.73 from the Grunt
            var hp = hero.Hp;
            for (var i = 0; i < 2 * west.AttackTicks; i++) sim.Step(Still);
            Assert.IsTrue(west.TargetsHero, "still held at 0.73");
            Assert.AreEqual(hp, hero.Hp, "no melee hit from beyond the hero's reach");
            // The East Grunt walked a hero-free route and is hitting the Keep; it never turns to the hero.
            StepWith(sim, () => Still, () => east.TargetBuildingId == sim.Keep.Id);
            Assert.IsFalse(east.TargetsHero);
        }

        [Test]
        public void AnEnemyAttackingABuildingNeverSwitchesToAHeroBesideIt()
        {
            var sim = NewHeroRun(Wave((0, EnemyKind.Grunt, MvpCampaign.West)));
            Walk(sim, 12, 14); // to x 9.45
            Walk(sim, 8, 6); // to z 11.05: 0.55 from the Keep-side attack cell (9,10)
            Assert.IsTrue(sim.StartNight());
            var grunt = sim.Enemies[0];
            StepWith(sim, () => Still, () => grunt.TargetBuildingId == sim.Keep.Id);
            var keep = sim.Keep.Hp;
            for (var i = 0; i < 100; i++) sim.Step(Still);
            Assert.IsFalse(grunt.TargetsHero, "the route end is never a hero decision point");
            Assert.AreEqual(HeroRules.MaxHp, sim.Hero.Hp);
            Assert.Less(sim.Keep.Hp, keep);
        }

        [Test]
        public void AProvokedRaiderArcherTakesTheHeroOverAHouseAndForgetsItOutOfRange()
        {
            var sim = NewHeroRun(Wave((0, EnemyKind.Archer, MvpCampaign.West)));
            var house = Place(sim, BuildingKind.House, 5, 12); // the archer stops at (3,10): cell distance sqrt(8) <= 3.5
            Assert.IsTrue(sim.StartNight());
            var archer = sim.Enemies[0];
            StepWith(sim, () => Still, () => archer.TargetBuildingId == house.Id);
            Assert.AreEqual(new GridPos(3, 10), archer.Cell);

            // Walk up to the archer and swing once. Entering its range alone does not steal it from the House (sticky).
            var hero = sim.Hero;
            HeroCommand Approach(double tx, double tz, bool attack)
            {
                double dx = tx - hero.X, dz = tz - hero.Z;
                var close = dx * dx + dz * dz < 0.45 * 0.45;
                return new HeroCommand(close ? -1 : (HeroRules.FacingToward(dx, dz) + 2) / 4 % 16, attack && close, HeroRules.FacingToward(dx, dz));
            }

            StepWith(sim, () => Approach(archer.X, archer.Z, true), () => archer.Hp < archer.Definition.MaxHp);
            Assert.AreEqual(house.Id, archer.TargetBuildingId, "still on the House the step it is hit");
            StepWith(sim, () => Still, () => archer.TargetsHero, 3);
            Assert.AreEqual(-1, archer.TargetBuildingId);
            Assert.AreEqual(sim.Tick - 1 + archer.AttackTicks, archer.AttackDueTick, "retargeting the hero restarts the full interval");

            // Out of its 3.5 range the provocation ends and the House is its target again.
            StepWith(sim, () => Approach(12.5, 10.5, false), () => !archer.TargetsHero && archer.TargetBuildingId == house.Id, 400);
            Assert.IsTrue(archer.Alive);
        }

        [Test]
        public void HeroDeathIsNeverDefeatButAKeepFallingTheSameStepStillIs()
        {
            // Hero alone: it falls, the night goes on, it respawns at its spawn cell with full HP.
            var sim = NewHeroRun(Wave((0, EnemyKind.Grunt, MvpCampaign.West), (0, EnemyKind.Grunt, MvpCampaign.East)));
            Walk(sim, 12, 66);
            Walk(sim, 8, 14);
            Assert.IsTrue(sim.StartNight());
            var hero = sim.Hero;
            hero.Hp = 1;
            var west = sim.Enemies.Single(e => e.EntryIndex == 0);
            var east = sim.Enemies.Single(e => e.EntryIndex == 1);
            StepWith(sim, () => Still, () => !hero.Alive);
            sim.Step(Still);
            Assert.AreEqual(RunState.Night, sim.State, "the hero falling never ends the night");
            Assert.IsFalse(west.TargetsHero, "a downed hero is nobody's target");
            StepWith(sim, () => Still, () => hero.Alive, HeroRules.RespawnTicks + 2);
            Assert.AreEqual((HeroRules.MaxHp, sim.HeroSpawnCell(GridState.KeepCell)), (hero.Hp, hero.Cell));

            // Both fall on one step: the Keep decides it. West holds the hero on its route, East hits the Keep.
            sim = NewHeroRun(Wave((0, EnemyKind.Grunt, MvpCampaign.West), (0, EnemyKind.Grunt, MvpCampaign.East)));
            Walk(sim, 12, 66);
            Walk(sim, 8, 14);
            Assert.IsTrue(sim.StartNight());
            hero = sim.Hero;
            west = sim.Enemies.Single(e => e.EntryIndex == 0);
            east = sim.Enemies.Single(e => e.EntryIndex == 1);
            StepWith(sim, () => Still, () => west.TargetsHero && east.TargetBuildingId == sim.Keep.Id);
            (hero.Hp, sim.Keep.Hp) = (1, 1);
            west.AttackDueTick = east.AttackDueTick = sim.Tick;
            sim.Step(Still);
            Assert.IsFalse(hero.Alive, "the hero fell on this step");
            Assert.AreEqual(RunState.Defeat, sim.State, "and the Keep falling on the same step is still Defeat");
        }

        [Test]
        public void TheHeroNeverEntersSolidOrNonWalkableCellsOrCutsADiagonalCorner()
        {
            // Two rocks that touch only at a corner, north-east of (11,12).
            var sim = NewHeroRun(Wave((0, EnemyKind.Grunt, MvpCampaign.West)), new[] { new GridPos(11, 13), new GridPos(12, 12) });
            Place(sim, BuildingKind.Wall, 10, 13);
            var hero = sim.Hero;
            Assert.AreEqual(new GridPos(10, 11), hero.Cell);
            Walk(sim, 0, 20); // north into the Wall
            Assert.AreEqual((12600, 13 * 1200 - HeroRules.RadiusUnits), (hero.UnitsX, hero.UnitsZ), "stops touching the Wall");
            Walk(sim, 4, 20); // east along row 12 into the rock at (12,12)
            Assert.AreEqual(12 * 1200 - HeroRules.RadiusUnits, hero.UnitsX);
            for (var i = 0; i < 60; i++)
            {
                Walk(sim, 2, 1); // north-east into the corner between (11,13) and (12,12)
                Assert.IsFalse(sim.HeroOverlapsBlocked(hero.UnitsX, hero.UnitsZ), $"overlap at {hero.UnitsX},{hero.UnitsZ}");
            }

            Assert.AreEqual(new GridPos(11, 12), hero.Cell, "boxed into the corner cell, never through the diagonal gap");
        }

        [Test]
        public void TheHeroSpawnsOnTheNearestFreeCellAndStepsAsideForABuilding()
        {
            var sim = NewHeroRun(Wave((0, EnemyKind.Grunt, MvpCampaign.West)));
            Assert.AreEqual(new GridPos(10, 11), sim.Hero.Cell, "north of the Keep first");
            Place(sim, BuildingKind.Wall, 10, 11); // on the hero: the placement succeeds
            Assert.IsTrue(Drain(sim).Any(e => e.Type == SimEventType.HeroRelocated));
            Assert.AreEqual(new GridPos(10, 12), sim.Hero.Cell, "the nearest free cell from where it stood, north first");
            PlaceWalls(sim, (11, 10), (10, 9), (9, 10));
            // All four neighbours walled: distance 2, north-ranked cells (dominant axis z > 0) first, then (z, x).
            Assert.AreEqual(new GridPos(9, 11), sim.HeroSpawnCell(GridState.KeepCell), "outside the walls, by the fixed tie-break");
        }

        /// <summary>
        /// Continue replays the night from the Start Night checkpoint. Plan 019: under release-rules-4 the hero is still
        /// running when the night starts, so the checkpoint also carries its velocity (a resting replay would diverge), while
        /// an ordinary Day save comes back at rest on the spawn cell and an out-of-range saved velocity is refused.
        /// </summary>
        [TestCase(ReleaseCampaign.RulesV3)]
        [TestCase(ReleaseCampaign.RulesV4)]
        public void TheSameCommandsReplayTheNightAndAContinuedNightStartsWhereTheHeroStood(string rules)
        {
            const uint seed = 7;
            var catalog = new RulesCatalog(RulesProfile.TwelveNightsContact(ReleaseDefinitions.CreateDefault()), RulesProfile.TwelveNightsWalking(ReleaseDefinitions.CreateDefault()),
                RulesProfile.TwelveNightsHero(ReleaseDefinitions.CreateDefault()), RulesProfile.TwelveNights(ReleaseDefinitions.CreateDefault()));
            var sim = new RunSimulation(new RunConfig
            {
                Definitions = catalog.Find(rules, ReleaseCampaign.CampaignId).Definitions, Seed = seed,
                Terrain = MapProfiles.Generate(MapProfiles.Meadow, seed), Campaign = ReleaseCampaign.Create(seed, MapProfiles.Meadow, rules),
            });
            Place(sim, BuildingKind.Tower, 9, 9);
            if (rules == ReleaseCampaign.RulesV3) Walk(sim, 10, 9); // south-west, somewhere that is not the spawn cell
            else
                for (var i = 0; i < 9; i++) Assert.AreEqual(1, sim.AdvanceDay(0.05, HeroCommand.Fine(43, false, 43))); // still running at Start Night
            var json = JsonUtility.ToJson(CheckpointCodec.Capture(sim, nightStart: true));
            var day = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(json), catalog, out var problem, out var message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            Assert.AreEqual((sim.Hero.UnitsX, sim.Hero.UnitsZ), (day.Hero.UnitsX, day.Hero.UnitsZ), "Continue starts the night where the hero stood");
            if (rules == ReleaseCampaign.RulesV4)
            {
                Assert.AreNotEqual((0, 0), (sim.Hero.VelX, sim.Hero.VelZ), "the hero is moving at Start Night");
                Assert.AreEqual((sim.Hero.VelX, sim.Hero.VelZ), (day.Hero.VelX, day.Hero.VelZ), "and the continued night starts at that velocity");
                var resting = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(JsonUtility.ToJson(CheckpointCodec.Capture(sim))), catalog, out problem, out message);
                Assert.AreEqual(CheckpointProblem.None, problem, message);
                Assert.AreEqual((0, 0, sim.HeroSpawnCell(GridState.KeepCell)), (resting.Hero.VelX, resting.Hero.VelZ, resting.Hero.Cell), "a Day save loads at rest on the spawn cell");
                var bad = JsonUtility.FromJson<RunCheckpoint>(json);
                bad.heroVx = HeroProfile.V4.MaxSpeedUnits + 1;
                Assert.IsNull(CheckpointCodec.Restore(bad, catalog, out problem, out _), "a velocity above the top speed is refused");
                Assert.AreEqual(CheckpointProblem.Invalid, problem);
            }

            Assert.IsTrue(sim.StartNight());
            Assert.IsTrue(day.StartNight());
            for (var i = 0; i < 6000 && sim.State == RunState.Night; i++)
            {
                var command = ReleasePolicyTests.ActiveHeroCommand(sim);
                Assert.AreEqual(command, ReleasePolicyTests.ActiveHeroCommand(day));
                sim.Step(command);
                day.Step(command);
            }

            Assert.AreNotEqual(RunState.Night, sim.State, "the night finished within the bound");
            Assert.Greater(sim.Stats.HeroKills, 0, "the scripted hero took part");
            Assert.AreEqual(sim.CaptureSnapshot().Replace(" rev=" + sim.Grid.Revision, ""), day.CaptureSnapshot().Replace(" rev=" + day.Grid.Revision, ""));
            // Plan 019: the release-rules-3 outcome of this command stream, recorded before the release-rules-4 motor shared its
            // code. A change here means the legacy hero (movement, swing or engagement) drifted.
            if (rules == ReleaseCampaign.RulesV3) Assert.AreEqual(V3ReplayFingerprint, Fingerprint(sim.CaptureSnapshot()), "release-rules-3 outcome unchanged");
        }

        // ---------- release-rules-4 (plan 019, specs/003 sections 3-4) ----------

        static RunSimulation NewContactRun() => NewHeroRun(Wave((0, EnemyKind.Grunt, MvpCampaign.West)), null, ReleaseCampaign.RulesV4);

        static void PlaceHero(Hero hero, int ux, int uz)
        {
            hero.UnitsX = hero.PrevUnitsX = ux;
            hero.UnitsZ = hero.PrevUnitsZ = uz;
            hero.VelX = hero.VelZ = 0;
        }

        /// <summary>
        /// The motor's caps: from rest it needs several steps to top speed, no heading ever exceeds the top speed after
        /// rounding, release stops it within the braking cap, and reversing runs through rest instead of flipping. The same
        /// command stream gives the same state whether it is stepped one by one or advanced in uneven chunks.
        /// </summary>
        [Test]
        public void TheContactMotorAcceleratesBrakesReversesAndNeverExceedsItsCap()
        {
            var p = HeroProfile.V4;
            for (var h = 0; h < 64; h++)
            {
                int vx = 0, vz = 0, steps = 0;
                for (; steps < 20 && (long)vx * vx + (long)vz * vz < (long)(p.MaxSpeedUnits - 1) * (p.MaxSpeedUnits - 1); steps++)
                {
                    int px = vx, pz = vz;
                    HeroMotion.Accelerate(ref vx, ref vz, h, p.MaxSpeedUnits, p);
                    Assert.LessOrEqual((long)(vx - px) * (vx - px) + (long)(vz - pz) * (vz - pz), (long)p.AccelUnits * p.AccelUnits, $"heading {h}: one step changes at most the acceleration cap");
                }

                Assert.Greater(steps, 1, $"heading {h}: top speed is not instant");
                for (var i = 0; i < 5; i++) HeroMotion.Accelerate(ref vx, ref vz, h, p.MaxSpeedUnits, p);
                Assert.LessOrEqual((long)vx * vx + (long)vz * vz, (long)p.MaxSpeedUnits * p.MaxSpeedUnits, $"heading {h}: never above the cap");
            }

            var sim = NewContactRun();
            var hero = sim.Hero;
            for (var i = 0; i < 6; i++) sim.AdvanceDay(0.05, HeroCommand.Fine(16, false, 16)); // east at top speed
            Assert.AreEqual((p.MaxSpeedUnits, 0), (hero.VelX, hero.VelZ));
            var x = hero.UnitsX;
            sim.AdvanceDay(0.05, HeroCommand.Fine(-1, false, 16));
            Assert.AreEqual(p.MaxSpeedUnits - p.BrakeUnits, hero.VelX, "release brakes by the braking cap");
            sim.AdvanceDay(0.05, HeroCommand.Fine(-1, false, 16));
            Assert.AreEqual((0, x + (p.MaxSpeedUnits - p.BrakeUnits)), (hero.VelX, hero.UnitsX), "at rest after two steps");
            for (var i = 0; i < 6; i++) sim.AdvanceDay(0.05, HeroCommand.Fine(16, false, 16));
            var seen = new List<int>();
            for (var i = 0; i < 8; i++)
            {
                sim.AdvanceDay(0.05, HeroCommand.Fine(48, false, 48)); // reverse: west
                seen.Add(hero.VelX);
            }

            CollectionAssert.AreEqual(new[] { 28, 14, 0, -14, -28, -42, -42, -42 }, seen, "reversing decelerates through zero at the acceleration cap");

            // Equal command streams, different chunking: identical state.
            RunSimulation Chunked(int[] chunks)
            {
                var run = NewContactRun();
                Assert.IsTrue(run.StartNight());
                var commands = new[] { HeroCommand.Fine(5, false, 5), HeroCommand.Fine(-1, false, 5), HeroCommand.Fine(40, true, 33), HeroCommand.Fine(21, false, 21) };
                var k = 0;
                foreach (var c in commands)
                    for (var left = 24; left > 0;)
                    {
                        var n = System.Math.Min(left, chunks[k++ % chunks.Length]);
                        Assert.AreEqual(n, run.Advance(n * 0.05, c));
                        left -= n;
                    }

                return run;
            }

            Assert.AreEqual(Chunked(new[] { 1 }).CaptureSnapshot(), Chunked(new[] { 5, 2, 7, 1, 3 }).CaptureSnapshot());
        }

        /// <summary>
        /// Into a Wall and a diagonal corner the body stops touching (its 0.06-tile radius), the blocked velocity component is
        /// dropped so pressing stores nothing, and the view's prediction is exactly the next step without changing the hero.
        /// </summary>
        [Test]
        public void TheContactHeroSlidesStopsAtWallsAndPredictionIsTheNextStepWithoutWritingIt()
        {
            // Test-only impassable cells block whole (a drawn Wall blocks only its palisade: see the next test).
            var sim = NewHeroRun(Wave((0, EnemyKind.Grunt, MvpCampaign.West)), new[] { new GridPos(10, 13), new GridPos(11, 13), new GridPos(12, 12) }, ReleaseCampaign.RulesV4);
            var hero = sim.Hero;
            var r = HeroProfile.V4.RadiusUnits;
            void Check(HeroCommand c)
            {
                var before = sim.CaptureSnapshot();
                sim.PredictHeroStep(c, out var px, out var pz);
                Assert.AreEqual(before, sim.CaptureSnapshot(), "prediction writes nothing");
                Assert.AreEqual(1, sim.AdvanceDay(0.05, c));
                Assert.AreEqual((px, pz), (hero.UnitsX, hero.UnitsZ), "prediction equals the step");
                Assert.IsFalse(sim.HeroOverlapsBlocked(hero.UnitsX, hero.UnitsZ), $"overlap at {hero.UnitsX},{hero.UnitsZ}");
            }

            for (var i = 0; i < 70; i++) Check(HeroCommand.Fine(0, false, 0)); // north into the Wall
            Assert.AreEqual((12600, 13 * 1200 - r, 0), (hero.UnitsX, hero.UnitsZ, hero.VelZ), "touching the blocker, no stored push");
            for (var i = 0; i < 100; i++) Check(HeroCommand.Fine(6, false, 6)); // north-north-east: slides east along the Wall
            Assert.AreEqual((12 * 1200 - r, 13 * 1200 - r), (hero.UnitsX, hero.UnitsZ), "boxed into the corner of (11,12)");
            Assert.AreEqual((0, 0), (hero.VelX, hero.VelZ));
            for (var i = 0; i < 20; i++) Check(HeroCommand.Fine(8, false, 8)); // north-east into the diagonal gap
            Assert.AreEqual(new GridPos(11, 12), hero.Cell, "never through the diagonal gap between rocks");
        }

        /// <summary>
        /// Owner playtest 2026-10-21: buildings block the hero only where they are drawn, so it walks through the gap the art
        /// shows between a Tower and a House that touch at a corner (whole cells used to close it). By Day a swing runs its
        /// wind-up and contact on the hero's Day clock and hits nothing.
        /// </summary>
        [Test]
        public void TheContactHeroPassesBetweenDrawnBuildingsAndSwingsByDay()
        {
            var sim = NewContactRun();
            Place(sim, BuildingKind.Tower, 10, 13);
            Place(sim, BuildingKind.House, 11, 14);
            var hero = sim.Hero;
            PlaceHero(hero, 11 * 1200 + 600, 13 * 1200 + 600); // (11,13): south-east of the corner the two share
            for (var i = 0; i < 60 && hero.Cell != new GridPos(10, 14); i++)
            {
                Assert.AreEqual(1, sim.AdvanceDay(0.05, HeroCommand.Fine(56, false, 56))); // north-west through the corner gap
                Assert.IsFalse(sim.HeroOverlapsBlocked(hero.UnitsX, hero.UnitsZ));
            }

            Assert.AreEqual(new GridPos(10, 14), hero.Cell, "through the gap between the drawn models");

            // Terrain too: between the boulders of two neighbouring rock cells, where the art leaves a path.
            var map = TerrainMap.Flat();
            map.Cells[13 * GridCoordinates.Size + 11] = map.Cells[13 * GridCoordinates.Size + 12] = TerrainType.Rock;
            map.Cells[13 * GridCoordinates.Size + 8] = TerrainType.Water;
            var wave = Wave((0, EnemyKind.Grunt, MvpCampaign.West));
            var rocky = new RunSimulation(new RunConfig
            {
                Definitions = ReleaseCampaign.Contact(ReleaseDefinitions.CreateDefault()), Wave = wave, Terrain = map,
                Campaign = new CampaignConfig
                {
                    Label = "hero test", Nights = new[] { wave }, OffersRewards = false, RulesVersion = ReleaseCampaign.RulesV4,
                    CampaignId = ReleaseCampaign.CampaignId, PerkPool = PerkRules.ReleaseIds,
                },
            });
            PlaceHero(rocky.Hero, 12 * 1200, 12 * 1200 + 600); // on the line between the two rock cells, south of them
            for (var i = 0; i < 80 && rocky.Hero.Cell.Z < 14; i++)
            {
                rocky.AdvanceDay(0.05, HeroCommand.Fine(0, false, 0));
                Assert.IsFalse(rocky.HeroOverlapsBlocked(rocky.Hero.UnitsX, rocky.Hero.UnitsZ));
            }

            Assert.GreaterOrEqual(rocky.Hero.Cell.Z, 14, "between the boulders, not stopped at the rock cells' edge");

            // Water: the hero steps onto the drawn bank, but not into the water beyond it.
            PlaceHero(rocky.Hero, 8 * 1200 + 600, 12 * 1200 + 600);
            for (var i = 0; i < 60; i++) rocky.AdvanceDay(0.05, HeroCommand.Fine(0, false, 0));
            Assert.Greater(rocky.Hero.UnitsZ, 13 * 1200 - HeroProfile.V4.RadiusUnits, "onto the bank inside the water cell");
            Assert.LessOrEqual(rocky.Hero.UnitsZ, 13 * 1200 + HeroProfile.BankUnits - HeroProfile.V4.RadiusUnits, "never past the water's edge");

            // Connected Walls stay closed where their arms meet; the hero stops at the palisade line.
            Place(rocky, BuildingKind.Wall, 5, 13);
            Place(rocky, BuildingKind.Wall, 6, 13);
            PlaceHero(rocky.Hero, 6 * 1200, 12 * 1200 + 600); // under the joint between the two
            for (var i = 0; i < 60; i++) rocky.AdvanceDay(0.05, HeroCommand.Fine(0, false, 0));
            Assert.AreEqual(13 * 1200 + 600 - HeroProfile.WallHalfUnits - HeroProfile.V4.RadiusUnits, rocky.Hero.UnitsZ, "stopped at the palisade, no gap at the joint");

            // The 1 x 1 Cannon (owner playtest 2026-10-21): the hero fits between the gun and its corner props, not into them.
            Place(rocky, BuildingKind.Cannon, 3, 16);
            const int gx = 3 * 1200 + 600, gz = 16 * 1200 + 600, gap = 317, prop = HeroProfile.FieldGunPropOffsetUnits;
            Assert.IsFalse(rocky.HeroOverlapsBlocked(gx + gap, gz + gap), "between the gun and the corner props");
            Assert.IsTrue(rocky.HeroOverlapsBlocked(gx + prop, gz + prop), "on the props");
            Assert.IsFalse(rocky.HeroOverlapsBlocked(gx - prop, gz + prop), "the empty corner");
            Assert.IsTrue(rocky.HeroOverlapsBlocked(gx + 300, gz), "on the gun");

            Drain(sim);
            sim.AdvanceDay(0.05, HeroCommand.Fine(-1, true, 0));
            Assert.IsTrue(hero.AttackPending, "accepted by Day");
            for (var i = 0; i < hero.Profile.WindUpTicks; i++) sim.AdvanceDay(0.05, HeroCommand.Fine(-1, false, 0));
            var events = Drain(sim);
            Assert.AreEqual(0, events.Single(e => e.Type == SimEventType.HeroContact).TargetId, "a Day swing reaches contact and hits nothing");
            Assert.AreEqual(RunState.Day, sim.State);
        }

        /// <summary>
        /// One attack, one contact: accepted on the step the button is read, damage exactly once three steps later against the
        /// committed facing and the hero's position then, nothing before it and nothing more while recovering. The misses
        /// come first, because a struck Grunt turns on the hero.
        /// </summary>
        [Test]
        public void AContactAttackHitsOnceAtItsDueStepWithTheCommittedAim()
        {
            var sim = NewContactRun();
            Assert.IsTrue(sim.StartNight());
            var grunt = sim.Enemies[0];
            StepWith(sim, () => Still, () => grunt.TargetBuildingId == sim.Keep.Id); // stands on (9,10) hitting the Keep
            var hero = sim.Hero;
            var p = hero.Profile;
            const int south = 32, north = 0;
            var hp = grunt.Hp;

            // Accepted toward the Grunt near the edge of reach, then backing off during the wind-up: the hero's position at
            // contact decides, so it misses.
            PlaceHero(hero, 9 * 1200 + 600, 10 * 1200 + 600 + 258);
            Drain(sim);
            sim.Step(HeroCommand.Fine(-1, true, south));
            for (var i = 0; i < p.WindUpTicks; i++) sim.Step(HeroCommand.Fine(north, false, south));
            Assert.AreEqual(0, Drain(sim).Single(e => e.Type == SimEventType.HeroContact).TargetId, "out of reach at contact");
            Assert.AreEqual(hp, grunt.Hp);

            // Accepted facing north (away) beyond touching distance: turning to the Grunt during the wind-up still misses.
            while (sim.Tick < hero.SwingReadyTick) sim.Step(Still);
            PlaceHero(hero, 9 * 1200 + 600, 10 * 1200 + 600 + 180); // 0.15 tile north of the Grunt
            Drain(sim);
            sim.Step(HeroCommand.Fine(-1, true, north));
            for (var i = 0; i < p.WindUpTicks; i++) sim.Step(HeroCommand.Fine(-1, false, south));
            Assert.AreEqual(0, Drain(sim).Single(e => e.Type == SimEventType.HeroContact).TargetId, "a miss still reaches contact");
            Assert.AreEqual(hp, grunt.Hp);

            while (sim.Tick < hero.SwingReadyTick) sim.Step(Still);
            Drain(sim);
            var accepted = sim.Tick;
            sim.Step(HeroCommand.Fine(-1, true, south));
            Assert.IsTrue(Drain(sim).Any(e => e.Type == SimEventType.HeroAttacked && e.TargetId == south), "accepted with its facing");
            for (var i = 1; i < p.WindUpTicks; i++)
            {
                sim.Step(HeroCommand.Fine(-1, true, north)); // the aim swings away during the wind-up
                Assert.AreEqual(hp, grunt.Hp, $"no damage {i} steps into the wind-up");
            }

            sim.Step(HeroCommand.Fine(-1, true, north));
            var contact = Drain(sim);
            Assert.AreEqual(accepted + p.WindUpTicks, contact.Single(e => e.Type == SimEventType.HeroContact).Tick);
            Assert.AreEqual(hp - p.SwingDamage, grunt.Hp, "the committed facing hits, once");
            Assert.AreEqual(grunt.Id, contact.Single(e => e.Type == SimEventType.HeroHit).TargetId);
            hp = grunt.Hp;
            while (sim.Tick < accepted + p.SwingIntervalTicks) sim.Step(HeroCommand.Fine(-1, true, south));
            Assert.AreEqual(hp, grunt.Hp, "no second contact while recovering");
            sim.Step(HeroCommand.Fine(-1, true, south));
            Assert.IsTrue(Drain(sim).Any(e => e.Type == SimEventType.HeroAttacked), "held attack is accepted again when ready");
        }

        /// <summary>
        /// Threat (release-rules-4): an enemy touching the hero is struck even behind its aim; struck, the Grunt leaves the
        /// Keep to chase the hero, gives up once the hero is beyond the leash, walks back to its cell centre and takes its
        /// route to the Keep again. Struck again (specs/008), it does not chase a second time: it turns on the hero where it
        /// stands while the hero is within its hit reach, then resumes the Keep with the same attack timing.
        /// </summary>
        [Test]
        public void AStruckEnemyChasesTheHeroOnceThenOnlyFightsBackInPlace()
        {
            var sim = NewContactRun();
            Assert.IsTrue(sim.StartNight());
            var grunt = sim.Enemies[0];
            StepWith(sim, () => Still, () => grunt.TargetBuildingId == sim.Keep.Id);
            var hero = sim.Hero;
            var hp = grunt.Hp;
            PlaceHero(hero, 9 * 1200 + 600, 10 * 1200 + 600 + 96); // 0.08 tile north, touching
            sim.Step(HeroCommand.Fine(-1, true, 0)); // aimed north, away from the Grunt
            for (var i = 0; i < hero.Profile.WindUpTicks; i++) sim.Step(Still);
            Assert.AreEqual(hp - hero.Profile.SwingDamage, grunt.Hp, "touching: struck whatever the aim");
            StepWith(sim, () => Still, () => grunt.Pursuing, 5);
            Assert.AreEqual(-1, grunt.TargetBuildingId, "it left the Keep for the hero");
            StepWith(sim, () => HeroCommand.Fine(0, false, 0), () => !grunt.Pursuing, 200); // run north out of the leash
            Assert.Greater(System.Math.Sqrt((grunt.X - hero.X) * (grunt.X - hero.X) + (grunt.Z - hero.Z) * (grunt.Z - hero.Z)), hero.Profile.PursuitLeash - 0.1);
            StepWith(sim, () => Still, () => !grunt.Returning, 200);
            Assert.AreEqual((grunt.Cell.X + 0.5, grunt.Cell.Z + 0.5), (grunt.X, grunt.Z), "back on a cell centre");
            StepWith(sim, () => Still, () => grunt.TargetBuildingId == sim.Keep.Id, 1000);

            GridCoordinates.TileCenter(grunt.Cell, out var gx, out var gz);
            var (ux, uz) = ((int)System.Math.Round(gx * 1200), (int)System.Math.Round(gz * 1200));
            PlaceHero(hero, ux, uz + 96); // touching again, inside its hit reach
            sim.Step(HeroCommand.Fine(-1, true, 0));
            for (var i = 0; i < hero.Profile.WindUpTicks; i++) sim.Step(Still);
            StepWith(sim, () => Still, () => grunt.TargetsHero, 5);
            Assert.IsFalse(grunt.Pursuing || grunt.Returning, "its one chase is used: it fights where it stands");
            Assert.AreEqual((gx, gz), (grunt.X, grunt.Z));

            PlaceHero(hero, ux, uz + 300); // 0.25 tile: beyond its 0.225 hit reach, so it lets go at once
            var due = grunt.AttackDueTick;
            var keepHp = sim.Keep.Hp;
            Assert.Less(due, sim.Tick + grunt.AttackTicks - 1, "scenario: the kept timing must differ from a restarted interval");
            sim.Step(Still);
            Assert.IsFalse(grunt.TargetsHero, "no hold beyond its hit reach");
            Assert.AreEqual(sim.Keep.Id, grunt.TargetBuildingId, "back on the Keep");
            Assert.AreEqual(sim.Keep.Hp < keepHp ? due + grunt.AttackTicks : due, grunt.AttackDueTick, "the attack timing is kept, not restarted");
            StepWith(sim, () => Still, () => sim.Keep.Hp < keepHp, (int)System.Math.Max(1, due - sim.Tick + 1));
        }

        /// <summary>
        /// Routes (release-rules-4) keep their length but go straight from the entry and turn late: with the Keep's west side
        /// walled, the West route runs along row 10 before stepping round, instead of stepping sideways at the entry.
        /// </summary>
        [Test]
        public void ContactRulesRoutesGoStraightAndTurnLate()
        {
            var legacy = NewHeroRun(Wave((0, EnemyKind.Grunt, MvpCampaign.West)), null, ReleaseCampaign.RulesV3);
            var v4 = NewContactRun();
            Place(legacy, BuildingKind.Wall, 9, 10);
            Place(v4, BuildingKind.Wall, 9, 10);
            var before = legacy.Planner.Plan(legacy.Grid, MvpCampaign.West, null);
            var route = v4.Planner.Plan(v4.Grid, MvpCampaign.West, null);
            Assert.IsTrue(route.Found && !route.IsBreach);
            Assert.AreEqual(before.Cost, route.Cost, "same length");
            for (var i = 1; i <= 8; i++) Assert.AreEqual(new GridPos(i, 10), route.Cells[i], "straight along row 10 first");
            for (var i = 1; i < route.Cells.Count; i++) Assert.AreEqual(1, route.Cells[i - 1].ManhattanDistance(route.Cells[i]), "cardinal steps");
            Assert.IsTrue(route.Goal.IsCardinalNeighbor(GridState.KeepCell));
        }

        /// <summary>A hero that falls during the wind-up loses that attack: no contact and no damage afterwards.</summary>
        [Test]
        public void DeathCancelsAnUnlandedContactAttack()
        {
            var sim = NewContactRun();
            Assert.IsTrue(sim.StartNight());
            var grunt = sim.Enemies[0];
            var hero = sim.Hero;
            // Standing just off the (5,10) centre on the West route: the Grunt engages the hero there.
            PlaceHero(hero, 5 * 1200 + 600, 10 * 1200 + 600 + 120);
            StepWith(sim, () => Still, () => grunt.TargetsHero);
            sim.Step(HeroCommand.Fine(-1, true, 32));
            Assert.IsTrue(hero.AttackPending);
            (hero.Hp, grunt.AttackDueTick) = (1, sim.Tick);
            var hp = grunt.Hp;
            Drain(sim);
            for (var i = 0; i < hero.Profile.WindUpTicks + 2; i++) sim.Step(Still);
            var events = Drain(sim);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.HeroDowned));
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.HeroContact || e.Type == SimEventType.HeroHit), "the attack died with the hero");
            Assert.AreEqual(hp, grunt.Hp);
        }

        /// <summary>A strike line that grazes the corner of a solid cell is blocked; one along open cells is not.</summary>
        [Test]
        public void AContactStrikeCannotPassASolidCorner()
        {
            bool Solid(GridPos c) => c == new GridPos(10, 11);
            const int u = HeroRules.UnitsPerTile;
            Assert.IsFalse(HeroCombatRules.StrikeClear(10 * u - 100, 11 * u + 100, 11 * u + 100, 11 * u + 100, Solid), "through the solid cell");
            Assert.IsFalse(HeroCombatRules.StrikeClear(10 * u - 100, 11 * u + 100, 10 * u + 100, 11 * u - 100, Solid), "touching only its corner");
            Assert.IsTrue(HeroCombatRules.StrikeClear(10 * u - 100, 11 * u - 10, 10 * u + 100, 11 * u - 10, Solid), "along open cells just below it");
        }

        const ulong V3ReplayFingerprint = 12213610989753421279UL; // recorded on adac6b4 (plans/evidence/019/baseline/Pin.xml)

        /// <summary>FNV-1a of a snapshot with normalised line ends.</summary>
        static ulong Fingerprint(string snapshot)
        {
            var h = 14695981039346656037UL;
            foreach (var ch in snapshot.Replace("\r\n", "\n"))
            {
                h ^= ch;
                h *= 1099511628211UL;
            }

            return h;
        }
    }
}
