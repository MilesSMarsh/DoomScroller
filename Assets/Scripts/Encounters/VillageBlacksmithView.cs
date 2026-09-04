using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // One establishment in the Village (see VillageView) — a stub. Crafting itself is a
    // later iteration; this just holds the establishment's place in the swipe order.
    public class VillageBlacksmithView : MonoBehaviour
    {
        public static VillageBlacksmithView Create(Transform parent)
        {
            var go = new GameObject("Blacksmith", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var view = go.AddComponent<VillageBlacksmithView>();

            var body = CreateLabel(go.transform, "Body", "Crafting is coming soon.", 30, FontStyle.Italic);
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
