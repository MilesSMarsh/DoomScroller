using System.Linq;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Armor/Talisman sibling of WeaponGenerator.GenerateFromDefinition — turns a
    // hand-authored EquipmentDefinition into a concrete, ownable ItemData at a specific
    // rarity. Kept separate from WeaponGenerator since armor/talismans aren't weapons
    // (no ability rolls, no attribute scaling), but shares its passive-roll helper/chance
    // so both can't drift apart.
    public static class EquipmentGenerator
    {
        public static ItemData GenerateFromDefinition(EquipmentDefinition definition, ItemRarity rarity, System.Random rng = null)
        {
            rng ??= new System.Random();
            var rarityIndex = (int)rarity;
            var power = definition.powerByRarity[rarityIndex];

            var item = ScriptableObject.CreateInstance<ItemData>();
            item.title = definition.equipmentName;
            item.category = definition.category;
            item.rarity = rarity;
            item.slotType = definition.slotType;
            item.itemColor = definition.itemColor;
            item.icon = definition.icon;
            item.powerValue = power;
            item.statSummary = $"+{power} {ItemCategoryInfo.Label(definition.category)}";

            item.passiveEffects = WeaponGenerator.RollPassives(definition.possiblePassives,
                WeaponGenerator.SkillCountByRarity[rarityIndex], rng);

            return item;
        }
    }
}
