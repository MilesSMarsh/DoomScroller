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
                  "variants (Phase 3 sideways swipe) scale up from this. For Dungeon, this " +
                  "is the guaranteed end-of-run reward's XP — combat rooms along the way " +
                  "grant none.")]
        public int baseXpReward = 50;

        [Tooltip("0 (every asset's default before this field existed) means derive gold " +
                  "from baseXpReward the old way (RewardGenerator.ComputeGoldReward) — any " +
                  "positive value overrides it. For Dungeon, this is the guaranteed " +
                  "end-of-run reward's gold.")]
        public int goldReward;

        [Header("Loot Table")]
        [Tooltip("For Combat/Treasure this drives that encounter's own reward; for " +
                  "Dungeon it drives only the guaranteed end-of-run reward (see " +
                  "dungeonRooms below for each room's own, separate loot table). Not read " +
                  "at all for Resource (see resourceThresholds below instead).")]
        public LootTable lootTable = new();

        [Header("Combat Simulation — Combat (Enemy/Ambush) only")]
        [Tooltip("Enemy HP for the live combat simulation.")]
        public int enemyHealth = 80;

        [Tooltip("Damage per enemy basic attack.")]
        public int enemyAttackDamage = 10;

        [Tooltip("Enemy basic attacks per second.")]
        public float enemyAttackSpeed = 0.8f;

        [Header("Damage Weaknesses — Combat only")]
        [Tooltip("How incoming damage is scaled per weapon DamageType. Unlisted types stay " +
                  "at a normal 1× — leave empty for no special weaknesses/resistances at " +
                  "all. Not used by Dungeon — each Fight room in dungeonRooms below has its " +
                  "own, separate weaknesses instead.")]
        public DamageTypeModifier[] damageTypeModifiers;

        [Header("Dungeon Rooms — Dungeon only")]
        [Tooltip("The authored, ordered room list a run is built from (see DungeonGenerator) " +
                  "— replaces any growth-formula scaling entirely; each room's own numbers " +
                  "are exactly what plays out. The guaranteed end-of-run reward (XP/gold/" +
                  "loot table above) is separate from this list and always happens when the " +
                  "run is completed, regardless of which of these rolled in.")]
        public DungeonRoomData[] dungeonRooms;

        [Header("Resource Minigame — Resource only")]
        [Tooltip("Only relevant when type is Resource. How long the timed damage window " +
                  "lasts, in seconds — the player's equipped weapon(s) auto-attack this " +
                  "node (which never attacks back) for exactly this long.")]
        public float resourceTimerSeconds = 10f;

        [Tooltip("Only relevant when type is Resource. How incoming damage is scaled per " +
                  "weapon DamageType — same mechanism as Combat's damageTypeModifiers, " +
                  "lets a better-suited weapon deal more damage to this node.")]
        public DamageTypeModifier[] resourceWeaknesses;

        [Tooltip("Only relevant when type is Resource. Reward tiers by total damage dealt " +
                  "in the timed window — every threshold whose damageRequired is met grants " +
                  "its own reward (they stack, not just the highest one reached), scaled by " +
                  "the player's Expertise (PlayerAttributes.ResourceYieldMultiplier).")]
        public ResourceThreshold[] resourceThresholds;
    }
}
