using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using static TheLastFortress.Tests.SimTestUtil;

namespace TheLastFortress.Tests
{
    /// <summary>
    /// Spec 017 under release-rules-4: a House earns 4 + 2 and a Farm 8 + 3 per free fertile cell touching its footprint by
    /// an edge. A building or trap on a cell removes the field; enemies do not; a shared cell feeds each neighbour (D2-a).
    /// A Grunt from the West walks row 10 on the flat board.
    /// </summary>
    public class LandIncomeTests
    {
        // The provisional values come from the release-rules-4 definitions; the tests protect the rule, not the numbers.
        static readonly BuildingDefinition House = ReleaseCampaign.Contact(ReleaseDefinitions.CreateDefault()).House;
        static readonly BuildingDefinition Farm = ReleaseCampaign.Contact(ReleaseDefinitions.CreateDefault()).Farm;

        static RunSimulation NewLandRun(HashSet<GridPos> fertile = null)
        {
            var wave = new WavePlan("w", new[] { new SpawnEntry(0, MvpCampaign.West, 0) }, new[] { MvpCampaign.West });
            var defs = ReleaseCampaign.Contact(ReleaseDefinitions.CreateDefault());
            defs.StartingGold = 2000;
            var map = TerrainMap.Flat();
            map.Fertile = fertile;
            return new RunSimulation(new RunConfig
            {
                Definitions = defs, Wave = wave, Terrain = map,
                Campaign = new CampaignConfig
                {
                    Label = "land test", Nights = new[] { wave }, OffersRewards = false, RulesVersion = ReleaseCampaign.RulesV4,
                    CampaignId = ReleaseCampaign.CampaignId, PerkPool = PerkRules.ReleaseIds,
                },
            });
        }

        /// <summary>
        /// A Farm at (5,5) and its eight edge neighbours. Packed in Walls it earns only its base; in the open it earns a field
        /// for each fertile neighbour, and a cell missing from the map's fertile list is not one (so "any land" fails).
        /// </summary>
        [TestCase(true, false, 0)]
        [TestCase(false, false, 8)]
        [TestCase(false, true, 3)]
        public void AFarmEarnsItsBasePlusEachFreeFertileField(bool packed, bool threeFertile, int fields)
        {
            var fertile = threeFertile ? new HashSet<GridPos> { new GridPos(4, 5), new GridPos(5, 7), new GridPos(7, 6), new GridPos(9, 9) } : null;
            var sim = NewLandRun(fertile);
            var farm = Place(sim, BuildingKind.Farm, 5, 5);
            if (packed) PlaceWalls(sim, (4, 5), (4, 6), (7, 5), (7, 6), (5, 4), (6, 4), (5, 7), (6, 7));
            var expected = Farm.DailyIncome + fields * Farm.LandIncome;
            Assert.AreEqual(expected, sim.BuildingIncome(farm));
            Assert.AreEqual(expected, sim.IncomeForecast().Farms, "forecast and payment use the same rule");
        }

        /// <summary>A Spike Trap on a field takes it; a Grunt standing on a field (raiding the House) does not.</summary>
        [Test]
        public void ATrapRemovesAFieldAndAnEnemyOnItDoesNot()
        {
            var sim = NewLandRun();
            var house = Place(sim, BuildingKind.House, 5, 11);
            Assert.AreEqual(House.DailyIncome + 4 * House.LandIncome, sim.BuildingIncome(house));
            Place(sim, BuildingKind.SpikeTrap, 4, 11);
            Assert.AreEqual(House.DailyIncome + 3 * House.LandIncome, sim.BuildingIncome(house), "a trap removes its field");

            Assert.IsTrue(sim.StartNight());
            StepUntil(sim, () => sim.Enemies.Count > 0 && sim.Enemies[0].Cell == new GridPos(5, 10) && sim.Enemies[0].RaidBuildingId == house.Id);
            Assert.AreEqual(House.DailyIncome + 3 * House.LandIncome, sim.BuildingIncome(house), "the raider on (5,10) leaves the field");
        }

