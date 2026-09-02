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
        // Bumped from 120 for mobile legibility (bigger progression/health text needs more
        // vertical room), then from 148 to fold in the 20px of top-padding every screen
        // used to add beyond this — that gap exposed a sliver of each screen's own
        // background color as a visibly separate, slightly-lighter grey band under the
        // bar instead of reading as one solid nav bar. Every other screen reads this
        // constant for its own top padding, so they all pick up the room automatically.
        public const int Height = 168;

        [SerializeField] private ProfileController profileController;

        private Text _progressionText;
        private Text _healthText;

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

            _progressionText = CreateLabel(rect, "Progression", "", TextAnchor.MiddleLeft, new Vector2(0, 0.5f),
                new Vector2(0.6f, 1), new Vector2(32, 0), Vector2.zero);

            _healthText = CreateLabel(rect, "Health", "", TextAnchor.MiddleLeft, new Vector2(0, 0),
                new Vector2(0.6f, 0.5f), new Vector2(32, 0), Vector2.zero);
            _healthText.fontSize = 30;

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
            profileController.Health.Changed += RefreshHealthText;
            RefreshProgressionText();
            RefreshHealthText();
        }

        private void Update()
        {
            // Health regenerates purely from real time (ProfileController.Update() drives
            // the actual tick); keep the always-visible countdown live without needing an
            // explicit event for every passing second.
            RefreshHealthText();
        }

        private void RefreshProgressionText()
        {
            var progression = profileController.Progression;
            _progressionText.text = $"Lv.{progression.Level} · {progression.CurrentXp}/{progression.XpToNextLevel} XP";
        }

        private void RefreshHealthText()
        {
            var health = profileController.Health;
            if (health.IsDepleted)
            {
                var seconds = Mathf.CeilToInt(health.SecondsUntilNextTick());
                _healthText.text = $"HP {health.CurrentHealth}/{health.MaxHealth} · next in {seconds / 60}:{seconds % 60:D2}";
                _healthText.color = new Color(0.95f, 0.4f, 0.35f);
            }
            else
            {
                _healthText.text = $"HP {health.CurrentHealth}/{health.MaxHealth}";
                _healthText.color = Color.white;
            }
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
            text.fontSize = 36;
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
            rect.sizeDelta = new Vector2(200, 104);

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
            labelText.fontSize = 34;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
        }
    }
}
