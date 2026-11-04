using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // Acceptance coverage for docs/tasks/006-resources-naming-cleanup.md: the ten renamed
    // Resources assets keep their GUIDs, carry their new file names, keep their identity
    // fields (saves key off those), the three enemy cards carry the approved descriptions,
    // and no asset reference in the project is left dangling.
    public class ResourcesNamingTests
    {
        public struct Rename
        {
            public string OldRelPath;   // relative to Assets/Resources, no extension
            public string NewRelPath;   // relative to Assets/Resources, no extension
            public string Guid;         // recorded from the .meta before the rename
            public System.Type AssetType;

            public override string ToString() => NewRelPath;
        }

        private static readonly Rename[] Renames =
        {
            new Rename { OldRelPath = "Abilities/Passive_New", NewRelPath = "Abilities/Passive_DamageUp", Guid = "3c1f34edd4daf1a0f8aa4666a83c1088", AssetType = typeof(PassiveDefinition) },
            new Rename { OldRelPath = "Abilities/Passive_New 1", NewRelPath = "Abilities/Passive_WeakenEnemy", Guid = "96e6b2e96b01b0b66852578e3a9942af", AssetType = typeof(PassiveDefinition) },
            new Rename { OldRelPath = "Abilities/Passive_New 2", NewRelPath = "Abilities/Passive_IncreaseAttackSpeed", Guid = "51c089a1f4a84efe5bc2d0a643ab925f", AssetType = typeof(PassiveDefinition) },
            new Rename { OldRelPath = "Encounters/Wolf", NewRelPath = "Encounters/Enemy_Wolf", Guid = "c56531fad4e2993b5b405c6a7ed78878", AssetType = typeof(EncounterData) },
            new Rename { OldRelPath = "WeaponDefinitions/Stone Sword", NewRelPath = "WeaponDefinitions/StoneSword", Guid = "fd1b4aa393510a943a82c27e208dfba4", AssetType = typeof(WeaponDefinition) },
            new Rename { OldRelPath = "WeaponDefinitions/Wooden Sword", NewRelPath = "WeaponDefinitions/WoodenSword", Guid = "1cc64e9a05244643ea6a4dc8b088eeb7", AssetType = typeof(WeaponDefinition) },
            new Rename { OldRelPath = "EquipmentDefinitions/Leather Helmet", NewRelPath = "EquipmentDefinitions/LeatherHelmet", Guid = "54da90afdcdf24529a0f97d192b4c386", AssetType = typeof(EquipmentDefinition) },
            new Rename { OldRelPath = "RecipeDefinitions/Recipe_Leather Helmet", NewRelPath = "RecipeDefinitions/Recipe_LeatherHelmet", Guid = "2d335f28e6762bbee83938b1d5d81128", AssetType = typeof(RecipeDefinition) },
            new Rename { OldRelPath = "Encounters/Enemy_Standard", NewRelPath = "Encounters/Enemy_RogueSlime", Guid = "8be2bb5911ac42e19cb34e8748102e74", AssetType = typeof(EncounterData) },
            new Rename { OldRelPath = "Encounters/Enemy_Hard", NewRelPath = "Encounters/Enemy_BonewroughtSentinel", Guid = "62f0ddde53814707a67df18b9aaf254b", AssetType = typeof(EncounterData) },
        };

        private static string AssetsRoot => Application.dataPath;
        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        private static string ResourcesFile(string relPath, string extension) =>
            Path.Combine(AssetsRoot, "Resources", relPath.Replace('/', Path.DirectorySeparatorChar) + extension);

        private static string ReadMetaGuid(string metaPath)
        {
            foreach (var line in File.ReadAllLines(metaPath))
            {
                if (line.StartsWith("guid:")) return line.Substring("guid:".Length).Trim();
            }

            return null;
        }

        // ---- Renames ----

        [TestCaseSource(nameof(Renames))]
        public void Rename_OldAssetAndMeta_NoLongerExist(Rename rename)
        {
            Assert.IsFalse(File.Exists(ResourcesFile(rename.OldRelPath, ".asset")),
                $"old asset still present: {rename.OldRelPath}.asset");
            Assert.IsFalse(File.Exists(ResourcesFile(rename.OldRelPath, ".asset.meta")),
                $"old meta still present: {rename.OldRelPath}.asset.meta");
        }

        [TestCaseSource(nameof(Renames))]
        public void Rename_NewAssetAndMeta_ExistWithOriginalGuid(Rename rename)
        {
            var assetPath = ResourcesFile(rename.NewRelPath, ".asset");
            var metaPath = ResourcesFile(rename.NewRelPath, ".asset.meta");

            Assert.IsTrue(File.Exists(assetPath), $"missing asset: {rename.NewRelPath}.asset");
            Assert.IsTrue(File.Exists(metaPath), $"missing meta: {rename.NewRelPath}.asset.meta");
            Assert.AreEqual(rename.Guid, ReadMetaGuid(metaPath),
                $"GUID changed for {rename.NewRelPath}; a rename through Unity must keep it");
        }

        [TestCaseSource(nameof(Renames))]
        public void Rename_FileNameHasNoSpaces(Rename rename)
        {
            var fileName = Path.GetFileName(rename.NewRelPath);
            Assert.IsFalse(fileName.Contains(" "), $"file name has a space: {fileName}");
        }

        [TestCaseSource(nameof(Renames))]
        public void Rename_LoadsByNewPath_WithMatchingObjectName(Rename rename)
        {
            var loaded = Resources.Load(rename.NewRelPath, rename.AssetType);

            Assert.IsNotNull(loaded, $"Resources.Load failed for {rename.NewRelPath}");
            Assert.AreEqual(Path.GetFileName(rename.NewRelPath), loaded.name,
                "m_Name should match the new file name");
        }

        [TestCaseSource(nameof(Renames))]
        public void Rename_LoadsByOldPath_NoLongerResolves(Rename rename)
        {
            var loaded = Resources.Load(rename.OldRelPath, rename.AssetType);
            Assert.IsNull(loaded, $"old path still resolves: {rename.OldRelPath}");
        }

        // ---- Identity fields (saves key off these; must be unchanged) ----

        [Test]
        public void Identity_PassiveNames_Unchanged()
        {
            AssertPassiveName("Abilities/Passive_DamageUp", "Damage Up");
            AssertPassiveName("Abilities/Passive_WeakenEnemy", "Weaken Enemy");
            AssertPassiveName("Abilities/Passive_IncreaseAttackSpeed", "Increase Attack Speed");
        }

        [Test]
        public void Identity_EncounterTitles_Unchanged()
        {
            Assert.AreEqual("Wolf", LoadOrFail<EncounterData>("Encounters/Enemy_Wolf").title);
            Assert.AreEqual("Rogue Slime", LoadOrFail<EncounterData>("Encounters/Enemy_RogueSlime").title);
            Assert.AreEqual("Bonewrought Sentinel", LoadOrFail<EncounterData>("Encounters/Enemy_BonewroughtSentinel").title);
        }

        [Test]
        public void Identity_WeaponNames_Unchanged()
        {
            Assert.AreEqual("Stone Sword", LoadOrFail<WeaponDefinition>("WeaponDefinitions/StoneSword").weaponName);
            Assert.AreEqual("Wooden Sword", LoadOrFail<WeaponDefinition>("WeaponDefinitions/WoodenSword").weaponName);
        }

        [Test]
        public void Identity_EquipmentName_Unchanged()
        {
            Assert.AreEqual("Leather Helmet", LoadOrFail<EquipmentDefinition>("EquipmentDefinitions/LeatherHelmet").equipmentName);
        }

        [Test]
        public void Identity_RecipeId_Unchanged()
        {
            Assert.AreEqual("86500eae-b76c-414e-bfd8-c04f57730d10",
                LoadOrFail<RecipeDefinition>("RecipeDefinitions/Recipe_LeatherHelmet").recipeId);
        }

        private static void AssertPassiveName(string relPath, string expected)
        {
            var def = LoadOrFail<PassiveDefinition>(relPath);
            Assert.IsNotNull(def.effect, $"{relPath} has no effect block");
            Assert.AreEqual(expected, def.effect.passiveName, $"passiveName changed on {relPath}");
        }

        private static T LoadOrFail<T>(string relPath) where T : Object
        {
            var loaded = Resources.Load<T>(relPath);
            Assert.IsNotNull(loaded, $"Resources.Load<{typeof(T).Name}> failed for {relPath}");
            return loaded;
        }

        // ---- Enemy card descriptions (approved by Miles 2026-11-03) ----

        [Test]
        public void Description_EnemyWolf_IsApprovedText()
        {
            Assert.AreEqual("A lean grey wolf, hungry and hunting alone.",
                LoadOrFail<EncounterData>("Encounters/Enemy_Wolf").description);
        }

        [Test]
        public void Description_EnemyRogueSlime_IsApprovedText()
        {
            Assert.AreEqual("A wandering ooze that dissolves whatever it swallows.",
                LoadOrFail<EncounterData>("Encounters/Enemy_RogueSlime").description);
        }

        [Test]
        public void Description_EnemyBonewroughtSentinel_IsApprovedText()
        {
            Assert.AreEqual("A guardian of fused bone that never leaves its post.",
                LoadOrFail<EncounterData>("Encounters/Enemy_BonewroughtSentinel").description);
        }

        [Test]
        public void StaleOldPaths_NotReferencedByScriptsOrTests()
        {
            // The three test files named in the spec now use the new paths. No script or
            // test should still load an old path literal (this file is excluded: it lists them).
            var oldPaths = Renames.Select(r => r.OldRelPath).ToArray();
            var offenders = new List<string>();
            foreach (var root in new[] { "Scripts", "Tests" })
            {
                var dir = Path.Combine(AssetsRoot, root);
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(file) == "ResourcesNamingTests.cs") continue;
                    var text = File.ReadAllText(file);
                    foreach (var old in oldPaths)
                    {
                        if (text.Contains(old)) offenders.Add($"{Path.GetFileName(file)} -> {old}");
                    }
                }
            }

            Assert.IsEmpty(offenders, "old asset paths still referenced: " + string.Join("; ", offenders));
        }

        // ---- Dangling GUIDs ----

        [Test]
        public void References_AllAssetGuids_ResolveToAProjectOrPackageMeta()
        {
            // Resolution sources: project .meta files (Assets/, Packages/) plus the package
            // cache Unity resolves package-owned GUIDs from (e.g. UGUI, URP).
            var known = new HashSet<string>();
            CollectMetaGuids(AssetsRoot, known);
            CollectMetaGuids(Path.Combine(ProjectRoot, "Packages"), known);
            CollectMetaGuids(Path.Combine(ProjectRoot, "Library", "PackageCache"), known);

            var dangling = new Dictionary<string, List<string>>();
            var refPattern = new Regex(@"guid: ([0-9a-f]{32})");
            // Scope (Miles, task 006): authored content only — Resources data, scenes, prefabs.
            // Unity-generated settings (e.g. Assets/Settings/Renderer2D.asset, which carries
            // URP debug-resource GUIDs the installed package lacks) are out of scope.
            var resourcesRoot = Path.Combine(AssetsRoot, "Resources");
            var scenesRoot = Path.Combine(AssetsRoot, "Scenes");
            var files = Directory.GetFiles(AssetsRoot, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".prefab")
                    || (f.EndsWith(".asset") && f.StartsWith(resourcesRoot))
                    || (f.EndsWith(".unity") && f.StartsWith(scenesRoot)));

            foreach (var file in files)
            {
                foreach (Match match in refPattern.Matches(File.ReadAllText(file)))
                {
                    var guid = match.Groups[1].Value;
                    if (IsBuiltInGuid(guid) || known.Contains(guid)) continue;

                    if (!dangling.TryGetValue(guid, out var users))
                    {
                        users = new List<string>();
                        dangling[guid] = users;
                    }

                    var rel = file.Substring(ProjectRoot.Length + 1).Replace('\\', '/');
                    if (!users.Contains(rel)) users.Add(rel);
                }
            }

            var report = string.Join("; ", dangling.Select(kv => $"{kv.Key} <- {string.Join(",", kv.Value)}"));
            Assert.IsEmpty(dangling, $"{dangling.Count} dangling GUID(s): {report}");
        }

        private static void CollectMetaGuids(string root, HashSet<string> into)
        {
            if (!Directory.Exists(root)) return;
            foreach (var meta in Directory.GetFiles(root, "*.meta", SearchOption.AllDirectories))
            {
                var guid = ReadMetaGuid(meta);
                if (!string.IsNullOrEmpty(guid)) into.Add(guid);
            }
        }

        private static bool IsBuiltInGuid(string guid) =>
            guid == "00000000000000000000000000000000"
            || guid == "0000000000000000e000000000000000"
            || guid.StartsWith("f000000000000000");
    }
}
