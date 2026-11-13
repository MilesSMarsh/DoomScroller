using System;
using System.Threading.Tasks;

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
    }
}
