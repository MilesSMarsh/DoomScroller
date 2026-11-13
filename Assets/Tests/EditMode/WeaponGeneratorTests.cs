using System;
using System.Linq;
using DoomScroller.CombatCore;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    public class WeaponGeneratorTests
    {
        private static WeaponDefinition MakeDefinition()
        {
            var def = ScriptableObject.CreateInstance<WeaponDefinition>();
            def.weaponName = "Test Blade";
            def.weaponType = WeaponType.Melee;
            def.damageType = DamageType.Slash;
            def.attackSpeed = 1.3f;
            def.statsByRarity = new[]
            {
                new WeaponRarityStats { baseDamage = 1, attributeScalingEntries = new[] { new AttributeScaling { attribute = PlayerAttribute.Constitution, coefficient = 0.1f } } },
                new WeaponRarityStats { baseDamage = 2, attributeScalingEntries = Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 4, attributeScalingEntries = Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 8, attributeScalingEntries = Array.Empty<AttributeScaling>() },
                new WeaponRarityStats { baseDamage = 16, attributeScalingEntries = Array.Empty<AttributeScaling>() }
            };
            def.possibleAbilities = Array.Empty<AbilityDefinition>();
            def.possiblePassives = Array.Empty<PassiveDefinition>();
            return def;
        }

        [Test]
        public void GenerateFromDefinition_CopiesEveryRarityTiersOwnRow_Exactly()
        {
            var definition = MakeDefinition();

            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
            {
                var item = WeaponGenerator.GenerateFromDefinition(definition, rarity, new FixedRandom());
                var expectedStats = definition.statsByRarity[(int)rarity];

                Assert.AreEqual(expectedStats.baseDamage, item.powerValue, $"rarity {rarity}");
                Assert.AreEqual(definition.attackSpeed, item.attackSpeed, $"rarity {rarity}");
                Assert.AreEqual(definition.damageType, item.damageType, $"rarity {rarity}");
                Assert.AreSame(expectedStats.attributeScalingEntries, item.attributeScalingEntries, $"rarity {rarity}");
            }
        }

        [Test]
        public void RollAbilities_CapsAtPoolSize_WhenPoolIsSmallerThanDesiredCount()
        {
            var pool = new[] { MakeAbility("A"), MakeAbility("B") };

            var rolled = WeaponGenerator.RollAbilities(pool, desiredCount: 5, new FixedRandom());

            Assert.AreEqual(2, rolled.Length);
        }

        [Test]
        public void RollAbilities_ReturnsExactlyDesiredCount_WhenPoolIsLarger()
        {
            var pool = new[] { MakeAbility("A"), MakeAbility("B"), MakeAbility("C"), MakeAbility("D") };

            var rolled = WeaponGenerator.RollAbilities(pool, desiredCount: 2, new FixedRandom());

            Assert.AreEqual(2, rolled.Length);
            // Every rolled entry actually came from the pool, no duplicates.
            Assert.AreEqual(rolled.Length, rolled.Select(a => a.abilityName).Distinct().Count());
        }

        [Test]
        public void RollAbilities_NullOrEmptyPool_ReturnsEmptyArray_NeverNullOrThrows()
        {
            Assert.IsEmpty(WeaponGenerator.RollAbilities(null, 3, new FixedRandom()));
            Assert.IsEmpty(WeaponGenerator.RollAbilities(Array.Empty<AbilityDefinition>(), 3, new FixedRandom()));
            Assert.IsEmpty(WeaponGenerator.RollAbilities(new[] { MakeAbility("A") }, 0, new FixedRandom()));
        }

        [Test]
        public void RollPassives_PreservesNameAndDescription()
        {
            var passive = ScriptableObject.CreateInstance<PassiveDefinition>();
            passive.effect = new StatusEffectData
            {
                passiveName = "Ember's Wrath", description = "A burning curse.",
                effectType = StatusEffectType.EmpowerDamage, magnitude = 0.1f
            };

            var rolled = WeaponGenerator.RollPassives(new[] { passive }, 1, new FixedRandom());

            Assert.AreEqual("Ember's Wrath", rolled[0].passiveName);
            Assert.AreEqual("A burning curse.", rolled[0].description);
        }

        [Test]
        public void RollPassives_CapsAtPoolSize_WhenPoolIsSmallerThanDesiredCount()
        {
            // The user's own bug report: a weapon with a 2-ability / 3-passive pool
            // should get MORE than one passive at a high enough rarity, not a coin-flip
            // capped at 1 — desiredCount(5) should cap at the pool's own size (3), not
            // at some fixed 1.
            var pool = new[] { MakePassive("A"), MakePassive("B"), MakePassive("C") };

            var rolled = WeaponGenerator.RollPassives(pool, desiredCount: 5, new FixedRandom());

            Assert.AreEqual(3, rolled.Length);
        }

        [Test]
        public void RollPassives_ReturnsExactlyDesiredCount_WhenPoolIsLarger()
        {
            var pool = new[] { MakePassive("A"), MakePassive("B"), MakePassive("C"), MakePassive("D") };

            var rolled = WeaponGenerator.RollPassives(pool, desiredCount: 2, new FixedRandom());

            Assert.AreEqual(2, rolled.Length);
            // Every rolled entry actually came from the pool, no duplicate references.
            Assert.AreEqual(rolled.Length, rolled.Distinct().Count());
        }

        [Test]
        public void RollPassives_NullOrEmptyPool_ReturnsEmptyArray_NeverNullOrThrows()
        {
            Assert.IsEmpty(WeaponGenerator.RollPassives(null, 3, new FixedRandom()));
            Assert.IsEmpty(WeaponGenerator.RollPassives(Array.Empty<PassiveDefinition>(), 3, new FixedRandom()));
            Assert.IsEmpty(WeaponGenerator.RollPassives(new[] { MakePassive("A") }, 0, new FixedRandom()));
        }

        [Test]
        public void RollSkills_DesiredCountIsATotal_AcrossBothPoolsCombined_NotEachIndependently()
        {
            var abilityPool = new[] { MakeAbility("A1"), MakeAbility("A2") };
            var passivePool = new[] { MakePassive("P1"), MakePassive("P2"), MakePassive("P3") };

            var (abilities, passives) = WeaponGenerator.RollSkills(abilityPool, passivePool, desiredCount: 1, new FixedRandom());

            Assert.AreEqual(1, abilities.Length + passives.Length);
        }

        [Test]
        public void RollSkills_CapsAtCombinedPoolSize_WhenPoolsTogetherAreSmallerThanDesiredCount()
        {
            var abilityPool = new[] { MakeAbility("A1") };
            var passivePool = new[] { MakePassive("P1") };

            var (abilities, passives) = WeaponGenerator.RollSkills(abilityPool, passivePool, desiredCount: 10, new FixedRandom());

            Assert.AreEqual(2, abilities.Length + passives.Length);
        }

        [Test]
        public void RollSkills_NullPools_ReturnsEmptyArrays_NeverNullOrThrows()
        {
            var (abilities, passives) = WeaponGenerator.RollSkills(null, null, 3, new FixedRandom());

            Assert.IsEmpty(abilities);
            Assert.IsEmpty(passives);
        }

        [Test]
        public void GenerateFromDefinition_AtEveryRarity_GrantsExactlyMinOfDesiredAndCombinedPoolSize_AsATotal()
        {
            // Mirrors the user's real content shape: 2 abilities authored, 3 passives
            // authored (an intentionally asymmetric pool) — SkillCountByRarity is a
            // TOTAL across both, not a count for each independently, so an Tempered
            // weapon here gets exactly 1 skill total (ability or passive), never 1 of
            // each (the bug being fixed).
            var definition = MakeDefinition();
            definition.possibleAbilities = new[] { MakeAbility("A1"), MakeAbility("A2") };
            definition.possiblePassives = new[] { MakePassive("P1"), MakePassive("P2"), MakePassive("P3") };
            const int combinedPoolSize = 5;

            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
            {
                var item = WeaponGenerator.GenerateFromDefinition(definition, rarity, new FixedRandom());
                var desired = WeaponGenerator.SkillCountByRarity[(int)rarity];
                var expectedTotal = Math.Min(desired, combinedPoolSize);

                Assert.AreEqual(expectedTotal, item.abilities.Length + item.passiveEffects.Length,
                    $"rarity {rarity}: total skills (abilities + passives combined)");
            }
        }

        [Test]
        public void GenerateFromDefinition_AtUncommonRarity_NeverGrantsBothAnAbilityAndAPassive()
        {
            // The exact bug report: Tempered (SkillCountByRarity == 1) used to give 1
            // ability AND 1 passive — it should give exactly one skill, either kind.
            var definition = MakeDefinition();
            definition.possibleAbilities = new[] { MakeAbility("A1"), MakeAbility("A2") };
            definition.possiblePassives = new[] { MakePassive("P1"), MakePassive("P2"), MakePassive("P3") };

            for (var seed = 0; seed < 30; seed++)
            {
                var item = WeaponGenerator.GenerateFromDefinition(definition, ItemRarity.Tempered, new System.Random(seed));

                Assert.AreEqual(1, item.abilities.Length + item.passiveEffects.Length);
                Assert.IsFalse(item.abilities.Length == 1 && item.passiveEffects.Length == 1);
            }
        }

        private static AbilityDefinition MakeAbility(string name)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.data = new WeaponAbilityData { abilityName = name };
            return ability;
        }

        private static PassiveDefinition MakePassive(string name)
        {
            var passive = ScriptableObject.CreateInstance<PassiveDefinition>();
            passive.effect = new StatusEffectData { effectType = StatusEffectType.EmpowerDamage, magnitude = 0.1f };
            return passive;
        }
    }
}
