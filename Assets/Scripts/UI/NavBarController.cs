using DoomScroller.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.UI
{
    // Persistent top bar, parented directly on the Canvas (not nested inside Feed or any
    // other screen's own runtime-built hierarchy) so its render/raycast order can't lose a
    // sibling-index race with whatever those screens construct in their own Awake(). Always
    // on top and always visible, including while scrolling the feed.
    public class NavBarController : MonoBehaviour
    {
        public const int Height = 120;

        [SerializeField] private ProfileController profileController;

        private Text _progressionText;

        private void Awake()
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0, Height);
            rect.anchoredPosition = Vector2.zero;

            var background = gameObject.AddComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.07f, 0.95f);

            _progressionText = CreateLabel(rect, "Progression", "", TextAnchor.MiddleLeft, new Vector2(0, 0),
                new Vector2(0.6f, 1), new Vector2(32, 0), Vector2.zero);

            CreateButton(rect, "ProfileButton", "Profile", () => profileController.Open());

            // Guarantee top rendering regardless of what other controllers do in their own
            // Awake() (this is exactly the ordering bug that hid the old corner button).
            rect.SetAsLastSibling();
        }

        private void Start()
        {
            // Deferred to Start(): Awake() order between components on different GameObjects
            // isn't guaranteed, so reading profileController.Progression here could run before
            // ProfileController.Awake() has created it. Start() is guaranteed to run only after
            // every component's Awake() in the scene has completed.
            profileController.Progression.Changed += RefreshProgressionText;
            RefreshProgressionText();
        }

        private void RefreshProgressionText()
        {
            var progression = profileController.Progression;
            _progressionText.text = $"Lv.{progression.Level} · {progression.CurrentXp}/{progression.XpToNextLevel} XP";
        }

        private static Text CreateLabel(Transform parent, string name, string content, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 34;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = content;
            return text;
        }

        private static void CreateButton(Transform parent, string name, string label, System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(1, 0.5f);
            rect.anchorMax = new Vector2(1, 0.5f);
            rect.pivot = new Vector2(1, 0.5f);
            rect.anchoredPosition = new Vector2(-24, 0);
            rect.sizeDelta = new Vector2(180, 84);

            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.15f);

            var button = go.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rect, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var labelText = labelGo.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 28;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
        }
    }
}
