using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace DoomScroller.Network
{
    // Must be called from the main thread (UnityWebRequest). Awaiting from the Unity
    // synchronization context keeps continuations on the main thread.
    public class UnityWebRequestTransport : IServerTransport
    {
        public async Task<ServerResponse> SendAsync(string method, string url, string jsonBody, string bearerToken, TimeSpan timeout)
        {
            using (var req = new UnityWebRequest(url, method))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                if (jsonBody != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                if (!string.IsNullOrEmpty(bearerToken)) req.SetRequestHeader("Authorization", "Bearer " + bearerToken);
                req.timeout = Math.Max(1, (int)timeout.TotalSeconds);

                var op = req.SendWebRequest();
                var tcs = new TaskCompletionSource<bool>();
                op.completed += _ => tcs.TrySetResult(true);
                await tcs.Task;

                // responseCode is 0 on timeout / connection failure.
                return new ServerResponse(req.responseCode, req.downloadHandler.text);
            }
        }
    }
}
