using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GravBoots.Editor
{
    /// <summary>
    /// The web build for itch.io: menu GravBoots → Build for Web (itch.io), or headless with
    /// <c>unity run . -- -executeMethod GravBoots.Editor.WebBuild.Build</c>. Applies the web
    /// player settings, builds the chambers into Builds/WebGL, and zips that folder (index.html
    /// at its root, as itch.io wants) to Builds/MissionApexStrike-web.zip. The Editor goes back
    /// to its previous build target afterwards.
    /// </summary>
    public static class WebBuild
    {
        public const string ProductName = "Mission: Apex Strike";
        public const string OutputFolder = "Builds/WebGL", ZipPath = "Builds/MissionApexStrike-web.zip";
        /// <summary>Where the last build's outcome is written (also logged).</summary>
        public const string ReportPath = "Logs/web-build.txt";
        static readonly string[] Scenes = { "Assets/_Project/Levels/Chamber_04.unity" };

        [MenuItem("GravBoots/Build for Web (itch.io)")]
        public static void Build()
        {
            BuildTarget previous = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup previousGroup = BuildPipeline.GetBuildTargetGroup(previous);
            string outcome;
            try
            {
                ApplySettings();
                if (Directory.Exists(OutputFolder))
                    Directory.Delete(OutputFolder, true);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = Scenes,
                    locationPathName = OutputFolder,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                });
                BuildSummary summary = report.summary;
                outcome = $"{summary.result}: {summary.totalErrors} errors, {summary.totalWarnings} warnings, "
                    + $"{summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0} s";
                if (summary.result == BuildResult.Succeeded)
                {
                    if (File.Exists(ZipPath))
                        File.Delete(ZipPath);
                    ZipFile.CreateFromDirectory(OutputFolder, ZipPath, System.IO.Compression.CompressionLevel.Optimal, false);
                    outcome += $"; zipped to {ZipPath} ({new FileInfo(ZipPath).Length / (1024f * 1024f):0.0} MB)";
                }
            }
            catch (Exception e)
            {
                outcome = "Failed: " + e.Message;
            }
            finally
            {
                if (EditorUserBuildSettings.activeBuildTarget != previous)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(previousGroup, previous);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {outcome}\n");
            Debug.Log("Web build: " + outcome);
        }

        /// <summary>
        /// Player settings for a page on itch.io. It doesn't send the Content-Encoding headers a
        /// compressed build needs, so the loader decompresses Gzip itself (decompression
        /// fallback). The void darkness reads the depth texture, which only the PC quality
        /// level's pipeline asset has, so the web uses PC.
        /// </summary>
        public static void ApplySettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = false;
            SetWebQuality("PC");
            SetCodeOptimization("DiskSize");
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// WebGL code optimization (UnityEditor.WebGL.UserBuildSettings.codeOptimization). Disk
        /// size without link-time optimization: a small download, and the link stays within a
        /// 14 GB machine's memory (an earlier build there was killed for running out). Set by
        /// reflection: the type only exists with the WebGL module installed.
        /// </summary>
        static void SetCodeOptimization(string value)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type settings = assembly.GetType("UnityEditor.WebGL.UserBuildSettings");
                var property = settings?.GetProperty("codeOptimization");
                if (property == null)
                    continue;
                property.SetValue(null, Enum.Parse(property.PropertyType, value));
                Debug.Log($"Web build: code optimization {property.GetValue(null)}");
                return;
            }
            Debug.LogWarning("Web build: couldn't find the WebGL code optimization setting");
        }

        static void SetWebQuality(string levelName)
        {
            int level = Array.IndexOf(QualitySettings.names, levelName);
            if (level < 0)
                return;
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            SerializedProperty perPlatform = quality.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                SerializedProperty entry = perPlatform.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == "WebGL")
                    entry.FindPropertyRelative("second").intValue = level;
            }
            quality.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
