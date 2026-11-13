using System;
using System.Linq;
using DoomScroller.Combat;
using DoomScroller.CombatCore;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    public class FightInputBuilderTests
    {
        [Test]
        public void AttributeIndexMatchesPlayerAttributeOrdinals()
        {
            Assert.AreEqual((int)PlayerAttribute.Constitution, AttributeIndex.Constitution);
            Assert.AreEqual((int)PlayerAttribute.Agility, AttributeIndex.Agility);
            Assert.AreEqual((int)PlayerAttribute.Spirit, AttributeIndex.Spirit);
            Assert.AreEqual((int)PlayerAttribute.Expertise, AttributeIndex.Expertise);
        }

        [Test]
        public void BuildPlayer_CarriesWeaponsAbilitiesPassivesAndDefense()
        {
            var ability = new WeaponAbilityData { abilityName = "Hit", powerBonus = 3, cooldownSeconds = 2f };
            var weapon = TestFixtures.MakeWeapon(power: 8, attackSpeed: 1.5f, damageType: DamageType.Arcane,
                abilities: new[] { ability },
                passives: new[] { new StatusEffectData { effectType = StatusEffectType.EmpowerDamage, magnitude = 0.2f } });
            var armor = TestFixtures.MakeGear(ItemCategory.Armor, EquipmentSlotType.Chest, power: 4);
            var loadout = TestFixtures.MakeLoadout(weapon, armor);

            var player = FightInputBuilder.BuildPlayer(loadout, new PlayerAttributes(), 77);

            Assert.AreEqual(77, player.startHp);
            Assert.AreEqual(4, player.defense);
            Assert.AreEqual(1, player.weapons.Length);
            Assert.AreEqual(8, player.weapons[0].power);
            Assert.AreEqual((int)DamageType.Arcane, player.weapons[0].damageType);
            Assert.AreEqual("Hit", player.abilities.Single().name);
            Assert.AreEqual(0, player.abilities[0].weaponIndex);
            Assert.AreEqual(StatusEffectType.EmpowerDamage, player.passives.Single().effectType);
            Assert.AreEqual(PlayerAttributes.BaseValue, player.attributes[AttributeIndex.Agility]);
        }

        [Test]
        public void Revive_IsNotArmedWithoutAutoConsume()
        {
            var loadout = TestFixtures.MakeLoadout(TestFixtures.MakeWeapon(power: 5, attackSpeed: 1f));
            var input = FightInputBuilder.Build(loadout, new PlayerAttributes(), new GameSettings(), 50, 1, 10, 1, 1f,
                null, true);
            Assert.IsFalse(input.reviveArmed);
        }
    }
}
