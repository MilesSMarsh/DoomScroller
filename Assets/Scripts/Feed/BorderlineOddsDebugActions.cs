#if UNITY_EDITOR || DEVELOPMENT_BUILD
using DoomScroller.Combat;
using UnityEngine;

namespace DoomScroller.Feed
{
    // The two debug actions shared by the Editor menu items and the in-game Debug panel.
    // Each returns the one-line result to show/log; `ok` is false when nothing changed.
    public static class BorderlineOddsDebugActions
    {
        public static string SetBorderlineHp(FeedController feed, out bool ok)
        {
            ok = false;
            if (feed == null)
            {
                return "No FeedController in the scene.";
            }

            var profile = feed.DebugProfile;
            var health = profile.Health;
            foreach (var card in feed.DebugVisibleFightCards())
            {
                if (BorderlineOddsFinder.TryFind(profile.Loadout, profile.Attributes, health.TrueMaxHealth,
                        card, Random.Range(0, int.MaxValue), out var r))
                {
                    health.DebugSetCurrentMaxHealth(r.Hp);
                    ok = true;
                    return $"{card.title}: {r.TierAtHp} -> {r.TierAfterPenalty} at {r.Hp} HP " +
                           $"(after penalty: {r.HpAfterPenalty} HP)";
                }
            }

            return "No card qualifies; nothing changed.";
        }

        public static string ApplyPenalty(FeedController feed)
        {
            if (feed == null)
            {
                return "No FeedController in the scene.";
            }

            feed.DebugProfile.Health.ApplyDefeatPenalty();
            return $"Defeat penalty applied: current max HP is now {feed.DebugProfile.Health.CurrentMaxHealth}.";
        }
    }
}
#endif
