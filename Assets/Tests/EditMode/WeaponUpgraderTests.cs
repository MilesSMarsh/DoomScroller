using System.Linq;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // The Blacksmith's Empower/Reroll actions — the user's own bug report: a named
    // weapon (rolled via WeaponGenerator.GenerateFromDefinition, sourceDefinition set)
    // should only ever draw skills from ITS OWN authored pool, never the wider
    // type-wide catalog every archetype-rolled weapon shares.
    public class WeaponUpgraderTests
    {
        private static AbilityDefinition MakeAbility(string name)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.data = new WeaponAbilityData { abilityName = name };
            ability.allowedWeaponTypes = new[] { WeaponType.Melee };
            return ability;
        }

        private static PassiveDefinition MakePassive(string name)
        {
            var passive = ScriptableObject.CreateInstance<PassiveDefinition>();
            passive.effect = new StatusEffectData
            {
                passiveName = name, effectType = StatusEffectType.EmpowerDamage, magnitude = 0.1f
            };
            passive.allowedWeaponTypes = new[] { WeaponType.Melee };
            return passive;
        }

        private static WeaponDefinition MakeNamedDefinition(string name, AbilityDefinition[] abilities,
            PassiveDefinition[] passives)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.weaponName = name;
            definition.weaponType = WeaponType.Melee;
            definition.possibleAbilities = abilities;
            definition.possiblePassives = passives;
            return definition;
        }

        [Test]
        public void Reroll_NamedWeapon_NeverDrawsOutsideItsOwnSourceDefinitionPool()
        {
            var definition = MakeNamedDefinition("Test Named Weapon A",
                new[] { MakeAbility("OwnAbilityOnly") }, new[] { MakePassive("OwnPassiveOnly") });

            var weapon = TestFixtures.MakeWeapon(power: 5, attackSpeed: 1f);
            weapon.weaponType = WeaponType.Melee;
            weapon.rarity = ItemRarity.Mythic; // desiredCount 4, but this weapon's own pool only has 1 of each
            weapon.sourceDefinition = definition;

            for (var trial = 0; trial < 20; trial++)
            {
                WeaponUpgrader.Reroll(weapon, new System.Random(trial));

                Assert.AreEqual(1, weapon.abilities.Length);
                Assert.AreEqual("OwnAbilityOnly", weapon.abilities[0].abilityName);
                Assert.AreEqual(1, weapon.passiveEffects.Length);
                Assert.AreEqual("OwnPassiveOnly", weapon.passiveEffects[0].passiveName);
            }
        }

        [Test]
        public void Empower_NamedWeapon_OnlyAddsSkillsFromItsOwnSourceDefinitionPool()
        {
            var definition = MakeNamedDefinition("Test Named Weapon B",
                new[] { MakeAbility("OwnAbilityOnly") }, new[] { MakePassive("OwnPassiveOnly") });

            var weapon = TestFixtures.MakeWeapon(power: 5, attackSpeed: 1f);
            weapon.weaponType = WeaponType.Melee;
            weapon.rarity = ItemRarity.Common;
            weapon.sourceDefinition = definition;
            weapon.abilities = System.Array.Empty<WeaponAbilityData>();
            weapon.passiveEffects = System.Array.Empty<StatusEffectData>();

            var rng = new System.Random(3);
            for (var i = 0; i < 4; i++)
            {
                WeaponUpgrader.Empower(weapon, rng);
            }

            Assert.AreEqual(ItemRarity.Mythic, weapon.rarity);
            Assert.AreEqual(1, weapon.abilities.Length, "the pool only ever had one ability to give");
            Assert.AreEqual("OwnAbilityOnly", weapon.abilities[0].abilityName);
            Assert.AreEqual(1, weapon.passiveEffects.Length, "the pool only ever had one passive to give");
            Assert.AreEqual("OwnPassiveOnly", weapon.passiveEffects[0].passiveName);
        }

        [Test]
        public void Reroll_ArchetypeRolledWeapon_WithNoSourceDefinition_FallsBackToTypeWideCatalog()
        {
            // No sourceDefinition (as WeaponGenerator.Generate leaves it) — Reroll has no
            // curated pool to restrict itself to, so it must fall back to the same
            // type-wide catalog its very first roll would have used.
            var weapon = TestFixtures.MakeWeapon(power: 5, attackSpeed: 1f);
            weapon.weaponType = WeaponType.Melee;
            weapon.rarity = ItemRarity.Mythic;
            weapon.sourceDefinition = null;

            WeaponUpgrader.Reroll(weapon, new System.Random(1));

            var typeWideNames = WeaponGenerator.TypeAbilityPool(WeaponType.Melee).Select(a => a.data.abilityName).ToList();
            CollectionAssert.IsSubsetOf(weapon.abilities.Select(a => a.abilityName), typeWideNames);
        }
    }
}
