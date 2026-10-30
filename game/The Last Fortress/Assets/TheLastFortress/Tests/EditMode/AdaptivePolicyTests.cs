using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TheLastFortress.Tests
{
    public class AdaptivePolicyTests
    {
        /// <summary>
        /// Spec 017 section 4: the adaptive builders must give a legal, repeatable plan on any board as played, or the probe
        /// measures a broken script instead of the rules. Every profile in all 8 orientations, seeds 1-2: the same plan twice;
        /// no placement off buildable land, on the reserved ring or over another planned building; the corridor never seals
        /// an entry that had an open Day-1 route; and a real plan (at least 20 defense items). Wins are not asserted.
        /// </summary>
        [Test]
        public void AdaptiveScriptsPlanLegalRepeatableLayoutsOnEveryOrientation()
        {
            var failures = new List<string>();
            foreach (var map in MapProfiles.All)
            for (var symmetry = 0; symmetry < 8; symmetry++)
            for (uint seed = 1; seed <= 2; seed++)
            foreach (var policy in AdaptivePolicies.All)
            {
                var where = $"{policy.Name} {map} s{symmetry} seed {seed}";
                var sim = NewRun(map, seed, symmetry);
                var (defense, economy) = policy.Build(sim);
                var again = policy.Build(NewRun(map, seed, symmetry));
                if (!defense.SequenceEqual(again.defense) || !economy.SequenceEqual(again.economy)) failures.Add($"{where}: plan differs between runs");
                if (defense.Length < 20) failures.Add($"{where}: only {defense.Length} defense items");

                var taken = new HashSet<GridPos> { sim.Keep.Cell };
                var solids = new List<GridPos>();
                foreach (var item in defense.Concat(economy))
                {
                    var p = item.Split(' ');
                    if (p.Length != 3 || p[1].StartsWith("vein")) continue; // upgrades and veins name no new cells
                    var kind = (BuildingKind)System.Enum.Parse(typeof(BuildingKind), p[0]);
                    foreach (var c in sim.Definitions.For(kind).Footprint(new GridPos(int.Parse(p[1]), int.Parse(p[2]))))
                    {
                        if (!GridCoordinates.IsInBounds(c) || GridCoordinates.IsReservedRing(c) || sim.Grid.IsImpassableTerrain(c) || !taken.Add(c))
                            failures.Add($"{where}: {item} is not on free buildable land");
                        if (kind != BuildingKind.SpikeTrap) solids.Add(c);
                    }
                }

                if (policy != AdaptivePolicies.Corridor) continue;
                foreach (var entry in sim.Campaign.Nights.SelectMany(n => n.EntryCells).Distinct())
                {
                    var before = sim.GetLiveRoute(entry);
                    var after = sim.Planner.Plan(sim.Grid, entry, solids);
                    if (before.Found && !before.IsBreach && (!after.Found || after.IsBreach)) failures.Add($"{where}: seals entry {entry}");
                }
            }

            CollectionAssert.IsEmpty(failures);
        }

        static RunSimulation NewRun(string map, uint seed, int symmetry) => new RunSimulation(new RunConfig
        {
            Definitions = ReleaseCampaign.Contact(ReleaseDefinitions.CreateDefault()), Seed = seed,
            Terrain = MapProfiles.Generate(map, seed, symmetry), Campaign = ReleaseCampaign.Create(seed, map, symmetry, ReleaseCampaign.RulesV4),
        });
    }
}
