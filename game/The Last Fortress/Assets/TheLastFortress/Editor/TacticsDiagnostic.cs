using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace TheLastFortress.Editor
{
    /// <summary>
    /// Plan 008 G0 diagnostic (not a test): the six-night MVP on the flat map with one reward policy and the same
    /// purchase list per Day, placed as a Tower ring, as a corridor of fire along the approaches, and as that corridor
    /// plus Spike Traps (the one added variable). Records per night the live routes, nominal damage by source, losses and
    /// gold. Every night is bounded by steps and throws with the seed/layout on timeout. Writes
    /// TestResults/008/tactics-diagnostic.txt. Batch: -executeMethod TheLastFortress.Editor.TacticsDiagnostic.Run
    /// </summary>
    public static class TacticsDiagnostic
    {
        const int MaxStepsPerNight = 40000;

        sealed class Layout
        {
            public string Name;
            public string[][] Days;
        }

        // Identical kinds and order per Day in Ring and Corridor; positions differ. TrapCorridor = Corridor + traps.
        static readonly Layout Ring = new Layout
        {
            Name = "tower-ring",
            Days = new[]
            {
                new[] { "Tower 9 9", "Tower 11 11", "Tower 9 11" },
                new[] { "Tower 11 9" },
                new[] { "Wall 9 10", "Wall 11 10", "Wall 10 9", "Wall 10 11" },
                new[] { "Tower 8 10", "Tower 12 10" },
                new[] { "Tower 10 8", "Tower 10 12" },
                new[] { "Wall 7 10", "Wall 13 10", "Wall 10 7", "Wall 10 13" },
            },
        };

        static readonly Layout Corridor = new Layout
        {
            Name = "corridor",
            Days = new[]
            {
                new[] { "Tower 6 9", "Tower 13 11", "Tower 8 11" },
                new[] { "Tower 11 9" },
                new[] { "Wall 4 10", "Wall 4 11", "Wall 15 10", "Wall 15 9" },
                new[] { "Tower 4 9", "Tower 15 11" },
                new[] { "Tower 9 14", "Tower 11 6" },
                new[] { "Wall 4 12", "Wall 15 8", "Wall 10 15", "Wall 10 5" },
            },
        };

        static readonly Layout TrapCorridor = new Layout
        {
            Name = "trap-corridor",
            Days = new[]
            {
                Corridor.Days[0],
                Append(Corridor.Days[1], "SpikeTrap 5 10"),
                Append(Corridor.Days[2], "SpikeTrap 14 11"),
                Append(Corridor.Days[3], "SpikeTrap 7 10", "SpikeTrap 12 10"),
                Append(Corridor.Days[4], "SpikeTrap 10 14", "SpikeTrap 10 6"),
                Append(Corridor.Days[5], "SpikeTrap 3 9", "SpikeTrap 16 10"),
            },
        };

        static string[] Append(string[] a, params string[] b)
        {
            var list = new List<string>(a);
            list.AddRange(b);
            return list.ToArray();
        }

        static readonly string[] PerkPreference =
        {
            PerkRules.Sharpshooters, PerkRules.FlamingArrows, PerkRules.ExpandedVision, PerkRules.FortressHeart, PerkRules.ReinforcedTimber,
            PerkRules.BarbedDefenses, PerkRules.EfficientFarming, PerkRules.LastStand, PerkRules.ThriftyConstruction, PerkRules.Recycling,
        };

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Plan 008 G0 tactics diagnostic — six-night MVP rules, flat map, same purchases and reward policy");
            sb.AppendLine("Damage is nominal per hit (shots x current damage, trap entries, burn ticks, reflections), overkill included.");
            sb.AppendLine();
            var summary = new List<string>();
            foreach (var seed in new uint[] { 1, 2, 3 })
            foreach (var layout in new[] { Ring, Corridor, TrapCorridor })
                summary.Add(Play(layout, seed, sb));
            sb.Insert(0, "## Summary\n" + string.Join("\n", summary) + "\n\n");
            var dir = Path.Combine("TestResults", "008");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "tactics-diagnostic.txt"), sb.ToString());
            Debug.Log("Tactics diagnostic written: " + Path.Combine(dir, "tactics-diagnostic.txt"));
        }

        static string Play(Layout layout, uint seed, StringBuilder log)
        {
            var sim = new RunSimulation(new RunConfig { Definitions = MvpDefinitions.CreateDefault(), Campaign = MvpCampaign.CreateSixNights(), Seed = seed });
            log.AppendLine($"## {layout.Name} seed={seed}");
            var events = new List<SimEvent>();
            int totalKeep = 0, totalLost = 0, totalSpent = 0;
            for (var day = 1; day <= 6 && !sim.IsTerminal; day++)
            {
                var actions = new List<string>();
                foreach (var a in layout.Days[day - 1])
                {
                    var p = a.Split(' ');
                    var kind = (BuildingKind)Enum.Parse(typeof(BuildingKind), p[0]);
                    var r = sim.TryPlace(kind, new GridPos(int.Parse(p[1]), int.Parse(p[2])), out var b);
                    if (r == PlacementRejection.None) totalSpent += b.PurchasePrice;
                    actions.Add(r == PlacementRejection.None ? a : $"{a} SKIPPED {r}");
                }

                var keepBefore = sim.Keep.Hp;
                if (sim.TryRepair(sim.Keep) == RepairRejection.None) actions.Add($"repair Keep +{sim.Keep.Hp - keepBefore}");
                var routes = new List<string>();
                foreach (var entry in sim.Wave.EntryCells)
                {
                    var route = sim.GetLiveRoute(entry);
                    routes.Add($"{WaveText.EntryName(entry)} {(route.Found ? route.Cells.Count + (route.IsBreach ? " breach" : " open") : "none")}");
                }

                log.AppendLine($"Day {day}: gold {sim.Gold}; {string.Join("; ", actions)}");
                log.AppendLine($"  routes (cells): {string.Join(", ", routes)}");
                sim.DrainEvents(events);
                events.Clear();
                if (!sim.StartNight()) throw new InvalidOperationException($"{layout.Name} seed {seed}: night {day} could not start: {string.Join("; ", sim.Diagnostics)}");
                int tower = 0, trap = 0, burn = 0, reflect = 0, shots = 0;
                var steps = 0;
                while (sim.State == RunState.Night)
                {
                    if (++steps > MaxStepsPerNight) throw new TimeoutException($"{layout.Name} seed {seed} night {day} exceeded {MaxStepsPerNight} steps");
                    sim.Step();
                    sim.DrainEvents(events);
                    foreach (var e in events)
                    {
                        switch (e.Type)
                        {
                            case SimEventType.TowerShot:
                                shots++;
                                var t = sim.GetBuilding(e.SourceId);
                                tower += t != null ? sim.TowerDamage(t) : sim.Definitions.Tower.AttackDamage;
                                break;
                            case SimEventType.TrapTriggered:
                                trap += sim.Definitions.SpikeTrap.TrapDamage;
                                break;
                            case SimEventType.BurnTick:
                                burn += PerkRules.BurnDamage;
                                break;
                            case SimEventType.DamageReflected:
                                var w = sim.GetBuilding(e.SourceId);
                                reflect += w != null ? sim.WallReflection(w) : EconomyRules.WallTrapReflect;
                                break;
                        }
                    }

                    events.Clear();
                }

                var recap = sim.LastNight;
                totalKeep += recap.KeepDamage;
                totalLost += recap.BuildingsLost;
                log.AppendLine($"  Night {day}: {sim.State} in {sim.NightElapsedSeconds:0.0}s; Keep -{recap.KeepDamage} ({sim.Keep.Hp}/{sim.Keep.MaxHp}); lost {recap.BuildingsLost}; " +
                               $"damage tower {tower} ({shots} shots) trap {trap} burn {burn} reflect {reflect}");
                if (sim.State != RunState.Reward) continue;
                var offer = sim.CurrentOffer;
                var pick = Array.Find(PerkPreference, id => Array.IndexOf(offer.PerkIds, id) >= 0);
                sim.ChoosePerk(offer.OfferId, pick);
                log.AppendLine($"  Reward {string.Join(",", offer.PerkIds)} -> {pick}; income +{sim.LastIncome.Total}");
            }

            var line = $"{layout.Name,-14} seed {seed}: {sim.State,-7} nights {sim.Stats.NightsCleared}/6, Keep damage {totalKeep}, buildings lost {totalLost}, spent {totalSpent}, final gold {sim.Gold}";
            log.AppendLine("Result: " + line);
            log.AppendLine();
            return line;
        }
    }
}
