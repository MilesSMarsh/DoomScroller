using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DoomScroller.Network;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    public class ServerClientTests
    {
        class FakeStore : IServerCredentialStore
        {
            public string InstallId { get; set; }
            public string Token { get; set; }
            public string PlayerId { get; set; }
        }

        class FakeTransport : IServerTransport
        {
            public readonly List<(string url, string token)> Calls = new List<(string, string)>();
            public Func<string, string, ServerResponse> Handler;
            public Task<ServerResponse> SendAsync(string method, string url, string body, string token, TimeSpan timeout)
            {
                Calls.Add((url, token));
                return Task.FromResult(Handler(url, token));
            }
        }

        static int signIns;
        static ServerResponse SignInOk() =>
            new ServerResponse(200, "{\"playerId\":\"p1\",\"token\":\"tok" + (++signIns) + "\"}");

        static ServerClient Make(IServerTransport t, FakeStore s) =>
            new ServerClient(t, s, "http://x/", TimeSpan.FromSeconds(1));

        [Test]
        public void SignIn_CreatesInstallIdOnce_AndStoresToken()
        {
            var store = new FakeStore();
            var t = new FakeTransport { Handler = (u, k) => SignInOk() };
            var c = Make(t, store);
            Assert.IsTrue(c.SignInAsync().Result);
            var install = store.InstallId;
            Assert.IsNotEmpty(install);
            Assert.IsTrue(c.SignInAsync().Result);
            Assert.AreEqual(install, store.InstallId);
            Assert.IsTrue(c.IsSignedIn);
            Assert.AreEqual("p1", c.PlayerId);
            Assert.AreEqual("http://x/auth/anonymous", t.Calls[0].url);
        }

        [Test]
        public void Send_AttachesStoredToken()
        {
            var store = new FakeStore { Token = "stored" };
            var t = new FakeTransport { Handler = (u, k) => new ServerResponse(200, "{}") };
            var res = Make(t, store).SendAsync("GET", "/auth/me").Result;
            Assert.IsTrue(res.Ok);
            Assert.AreEqual(1, t.Calls.Count);
            Assert.AreEqual("stored", t.Calls[0].token);
            Assert.AreEqual("http://x/auth/me", t.Calls[0].url);
        }

        [Test]
        public void Send_Unauthorized_ReSignsInOnce_ThenFailsCleanly()
        {
            var store = new FakeStore { Token = "stale" };
            var t = new FakeTransport
            {
                Handler = (u, k) => u.EndsWith("/auth/anonymous") ? SignInOk() : new ServerResponse(401, null)
            };
            var res = Make(t, store).SendAsync("GET", "/auth/me").Result;
            Assert.AreEqual(401, res.Status);
            // request, one sign-in, one retry — no loop
            Assert.AreEqual(3, t.Calls.Count);
            Assert.IsTrue(t.Calls[1].url.EndsWith("/auth/anonymous"));
        }

        [Test]
        public void Send_Unauthorized_ThenRetrySucceeds()
        {
            var store = new FakeStore { Token = "stale" };
            var t = new FakeTransport
            {
                Handler = (u, k) => u.EndsWith("/auth/anonymous") ? SignInOk()
                    : k == "stale" ? new ServerResponse(401, null) : new ServerResponse(200, "{}")
            };
            Assert.IsTrue(Make(t, store).SendAsync("GET", "/auth/me").Result.Ok);
        }

        [Test]
        public void Send_WhenSignInFails_ReturnsNoResponse()
        {
            var t = new FakeTransport { Handler = (u, k) => new ServerResponse(0, null) };
            var res = Make(t, new FakeStore()).SendAsync("GET", "/auth/me").Result;
            Assert.AreEqual(0, res.Status);
            Assert.AreEqual(1, t.Calls.Count);
        }

        [Test]
        public void Send_WhenSignedOut_SignsInFirst_AndAttachesNewToken()
        {
            var store = new FakeStore();
            var t = new FakeTransport
            {
                Handler = (u, k) => u.EndsWith("/auth/anonymous") ? SignInOk() : new ServerResponse(200, "{}")
            };
            var res = Make(t, store).SendAsync("GET", "/auth/me").Result;
            Assert.IsTrue(res.Ok);
            Assert.AreEqual(2, t.Calls.Count);
            Assert.IsTrue(t.Calls[0].url.EndsWith("/auth/anonymous"));
            Assert.IsNull(t.Calls[0].token);
            Assert.AreEqual(store.Token, t.Calls[1].token);
            Assert.IsNotEmpty(store.Token);
        }

        [Test]
        public void Send_Unauthorized_WhenReSignInFails_FailsCleanlyWith401()
        {
            var store = new FakeStore { Token = "stale" };
            var t = new FakeTransport
            {
                Handler = (u, k) => u.EndsWith("/auth/anonymous") ? new ServerResponse(500, null)
                    : new ServerResponse(401, null)
            };
            var res = Make(t, store).SendAsync("GET", "/auth/me").Result;
            Assert.AreEqual(401, res.Status);
            // request, one failed sign-in, no retry of the protected request
            Assert.AreEqual(2, t.Calls.Count);
            Assert.IsNull(store.Token);
        }

        [Test]
        public void Send_ServerError_DoesNotTriggerReSignIn()
        {
            var store = new FakeStore { Token = "stored" };
            var t = new FakeTransport { Handler = (u, k) => new ServerResponse(500, null) };
            var res = Make(t, store).SendAsync("GET", "/auth/me").Result;
            Assert.AreEqual(500, res.Status);
            Assert.AreEqual(1, t.Calls.Count);
            Assert.AreEqual("stored", store.Token);
        }

        [Test]
        public void SignIn_SendsStoredInstallId_InJsonBody()
        {
            var store = new FakeStore { InstallId = "11111111-2222-3333-4444-555555555555" };
            string sentBody = null;
            var t = new BodyCapturingTransport(b => sentBody = b, (u, k) => SignInOk());
            Assert.IsTrue(Make(t, store).SignInAsync().Result);
            Assert.IsNotNull(sentBody);
            StringAssert.Contains("\"installId\":\"11111111-2222-3333-4444-555555555555\"", sentBody);
            Assert.AreEqual("11111111-2222-3333-4444-555555555555", store.InstallId);
        }

        [Test]
        public void SignIn_ResponseWithoutToken_ReturnsFalse_AndStoresNothing()
        {
            var store = new FakeStore();
            var t = new FakeTransport { Handler = (u, k) => new ServerResponse(200, "{\"playerId\":\"p1\"}") };
            Assert.IsFalse(Make(t, store).SignInAsync().Result);
            Assert.IsNull(store.Token);
            Assert.IsNull(store.PlayerId);
        }

        [Test]
        public void SignIn_MalformedBody_ReturnsFalse()
        {
            var store = new FakeStore();
            var t = new FakeTransport { Handler = (u, k) => new ServerResponse(200, "not json {") };
            Assert.IsFalse(Make(t, store).SignInAsync().Result);
            Assert.IsNull(store.Token);
        }

        [Test]
        public void SignIn_NonSuccessStatus_ReturnsFalse_AndKeepsExistingToken()
        {
            var store = new FakeStore { Token = "existing", PlayerId = "p0" };
            var t = new FakeTransport { Handler = (u, k) => new ServerResponse(400, null) };
            Assert.IsFalse(Make(t, store).SignInAsync().Result);
            Assert.AreEqual("existing", store.Token);
            Assert.AreEqual("p0", store.PlayerId);
        }

        [Test]
        public void Client_UsesConfiguredBaseUrl_WithoutDoubleSlash()
        {
            var store = new FakeStore { Token = "stored" };
            var t = new FakeTransport { Handler = (u, k) => new ServerResponse(200, "{}") };
            new ServerClient(t, store, "http://example.test:9000//", TimeSpan.FromSeconds(1))
                .SendAsync("GET", "/auth/me").Wait();
            Assert.AreEqual("http://example.test:9000/auth/me", t.Calls[0].url);
        }

        class BodyCapturingTransport : IServerTransport
        {
            readonly Action<string> onBody;
            readonly Func<string, string, ServerResponse> handler;
            public BodyCapturingTransport(Action<string> onBody, Func<string, string, ServerResponse> handler)
            {
                this.onBody = onBody;
                this.handler = handler;
            }
            public Task<ServerResponse> SendAsync(string method, string url, string body, string token, TimeSpan timeout)
            {
                onBody(body);
                return Task.FromResult(handler(url, token));
            }
        }
    }
}
