using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DoomScroller.Inventory;
using DoomScroller.Network;
using DoomScroller.Progression;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    public class ProfileSyncTests
    {
        private class FakeClient : IServerClient
        {
            public bool IsSignedIn { get; set; } = true;
            public string PlayerId => "p";
            public bool SignInOk = true;
            public ProfileResponse LoadResponse = new ProfileResponse(200, true, null, 0, null);
            public Func<ProfileResponse> SaveResponse = () => new ProfileResponse(200, false, "{}", 2, null);
            public TaskCompletionSource<ProfileResponse> PendingSave;
            public int Loads, Saves, SignIns;
            public List<int> SaveRevisions = new List<int>();

            public Task<bool> SignInAsync() { SignIns++; IsSignedIn = SignInOk; return Task.FromResult(SignInOk); }
            public Task<ServerResponse> SendAsync(string m, string p, string b = null) => Task.FromResult(new ServerResponse(200, "{}"));
            public Task<ProfileResponse> LoadProfileAsync() { Loads++; return Task.FromResult(LoadResponse); }
            public Task<ProfileResponse> SaveProfileAsync(string json, int rev)
            {
                Saves++;
                SaveRevisions.Add(rev);
                return PendingSave != null ? PendingSave.Task : Task.FromResult(SaveResponse());
            }
        }

        private double _now;
        private int _lost;

        [SetUp]
        public void SetUp() { _now = 0; _lost = 0; }

        private ProfileSync Make(FakeClient c, double debounce = 1.0)
        {
            var s = new ProfileSync(c, () => "{}", () => _now, debounce);
            s.ConnectionLost += () => _lost++;
            return s;
        }

        [Test]
        public void NewPlayer_LoadsAsNew_AndSavesWithRevisionZero()
        {
            var c = new FakeClient();
            var s = Make(c);
            Assert.AreEqual(ProfileLoadOutcome.New, s.LoadAsync().Result);
            s.RequestSave();
            s.Flush();
            Assert.AreEqual(1, c.Saves);
            Assert.AreEqual(0, c.SaveRevisions[0]);
            Assert.AreEqual(0, _lost);
        }

        [Test]
        public void ExistingProfile_Loads()
        {
            var c = new FakeClient { LoadResponse = new ProfileResponse(200, false, "{\"gold\":5}", 7, "2026-01-01T00:00:00Z") };
            var s = Make(c);
            Assert.AreEqual(ProfileLoadOutcome.Existing, s.LoadAsync().Result);
            Assert.AreEqual("{\"gold\":5}", s.LoadedProfile.Json);
            s.RequestSave();
            s.Flush();
            Assert.AreEqual(7, c.SaveRevisions[0]);
        }

        [Test]
        public void LoadFailure_RaisesConnectionLost_NotNew()
        {
            var c = new FakeClient { LoadResponse = new ProfileResponse(0, false, null, 0, null) };
            var s = Make(c);
            Assert.AreEqual(ProfileLoadOutcome.Failed, s.LoadAsync().Result);
            Assert.AreEqual(1, _lost);
            Assert.IsFalse(s.IsLoaded);
            s.RequestSave();
            s.Flush();
            Assert.AreEqual(0, c.Saves);
        }

        [Test]
        public void SignInFailure_RaisesConnectionLost()
        {
            var c = new FakeClient { IsSignedIn = false, SignInOk = false };
            var s = Make(c);
            Assert.AreEqual(ProfileLoadOutcome.Failed, s.LoadAsync().Result);
            Assert.AreEqual(1, _lost);
            Assert.AreEqual(0, c.Loads);
        }

        [Test]
        public void SaveFailure_RaisesConnectionLost()
        {
            var c = new FakeClient { SaveResponse = () => new ProfileResponse(0, false, null, 0, null) };
            var s = Make(c);
            s.LoadAsync().Wait();
            s.RequestSave();
            s.Flush();
            Assert.AreEqual(1, _lost);
        }

        [Test]
        public void Burst_SendsOneSave_AfterDebounce()
        {
            var c = new FakeClient();
            var s = Make(c, 1.0);
            s.LoadAsync().Wait();
            for (var i = 0; i < 10; i++) { s.RequestSave(); s.Pump(); }
            Assert.AreEqual(0, c.Saves);
            _now = 1.5;
            s.Pump();
            s.Pump();
            Assert.AreEqual(1, c.Saves);
        }

        [Test]
        public void ChangeDuringSave_QueuesExactlyOneMore()
        {
            var c = new FakeClient { PendingSave = new TaskCompletionSource<ProfileResponse>() };
            var s = Make(c, 0);
            s.LoadAsync().Wait();
            s.RequestSave();
            s.Pump();
            Assert.AreEqual(1, c.Saves);
            for (var i = 0; i < 5; i++) { s.RequestSave(); s.Pump(); }
            Assert.AreEqual(1, c.Saves, "at most one in flight");
            c.PendingSave.SetResult(new ProfileResponse(200, false, "{}", 2, null));
            c.PendingSave = null;
            s.Pump();
            Assert.AreEqual(2, c.Saves);
            Assert.AreEqual(2, c.SaveRevisions[1]);
        }

        [Test]
        public void Conflict_ReloadsAndRaisesReload_NoOverwrite()
        {
            var c = new FakeClient
            {
                LoadResponse = new ProfileResponse(200, false, "{\"gold\":1}", 3, null),
                SaveResponse = () => new ProfileResponse(409, false, null, 4, null),
            };
            var s = Make(c);
            s.LoadAsync().Wait();
            bool? reload = null;
            s.ServerProfileApplied += (_, r) => reload = r;
            c.LoadResponse = new ProfileResponse(200, false, "{\"gold\":9}", 4, null);
            s.RequestSave();
            s.Flush();
            Assert.AreEqual(true, reload);
            Assert.AreEqual(1, c.Saves);
            Assert.AreEqual(0, _lost);
        }

        [Test]
        public void Regen_DeviceClockAnHourAhead_CreditsNothing()
        {
            var server = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var device = server.AddHours(1);
            var mono = 0.0;
            var h = new PlayerHealth(100, 10, 30f, () => device, () => mono);
            h.LoadState(100, 40, server.Ticks);
            h.SetServerTime(server);
            h.Regenerate();
            Assert.AreEqual(40, h.CurrentMaxHealth);

            device = device.AddHours(5); // device clock changes mean nothing
            mono += 60; // 60s real time = 2 ticks
            h.Regenerate();
            Assert.AreEqual(60, h.CurrentMaxHealth);
        }

        [Test]
        public void ApplyServerState_SetsCurrentMax()
        {
            var h = new PlayerHealth(100, 10, 30f);
            h.ApplyServerState(55, DateTime.UtcNow.Ticks);
            Assert.AreEqual(55, h.CurrentMaxHealth);
        }
    }
}