        /// <summary>
        /// D2-a and the owner's v0.3 terms: two Houses sharing a field each count it; Market Square pays +1 per field (not +4
        /// for a neighbouring House); a Farm keeps its +5 House link and Efficient Farming floors the whole sum.
        /// </summary>
        [Test]
        public void SharedFieldsCountForEachNeighbourAndPerksSitOnTop()
        {
            var sim = NewLandRun();
            Place(sim, BuildingKind.House, 5, 5);
            Place(sim, BuildingKind.House, 7, 5);
            int H(int fields, int perField) => House.DailyIncome + fields * perField;
            Assert.AreEqual(2 * H(4, House.LandIncome), sim.IncomeForecast().Houses, "(6,5) feeds both Houses");
            Assert.AreEqual(2 * H(4, House.LandIncome + 1), sim.IncomeForecastWith(PerkRules.MarketSquare).Houses, "Market Square: +1 per field");

            var touching = Place(sim, BuildingKind.House, 5, 6); // beside the first House: a neighbour, so it takes a field
            Assert.AreEqual(H(3, House.LandIncome + 1), EconomyRules.BuildingIncome(sim.Grid, touching, 0, PerkRules.MarketSquareBonus), "no +4 for the neighbouring House");

            var farm = Place(sim, BuildingKind.Farm, 12, 12);
            Place(sim, BuildingKind.House, 11, 12);
            var linked = Farm.DailyIncome + 7 * Farm.LandIncome + EconomyRules.FarmHouseBonus;
            Assert.AreEqual(linked, sim.BuildingIncome(farm));
            Assert.AreEqual(linked * (100 + PerkRules.EfficientFarmingPercent) / 100, sim.IncomeForecastWith(PerkRules.EfficientFarming).Farms);
        }

        /// <summary>
        /// The fertile list survives a save; a save without it (older runs) counts every free land cell, and the legacy
        /// release-rules-3 House keeps its flat 10 a day.
        /// </summary>
        [Test]
        public void FertileLandRoundTripsThroughSavesAndOlderSavesCountAllLand()
        {
            var catalog = new RulesCatalog(RulesProfile.TwelveNightsContact(ReleaseDefinitions.CreateDefault()));
            var terrain = MapProfiles.Generate(MapProfiles.Meadow, 3);
            Assert.IsNotNull(terrain.Fertile, "scenario: a generated map lists fertile land");
            var sim = new RunSimulation(new RunConfig
            {
                Definitions = catalog.Find(ReleaseCampaign.RulesV4, ReleaseCampaign.CampaignId).Definitions, Seed = 3,
                Terrain = terrain, Campaign = ReleaseCampaign.Create(3, MapProfiles.Meadow, ReleaseCampaign.RulesV4),
            });
            // Scenario: the first Farm site, in (z, x) order, with eight land neighbours of which some but not all are fertile.
            Building farm = null;
            for (var z = 2; z < 17 && farm == null; z++)
            for (var x = 2; x < 17 && farm == null; x++)
            {
                var cells = sim.Definitions.Farm.Footprint(new GridPos(x, z));
                var land = 0;
                foreach (var c in cells)
                foreach (var d in GridPos.CardinalOrder)
                    if (System.Array.IndexOf(cells, c + d) < 0 && terrain[c + d] == TerrainType.Land && sim.Grid.GetOccupant(c + d) == null) land++;
                var fields = EconomyRules.FieldCells(sim.Grid, cells).Count;
                if (land == 8 && fields > 0 && fields < 8 && sim.TryPlace(BuildingKind.Farm, new GridPos(x, z), out var placed) == PlacementRejection.None) farm = placed;
            }

            Assert.IsNotNull(farm, "scenario: a Farm site with partly fertile neighbours");
            var income = sim.BuildingIncome(farm);
            var json = JsonUtility.ToJson(CheckpointCodec.Capture(sim));

            var back = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(json), catalog, out var problem, out var message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            CollectionAssert.AreEquivalent(terrain.Fertile, back.Config.Terrain.Fertile);
            Assert.AreEqual(income, back.BuildingIncome(back.GetBuilding(farm.Id)));

            var older = Regex.Replace(Regex.Replace(json, ",?\"fertile\":\\[[^\\]]*\\]", ""), ",?\"fertileListed\":(true|false)", "");
            Assert.IsFalse(older.Contains("fertile"), "scenario: a save from before fertile land");
            back = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(older), catalog, out problem, out message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            Assert.IsNull(back.Config.Terrain.Fertile);
            Assert.AreEqual(Farm.DailyIncome + 8 * Farm.LandIncome, back.BuildingIncome(back.GetBuilding(farm.Id)), "every free land cell is a field");

            var legacy = NewRun(defs: ReleaseCampaign.Walking(ReleaseDefinitions.CreateDefault()));
            Assert.AreEqual(10, legacy.BuildingIncome(Place(legacy, BuildingKind.House, 5, 5)), "release-rules-3 keeps flat income");
        }
    }
}
