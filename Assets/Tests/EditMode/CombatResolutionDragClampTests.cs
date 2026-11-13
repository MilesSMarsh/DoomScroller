using DoomScroller.Encounters;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    public class CombatResolutionDragClampTests
    {
        private const float W = 1000f;

        [Test]
        public void Locked_ClampsLeftwardDragToZero()
        {
            var max = CombatResolutionView.MaxReachablePage(0, false);
            Assert.AreEqual(0, max);
            Assert.AreEqual(0f, CombatResolutionView.ClampDragX(-300f, 0, max, W));
            Assert.AreEqual(0f, CombatResolutionView.ClampDragX(-5000f, 0, max, W));
        }

        [Test]
        public void Locked_ClampsRightwardDragToZero()
        {
            Assert.AreEqual(0f, CombatResolutionView.ClampDragX(200f, 0, 0, W));
        }

        [Test]
        public void Unlocked_AllowsDownToMinusPageWidth()
        {
            var max = CombatResolutionView.MaxReachablePage(0, true);
            Assert.AreEqual(1, max);
            Assert.AreEqual(-300f, CombatResolutionView.ClampDragX(-300f, 0, max, W));
            Assert.AreEqual(-W, CombatResolutionView.ClampDragX(-5000f, 0, max, W));
            Assert.AreEqual(0f, CombatResolutionView.ClampDragX(400f, 0, max, W));
        }

        [Test]
        public void Treasure_PinsToMinusPageWidthBothWays()
        {
            var max = CombatResolutionView.MaxReachablePage(1, false);
            Assert.AreEqual(1, max);
            Assert.AreEqual(-W, CombatResolutionView.ClampDragX(-W + 300f, 1, max, W));
            Assert.AreEqual(-W, CombatResolutionView.ClampDragX(-W - 300f, 1, max, W));
            Assert.AreEqual(-W, CombatResolutionView.ClampDragX(-W + 300f, 1, 1, W));
        }
    }
}
