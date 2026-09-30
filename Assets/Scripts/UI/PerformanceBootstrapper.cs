using UnityEngine;

namespace DoomScroller.UI
{
    // Nothing in this project ever sets a frame-rate cap, and the active Quality tier
    // ("Very Low") has vSyncCount: 0 — so with neither VSync nor a target frame rate in
    // play, Unity renders as many frames per second as the hardware allows, forever, for
    // a screen that's mostly static UI. On a desktop GPU that alone is enough to peg
    // usage at 100% sitting idle; on a real mobile device it burns battery/heat for no
    // visual benefit. 60 is a standard mobile-game target — smooth without chasing
    // whatever uncapped number the hardware happens to be capable of.
    public static class PerformanceBootstrapper
    {
        private const int TargetFrameRate = 60;

        public static void Apply()
        {
            // VSync (if a higher Quality tier ever turns it on) overrides targetFrameRate
            // entirely and locks to the display's own refresh rate instead — explicit 0
            // here means this cap actually takes effect regardless of which Quality tier
            // is active.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}
