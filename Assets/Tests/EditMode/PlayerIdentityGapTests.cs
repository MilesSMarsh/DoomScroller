using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DoomScroller.Network;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    // Task 011 edge cases not covered by ServerClientTests / ServerClientContractTests:
    // empty install id, install id kept across failed sign-ins, empty/null sign-in bodies,
    // and a signed-in client reading its stored player id without a network call.
    public class PlayerIdentityGapTests
    {
        class MemoryStore : IServerCredentialStore
        {
            public string InstallId { get; set; }
            public string Token { get; set; }
            public string PlayerId { get; set; }
        }

        class QueueTransport : IServerTransport
        {
            public readonly List<(string url, string body, string token)> Calls = new List<(string, string, string)>();
            readonly Queue<ServerResponse> responses = new Queue<ServerResponse>();

            public QueueTransport Enqueue(params ServerResponse[] items)
            {
                foreach (var r in items) responses.Enqueue(r);
                return this;
            }

            public Task<ServerResponse> SendAsync(string method, string url, string body, string token, TimeSpan timeout)
            {
                Calls.Add((url, body, token));
                var next = responses.Count > 0 ? responses.Dequeue() : new ServerResponse(200, "{}");
                return Task.FromResult(next);
            }
        }

        static ServerClient Make(IServerTransport t, IServerCredentialStore s) =>
            new ServerClient(t, s, "http://dev.test", TimeSpan.FromSeconds(5));

        static ServerResponse SignInOk(string player, string token) =>
            new ServerResponse(200, "{\"playerId\":\"" + player + "\",\"token\":\"" + token + "\"}");

        [Test]
        public void SignIn_EmptyInstallId_GeneratesNewGuid_AndSendsIt()
        {
            var store = new MemoryStore { InstallId = "" };
            var t = new QueueTransport().Enqueue(SignInOk("p1", "tok"));
            Assert.IsTrue(Make(t, store).SignInAsync().Result);

            Assert.IsTrue(Guid.TryParse(store.InstallId, out var parsed));
            Assert.AreNotEqual(Guid.Empty, parsed);
            StringAssert.Contains("\"installId\":\"" + store.InstallId + "\"", t.Calls[0].body);
        }

        [Test]
        public void SignIn_FailedRequest_KeepsInstallId_ForNextAttempt()
        {
            // Install id is created once and survives a failed sign-in, so a retry on the
            // same device is the same install to the server.
            var store = new MemoryStore();
            var t = new QueueTransport().Enqueue(new ServerResponse(500, null), SignInOk("p1", "tok"));
            var client = Make(t, store);

            Assert.IsFalse(client.SignInAsync().Result);
            var install = store.InstallId;
            Assert.IsTrue(Guid.TryParse(install, out _));

            Assert.IsTrue(client.SignInAsync().Result);
            Assert.AreEqual(install, store.InstallId);
            StringAssert.Contains("\"installId\":\"" + install + "\"", t.Calls[1].body);
        }

        [Test]
        public void SignIn_EmptyTokenString_ReturnsFalse_AndStoresNothing()
        {
            var store = new MemoryStore();
            var t = new QueueTransport().Enqueue(new ServerResponse(200, "{\"playerId\":\"p1\",\"token\":\"\"}"));
            Assert.IsFalse(Make(t, store).SignInAsync().Result);
            Assert.IsNull(store.Token);
            Assert.IsNull(store.PlayerId);
            Assert.IsFalse(Make(t, store).IsSignedIn);
        }

        [Test]
        public void SignIn_NullBody_ReturnsFalse_WithoutThrowing()
        {
            var store = new MemoryStore();
            var t = new QueueTransport().Enqueue(new ServerResponse(200, null));
            // An exception from .Result would fail the test; a clean result is the expectation.
            Assert.IsFalse(Make(t, store).SignInAsync().Result);
            Assert.IsNull(store.Token);
        }

        [Test]
        public void SignedInClient_ReadsStoredPlayerId_WithoutSigningInAgain()
        {
            var store = new MemoryStore { Token = "stored", PlayerId = "p9", InstallId = "11111111-2222-3333-4444-555555555555" };
            var t = new QueueTransport().Enqueue(new ServerResponse(200, "{}"));
            var client = Make(t, store);

            Assert.IsTrue(client.IsSignedIn);
            Assert.AreEqual("p9", client.PlayerId);
            Assert.IsTrue(client.SendAsync("GET", "/auth/me").Result.Ok);
            Assert.AreEqual(1, t.Calls.Count, "a stored token must not trigger a sign-in call");
            Assert.AreEqual("stored", t.Calls[0].token);
        }
    }
}
