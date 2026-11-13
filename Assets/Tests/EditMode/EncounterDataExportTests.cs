using System.IO;
using System.Linq;
using DoomScroller.Editor;
using DoomScroller.Feed;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests
{
    public class EncounterDataExportTests
    {
        static EncounterData[] Fights() =>
            Resources.LoadAll<EncounterData>(EncounterDataExporter.ResourcesFolder)
                .Where(EncounterDataExporter.IsFight).ToArray();

        [Test]
        public void Export_HasOneEntryPerFightEncounter_WithAssetValues()
        {
            var fights = Fights();
            Assert.IsNotEmpty(fights);
            var export = EncounterDataExporter.Build(
                Resources.LoadAll<EncounterData>(EncounterDataExporter.ResourcesFolder));

            Assert.AreEqual(fights.Length, export.encounters.Length);
            Assert.AreEqual(export.encounters.Length, export.encounters.Select(e => e.id).Distinct().Count());
            foreach (var asset in fights)
            {
                var e = export.encounters.Single(x => x.id == asset.name);
                Assert.AreEqual(asset.Id, e.id);
                Assert.AreEqual(asset.type.ToString(), e.type);
                Assert.AreEqual(asset.enemyHealth, e.enemyHealth);
                Assert.AreEqual(asset.enemyAttackDamage, e.enemyAttackDamage);
                Assert.AreEqual(asset.enemyAttackSpeed, e.enemyAttackSpeed);
                var mods = asset.damageTypeModifiers ?? new DamageTypeModifier[0];
                Assert.AreEqual(mods.Length, e.damageTypeModifiers.Length);
                for (int i = 0; i < mods.Length; i++)
                {
                    Assert.AreEqual(mods[i].type.ToString(), e.damageTypeModifiers[i].damageType);
                    Assert.AreEqual(mods[i].multiplier, e.damageTypeModifiers[i].multiplier);
                }
            }
        }

        [Test]
        public void CommittedEncountersJson_MatchesFreshExport()
        {
            var path = EncounterDataExporter.OutputPath;
            Assert.IsTrue(File.Exists(path),
                "Missing " + EncounterDataExporter.RelativeOutputPath + " — run DoomScroller > Export Encounter Data.");
            var committed = File.ReadAllText(path).Replace("\r\n", "\n");
            Assert.AreEqual(EncounterDataExporter.ExportCurrentAssets(), committed,
                "encounters.json is stale — run DoomScroller > Export Encounter Data and commit.");
        }
    }
}
