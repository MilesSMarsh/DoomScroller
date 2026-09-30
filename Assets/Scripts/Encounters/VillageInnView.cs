using DoomScroller.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // One establishment in the Village (see VillageView): a free, instant full heal
    // (PlayerHealth.FullyHeal) — no dungeon-run nuance here, just the persistent
    // max-HP ceiling players walk into the feed with.
    public class VillageInnView : MonoBehaviour
    {
        private Text _hpText;
        private GameObject _restButtonGo;
        private Button _restButton;
        private Text _restButtonLabel;
        private PlayerHealth _health;
        private string _lastHpText;

        public static VillageInnView Create(Transform parent)
        {
            var go = new GameObject("Inn", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var view = go.AddComponent<VillageInnView>();

            var subtitle = CreateLabel(go.transform, "Subtitle", "Rest up before your next encounter.", 28, FontStyle.Italic);
            subtitle.color = new Color(1f, 1f, 1f, 0.6f);
            SetFixedHeight(subtitle.GetComponent<LayoutElement>(), 48);

            view._hpText = CreateLabel(go.transform, "HpText", "", 40, FontStyle.Bold);
            SetFixedHeight(view._hpText.GetComponent<LayoutElement>(), 60);

            var buttonGo = CreateButton(go.transform, "RestButton", "Rest (Full Heal)", () => view.OnRestClicked());
            SetFixedHeight(buttonGo.GetComponent<LayoutElement>(), 100);
            view._restButtonGo = buttonGo;
            view._restButton = buttonGo.GetComponent<Button>();
            view._restButtonLabel = buttonGo.GetComponentInChildren<Text>();

            return view;
        }

        public void Show(PlayerHealth health)
        {
            _health = health;
            Refresh();
        }

        private void Update()
        {
            // Health regenerates from real time passing — keep the readout live while
            // this page happens to be visible, same reasoning as ProfileView's own
            // health label (RefreshHealthText).
            if (_health != null)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            var text = $"HP {_health.CurrentMaxHealth}/{_health.TrueMaxHealth}";

            // Cheap early-out, same reasoning as NavBarController/ProfileView's own
            // RefreshHealthText — this runs every Update() while the Inn is open, but
            // Text.text/Button.interactable both dirty the Canvas for a rebuild even when
            // nothing actually changed.
            if (text == _lastHpText)
            {
                return;
            }

            _lastHpText = text;
            _hpText.text = text;

            var atFull = _health.CurrentMaxHealth >= _health.TrueMaxHealth;
            _restButtonLabel.text = atFull ? "Already at full health" : "Rest (Full Heal)";
            _restButton.interactable = !atFull;
        }

        private void OnRestClicked()
        {
            _health.FullyHeal();
            Refresh();
        }

        private static void SetFixedHeight(LayoutElement layoutElement, float height)
        {
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleHeight = 0;
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

            go.AddComponent<LayoutElement>();

            return text;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.15f);

            go.AddComponent<LayoutElement>();

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
            labelText.fontSize = 32;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false;

            return go;
        }
    }
}
