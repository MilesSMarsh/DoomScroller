using System;
using System.Linq;
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

        // Bubbled up from EquipSlotClusterView — ProfileController owns navigating to
        // EquipSlotDetailView, matching how EncounterFlowController coordinates its own
        // sibling screens rather than views reaching into each other directly.
        public event Action<EquipmentSlotType> SlotTapped;

        private PlayerLoadout _loadout;
        private PlayerProgression _progression;
        private PlayerHealth _health;
        private RectTransform _root;
        private RectTransform _abilitiesContainer;
        private Text _progressionText;
        private Text _healthText;
        private EquipSlotClusterView _equipCluster;

        public static ProfileView Create(Transform parent, PlayerLoadout loadout, PlayerProgression progression, PlayerHealth health)
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
            // Left/right/bottom padding moves onto the scroll Content below — only the
            // nav-bar clearance belongs on the root, since that's fixed regardless of
            // scroll position.
            layoutGroup.padding = new RectOffset(0, 0, NavBarController.Height, 0);
            layoutGroup.spacing = 12;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            // Must be true — see EquipSlotClusterView/CombatResolutionView for why.
            layoutGroup.childControlHeight = true;

            var view = go.AddComponent<ProfileView>();
            view._loadout = loadout;
            view._progression = progression;
            view._health = health;
            view._root = rect;

            // Scrollable body: equip cluster + abilities can outgrow a short/narrow
            // screen (or just a long ability list), so this panel scrolls instead of
            // clipping. Close stays pinned below, outside the scroll area.
            var content = ScrollAreaFactory.Create(rect, new RectOffset(40, 40, 0, 40), 12);

            CreateLabel(content, "Title", "Profile", 64, FontStyle.Bold);
            CreatePortrait(content, "PlayerPortrait", loadout.PlayerSprite, 220);
            view._progressionText = CreateLabel(content, "Progression", "", 38, FontStyle.Italic);
            view._healthText = CreateLabel(content, "Health", "", 38, FontStyle.Italic);

            view._equipCluster = EquipSlotClusterView.Create(content, loadout);

            CreateLabel(content, "AbilitiesHeader", "Weapon Abilities (tap to toggle, max " +
                        PlayerLoadout.MaxActiveAbilities + ")", 32, FontStyle.Italic);

            var abilitiesGo = new GameObject("Abilities", typeof(RectTransform));
            view._abilitiesContainer = (RectTransform)abilitiesGo.transform;
            view._abilitiesContainer.SetParent(content, false);
            var abilitiesLayout = abilitiesGo.AddComponent<VerticalLayoutGroup>();
            abilitiesLayout.spacing = 8;
            abilitiesLayout.childForceExpandWidth = true;
            abilitiesLayout.childForceExpandHeight = false;
            abilitiesLayout.childControlWidth = true;
            abilitiesLayout.childControlHeight = true;

            CreateButton(rect, "CloseButton", "Close", () => view.CloseRequested?.Invoke());

            view._equipCluster.SlotTapped += slot => view.SlotTapped?.Invoke(slot);

            loadout.Changed += view.Refresh;
            progression.Changed += view.Refresh;
            health.Changed += view.RefreshHealthText;

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

        private void Update()
        {
            // Health regenerates purely from real time passing, so keep its line live
            // while the player is sitting on this screen, not just on Loadout/Progression
            // events (ProfileController.Update() drives the actual regen tick; this just
            // keeps the label in sync with it).
            RefreshHealthText();
        }

        private void RefreshHealthText()
        {
            if (_health.IsDepleted)
            {
                var seconds = Mathf.CeilToInt(_health.SecondsUntilNextTick());
                _healthText.text = $"HP {_health.CurrentHealth}/{_health.MaxHealth} — " +
                    $"out of energy, next in {seconds / 60}:{seconds % 60:D2}";
            }
            else
            {
                _healthText.text = $"HP {_health.CurrentHealth}/{_health.MaxHealth}";
            }
        }

        private void Refresh()
        {
            _progressionText.text = $"Level {_progression.Level} — {_progression.CurrentXp}/{_progression.XpToNextLevel} XP";
            RefreshHealthText();
            _equipCluster.Refresh();
            RefreshAbilities();
        }

        private void RefreshAbilities()
        {
            for (var i = _abilitiesContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_abilitiesContainer.GetChild(i).gameObject);
            }

            var abilities = _loadout.GetEquippedWeaponAbilities().ToList();
            if (abilities.Count == 0)
            {
                CreateLabel(_abilitiesContainer, "NoWeapon", "No weapon equipped", 32, FontStyle.Normal);
                return;
            }

            foreach (var ability in abilities.Where(a => !a.isActive))
            {
                var text = CreateLabel(_abilitiesContainer, $"Ability_{ability.abilityName}",
                    $"(passive) {ability.abilityName} — {ability.description}", 30, FontStyle.Normal);
                text.alignment = TextAnchor.MiddleLeft;
                text.color = new Color(1f, 1f, 1f, 0.75f);
            }

            foreach (var ability in abilities.Where(a => a.isActive))
            {
                var capturedName = ability.abilityName;
                var isOn = _loadout.IsAbilityActive(capturedName);
                var text = CreateRow(_abilitiesContainer, $"Ability_{capturedName}",
                    () => _loadout.ToggleAbility(capturedName));
                text.text = $"[{(isOn ? "x" : " ")}] {ability.abilityName} — {ability.description}";
                text.fontSize = 32;
            }
        }

        private static Text CreateRow(Transform parent, string name, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.08f);

            var layoutElement = go.AddComponent<LayoutElement>();
            // Tall enough for two wrapped lines at this row's font size (ability
            // descriptions can run long) without clipping or spilling into neighbors.
            layoutElement.preferredHeight = 124;

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
            textRect.offsetMin = new Vector2(20, 0);
            textRect.offsetMax = new Vector2(-20, 0);

            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        // A fixed-size square portrait, centered — not full-width like the rest of this
        // panel's rows, so it gets its own plain (non-layout-driven) child rather than
        // fighting the parent VerticalLayoutGroup's childForceExpandWidth.
        private static void CreatePortrait(Transform parent, string name, Sprite sprite, float size)
        {
            var rowGo = new GameObject(name, typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            rowGo.AddComponent<LayoutElement>().preferredHeight = size;

            var imageGo = new GameObject("Portrait", typeof(RectTransform));
            var imageRect = (RectTransform)imageGo.transform;
            imageRect.SetParent(rowGo.transform, false);
            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.sizeDelta = new Vector2(size, size);

            var image = imageGo.AddComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            image.preserveAspect = true;
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
