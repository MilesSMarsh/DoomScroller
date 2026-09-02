using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoomScroller.Feed
{
    // Snaps the feed's ScrollRect to the nearest full-screen card after each drag,
    // so swiping moves exactly one encounter at a time (Reels/TikTok-style paging).
    [RequireComponent(typeof(ScrollRect))]
    public class FeedPager : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        private const float SnapSpeed = 12f;
        private const float SnapThreshold = 0.5f;
        private const float FlickVelocityThreshold = 400f;

        private ScrollRect _scrollRect;
        private float _pageHeight;
        private int _pageCount;
        private float _targetY;
        private bool _isSnapping;
        private Func<int, int> _onPageRequested;

        public void Configure(float pageHeight, int pageCount)
        {
            _pageHeight = pageHeight;
            _pageCount = Mathf.Max(pageCount, 1);
        }

        // Called with the drag's raw destination page before it's committed to, so the
        // feed can generate ahead / trim behind (and shift content to compensate) first.
        // Returns the (possibly re-based) local page index to actually snap to.
        public void SetPageRequestHandler(Func<int, int> handler)
        {
            _onPageRequested = handler;
        }

        private void Awake()
        {
            _scrollRect = GetComponent<ScrollRect>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Let the player's new drag take over immediately instead of fighting
            // a still-settling snap from the previous swipe.
            _isSnapping = false;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_pageHeight <= 0f || _scrollRect.content == null)
            {
                return;
            }

            var currentY = _scrollRect.content.anchoredPosition.y;
            var rawPage = currentY / _pageHeight;
            var velocityY = _scrollRect.velocity.y;

            int page;
            if (Mathf.Abs(velocityY) > FlickVelocityThreshold)
            {
                // A quick flick advances a page even before crossing the halfway point.
                // Content's anchoredPosition.y increases as you swipe up toward later pages,
                // so positive velocity means advancing (round up); negative means retreating.
                page = velocityY > 0 ? Mathf.CeilToInt(rawPage) : Mathf.FloorToInt(rawPage);
            }
            else
            {
                page = Mathf.RoundToInt(rawPage);
            }

            page = Mathf.Max(page, 0);

            if (_onPageRequested != null)
            {
                page = _onPageRequested(page);
            }

            page = Mathf.Clamp(page, 0, _pageCount - 1);
            _targetY = page * _pageHeight;
            _isSnapping = true;
            _scrollRect.velocity = Vector2.zero;
        }

        private void LateUpdate()
        {
            if (!_isSnapping || _scrollRect.content == null)
            {
                return;
            }

            var pos = _scrollRect.content.anchoredPosition;
            pos.y = Mathf.Lerp(pos.y, _targetY, Time.unscaledDeltaTime * SnapSpeed);

            if (Mathf.Abs(pos.y - _targetY) < SnapThreshold)
            {
                pos.y = _targetY;
                _isSnapping = false;
            }

            _scrollRect.content.anchoredPosition = pos;
            _scrollRect.velocity = Vector2.zero;
        }
    }
}
