using System;
using System.Collections.Generic;
using DoomScroller.Progression;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Inventory
{
    // Full-screen "bio page" for equipment. Built entirely at runtime, greybox — no prefab yet.
    public class ProfileView : MonoBehaviour
    {
        public event Action CloseRequested;

        private PlayerLoadout _loadout;
        private PlayerProgression _progression;
        private RectTransform _root;
        private RectTransform _abilitiesContainer;
        private Text _progressionText;
        private readonly Dictionary<EquipmentSlotType, Text> _slotTexts = new();

        public static ProfileView Create(Transform parent, PlayerLoadout loadout, PlayerProgression progression)
        {
            var go = new GameObject("Profile", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = go.AddComponent<Image>();
            background.color = new Color(0.08f, 0.08f, 0.1f, 1f);

            var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(40, 40, NavBarController.Height + 20, 40);
            layoutGroup.spacing = 12;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;

            var view = go.AddComponent<ProfileView>();
            view._loadout = loadout;
            view._progression = progression;
            view._root = rect;

            CreateLabel(rect, "Title", "Profile", 44, FontStyle.Bold);
            view._progressionText = CreateLabel(rect, "Progression", "", 26, FontStyle.Italic);

            foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                var capturedSlot = slot;
                view._slotTexts[slot] = CreateRow(rect, $"Slot_{slot}", () => loadout.CycleEquipped(capturedSlot));
            }

            CreateLabel(rect, "AbilitiesHeader", "Weapon Abilities (tap to toggle, max " +
                        PlayerLoadout.MaxActiveAbilities + ")", 24, FontStyle.Italic);

            var abilitiesGo = new GameObject("Abilities", typeof(RectTransform));
            view._abilitiesContainer = (RectTransform)abilitiesGo.transform;
            view._abilitiesContainer.SetParent(rect, false);
            var abilitiesLayout = abilitiesGo.AddComponent<VerticalLayoutGroup>();
            abilitiesLayout.spacing = 8;
            abilitiesLayout.childForceExpandWidth = true;
            abilitiesLayout.childForceExpandHeight = false;
            abilitiesLayout.childControlWidth = true;
            abilitiesLayout.childControlHeight = false;
            var abilitiesFitter = abilitiesGo.AddComponent<ContentSizeFitter>();
            abilitiesFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateButton(rect, "CloseButton", "Close", () => view.CloseRequested?.Invoke());

            loadout.Changed += view.Refresh;
            progression.Changed += view.Refresh;

            go.SetActive(false);
            return view;
        }

        public void Show()
        {
            Refresh();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Refresh()
        {
            _progressionText.text = $"Level {_progression.Level} — {_progression.CurrentXp}/{_progression.XpToNextLevel} XP";

            foreach (var kvp in _slotTexts)
            {
                var item = _loadout.GetEquipped(kvp.Key);
                kvp.Value.text = item == null
                    ? $"{kvp.Key}: None (tap to equip)"
                    : $"{kvp.Key}: {item.title} ({item.statSummary}) — tap to change";
            }

            RefreshAbilities();
        }

        private void RefreshAbilities()
        {
            for (var i = _abilitiesContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_abilitiesContainer.GetChild(i).gameObject);
            }

            var weapon = _loadout.GetEquipped(EquipmentSlotType.Weapon);
            if (weapon == null || weapon.abilities == null || weapon.abilities.Length == 0)
            {
                CreateLabel(_abilitiesContainer, "NoWeapon", "No weapon equipped", 22, FontStyle.Normal);
                return;
            }

            foreach (var ability in weapon.abilities)
            {
                var capturedName = ability.abilityName;
                var isActive = _loadout.IsAbilityActive(capturedName);
                var text = CreateRow(_abilitiesContainer, $"Ability_{capturedName}",
                    () => _loadout.ToggleAbility(capturedName));
                text.text = $"[{(isActive ? "x" : " ")}] {ability.abilityName} — {ability.description}";
                text.fontSize = 24;
            }
        }

        private static Text CreateRow(Transform parent, string name, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.08f);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 72;

            var button = go.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());

            // Text lives on its own child GameObject: a single GameObject can only
            // usefully drive one Graphic via its CanvasRenderer, so it can't share
            // the row's own Image.
            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16, 0);
            textRect.offsetMax = new Vector2(-16, 0);

            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            return text;
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
        }
    }
}
