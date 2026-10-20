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

        /// <summary>A one-night release-rules-2 run on the flat board, so the hero exists and nothing else varies.</summary>
        static RunSimulation NewHeroRun(WavePlan wave, IReadOnlyList<GridPos> impassable = null) => new RunSimulation(new RunConfig
        {
            Definitions = ReleaseDefinitions.CreateDefault(), Wave = wave, ImpassableCells = impassable,
            Campaign = new CampaignConfig
            {
                Label = "hero test", Nights = new[] { wave }, OffersRewards = false, RulesVersion = ReleaseCampaign.RulesV2,
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

        [Test]
        public void TheSameCommandsReplayTheNightAndAContinuedNightStartsWhereTheHeroStood()
        {
            const uint seed = 7;
            var catalog = new RulesCatalog(RulesProfile.TwelveNightsWalking(ReleaseDefinitions.CreateDefault()),
                RulesProfile.TwelveNightsHero(ReleaseDefinitions.CreateDefault()), RulesProfile.TwelveNights(ReleaseDefinitions.CreateDefault()));
            var sim = new RunSimulation(new RunConfig
            {
                Definitions = catalog.Default.Definitions, Seed = seed, Terrain = MapProfiles.Generate(MapProfiles.Meadow, seed),
                Campaign = ReleaseCampaign.Create(seed, MapProfiles.Meadow),
            });
            Place(sim, BuildingKind.Tower, 9, 9);
            Walk(sim, 10, 9); // south-west, somewhere that is not the spawn cell
            var json = JsonUtility.ToJson(CheckpointCodec.Capture(sim, nightStart: true));
            var day = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(json), catalog, out var problem, out var message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            Assert.AreEqual((sim.Hero.UnitsX, sim.Hero.UnitsZ), (day.Hero.UnitsX, day.Hero.UnitsZ), "Continue starts the night where the hero stood");

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
        }
    }
}
