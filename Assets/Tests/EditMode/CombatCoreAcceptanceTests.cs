using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DoomScroller.Combat;
using DoomScroller.CombatCore;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // Acceptance checks for spec 008 (Unity-free shared combat library). The CombatCore
    // types don't exist yet, so these look them up by name via reflection instead of
    // naming them directly: the assembly keeps compiling, and each check goes green once
    // the matching piece lands. Until then the red results are the point.
    public class CombatCoreAcceptanceTests
    {
        private const string CoreNamespace = "DoomScroller.CombatCore";

        private static string CoreFolder => Path.Combine(Application.dataPath, "Scripts", "CombatCore");

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(fullName, throwOnError: false))
                .FirstOrDefault(t => t != null);
        }

        // Criterion 1: the assembly exists, has no engine references, and nothing in it
        // touches UnityEngine or the Unity-side data types (criterion 2's ban list).
        [Test]
        public void CombatCore_AsmdefExistsWithNoEngineReferences()
        {
            var asmdef = Path.Combine(CoreFolder, "DoomScroller.CombatCore.asmdef");
            Assert.IsTrue(File.Exists(asmdef), "missing " + asmdef);
            var text = File.ReadAllText(asmdef);
            StringAssert.Contains("\"noEngineReferences\": true", text);
        }

        [Test]
        public void CombatCore_SourceHasNoUnityOrScriptableObjectReferences()
        {
            Assert.IsTrue(Directory.Exists(CoreFolder), "Assets/Scripts/CombatCore does not exist yet");
            var banned = new[] { "UnityEngine", "ItemData", "PlayerLoadout", "GameSettings", "ScriptableObject" };
            var offenders = Directory.GetFiles(CoreFolder, "*.cs", SearchOption.AllDirectories)
                .Where(f => banned.Any(b => File.ReadAllText(f).Contains(b)))
                .Select(Path.GetFileName)
                .ToArray();
            Assert.IsEmpty(offenders, "CombatCore files reference banned types: " + string.Join(", ", offenders));
        }

        // Criterion 2 and 3: the plain-data input and output types exist in CombatCore and
        // can be serialized with JsonUtility.
        [TestCase("FightInput")]
        [TestCase("FightResult")]
        [TestCase("CombatBeat")]
        public void CombatCore_PlainDataTypeExistsAndIsSerializable(string typeName)
        {
            var type = FindType(CoreNamespace + "." + typeName);
            Assert.IsNotNull(type, CoreNamespace + "." + typeName + " does not exist");
            Assert.IsTrue(type.IsSerializable || type.GetCustomAttributes(typeof(SerializableAttribute), false).Length > 0,
                typeName + " must be [Serializable]");
        }

        // Criterion 4: the adapter lives in the main assembly's Combat namespace.
        [Test]
        public void FightInputBuilder_ExistsInCombatNamespace()
        {
            var type = FindType("DoomScroller.Combat.FightInputBuilder");
            Assert.IsNotNull(type, "DoomScroller.Combat.FightInputBuilder does not exist");
        }

        // Criterion 5: manual casting is gone from the simulator and settings.
        [Test]
        public void CombatSimulator_HasNoCastManualAbilityMethod()
        {
            var method = typeof(CombatSimulator).GetMethod("CastManualAbility",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(method, "CastManualAbility still exists");
        }

        [Test]
        public void GameSettings_HasNoManualAbilityCastingProperty()
        {
            var prop = typeof(GameSettings).GetProperty("ManualAbilityCasting",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(prop, "GameSettings.ManualAbilityCasting still exists");
        }

        // Criterion 5 (save compat): a save written before manual casting was removed must
        // still load. Passes now and must keep passing after the removal.
        [Test]
        public void SaveData_OldSaveWithManualAbilityCastingStillLoads()
        {
            const string oldJson = "{\"autoConsumeRevivePotions\":true,\"manualAbilityCasting\":true}";
            SaveData save = null;
            Assert.DoesNotThrow(() => save = JsonUtility.FromJson<SaveData>(oldJson));
            Assert.IsNotNull(save);
            Assert.IsTrue(save.autoConsumeRevivePotions, "other fields must survive an old save");
        }
    }
}
