using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // Regression coverage for the bug report: icon art drawn for Wood never showed on
    // already-owned copies, because ItemSnapshot.Restore rebuilt the ItemData without
    // re-reading its icon off the source definition. Uses the real authored assets,
    // since Restore resolves definitions against the live Resources catalog.
    public class ItemSnapshotIconTests
    {
        [Test]
        public void Material_RestoresIcon_FromItsDefinition()
        {
            var definition = Resources.Load<MaterialDefinition>("MaterialDefinitions/Wood");
            var item = MaterialGenerator.GenerateFromDefinition(definition, ItemRarity.Plain);

            var restored = ItemSnapshot.Capture(item).Restore();

            Assert.AreSame(definition, restored.materialSource);
            Assert.AreSame(definition.icon, restored.icon);
        }

        [Test]
        public void Material_SavedBeforeIconWasAssigned_PicksUpCurrentIcon()
        {
            var definition = Resources.Load<MaterialDefinition>("MaterialDefinitions/Wood");
            var item = MaterialGenerator.GenerateFromDefinition(definition, ItemRarity.Plain);
            item.icon = null;

            var restored = ItemSnapshot.Capture(item).Restore();

            Assert.AreSame(definition.icon, restored.icon);
        }

        [Test]
        public void Equipment_RoundTrips_SourceDefinition_AndIcon()
        {
            var definition = Resources.Load<EquipmentDefinition>("EquipmentDefinitions/HayfieldHelm");
            var item = EquipmentGenerator.GenerateFromDefinition(definition, ItemRarity.Plain, new System.Random(1));
            Assert.AreSame(definition, item.equipmentSource);

            var restored = ItemSnapshot.Capture(item).Restore();

            Assert.AreSame(definition, restored.equipmentSource, "should resolve back to the same live catalog asset");
            Assert.AreSame(definition.icon, restored.icon);
        }
    }
}
