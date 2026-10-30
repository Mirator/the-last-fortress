using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheLastFortress.Tests
{
    /// <summary>Serialized definitions and layout fixtures must match the specification and reproduce outcomes.</summary>
    public class SerializedDataTests
    {
        const string Data = "Assets/TheLastFortress/Data/";

        static LayoutFixture Load(string file, out string expected)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Data + "Layouts/" + file);
            Assert.IsNotNull(asset, file);
            return LayoutJson.Parse(asset.text, out expected);
        }

        static RunState Play(LayoutFixture layout, out int goldLeft, out int keepHp)
        {
            var sim = new RunSimulation(RunConfig.CreateMain());
            CollectionAssert.IsEmpty(sim.ApplyLayout(layout, bypassCost: false), "layout must be affordable and legal");
            goldLeft = sim.Gold;
            Assert.IsTrue(sim.StartNight());
            for (var i = 0; i < 20000 && sim.State == RunState.Night; i++) sim.Step();
            keepHp = sim.Keep.Hp;
            return sim.State;
        }

        [Test]
        public void DefinitionsAssetMatchesSpecificationDefaults()
        {
            var asset = AssetDatabase.LoadAssetAtPath<PrototypeDefinitionsAsset>(Data + "PrototypeDefinitions.asset");
            Assert.IsNotNull(asset);
            var a = asset.ToDefinitions();
            var d = PrototypeDefinitions.CreateDefault();
            CollectionAssert.IsEmpty(a.Validate());
            Assert.AreEqual(d.StartingGold, a.StartingGold);
            foreach (var kind in new[] { BuildingKind.Keep, BuildingKind.Wall, BuildingKind.Tower })
            {
                var x = a.For(kind);
                var y = d.For(kind);
                Assert.AreEqual(y.Cost, x.Cost, kind + " cost");
                Assert.AreEqual(y.MaxHp, x.MaxHp, kind + " hp");
                Assert.AreEqual(y.AttackDamage, x.AttackDamage, kind + " damage");
                Assert.AreEqual(y.AttackIntervalSeconds, x.AttackIntervalSeconds, 1e-9, kind + " interval");
                Assert.AreEqual(y.Range, x.Range, 1e-9, kind + " range");
            }

            Assert.AreEqual(d.Grunt.MaxHp, a.Grunt.MaxHp);
            Assert.AreEqual(d.Grunt.AttackDamage, a.Grunt.AttackDamage);
            Assert.AreEqual(d.Grunt.AttackIntervalSeconds, a.Grunt.AttackIntervalSeconds, 1e-9);
            Assert.AreEqual(d.Grunt.SpeedTilesPerSecond, a.Grunt.SpeedTilesPerSecond, 1e-9);
        }

        [Test]
        public void WinningLayoutWinsOnNormalBudget()
        {
            var layout = Load("WinningLayout.json", out var expected);
            Assert.AreEqual("Victory", expected);
            Assert.LessOrEqual(layout.Placements.Sum(p => PrototypeDefinitions.CreateDefault().For(p.Kind).Cost), 250);
            Assert.AreEqual(RunState.Victory, Play(layout, out var gold, out var keep));
            TestContext.WriteLine($"Winning layout: {string.Join(", ", layout.Placements)}; gold left {gold}; Keep {keep}");
        }

        [Test]
        public void LosingLayoutLoses()
        {
            var layout = Load("LosingLayout.json", out var expected);
            Assert.AreEqual("Defeat", expected);
            Assert.AreEqual(0, layout.Placements.Count, "Keep-only");
            Assert.AreEqual(RunState.Defeat, Play(layout, out _, out var keep));
            Assert.AreEqual(0, keep);
        }

        [Test]
        public void StressLayoutJsonEqualsSpecificationLayout()
        {
            var json = Load("StressLayout.json", out _);
            var spec = LayoutFixture.CreateStressLayout();
            CollectionAssert.AreEquivalent(spec.Placements.Select(p => p.ToString()), json.Placements.Select(p => p.ToString()));
        }

        /// <summary>
        /// Plan 011: the shipped release content points at authored art, and rerunning the blockout setups keeps it — the
        /// generator is never even invoked for a reference outside its own Prefabs/&lt;name&gt;.prefab.
        /// </summary>
        [Test]
        public void AuthoredArtSurvivesBlockoutRegeneration()
        {
            var c = AssetDatabase.LoadAssetAtPath<PrototypeContent>(Data + "ReleaseContent.asset");
            var assigned = new (Object prefab, string generated)[]
            {
                (c.keepPrefab, "Keep"), (c.towerPrefab, "ArcherTower"), (c.gruntPrefab, "Grunt"), (c.wallPrefab, "Wall"), (c.housePrefab, "House"),
                (c.runnerPrefab, "Runner"), (c.cannonPrefab, "ReleaseCannon"), (c.bomberPrefab, "ReleaseBomber"),
            }.Concat(c.wallShapePrefabs.Select((p, s) => ((Object)p, "MvpWall" + (WallShape)s)));
            foreach (var (prefab, generated) in assigned)
            {
                StringAssert.StartsWith("Assets/TheLastFortress/Art/", AssetDatabase.GetAssetPath(prefab), generated);
                var kept = TheLastFortress.Editor.PrototypeSetup.Generated((GameObject)prefab, () => throw new AssertionException($"{generated}: blockout regenerated over authored art"), generated);
                Assert.AreSame(prefab, kept, generated);
            }

            Assert.IsNotNull(c.art, "release content keeps its art direction");
        }

        /// <summary>
        /// Plan 013 terrain (mainland since plan 016): buildings, enemies and the hero stand at y = 0, so every walkable,
        /// buildable board cell must stay exactly flat while water cells sink below the river surface (blocked terrain looks
        /// blocked), and each entry's road leaves the board at board level. Fails if the surround or river carving ever bends the
        /// played board or leaves an entry without its road.
        /// </summary>
        [Test]
        public void TerrainKeepsTheBoardFlatAndSinksOnlyWater()
        {
            foreach (var profile in MapProfiles.All.Concat(MapRecipes.All)) // spec 017: the recipes' own entries and rivers too
            for (var seed = 1u; seed <= 3; seed++)
            {
                var map = MapRecipes.IsRecipe(profile) ? MapRecipes.Generate(profile, seed) : MapProfiles.Generate(profile, seed);
                var shape = new WorldView.TerrainShape(map);
                var crossings = new System.Collections.Generic.HashSet<GridPos>(MapProfiles.RiverCrossings(map));
                for (var z = 0; z < GridCoordinates.Size; z++)
                for (var x = 0; x < GridCoordinates.Size; x++)
                {
                    var cell = new GridPos(x, z);
                    if (map[cell] == TerrainType.Water)
                        Assert.Less(shape.Height(x + 0.5f, z + 0.5f), -0.06f, $"{profile}/{seed} water {cell} sits below the river surface");
                    else if (!crossings.Contains(cell))
                        for (var k = 0; k <= 4; k++)
                        for (var j = 0; j <= 4; j++)
                            Assert.AreEqual(0f, shape.Height(x + k * 0.25f, z + j * 0.25f), $"{profile}/{seed} land {cell} stays flat");
                }

                foreach (var entry in map.EntriesOrSides())
                {
                    var mouth = RouteOverlay.RoadMouth(entry);
                    Assert.AreEqual(0f, shape.Height(mouth.x, mouth.z), 0.03f, $"{profile}/{seed} road mouth at {entry} is at board level");
                    Assert.Less(shape.RoadDistance(mouth.x, mouth.z), WorldView.TerrainShape.RoadHalfWidth, $"{profile}/{seed} entry {entry} has its road");
                }
            }
        }

        [Test]
        public void LayoutJsonRoundTrips()
        {
            var spec = LayoutFixture.CreateStressLayout();
            var back = LayoutJson.Parse(LayoutJson.ToJson(spec, "x"), out var expected);
            Assert.AreEqual("x", expected);
            CollectionAssert.AreEqual(spec.Placements.Select(p => p.ToString()), back.Placements.Select(p => p.ToString()));
        }
    }
}
