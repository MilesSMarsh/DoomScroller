using DoomScroller.Inventory;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Tooltip("The creature portrait (Combat) or chest art (Treasure) shown INSIDE the " +
                  "encounter itself — see CombatPlaybackView's enemy portrait and " +
                  "RewardChestView's chest image. Distinct from artwork above, which is only " +
                  "ever the feed card's own background. Falls back to cardColor when unset, " +
                  "same convention as artwork.")]
        public Sprite foregroundArt;

        [Tooltip("Flat fallback color used when no artwork/foregroundArt is assigned.")]
        public Color cardColor = Color.gray;

        [FormerlySerializedAs("rewardSummary")]
        [Tooltip("Hand-authored flavor text shown on the feed card, e.g. \"Bandits close in " +
                  "from both sides — no escape.\" Not reward info — see " +
                  "FeedCardView.ExpectedRewardText/RewardGenerator.DescribeLootTable for the " +
                  "separate, auto-generated reward-expectation text.")]
        public string description;

        [Tooltip("Base XP for the entry-level variant shown in the feed; harder in-encounter " +
                  "variants (Phase 3 sideways swipe) scale up from this.")]
        public int baseXpReward = 50;

        [Header("Loot Table (Combat / Treasure only)")]
        [Tooltip("Drop chance per ItemRarity tier (index 0..4 = Common/Uncommon/Rare/" +
                  "Legendary/Mythic) — doesn't need to sum to exactly 100, RewardGenerator." +
                  "RollRarity normalizes against whatever the total actually is. All-zero " +
                  "(the default on every pre-existing asset) always rolls Common, matching " +
                  "this project's original behavior before loot tables existed. Not read at " +
                  "all for Dungeon (keeps its own hardcoded per-depth progression) or " +
                  "Resource (grants a fixed resourceMaterial instead of a rarity roll).")]
        public float[] lootTablePercentByRarity = new float[5];

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
