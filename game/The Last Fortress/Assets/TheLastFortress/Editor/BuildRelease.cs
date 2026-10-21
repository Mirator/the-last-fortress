using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TheLastFortress.Editor
{
    /// <summary>
    /// Windows x64 non-development build of only the release scene (Scenes/Release.unity) into Builds/Release. Validates
    /// the release content first (a content error fails the build), writes build-report.txt next to the executable with
    /// the scene list, rules/content version and Unity version, and throws (non-zero exit in batch mode) on failure.
    /// Does not modify build settings. Batch: -executeMethod TheLastFortress.Editor.BuildRelease.BuildWindows
    /// </summary>
    public static class BuildRelease
    {
        public const string OutputDirectory = "Builds/Release";
        public const string ExecutablePath = OutputDirectory + "/TheLastFortress.exe";

        [MenuItem("The Last Fortress/Build Release")]
        public static void BuildWindows()
        {
            ReleaseContentValidation.ValidateAll(); // throws on any content error
            // -releaseOutput <dir> (plan 019 tooling): build elsewhere, e.g. while the normal build is being played.
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, "-releaseOutput");
            var outputDirectory = at >= 0 && at + 1 < args.Length ? args[at + 1] : OutputDirectory;
            Directory.CreateDirectory(outputDirectory);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ReleaseSetup.ScenePath },
                locationPathName = Path.Combine(outputDirectory, "TheLastFortress.exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            var started = DateTime.Now;
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            var sb = new StringBuilder();
            sb.AppendLine($"started={started:O}");
            sb.AppendLine($"result={summary.result}");
            sb.AppendLine($"platform={summary.platform} options={summary.options}");
            sb.AppendLine($"output={summary.outputPath}");
            sb.AppendLine($"total_size_bytes={summary.totalSize}");
            sb.AppendLine($"duration={summary.totalTime}");
            sb.AppendLine($"errors={summary.totalErrors} warnings={summary.totalWarnings}");
            sb.AppendLine($"scenes={string.Join(",", options.scenes)}");
            sb.AppendLine($"rules={ReleaseCampaign.RulesVersion} campaign={ReleaseCampaign.CampaignId} content={ReleaseCampaign.ContentVersion} legacy={MvpCampaign.RulesVersion}");
            sb.AppendLine($"unity={Application.unityVersion} version={Application.version}");
            foreach (var step in report.steps)
            foreach (var msg in step.messages)
                if (msg.type == LogType.Error || msg.type == LogType.Exception)
                    sb.AppendLine($"error: [{step.name}] {msg.content}");
            File.WriteAllText(Path.Combine(outputDirectory, "build-report.txt"), sb.ToString());

            if (summary.result != BuildResult.Succeeded)
                throw new UnityEditor.Build.BuildFailedException($"Release build {summary.result} with {summary.totalErrors} errors; see {outputDirectory}/build-report.txt");
            Debug.Log($"Release build succeeded: {summary.outputPath} ({summary.totalSize} bytes, {summary.totalTime})");
        }
    }
}
