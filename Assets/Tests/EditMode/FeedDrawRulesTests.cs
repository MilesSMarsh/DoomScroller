using DoomScroller.Feed;
using DoomScroller.Progression;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    public class FeedDrawRulesTests
    {
        // TrueMax 100 -> Floor 20.
        private static PlayerHealth MakeHealth() => new PlayerHealth(100, 10, 30f);

        [Test]
        public void IsAtFloor_FalseAtFullHealth()
        {
            Assert.IsFalse(MakeHealth().IsAtFloor);
        }

        [Test]
        public void IsAtFloor_FalseJustAboveFloor()
        {
            var health = MakeHealth();
            health.LoadState(100, 21, health.LastRegenUtcTicks);
            Assert.IsFalse(health.IsAtFloor);
        }

        [Test]
        public void IsAtFloor_TrueAtFloor()
        {
            var health = MakeHealth();
            health.LoadState(100, 20, health.LastRegenUtcTicks);
            Assert.IsTrue(health.IsAtFloor);
        }

        [Test]
        public void IsAtFloor_TrueAfterRepeatedDefeats()
        {
            var health = MakeHealth();
            for (var i = 0; i < 5; i++)
            {
                health.ApplyDefeatPenalty();
            }

            Assert.IsTrue(health.IsAtFloor);
        }

        [Test]
        public void Ambush_BlockedAtFloor_AllowedAbove()
        {
            Assert.IsFalse(FeedDrawRules.IsDrawable(EncounterType.Ambush, true));
            Assert.IsTrue(FeedDrawRules.IsDrawable(EncounterType.Ambush, false));
        }

        [TestCase(EncounterType.Enemy)]
        [TestCase(EncounterType.Treasure)]
        [TestCase(EncounterType.Resource)]
        [TestCase(EncounterType.Dungeon)]
        [TestCase(EncounterType.WanderingMerchant)]
        [TestCase(EncounterType.Tavern)]
        public void OtherTypes_AllowedAtFloor(EncounterType type)
        {
            Assert.IsTrue(FeedDrawRules.IsDrawable(type, true));
            Assert.IsTrue(FeedDrawRules.IsDrawable(type, false));
        }
    }
}
