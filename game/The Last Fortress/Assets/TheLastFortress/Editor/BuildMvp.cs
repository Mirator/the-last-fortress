using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TheLastFortress.Editor
{
    /// <summary>
    /// Windows x64 non-development build of only the MVP scene (Scenes/Mvp.unity). Writes build-report.txt next to the
    /// executable and throws (non-zero exit in batch mode) on failure. Does not modify build settings.
    /// </summary>
    public static class BuildMvp
    {
        public const string OutputDirectory = "Builds/Mvp";
        public const string ExecutablePath = OutputDirectory + "/TheLastFortress.exe";

        [MenuItem("The Last Fortress/Build MVP")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory(OutputDirectory);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MvpSetup.ScenePath },
                locationPathName = ExecutablePath,
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
            sb.AppendLine($"unity={Application.unityVersion}");
            foreach (var step in report.steps)
            foreach (var msg in step.messages)
                if (msg.type == LogType.Error || msg.type == LogType.Exception)
                    sb.AppendLine($"error: [{step.name}] {msg.content}");
            File.WriteAllText(Path.Combine(OutputDirectory, "build-report.txt"), sb.ToString());

            if (summary.result != BuildResult.Succeeded)
                throw new UnityEditor.Build.BuildFailedException($"MVP build {summary.result} with {summary.totalErrors} errors; see {OutputDirectory}/build-report.txt");
            Debug.Log($"MVP build succeeded: {summary.outputPath} ({summary.totalSize} bytes, {summary.totalTime})");
        }
    }
}
