using System;
using System.Threading.Tasks;
using DoomScroller.CombatCore;

namespace DoomScroller.Network
{
    public readonly struct ServerResponse
    {
        public readonly long Status;   // HTTP status; 0 = no response (timeout / network error)
        public readonly string Body;
        public ServerResponse(long status, string body) { Status = status; Body = body; }
        public bool Ok => Status >= 200 && Status < 300;
    }

    // Raw HTTP, behind an interface so logic is testable (UnityWebRequest needs the main thread).
    public interface IServerTransport
    {
        Task<ServerResponse> SendAsync(string method, string url, string jsonBody, string bearerToken, TimeSpan timeout);
    }

    // Device-local storage for the install id and token.
    public interface IServerCredentialStore
    {
        string InstallId { get; set; }
        string Token { get; set; }
        string PlayerId { get; set; }
    }

    // Result of LoadProfile/SaveProfile. Json is the SaveData JSON (null when IsNew or on failure).
    // Status 409 = stale revision (Revision is the server's current one); 0 = no response.
    public readonly struct ProfileResponse
    {
        public readonly long Status;
        public readonly bool IsNew;
        public readonly string Json;
        public readonly int Revision;
        public readonly string ServerTimeUtc;
        public ProfileResponse(long status, bool isNew, string json, int revision, string serverTimeUtc)
        { Status = status; IsNew = isNew; Json = json; Revision = revision; ServerTimeUtc = serverTimeUtc; }
        public bool Ok => Status >= 200 && Status < 300;
        public bool Conflict => Status == 409;
    }

    // Result of CommitFight/GetPendingFight. Status 0 = no response; 400 = Error says why
    // (profile untouched); 409 = profile changed mid-fight, safe to retry with the same id.
    // HasPending is false when GET /fights/pending found nothing. Result is the server's
    // event log to replay; Revision is the profile revision after the fight's changes.
    public readonly struct FightResponse
    {
        public readonly long Status;
        public readonly bool HasPending;
        public readonly string FightId;
        public readonly string EncounterId;
        public readonly int Seed;
        public readonly bool ReviveFired;
        public readonly int Revision;
        public readonly FightResult Result;
        public readonly string Error;
        public FightResponse(long status, bool hasPending, string fightId, string encounterId, int seed, bool reviveFired,
            int revision, FightResult result, string error)
        {
            Status = status; HasPending = hasPending; FightId = fightId; EncounterId = encounterId; Seed = seed;
            ReviveFired = reviveFired; Revision = revision; Result = result; Error = error;
        }
        public bool Ok => Status >= 200 && Status < 300;
    }

    public interface IServerClient
    {
        bool IsSignedIn { get; }
        string PlayerId { get; }
        Task<bool> SignInAsync();
        // Authenticated request. On 401, signs in once more and retries once, then fails cleanly.
        Task<ServerResponse> SendAsync(string method, string path, string jsonBody = null);
        // GET /profile. IsNew = first sign-in, no stored profile yet.
        Task<ProfileResponse> LoadProfileAsync();
        // PUT /profile with the whole save JSON and the revision it was based on (0 for none).
        Task<ProfileResponse> SaveProfileAsync(string saveJson, int baseRevision);

        // POST /fights. The id is client-generated and reused on retry: the server returns
        // the stored fight instead of rolling again. reviveArmed = the AutoConsumeRevivePotions
        // setting; the server arms a Revive only if the stored profile carries one.
        // Defaults keep older test doubles compiling; ServerClient overrides them.
        Task<FightResponse> CommitFightAsync(string fightId, string encounterId, bool reviveArmed) =>
            Task.FromResult(new FightResponse(0, false, null, null, 0, false, 0, null, null));

        // GET /fights/pending: a committed fight not yet acknowledged, to replay after a reconnect.
        Task<FightResponse> GetPendingFightAsync() =>
            Task.FromResult(new FightResponse(0, false, null, null, 0, false, 0, null, null));

        // POST /fights/{id}/ack once the replay has been shown. True when the server recorded it.
        Task<bool> AcknowledgeFightAsync(string fightId) => Task.FromResult(false);
    }
}
