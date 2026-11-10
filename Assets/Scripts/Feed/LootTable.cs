using System;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Feed
{
    // Shared by EncounterData and DungeonRoomData — every place a reward gets rolled
    // from. See RewardGenerator.RollLootTable/DescribeLootTable for the actual roll and
    // preview text.
    [Serializable]
    public class LootTable
    {
        [Tooltip("Drop chance per ItemRarity tier (index 0..4 = Plain/Tempered/Runed/" +
                  "Relic/Exalted) — doesn't need to sum to exactly 100, RewardGenerator." +
                  "RollRarity normalizes against whatever the total actually is. All-zero " +
                  "(the default) always rolls Plain.")]
        public float[] percentByRarity = new float[5];

        [Tooltip("How many separate items one reward grants — each rolled independently " +
                  "through this same table (its own rarity roll, its own pick from " +
                  "weapons/equipment below). 1/1 grants exactly one, same as every table " +
                  "before this field existed.")]
        public int minItemCount = 1;

        [Tooltip("See minItemCount. Must be >= minItemCount.")]
        public int maxItemCount = 1;

        [Tooltip("Specific authored weapons this table can roll, at whichever rarity the " +
                  "percentages above land on (WeaponGenerator.GenerateFromDefinition) — " +
                  "combined with equipment below into one pool an individual roll picks " +
                  "uniformly from. Both empty (the default) falls back to a fully generic " +
                  "archetype roll (RewardGenerator.GenerateItem).")]
        public WeaponDefinition[] weapons;

        [Tooltip("Specific authored Armor/Talisman this table can roll, same rules as " +
                  "weapons above (EquipmentGenerator.GenerateFromDefinition).")]
        public EquipmentDefinition[] equipment;

        [Tooltip("Specific authored crafting materials this table can roll, at whichever " +
                  "rarity the percentages above land on (MaterialGenerator." +
                  "GenerateFromDefinition) — combined with weapons/equipment above into " +
                  "one pool an individual roll picks uniformly from.")]
        public MaterialDefinition[] materials;
    }
}
