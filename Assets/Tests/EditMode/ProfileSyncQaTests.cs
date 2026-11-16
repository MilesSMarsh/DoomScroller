using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DoomScroller.Inventory;
using DoomScroller.Network;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoomScroller.Tests.EditMode
{
    // QA coverage for task 013 (client loads and saves the profile through the server).
    // Gaps not already covered by ProfileSyncTests.cs: IsLoaded gating, exception paths,
    // revision chaining, Flush-during-save, halted state, server-owned HP clamping,
    // regen after a device-clock change post-sync, and the SaveSystem/pause-flush guards.
    public class ProfileSyncQaTests
    {
        private class QaClient : IServerClient
        {
            public bool IsSignedIn { get; set; } = true;
            public string PlayerId => "qa";
            public Func<Task<bool>> SignIn = () => Task.FromResult(true);
            public Func<ProfileResponse> Load = () => new ProfileResponse(200, true, null, 0, null);
            public Func<string, int, ProfileResponse> Save = (json, rev) => new ProfileResponse(200, false, json, rev + 1, null);
            public TaskCompletionSource<ProfileResponse> PendingSave;
            public readonly List<int> SaveRevisions = new List<int>();
            public readonly List<string> SaveJsons = new List<string>();
            public int Loads, Saves;

            public Task<bool> SignInAsync()
            {
                var t = SignIn();
                IsSignedIn = t.IsCompleted && t.Status == TaskStatus.RanToCompletion && t.Result;
                return t;
            }

            public Task<ServerResponse> SendAsync(string method, string path, string jsonBody = null) =>
                Task.FromResult(new ServerResponse(200, "{}"));

            public Task<ProfileResponse> LoadProfileAsync()
            {
                Loads++;
                return Task.FromResult(Load());
            }

            public Task<ProfileResponse> SaveProfileAsync(string saveJson, int baseRevision)
            {
                Saves++;
                SaveRevisions.Add(baseRevision);
                SaveJsons.Add(saveJson);
                if (PendingSave != null)
                {
                    return PendingSave.Task;
                }

                return Task.FromResult(Save(saveJson, baseRevision));
            }
        }

        private double _now;
        private int _lost;

        [SetUp]
        public void SetUp()
        {
            _now = 0;
            _lost = 0;
        }

        private ProfileSync Make(QaClient c, double debounce = 1.0)
        {
            var s = new ProfileSync(c, () => "{\"gold\":1}", () => _now, debounce);
            s.ConnectionLost += () => _lost++;
            return s;
        }

        // ---- Startup gate: gameplay input waits for IsLoaded ----

        [Test]
        public void IsLoaded_FalseBeforeLoad_TrueAfterSuccessfulLoad()
        {
            var s = Make(new QaClient());
            Assert.IsFalse(s.IsLoaded);
            s.LoadAsync().Wait();
            Assert.IsTrue(s.IsLoaded);
        }

        [Test]
        public void IsLoaded_StaysFalseAfterFailedLoad()
        {
            var c = new QaClient { Load = () => new ProfileResponse(503, false, null, 0, null) };
            var s = Make(c);
            s.LoadAsync().Wait();
            Assert.IsFalse(s.IsLoaded);
            Assert.AreEqual(1, _lost);
        }

        [Test]
        public void SaveBeforeLoad_IsIgnored_NoRequestSent()
        {
            var c = new QaClient();
            var s = Make(c);
            s.RequestSave();
            s.Pump();
            s.Flush();
            Assert.AreEqual(0, c.Saves);
            Assert.AreEqual(0, _lost);
        }

        // ---- Failure paths: ConnectionLost, never a fresh profile over the server's ----

        [Test]
        public void LoadOk_ButNotNew_AndEmptyJson_IsFailure_NotExisting()
        {
            var c = new QaClient { Load = () => new ProfileResponse(200, false, "", 3, null) };
            var s = Make(c);
            Assert.AreEqual(ProfileLoadOutcome.Failed, s.LoadAsync().Result);
            Assert.AreEqual(1, _lost);
            Assert.IsFalse(s.IsLoaded);
        }

        [Test]
        public void LoadOk_ButNotNew_NullJson_IsFailure()
        {
            var c = new QaClient { Load = () => new ProfileResponse(200, false, null, 3, null) };
            var s = Make(c);
            Assert.AreEqual(ProfileLoadOutcome.Failed, s.LoadAsync().Result);
            Assert.AreEqual(1, _lost);
        }

        [Test]
        public void SignInThrows_IsFailure_AndRaisesConnectionLost()
        {
            var c = new QaClient
            {
                IsSignedIn = false,
                SignIn = () => Task.FromException<bool>(new Exception("boom")),
            };
            var s = Make(c);
            Assert.AreEqual(ProfileLoadOutcome.Failed, s.LoadAsync().Result);
            Assert.AreEqual(1, _lost);
            Assert.AreEqual(0, c.Loads);
        }

        [Test]
        public void LoadThrows_IsFailure_AndNeverReportsNew()
        {
            var c = new QaClient();
            c.Load = () => throw new Exception("network");
            var s = Make(c);
            var outcome = s.LoadAsync().Result;
            Assert.AreNotEqual(ProfileLoadOutcome.New, outcome);
            Assert.AreEqual(ProfileLoadOutcome.Failed, outcome);
            Assert.AreEqual(1, _lost);
        }

        [Test]
        public void SaveThrows_RaisesConnectionLost_AndHaltsFurtherSaves()
        {
            var c = new QaClient();
            var s = Make(c, 0);
            s.LoadAsync().Wait();
            c.PendingSave = null;
            c.Save = (json, rev) => throw new Exception("socket reset");
            s.RequestSave();
            s.Pump();
            Assert.AreEqual(1, _lost);
            Assert.AreEqual(1, c.Saves);

            // Halted: further changes must not be sent over the server's profile.
            s.RequestSave();
            s.Pump();
            s.Flush();
            Assert.AreEqual(1, c.Saves, "no saves while halted");
        }

        [Test]
        public void ConnectionLost_RaisedOncePerFailure_NotPerRetriedChange()
        {
            var c = new QaClient { Save = (j, r) => new ProfileResponse(0, false, null, 0, null) };
            var s = Make(c, 0);
            s.LoadAsync().Wait();
            s.RequestSave();
            s.Flush();
            s.RequestSave();
            s.Flush();
            s.Pump();
            Assert.AreEqual(1, _lost);
        }

        [Test]
        public void FailedSave_ThenReset_ThenLoad_ResendsWithServerRevision()
        {
            var c = new QaClient { Load = () => new ProfileResponse(200, false, "{\"gold\":1}", 4, null) };
            var s = Make(c, 0);
            s.LoadAsync().Wait();
            c.Save = (j, r) => new ProfileResponse(0, false, null, 0, null);
            s.RequestSave();
            s.Flush();
            Assert.AreEqual(1, _lost);

            // Connection-lost screen (014) retries: Reset then a fresh load.
            s.Reset();
            Assert.IsFalse(s.IsLoaded);
            c.Save = (j, r) => new ProfileResponse(200, false, j, r + 1, null);
            Assert.AreEqual(ProfileLoadOutcome.Existing, s.LoadAsync().Result);
            s.RequestSave();
            s.Flush();
            Assert.AreEqual(4, c.SaveRevisions[c.SaveRevisions.Count - 1]);
        }

        [Test]
        public void Conflict_ThenReloadFails_RaisesConnectionLost_NoReloadApplied()
        {
            var c = new QaClient { Load = () => new ProfileResponse(200, false, "{\"gold\":1}", 3, null) };
            c.Save = (j, r) => new ProfileResponse(409, false, null, 4, null);
            var s = Make(c, 0);
            s.LoadAsync().Wait();
            bool reloadRaised = false;
            s.ServerProfileApplied += (_, __) => reloadRaised = true;

            c.Load = () => new ProfileResponse(0, false, null, 0, null);
            s.RequestSave();
            s.Flush();

            Assert.AreEqual(1, _lost);
            Assert.IsFalse(reloadRaised, "a failed reload must not apply anything");
        }

        // ---- Revisions and save results ----

        [Test]
        public void SuccessiveSaves_ChainRevisions_FromServerResponse()
        {
            var c = new QaClient { Load = () => new ProfileResponse(200, false, "{}", 7, null) };
            var s = Make(c, 0);
            s.LoadAsync().Wait();
            s.RequestSave();
            s.Flush();
            s.RequestSave();
            s.Flush();
            s.RequestSave();
            s.Flush();
            CollectionAssert.AreEqual(new[] { 7, 8, 9 }, c.SaveRevisions);
            Assert.AreEqual(3, s.SaveCount);
        }

        [Test]
        public void SuccessfulSave_RaisesApplied_WithIsReloadFalse()
        {
            var c = new QaClient { Load = () => new ProfileResponse(200, false, "{}", 0, null) };
            var s = Make(c, 0);
            s.LoadAsync().Wait();
            bool? isReload = null;
            s.ServerProfileApplied += (_, r) => isReload = r;
            s.RequestSave();
            s.Flush();
            Assert.AreEqual(false, isReload);
        }

        [Test]
        public void SaveSendsCapturedJson_NotStaleCache()
        {
            var c = new QaClient();
            var counter = 0;
            var s = new ProfileSync(c, () => "{\"n\":" + (++counter) + "}", () => _now, 0);
            s.LoadAsync().Wait();
            s.RequestSave();
            s.Flush();
            s.RequestSave();
            s.Flush();
            CollectionAssert.AreEqual(new[] { "{\"n\":1}", "{\"n\":2}" }, c.SaveJsons);
        }

        // ---- Debounce edge cases ----

        [Test]
        public void Pump_BeforeDebounceElapses_SendsNothing()
        {
            var c = new QaClient();
            var s = Make(c, 1.0);
            s.LoadAsync().Wait();
            s.RequestSave();
            _now = 0.99;
            s.Pump();
            Assert.AreEqual(0, c.Saves);
        }

        [Test]
        public void Burst_ThenNoFurtherChanges_NoExtraSavesOnLaterPumps()
        {
            var c = new QaClient();
            var s = Make(c, 1.0);
            s.LoadAsync().Wait();
            for (var i = 0; i < 5; i++) { s.RequestSave(); }
            _now = 1.0;
            s.Pump();
            _now = 10.0;
            for (var i = 0; i < 100; i++) { s.Pump(); }
            Assert.AreEqual(1, c.Saves);
        }

        [UnityTest]
        public IEnumerator Flush_DuringInFlightSave_SendsOneMore_WithoutDebounce()
        {
            var c = new QaClient { PendingSave = new TaskCompletionSource<ProfileResponse>() };
            var s = Make(c, 1.0);
            s.LoadAsync().Wait();
            s.RequestSave();
            s.Flush(); // starts save 1, now in flight
            Assert.AreEqual(1, c.Saves);

            s.RequestSave(); // change arrives mid-save; debounce not elapsed
            s.Flush();
            Assert.AreEqual(1, c.Saves, "flush must wait for the in-flight save");

            c.PendingSave.SetResult(new ProfileResponse(200, false, "{}", 1, null));
            c.PendingSave = null;
            // The save's continuation (which re-runs the pending flush) is posted to the
            // main thread, so yield one editor update to let it run.
            yield return null;
            Assert.AreEqual(2, c.Saves);
        }

        [Test]
        public void Flush_WithNothingDirty_SendsNothing()
        {
            var c = new QaClient();
            var s = Make(c);
            s.LoadAsync().Wait();
            s.Flush();
            Assert.AreEqual(0, c.Saves);
        }

        // ---- PlayerHealth: server-owned current max and regen clock ----

        [Test]
        public void ApplyServerState_ClampsBelowFloor_ToFloor()
        {
            var h = new PlayerHealth(100, 10, 30f, () => DateTime.UtcNow);
            var fired = 0;
            h.Changed += () => fired++;
            h.ApplyServerState(-5, DateTime.UtcNow.Ticks);
            Assert.AreEqual(h.Floor, h.CurrentMaxHealth);
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void ApplyServerState_ClampsAboveTrueMax_ToTrueMax()
        {
            var h = new PlayerHealth(100, 10, 30f, () => DateTime.UtcNow);
            h.ApplyServerState(9999, DateTime.UtcNow.Ticks);
            Assert.AreEqual(100, h.CurrentMaxHealth);
        }

        [Test]
        public void ApplyServerState_RegenClock_IsServersNotDevices()
        {
            var server = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
            var device = server;
            var mono = 0.0;
            var h = new PlayerHealth(100, 10, 30f, () => device, () => mono);
            h.SetServerTime(server);
            h.ApplyServerState(40, server.Ticks);
            mono = 30;
            h.Regenerate();
            Assert.AreEqual(50, h.CurrentMaxHealth, "one 30s tick credits exactly one regen step");
        }

        [Test]
        public void Regen_DeviceClockChangedAfterSync_CreditsNothing_ForwardOrBack()
        {
            var server = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
            var device = server;
            var h = new PlayerHealth(100, 10, 30f, () => device);
            h.LoadState(100, 40, server.Ticks);
            h.SetServerTime(server);

            device = server.AddHours(1); // user sets clock forward after sync
            h.Regenerate();
            Assert.AreEqual(40, h.CurrentMaxHealth, "forward clock jump credits nothing");

            device = server.AddHours(-1); // and backward
            h.Regenerate();
            Assert.AreEqual(40, h.CurrentMaxHealth, "backward clock jump credits nothing");
        }

        [Test]
        public void Regen_ServerTimeAdvances_CreditsTicks_RegardlessOfDeviceClock()
        {
            var server = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
            var device = server.AddHours(-3);
            var mono = 0.0;
            var h = new PlayerHealth(100, 10, 30f, () => device, () => mono);
            h.LoadState(100, 40, server.Ticks);
            h.SetServerTime(server);

            mono = 90; // three 30s ticks of real time
            h.Regenerate();
            Assert.AreEqual(70, h.CurrentMaxHealth);
        }

        // ---- Save-break and lifecycle guards (source-level) ----

        [Test]
        public void SaveSystem_IsNotCalledByAnyGameScript()
        {
            var scripts = Path.Combine(Application.dataPath, "Scripts");
            var offenders = new List<string>();
            foreach (var file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "SaveSystem.cs")
                {
                    continue;
                }

                if (Regex.IsMatch(File.ReadAllText(file), @"\bSaveSystem\s*\.\s*(Save|Load|Read|Write|Exists|Delete)\w*\s*\("))
                {
                    offenders.Add(file);
                }
            }

            Assert.AreEqual(0, offenders.Count, "SaveSystem disk I/O must not be called by the game: " + string.Join(", ", offenders));
        }

        [Test]
        public void ProfileController_FlushesOnPauseAndQuit()
        {
            var path = Path.Combine(Application.dataPath, "Scripts", "Inventory", "ProfileController.cs");
            var src = File.ReadAllText(path);
            Assert.IsTrue(Regex.IsMatch(src, @"OnApplicationPause\(bool \w+\)\s*\{[^}]*_sync\?\.Flush\(\)"),
                "OnApplicationPause must flush the debounced save");
            Assert.IsTrue(Regex.IsMatch(src, @"OnApplicationQuit\(\)\s*\{[^}]*_sync\?\.Flush\(\)"),
                "OnApplicationQuit must flush the debounced save");
        }

        [Test]
        public void ProfileController_NeverStartsFreshAfterFailedLoad()
        {
            var path = Path.Combine(Application.dataPath, "Scripts", "Inventory", "ProfileController.cs");
            var src = File.ReadAllText(path);
            var failIdx = src.IndexOf("ProfileLoadOutcome.Failed", StringComparison.Ordinal);
            var freshIdx = src.IndexOf("StartFresh();", StringComparison.Ordinal);
            Assert.IsTrue(failIdx >= 0 && freshIdx > failIdx,
                "StartFresh must only run after the Failed early-return guard");
            Assert.IsTrue(Regex.IsMatch(src, @"ProfileLoadOutcome\.Failed\)\s*\{\s*return;"),
                "a Failed load must return before any fresh-profile path");
        }
    }
}
