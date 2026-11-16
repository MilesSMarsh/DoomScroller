using System;
using System.Threading.Tasks;
using DoomScroller.CombatCore;
using UnityEngine;

namespace DoomScroller.Network
{
    public class ServerClient : IServerClient
    {
        [Serializable] class SignInRequest { public string installId; }
        [Serializable] class SignInResponse { public string playerId; public string token; }

        readonly IServerTransport transport;
        readonly IServerCredentialStore store;
        readonly string baseUrl;
        readonly TimeSpan timeout;

        public ServerClient(IServerTransport transport, IServerCredentialStore store, string baseUrl, TimeSpan timeout)
        {
            this.transport = transport;
            this.store = store;
            this.baseUrl = baseUrl.TrimEnd('/');
            this.timeout = timeout;
        }

        public bool IsSignedIn => !string.IsNullOrEmpty(store.Token);
        public string PlayerId => store.PlayerId;

        public async Task<bool> SignInAsync()
        {
            if (string.IsNullOrEmpty(store.InstallId)) store.InstallId = Guid.NewGuid().ToString();
            var body = JsonUtility.ToJson(new SignInRequest { installId = store.InstallId });
            var res = await transport.SendAsync("POST", baseUrl + "/auth/anonymous", body, null, timeout);
            if (!res.Ok) return false;
            SignInResponse parsed;
            try { parsed = JsonUtility.FromJson<SignInResponse>(res.Body); }
            catch (ArgumentException) { return false; }
            if (parsed == null || string.IsNullOrEmpty(parsed.token)) return false;
            store.Token = parsed.token;
            store.PlayerId = parsed.playerId;
            return true;
        }

        public async Task<ServerResponse> SendAsync(string method, string path, string jsonBody = null)
        {
            if (!IsSignedIn && !await SignInAsync()) return new ServerResponse(0, null);
            var res = await transport.SendAsync(method, baseUrl + path, jsonBody, store.Token, timeout);
            if (res.Status != 401) return res;

            // Token rejected: one re-sign-in and one retry, then give up.
            store.Token = null;
            if (!await SignInAsync()) return res;
            return await transport.SendAsync(method, baseUrl + path, jsonBody, store.Token, timeout);
        }

        [Serializable]
        class ProfileEnvelope { public bool isNew; public int revision; public string serverTimeUtc; public string profile; }

        public async Task<ProfileResponse> LoadProfileAsync() =>
            ParseProfile(await SendAsync("GET", "/profile"));

        public async Task<ProfileResponse> SaveProfileAsync(string saveJson, int baseRevision) =>
            ParseProfile(await SendAsync("PUT", "/profile?revision=" + baseRevision, saveJson));

        static ProfileResponse ParseProfile(ServerResponse res)
        {
            if (res.Status == 0 || string.IsNullOrEmpty(res.Body)) return new ProfileResponse(res.Status, false, null, 0, null);
            ProfileEnvelope env;
            try { env = JsonUtility.FromJson<ProfileEnvelope>(res.Body); }
            catch (ArgumentException) { return new ProfileResponse(res.Ok ? 0 : res.Status, false, null, 0, null); }
            if (env == null) return new ProfileResponse(res.Ok ? 0 : res.Status, false, null, 0, null);
            var json = string.IsNullOrEmpty(env.profile) ? null : env.profile;
            return new ProfileResponse(res.Status, env.isNew, json, env.revision, env.serverTimeUtc);
        }

        [Serializable]
        class FightRequest { public string fightId; public string encounterId; public bool reviveArmed; }

        [Serializable]
        class FightEnvelope
        {
            public bool hasPending; public string fightId; public string encounterId; public int seed;
            public bool reviveFired; public int revision; public FightResult result; public string error;
        }

        public async Task<FightResponse> CommitFightAsync(string fightId, string encounterId, bool reviveArmed)
        {
            var body = JsonUtility.ToJson(new FightRequest { fightId = fightId, encounterId = encounterId, reviveArmed = reviveArmed });
            return ParseFight(await SendAsync("POST", "/fights", body));
        }

        public async Task<FightResponse> GetPendingFightAsync() => ParseFight(await SendAsync("GET", "/fights/pending"));

        public async Task<bool> AcknowledgeFightAsync(string fightId) =>
            (await SendAsync("POST", "/fights/" + Uri.EscapeDataString(fightId) + "/ack")).Ok;

        static FightResponse ParseFight(ServerResponse res)
        {
            if (res.Status == 0 || string.IsNullOrEmpty(res.Body)) return new FightResponse(res.Status, false, null, null, 0, false, 0, null, null);
            FightEnvelope env;
            try { env = JsonUtility.FromJson<FightEnvelope>(res.Body); }
            catch (ArgumentException) { env = null; }
            if (env == null) return new FightResponse(res.Ok ? 0 : res.Status, false, null, null, 0, false, 0, null, null);
            return new FightResponse(res.Status, env.hasPending, env.fightId, env.encounterId, env.seed, env.reviveFired,
                env.revision, env.hasPending ? env.result : null, env.error);
        }

        public static ServerClient CreateDefault()
        {
            var settings = ServerSettings.Load();
            return new ServerClient(new UnityWebRequestTransport(), new PlayerPrefsCredentialStore(),
                settings.BaseUrl, TimeSpan.FromSeconds(settings.TimeoutSeconds));
        }
    }
}
