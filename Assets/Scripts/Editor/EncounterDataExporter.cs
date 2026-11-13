using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoomScroller.Feed;
using UnityEditor;
using UnityEngine;

namespace DoomScroller.Editor
{
    // Writes the fight-type encounters' combat stats to Server/data/encounters.json so the
    // server reads the same authored assets the game does.
    public static class EncounterDataExporter
    {
        public const string ResourcesFolder = "Encounters";
        public const string RelativeOutputPath = "Server/data/encounters.json";

        [Serializable]
        public class ModifierEntry
        {
            public string damageType;
            public float multiplier;
        }

        [Serializable]
        public class EncounterEntry
        {
            public string id;
            public string type;
            public int enemyHealth;
            public int enemyAttackDamage;
            public float enemyAttackSpeed;
            public ModifierEntry[] damageTypeModifiers;
        }

        [Serializable]
        public class ExportFile
        {
            public EncounterEntry[] encounters;
        }

        public static bool IsFight(EncounterData e) =>
            e.type == EncounterType.Enemy || e.type == EncounterType.Ambush;

        public static ExportFile Build(IEnumerable<EncounterData> all)
        {
            var entries = all
                .Where(e => e != null && IsFight(e))
                .OrderBy(e => e.Id, StringComparer.Ordinal)
                .Select(e => new EncounterEntry
                {
                    id = e.Id,
                    type = e.type.ToString(),
                    enemyHealth = e.enemyHealth,
                    enemyAttackDamage = e.enemyAttackDamage,
                    enemyAttackSpeed = e.enemyAttackSpeed,
                    damageTypeModifiers = (e.damageTypeModifiers ?? new DamageTypeModifier[0])
                        .Select(m => new ModifierEntry { damageType = m.type.ToString(), multiplier = m.multiplier })
                        .ToArray(),
                })
                .ToArray();
            return new ExportFile { encounters = entries };
        }

        public static string ToJson(IEnumerable<EncounterData> all) =>
            JsonUtility.ToJson(Build(all), true) + "\n";

        public static string ExportCurrentAssets() =>
            ToJson(Resources.LoadAll<EncounterData>(ResourcesFolder));

        public static string OutputPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", RelativeOutputPath));

        public static void WriteFile()
        {
            var path = OutputPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, ExportCurrentAssets());
            Debug.Log("Exported encounters to " + path);
        }

        [MenuItem("DoomScroller/Export Encounter Data")]
        public static void ExportMenu() => WriteFile();

        // Batch: Unity -batchmode -quit -executeMethod DoomScroller.Editor.EncounterDataExporter.ExportBatch
        public static void ExportBatch()
        {
            try { WriteFile(); }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }
    }
}
