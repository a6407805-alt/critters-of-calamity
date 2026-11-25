using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CrittersOfCalamity.EditorTools
{
    /// <summary>
    /// Builds the Windows release that the launcher downloads.
    ///   Menu: Critters > Build Windows Release
    ///   Or drop a file at Builds/build-request.txt containing the version (for example 0.2.0); the open Editor
    ///   picks it up within a few seconds, builds, and writes Builds/build-result.txt.
    /// Output: Builds/win64/ (the game, with version.txt) and Builds/CrittersOfCalamity-win64.zip (the release asset).
    /// </summary>
    [InitializeOnLoad]
    public static class ReleaseBuilder
    {
        public const string AssetName = "CrittersOfCalamity-win64.zip";
        const string BuildsDir = "Builds";
        const string RequestFile = "Builds/build-request.txt";
        const string ResultFile = "Builds/build-result.txt";
        static double _nextPoll;

        static ReleaseBuilder()
        {
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 3.0;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(RequestFile)) return;

            string version = File.ReadAllText(RequestFile).Trim();
            File.Delete(RequestFile);
            if (string.IsNullOrEmpty(version)) version = PlayerSettings.bundleVersion;
            Build(version);
        }

        [MenuItem("Critters/Build Windows Release")]
        static void BuildFromMenu() => Build(PlayerSettings.bundleVersion);

        public static void Build(string version)
        {
            Directory.CreateDirectory(BuildsDir);
            string outDir = Path.Combine(BuildsDir, "win64");
            string zipPath = Path.Combine(BuildsDir, AssetName);
            try
            {
                if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
                PlayerSettings.bundleVersion = version;

                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Path.Combine(outDir, "Critters of Calamity.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    WriteResult($"FAILED {version}: {report.summary.result}, {report.summary.totalErrors} error(s).");
                    return;
                }

                File.WriteAllText(Path.Combine(outDir, "version.txt"), version);
                // Strip the debug folder Unity adds; players don't need it
                foreach (var dir in Directory.GetDirectories(outDir, "*DoNotShip*")) Directory.Delete(dir, true);
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(outDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);
                long bytes = new FileInfo(zipPath).Length;
                WriteResult($"OK {version}: {zipPath} ({bytes / (1024 * 1024)} MB)");
                Debug.Log($"[ReleaseBuilder] Built version {version}: {zipPath}");
            }
            catch (Exception ex)
            {
                WriteResult($"FAILED {version}: {ex.Message}");
                Debug.LogError(ex);
            }
        }

        static void WriteResult(string line)
        {
            File.WriteAllText(ResultFile, line + Environment.NewLine + DateTime.Now.ToString("u"));
            Debug.Log("[ReleaseBuilder] " + line);
        }
    }
}
