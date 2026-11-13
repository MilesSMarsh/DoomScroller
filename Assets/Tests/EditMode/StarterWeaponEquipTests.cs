using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoomScroller.Tests.EditMode
{
    // Covers ProfileController.EquipStarterWeapon (the fresh-game path only; Awake's
    // save-vs-fresh branch is not exercised here). Driven via reflection on an
    // uninitialized instance, same pattern as FeedDrawNextTests.
    public class StarterWeaponEquipTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static WeaponDefinition LoadWoodenSword() =>
            Resources.Load<WeaponDefinition>("WeaponDefinitions/HallowWoodSword");

        private static ProfileController MakeProfile(WeaponDefinition definition)
        {
            var profile = (ProfileController)System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(typeof(ProfileController));
            SetField(profile, "_loadout", new PlayerLoadout(new List<ItemData>()));
            SetField(profile, "starterWeaponDefinition", definition);
            return profile;
        }

        private static void InvokeEquipStarterWeapon(ProfileController profile)
        {
            var method = typeof(ProfileController).GetMethod("EquipStarterWeapon", AnyInstance);
            Assert.IsNotNull(method, "EquipStarterWeapon not found on ProfileController");
            method.Invoke(profile, null);
        }

        private static PlayerLoadout LoadoutOf(ProfileController profile)
        {
            var loadout = (PlayerLoadout)GetField(profile, "_loadout");
            Assert.IsNotNull(loadout);
            return loadout;
        }

        [Test]
        public void EquipStarterWeapon_WithDefinition_EquipsCommonWoodenSword()
        {
            var profile = MakeProfile(LoadWoodenSword());

            InvokeEquipStarterWeapon(profile);

            // Equip places the item in its slot (it is not added to the inventory list),
            // so find it by scanning every equipment slot.
            var loadout = LoadoutOf(profile);
            var equippedItems = new List<ItemData>();
            foreach (EquipmentSlotType slot in System.Enum.GetValues(typeof(EquipmentSlotType)))
            {
                var item = loadout.GetEquipped(slot);
                if (item != null)
                {
                    equippedItems.Add(item);
                }
            }

            Assert.AreEqual(1, equippedItems.Count, "exactly one starter weapon should be equipped");
            var sword = equippedItems[0];
            Assert.AreEqual("Plain Hallow Wood Sword", sword.title);
            Assert.AreEqual(ItemCategory.Weapon, sword.category);
            Assert.AreEqual(ItemRarity.Plain, sword.rarity);
            Assert.AreSame(LoadWoodenSword(), sword.sourceDefinition);
            Assert.AreSame(sword, loadout.GetEquipped(EquipmentSlotType.RightHand));
            Assert.IsNull(loadout.GetEquipped(EquipmentSlotType.Head));
        }

        [Test]
        public void EquipStarterWeapon_NullDefinition_LogsWarningAndLeavesLoadoutEmpty()
        {
            var profile = MakeProfile(null);
            LogAssert.Expect(LogType.Warning, new Regex("starterWeaponDefinition is unassigned"));

            Assert.DoesNotThrow(() => InvokeEquipStarterWeapon(profile));

            var loadout = LoadoutOf(profile);
            Assert.AreEqual(0, loadout.GetAllInventory().Count);
            Assert.IsNull(loadout.GetEquipped(EquipmentSlotType.RightHand));
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, AnyInstance);
            Assert.IsNotNull(field, $"field '{name}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static object GetField(object target, string name)
        {
            var field = target.GetType().GetField(name, AnyInstance);
            Assert.IsNotNull(field, $"field '{name}' not found on {target.GetType().Name}");
            return field.GetValue(target);
        }
    }
}
