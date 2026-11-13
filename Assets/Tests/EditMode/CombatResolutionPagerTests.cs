using System.Reflection;
using DoomScroller.Encounters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DoomScroller.Tests.EditMode
{
    // Drives the real OnBeginDrag / OnDrag / OnEndDrag handlers of CombatResolutionView
    // on a bare instance (no scene, no UI prefab). Private pager state is set through
    // reflection, the same state Show() and RevealSequence() would set.
    public class CombatResolutionPagerTests
    {
        private const float W = 1000f;
        private const float StartX = 500f;
        private const float StartY = 500f;

        private GameObject _go;
        private CombatResolutionView _view;
        private RectTransform _content;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CombatResolutionPagerTest", typeof(RectTransform));
            _view = _go.AddComponent<CombatResolutionView>();
            _content = _go.GetComponent<RectTransform>();
            Set("_content", _content);
            Set("_pageWidth", W);
            Set("_currentPage", 0);
            Set("_minPage", 0);
            Set("_canReachRewardPage", false);
            Set("_isSnapping", false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        // Fight playing, or after a defeat: the reward page is locked (canReach = false).
        [Test]
        public void FightOrDefeat_LeftwardDrag_DoesNotMoveContent()
        {
            Drag(-300f, 0f);
            Assert.AreEqual(0f, _content.anchoredPosition.x, 0.001f);
        }

        [Test]
        public void FightOrDefeat_LargeLeftwardDrag_DoesNotMoveContent()
        {
            Drag(-5000f, 0f);
            Assert.AreEqual(0f, _content.anchoredPosition.x, 0.001f);
        }

        [Test]
        public void FightOrDefeat_LeftwardDragAndRelease_StaysOnFightPage()
        {
            Drag(-300f, 0f);
            Release(-300f, 0f);
            Assert.AreEqual(0, Get<int>("_currentPage"));
            Assert.AreEqual(0f, Get<float>("_targetX"), 0.001f);
        }

        [Test]
        public void FightOrDefeat_RightwardDrag_DoesNotMoveContent()
        {
            Drag(300f, 0f);
            Assert.AreEqual(0f, _content.anchoredPosition.x, 0.001f);
        }

        // Victory: the reward page is reachable, and the swipe behaves as before.
        [Test]
        public void Victory_LeftwardDrag_FollowsFinger()
        {
            Set("_canReachRewardPage", true);
            Drag(-300f, 0f);
            Assert.AreEqual(-300f, _content.anchoredPosition.x, 0.001f);
        }

        [Test]
        public void Victory_LeftwardDragBeyondPage_ClampsAtRewardPage()
        {
            Set("_canReachRewardPage", true);
            Drag(-5000f, 0f);
            Assert.AreEqual(-W, _content.anchoredPosition.x, 0.001f);
        }

        [Test]
        public void Victory_SwipeOverThreshold_SnapsToRewardPage()
        {
            Set("_canReachRewardPage", true);
            Drag(-300f, 0f);
            Release(-300f, 0f);
            Assert.AreEqual(1, Get<int>("_currentPage"));
            Assert.AreEqual(-W, Get<float>("_targetX"), 0.001f);
        }

        [Test]
        public void Victory_SwipeUnderThreshold_SnapsBackToFightPage()
        {
            Set("_canReachRewardPage", true);
            Drag(-50f, 0f);
            Release(-50f, 0f);
            Assert.AreEqual(0, Get<int>("_currentPage"));
            Assert.AreEqual(0f, Get<float>("_targetX"), 0.001f);
        }

        [Test]
        public void Victory_RightwardDragFromRewardPage_FollowsFinger()
        {
            Set("_canReachRewardPage", true);
            Set("_currentPage", 1);
            _content.anchoredPosition = new Vector2(-W, 0f);
            Drag(300f, 0f);
            Assert.AreEqual(-W + 300f, _content.anchoredPosition.x, 0.001f);
        }

        [Test]
        public void Victory_RightwardDragPastFightPage_ClampsAtFightPage()
        {
            Set("_canReachRewardPage", true);
            Set("_currentPage", 1);
            _content.anchoredPosition = new Vector2(-W, 0f);
            Drag(5000f, 0f);
            Assert.AreEqual(0f, _content.anchoredPosition.x, 0.001f);
        }

        // Treasure card: pinned on the chest page (min = max = 1).
        [Test]
        public void Treasure_RightwardDrag_CannotShowEmptyFightPage()
        {
            Set("_minPage", 1);
            Set("_currentPage", 1);
            Set("_canReachRewardPage", false);
            _content.anchoredPosition = new Vector2(-W, 0f);
            Drag(300f, 0f);
            Assert.AreEqual(-W, _content.anchoredPosition.x, 0.001f);
        }

        [Test]
        public void Treasure_LargeRightwardDrag_CannotShowEmptyFightPage()
        {
            Set("_minPage", 1);
            Set("_currentPage", 1);
            _content.anchoredPosition = new Vector2(-W, 0f);
            Drag(5000f, 0f);
            Assert.AreEqual(-W, _content.anchoredPosition.x, 0.001f);
        }

        [Test]
        public void Treasure_LeftwardDrag_StaysPinned()
        {
            Set("_minPage", 1);
            Set("_currentPage", 1);
            _content.anchoredPosition = new Vector2(-W, 0f);
            Drag(-300f, 0f);
            Assert.AreEqual(-W, _content.anchoredPosition.x, 0.001f);
        }

        [Test]
        public void Treasure_RightwardDragAndRelease_StaysOnChestPage()
        {
            Set("_minPage", 1);
            Set("_currentPage", 1);
            _content.anchoredPosition = new Vector2(-W, 0f);
            Drag(300f, 0f);
            Release(300f, 0f);
            Assert.AreEqual(1, Get<int>("_currentPage"));
            Assert.AreEqual(-W, Get<float>("_targetX"), 0.001f);
        }

        // Vertical drags never move the pager, even after a victory.
        [Test]
        public void Victory_VerticalDrag_DoesNotMoveContent()
        {
            Set("_canReachRewardPage", true);
            Drag(0f, -300f);
            Assert.AreEqual(0f, _content.anchoredPosition.x, 0.001f);
        }

        private void Begin()
        {
            _view.OnBeginDrag(MakeEvent(StartX, StartY));
        }

        // Starts a drag at the origin point and moves the pointer by (dx, dy).
        private void Drag(float dx, float dy)
        {
            Begin();
            _view.OnDrag(MakeEvent(StartX + dx, StartY + dy));
        }

        private void Release(float dx, float dy)
        {
            _view.OnEndDrag(MakeEvent(StartX + dx, StartY + dy));
        }

        private static PointerEventData MakeEvent(float x, float y)
        {
            return new PointerEventData(null) { position = new Vector2(x, y) };
        }

        private void Set(string field, object value)
        {
            FieldInfo(field).SetValue(_view, value);
        }

        private T Get<T>(string field)
        {
            return (T)FieldInfo(field).GetValue(_view);
        }

        private static FieldInfo FieldInfo(string name)
        {
            var f = typeof(CombatResolutionView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"CombatResolutionView has no field {name}");
            return f;
        }
    }
}
