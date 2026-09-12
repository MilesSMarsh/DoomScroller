using System;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // A Resource-type feed card: no combat, no chest ceremony — the material is granted
    // the instant the card is entered (Show()), scaled by the player's Expertise
    // (PlayerAttributes.ResourceYieldMultiplier). Deliberately simpler than
    // CombatResolutionView's reward-chest flow — a gathered log or ore vein reads as a
    // quick pickup, not a treasure moment, and this avoids touching RewardChestView's
    // already-verified XP/gold/item-shaped Show().
    public class ResourceEncounterView : MonoBehaviour
    {
        public event Action ReturnToFeedRequested;
        public event Action<EncounterData> EncounterWon;

        private Image _icon;
        private Text _titleText;
        private Text _yieldText;
        private Text _bonusText;

        public static ResourceEncounterView Create(Transform parent)
        {
            var go = new GameObject("ResourceEncounterView", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = go.AddComponent<Image>();
            background.color = new Color(0.05f, 0.06f, 0.05f, 1f);

            var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(40, 40, NavBarController.Height + 40, 40);
            layoutGroup.spacing = 20;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;

            var view = go.AddComponent<ResourceEncounterView>();

            view._titleText = CreateLabel(rect, "Title", "", 48, FontStyle.Bold);

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(rect, false);
            iconGo.AddComponent<LayoutElement>().preferredHeight = 220;
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.sizeDelta = new Vector2(220, 220);
            view._icon = iconGo.AddComponent<Image>();

            view._yieldText = CreateLabel(rect, "Yield", "", 38, FontStyle.Italic);
            view._yieldText.color = new Color(0.7f, 1f, 0.75f, 0.95f);

            // Hidden by default (SetActive(false)) rather than left as an empty string —
            // an empty Text still reserves its LayoutElement's preferredHeight, which
            // would leave a blank gap on every non-bonus visit otherwise.
            view._bonusText = CreateLabel(rect, "Bonus", "", 32, FontStyle.Bold);
            view._bonusText.color = new Color(1f, 0.85f, 0.3f, 0.95f);
            view._bonusText.gameObject.SetActive(false);

            CreateButton(rect, "ReturnButton", "Return", () => view.ReturnToFeedRequested?.Invoke());

            go.SetActive(false);
            return view;
        }

        // Grants the material immediately — no confirm step, matching this project's
        // existing immediate-action UI conventions (e.g. tap-to-equip, Stats' "+" button).
        public void Show(EncounterData data, PlayerLoadout loadout, PlayerAttributes attributes)
        {
            gameObject.SetActive(true);

            var yield = Mathf.Max(1, Mathf.RoundToInt(data.baseResourceYield * attributes.ResourceYieldMultiplier));
            for (var i = 0; i < yield; i++)
            {
                loadout.AddToInventory(data.resourceMaterial);
            }

            // Independent roll, separate from the scaled primary yield above — a rarer
            // bonus find, not something Expertise should inflate the odds or amount of.
            var gotSecondary = data.secondaryResourceMaterial != null
                                && new System.Random().NextDouble() < data.secondaryResourceChance;
            if (gotSecondary)
            {
                for (var i = 0; i < data.secondaryResourceYield; i++)
                {
                    loadout.AddToInventory(data.secondaryResourceMaterial);
                }
            }

            _titleText.text = data.title;
            _icon.color = data.resourceMaterial.itemColor;
            _yieldText.text = $"+{yield} {data.resourceMaterial.title}";
            _bonusText.gameObject.SetActive(gotSecondary);
            if (gotSecondary)
            {
                _bonusText.text = $"Bonus! +{data.secondaryResourceYield} {data.secondaryResourceMaterial.title}";
            }

            EncounterWon?.Invoke(data);
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
            layoutElement.preferredHeight = fontSize + 24;

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
