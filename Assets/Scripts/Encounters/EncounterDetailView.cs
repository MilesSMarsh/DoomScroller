using System;
using System.Collections.Generic;
using DoomScroller.Feed;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // Full-screen "you're entering this encounter" panel. Sideways swipe cycles through
    // progressively harder/better-reward variants before the player commits.
    public class EncounterDetailView : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        private const float SwipeThreshold = 80f;

        public event Action<EncounterData, EncounterVariant> Committed;
        public event Action BackRequested;

        private Image _background;
        private Text _titleText;
        private Text _difficultyText;
        private Text _rewardText;
        private Text _hintText;

        private EncounterData _data;
        private IReadOnlyList<EncounterVariant> _variants;
        private int _variantIndex;
        private Vector2 _dragStart;

        public static EncounterDetailView Create(Transform parent)
        {
            var go = new GameObject("EncounterDetail", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = go.AddComponent<Image>();
            background.color = new Color(0.1f, 0.1f, 0.12f, 1f);

            var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(48, 48, NavBarController.Height + 20, 0);
            layoutGroup.spacing = 16;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;

            var view = go.AddComponent<EncounterDetailView>();
            view._background = background;
            view._titleText = CreateLabel(rect, "Title", 48, FontStyle.Bold, TextAnchor.MiddleCenter);
            view._difficultyText = CreateLabel(rect, "Difficulty", 32, FontStyle.Italic, TextAnchor.MiddleCenter);
            view._rewardText = CreateLabel(rect, "Reward", 32, FontStyle.Normal, TextAnchor.MiddleCenter);
            view._hintText = CreateLabel(rect, "Hint", 22, FontStyle.Normal, TextAnchor.MiddleCenter);
            view._hintText.color = new Color(1, 1, 1, 0.6f);

            CreateButton(rect, "BackButton", "Back", () => view.BackRequested?.Invoke());
            CreateButton(rect, "CommitButton", "Commit", () =>
                view.Committed?.Invoke(view._data, view._variants[view._variantIndex]));

            go.SetActive(false);
            return view;
        }

        public void Show(EncounterData data)
        {
            _data = data;
            _variants = EncounterVariantGenerator.Generate(data);
            _variantIndex = 0;
            _background.color = data.cardColor * 0.5f + new Color(0, 0, 0, 0.5f);
            RefreshLabels();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragStart = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            var deltaX = eventData.position.x - _dragStart.x;
            if (Mathf.Abs(deltaX) < SwipeThreshold)
            {
                return;
            }

            // Swipe left (deltaX < 0) steps to a harder variant; swipe right steps back down.
            var step = deltaX < 0 ? 1 : -1;
            _variantIndex = Mathf.Clamp(_variantIndex + step, 0, _variants.Count - 1);
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            var variant = _variants[_variantIndex];
            var hasDifficultyChoice = _variants.Count > 1;

            _titleText.text = _data.title;
            _difficultyText.text = hasDifficultyChoice
                ? $"{_data.type} · {variant.DifficultyLabel}"
                : $"{_data.type}";
            _rewardText.text = variant.RewardSummary;
            _hintText.text = hasDifficultyChoice ? "Swipe left/right to change difficulty" : "";
        }

        private static Text CreateLabel(Transform parent, string name, int fontSize, FontStyle style, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = fontSize + 14;

            return text;
        }

        private static void CreateButton(Transform parent, string name, string label, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.15f);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 96;

            var button = go.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());

            var labelText = CreateLabel(go.transform, "Label", 32, FontStyle.Bold, TextAnchor.MiddleCenter);
            var labelRect = (RectTransform)labelText.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            labelText.text = label;
        }
    }
}
