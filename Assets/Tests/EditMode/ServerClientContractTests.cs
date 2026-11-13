using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DoomScroller.Network;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // Task 011 coverage gaps: timeout passthrough, token swap on retry, status-0 handling,
    // install id persistence across client instances, and the device credential store.
    public class ServerClientContractTests
    {
        class MemoryStore : IServerCredentialStore
        {
            public string InstallId { get; set; }
            public string Token { get; set; }
            public string PlayerId { get; set; }
        }

        struct Call
        {
            public string Method;
            public string Url;
            public string Body;
            public string Token;
            public TimeSpan Timeout;
        }

        // Replays scripted responses in order; the last one repeats if the queue runs out.
        class ScriptedTransport : IServerTransport
        {
            public readonly List<Call> Calls = new List<Call>();
            readonly Queue<ServerResponse> responses = new Queue<ServerResponse>();
            ServerResponse last = new ServerResponse(200, "{}");

            public ScriptedTransport Enqueue(params ServerResponse[] items)
            {
                foreach (var r in items) responses.Enqueue(r);
                return this;
            }

            public Task<ServerResponse> SendAsync(string method, string url, string body, string token, TimeSpan timeout)
            {
                Calls.Add(new Call { Method = method, Url = url, Body = body, Token = token, Timeout = timeout });
                if (responses.Count > 0) last = responses.Dequeue();
                return Task.FromResult(last);
            }
        }

        static string SignInBody(string playerId, string token) =>
            "{\"playerId\":\"" + playerId + "\",\"token\":\"" + token + "\"}";

        static ServerClient Make(IServerTransport t, IServerCredentialStore s, TimeSpan? timeout = null) =>
            new ServerClient(t, s, "http://dev.test", timeout ?? TimeSpan.FromSeconds(7));

        [Test]
        public void Send_PassesConfiguredTimeout_OnEveryRequest()
        {
            var t = new ScriptedTransport().Enqueue(
                new ServerResponse(200, SignInBody("p1", "tok-a")),
                new ServerResponse(200, "{}"));
            Make(t, new MemoryStore(), TimeSpan.FromSeconds(7)).SendAsync("GET", "/auth/me").Wait();
            Assert.AreEqual(2, t.Calls.Count);
            foreach (var call in t.Calls) Assert.AreEqual(TimeSpan.FromSeconds(7), call.Timeout);
        }

        [Test]
        public void Send_PassesMethodBodyAndUrl_ToTransport()
        {
            var store = new MemoryStore { Token = "stored" };
            var t = new ScriptedTransport().Enqueue(new ServerResponse(200, "{}"));
            Make(t, store).SendAsync("POST", "/auth/me", "{\"x\":1}").Wait();
            Assert.AreEqual("POST", t.Calls[0].Method);
            Assert.AreEqual("http://dev.test/auth/me", t.Calls[0].Url);
            Assert.AreEqual("{\"x\":1}", t.Calls[0].Body);
        }

        [Test]
        public void Send_Unauthorized_RetryAttachesNewToken_NotStaleOne()
        {
            var store = new MemoryStore { Token = "stale" };
            var t = new ScriptedTransport().Enqueue(
                new ServerResponse(401, null),                       // protected call, stale token
                new ServerResponse(200, SignInBody("p1", "fresh")),  // re-sign-in
                new ServerResponse(200, "{\"ok\":true}"));           // retry
            var res = Make(t, store).SendAsync("POST", "/auth/me", "{}").Result;
            Assert.IsTrue(res.Ok);
            Assert.AreEqual("{\"ok\":true}", res.Body);
            Assert.AreEqual("stale", t.Calls[0].Token);
            Assert.AreEqual("fresh", t.Calls[2].Token);
            Assert.AreEqual("{}", t.Calls[2].Body, "retry must resend the original body");
            Assert.AreEqual("fresh", store.Token);
        }

        [Test]
        public void Send_Unauthorized_ReSignIn_UpdatesStoredPlayerId()
        {
            var store = new MemoryStore { Token = "stale", PlayerId = "old-player" };
            var t = new ScriptedTransport().Enqueue(
                new ServerResponse(401, null),
                new ServerResponse(200, SignInBody("new-player", "fresh")),
                new ServerResponse(200, "{}"));
            Make(t, store).SendAsync("GET", "/auth/me").Wait();
            Assert.AreEqual("new-player", store.PlayerId);
        }

        [Test]
        public void Send_NoResponseOnSignedInRequest_ReturnsStatusZero_WithoutReSignIn()
        {
            // Timeout / connection failure on a signed-in request: no 401, so no re-sign-in.
            var store = new MemoryStore { Token = "stored" };
            var t = new ScriptedTransport().Enqueue(new ServerResponse(0, null));
            var res = Make(t, store).SendAsync("GET", "/auth/me").Result;
            Assert.AreEqual(0, res.Status);
            Assert.IsFalse(res.Ok);
            Assert.AreEqual(1, t.Calls.Count);
            Assert.AreEqual("stored", store.Token);
        }

        [Test]
        public void Send_SignInTimesOut_ReturnsStatusZero_AndDoesNotSend()
        {
            var t = new ScriptedTransport().Enqueue(new ServerResponse(0, null));
            var res = Make(t, new MemoryStore()).SendAsync("GET", "/auth/me").Result;
            Assert.AreEqual(0, res.Status);
            Assert.AreEqual(1, t.Calls.Count, "only the sign-in attempt; the protected request is never sent");
            Assert.IsTrue(t.Calls[0].Url.EndsWith("/auth/anonymous"));
        }

        [Test]
        public void SignIn_EmptyTokenString_IsNotSignedIn()
        {
            var store = new MemoryStore { Token = "" };
            var t = new ScriptedTransport();
            Assert.IsFalse(Make(t, store).IsSignedIn);
        }

        [Test]
        public void InstallId_IsValidGuid_AndSurvivesNewClientInstance()
        {
            // Simulates an app restart: same device store, new ServerClient.
            var store = new MemoryStore();
            var first = new ScriptedTransport().Enqueue(new ServerResponse(200, SignInBody("p1", "t1")));
            Assert.IsTrue(Make(first, store).SignInAsync().Result);
            var install = store.InstallId;
            Assert.IsTrue(Guid.TryParse(install, out var parsed));
            Assert.AreNotEqual(Guid.Empty, parsed);

            var second = new ScriptedTransport().Enqueue(new ServerResponse(200, SignInBody("p1", "t2")));
            Assert.IsTrue(Make(second, store).SignInAsync().Result);
            StringAssert.Contains("\"installId\":\"" + install + "\"", second.Calls[0].Body);
            Assert.AreEqual(install, store.InstallId);
        }

        [Test]
        public void InstallId_DifferentDevices_GetDifferentIds()
        {
            var a = new MemoryStore();
            var b = new MemoryStore();
            Make(new ScriptedTransport().Enqueue(new ServerResponse(200, SignInBody("p1", "t"))), a).SignInAsync().Wait();
            Make(new ScriptedTransport().Enqueue(new ServerResponse(200, SignInBody("p2", "t"))), b).SignInAsync().Wait();
            Assert.IsNotEmpty(a.InstallId);
            Assert.AreNotEqual(a.InstallId, b.InstallId);
        }

        [Test]
        public void ServerSettings_Load_WithoutAsset_FallsBackToUsableDefaults()
        {
            // No ServerSettings asset ships under Resources yet, so Load() must fall back.
            var s = ServerSettings.Load();
            Assert.IsNotNull(s);
            StringAssert.StartsWith("http", s.BaseUrl);
            Assert.Greater(s.TimeoutSeconds, 0);
        }

        [Test]
        public void CredentialStore_RoundTripsAndClearsOnNull()
        {
            var keys = new[] { "ds.server.installId", "ds.server.token", "ds.server.playerId" };
            try
            {
                foreach (var k in keys) PlayerPrefs.DeleteKey(k);
                var store = new PlayerPrefsCredentialStore();
                // Unity's GetString may return "" or null for a missing key; both mean "absent".
                Assert.IsTrue(string.IsNullOrEmpty(store.InstallId));
                Assert.IsTrue(string.IsNullOrEmpty(store.Token));
                Assert.IsTrue(string.IsNullOrEmpty(store.PlayerId));

                store.InstallId = "11111111-2222-3333-4444-555555555555";
                store.Token = "tok";
                store.PlayerId = "p1";
                Assert.AreEqual("11111111-2222-3333-4444-555555555555", new PlayerPrefsCredentialStore().InstallId);
                Assert.AreEqual("tok", new PlayerPrefsCredentialStore().Token);
                Assert.AreEqual("p1", new PlayerPrefsCredentialStore().PlayerId);

                store.Token = null;
                Assert.IsTrue(string.IsNullOrEmpty(new PlayerPrefsCredentialStore().Token));
                Assert.IsFalse(PlayerPrefs.HasKey("ds.server.token"));
            }
            finally
            {
                foreach (var k in keys) PlayerPrefs.DeleteKey(k);
            }
        }
    }
}
