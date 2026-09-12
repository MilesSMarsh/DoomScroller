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
        // How thick the rarity-colored ring around an icon reads — thin enough to stay
        // a border, not a second background.
        private const float BorderThickness = 4f;

        public readonly struct Row
        {
            public readonly Image Icon;
            public readonly Image Border;
            public readonly Text CaptionText;
            public readonly Text TitleText;
            public readonly Text StatText;

            public Row(Image icon, Image border, Text captionText, Text titleText, Text statText)
            {
                Icon = icon;
                Border = border;
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

            var (icon, border) = CreateIconFrame(rowGo.transform, squareSize);

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

            return new Row(icon, border, caption, title, stat);
        }

        // A rarity-colored frame behind an icon square, inset by BorderThickness on
        // every side — reads as a colored ring regardless of whether the icon ends up a
        // real sprite or the flat itemColor fallback square (a plain Outline component
        // only shows against transparent pixels, so it'd be invisible on an opaque
        // fallback square). Shared by Create() and any custom row layout that wants the
        // same icon+border treatment without GearRowFactory's own caption/title/stat
        // text column (e.g. VillageBlacksmithView's recipe/upgrade rows).
        public static (Image Icon, Image Border) CreateIconFrame(Transform parent, float squareSize)
        {
            var iconFrameGo = new GameObject("IconFrame", typeof(RectTransform));
            iconFrameGo.transform.SetParent(parent, false);
            SetFixedLayout(iconFrameGo.AddComponent<LayoutElement>(), squareSize, squareSize);
            var border = iconFrameGo.AddComponent<Image>();
            border.raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(iconFrameGo.transform, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(BorderThickness, BorderThickness);
            iconRect.offsetMax = new Vector2(-BorderThickness, -BorderThickness);
            var icon = iconGo.AddComponent<Image>();
            return (icon, border);
        }

        // Renders a real sprite when assigned, falling back to a flat tinted square
        // otherwise — same convention as EncounterData.artwork/cardColor. The one place
        // every gear row's icon should be set through, so real art and the greybox
        // fallback can never drift out of sync.
        public static void SetIcon(Image icon, Sprite sprite, Color fallbackColor)
        {
            icon.sprite = sprite;
            icon.color = sprite != null ? Color.white : fallbackColor;
        }

        // Item-aware overload — sets the icon AND its rarity-colored border in one call,
        // so a call site can't set one and forget the other. Pass null for a row that
        // isn't showing a concrete item right now (a locked/empty equip slot) to hide
        // the border rather than showing a stale or misleading rarity.
        public static void SetIcon(Row row, Sprite sprite, Color fallbackColor, ItemRarity? rarity)
        {
            SetIcon(row.Icon, sprite, fallbackColor);
            SetRarityBorder(row.Border, rarity);
        }

        public static void SetRarityBorder(Image border, ItemRarity? rarity)
        {
            border.color = rarity.HasValue ? ItemRarityInfo.ColorFor(rarity.Value) : Color.clear;
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
