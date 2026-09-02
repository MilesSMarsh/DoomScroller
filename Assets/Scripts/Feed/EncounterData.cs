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

        [Tooltip("Combat challenge rating for the entry-level variant (Phase 5 resolution). " +
                  "Ignored for Treasure — those never fight.")]
        public int baseChallenge = 40;
    }
}
