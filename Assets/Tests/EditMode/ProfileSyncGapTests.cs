using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DoomScroller.Inventory;
using DoomScroller.Network;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // QA gap coverage for task 013, on top of ProfileSyncTests and ProfileSyncQaTests:
    // revision chaining after a 409 reload, a server error on a "new" load, Reset gating,
    // and the startup ordering in ProfileController (load before any fresh/loaded apply).
    public class ProfileSyncGapTests
    {
        private class GapClient : IServerClient
        {
            public bool IsSignedIn { get; set; } = true;
            public string PlayerId => "gap";
            public Func<ProfileResponse> Load = () => new ProfileResponse(200, false, "{}", 3, null);
            public Func<string, int, ProfileResponse> Save = (j, r) => new ProfileResponse(200, false, j, r + 1, null);
            public readonly List<int> SaveRevisions = new List<int>();
            public int Loads, Saves;

            public Task<bool> SignInAsync() { IsSignedIn = true; return Task.FromResult(true); }
            public Task<ServerResponse> SendAsync(string m, string p, string b = null) =>
                Task.FromResult(new ServerResponse(200, "{}"));
            public Task<ProfileResponse> LoadProfileAsync() { Loads++; return Task.FromResult(Load()); }
            public Task<ProfileResponse> SaveProfileAsync(string json, int rev)
            {
                Saves++;
                SaveRevisions.Add(rev);
                return Task.FromResult(Save(json, rev));
            }
        }

        private double _now;
        private int _lost;

        [SetUp]
        public void SetUp() { _now = 0; _lost = 0; }

        private ProfileSync Make(GapClient c, double debounce = 0)
        {
            var s = new ProfileSync(c, () => "{\"gold\":1}", () => _now, debounce);
            s.ConnectionLost += () => _lost++;
            return s;
        }

        [Test]
        public void Conflict_ThenReloadSucceeds_NextSaveUsesReloadedRevision_NoConnectionLost()
        {
            var c = new GapClient
            {
                Load = () => new ProfileResponse(200, false, "{\"gold\":1}", 3, null),
            };
            c.Save = (j, r) => c.Saves == 1
                ? new ProfileResponse(409, false, null, 4, null)
                : new ProfileResponse(200, false, j, r + 1, null);
            var s = Make(c);
            s.LoadAsync().Wait();

            // Server changed the profile (a fight): the reload now returns revision 4.
            c.Load = () => new ProfileResponse(200, false, "{\"gold\":9}", 4, null);
            s.RequestSave();
            s.Flush(); // 409 -> reload
            Assert.AreEqual(0, _lost, "a conflict with a successful reload is not a connection loss");

            s.RequestSave();
            s.Flush();
            Assert.AreEqual(2, c.Saves);
            Assert.AreEqual(4, c.SaveRevisions[1], "the save after a reload must chain from the reloaded revision");
            Assert.AreEqual(0, _lost);
        }

        [Test]
        public void NewLoad_WithServerError_IsFailure_NeverStartsFresh()
        {
            // IsNew=true but the status is an error: must not be treated as a new player.
            var c = new GapClient { Load = () => new ProfileResponse(500, true, null, 0, null) };
            var s = Make(c);
            Assert.AreEqual(ProfileLoadOutcome.Failed, s.LoadAsync().Result);
            Assert.AreEqual(1, _lost);
            Assert.IsFalse(s.IsLoaded);
        }

        [Test]
        public void NewPlayer_FirstSave_SendsRevisionZero_AndIsLoadedBeforeSave()
        {
            var c = new GapClient { Load = () => new ProfileResponse(200, true, null, 0, null) };
            var s = Make(c);
            Assert.AreEqual(ProfileLoadOutcome.New, s.LoadAsync().Result);
            Assert.IsTrue(s.IsLoaded, "gameplay input gate opens once the load succeeds");
            s.RequestSave();
            s.Flush();
            Assert.AreEqual(1, c.Saves);
            Assert.AreEqual(0, c.SaveRevisions[0]);
        }

        [Test]
        public void Reset_ClearsLoaded_AndPumpSendsNothingUntilLoadedAgain()
        {
            var c = new GapClient();
            var s = Make(c, 0);
            s.LoadAsync().Wait();
            s.Reset();
            Assert.IsFalse(s.IsLoaded);
            s.RequestSave();
            s.Pump();
            s.Flush();
            Assert.AreEqual(0, c.Saves, "nothing is sent between Reset and a fresh load");
        }

        [Test]
        public void Startup_LoadsBeforeAnyFreshOrLoadedApply_AndRaisesLoadedLast()
        {
            var path = Path.Combine(Application.dataPath, "Scripts", "Inventory", "ProfileController.cs");
            var src = File.ReadAllText(path);
            var start = src.IndexOf("public async System.Threading.Tasks.Task RunStartupAsync", StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "RunStartupAsync not found");
            var end = src.IndexOf("private void BuildDefaultState", start, StringComparison.Ordinal);
            var body = src.Substring(start, end - start);

            var load = body.IndexOf("_sync.LoadAsync()", StringComparison.Ordinal);
            var fresh = body.IndexOf("StartFresh();", StringComparison.Ordinal);
            var applied = body.IndexOf("ApplyProfile(_sync.LoadedProfile.Json)", StringComparison.Ordinal);
            var loaded = body.IndexOf("Loaded?.Invoke()", StringComparison.Ordinal);

            Assert.IsTrue(load >= 0 && fresh > load, "sign-in/load must precede StartFresh");
            Assert.IsTrue(applied > load, "an existing profile is applied only after LoadProfile");
            Assert.IsTrue(loaded > fresh && loaded > applied, "gameplay Loaded event fires after the profile is applied");
        }

        [Test]
        public void ServerProfileApplied_AppliesCurrentMaxHealth_ToPlayerHealth()
        {
            // Source-level check: after load and after every save the server's healthCurrentMax
            // is applied (the controller owns the mapping; ProfileSync only raises the event).
            var path = Path.Combine(Application.dataPath, "Scripts", "Inventory", "ProfileController.cs");
            var src = File.ReadAllText(path);
            var start = src.IndexOf("private void OnServerProfileApplied", StringComparison.Ordinal);
            Assert.IsTrue(start >= 0);
            var body = src.Substring(start, src.IndexOf("private string CaptureJson", start, StringComparison.Ordinal) - start);
            Assert.IsTrue(body.Contains("_health.ApplyServerState(save.healthCurrentMax"),
                "server current max must be applied on load and save");
        }
    }
}
