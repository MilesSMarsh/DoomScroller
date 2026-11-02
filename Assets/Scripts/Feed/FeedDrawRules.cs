namespace DoomScroller.Feed
{
    // Pure draw-eligibility rules for the feed's shuffle bag, kept out of FeedController
    // so they're testable without a scene.
    public static class FeedDrawRules
    {
        // A player sitting at the max-HP floor is never served a new Ambush (it can't be
        // scrolled past, so a debuffed player would be forced into fights). Every other
        // type is unaffected.
        public static bool IsDrawable(EncounterType type, bool playerAtFloor)
        {
            return !(playerAtFloor && type == EncounterType.Ambush);
        }
    }
}
