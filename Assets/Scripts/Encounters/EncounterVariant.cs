namespace DoomScroller.Encounters
{
    // One difficulty step of an encounter, surfaced by sideways-swiping before committing.
    public readonly struct EncounterVariant
    {
        public int TierIndex { get; }
        public string DifficultyLabel { get; }
        public string RewardSummary { get; }
        public int XpReward { get; }

        public EncounterVariant(int tierIndex, string difficultyLabel, string rewardSummary, int xpReward)
        {
            TierIndex = tierIndex;
            DifficultyLabel = difficultyLabel;
            RewardSummary = rewardSummary;
            XpReward = xpReward;
        }
    }
}
