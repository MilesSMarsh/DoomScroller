using System.Linq;
using DoomScroller.Combat;
using DoomScroller.CombatCore;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    // "Weapons deal the correct amount of damage" / "the correct type of damage" /
    // "the armour is providing damage mitigation" / "the talismans are giving correct
    // abilities and passives" — everything here reads the FIRST beat of a fresh fight,
    // which is always the player's basic attack: both the player and enemy timers start
    // at NextFireTime 0, and CombatSimulator's earliest-time selection keeps whichever
    // timer was checked first on an exact tie — the player timer is declared before the
    // enemy timer in the constructor, so it always wins that opening tie regardless of
    // either side's attack speed.
    public class WeaponDamageTests
    {
        private static CombatBeat FirstPlayerBeat(PlayerLoadout loadout, int enemyHealth = 100000,
            int enemyDamage = 0, DamageTypeModifier[] enemyWeaknesses = null)
        {
            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), 1000, enemyHealth,
                enemyDamage, 1f, enemyWeaknesses, new FixedRandom());
            return sim.RunUntilDeathOrVictory().First(b => b.Actor == CombatActor.Player);
        }

        [Test]
        public void BasicAttack_DealsExactlyWeaponPower_WhenNoScalingOrPassives()
        {
            var weapon = TestFixtures.MakeWeapon(power: 10, attackSpeed: 1f);
            var loadout = TestFixtures.MakeLoadout(weapon);

            var beat = FirstPlayerBeat(loadout);

            Assert.IsFalse(beat.WasMiss);
            Assert.AreEqual(10, beat.DisplayDamage);
        }

        [Test]
        public void BasicAttack_AddsAttributeScalingBonus_RoundedToNearestInt()
        {
            // Default PlayerAttributes seed every attribute at BaseValue (5) — no
            // Allocate() calls needed to exercise scaling.
            var weapon = TestFixtures.MakeWeapon(power: 10, attackSpeed: 1f,
                scaling: new[] { new AttributeScaling { attribute = PlayerAttribute.Constitution, coefficient = 2f } });
            var loadout = TestFixtures.MakeLoadout(weapon);

            // powerValue(10) + round(Constitution(5) * 2.0 = 10) = 20.
            var beat = FirstPlayerBeat(loadout);

            Assert.AreEqual(20, beat.DisplayDamage);
        }

        [Test]
        public void DualWield_SumsEachWeaponsDamageIndependently_BeforeItsOwnTypeMultiplier()
        {
            var slash = TestFixtures.MakeWeapon(power: 10, attackSpeed: 1f, damageType: DamageType.Slash,
                slot: EquipmentSlotType.RightHand);
            var pierce = TestFixtures.MakeWeapon(power: 6, attackSpeed: 1f, damageType: DamageType.Pierce,
                slot: EquipmentSlotType.LeftHand);
            var loadout = TestFixtures.MakeLoadout(slash, pierce);
            var weaknesses = new[] { new DamageTypeModifier { type = DamageType.Slash, multiplier = 2f } };

            // Slash weapon doubled (10*2=20), Pierce weapon untouched (6*1=6) — 26 total,
            // not one shared multiplier applied to (10+6).
            var beat = FirstPlayerBeat(loadout, enemyWeaknesses: weaknesses);

            Assert.AreEqual(26, beat.DisplayDamage);
        }

        [Test]
        public void DamageType_UnmatchedByAnyWeakness_StaysAtBaseline()
        {
            var weapon = TestFixtures.MakeWeapon(power: 10, attackSpeed: 1f, damageType: DamageType.Arcane);
            var loadout = TestFixtures.MakeLoadout(weapon);
            var weaknesses = new[] { new DamageTypeModifier { type = DamageType.Slash, multiplier = 3f } };

            var beat = FirstPlayerBeat(loadout, enemyWeaknesses: weaknesses);

            Assert.AreEqual(10, beat.DisplayDamage);
        }

        [Test]
        public void DamageType_NullWeaknessArray_StaysAtBaseline()
        {
            var weapon = TestFixtures.MakeWeapon(power: 10, attackSpeed: 1f, damageType: DamageType.Elemental);
            var loadout = TestFixtures.MakeLoadout(weapon);

            var beat = FirstPlayerBeat(loadout, enemyWeaknesses: null);

            Assert.AreEqual(10, beat.DisplayDamage);
        }

        [Test]
        public void Armor_MitigatesIncomingDamage_ByExactlyItsPower()
        {
            var weapon = TestFixtures.MakeWeapon(power: 0, attackSpeed: 1f);
            var armor = TestFixtures.MakeGear(ItemCategory.Armor, EquipmentSlotType.Head, power: 7);
            var loadout = TestFixtures.MakeLoadout(weapon, armor);

            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), 1000, 100000,
                enemyAttackDamage: 20, enemyAttackSpeed: 1f, enemyWeaknesses: null, rng: new FixedRandom());
            var enemyBeat = sim.RunUntilDeathOrVictory().First(b => b.Actor == CombatActor.Enemy);

            Assert.AreEqual(13, enemyBeat.DisplayDamage);
        }

        [Test]
        public void Armor_CanNeverReduceAHitBelowOne_NoMatterHowHigh()
        {
            var weapon = TestFixtures.MakeWeapon(power: 0, attackSpeed: 1f);
            var armor = TestFixtures.MakeGear(ItemCategory.Armor, EquipmentSlotType.Head, power: 500);
            var loadout = TestFixtures.MakeLoadout(weapon, armor);

            var sim = TestFixtures.MakeSim(loadout, new PlayerAttributes(), 1000, 100000,
                enemyAttackDamage: 20, enemyAttackSpeed: 1f, enemyWeaknesses: null, rng: new FixedRandom());
            var enemyBeat = sim.RunUntilDeathOrVictory().First(b => b.Actor == CombatActor.Enemy);

            Assert.AreEqual(1, enemyBeat.DisplayDamage);
        }

        [Test]
        public void Talisman_EmpowerDamagePassive_ActuallyAppliesInCombat()
        {
            // Regression test for the PlayerLoadout.GetEquippedPassiveEffects fix — this
            // used to be silently inert because that method only scanned weapon hand
            // slots.
            var weapon = TestFixtures.MakeWeapon(power: 10, attackSpeed: 1f);
            var passiveTalisman = TestFixtures.MakeGear(ItemCategory.Talisman, EquipmentSlotType.Talisman1, power: 0,
                passives: new[] { new StatusEffectData { effectType = StatusEffectType.EmpowerDamage, magnitude = 0.5f } });
            var loadout = TestFixtures.MakeLoadout(weapon, passiveTalisman);

            var beat = FirstPlayerBeat(loadout);

            // 10 * 1.5 = 15.
            Assert.AreEqual(15, beat.DisplayDamage);
        }

        [Test]
        public void Talisman_WithoutPassive_LeavesDamageAtBaseline()
        {
            var weapon = TestFixtures.MakeWeapon(power: 10, attackSpeed: 1f);
            var plainTalisman = TestFixtures.MakeGear(ItemCategory.Talisman, EquipmentSlotType.Talisman1, power: 0);
            var loadout = TestFixtures.MakeLoadout(weapon, plainTalisman);

            var beat = FirstPlayerBeat(loadout);

            Assert.AreEqual(10, beat.DisplayDamage);
        }

        [Test]
        public void Talisman_AbilitiesAreIgnored_AbilitiesStayWeaponOnlyByDesign()
        {
            var talismanWithAbilities = TestFixtures.MakeGear(ItemCategory.Talisman, EquipmentSlotType.Talisman1, power: 0,
                abilities: new[] { new WeaponAbilityData { abilityName = "ShouldNeverFire", cooldownSeconds = 1f } });
            var loadout = TestFixtures.MakeLoadout(talismanWithAbilities);

            Assert.IsEmpty(loadout.GetEquippedWeaponAbilityPairs());
        }
    }
}
