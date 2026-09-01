using UnityEngine;

namespace DoomScroller.Feed
{
    // One feed "post". Cards are built from instances of this asset, not hardcoded,
    // so new encounters can be added without touching UI code (Phase 2 acceptance criteria).
    [CreateAssetMenu(menuName = "DoomScroller/Encounter Data", fileName = "NewEncounter")]
    public class EncounterData : ScriptableObject
    {
        public string title;
        public EncounterType type;
        public int difficultyTier = 1;
        public string difficultyLabel = "Standard";
        public string rewardSummary;
        public Color cardColor = Color.gray;

        [Tooltip("Base XP for the entry-level variant shown in the feed; harder in-encounter " +
                  "variants (Phase 3 sideways swipe) scale up from this.")]
        public int baseXpReward = 50;

        [Tooltip("Combat challenge rating for the entry-level variant (Phase 5 resolution). " +
                  "Ignored for Treasure — those never fight.")]
        public int baseChallenge = 40;
    }
}
