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
        public ItemCategory category;
        public string statSummary;
        public Color itemColor = Color.gray;

        [Tooltip("Flat gold granted by the Market's Sell tab — not discounted or scaled by " +
                  "anything (Expertise's GoldCostMultiplier only discounts spending). Exact " +
                  "balance is a placeholder, tuned later.")]
        public int goldValue = 5;

        [Tooltip("Combat power this item contributes while equipped (Phase 5 resolution). " +
                  "Defence, damage, etc. are all the same underlying number for now — " +
                  "StatLabel is purely which word to show next to it.")]
        public int powerValue = 10;

        [Tooltip("Only relevant when category is Weapon.")]
        public WeaponAbilityData[] abilities;

        [Tooltip("Only relevant when category is Weapon. Always active for the whole fight " +
                  "while this weapon is equipped — no cast or cooldown needed, unlike abilities.")]
        public StatusEffectData[] passiveEffects;

        [Tooltip("Only relevant when category is Weapon. Equipping a two-handed weapon " +
                  "auto-unequips and locks the other hand slot until this weapon is " +
                  "unequipped again.")]
        public bool isTwoHanded;

        [Tooltip("Only relevant when category is Weapon. Basic attacks per second in the " +
                  "combat simulation (Phase 8).")]
        public float attackSpeed = 1f;

        [Tooltip("Only relevant when category is Weapon. This weapon's basic attack fires " +
                  "a projectile at the target instead of a melee lunge (Phase 8).")]
        public bool isRanged;

        [Tooltip("Only relevant when category is Potion.")]
        public PotionEffect potionEffect;

        [Tooltip("Only relevant when category is Potion. MaxHealthRestore: how much " +
                  "current max HP it restores. Revive: the HP the player is revived with.")]
        public int potionAmount = 20;

        [Tooltip("Only relevant when category is Weapon. Which player attribute(s) this " +
                  "weapon's basic attack scales off, and by how much per point — not " +
                  "hardcoded to melee/Constitution, so a magic sword can also scale off " +
                  "Spirit, or a heavy bow off Constitution. Different tiers of the same " +
                  "weapon concept just author different coefficients.")]
        public AttributeScaling[] attributeScalingEntries;

        // Not persisted — a display-only lookup so combat math never has to know
        // "defence" and "damage" are the same field under different labels.
        public string StatLabel => ItemCategoryInfo.Label(category);
    }
}
