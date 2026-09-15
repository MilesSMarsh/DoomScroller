using System;
using System.Collections.Generic;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // Overrides NextDouble to always return exactly 0.5, which makes CombatSimulator's
    // per-swing variance (1 + (NextDouble()*2-1)*0.15) resolve to exactly 1.0 and its
    // miss check (NextDouble() < 0.08) never trip — every damage-amount assertion
    // downstream becomes an exact integer equality instead of a range check.
    internal sealed class FixedRandom : System.Random
    {
        public override double NextDouble() => 0.5;
    }

    internal static class TestFixtures
    {
        public static ItemData MakeWeapon(int power, float attackSpeed, DamageType damageType = DamageType.Slash,
            EquipmentSlotType slot = EquipmentSlotType.RightHand, AttributeScaling[] scaling = null,
            WeaponAbilityData[] abilities = null, StatusEffectData[] passives = null, bool isTwoHanded = false)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.category = ItemCategory.Weapon;
            item.slotType = slot;
            item.powerValue = power;
            item.attackSpeed = attackSpeed;
            item.damageType = damageType;
            item.attributeScalingEntries = scaling ?? Array.Empty<AttributeScaling>();
            item.abilities = abilities ?? Array.Empty<WeaponAbilityData>();
            item.passiveEffects = passives;
            item.isTwoHanded = isTwoHanded;
            return item;
        }

        public static ItemData MakeGear(ItemCategory category, EquipmentSlotType slot, int power,
            StatusEffectData[] passives = null, WeaponAbilityData[] abilities = null)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.category = category;
            item.slotType = slot;
            item.powerValue = power;
            item.passiveEffects = passives;
            item.abilities = abilities;
            return item;
        }

        public static PlayerLoadout MakeLoadout(params ItemData[] items)
        {
            var loadout = new PlayerLoadout(new List<ItemData>(items));
            foreach (var item in items)
            {
                loadout.Equip(item);
            }

            return loadout;
        }

        // Mirrors the earliest-NextFireTime-wins, first-declared-wins-ties selection
        // CombatSimulator.RunUntilDeathOrVictory uses on its own _timers list — a pure
        // scheduling reference model (no damage/rng involved), so attack-rate and
        // cooldown tests can assert an exact expected beat count instead of a
        // statistical one. Timer A is whichever one is declared earlier in the real
        // simulator's own _timers list (it wins exact ties).
        public static (int CountA, int CountB) MergeCounts(float intervalA, float initialA, float intervalB,
            float initialB, int totalBeats)
        {
            float nextA = initialA, nextB = initialB;
            int countA = 0;
            int countB = 0;
            for (var i = 0; i < totalBeats; i++)
            {
                if (nextB < nextA)
                {
                    countB++;
                    nextB += intervalB;
                }
                else
                {
                    countA++;
                    nextA += intervalA;
                }
            }

            return (countA, countB);
        }

        // Three-way sibling of MergeCounts, tie-break priority A > B > C (matches a
        // three-entry _timers list where A is declared at index 0, B at index 1, C at
        // index 2).
        public static (int CountA, int CountB, int CountC) MergeCounts3(float intervalA, float initialA,
            float intervalB, float initialB, float intervalC, float initialC, int totalBeats)
        {
            float nextA = initialA, nextB = initialB, nextC = initialC;
            int countA = 0;
            int countB = 0;
            int countC = 0;
            for (var i = 0; i < totalBeats; i++)
            {
                if (nextA <= nextB && nextA <= nextC)
                {
                    countA++;
                    nextA += intervalA;
                }
                else if (nextB <= nextC)
                {
                    countB++;
                    nextB += intervalB;
                }
                else
                {
                    countC++;
                    nextC += intervalC;
                }
            }

            return (countA, countB, countC);
        }
    }
}
