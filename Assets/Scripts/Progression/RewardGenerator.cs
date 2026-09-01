using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Turns a variant's stated reward tier into an actual, immediately-equippable ItemData —
    // so what the encounter-detail screen promised is exactly what gets granted.
    public static class RewardGenerator
    {
        private static readonly string[] RarityNames = { "Common", "Uncommon", "Rare" };
        private static readonly int[] RarityPower = { 8, 14, 22 };
        private static readonly Color[] RarityColors =
        {
            new(0.7f, 0.7f, 0.7f, 1f),
            new(0.3f, 0.6f, 0.9f, 1f),
            new(0.8f, 0.6f, 0.15f, 1f)
        };

        public static ItemData GenerateItem(int rarityTierIndex, System.Random rng = null)
        {
            rng ??= new System.Random();
            rarityTierIndex = Mathf.Clamp(rarityTierIndex, 0, RarityNames.Length - 1);

            var slots = new[] { EquipmentSlotType.Weapon, EquipmentSlotType.Armor, EquipmentSlotType.Accessory };
            var slot = slots[rng.Next(slots.Length)];
            var rarityName = RarityNames[rarityTierIndex];
            var power = RarityPower[rarityTierIndex];

            var item = ScriptableObject.CreateInstance<ItemData>();
            item.title = $"{rarityName} {slot}";
            item.slotType = slot;
            item.statSummary = $"+{power} Power";
            item.powerValue = power;
            item.itemColor = RarityColors[rarityTierIndex];

            if (slot == EquipmentSlotType.Weapon)
            {
                item.abilities = new[]
                {
                    new WeaponAbilityData
                    {
                        abilityName = "Strike",
                        description = "A reliable basic attack",
                        powerBonus = power / 2
                    }
                };
            }

            return item;
        }
    }
}
