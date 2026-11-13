using UnityEngine;

namespace DoomScroller.Network
{
    public class PlayerPrefsCredentialStore : IServerCredentialStore
    {
        const string InstallKey = "ds.server.installId";
        const string TokenKey = "ds.server.token";
        const string PlayerKey = "ds.server.playerId";

        public string InstallId { get => Get(InstallKey); set => Set(InstallKey, value); }
        public string Token { get => Get(TokenKey); set => Set(TokenKey, value); }
        public string PlayerId { get => Get(PlayerKey); set => Set(PlayerKey, value); }

        static string Get(string k) => PlayerPrefs.GetString(k, null);
        static void Set(string k, string v)
        {
            if (string.IsNullOrEmpty(v)) PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetString(k, v);
            PlayerPrefs.Save();
        }
    }
}
