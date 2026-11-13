using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DoomScroller.Editor;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // QA coverage for docs/tasks/015-encounter-data-export.md — criteria not covered by
    // EncounterDataExportTests: the Id property, fight-type filtering, the menu/batch
    // entry points, null/empty input, ordering determinism, and the stale-data guard's
    // sensitivity. All synthetic data is built in memory; nothing is written to disk.
    public class EncounterDataExportQaTests
    {
        private static EncounterData MakeEncounter(string name, EncounterType type, int hp = 80,
            int dmg = 10, float speed = 0.8f, DamageTypeModifier[] mods = null)
        {
            var e = ScriptableObject.CreateInstance<EncounterData>();
            e.name = name;
            e.type = type;
            e.enemyHealth = hp;
            e.enemyAttackDamage = dmg;
            e.enemyAttackSpeed = speed;
            e.damageTypeModifiers = mods;
            return e;
        }

        private static void Destroy(IEnumerable<EncounterData> list)
        {
            foreach (var e in list)
            {
                if (e != null) UnityEngine.Object.DestroyImmediate(e);
            }
        }

        [Test]
        public void Id_EqualsAssetName()
        {
            var e = MakeEncounter("Enemy_Wolf", EncounterType.Enemy);
            try
            {
                Assert.AreEqual("Enemy_Wolf", e.Id);
            }
            finally
            {
                Destroy(new[] { e });
            }
        }

        [Test]
        public void Id_IsNotASerializedField()
        {
            // Criterion: "no new serialized field". Id must be a computed property only.
            var fields = typeof(EncounterData).GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            Assert.IsFalse(fields.Any(f => string.Equals(f.Name, "id", StringComparison.OrdinalIgnoreCase)),
                "EncounterData must not gain an id field; Id is derived from the asset name.");
            var idProp = typeof(EncounterData).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(idProp, "EncounterData.Id property missing.");
            Assert.IsFalse(idProp.CanWrite, "Id must be read-only.");
        }

        [Test]
        public void IsFight_TrueOnlyForEnemyAndAmbush()
        {
            var all = (EncounterType[])Enum.GetValues(typeof(EncounterType));
            foreach (var type in all)
            {
                var e = MakeEncounter("X", type);
                try
                {
                    bool expected = type == EncounterType.Enemy || type == EncounterType.Ambush;
                    Assert.AreEqual(expected, EncounterDataExporter.IsFight(e), "IsFight wrong for " + type);
                }
                finally
                {
                    Destroy(new[] { e });
                }
            }
        }

        [Test]
        public void Build_ExcludesEveryNonFightType()
        {
            var list = new List<EncounterData>
            {
                MakeEncounter("Treasure_A", EncounterType.Treasure),
                MakeEncounter("Dungeon_A", EncounterType.Dungeon),
                MakeEncounter("Tavern_A", EncounterType.Tavern),
                MakeEncounter("Resource_A", EncounterType.Resource),
                MakeEncounter("Landing_A", EncounterType.Landing),
                MakeEncounter("Merchant_A", EncounterType.WanderingMerchant),
                MakeEncounter("Enemy_A", EncounterType.Enemy),
                MakeEncounter("Ambush_A", EncounterType.Ambush),
            };
            try
            {
                var export = EncounterDataExporter.Build(list);
                CollectionAssert.AreEquivalent(new[] { "Enemy_A", "Ambush_A" },
                    export.encounters.Select(e => e.id));
            }
            finally
            {
                Destroy(list);
            }
        }

        [Test]
        public void Build_NullAndEmptyInput_YieldEmptyEncounters()
        {
            Assert.IsNotNull(EncounterDataExporter.Build(Array.Empty<EncounterData>()).encounters);
            Assert.AreEqual(0, EncounterDataExporter.Build(Array.Empty<EncounterData>()).encounters.Length);

            var withNull = new EncounterData[] { null };
            Assert.AreEqual(0, EncounterDataExporter.Build(withNull).encounters.Length);
        }

        [Test]
        public void Build_NullDamageTypeModifiers_ExportsEmptyArray()
        {
            var e = MakeEncounter("Enemy_NoMods", EncounterType.Enemy, mods: null);
            try
            {
                var entry = EncounterDataExporter.Build(new[] { e }).encounters.Single();
                Assert.IsNotNull(entry.damageTypeModifiers);
                Assert.AreEqual(0, entry.damageTypeModifiers.Length);
            }
            finally
            {
                Destroy(new[] { e });
            }
        }

        [Test]
        public void Build_CopiesEveryField_FromSyntheticAsset()
        {
            var e = MakeEncounter("Ambush_Synth", EncounterType.Ambush, hp: 42, dmg: 7, speed: 1.25f,
                mods: new[]
                {
                    new DamageTypeModifier { type = DamageType.Pierce, multiplier = 1.5f },
                    new DamageTypeModifier { type = DamageType.Elemental, multiplier = 0.25f },
                });
            try
            {
                var entry = EncounterDataExporter.Build(new[] { e }).encounters.Single();
                Assert.AreEqual("Ambush_Synth", entry.id);
                Assert.AreEqual("Ambush", entry.type);
                Assert.AreEqual(42, entry.enemyHealth);
                Assert.AreEqual(7, entry.enemyAttackDamage);
                Assert.AreEqual(1.25f, entry.enemyAttackSpeed);
                Assert.AreEqual(2, entry.damageTypeModifiers.Length);
                Assert.AreEqual("Pierce", entry.damageTypeModifiers[0].damageType);
                Assert.AreEqual(1.5f, entry.damageTypeModifiers[0].multiplier);
                Assert.AreEqual("Elemental", entry.damageTypeModifiers[1].damageType);
                Assert.AreEqual(0.25f, entry.damageTypeModifiers[1].multiplier);
            }
            finally
            {
                Destroy(new[] { e });
            }
        }

        [Test]
        public void Build_IsOrderIndependent_ForSeededShuffles()
        {
            var rng = new System.Random(15);
            var names = new[] { "Enemy_Zeta", "Enemy_Alpha", "Ambush_Mid", "Enemy_Beta", "Ambush_Alpha" };
            var assets = names.Select((n, i) => MakeEncounter(n,
                i % 2 == 0 ? EncounterType.Enemy : EncounterType.Ambush, hp: 50 + i)).ToList();
            try
            {
                var reference = EncounterDataExporter.ToJson(assets);
                for (int round = 0; round < 25; round++)
                {
                    var shuffled = assets.OrderBy(_ => rng.Next()).ToList();
                    Assert.AreEqual(reference, EncounterDataExporter.ToJson(shuffled),
                        "Export must not depend on Resources load order (round " + round + ").");
                }

                var ids = EncounterDataExporter.Build(assets).encounters.Select(e => e.id).ToArray();
                CollectionAssert.AreEqual(ids.OrderBy(x => x, StringComparer.Ordinal).ToArray(), ids);
            }
            finally
            {
                Destroy(assets);
            }
        }

        [Test]
        public void ToJson_RoundTrips_ThroughJsonUtility()
        {
            var a = MakeEncounter("Enemy_RoundTrip", EncounterType.Enemy, hp: 66, dmg: 9, speed: 0.73f,
                mods: new[] { new DamageTypeModifier { type = DamageType.Blunt, multiplier = 1.15f } });
            try
            {
                var json = EncounterDataExporter.ToJson(new[] { a });
                var parsed = JsonUtility.FromJson<EncounterDataExporter.ExportFile>(json);
                var entry = parsed.encounters.Single();
                Assert.AreEqual("Enemy_RoundTrip", entry.id);
                Assert.AreEqual(66, entry.enemyHealth);
                Assert.AreEqual(9, entry.enemyAttackDamage);
                Assert.AreEqual(0.73f, entry.enemyAttackSpeed, 1e-6f);
                Assert.AreEqual("Blunt", entry.damageTypeModifiers.Single().damageType);
                Assert.AreEqual(1.15f, entry.damageTypeModifiers.Single().multiplier, 1e-6f);
            }
            finally
            {
                Destroy(new[] { a });
            }
        }

        [Test]
        public void StaleGuard_ChangingAnyExportedValue_ChangesJson()
        {
            // The committed-file test only has teeth if the export actually reflects each field.
            var baseline = MakeEncounter("Enemy_Guard", EncounterType.Enemy, hp: 80, dmg: 10, speed: 0.8f,
                mods: new[] { new DamageTypeModifier { type = DamageType.Slash, multiplier = 1f } });
            var variants = new List<EncounterData>();
            try
            {
                string Json(EncounterData e) => EncounterDataExporter.ToJson(new[] { e });
                string original = Json(baseline);

                variants.Add(MakeEncounter("Enemy_Guard", EncounterType.Enemy, hp: 81, dmg: 10, speed: 0.8f,
                    mods: baseline.damageTypeModifiers));
                variants.Add(MakeEncounter("Enemy_Guard", EncounterType.Enemy, hp: 80, dmg: 11, speed: 0.8f,
                    mods: baseline.damageTypeModifiers));
                variants.Add(MakeEncounter("Enemy_Guard", EncounterType.Enemy, hp: 80, dmg: 10, speed: 0.9f,
                    mods: baseline.damageTypeModifiers));
                variants.Add(MakeEncounter("Enemy_Guard", EncounterType.Enemy, hp: 80, dmg: 10, speed: 0.8f,
                    mods: new[] { new DamageTypeModifier { type = DamageType.Slash, multiplier = 1.5f } }));
                variants.Add(MakeEncounter("Enemy_Guard", EncounterType.Enemy, hp: 80, dmg: 10, speed: 0.8f,
                    mods: new[] { new DamageTypeModifier { type = DamageType.Pierce, multiplier = 1f } }));
                variants.Add(MakeEncounter("Enemy_Renamed", EncounterType.Enemy, hp: 80, dmg: 10, speed: 0.8f,
                    mods: baseline.damageTypeModifiers));
                variants.Add(MakeEncounter("Enemy_Guard", EncounterType.Ambush, hp: 80, dmg: 10, speed: 0.8f,
                    mods: baseline.damageTypeModifiers));

                foreach (var v in variants)
                {
                    Assert.AreNotEqual(original, Json(v), "Changing a field must change the export.");
                }

                Assert.AreEqual(original, Json(baseline), "Export must be stable for identical input.");
            }
            finally
            {
                Destroy(variants);
                Destroy(new[] { baseline });
            }
        }

        [Test]
        public void ResourcesEncounterNames_AreUnique()
        {
            var names = Resources.LoadAll<EncounterData>(EncounterDataExporter.ResourcesFolder)
                .Where(EncounterDataExporter.IsFight)
                .Select(e => e.Id)
                .ToArray();
            Assert.IsNotEmpty(names);
            Assert.AreEqual(names.Length, names.Distinct().Count(), "Duplicate fight encounter ids in Resources.");
        }

        [Test]
        public void MenuItemAndBatchEntryPoints_Exist()
        {
            var exporterType = typeof(EncounterDataExporter);

            var batch = exporterType.GetMethod("ExportBatch", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(batch, "Batch-mode method ExportBatch missing.");
            Assert.AreEqual(0, batch.GetParameters().Length, "ExportBatch must take no parameters for -executeMethod.");

            var menu = exporterType.GetMethod("ExportMenu", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(menu, "Menu method ExportMenu missing.");
            var attr = menu.GetCustomAttribute<UnityEditor.MenuItem>();
            Assert.IsNotNull(attr, "ExportMenu must carry [MenuItem].");
            Assert.AreEqual("DoomScroller/Export Encounter Data", attr.menuItem);
        }

        [Test]
        public void CommittedJson_ContainsOnlyFightEntries_WithUniqueIds()
        {
            var json = EncounterDataExporter.ExportCurrentAssets();
            var export = JsonUtility.FromJson<EncounterDataExporter.ExportFile>(json);
            Assert.IsNotEmpty(export.encounters);
            Assert.IsTrue(export.encounters.All(e => e.type == "Enemy" || e.type == "Ambush"),
                "Only Enemy/Ambush may appear in encounters.json (dungeon rooms and resource nodes stay device-resolved).");
            Assert.AreEqual(export.encounters.Length, export.encounters.Select(e => e.id).Distinct().Count());
        }
    }
}
