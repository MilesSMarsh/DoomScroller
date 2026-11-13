using UnityEngine;

namespace DoomScroller.Network
{
    // Base URL and timeout live in an asset (Resources/ServerSettings), never hardcoded in
    // logic. If the asset is missing, Load() falls back to the local dev server.
    [CreateAssetMenu(menuName = "DoomScroller/Server Settings", fileName = "ServerSettings")]
    public class ServerSettings : ScriptableObject
    {
        public const string ResourcePath = "ServerSettings";

        [SerializeField] string baseUrl = "http://localhost:8088";
        [SerializeField] int timeoutSeconds = 10;

        public string BaseUrl => baseUrl;
        public int TimeoutSeconds => timeoutSeconds;

        public static ServerSettings Load()
        {
            var s = Resources.Load<ServerSettings>(ResourcePath);
            return s != null ? s : CreateInstance<ServerSettings>();
        }
    }
}
