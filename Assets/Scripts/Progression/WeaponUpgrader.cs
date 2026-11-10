using System.Collections.Generic;
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
        // Indexed by the weapon's CURRENT rarity (index 4/Exalted is never read, since
        // CanEmpower is false there — Exalted is the max tier).
        private static readonly float[] EmpowerGoldMultiplierByRarity = { 1.5f, 2.5f, 4f, 6f, 0f };
        private const float RerollGoldMultiplier = 1.5f;

        public static WeaponUpgradeConfig LoadConfig()
        {
            return Resources.Load<WeaponUpgradeConfig>(ConfigResourcesPath);
        }

        public static bool CanEmpower(ItemData weapon)
        {
            return weapon != null && weapon.category == ItemCategory.Weapon && weapon.rarity < ItemRarity.Exalted;
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

        // A named weapon (rolled via GenerateFromDefinition, sourceDefinition set) only
        // ever draws from its own curated pool — the whole point of authoring one. An
        // archetype-rolled weapon (WeaponGenerator.Generate, sourceDefinition null) has
        // no pool of its own, so it falls back to the type-wide catalog, same as its
        // initial roll did.
        private static IEnumerable<AbilityDefinition> AbilityPoolFor(ItemData weapon)
        {
            return weapon.sourceDefinition != null
                ? weapon.sourceDefinition.possibleAbilities ?? System.Array.Empty<AbilityDefinition>()
                : WeaponGenerator.TypeAbilityPool(weapon.weaponType);
        }

        private static IEnumerable<PassiveDefinition> PassivePoolFor(ItemData weapon)
        {
            return weapon.sourceDefinition != null
                ? weapon.sourceDefinition.possiblePassives ?? System.Array.Empty<PassiveDefinition>()
                : WeaponGenerator.TypePassivePool(weapon.weaponType);
        }

        // Raises rarity by one tier and recomputes power/scaling/goldValue for it via the
        // same archetype formula WeaponGenerator.Generate itself uses — a higher rarity
        // always allows one more TOTAL skill than before (see
        // WeaponGenerator.SkillCountByRarity — abilities and passives combined, not
        // each independently), so this also rolls one additional skill — ability or
        // passive, whichever — from the weapon's own pool (see
        // AbilityPoolFor/PassivePoolFor) if one's available that the weapon doesn't
        // already carry. Silently skips the roll if neither pool has anything new to
        // offer (a thin pool, same "yields fewer, not an error" convention as an initial
        // roll) — rarity/stats still advance either way.
        public static void Empower(ItemData weapon, System.Random rng = null)
        {
            rng ??= new System.Random();

            var newRarity = weapon.rarity + 1;
            var (power, scaling) = WeaponGenerator.ComputeArchetypeStats(weapon.weaponType, newRarity);

            weapon.rarity = newRarity;
            weapon.title = WeaponGenerator.TitleFor(newRarity, weapon.sourceDefinition != null
                ? weapon.sourceDefinition.weaponName
                : weapon.weaponType.ToString());
            weapon.powerValue = power;
            weapon.statSummary = $"+{power} {ItemCategoryInfo.Label(ItemCategory.Weapon)}";
            weapon.attributeScalingEntries = scaling;
            weapon.goldValue = RewardGenerator.RarityGoldValue[(int)newRarity];

            var desiredCount = WeaponGenerator.SkillCountByRarity[(int)newRarity];
            var currentAbilities = weapon.abilities ?? System.Array.Empty<WeaponAbilityData>();
            var currentPassives = weapon.passiveEffects ?? System.Array.Empty<StatusEffectData>();
            if (currentAbilities.Length + currentPassives.Length >= desiredCount)
            {
                return;
            }

            var abilityCandidates = AbilityPoolFor(weapon).Where(a => a != null && !currentAbilities.Contains(a.data)).ToList();
            var passiveCandidates = PassivePoolFor(weapon).Where(p => p != null && !currentPassives.Contains(p.effect)).ToList();
            var totalCandidates = abilityCandidates.Count + passiveCandidates.Count;
            if (totalCandidates == 0)
            {
                return;
            }

            // One combined draw across both pools — an ability and a passive are equally
            // likely candidates for this one new slot, weighted by how many of each are
            // actually still available.
            var pick = rng.Next(totalCandidates);
            if (pick < abilityCandidates.Count)
            {
                weapon.abilities = currentAbilities.Append(abilityCandidates[pick].data).ToArray();
            }
            else
            {
                weapon.passiveEffects = currentPassives.Append(passiveCandidates[pick - abilityCandidates.Count].effect).ToArray();
            }
        }

        // Fresh draw of the weapon's full skill set (same TOTAL count — abilities and
        // passives combined — as the weapon's current rarity allows), from the weapon's
        // own pool (see AbilityPoolFor/PassivePoolFor) — rarity itself is untouched.
        public static void Reroll(ItemData weapon, System.Random rng = null)
        {
            rng ??= new System.Random();

            var desiredCount = WeaponGenerator.SkillCountByRarity[(int)weapon.rarity];
            var (abilities, passives) = WeaponGenerator.RollSkills(AbilityPoolFor(weapon), PassivePoolFor(weapon), desiredCount, rng);
            weapon.abilities = abilities;
            weapon.passiveEffects = passives;
        }
    }
}
