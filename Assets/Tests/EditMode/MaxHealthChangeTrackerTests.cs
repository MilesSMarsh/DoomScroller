using DoomScroller.Feed;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    public class MaxHealthChangeTrackerTests
    {
        [Test]
        public void FirstValue_AlwaysCounts()
        {
            Assert.IsTrue(new MaxHealthChangeTracker().ShouldRefresh(100));
        }

        [Test]
        public void Decrease_Refreshes()
        {
            var t = new MaxHealthChangeTracker();
            t.ShouldRefresh(100);
            Assert.IsTrue(t.ShouldRefresh(75));
        }

        [Test]
        public void Increase_Refreshes()
        {
            var t = new MaxHealthChangeTracker();
            t.ShouldRefresh(50);
            Assert.IsTrue(t.ShouldRefresh(75));
        }

        [Test]
        public void UnchangedValue_DoesNotRefresh()
        {
            var t = new MaxHealthChangeTracker();
            t.ShouldRefresh(100);
            Assert.IsFalse(t.ShouldRefresh(100));
            Assert.IsFalse(t.ShouldRefresh(100));
        }
    }
}
