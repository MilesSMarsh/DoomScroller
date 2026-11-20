using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using DoomScroller.Network;

namespace DoomScroller.Tests.EditMode
{
    // Acceptance coverage for docs/tasks/021-cli-ios-dev-build.md. Real builds are not run
    // here (they need the iOS module, a Mac and Xcode); the build script's behaviour is
    // checked via its public constants, the live PlayerSettings, and source-level checks
    // of the build script and shell tools (the build method itself is private/exits Unity).
    public class IosDevBuildAcceptanceTests
    {
        const string BuildScriptPath = "Assets/Scripts/Editor/BuildScript.cs";
        const string BuildIosShPath = "tools/build-ios.sh";
        const string BuildXcodeShPath = "tools/build-ios-xcode.sh";
        const string RunTestsShPath = "tools/run-editmode-tests.sh";

        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string ReadProjectFile(string relPath) =>
            File.ReadAllText(Path.Combine(ProjectRoot, relPath));

        // Build method lives in the Editor asmdef, namespace DoomScroller.Editor.
        [Test]
        public void BuildScript_IsInEditorNamespace_WithIosEntryPoints()
        {
            var type = typeof(DoomScroller.Editor.BuildScript);
            Assert.AreEqual("DoomScroller.Editor", type.Namespace);
            Assert.AreEqual("DoomScroller.Editor", type.Assembly.GetName().Name);
            Assert.IsNotNull(type.GetMethod("BuildIosDev", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static),
                "BuildIosDev must be a public static entry point for -executeMethod");
            Assert.IsNotNull(type.GetMethod("BuildIosRelease", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static),
                "BuildIosRelease must be a public static entry point for -executeMethod");
        }

        [Test]
        public void BuildScript_ExportsToBuildsIos()
        {
            Assert.AreEqual("Builds/iOS", DoomScroller.Editor.BuildScript.IosOutputPath);
        }

        // Scenes come from EditorBuildSettings, not a hardcoded list.
        [Test]
        public void BuildScript_TakesScenesFromEditorBuildSettings_NotHardcoded()
        {
            var src = ReadProjectFile(BuildScriptPath);
            StringAssert.Contains("EditorBuildSettings.scenes", src);
            StringAssert.Contains("s.enabled", src);
            StringAssert.DoesNotContain("SampleScene.unity", src);
        }

        // Development build flag follows the entry point (dev = Development, release = None).
        [Test]
        public void BuildScript_DevelopmentFlagFollowsEntryPoint()
        {
            var src = ReadProjectFile(BuildScriptPath);
            StringAssert.Contains("BuildIosDev() => BuildIos(development: true)", src);
            StringAssert.Contains("BuildIosRelease() => BuildIos(development: false)", src);
            StringAssert.Contains("development ? BuildOptions.Development : BuildOptions.None", src);
            StringAssert.Contains("BuildTarget.iOS", src);
        }

        // Criterion 5: plain http only in development builds; ATS restored afterwards.
        [Test]
        public void BuildScript_AllowsPlainHttpOnlyInDevelopmentBuilds()
        {
            var src = ReadProjectFile(BuildScriptPath);
            StringAssert.Contains(
                "development ? InsecureHttpOption.AlwaysAllowed : InsecureHttpOption.NotAllowed", src);
            StringAssert.Contains("PlayerSettings.insecureHttpOption = oldHttp;", src);
        }

        [Test]
        public void ProjectSettings_InsecureHttpIsNotAllowedAtRest()
        {
            Assert.AreEqual(InsecureHttpOption.NotAllowed, PlayerSettings.insecureHttpOption,
                "Editor's ATS setting must stay NotAllowed; only the dev build flips it temporarily");
        }

        // Criterion 4: iOS player settings buildable from CLI.
        [Test]
        public void IosPlayerSettings_BundleIdIsDoomScrollerApp()
        {
            Assert.AreEqual("com.doomscroller.app",
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS));
            Assert.AreEqual(DoomScroller.Editor.BuildScript.BundleId, "com.doomscroller.app");
        }

