using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TheLastFortress.Editor
{
    /// <summary>
    /// Plan 008 release content validator. Checks the release content asset and its definitions (naming asset and field),
    /// every purchasable building and enemy kind's prefab and HUD icon, the perk pool's titles and effects, every map
    /// profile's skeleton plus a bounded seed corpus, and the twelve stored nights for those seeds (structure, defined
    /// kinds, Day-1 routes on the generated map). Writes TestResults/008/release-validation.txt and throws when anything
    /// fails, so batch mode exits non-zero. No generic mod/plugin framework: this is the release catalog only.
    /// Batch: -executeMethod TheLastFortress.Editor.ReleaseContentValidation.ValidateAll
    /// </summary>
    public static class ReleaseContentValidation
    {
        /// <summary>Seeds checked per map profile (campaign + generated terrain).</summary>
        public const int SeedCorpus = 50;

        [MenuItem("The Last Fortress/Validate Release Content")]
        public static void ValidateAll()
        {
            var errors = new List<string>();
            var notes = new List<string>();
            Validate(errors, notes);
            var sb = new StringBuilder();
            sb.AppendLine($"# Release content validation {DateTime.Now:O}");
            sb.AppendLine($"rules={ReleaseCampaign.RulesVersion} campaign={ReleaseCampaign.CampaignId} content={ReleaseCampaign.ContentVersion}");
            foreach (var n in notes) sb.AppendLine(n);
            sb.AppendLine($"errors={errors.Count}");
            foreach (var e in errors) sb.AppendLine("error: " + e);
            var dir = Path.Combine("TestResults", "008");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "release-validation.txt"), sb.ToString());
            if (errors.Count > 0)
                throw new InvalidOperationException($"Release content invalid ({errors.Count} errors): {string.Join(" | ", errors.GetRange(0, Math.Min(errors.Count, 8)))}");
            Debug.Log($"Release content valid: {string.Join("; ", notes)}");
        }

        static void Validate(List<string> errors, List<string> notes)
        {
            var content = AssetDatabase.LoadAssetAtPath<PrototypeContent>(ReleaseSetup.ContentPath);
            if (content == null)
            {
                errors.Add($"{ReleaseSetup.ContentPath}: missing; run The Last Fortress > Setup Release Scene.");
                return;
            }

            if (content.releaseDefinitions == null)
            {
                errors.Add($"{content.name}.releaseDefinitions: not assigned.");
                return;
            }

            if (content.mvpDefinitions == null) errors.Add($"{content.name}.mvpDefinitions: not assigned (legacy six-night saves could not continue).");
            if (!File.Exists(ReleaseSetup.ScenePath)) errors.Add($"{ReleaseSetup.ScenePath}: missing.");

            errors.AddRange(content.releaseDefinitions.Validate());
            var defs = content.releaseDefinitions.ToDefinitions();

            foreach (var kind in RulesProfile.ReleaseKinds)
            {
                if (Prefab(() => content.PrefabFor(kind)) == null) errors.Add($"{content.name}: no prefab for building {kind}.");
                if (content.IconFor(kind) == null) errors.Add($"{content.name}.buildingIcons[{(int)kind}]: no HUD icon for {kind}.");
            }

            if (Prefab(() => content.PrefabFor(BuildingKind.Keep)) == null) errors.Add($"{content.name}.keepPrefab: missing.");
            for (var k = 0; k < EnemyDefinition.KindCount; k++)
                if (Prefab(() => content.EnemyPrefabFor((EnemyKind)k)) == null) errors.Add($"{content.name}: no prefab for enemy {(EnemyKind)k}.");
            if (content.wallShapePrefabs == null || content.wallShapePrefabs.Length != 6) errors.Add($"{content.name}.wallShapePrefabs: expected 6 connected Wall shapes.");
            if (Prefab(() => content.heroPrefab) == null) errors.Add($"{content.name}.heroPrefab: missing (run The Last Fortress > Art > Import and Assign Hero).");
            else if (content.heroPrefab.GetComponent<CharacterRig>() == null) errors.Add($"{content.name}.heroPrefab: no CharacterRig.");

            var seen = new HashSet<string>();
            foreach (var id in PerkRules.ReleaseIds)
            {
                if (!seen.Add(id)) errors.Add($"PerkRules.ReleaseIds: duplicate '{id}'.");
                if (PerkRules.Effect(id) == id) errors.Add($"PerkRules: '{id}' has no effect text for its card.");
                var affected = PerkRules.AffectedKind(id);
                if (affected.HasValue && defs.For(affected.Value) == null) errors.Add($"PerkRules: '{id}' affects {affected}, which the release does not define.");
            }

            if (PerkRules.ReleaseIds.Length != 24) errors.Add($"PerkRules.ReleaseIds: {PerkRules.ReleaseIds.Length} perks, the 008 contract freezes 24.");

            var fallbacks = 0;
            foreach (var id in MapProfiles.All)
            {
                var profile = MapProfiles.Get(id);
                var skeleton = MapProfiles.Validate(profile, MapProfiles.Skeleton(profile, 0));
                if (skeleton != null) errors.Add($"MapProfiles.{id}: skeleton invalid ({skeleton}); the fallback would be unplayable.");
                for (uint seed = 0; seed < SeedCorpus; seed++)
                {
                    var map = MapProfiles.Generate(id, seed);
                    if (map.Fallback) fallbacks++;
                    else
                    {
                        var why = MapProfiles.Validate(profile, map);
                        if (why != null) errors.Add($"MapProfiles.{id} seed {seed}: {why}.");
                    }

                    var campaign = ReleaseCampaign.Create(seed, id);
                    foreach (var e in ReleaseCampaign.Validate(campaign, defs)) errors.Add($"ReleaseCampaign seed {seed}: {e}");
                    var sim = new RunSimulation(new RunConfig { Definitions = defs, Seed = seed, Terrain = map, Campaign = campaign });
                    for (var n = 0; n < campaign.TotalNights; n++)
                        foreach (var entry in campaign.Nights[n].EntryCells)
                            if (!sim.GetLiveRoute(entry).Found)
                                errors.Add($"{id} seed {seed} night {n + 1}: entry {entry} has no route on Day 1.");
                }
            }

            notes.Add($"map profiles {MapProfiles.All.Length} x seeds 0..{SeedCorpus - 1}: {fallbacks} fallbacks");
            notes.Add($"buildings {RulesProfile.ReleaseKinds.Length + 1} (incl. Keep), enemies {EnemyDefinition.KindCount}, perks {PerkRules.ReleaseIds.Length}, nights {ReleaseCampaign.Table().Length}");
        }

        static GameObject Prefab(Func<GameObject> get)
        {
            try
            {
                return get();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
