using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    public class StarterWeaponTests
    {
        private static WeaponDefinition Load() =>
            Resources.Load<WeaponDefinition>("WeaponDefinitions/HallowWoodSword");

        [Test]
        public void WoodenSwordAsset_Exists()
        {
            Assert.IsNotNull(Load());
        }

        [Test]
        public void Create_ReturnsCommonWoodenSword_FromDefinition()
        {
            var definition = Load();
            var item = StarterWeapon.Create(definition, new System.Random(1));

            Assert.IsNotNull(item);
            Assert.AreEqual(ItemCategory.Weapon, item.category);
            Assert.AreEqual(ItemRarity.Plain, item.rarity);
            Assert.AreSame(definition, item.sourceDefinition);
            Assert.AreEqual("Plain Hallow Wood Sword", item.title);
            Assert.AreEqual(definition.statsByRarity[0].baseDamage, item.powerValue);
        }

        [Test]
        public void Create_HasNoAbilitiesOrPassives()
        {
            Assert.AreEqual(0, WeaponGenerator.SkillCountByRarity[0]);
            var item = StarterWeapon.Create(Load(), new System.Random(1));

            Assert.AreEqual(0, item.abilities.Length);
            Assert.AreEqual(0, item.passiveEffects.Length);
        }

        [Test]
        public void Create_SameSeed_GivesSameResult()
        {
            var a = StarterWeapon.Create(Load(), new System.Random(7));
            var b = StarterWeapon.Create(Load(), new System.Random(7));

            Assert.AreEqual(a.title, b.title);
            Assert.AreEqual(a.powerValue, b.powerValue);
            Assert.AreEqual(a.attackSpeed, b.attackSpeed);
            Assert.AreEqual(a.abilities.Length, b.abilities.Length);
            Assert.AreEqual(a.passiveEffects.Length, b.passiveEffects.Length);
        }

        [Test]
        public void Create_NullDefinition_ReturnsNull()
        {
            Assert.IsNull(StarterWeapon.Create(null, new System.Random(1)));
        }

        [Test]
        public void Create_RoundTripsThroughSaveData_AndRelinksDefinition()
        {
            var definition = Load();
            var item = StarterWeapon.Create(definition, new System.Random(1));

            var save = new SaveData();
            save.itemPool.Add(ItemSnapshot.Capture(item));
            var json = JsonUtility.ToJson(save);
            var loaded = JsonUtility.FromJson<SaveData>(json);
            var restored = loaded.itemPool[0].Restore();

            Assert.AreEqual("Plain Hallow Wood Sword", restored.title);
            Assert.AreEqual(ItemRarity.Plain, restored.rarity);
            Assert.AreSame(definition, restored.sourceDefinition);
        }

        [Test]
        public void Restore_SaveFromBeforeRename_RelinksToHallowWoodSword()
        {
            var definition = Load();
            var item = StarterWeapon.Create(definition, new System.Random(1));
            var snapshot = ItemSnapshot.Capture(item);
            snapshot.title = "Wooden Sword";
            snapshot.sourceWeaponName = "Wooden Sword";

            var restored = snapshot.Restore();

            Assert.AreEqual("Plain Hallow Wood Sword", restored.title);
            Assert.AreSame(definition, restored.sourceDefinition);
        }
    }
}
