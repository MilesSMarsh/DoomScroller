using System;
using System.Collections.Generic;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Feed
{
    // Builds a vertically scrolling feed (Scroll View: Viewport/Content) at runtime
    // and populates it with one card per EncounterData entry.
    public class FeedController : MonoBehaviour
    {
        [SerializeField] private List<EncounterData> encounters = new();

        public event Action<EncounterData> EncounterSelected;

        private void Awake()
        {
            EventSystemBootstrapper.EnsureExists();
            BuildFeed();
        }

        private void BuildFeed()
        {
            var rect = (RectTransform)transform;

            var scrollRect = gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;

            var viewport = CreateStretchChild(rect, "Viewport");
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = CreateStretchChild(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            // No spacing/padding: each card is sized to exactly one viewport height below,
            // so cards must sit flush against each other to fill the screen with no gaps.
            var layoutGroup = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 0;
            layoutGroup.padding = new RectOffset(0, 0, 0, 0);
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            // Must be true: with this false, the group sizes itself off each card's
            // *current* rect (the untouched 100x100 default) instead of the card's
            // declared LayoutElement.preferredHeight, and never assigns it either.
            layoutGroup.childControlHeight = true;

            var sizeFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = content;

            // Force an immediate layout pass: viewport.rect wouldn't otherwise be
            // guaranteed to reflect the real screen size this early (same-frame Awake).
            Canvas.ForceUpdateCanvases();
            var pageHeight = viewport.rect.height;
            var cardCount = 0;
            foreach (var encounter in encounters)
            {
                if (encounter == null)
                {
                    continue;
                }

                FeedCardView.Create(content, encounter, pageHeight, e => EncounterSelected?.Invoke(e));
                cardCount++;
            }

            // Content's height is only computed lazily by VerticalLayoutGroup/ContentSizeFitter;
            // force it to resolve now so ScrollRect sees the true scrollable range from frame one.
            Canvas.ForceUpdateCanvases();

            var pager = gameObject.AddComponent<FeedPager>();
            pager.Configure(pageHeight, cardCount);
        }

        private static RectTransform CreateStretchChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
