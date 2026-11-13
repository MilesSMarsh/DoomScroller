using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DoomScroller.Network;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // QA coverage for task 012 (server-stored profile), client side: LoadProfileAsync /
    // SaveProfileAsync against a fake transport. Complements ServerClientTests.cs.
    public class ServerProfileQaTests
    {
        class MemoryStore : IServerCredentialStore
        {
            public string InstallId { get; set; }
            public string Token { get; set; }
            public string PlayerId { get; set; }
        }

        class ScriptedTransport : IServerTransport
        {
            public readonly List<(string method, string url, string body, string token)> Calls = new();
            public Func<string, string, string, string, ServerResponse> Handler;

            public Task<ServerResponse> SendAsync(string method, string url, string body, string token, TimeSpan timeout)
            {
                Calls.Add((method, url, body, token));
                return Task.FromResult(Handler(method, url, body, token));
            }
        }

        [Serializable]
        class EnvelopeDto
        {
            public bool isNew;
            public int revision;
            public string serverTimeUtc;
            public string profile;
        }

        const string SignInBody = "{\"playerId\":\"p1\",\"token\":\"fresh\"}";
        const string Time = "2026-11-13T12:00:00Z";

        static ServerClient Make(IServerTransport t, IServerCredentialStore s) =>
            new ServerClient(t, s, "http://x", TimeSpan.FromSeconds(1));

        static string Envelope(bool isNew, int revision, string profile) =>
            JsonUtility.ToJson(new EnvelopeDto { isNew = isNew, revision = revision, serverTimeUtc = Time, profile = profile });

        static string SaveDataJson()
        {
            var data = new SaveData { gold = 250, healthTrueMax = 100, healthCurrentMax = 60, progressionLevel = 3 };
            return JsonUtility.ToJson(data);
        }

        static string FixturePath() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Server", "tests", "fixtures", "savedata_sample.json"));

        [Test]
        public void LoadProfile_UsesGetOnProfilePath_WithStoredToken()
        {
            var store = new MemoryStore { Token = "t" };
            var t = new ScriptedTransport { Handler = (m, u, b, k) => new ServerResponse(200, Envelope(true, 0, null)) };
            Make(t, store).LoadProfileAsync().Wait();
            Assert.AreEqual("GET", t.Calls[0].method);
            Assert.AreEqual("http://x/profile", t.Calls[0].url);
            Assert.AreEqual("t", t.Calls[0].token);
            Assert.IsNull(t.Calls[0].body);
        }

        [Test]
        public void LoadProfile_NewMarker_IsOkNewWithNoJson_AndCarriesServerTime()
        {
            var store = new MemoryStore { Token = "t" };
            var t = new ScriptedTransport { Handler = (m, u, b, k) => new ServerResponse(200, Envelope(true, 0, null)) };
            var r = Make(t, store).LoadProfileAsync().Result;
            Assert.IsTrue(r.Ok);
            Assert.IsTrue(r.IsNew);
            Assert.IsNull(r.Json);
            Assert.AreEqual(0, r.Revision);
            Assert.AreEqual(Time, r.ServerTimeUtc);
        }

        [Test]
        public void LoadProfile_EmptyProfileString_IsTreatedAsNoJson()
        {
            var store = new MemoryStore { Token = "t" };
            var t = new ScriptedTransport { Handler = (m, u, b, k) => new ServerResponse(200, Envelope(false, 2, "")) };
            var r = Make(t, store).LoadProfileAsync().Result;
            Assert.IsNull(r.Json);
            Assert.AreEqual(2, r.Revision);
        }

        [Test]
        public void LoadProfile_Unauthorized_ReSignsInAndReturnsProfileOnRetry()
        {
            var store = new MemoryStore { Token = "stale" };
            var profile = SaveDataJson();
            var t = new ScriptedTransport
            {
                Handler = (m, u, b, k) =>
                {
                    if (u.EndsWith("/auth/anonymous")) return new ServerResponse(200, SignInBody);
                    if (k == "stale") return new ServerResponse(401, null);
                    return new ServerResponse(200, Envelope(false, 5, profile));
                }
            };
            var r = Make(t, store).LoadProfileAsync().Result;
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(5, r.Revision);
            Assert.AreEqual(profile, r.Json);
            Assert.AreEqual("fresh", store.Token);
        }

        [Test]
        public void LoadProfile_ServerError_ReturnsStatus_WithNoJson()
        {
            var store = new MemoryStore { Token = "t" };
            var t = new ScriptedTransport { Handler = (m, u, b, k) => new ServerResponse(503, "{\"error\":\"down\"}") };
            var r = Make(t, store).LoadProfileAsync().Result;
            Assert.AreEqual(503, r.Status);
            Assert.IsFalse(r.Ok);
            Assert.IsNull(r.Json);
            Assert.IsFalse(r.IsNew);
        }

        [Test]
        public void LoadProfile_MalformedBody_On200_IsNotOk()
        {
            var store = new MemoryStore { Token = "t" };
            var t = new ScriptedTransport { Handler = (m, u, b, k) => new ServerResponse(200, "<html>proxy</html>") };
            var r = Make(t, store).LoadProfileAsync().Result;
            Assert.IsFalse(r.Ok, "A 200 with an unreadable body must not look like a successful load.");
            Assert.IsNull(r.Json);
        }

        [Test]
        public void LoadProfile_NullBody_On200_IsNotOk()
        {
            var store = new MemoryStore { Token = "t" };
            var t = new ScriptedTransport { Handler = (m, u, b, k) => new ServerResponse(200, "null") };
            var r = Make(t, store).LoadProfileAsync().Result;
            Assert.IsFalse(r.Ok);
            Assert.IsNull(r.Json);
        }

        [Test]
        public void LoadProfile_NoResponseWhileSignedOut_IsStatusZero_AndNoGet()
        {
            var t = new ScriptedTransport { Handler = (m, u, b, k) => new ServerResponse(0, null) };
            var r = Make(t, new MemoryStore()).LoadProfileAsync().Result;
            Assert.AreEqual(0, r.Status);
            Assert.IsFalse(r.Ok);
            Assert.IsFalse(r.Conflict);
            foreach (var c in t.Calls) Assert.AreNotEqual("GET", c.method);
        }

        [Test]
        public void SaveProfile_FirstSave_SendsRevisionZero_AndReturnsNewRevision()
        {
            var store = new MemoryStore { Token = "t" };
            var json = SaveDataJson();
            var t = new ScriptedTransport
            {
                Handler = (m, u, b, k) => new ServerResponse(200, Envelope(false, 1, b))
            };
            var r = Make(t, store).SaveProfileAsync(json, 0).Result;
            Assert.AreEqual("PUT", t.Calls[0].method);
            Assert.AreEqual("http://x/profile?revision=0", t.Calls[0].url);
            Assert.AreEqual(json, t.Calls[0].body);
            Assert.IsTrue(r.Ok);
            Assert.IsFalse(r.Conflict);
            Assert.AreEqual(1, r.Revision);
            Assert.AreEqual(Time, r.ServerTimeUtc);
            Assert.AreEqual(json, r.Json);
        }

        [Test]
        public void SaveProfile_Conflict_IsNotOk_HasNoJson_AndReportsServerRevision()
        {
            var store = new MemoryStore { Token = "t" };
            var t = new ScriptedTransport
            {
                Handler = (m, u, b, k) => new ServerResponse(409, "{\"error\":\"Stale revision.\",\"revision\":9,\"serverTimeUtc\":\"" + Time + "\"}")
            };
            var r = Make(t, store).SaveProfileAsync("{}", 4).Result;
            Assert.IsTrue(r.Conflict);
            Assert.IsFalse(r.Ok);
            Assert.IsNull(r.Json);
            Assert.AreEqual(9, r.Revision);
        }

        [Test]
        public void SaveProfile_BadRequest_IsNotConflict()
        {
            var store = new MemoryStore { Token = "t" };
            var t = new ScriptedTransport { Handler = (m, u, b, k) => new ServerResponse(400, "{\"error\":\"Document too large.\"}") };
            var r = Make(t, store).SaveProfileAsync("{}", 1).Result;
            Assert.AreEqual(400, r.Status);
            Assert.IsFalse(r.Ok);
            Assert.IsFalse(r.Conflict);
        }

        [Test]
        public void SaveProfile_NetworkFailure_IsStatusZero()
        {
            var store = new MemoryStore { Token = "t" };
            var t = new ScriptedTransport { Handler = (m, u, b, k) => new ServerResponse(0, null) };
            var r = Make(t, store).SaveProfileAsync("{}", 1).Result;
            Assert.AreEqual(0, r.Status);
            Assert.IsFalse(r.Ok);
            Assert.IsFalse(r.Conflict);
        }

        [Test]
        public void SaveProfile_WhenSignInFails_SendsNoPut()
        {
            var t = new ScriptedTransport { Handler = (m, u, b, k) => new ServerResponse(0, null) };
            var r = Make(t, new MemoryStore()).SaveProfileAsync("{}", 1).Result;
            Assert.AreEqual(0, r.Status);
            foreach (var c in t.Calls) Assert.AreNotEqual("PUT", c.method);
        }

        [Test]
        public void SaveProfile_Unauthorized_ReSignsInAndRetriesSameBodyAndRevision()
        {
            var store = new MemoryStore { Token = "stale" };
            var json = SaveDataJson();
            var t = new ScriptedTransport
            {
                Handler = (m, u, b, k) =>
                {
                    if (u.EndsWith("/auth/anonymous")) return new ServerResponse(200, SignInBody);
                    if (k == "stale") return new ServerResponse(401, null);
                    return new ServerResponse(200, Envelope(false, 3, b));
                }
            };
            var r = Make(t, store).SaveProfileAsync(json, 2).Result;
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(3, r.Revision);
            var puts = t.Calls.FindAll(c => c.method == "PUT");
            Assert.AreEqual(2, puts.Count, "one rejected PUT, one retried PUT");
            Assert.AreEqual(json, puts[1].body);
            Assert.AreEqual("http://x/profile?revision=2", puts[1].url);
            Assert.AreEqual("fresh", puts[1].token);
        }

        // Two devices load the same revision; the first save wins, the second is rejected
        // with the server's current revision instead of silently overwriting.
        [Test]
        public void TwoDevices_SecondStaleSave_IsRejectedWith409_AndDoesNotOverwrite()
        {
            var server = new FakeProfileServer();
            var deviceA = Make(server.Transport(), new MemoryStore { Token = "a" });
            var deviceB = Make(server.Transport(), new MemoryStore { Token = "b" });

            var a = deviceA.LoadProfileAsync().Result;
            var b = deviceB.LoadProfileAsync().Result;
            Assert.AreEqual(a.Revision, b.Revision);

            var saveA = deviceA.SaveProfileAsync("{\"gold\":1}", a.Revision).Result;
            Assert.IsTrue(saveA.Ok);
            Assert.AreEqual(a.Revision + 1, saveA.Revision);

            var saveB = deviceB.SaveProfileAsync("{\"gold\":2}", b.Revision).Result;
            Assert.IsTrue(saveB.Conflict);
            Assert.AreEqual(saveA.Revision, saveB.Revision);

            var reread = deviceB.LoadProfileAsync().Result;
            Assert.AreEqual("{\"gold\":1}", reread.Json, "device A's save must survive device B's stale write");
            Assert.AreEqual(saveA.Revision, reread.Revision);
        }

        [Test]
        public void SaveData_SampleFromServerFixture_LoadsIntoSaveData()
        {
            var path = FixturePath();
            Assert.IsTrue(File.Exists(path), "Missing fixture: " + path);
            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveData>(json);
            Assert.IsNotNull(data);
            Assert.AreEqual(100, data.healthTrueMax);
            Assert.AreEqual(60, data.healthCurrentMax);
            Assert.AreEqual(250, data.gold);
            Assert.AreEqual(3, data.progressionLevel);
        }

        [Test]
        public void SaveData_SampleFromServerFixture_SurvivesJsonRoundTrip_ThroughProfileTransport()
        {
            var json = File.ReadAllText(FixturePath());
            var data = JsonUtility.FromJson<SaveData>(json);
            var reJson = JsonUtility.ToJson(data);
            var again = JsonUtility.FromJson<SaveData>(reJson);
            Assert.AreEqual(data.healthTrueMax, again.healthTrueMax);
            Assert.AreEqual(data.healthCurrentMax, again.healthCurrentMax);
            Assert.AreEqual(data.gold, again.gold);
            Assert.AreEqual(data.progressionLevel, again.progressionLevel);
            Assert.AreEqual(data.autoConsumeRevivePotions, again.autoConsumeRevivePotions);

            // The client hands the document to the server verbatim and gets it back verbatim.
            var store = new MemoryStore { Token = "t" };
            var t = new ScriptedTransport
            {
                Handler = (m, u, b, k) => m == "PUT" ? new ServerResponse(200, Envelope(false, 1, b))
                    : new ServerResponse(200, Envelope(false, 1, json))
            };
            var client = Make(t, store);
            var saved = client.SaveProfileAsync(reJson, 0).Result;
            Assert.AreEqual(reJson, saved.Json);
            var loaded = client.LoadProfileAsync().Result;
            Assert.AreEqual(json, loaded.Json);
        }

        // Minimal in-memory stand-in for the server's /profile routes (revision-checked PUT).
        class FakeProfileServer
        {
            string document;
            int revision;

            public IServerTransport Transport() => new ScriptedTransport
            {
                Handler = Handle
            };

            ServerResponse Handle(string method, string url, string body, string token)
            {
                if (url.EndsWith("/auth/anonymous")) return new ServerResponse(200, SignInBody);
                if (method == "GET")
                    return new ServerResponse(200, Envelope(revision == 0, revision, document));
                if (method == "PUT")
                {
                    var q = url.IndexOf("revision=", StringComparison.Ordinal);
                    var sent = int.Parse(url.Substring(q + "revision=".Length));
                    if (sent != revision)
                        return new ServerResponse(409, "{\"error\":\"Stale revision.\",\"revision\":" + revision + ",\"serverTimeUtc\":\"" + Time + "\"}");
                    document = body;
                    revision++;
                    return new ServerResponse(200, Envelope(false, revision, document));
                }
                return new ServerResponse(404, null);
            }
        }
    }
}
