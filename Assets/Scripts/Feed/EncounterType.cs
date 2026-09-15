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
        Landing,

        // A small, gold-priced shop that rerolls its stock every time a *new* sighting
        // is drawn from the feed's shuffle bag, but stays stable if you leave and come
        // back to that same sighting — see FeedController.EnsureFilledThrough's
        // per-sighting Instantiate and WanderingMerchantView's stock cache.
        WanderingMerchant,

        // Combat-wise identical to Enemy, but the feed physically refuses to let the
        // player scroll past an unresolved one in either direction (see
        // FeedController.OnPageRequested/ResolveAmbushBlockedDestination) until it's won
        // or lost — no Leave button either (CombatResolutionView.Show). Resolved the
        // instant FeedController.MarkWon/MarkLost locks the card, same as any other fight.
        Ambush
    }
}
