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
        private GameSettings _settings;
        private RectTransform _root;
        private RectTransform _abilitiesContainer;
        private Text _progressionText;
        private Text _healthText;
        private Text _manualCastingToggleText;
        private EquipSlotClusterView _equipCluster;

        private GameObject _inventoryPanelGo;
        private GameObject _statsPanelGo;
        private Image _inventoryTabImage;
        private Image _statsTabImage;

        public static ProfileView Create(Transform parent, PlayerLoadout loadout, PlayerProgression progression,
            PlayerHealth health, PlayerStats stats, GameSettings settings)
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
            view._settings = settings;
            view._root = rect;

            // Scrollable body: equip cluster + abilities can outgrow a short/narrow
            // screen (or just a long ability list), so this panel scrolls instead of
            // clipping. Close stays pinned below, outside the scroll area.
            var content = ScrollAreaFactory.Create(rect, new RectOffset(40, 40, 0, 40), 12);

            CreateLabel(content, "Title", "Profile", 64, FontStyle.Bold);
            CreatePortrait(content, "PlayerPortrait", loadout.PlayerSprite, 220);
            view._progressionText = CreateLabel(content, "Progression", "", 38, FontStyle.Italic);
            view._healthText = CreateLabel(content, "Health", "", 38, FontStyle.Italic);

            // Two tabs sharing everything above (title/portrait/progression/health):
            // Inventory (today's equip cluster + abilities, unchanged) and the new Stats
            // allocator. Only one panel is active at a time; switching tabs doesn't
            // rebuild either, so neither loses scroll-independent state.
            var tabRowGo = new GameObject("TabRow", typeof(RectTransform));
            tabRowGo.transform.SetParent(content, false);
            tabRowGo.AddComponent<LayoutElement>().preferredHeight = 96;
            var tabRowLayout = tabRowGo.AddComponent<HorizontalLayoutGroup>();
            tabRowLayout.spacing = 12;
            tabRowLayout.childForceExpandWidth = true;
            tabRowLayout.childForceExpandHeight = true;
            tabRowLayout.childControlWidth = true;
            tabRowLayout.childControlHeight = true;

            CreateTabButton(tabRowGo.transform, "InventoryTab", "Inventory", () => view.SelectTab(true), out view._inventoryTabImage);
            CreateTabButton(tabRowGo.transform, "StatsTab", "Stats", () => view.SelectTab(false), out view._statsTabImage);

            var inventoryPanelGo = new GameObject("InventoryPanel", typeof(RectTransform));
            view._inventoryPanelGo = inventoryPanelGo;
            inventoryPanelGo.transform.SetParent(content, false);
            var inventoryPanelLayout = inventoryPanelGo.AddComponent<VerticalLayoutGroup>();
            inventoryPanelLayout.spacing = 12;
            inventoryPanelLayout.childForceExpandWidth = true;
            inventoryPanelLayout.childForceExpandHeight = false;
            inventoryPanelLayout.childControlWidth = true;
            inventoryPanelLayout.childControlHeight = true;

            view._equipCluster = EquipSlotClusterView.Create(inventoryPanelGo.transform, loadout);

            // Global on/off for every active ability at once — mirrors PotionInventoryView's
            // auto-consume-revive-potions settings row exactly (tap-to-flip button + a
            // label describing the current state).
            var manualCastingRowGo = new GameObject("ManualCastingRow", typeof(RectTransform));
            manualCastingRowGo.transform.SetParent(inventoryPanelGo.transform, false);
            manualCastingRowGo.AddComponent<LayoutElement>().preferredHeight = 96;
            manualCastingRowGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            var manualCastingButton = manualCastingRowGo.AddComponent<Button>();
            manualCastingButton.onClick.AddListener(() =>
            {
                view._settings.SetManualAbilityCasting(!view._settings.ManualAbilityCasting);
                view.RefreshManualCastingToggle();
            });

            var manualCastingTextGo = new GameObject("Label", typeof(RectTransform));
            manualCastingTextGo.transform.SetParent(manualCastingRowGo.transform, false);
            var manualCastingTextRect = (RectTransform)manualCastingTextGo.transform;
            manualCastingTextRect.anchorMin = Vector2.zero;
            manualCastingTextRect.anchorMax = Vector2.one;
            manualCastingTextRect.offsetMin = new Vector2(24, 0);
            manualCastingTextRect.offsetMax = new Vector2(-24, 0);
            view._manualCastingToggleText = manualCastingTextGo.AddComponent<Text>();
            view._manualCastingToggleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view._manualCastingToggleText.fontSize = 28;
            view._manualCastingToggleText.fontStyle = FontStyle.Bold;
            view._manualCastingToggleText.alignment = TextAnchor.MiddleLeft;
            view._manualCastingToggleText.color = Color.white;
            view._manualCastingToggleText.horizontalOverflow = HorizontalWrapMode.Wrap;

            CreateLabel(inventoryPanelGo.transform, "AbilitiesHeader", "Weapon Abilities (tap to toggle, max " +
                        PlayerLoadout.MaxActiveAbilities + ")", 32, FontStyle.Italic);

            var abilitiesGo = new GameObject("Abilities", typeof(RectTransform));
            view._abilitiesContainer = (RectTransform)abilitiesGo.transform;
            view._abilitiesContainer.SetParent(inventoryPanelGo.transform, false);
            var abilitiesLayout = abilitiesGo.AddComponent<VerticalLayoutGroup>();
            abilitiesLayout.spacing = 8;
            abilitiesLayout.childForceExpandWidth = true;
            abilitiesLayout.childForceExpandHeight = false;
            abilitiesLayout.childControlWidth = true;
            abilitiesLayout.childControlHeight = true;

            view._statsPanelGo = StatsPanelView.Create(content, stats).gameObject;

            CreateButton(rect, "CloseButton", "Close", () => view.CloseRequested?.Invoke());

            view._equipCluster.SlotTapped += slot => view.SlotTapped?.Invoke(slot);

            loadout.Changed += view.Refresh;
            progression.Changed += view.Refresh;
            health.Changed += view.RefreshHealthText;
            settings.Changed += view.RefreshManualCastingToggle;

            view.SelectTab(true);

            go.SetActive(false);
            return view;
        }

        // No page-router/enum needed for just two tabs — a bool reads perfectly clearly
        // at both call sites (SelectTab(true)/SelectTab(false)) without adding a type.
        private void SelectTab(bool inventory)
        {
            _inventoryPanelGo.SetActive(inventory);
            _statsPanelGo.SetActive(!inventory);

            // Same "only tint the selected one" treatment as nothing else in this project
            // does yet, but simple enough not to need a shared helper for two buttons.
            _inventoryTabImage.color = inventory ? SelectedTabColor : UnselectedTabColor;
            _statsTabImage.color = inventory ? UnselectedTabColor : SelectedTabColor;
        }

        private static readonly Color SelectedTabColor = new(1f, 1f, 1f, 0.25f);
        private static readonly Color UnselectedTabColor = new(1f, 1f, 1f, 0.08f);

        private static void CreateTabButton(Transform parent, string name, string label, Action onClick, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            image = go.AddComponent<Image>();
            image.color = UnselectedTabColor;

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
            labelText.fontSize = 34;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false;
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
            if (_health.CurrentMaxHealth < _health.TrueMaxHealth)
            {
                var seconds = Mathf.CeilToInt(_health.SecondsUntilNextTick());
                _healthText.text = $"HP {_health.CurrentMaxHealth}/{_health.TrueMaxHealth} — " +
                    $"full in {seconds / 60}:{seconds % 60:D2}";
            }
            else
            {
                _healthText.text = $"HP {_health.CurrentMaxHealth}/{_health.TrueMaxHealth}";
            }
        }

        private void Refresh()
        {
            _progressionText.text = $"Level {_progression.Level} — {_progression.CurrentXp}/{_progression.XpToNextLevel} XP";
            RefreshHealthText();
            _equipCluster.Refresh();
            RefreshManualCastingToggle();
            RefreshAbilities();
        }

        private void RefreshManualCastingToggle()
        {
            _manualCastingToggleText.text = _settings.ManualAbilityCasting
                ? "Manual ability casting: ON (tap to auto-cast instead)"
                : "Manual ability casting: OFF (tap to cast by hand instead)";
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
                CreateAbilityIconRow(_abilitiesContainer, ability, null,
                    $"(passive) {ability.abilityName} — {ability.description}",
                    new Color(1f, 1f, 1f, 0.75f));
            }

            foreach (var ability in abilities.Where(a => a.isActive))
            {
                var capturedName = ability.abilityName;
                var isOn = _loadout.IsAbilityActive(capturedName);
                CreateAbilityIconRow(_abilitiesContainer, ability, () => _loadout.ToggleAbility(capturedName),
                    $"[{(isOn ? "x" : " ")}] {ability.abilityName} — {ability.description}", Color.white);
            }
        }

        // A small colored square (the ability's greybox icon identity) beside the
        // existing text row — shared shape for both the passive (non-tappable) and
        // active (tap-to-toggle) rows, since the only real difference between them is
        // whether onClick is null.
        private static void CreateAbilityIconRow(Transform parent, WeaponAbilityData ability, Action onClick,
            string label, Color textColor)
        {
            var go = new GameObject($"Ability_{ability.abilityName}", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.08f);

            var layoutElement = go.AddComponent<LayoutElement>();
            // Tall enough for two wrapped lines at this row's font size (ability
            // descriptions can run long) without clipping or spilling into neighbors.
            layoutElement.preferredHeight = 124;

            var rowLayout = go.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(20, 20, 0, 0);
            rowLayout.spacing = 16;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            if (onClick != null)
            {
                var button = go.AddComponent<Button>();
                button.onClick.AddListener(() => onClick());
            }

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var iconLayout = iconGo.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 64;
            iconLayout.preferredHeight = 64;
            iconLayout.flexibleWidth = 0;
            iconLayout.flexibleHeight = 0;
            var icon = iconGo.AddComponent<Image>();
            icon.color = ability.iconColor;
            icon.raycastTarget = false;

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textLayout = textGo.AddComponent<LayoutElement>();
            textLayout.minWidth = 0;
            textLayout.preferredWidth = 0;
            textLayout.flexibleWidth = 1;

            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = textColor;
            text.text = label;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
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
