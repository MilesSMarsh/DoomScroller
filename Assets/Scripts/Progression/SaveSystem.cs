using System.IO;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Pure file I/O — no game-state knowledge. ProfileController owns building/applying
    // a SaveData; this just gets it to and from disk. One fixed file (single implicit
    // save slot, no save-selection UI) at Application.persistentDataPath, which is
    // writable on both the Editor (per-project) and real iOS/Android builds.
    public static class SaveSystem
    {
        private const string FileName = "savegame.json";

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool TryReadFromDisk(out SaveData data)
        {
            data = null;

            if (!File.Exists(FilePath))
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(FilePath);
                data = JsonUtility.FromJson<SaveData>(json);
                return data != null;
            }
            catch (System.Exception e)
            {
                // A corrupt/unreadable save shouldn't hard-crash the game — fall back to
                // a fresh start the same way a first-ever launch does.
                Debug.LogError($"SaveSystem: failed to read save file, starting fresh instead. {e}");
                data = null;
                return false;
            }
        }

        // Writes to a temp file first, then swaps it in — a phone can lose power or have
        // the app killed by the OS mid-write (autosave fires on every gameplay change,
        // not just on a clean quit), and a WriteAllText straight to the real path would
        // leave a half-written, corrupt save behind if that happens partway through. The
        // temp-then-swap keeps the previous save intact until the new one has fully
        // landed on disk. Wrapped in try/catch too — a full disk or sandbox permission
        // hiccup on a real device shouldn't crash the game over something as
        // non-critical as an autosave.
        public static void WriteToDisk(SaveData data)
        {
            try
            {
                var json = JsonUtility.ToJson(data, prettyPrint: true);
                var tempPath = FilePath + ".tmp";
                File.WriteAllText(tempPath, json);

                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }

                File.Move(tempPath, FilePath);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SaveSystem: failed to write save file. {e}");
            }
        }
    }
}
