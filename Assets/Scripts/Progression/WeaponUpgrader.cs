using System.Linq;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Progression
{
    // The Blacksmith's Upgrade tab logic — Empower (raise rarity, gain a skill slot) and
    // Reroll (fresh abilities + passive at the same rarity). Both mutate the given
    // ItemData IN PLACE rather than replacing it: the weapon may currently be equipped or
    // stacked with other owned copies, and swapping the reference would break the
    // equip-slot binding and inventory identity. Gating/spending lives on PlayerLoadout
    // (TryEmpowerWeapon/TryRerollWeapon), same split as every other Try* action.
    public static class WeaponUpgrader
    {
        private const string ConfigResourcesPath = "WeaponUpgradeConfig";

        // Placeholder, tuned later like every other number introduced this session.
        // Indexed by the weapon's CURRENT rarity (index 4/Mythic is never read, since
        // CanEmpower is false there — Mythic is the max tier).
        private static readonly float[] EmpowerGoldMultiplierByRarity = { 1.5f, 2.5f, 4f, 6f, 0f };
        private const float RerollGoldMultiplier = 1.5f;

        public static WeaponUpgradeConfig LoadConfig()
        {
            return Resources.Load<WeaponUpgradeConfig>(ConfigResourcesPath);
        }

        public static bool CanEmpower(ItemData weapon)
        {
            return weapon != null && weapon.category == ItemCategory.Weapon && weapon.rarity < ItemRarity.Mythic;
        }

        public static int EmpowerGoldCost(ItemData weapon)
        {
            return Mathf.RoundToInt(weapon.goldValue * EmpowerGoldMultiplierByRarity[(int)weapon.rarity]);
        }

        public static int RerollGoldCost(ItemData weapon)
        {
            return Mathf.RoundToInt(weapon.goldValue * RerollGoldMultiplier);
        }

        public static RecipeIngredient[] EmpowerIngredients(ItemData weapon)
        {
            var config = LoadConfig();
            var tier = config != null ? config.empowerCostByRarity[(int)weapon.rarity] : null;
            return tier?.ingredients ?? System.Array.Empty<RecipeIngredient>();
        }

        public static RecipeIngredient[] RerollIngredients()
        {
            var config = LoadConfig();
            return config != null ? config.rerollIngredients : System.Array.Empty<RecipeIngredient>();
        }

        // Raises rarity by one tier and recomputes power/scaling/goldValue for it via the
        // same archetype formula WeaponGenerator.Generate itself uses — a higher rarity
        // always allows one more ability than before (see
        // WeaponGenerator.AbilityCountByRarity), so this also rolls one additional
        // ability from the type-wide pool if one's available that the weapon doesn't
        // already carry. Silently skips the ability roll if the pool has nothing new to
        // offer (a thin pool, same "yields fewer, not an error" convention as an initial
        // roll) — rarity/stats still advance either way.
        public static void Empower(ItemData weapon, System.Random rng = null)
        {
            rng ??= new System.Random();

            var newRarity = weapon.rarity + 1;
            var (power, scaling) = WeaponGenerator.ComputeArchetypeStats(weapon.weaponType, newRarity);

            weapon.rarity = newRarity;
            weapon.powerValue = power;
            weapon.statSummary = $"+{power} {ItemCategoryInfo.Label(ItemCategory.Weapon)}";
            weapon.attributeScalingEntries = scaling;
            weapon.goldValue = RewardGenerator.RarityGoldValue[(int)newRarity];

            var current = weapon.abilities ?? System.Array.Empty<WeaponAbilityData>();
            var desiredCount = WeaponGenerator.AbilityCountByRarity[(int)newRarity];
            if (current.Length >= desiredCount)
            {
                return;
            }

            var candidates = WeaponGenerator.TypeAbilityPool(weapon.weaponType)
                .Where(a => a != null && !current.Contains(a.data))
                .ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            var chosen = candidates[rng.Next(candidates.Count)];
            weapon.abilities = current.Append(chosen.data).ToArray();
        }

        // Fresh draw of both the ability list (same count as the weapon's current
        // rarity) and the independent-chance passive effect, from the type-wide pools —
        // rarity itself is untouched.
        public static void Reroll(ItemData weapon, System.Random rng = null)
        {
            rng ??= new System.Random();

            var desiredCount = WeaponGenerator.AbilityCountByRarity[(int)weapon.rarity];
            weapon.abilities = WeaponGenerator.RollAbilities(WeaponGenerator.TypeAbilityPool(weapon.weaponType), desiredCount, rng);
            weapon.passiveEffects = WeaponGenerator.RollPassive(WeaponGenerator.TypePassivePool(weapon.weaponType), rng);
        }
    }
}
