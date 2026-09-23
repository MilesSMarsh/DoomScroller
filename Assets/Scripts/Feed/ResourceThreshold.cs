using System;
using DoomScroller.Inventory;

namespace DoomScroller.Feed
{
    // One reward tier for a Resource encounter's timed minigame — every threshold whose
    // damageRequired is met by the player's total damage dealt grants its own reward
    // (thresholds stack, they aren't "highest tier only" — see RewardGenerator/
    // ResourceEncounterView for the actual resolution).
    [Serializable]
    public class ResourceThreshold
    {
        public int damageRequired;
        public MaterialDefinition rewardMaterial;
        public ItemRarity rewardRarity;
        public int rewardAmount = 1;
    }
}
