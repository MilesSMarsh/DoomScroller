using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.UI
{
    // Builds a vertical Scroll View (ScrollRect + Viewport + Content, sized to fill
    // whatever flexible space its parent's LayoutGroup gives it) — the same
    // Viewport/Content/ContentSizeFitter shape FeedController already uses for its card
    // list, factored out so any panel whose content can outgrow the screen (equip
    // cluster + abilities, combat resolution + message board, a slot's item list) shrinks
    // to scroll instead of clipping on a short or narrow aspect ratio. Returns Content —
    // build the panel's actual rows as children of that, not of `parent`.
    public static class ScrollAreaFactory
    {
        public static RectTransform Create(Transform parent, RectOffset contentPadding, float spacing)
        {
            var scrollAreaGo = new GameObject("ScrollArea", typeof(RectTransform));
            scrollAreaGo.transform.SetParent(parent, false);
            var scrollAreaLayout = scrollAreaGo.AddComponent<LayoutElement>();
            scrollAreaLayout.minHeight = 0;
            scrollAreaLayout.preferredHeight = 0;
            scrollAreaLayout.flexibleHeight = 1;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            var viewportRect = (RectTransform)viewportGo.transform;
            viewportRect.SetParent(scrollAreaGo.transform, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            var viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            var contentRect = (RectTransform)contentGo.transform;
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;

            var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = contentPadding;
            contentLayout.spacing = spacing;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;

            var sizeFitter = contentGo.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollAreaGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            return contentRect;
        }
    }
}
