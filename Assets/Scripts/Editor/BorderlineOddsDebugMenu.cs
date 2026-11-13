using DoomScroller.Feed;
using UnityEditor;
using UnityEngine;

namespace DoomScroller.Editor
{
    // Play Mode debug helpers for checking that feed odds badges refresh after a defeat.
    // Logic lives in BorderlineOddsDebugActions (shared with the in-game Debug panel).
    public static class BorderlineOddsDebugMenu
    {
        private const string BorderlinePath = "DoomScroller/Debug/Borderline Odds HP";
        private const string PenaltyPath = "DoomScroller/Debug/Apply Defeat Penalty";

        [MenuItem(BorderlinePath)]
        public static void SetBorderlineHp()
        {
            var message = BorderlineOddsDebugActions.SetBorderlineHp(
                Object.FindFirstObjectByType<FeedController>(), out var ok);
            if (ok)
            {
                Debug.Log("Borderline Odds HP: " + message);
            }
            else
            {
                Debug.LogWarning("Borderline Odds HP: " + message);
            }
        }

        [MenuItem(BorderlinePath, true)]
        private static bool ValidateBorderline() => Application.isPlaying;

        [MenuItem(PenaltyPath)]
        public static void ApplyPenalty()
        {
            Debug.Log("Apply Defeat Penalty: " +
                      BorderlineOddsDebugActions.ApplyPenalty(Object.FindFirstObjectByType<FeedController>()));
        }

        [MenuItem(PenaltyPath, true)]
        private static bool ValidatePenalty() => Application.isPlaying;
    }
}
