using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Inventory
{
    // Shared "square icon + text stack beside it" visual — the standardized look for
    // every equipment row in the game (the equip cluster's slots, and the equip-detail
    // screen's currently-equipped display and item list). Callers own interaction
    // (Button/click handling); this only builds the visual and hands back the pieces to
    // populate.
    public static class GearRowFactory
    {
        public readonly struct Row
        {
            public readonly Image Icon;
            public readonly Text CaptionText;
            public readonly Text TitleText;
            public readonly Text StatText;

            public Row(Image icon, Text captionText, Text titleText, Text statText)
            {
                Icon = icon;
                CaptionText = captionText;
                TitleText = titleText;
                StatText = statText;
            }
        }

        public static Row Create(Transform parent, float squareSize, int captionSize, int titleSize, int statSize)
        {
            var rowGo = new GameObject("GearRow", typeof(RectTransform));
            var rowRect = (RectTransform)rowGo.transform;
            rowRect.SetParent(parent, false);
            // Stretch to fill whatever parent it's given — without this it sits at
            // Unity's untouched 100x100 default (verified live via RectTransform dumps).
            rowRect.anchorMin = Vector2.zero;
            rowRect.anchorMax = Vector2.one;
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;

            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 16;
            rowLayout.padding = new RectOffset(12, 12, 8, 8);
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(rowGo.transform, false);
            SetFixedLayout(iconGo.AddComponent<LayoutElement>(), squareSize, squareSize);
            var icon = iconGo.AddComponent<Image>();

            var textColumnGo = new GameObject("TextColumn", typeof(RectTransform));
            textColumnGo.transform.SetParent(rowGo.transform, false);
            var textColumnLayoutElement = textColumnGo.AddComponent<LayoutElement>();
            // All four explicit — see the "unset LayoutElement property falls through to
            // a sibling component's own computed value" lesson from EquipSlotClusterView.
            textColumnLayoutElement.minWidth = 0;
            textColumnLayoutElement.preferredWidth = 0;
            textColumnLayoutElement.flexibleWidth = 1;
            textColumnLayoutElement.flexibleHeight = 0;

            var textColumnLayout = textColumnGo.AddComponent<VerticalLayoutGroup>();
            textColumnLayout.childAlignment = TextAnchor.MiddleLeft;
            textColumnLayout.childForceExpandWidth = true;
            textColumnLayout.childForceExpandHeight = false;
            textColumnLayout.childControlWidth = true;
            textColumnLayout.childControlHeight = true;

            var caption = CreateText(textColumnGo.transform, captionSize, FontStyle.Bold, new Color(1f, 1f, 1f, 0.55f));
            var title = CreateText(textColumnGo.transform, titleSize, FontStyle.Bold, Color.white);
            var stat = CreateText(textColumnGo.transform, statSize, FontStyle.Normal, new Color(1f, 1f, 1f, 0.7f));

            return new Row(icon, caption, title, stat);
        }

        private static void SetFixedLayout(LayoutElement layoutElement, float width, float height)
        {
            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            layoutElement.flexibleWidth = 0;
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleHeight = 0;
        }

        private static Text CreateText(Transform parent, int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            // The row itself (not individual text lines) handles taps.
            text.raycastTarget = false;

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.minHeight = fontSize + 10;
            layoutElement.preferredHeight = fontSize + 10;
            layoutElement.flexibleHeight = 0;

            return text;
        }
    }
}
