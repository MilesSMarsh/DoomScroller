using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // Acceptance-criteria gaps for task 013 not pinned by ProfileSyncTests /
    // ProfileSyncQaTests / ProfileSyncGapTests: the local savegame.json is never read
    // or uploaded, and the server's current max HP is applied on the save path too.
    public class ProfileSyncAcceptanceTests
    {
        private static string ReadScript(string relative) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", relative));

        [Test]
        public void LocalSaveFile_IsNotReadOrUploaded_ByProfileSyncOrController()
        {
            foreach (var relative in new[] { "Inventory/ProfileController.cs", "Inventory/ProfileSync.cs" })
            {
                var src = ReadScript(relative);
                Assert.IsFalse(src.Contains("savegame.json"), relative + " must not reference the old local save file");
                Assert.IsFalse(src.Contains("persistentDataPath"), relative + " must not touch local disk storage");
                Assert.IsFalse(Regex.IsMatch(src, @"\bSaveSystem\b"), relative + " must not depend on SaveSystem");
            }
        }

        [Test]
        public void SaveSystem_IsMarkedUnused_InItsDoc()
        {
            var src = ReadScript("Progression/SaveSystem.cs");
            Assert.IsTrue(src.Contains("UNUSED"), "SaveSystem must document that the game no longer uses it");
            Assert.IsTrue(src.Contains("savegame.json"), "the ignored-file behaviour must be documented on SaveSystem");
        }

        [Test]
        public void ServerProfileApplied_AppliesServerCurrentMax_OnSuccessfulSave_NotOnlyOnReload()
        {
            // OnServerProfileApplied runs for both the normal save ack (isReload false) and the
            // 409 reload (isReload true); current max must be applied in both, unconditionally.
            var src = ReadScript("Inventory/ProfileController.cs");
            var start = src.IndexOf("private void OnServerProfileApplied", System.StringComparison.Ordinal);
            var end = src.IndexOf("private string CaptureJson", start, System.StringComparison.Ordinal);
            Assert.IsTrue(start >= 0 && end > start, "OnServerProfileApplied not found");
            var body = src.Substring(start, end - start);

            var applyIdx = body.IndexOf("_health.ApplyServerState(save.healthCurrentMax", System.StringComparison.Ordinal);
            var reloadIdx = body.IndexOf("if (isReload)", System.StringComparison.Ordinal);
            Assert.IsTrue(applyIdx > 0, "server healthCurrentMax must be applied");
            Assert.IsTrue(reloadIdx >= 0 && applyIdx > reloadIdx, "the reload-only block must not gate the HP apply");
            Assert.IsFalse(Regex.IsMatch(body.Substring(applyIdx - 200, 200), @"if\s*\(isReload\)\s*\{[^}]*ApplyServerState"),
                "ApplyServerState must not sit inside the isReload branch");
        }
    }
}
