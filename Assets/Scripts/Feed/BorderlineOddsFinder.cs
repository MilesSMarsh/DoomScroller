using System;
using DoomScroller.Combat;
using DoomScroller.Inventory;
using DoomScroller.Progression;

namespace DoomScroller.Feed
{
    // Debug helper: finds the lowest current max HP at which a fight card still shows its
    // true-max tier but one defeat penalty would drop it a tier. Reuses the estimator's
    // seeded path; the same seed is used for every probe so results are deterministic.
    public static class BorderlineOddsFinder
    {
        public readonly struct Result
        {
            public readonly int Hp;
            public readonly WinOddsTier TierAtHp;
            public readonly int HpAfterPenalty;
            public readonly WinOddsTier TierAfterPenalty;

            public Result(int hp, WinOddsTier tierAtHp, int hpAfterPenalty, WinOddsTier tierAfterPenalty)
            {
                Hp = hp;
                TierAtHp = tierAtHp;
                HpAfterPenalty = hpAfterPenalty;
                TierAfterPenalty = tierAfterPenalty;
            }
        }

        // Returns false when no HP in [Floor, trueMaxHealth] qualifies (or a non-fight card).
        public static bool TryFind(PlayerLoadout loadout, PlayerAttributes attributes, int trueMaxHealth,
            EncounterData data, int seed, out Result result)
        {
            result = default;
            if (data == null || WinOddsEstimator.IsNonFight(data.type))
            {
                return false;
            }

            var floor = new PlayerHealth(trueMaxHealth, 0, 1f).Floor;
            WinOddsTier TierAt(int hp) => WinOddsEstimator.Estimate(loadout, attributes, hp, data, seed).Tier;

            var topTier = TierAt(trueMaxHealth);
            if (topTier == WinOddsTier.LongShot)
            {
                return false;
            }

            // Lowest HP whose tier still equals topTier (tier is near-monotonic in HP).
            var lo = floor;
            var hi = trueMaxHealth;
            while (lo < hi)
            {
                var mid = lo + (hi - lo) / 2;
                if (TierAt(mid) == topTier)
                {
                    hi = mid;
                }
                else
                {
                    lo = mid + 1;
                }
            }

            if (TierAt(lo) != topTier)
            {
                return false;
            }

            var afterPenalty = HpAfterPenalty(trueMaxHealth, lo);
            var tierAfter = TierAt(afterPenalty);
            if (tierAfter <= topTier)
            {
                return false;
            }

            result = new Result(lo, topTier, afterPenalty, tierAfter);
            return true;
        }

        // Uses PlayerHealth itself so the penalty/floor rules aren't duplicated here.
        private static int HpAfterPenalty(int trueMax, int currentMax)
        {
            var health = new PlayerHealth(trueMax, 0, 1f);
            health.LoadState(trueMax, currentMax, DateTime.UtcNow.Ticks);
            health.ApplyDefeatPenalty();
            return health.CurrentMaxHealth;
        }
    }
}
