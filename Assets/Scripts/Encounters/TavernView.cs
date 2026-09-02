using System;
using DoomScroller.Feed;
using DoomScroller.Social;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // A standalone social hub reachable from the feed like any other card, but it never
    // fights and never locks (EncounterFlowController never wires its own events into
    // FeedController.MarkWon/MarkLost/MarkAbandoned, unlike every other encounter type)
    // — the whole point is it's always there to revisit. Just hosts the message board
    // for now; the user plans to iterate on the Tavern beyond that later.
    public class TavernView : MonoBehaviour
    {
        public event Action ReturnToFeedRequested;

        private Text _titleText;
        private MessageBoardView _messageBoard;

        public static TavernView Create(Transform parent)
        {
            var go = new GameObject("Tavern", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = go.AddComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.06f, 1f);

            var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(0, 0, NavBarController.Height, 0);
            layoutGroup.spacing = 0;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;

            var view = go.AddComponent<TavernView>();

            var content = ScrollAreaFactory.Create(rect, new RectOffset(48, 48, 24, 40), 20);

            view._titleText = CreateLabel(content, "Title", "The Tavern", 54, FontStyle.Bold);
            view._titleText.GetComponent<LayoutElement>().preferredHeight = 78;

            var subtitle = CreateLabel(content, "Subtitle", "Trade tips, talk gear, no combat here.", 28, FontStyle.Italic);
            subtitle.color = new Color(1f, 1f, 1f, 0.6f);

            view._messageBoard = MessageBoardView.Create(content);

            CreateButton(rect, "ReturnButton", "Leave", () => view.ReturnToFeedRequested?.Invoke());

            go.SetActive(false);
            return view;
        }

        public void Show(EncounterData data)
        {
            gameObject.SetActive(true);
            _titleText.text = data.title;
            _messageBoard.PopulateWithEntries(MessageBoardGenerator.GenerateTavernChatter());
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private static Text CreateLabel(Transform parent, string name, string content, int fontSize, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = fontSize + 20;

            return text;
        }

        private static void CreateButton(Transform parent, string name, string label, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.15f);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 120;

            var button = go.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var labelText = labelGo.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 40;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
        }
    }
}
