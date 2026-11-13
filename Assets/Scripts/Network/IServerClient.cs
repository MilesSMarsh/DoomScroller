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

    public interface IServerClient
    {
        bool IsSignedIn { get; }
        string PlayerId { get; }
        Task<bool> SignInAsync();
        // Authenticated request. On 401, signs in once more and retries once, then fails cleanly.
        Task<ServerResponse> SendAsync(string method, string path, string jsonBody = null);
    }
}
