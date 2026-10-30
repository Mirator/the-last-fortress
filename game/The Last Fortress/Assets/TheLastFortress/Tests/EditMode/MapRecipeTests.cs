using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TheLastFortress.Tests
{
    public class MapRecipeTests
    {
        /// <summary>
        /// Spec 017 D8 gate, seeds 1-100 on each recipe: the same map twice; a generated map passes every rule (open routes
        /// of 9-22 steps from 3-5 spaced entries, a choke and an open approach, a Keep in the central area with two clear
        /// sides, no isolated pocket, 50-110 fertile cells); fallbacks stay under 2%. Attempt failure reasons are logged so
        /// a recipe that needs many attempts shows why.
        /// </summary>
        [Test]
        public void RecipesMakeValidRepeatableMapsAndRarelyFallBack()
        {
            var failures = new List<string>();
            var summary = new List<string>();
            foreach (var recipe in MapRecipes.All)
            {
                int fallbacks = 0, attempts = 0, minFertile = int.MaxValue, maxFertile = 0;
                var entries = new int[MapRecipes.MaxEntries + 1];
                var reasons = new Dictionary<string, int>();
                for (uint seed = 1; seed <= 100; seed++)
                {
                    var map = MapRecipes.Generate(recipe, seed);
                    var again = MapRecipes.Generate(recipe, seed);
                    if (!map.Cells.SequenceEqual(again.Cells) || map.Keep != again.Keep || !map.Entries.SequenceEqual(again.Entries) ||
                        !map.Fertile.SetEquals(again.Fertile) || !map.Veins.SequenceEqual(again.Veins))
                        failures.Add($"{recipe} seed {seed}: not deterministic");

                    for (var a = 0; a < (map.Fallback ? MapRecipes.MaxAttempts : map.Attempt); a++)
                    {
                        var c = MapRecipes.Candidate(recipe, seed, a);
                        var why = c == null ? "entries" : System.Text.RegularExpressions.Regex.Replace(MapRecipes.Validate(c) ?? "?", @"[\d(), ]+", " ").Trim();
                        reasons[why] = reasons.TryGetValue(why, out var n) ? n + 1 : 1;
                    }

                    if (map.Fallback) fallbacks++;
                    else
                    {
                        attempts += map.Attempt + 1;
                        var why = MapRecipes.Validate(map);
                        if (why != null) failures.Add($"{recipe} seed {seed}: {why}");
                        minFertile = System.Math.Min(minFertile, map.Fertile.Count);
                        maxFertile = System.Math.Max(maxFertile, map.Fertile.Count);
                        entries[map.Entries.Count]++;
                        foreach (var e in map.Entries)
                            if (map.Entries.Any(o => o != e && System.Math.Max(System.Math.Abs(o.X - e.X), System.Math.Abs(o.Z - e.Z)) < MapRecipes.EntrySpacing))
                                failures.Add($"{recipe} seed {seed}: entries too close");
                    }
                }

                if (fallbacks >= 2) failures.Add($"{recipe}: {fallbacks}/100 fallbacks");
                summary.Add($"{recipe}: fallbacks {fallbacks}/100, mean attempts {attempts / (double)System.Math.Max(1, 100 - fallbacks):0.0}, " +
                            $"fertile {minFertile}-{maxFertile}, entries 3/4/5 = {entries[3]}/{entries[4]}/{entries[5]}; rejected: " +
                            string.Join(", ", reasons.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
            }

            TestContext.WriteLine(string.Join("\n", summary));
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "recipes037.txt"), string.Join("\n", summary));
            CollectionAssert.IsEmpty(failures);
        }
    }
}
