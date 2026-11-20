using System;
using System.IO;
using System.Linq;
using DoomScroller.Network;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DoomScroller.Editor
{
    // Command-line iOS build entry points. Invoked by tools/build-ios.sh via -executeMethod.
    // Exports the Xcode project to Builds/iOS/; tools/build-ios-xcode.sh (on a Mac) builds it.
    public static class BuildScript
    {
        public const string IosOutputPath = "Builds/iOS";
        public const string BundleId = "com.doomscroller.app";
        public const string MinIosVersion = "15.0";

        const string TempSettingsPath = "Assets/Resources/ServerSettings.asset";

        public static void BuildIosDev() => BuildIos(development: true);

        public static void BuildIosRelease() => BuildIos(development: false);

        static void BuildIos(bool development)
        {
            int code;
            try { code = Run(development); }
            catch (Exception e) { Debug.LogError("BUILD FAILED: " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        static int Run(bool development)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                return Fail("No enabled scenes in EditorBuildSettings.");

            ApplyIosPlayerSettings();

            var serverUrl = GetArg("-serverUrl");
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var output = Path.Combine(projectRoot, IosOutputPath);
            Directory.CreateDirectory(output);

            // ATS: plain http only in development builds. Restored afterwards so the asset stays clean.
            var oldHttp = PlayerSettings.insecureHttpOption;
            PlayerSettings.insecureHttpOption = development ? InsecureHttpOption.AlwaysAllowed : InsecureHttpOption.NotAllowed;

            var createdSettings = false;
            try
            {
                if (!string.IsNullOrEmpty(serverUrl))
                    createdSettings = WriteTempServerSettings(serverUrl);

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.iOS,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                });

                if (report.summary.result != BuildResult.Succeeded)
                    return Fail($"Build {report.summary.result}: {report.summary.totalErrors} error(s).");
                Debug.Log($"BUILD SUCCEEDED: {output} ({(development ? "development" : "release")}, server={(string.IsNullOrEmpty(serverUrl) ? "default" : serverUrl)})");
                return 0;
            }
            finally
            {
                PlayerSettings.insecureHttpOption = oldHttp;
                if (createdSettings) DeleteTempServerSettings();
            }
        }

        // Values already committed in ProjectSettings; only written when they differ.
        static void ApplyIosPlayerSettings()
        {
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS) != BundleId)
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.iOS) != ScriptingImplementation.IL2CPP)
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            if (PlayerSettings.GetArchitecture(NamedBuildTarget.iOS) != 1) // 1 = ARM64
                PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1);
            if (PlayerSettings.iOS.targetOSVersionString != MinIosVersion)
                PlayerSettings.iOS.targetOSVersionString = MinIosVersion;
            var team = Environment.GetEnvironmentVariable("APPLE_TEAM_ID");
            if (!string.IsNullOrEmpty(team)) PlayerSettings.iOS.appleDeveloperTeamID = team;
        }

        // Writes Assets/Resources/ServerSettings.asset for the build only (deleted afterwards).
        // Returns false (and leaves it alone) if an authored asset already exists.
        static bool WriteTempServerSettings(string url)
        {
            if (File.Exists(TempSettingsPath))
                throw new InvalidOperationException(TempSettingsPath + " already exists; remove it or drop --server-url.");
            var settings = ScriptableObject.CreateInstance<ServerSettings>();
            var so = new SerializedObject(settings);
            so.FindProperty("baseUrl").stringValue = url;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(settings, TempSettingsPath);
            AssetDatabase.SaveAssets();
            return true;
        }

        static void DeleteTempServerSettings()
        {
            AssetDatabase.DeleteAsset(TempSettingsPath);
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        static int Fail(string message)
        {
            Debug.LogError("BUILD FAILED: " + message);
            return 1;
        }
    }
}
