using UnityEngine;

namespace DoomScroller.Network
{
    // Creates the shared ServerClient at startup and signs in once, so every later request
    // is authenticated. Runs without a scene object (RuntimeInitializeOnLoadMethod).
    public static class ServerSession
    {
        public static IServerClient Client { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static async void Start()
        {
            Client = ServerClient.CreateDefault();
            try
            {
                bool ok = await Client.SignInAsync();
                if (ok) Debug.Log($"[Server] Signed in as player {Client.PlayerId}");
                else Debug.LogWarning("[Server] Sign-in failed (is the dev server running?)");
            }
            catch (System.Exception e) { Debug.LogWarning("[Server] Sign-in error: " + e.Message); }
        }
    }
}
