using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Feed
{
    // One feed "post" — a hand-authored asset, not a hardcoded card, so new encounters
    // can be added without touching UI code (Phase 2 acceptance criteria). Any asset of
    // this type dropped into Assets/Resources/Encounters/ is automatically picked up by
    // the feed (see FeedController) — no registration step needed.
    [CreateAssetMenu(menuName = "DoomScroller/Encounter Data", fileName = "NewEncounter")]
    public class EncounterData : ScriptableObject
    {
        public string title;
        public EncounterType type;

        [Tooltip("The card's full-bleed feed art. Export roughly matching the phone screen's " +
                  "portrait aspect ratio — it's stretched to fill, so a mismatched ratio will " +
                  "distort. Leave empty to fall back to a flat cardColor tile.")]
        public Sprite artwork;

        [Tooltip("Flat fallback color used when no artwork is assigned.")]
        public Color cardColor = Color.gray;

        [Tooltip("Reward text shown on the feed card, e.g. \"80 XP + Gear\".")]
        public string rewardSummary;

        [Tooltip("Base XP for the entry-level variant shown in the feed; harder in-encounter " +
                  "variants (Phase 3 sideways swipe) scale up from this.")]
        public int baseXpReward = 50;

        [Header("Combat Simulation (Phase 8)")]
        [Tooltip("Enemy HP for the live combat simulation. Ignored for Treasure.")]
        public int enemyHealth = 80;

        [Tooltip("Damage per enemy basic attack.")]
        public int enemyAttackDamage = 10;

        [Tooltip("Enemy basic attacks per second.")]
        public float enemyAttackSpeed = 0.8f;

        [Header("Damage Weaknesses")]
        [Tooltip("How this enemy's incoming damage is scaled per weapon DamageType. " +
                  "Unlisted types stay at a normal 1× — leave empty for no special " +
                  "weaknesses/resistances at all.")]
        public DamageTypeModifier[] damageTypeModifiers;

        [Header("Resource Encounter")]
        [Tooltip("Only relevant when type is Resource. The material instantly granted on entry.")]
        public ItemData resourceMaterial;

        [Tooltip("Only relevant when type is Resource. Units granted before scaling by the " +
                  "player's Expertise (PlayerAttributes.ResourceYieldMultiplier).")]
        public int baseResourceYield = 5;

        [Tooltip("Only relevant when type is Resource. An optional rarer secondary " +
                  "material with a small independent chance to also drop alongside " +
                  "resourceMaterial each time this encounter is entered. Leave unset for " +
                  "no secondary drop.")]
        public ItemData secondaryResourceMaterial;

        [Tooltip("Only relevant when secondaryResourceMaterial is set. Independent roll " +
                  "chance (0-1) each time this encounter is entered.")]
        [Range(0f, 1f)]
        public float secondaryResourceChance = 0.1f;

        [Tooltip("Only relevant when secondaryResourceMaterial is set. Units granted on a " +
                  "successful roll — not scaled by Expertise (a bonus find, not the main " +
                  "yield).")]
        public int secondaryResourceYield = 1;
    }
}