        [Test]
        public void IosPlayerSettings_UsesIl2Cpp()
        {
            Assert.AreEqual(ScriptingImplementation.IL2CPP,
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.iOS));
        }

        [Test]
        public void IosPlayerSettings_TargetsArm64()
        {
            // 1 = ARM64 for the iOS target (matches BuildScript.ApplyIosPlayerSettings).
            Assert.AreEqual(1, PlayerSettings.GetArchitecture(NamedBuildTarget.iOS));
        }

        [Test]
        public void IosPlayerSettings_MinimumOsMatchesBuildScript()
        {
            Assert.AreEqual(DoomScroller.Editor.BuildScript.MinIosVersion,
                PlayerSettings.iOS.targetOSVersionString);
            Assert.AreEqual("15.0", DoomScroller.Editor.BuildScript.MinIosVersion);
        }

        // Build script applies these at build time; they must be in the source so a
        // fresh checkout builds the same way.
        [Test]
        public void BuildScript_AppliesIosPlayerSettingsFromSource()
        {
            var src = ReadProjectFile(BuildScriptPath);
            StringAssert.Contains("SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId)", src);
            StringAssert.Contains("SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP)", src);
            StringAssert.Contains("SetArchitecture(NamedBuildTarget.iOS, 1)", src);
            StringAssert.Contains("targetOSVersionString = MinIosVersion", src);
        }

        // Criterion 6 (--server-url): the URL is written into the same field ServerSettings
        // reads, and the default without the flag stays the local dev server.
        [Test]
        public void ServerSettings_BaseUrlFieldExistsForBuildTimeOverride()
        {
            var settings = ScriptableObject.CreateInstance<ServerSettings>();
            try
            {
                var so = new SerializedObject(settings);
                var prop = so.FindProperty("baseUrl");
                Assert.IsNotNull(prop, "BuildScript writes ServerSettings.baseUrl at build time");
                Assert.AreEqual(SerializedPropertyType.String, prop.propertyType);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ServerSettings_DefaultBaseUrlIsLocalDevServer()
        {
            var settings = ScriptableObject.CreateInstance<ServerSettings>();
            try
            {
                Assert.AreEqual("http://localhost:8088", settings.BaseUrl);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void BuildScript_ServerUrlArgIsWrittenToTempSettingsAndRemoved()
        {
            var src = ReadProjectFile(BuildScriptPath);
            StringAssert.Contains("GetArg(\"-serverUrl\")", src);
            StringAssert.Contains("WriteTempServerSettings(serverUrl)", src);
            StringAssert.Contains("DeleteTempServerSettings()", src);
            StringAssert.Contains("if (createdSettings) DeleteTempServerSettings();", src);
        }

        // Criterion 1: build-ios.sh is a batch-mode wrapper with the same lock check as the
        // EditMode runner, exits 2 when locked, and prints the Xcode project path.
        [Test]
        public void BuildIosSh_RunsBatchBuildWithLockCheck()
        {
            var src = ReadProjectFile(BuildIosShPath);
            StringAssert.Contains("-batchmode", src);
            StringAssert.Contains("-executeMethod \"$METHOD\"", src);
            StringAssert.Contains("DoomScroller.Editor.BuildScript.BuildIosDev", src);
            StringAssert.Contains("DoomScroller.Editor.BuildScript.BuildIosRelease", src);
            StringAssert.Contains("Temp/UnityLockfile", src);
            StringAssert.Contains("pgrep -f \"Unity.*-projectpath.*DoomScroller\"", src);
            StringAssert.Contains("exit 2", src);
        }

        [Test]
        public void BuildIosSh_UsesSameUnityPathEnvAsEditModeRunner()
        {
            var build = ReadProjectFile(BuildIosShPath);
            var tests = ReadProjectFile(RunTestsShPath);
            StringAssert.Contains("UNITY_PATH", build);
            StringAssert.Contains("UNITY_PATH", tests);
            StringAssert.Contains("6000.5.10f1/Editor/Unity", tests);
            StringAssert.Contains("6000.5.10f1", build);
        }

        [Test]
        public void BuildIosSh_ExitsZeroOnlyOnSuccessAndPrintsProjectPath()
        {
            var src = ReadProjectFile(BuildIosShPath);
            StringAssert.Contains("grep -q \"BUILD SUCCEEDED\"", src);
            StringAssert.Contains("exit 1", src);
            StringAssert.Contains("Builds/iOS/Unity-iPhone.xcodeproj", src);
            StringAssert.Contains("Errors from $LOG", src);
            StringAssert.Contains("--server-url", src);
        }

        [Test]
        public void BuildIosSh_ReleaseFlagSelectsNonDevelopmentBuild()
        {
            var src = ReadProjectFile(BuildIosShPath);
            StringAssert.Contains("--release) METHOD=\"DoomScroller.Editor.BuildScript.BuildIosRelease\"", src);
        }

        // Criterion 3: Xcode script — team id from env/arg, dev-signed build, archive + export.
        [Test]
        public void BuildXcodeSh_TeamIdFromEnvOrArgument()
        {
            var src = ReadProjectFile(BuildXcodeShPath);
            StringAssert.Contains("${2:-${APPLE_TEAM_ID:-}}", src);
            StringAssert.Contains("DEVELOPMENT_TEAM=\"$TEAM\"", src);
        }

        [Test]
        public void BuildXcodeSh_DevAndTestflightModes()
        {
            var src = ReadProjectFile(BuildXcodeShPath);
            StringAssert.Contains("dev|testflight", src);
            StringAssert.Contains("archive", src);
            StringAssert.Contains("-exportArchive", src);
            StringAssert.Contains("METHOD=development", src);
            StringAssert.Contains("METHOD=app-store-connect", src);
            StringAssert.Contains("-allowProvisioningUpdates", src);
        }

        [Test]
        public void BuildXcodeSh_CommitsNoSecrets()
        {
            var src = ReadProjectFile(BuildXcodeShPath);
            StringAssert.DoesNotContain("BEGIN PRIVATE KEY", src);
            StringAssert.DoesNotContain("BEGIN CERTIFICATE", src);
            Assert.IsFalse(Regex.IsMatch(src, @"(?i)password\s*=\s*\S+"), "No password literal in script");
            Assert.IsFalse(Regex.IsMatch(src, @"DEVELOPMENT_TEAM=""?[A-Z0-9]{10}\b"),
                "No hardcoded Apple team id in script");
        }

        // Criterion 7: Builds/ is git-ignored (pattern in .gitignore).
        [Test]
        public void Gitignore_IgnoresBuildsDirectory()
        {
            var gitignore = ReadProjectFile(".gitignore");
            Assert.IsTrue(Regex.IsMatch(gitignore, @"(?m)^/?\[?Bb\]?uilds/?\s*$"),
                ".gitignore must ignore the Builds/ directory");
        }

        // Criterion 8: CLAUDE.md Build line names the commands.
        [Test]
        public void ClaudeMd_BuildLineNamesIosCommands()
        {
            var src = ReadProjectFile("CLAUDE.md");
            var buildLine = Regex.Match(src, @"(?m)^- Build[^\n]*(\n  [^\n]*)?");
            Assert.IsTrue(buildLine.Success, "CLAUDE.md must have a Build line");
            StringAssert.Contains("tools/build-ios.sh", buildLine.Value);
            StringAssert.Contains("tools/build-ios-xcode.sh", buildLine.Value);
            StringAssert.DoesNotContain("Android", buildLine.Value);
        }

        // Scope guard: no Android build path left in the iOS build script.
        [Test]
        public void BuildScript_HasNoAndroidPath()
        {
            var src = ReadProjectFile(BuildScriptPath);
            StringAssert.DoesNotContain("BuildTarget.Android", src);
            StringAssert.DoesNotContain("NamedBuildTarget.Android", src);
        }
    }
}
