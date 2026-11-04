using System;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;

namespace DoomScroller.Combat
{
    public readonly struct WinOddsResult
    {
        // Raw win fraction 0-1; kept so the UI can choose whether to show a %.
        public readonly float WinFraction;
        public readonly WinOddsTier Tier;

        public WinOddsResult(float winFraction, WinOddsTier tier)
        {
            WinFraction = winFraction;
            Tier = tier;
        }
    }

    // Runs the real CombatSimulator many times to estimate win odds for a feed card.
    // Pure logic; no revive applied, player settings ignored.
    public static class WinOddsEstimator
    {
        public const int DefaultRunCount = 64;

        // Tier cut-offs; tune here.
        public const float LikelyThreshold = 0.70f;
        public const float RiskyThreshold = 0.40f;

        public static WinOddsTier TierFor(float winFraction)
        {
            if (winFraction >= LikelyThreshold)
            {
                return WinOddsTier.Likely;
            }

            return winFraction >= RiskyThreshold ? WinOddsTier.Risky : WinOddsTier.LongShot;
        }

        public static bool IsNonFight(EncounterType type)
        {
            return type == EncounterType.Treasure || type == EncounterType.Tavern ||
                   type == EncounterType.Resource || type == EncounterType.Landing ||
                   type == EncounterType.WanderingMerchant ||
                   // Dungeon handling will be revisited later; skips the sim for now.
                   type == EncounterType.Dungeon;
        }

        // Throws ArgumentOutOfRangeException when runCount <= 0.
        public static WinOddsResult Estimate(PlayerLoadout loadout, PlayerAttributes attributes, int playerStartHp,
            EncounterData data, Random source, int runCount = DefaultRunCount)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (runCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(runCount), "runCount must be positive.");
            }

            if (IsNonFight(data.type))
            {
                return new WinOddsResult(1f, WinOddsTier.Likely);
            }

            // Only fights touch these, so non-fight cards may pass null.
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            source ??= new Random();
            var wins = 0;
            for (var i = 0; i < runCount; i++)
            {
                // Fresh settings each run: auto-cast abilities, no revive.
                var sim = CombatResolver.CreateSimulator(loadout, attributes, new GameSettings(), data, playerStartHp,
                    new Random(source.Next()));
                sim.RunUntilDeathOrVictory();
                if (sim.IsEnemyDead)
                {
                    wins++;
                }
            }

            var fraction = (float)wins / runCount;
            return new WinOddsResult(fraction, TierFor(fraction));
        }

        public static WinOddsResult Estimate(PlayerLoadout loadout, PlayerAttributes attributes, int playerStartHp,
            EncounterData data, int seed, int runCount = DefaultRunCount)
        {
            return Estimate(loadout, attributes, playerStartHp, data, new Random(seed), runCount);
        }
    }
}
