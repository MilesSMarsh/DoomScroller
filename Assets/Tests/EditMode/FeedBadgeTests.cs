using DoomScroller.Combat;
using DoomScroller.Feed;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    public class FeedBadgeTests
    {
        [TestCase(WinOddsTier.Likely, "Likely")]
        [TestCase(WinOddsTier.Risky, "Risky")]
        [TestCase(WinOddsTier.LongShot, "Long shot")]
        public void LabelFor_MapsEachTier(WinOddsTier tier, string expected)
        {
            Assert.AreEqual(expected, FeedBadge.LabelFor(tier));
        }

        [TestCase(EncounterType.Treasure)]
        [TestCase(EncounterType.Tavern)]
        [TestCase(EncounterType.Resource)]
        [TestCase(EncounterType.Landing)]
        [TestCase(EncounterType.WanderingMerchant)]
        [TestCase(EncounterType.Dungeon)]
        public void IsVisible_FalseForNonFight(EncounterType type)
        {
            Assert.IsFalse(FeedBadge.IsVisible(type));
        }

        [TestCase(EncounterType.Enemy)]
        [TestCase(EncounterType.Ambush)]
        public void IsVisible_TrueForFights(EncounterType type)
        {
            Assert.IsTrue(FeedBadge.IsVisible(type));
        }

        [Test]
        public void ColorFor_ThreeDistinctColours()
        {
            var l = FeedBadge.ColorFor(WinOddsTier.Likely);
            var r = FeedBadge.ColorFor(WinOddsTier.Risky);
            var s = FeedBadge.ColorFor(WinOddsTier.LongShot);
            Assert.AreNotEqual(l, r);
            Assert.AreNotEqual(r, s);
            Assert.AreNotEqual(l, s);
        }
    }
}
