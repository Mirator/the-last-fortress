using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TheLastFortress.Tests
{
    /// <summary>Spec 017 D6/D7: the Keep and the entries belong to the map; the campaign names entries by index.</summary>
    public class MapKeepAndEntriesTests
    {
        static readonly GridPos[] Five = { new GridPos(0, 4), new GridPos(19, 14), new GridPos(8, 19), new GridPos(13, 0), new GridPos(0, 15) };

        /// <summary>
        /// v0.4: a night keeps its entry count capped at k, a missing index takes the lowest unused one, and Nights 8-12 open
        /// every entry. Night 7 (W E S) tells "lowest unused" apart from dropping or wrapping on a 3-entry map.
        /// </summary>
        [TestCase(4, 7, new[] { 0, 1, 3 })]
        [TestCase(3, 7, new[] { 0, 1, 2 })]
        [TestCase(3, 5, new[] { 0, 1, 2 })]
        [TestCase(3, 12, new[] { 0, 1, 2 })]
        [TestCase(5, 7, new[] { 0, 1, 3 })]
        [TestCase(5, 8, new[] { 0, 1, 2, 3, 4 })]
        public void NightsUseTheMapsEntriesByIndex(int k, int night, int[] expected)
        {
            var map = Five.Take(k).ToList();
            var r = ReleaseCampaign.Table(ReleaseCampaign.RulesV4)[night - 1];
            CollectionAssert.AreEqual(expected.Select(i => map[i]), ReleaseCampaign.NightEntries(night, r, map));
        }

        /// <summary>
        /// A map with its Keep off centre and three entries: the Keep stands there, every entry's route ends beside it, the
        /// stored nights use the map's entries, and a save brings back the same Keep, entries and nights.
        /// </summary>
        [Test]
        public void AnOffCentreKeepAndItsEntriesPlayAndSurviveASave()
        {
            var map = TerrainMap.Flat();
            map.Authored = false;
            map.Profile = MapProfiles.Meadow;
            map.Seed = 9;
            map.Keep = new GridPos(7, 12);
            map.Entries = Five.Take(3).ToList();
            var catalog = new RulesCatalog(RulesProfile.TwelveNightsContact(ReleaseDefinitions.CreateDefault()));
            var sim = new RunSimulation(new RunConfig
            {
                Definitions = catalog.Find(ReleaseCampaign.RulesV4, ReleaseCampaign.CampaignId).Definitions, Seed = 9,
                Terrain = map, Campaign = ReleaseCampaign.Create(9, map, ReleaseCampaign.RulesV4),
            });

            Assert.AreEqual(map.Keep, sim.Keep.Cell);
            CollectionAssert.IsEmpty(sim.ValidateMap());
            foreach (var entry in map.Entries)
            {
                var route = sim.GetLiveRoute(entry);
                Assert.IsTrue(route.Found && !route.IsBreach, $"{entry} has an open route");
                Assert.IsTrue(route.Goal.IsCardinalNeighbor(map.Keep), $"{entry} ends beside the Keep");
            }

            CollectionAssert.AreEquivalent(map.Entries, sim.Campaign.Nights.SelectMany(n => n.EntryCells).Distinct());

            var json = JsonUtility.ToJson(CheckpointCodec.Capture(sim));
            var back = CheckpointCodec.Restore(JsonUtility.FromJson<RunCheckpoint>(json), catalog, out var problem, out var message);
            Assert.AreEqual(CheckpointProblem.None, problem, message);
            Assert.AreEqual(map.Keep, back.Keep.Cell);
            CollectionAssert.AreEqual(map.Entries, back.Config.Terrain.Entries);
            for (var n = 0; n < 12; n++) CollectionAssert.AreEqual(sim.Campaign.Nights[n].EntryCells, back.Campaign.Nights[n].EntryCells);
        }
    }
}
