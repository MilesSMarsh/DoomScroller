namespace DoomScroller.Feed
{
    // Ordinal-serialized by Unity — always append new values at the end, never insert,
    // or existing EncounterData assets silently reinterpret to the wrong type.
    public enum EncounterType
    {
        Treasure,
        Enemy,
        Dungeon,
        Tavern,
        Resource,

        // A blank, non-interactive card shown once, at the very start of the feed, on
        // first open — see FeedController.LoadPool/BuildFeed. Never drawn by the normal
        // shuffle bag.
        Landing
    }
}
