using System;
using System.Collections.Generic;
using DoomScroller.Feed;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Social
{
    // Minimal social layer stub (Phase 7): a short list of comments on the just-resolved
    // encounter, plus a single-line reply field so the player can post their own. Full
    // threading/replies/moderation are explicitly deferred per GAME_DESIGN.md.
    public class MessageBoardView : MonoBehaviour
    {
        // Fired whenever the comment list's content (and therefore this panel's height)
        // changes, so an ancestor with childControlHeight=false can force a full layout
        // rebuild — ContentSizeFitter resolves this panel's new size in a later rebuild
        // pass than an ancestor's own position pass, so a plain Canvas.ForceUpdateCanvases
        // isn't enough to keep siblings below it from overlapping.
        public event Action ContentChanged;

        private RectTransform _commentsContainer;
        private InputField _replyInput;
        private List<MessageBoardEntry> _comments = new();

        public static MessageBoardView Create(Transform parent)
        {
            var go = new GameObject("MessageBoard", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            // Must be true: with this false, sibling positioning doesn't reliably track
            // each child's declared LayoutElement.preferredHeight (see FeedController's
            // childControlHeight note) — rows ended up spaced by Unity's untouched
            // 100px default instead, verified live via RectTransform dumps.
            layout.childControlHeight = true;

            // No ContentSizeFitter here: this panel's own ancestor (CombatResolutionView's
            // outer group) has childControlHeight=true and will size this rect itself from
            // this VerticalLayoutGroup's reported preferred height. A ContentSizeFitter on
            // top of that would be a second writer racing the ancestor for the same
            // sizeDelta — the ancestor's position write and the fitter's size write ended
            // up disagreeing, verified live via RectTransform dumps (position based on a
            // stale height, box drawn at the fitter's newer one).
            var view = go.AddComponent<MessageBoardView>();

            CreateHeader(rect, "Message Board");

            var commentsGo = new GameObject("Comments", typeof(RectTransform));
            view._commentsContainer = (RectTransform)commentsGo.transform;
            view._commentsContainer.SetParent(rect, false);
            var commentsLayout = commentsGo.AddComponent<VerticalLayoutGroup>();
            commentsLayout.spacing = 4;
            commentsLayout.childForceExpandWidth = true;
            commentsLayout.childForceExpandHeight = false;
            commentsLayout.childControlWidth = true;
            commentsLayout.childControlHeight = true;
            // Same reasoning as above: MessageBoard's own group already controls this
            // child's height, so no ContentSizeFitter here either.

            view._replyInput = CreateReplyRow(rect, () => view.OnPostClicked());

            go.SetActive(false);
            return view;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // Regenerates the canned NPC reactions for this encounter's outcome and clears
        // any reply left over from a previous encounter.
        public void Populate(EncounterData data, bool victory)
        {
            PopulateWithEntries(MessageBoardGenerator.Generate(data, victory));
        }

        // Same as Populate, but takes already-generated entries directly — used by
        // TavernView, whose chatter isn't tied to a specific encounter's win/loss.
        public void PopulateWithEntries(IReadOnlyList<MessageBoardEntry> entries)
        {
            gameObject.SetActive(true);
            _comments = new List<MessageBoardEntry>(entries);
            RebuildCommentRows();
            _replyInput.text = "";
            ContentChanged?.Invoke();
        }

        private void OnPostClicked()
        {
            var text = _replyInput.text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _comments.Add(new MessageBoardEntry("You", text));
            RebuildCommentRows();
            _replyInput.text = "";
            ContentChanged?.Invoke();
        }

        private void RebuildCommentRows()
        {
            for (var i = _commentsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_commentsContainer.GetChild(i).gameObject);
            }

            foreach (var comment in _comments)
            {
                var rowGo = new GameObject($"Comment_{comment.Author}", typeof(RectTransform));
                rowGo.transform.SetParent(_commentsContainer, false);

                var rowText = rowGo.AddComponent<Text>();
                rowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                rowText.fontSize = 28;
                rowText.color = Color.white;
                rowText.horizontalOverflow = HorizontalWrapMode.Wrap;
                rowText.verticalOverflow = VerticalWrapMode.Overflow;
                rowText.text = $"<b>{comment.Author}:</b> {comment.Text}";

                var rowLayout = rowGo.AddComponent<LayoutElement>();
                rowLayout.preferredHeight = 42;
            }
        }

        private static void CreateHeader(Transform parent, string label)
        {
            var go = new GameObject("Header", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.text = label;

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 44;
        }

        private static InputField CreateReplyRow(Transform parent, Action onPost)
        {
            var rowGo = new GameObject("ReplyRow", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);

            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12;
            rowLayout.childForceExpandWidth = false;
            // False, and Input/PostButton each declare their own preferredHeight=72
            // instead: childForceExpandHeight=true here doesn't just fill this row's own
            // children, it also makes the row self-report flexibleHeight=1 to ITS OWN
            // parent (MessageBoard) — which then handed it all leftover vertical space
            // (a 780px-tall reply row). Verified live via LayoutUtility dumps.
            rowLayout.childForceExpandHeight = false;
            // Must be true, same reason as elsewhere: without it the row won't actually
            // write the input's flexible-width expansion or the button's fixed width.
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            var rowLayoutElement = rowGo.AddComponent<LayoutElement>();
            rowLayoutElement.preferredHeight = 88;

            var inputGo = new GameObject("Input", typeof(RectTransform));
            inputGo.transform.SetParent(rowGo.transform, false);

            var inputImage = inputGo.AddComponent<Image>();
            inputImage.color = new Color(1, 1, 1, 0.08f);

            var inputLayout = inputGo.AddComponent<LayoutElement>();
            inputLayout.flexibleWidth = 1;
            // Without an explicit preferredHeight, this reports as "unspecified" and the
            // row's own height calc falls back to a much larger value from further down
            // the hierarchy — verified live via RectTransform dumps.
            inputLayout.preferredHeight = 88;

            var inputField = inputGo.AddComponent<InputField>();
            inputField.lineType = InputField.LineType.SingleLine;

            var inputTextRect = CreateStretchChild(inputGo.transform, "Text", new Vector2(16, 6), new Vector2(-16, -6));
            var inputText = inputTextRect.gameObject.AddComponent<Text>();
            inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            inputText.fontSize = 30;
            inputText.color = Color.white;
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.horizontalOverflow = HorizontalWrapMode.Overflow;
            inputText.verticalOverflow = VerticalWrapMode.Truncate;

            var placeholderRect = CreateStretchChild(inputGo.transform, "Placeholder", new Vector2(16, 6), new Vector2(-16, -6));
            var placeholderText = placeholderRect.gameObject.AddComponent<Text>();
            placeholderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeholderText.fontSize = 30;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.color = new Color(1, 1, 1, 0.4f);
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.text = "Add a comment...";

            inputField.textComponent = inputText;
            inputField.placeholder = placeholderText;

            var buttonGo = new GameObject("PostButton", typeof(RectTransform));
            buttonGo.transform.SetParent(rowGo.transform, false);

            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(1, 1, 1, 0.18f);

            var buttonLayout = buttonGo.AddComponent<LayoutElement>();
            buttonLayout.preferredWidth = 170;
            buttonLayout.preferredHeight = 88;

            var button = buttonGo.AddComponent<Button>();
            button.onClick.AddListener(() => onPost());

            var buttonLabelRect = CreateStretchChild(buttonGo.transform, "Label", Vector2.zero, Vector2.zero);
            var buttonLabelText = buttonLabelRect.gameObject.AddComponent<Text>();
            buttonLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonLabelText.fontSize = 30;
            buttonLabelText.fontStyle = FontStyle.Bold;
            buttonLabelText.alignment = TextAnchor.MiddleCenter;
            buttonLabelText.color = Color.white;
            buttonLabelText.text = "Post";

            return inputField;
        }

        private static RectTransform CreateStretchChild(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }
    }
}
