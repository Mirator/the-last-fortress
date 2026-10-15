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
