using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoomScroller.Feed
{
    // Greybox feed card, built entirely at runtime from EncounterData — no prefab asset yet.
    // Sized to exactly one page's height by FeedController, so each encounter fills the screen.
    public class FeedCardView : MonoBehaviour, IPointerClickHandler
    {
        private Image _background;
        private Text _titleText;
        private Text _badgeText;
        private Text _rewardText;
        private EncounterData _data;
        private Action<EncounterData> _onTapped;

        public static FeedCardView Create(Transform parent, EncounterData data, float height, Action<EncounterData> onTapped = null)
        {
            var go = new GameObject($"Card_{data.title}", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1f);

            var outerLayout = go.AddComponent<LayoutElement>();
            outerLayout.preferredHeight = height;
            outerLayout.minHeight = height;

            var background = go.AddComponent<Image>();

            var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(24, 24, 20, 20);
            layoutGroup.spacing = 8;
            layoutGroup.childAlignment = TextAnchor.MiddleLeft;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;

            var view = go.AddComponent<FeedCardView>();
            view._background = background;
            view._titleText = CreateLabel(rect, "Title", 36, FontStyle.Bold);
            view._badgeText = CreateLabel(rect, "Badge", 24, FontStyle.Italic);
            view._rewardText = CreateLabel(rect, "Reward", 28, FontStyle.Normal);
            view._onTapped = onTapped;

            view.Bind(data);
            return view;
        }

        public void Bind(EncounterData data)
        {
            _data = data;
            _background.color = data.cardColor;
            _titleText.text = data.title;
            _badgeText.text = $"{data.type} · {data.difficultyLabel}";
            _rewardText.text = data.rewardSummary;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onTapped?.Invoke(_data);
        }

        private static Text CreateLabel(Transform parent, string name, int fontSize, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = fontSize + 10;

            return text;
        }
    }
}
