using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // One establishment in the Village (see VillageView) — a stub. Quests themselves are
    // a later iteration; this just holds the establishment's place in the swipe order.
    public class VillageQuestBoardView : MonoBehaviour
    {
        public static VillageQuestBoardView Create(Transform parent)
        {
            var go = new GameObject("QuestBoard", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var view = go.AddComponent<VillageQuestBoardView>();

            var body = CreateLabel(go.transform, "Body", "Quests are coming soon.", 30, FontStyle.Italic);
            body.color = new Color(1f, 1f, 1f, 0.6f);

            return view;
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
            layoutElement.preferredHeight = fontSize + 24;

            return text;
        }
    }
}
