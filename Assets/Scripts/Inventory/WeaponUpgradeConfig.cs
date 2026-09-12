using System;
using UnityEngine;

namespace DoomScroller.Inventory
{
    [Serializable]
    public class EmpowerTierCost
    {
        public RecipeIngredient[] ingredients = Array.Empty<RecipeIngredient>();
    }

    // Singleton-style asset (one instance expected, at Assets/Resources/
    // WeaponUpgradeConfig.asset, created lazily by the Equipment Menu window) holding the
    // Blacksmith's Empower/Reroll material costs — see WeaponUpgrader for how these are
    // read and applied, and PlayerLoadout.TryEmpowerWeapon/TryRerollWeapon for how they're
    // spent.
    [CreateAssetMenu(menuName = "DoomScroller/Weapon Upgrade Config", fileName = "WeaponUpgradeConfig")]
    public class WeaponUpgradeConfig : ScriptableObject
    {
        [Tooltip("Index i = ingredients required to Empower a weapon currently at rarity " +
                  "i up to i+1 (0 = Common->Uncommon, 1 = Uncommon->Rare, 2 = Rare-> " +
                  "Legendary, 3 = Legendary->Mythic). Shared across every weapon " +
                  "regardless of type. Index 4 (Mythic) is never read — nothing empowers " +
                  "further, it's the max tier — kept only for the same \"always size 5, " +
                  "index by ItemRarity\" convention as WeaponDefinition.statsByRarity.")]
        public EmpowerTierCost[] empowerCostByRarity = new EmpowerTierCost[5];

        [Tooltip("Reroll doesn't change rarity, so its cost isn't tier-indexed — one " +
                  "shared list for any weapon at any rarity.")]
        public RecipeIngredient[] rerollIngredients = Array.Empty<RecipeIngredient>();
    }
}
