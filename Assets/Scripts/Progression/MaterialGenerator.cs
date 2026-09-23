using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Progression
{
    public static class MaterialGenerator
    {
        // Rarity read off item.rarity + rendered via GearRowFactory's existing
        // rarity-border support — no rarity prefix in the title, same convention as
        // WeaponGenerator.GenerateFromDefinition's named-weapon path. itemId is left
        // blank, which is the correct "clone on save/restore" path (see ItemData.itemId's
        // own doc comment) since every generated instance is genuinely unique.
        public static ItemData GenerateFromDefinition(MaterialDefinition definition, ItemRarity rarity)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.title = definition.materialName;
            item.category = ItemCategory.Material;
            item.rarity = rarity;
            item.icon = definition.icon;
            item.itemColor = definition.itemColor;
            item.statSummary = "Crafting material";
            item.goldValue = Mathf.RoundToInt(definition.goldValue *
                (RewardGenerator.RarityGoldValue[(int)rarity] / (float)RewardGenerator.RarityGoldValue[0]));
            item.materialSource = definition;
            return item;
        }
    }
}
