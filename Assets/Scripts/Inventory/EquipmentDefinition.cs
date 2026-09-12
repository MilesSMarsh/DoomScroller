using UnityEngine;

namespace DoomScroller.Inventory
{
    // A hand-authored Armor/Talisman *concept* — the non-weapon sibling of
    // WeaponDefinition, same shape minus the weapon-only fields (no weaponType/
    // damageType/attributeScalingEntries; armor and talismans don't scale off attributes
    // or deal typed damage today). Listed/edited by the Equipment Menu window
    // (Assets/Editor/EquipmentMenuWindow.cs); turned into a concrete ItemData at a
    // specific rarity by EquipmentGenerator.GenerateFromDefinition.
    [CreateAssetMenu(menuName = "DoomScroller/Equipment Definition", fileName = "NewEquipment")]
    public class EquipmentDefinition : ScriptableObject
    {
        public string equipmentName;

        [Tooltip("Armor or Talisman — gates which EquipmentSlotType values are valid " +
                  "(see ItemCategoryInfo.SlotsFor).")]
        public ItemCategory category;

        public EquipmentSlotType slotType;
        public Sprite icon;
        public Color itemColor = Color.gray;

        [Tooltip("Power (defence) for each rarity tier — always 5 entries, index 0..4 = " +
                  "Common/Uncommon/Rare/Legendary/Mythic.")]
        public int[] powerByRarity = new int[5];

        [Tooltip("The pool this piece can roll a passive \"effect\" from when generated " +
                  "(same independent-chance roll WeaponGenerator uses, and the same " +
                  "PassiveDefinition catalog weapons draw from — not gated by " +
                  "allowedWeaponTypes, which is a weapon-specific concept). Not yet read " +
                  "by combat math; authored/rolled only for now.")]
        public PassiveDefinition[] possiblePassives;
    }
}
