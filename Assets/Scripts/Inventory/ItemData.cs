using UnityEngine;

namespace DoomScroller.Inventory
{
    // One equippable item. Instances are authored as assets, not hardcoded, so new
    // gear can be added without touching UI code (same pattern as Feed's EncounterData).
    [CreateAssetMenu(menuName = "DoomScroller/Item Data", fileName = "NewItem")]
    public class ItemData : ScriptableObject
    {
        public string title;
        public EquipmentSlotType slotType;
        public string statSummary;
        public Color itemColor = Color.gray;

        [Tooltip("Combat power this item contributes while equipped (Phase 5 resolution).")]
        public int powerValue = 10;

        [Tooltip("Only relevant when slotType is Weapon.")]
        public WeaponAbilityData[] abilities;
    }
}
