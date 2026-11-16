using System;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Plain C# so gold math is testable without a running scene — same shape as
    // PlayerHealth/PlayerProgression. Gold is earned from combat rewards
    // (RewardGenerator.ComputeGoldReward) and spent at the Village (potion refills,
    // the Market).
    public class PlayerWallet
    {
        public int Gold { get; private set; }

        public event Action Changed;

        // Tells listeners (screens, the feed) that state was replaced by a server load.
        // LoadState/RestoreFrom stay silent because they also run before anything subscribes.
        public void NotifyLoaded() => Changed?.Invoke();

        public PlayerWallet(int startingGold)
        {
            Gold = Mathf.Max(0, startingGold);
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Gold += amount;
            Changed?.Invoke();
        }

        // False (and no state change) if funds are insufficient — callers never need to
        // check Gold themselves first.
        public bool TrySpendGold(int amount)
        {
            if (amount <= 0 || Gold < amount)
            {
                return false;
            }

            Gold -= amount;
            Changed?.Invoke();
            return true;
        }

        // The save/load path. Doesn't fire Changed — called once, before anything
        // subscribes.
        public void LoadState(int gold)
        {
            Gold = gold;
        }
    }
}
