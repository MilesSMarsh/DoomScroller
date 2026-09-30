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
        private RectTransform _materialsContainer;
        private Text _progressionText;
        private Text _healthText;
        private string _lastHealthText;
        private Text _manualCastingToggleText;
        private EquipSlotClusterView _equipCluster;

        private GameObject _inventoryPanelGo;
        private GameObject _abilitiesPanelGo;
        private GameObject _attributesPanelGo;
        private Image _inventoryTabImage;
        private Image _abilitiesTabImage;
        private Image _attributesTabImage;

        // The Inventory tab's own inner sub-tabs — Equipment/Potions/Materials, one
        // nesting level below the three above.
        private GameObject _equipmentPanelGo;
        private GameObject _potionsPanelGo;
        private GameObject _materialsPanelGo;
        private Image _equipmentTabImage;
        private Image _potionsTabImage;
        private Image _materialsTabImage;

        public static ProfileView Create(Transform parent, PlayerLoadout loadout, PlayerProgression progression,
            PlayerHealth health, PlayerAttributes attributes, GameSettings settings)
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

            // Three tabs sharing everything above (title/portrait/progression/health):
            // Inventory (equip cluster), Abilities (its own tab now — the equip cluster
            // plus manual-casting toggle plus a growing ability list no longer all fit
            // on one screen together) and Attributes. Only one panel is active at a time;
            // switching tabs doesn't rebuild any of them, so none loses scroll-independent
            // state.
            var tabRowGo = new GameObject("TabRow", typeof(RectTransform));
            tabRowGo.transform.SetParent(content, false);
            tabRowGo.AddComponent<LayoutElement>().preferredHeight = 96;
            var tabRowLayout = tabRowGo.AddComponent<HorizontalLayoutGroup>();
            tabRowLayout.spacing = 12;
            tabRowLayout.childForceExpandWidth = true;
            tabRowLayout.childForceExpandHeight = true;
            tabRowLayout.childControlWidth = true;
            tabRowLayout.childControlHeight = true;

            CreateTabButton(tabRowGo.transform, "InventoryTab", "Inventory", () => view.SelectTab(0), out view._inventoryTabImage);
            CreateTabButton(tabRowGo.transform, "AbilitiesTab", "Abilities", () => view.SelectTab(1), out view._abilitiesTabImage);
            CreateTabButton(tabRowGo.transform, "AttributesTab", "Attributes", () => view.SelectTab(2), out view._attributesTabImage);

            var inventoryPanelGo = new GameObject("InventoryPanel", typeof(RectTransform));
            view._inventoryPanelGo = inventoryPanelGo;
            inventoryPanelGo.transform.SetParent(content, false);
            var inventoryPanelLayout = inventoryPanelGo.AddComponent<VerticalLayoutGroup>();
            inventoryPanelLayout.spacing = 12;
            inventoryPanelLayout.childForceExpandWidth = true;
            inventoryPanelLayout.childForceExpandHeight = false;
            inventoryPanelLayout.childControlWidth = true;
            inventoryPanelLayout.childControlHeight = true;

            // Inventory's own inner tab row — Equipment/Potions/Materials — one nesting
            // level below the outer Inventory/Abilities/Stats row, same
            // CreateTabButton/SelectedTabColor pattern.
            var inventoryTabRowGo = new GameObject("InventoryTabRow", typeof(RectTransform));
            inventoryTabRowGo.transform.SetParent(inventoryPanelGo.transform, false);
            inventoryTabRowGo.AddComponent<LayoutElement>().preferredHeight = 88;
            var inventoryTabRowLayout = inventoryTabRowGo.AddComponent<HorizontalLayoutGroup>();
            inventoryTabRowLayout.spacing = 12;
            inventoryTabRowLayout.childForceExpandWidth = true;
            inventoryTabRowLayout.childForceExpandHeight = true;
            inventoryTabRowLayout.childControlWidth = true;
            inventoryTabRowLayout.childControlHeight = true;

            CreateTabButton(inventoryTabRowGo.transform, "EquipmentTab", "Equipment",
                () => view.SelectInventoryTab(0), out view._equipmentTabImage);
            CreateTabButton(inventoryTabRowGo.transform, "PotionsTab", "Potions",
                () => view.SelectInventoryTab(1), out view._potionsTabImage);
            CreateTabButton(inventoryTabRowGo.transform, "MaterialsTab", "Materials",
                () => view.SelectInventoryTab(2), out view._materialsTabImage);

            var equipmentPanelGo = new GameObject("EquipmentPanel", typeof(RectTransform));
            view._equipmentPanelGo = equipmentPanelGo;
            equipmentPanelGo.transform.SetParent(inventoryPanelGo.transform, false);
            var equipmentPanelLayout = equipmentPanelGo.AddComponent<VerticalLayoutGroup>();
            equipmentPanelLayout.childForceExpandWidth = true;
            equipmentPanelLayout.childForceExpandHeight = false;
            equipmentPanelLayout.childControlWidth = true;
            equipmentPanelLayout.childControlHeight = true;

            view._equipCluster = EquipSlotClusterView.Create(equipmentPanelGo.transform, loadout);

            view._potionsPanelGo = PotionInventoryView.Create(inventoryPanelGo.transform, loadout, health, settings).gameObject;

            var materialsPanelGo = new GameObject("MaterialsPanel", typeof(RectTransform));
            view._materialsPanelGo = materialsPanelGo;
            materialsPanelGo.transform.SetParent(inventoryPanelGo.transform, false);
            var materialsPanelLayout = materialsPanelGo.AddComponent<VerticalLayoutGroup>();
            materialsPanelLayout.spacing = 12;
            materialsPanelLayout.childForceExpandWidth = true;
            materialsPanelLayout.childForceExpandHeight = false;
            materialsPanelLayout.childControlWidth = true;
            materialsPanelLayout.childControlHeight = true;

            CreateLabel(materialsPanelGo.transform, "MaterialsHeader", "Crafting Materials", 32, FontStyle.Italic);

            var materialsGo = new GameObject("Materials", typeof(RectTransform));
            view._materialsContainer = (RectTransform)materialsGo.transform;
            view._materialsContainer.SetParent(materialsPanelGo.transform, false);
            var materialsLayout = materialsGo.AddComponent<VerticalLayoutGroup>();
            materialsLayout.spacing = 8;
            materialsLayout.childForceExpandWidth = true;
            materialsLayout.childForceExpandHeight = false;
            materialsLayout.childControlWidth = true;
            materialsLayout.childControlHeight = true;

            var abilitiesPanelGo = new GameObject("AbilitiesPanel", typeof(RectTransform));
            view._abilitiesPanelGo = abilitiesPanelGo;
            abilitiesPanelGo.transform.SetParent(content, false);
            var abilitiesPanelLayout = abilitiesPanelGo.AddComponent<VerticalLayoutGroup>();
            abilitiesPanelLayout.spacing = 12;
            abilitiesPanelLayout.childForceExpandWidth = true;
            abilitiesPanelLayout.childForceExpandHeight = false;
            abilitiesPanelLayout.childControlWidth = true;
            abilitiesPanelLayout.childControlHeight = true;

            // Global on/off for every active ability at once — mirrors PotionInventoryView's
            // auto-consume-revive-potions settings row exactly (tap-to-flip button + a
            // label describing the current state).
            var manualCastingRowGo = new GameObject("ManualCastingRow", typeof(RectTransform));
            manualCastingRowGo.transform.SetParent(abilitiesPanelGo.transform, false);
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

            CreateLabel(abilitiesPanelGo.transform, "AbilitiesHeader",
                "Weapon Abilities (every ability on your equipped weapons fights alongside you)",
                32, FontStyle.Italic);

            var abilitiesGo = new GameObject("Abilities", typeof(RectTransform));
            view._abilitiesContainer = (RectTransform)abilitiesGo.transform;
            view._abilitiesContainer.SetParent(abilitiesPanelGo.transform, false);
            var abilitiesLayout = abilitiesGo.AddComponent<VerticalLayoutGroup>();
            abilitiesLayout.spacing = 8;
            abilitiesLayout.childForceExpandWidth = true;
            abilitiesLayout.childForceExpandHeight = false;
            abilitiesLayout.childControlWidth = true;
            abilitiesLayout.childControlHeight = true;

            view._attributesPanelGo = AttributesPanelView.Create(content, attributes).gameObject;

            CreateButton(rect, "CloseButton", "Close", () => view.CloseRequested?.Invoke());

            view._equipCluster.SlotTapped += slot => view.SlotTapped?.Invoke(slot);

            loadout.Changed += view.Refresh;
            progression.Changed += view.Refresh;
            health.Changed += view.RefreshHealthText;
            settings.Changed += view.RefreshManualCastingToggle;

            view.SelectTab(0);
            view.SelectInventoryTab(0);

            go.SetActive(false);
            return view;
        }

        // Three static tabs — an int index (matching the CreateTabButton calls above in
        // order) reads clearly enough at each call site without a dedicated enum/router
        // type for something this small.
        private void SelectTab(int index)
        {
            _inventoryPanelGo.SetActive(index == 0);
            _abilitiesPanelGo.SetActive(index == 1);
            _attributesPanelGo.SetActive(index == 2);

            // Same "only tint the selected one" treatment as nothing else in this project
            // does yet, but simple enough not to need a shared helper for three buttons.
            _inventoryTabImage.color = index == 0 ? SelectedTabColor : UnselectedTabColor;
            _abilitiesTabImage.color = index == 1 ? SelectedTabColor : UnselectedTabColor;
            _attributesTabImage.color = index == 2 ? SelectedTabColor : UnselectedTabColor;
        }

        // Inventory's own inner sub-tabs — Equipment/Potions/Materials — same shape as
        // SelectTab, one nesting level down.
        private void SelectInventoryTab(int index)
        {
            _equipmentPanelGo.SetActive(index == 0);
            _potionsPanelGo.SetActive(index == 1);
            _materialsPanelGo.SetActive(index == 2);

            _equipmentTabImage.color = index == 0 ? SelectedTabColor : UnselectedTabColor;
            _potionsTabImage.color = index == 1 ? SelectedTabColor : UnselectedTabColor;
            _materialsTabImage.color = index == 2 ? SelectedTabColor : UnselectedTabColor;
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
            string text;
            if (_health.CurrentMaxHealth < _health.TrueMaxHealth)
            {
                var seconds = Mathf.CeilToInt(_health.SecondsUntilFullHealth());
                text = $"HP {_health.CurrentMaxHealth}/{_health.TrueMaxHealth} — " +
                    $"full in {seconds / 60}:{seconds % 60:D2}";
            }
            else
            {
                text = $"HP {_health.CurrentMaxHealth}/{_health.TrueMaxHealth}";
            }

            // Same cheap early-out as NavBarController.RefreshHealthText — this runs every
            // Update() while the screen is open, but Text.text dirties the Canvas for a
            // rebuild even when the string is unchanged.
            if (text == _lastHealthText)
            {
                return;
            }

            _lastHealthText = text;
            _healthText.text = text;
        }

        private void Refresh()
        {
            _progressionText.text = $"Level {_progression.Level} — {_progression.CurrentXp}/{_progression.XpToNextLevel} XP";
            RefreshHealthText();
            _equipCluster.Refresh();
            RefreshManualCastingToggle();
            RefreshAbilities();
            RefreshMaterials();
        }

        private void RefreshManualCastingToggle()
        {
            _manualCastingToggleText.text = _settings.ManualAbilityCasting
                ? "Manual ability casting: ON (tap to auto-cast instead)"
                : "Manual ability casting: OFF (tap to cast by hand instead)";
        }

        private static readonly Color PassiveIconColor = new(0.5f, 0.55f, 0.6f, 1f);

        private void RefreshAbilities()
        {
            for (var i = _abilitiesContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_abilitiesContainer.GetChild(i).gameObject);
            }

            var abilities = _loadout.GetEquippedWeaponAbilities().ToList();
            var passives = _loadout.GetEquippedPassiveEffects().ToList();
            if (abilities.Count == 0 && passives.Count == 0)
            {
                CreateLabel(_abilitiesContainer, "NoWeapon", "No weapon equipped", 32, FontStyle.Normal);
                return;
            }

            // No toggle, no cap — every ability on an equipped weapon fights, so this is
            // purely informational now. Any onCastEffects ride along in the same row so
            // its buff/debuff is visible right next to the hit that triggers it.
            foreach (var ability in abilities)
            {
                var label = $"{ability.abilityName} — {ability.description}";
                if (ability.onCastEffects is { Length: > 0 })
                {
                    label += " (" + string.Join(", ", ability.onCastEffects.Select(e => e.Describe())) + ")";
                }

                CreateAbilityIconRow(_abilitiesContainer, ability.iconColor, ability.icon, label);
            }

            // Always-on weapon traits, listed separately since — unlike the abilities
            // above — nothing needs to be cast for these to be in effect.
            foreach (var passive in passives)
            {
                var label = !string.IsNullOrEmpty(passive.passiveName)
                    ? $"{passive.passiveName} — {passive.description} ({passive.DescribePassive()})"
                    : $"Passive — {passive.DescribePassive()}";
                CreateAbilityIconRow(_abilitiesContainer, PassiveIconColor, null, label);
            }
        }

        // Grouped by (family, rarity) — each generated instance is now its own unique
        // ItemData (see MaterialGenerator.GenerateFromDefinition), so N owned Common
        // Wood and M owned Rare Wood are always two separate rows/stacks, never merged
        // into one "Wood" total (the user's own explicit call).
        private void RefreshMaterials()
        {
            for (var i = _materialsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_materialsContainer.GetChild(i).gameObject);
            }

            var groups = _loadout.GetMaterials().GroupBy(m => (m.materialSource, m.rarity)).ToList();
            if (groups.Count == 0)
            {
                CreateLabel(_materialsContainer, "NoMaterials", "No crafting materials yet", 32, FontStyle.Normal);
                return;
            }

            foreach (var group in groups)
            {
                CreateMaterialRow(_materialsContainer, group.First(), group.Count());
            }
        }

        private static void CreateMaterialRow(Transform parent, ItemData material, int count)
        {
            var rowGo = new GameObject($"Material_{material.title}_{material.rarity}", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var layoutElement = rowGo.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 100;

            var row = GearRowFactory.Create(rowGo.transform, 64, 22, 28, 24);
            GearRowFactory.SetIcon(row, material.icon, material.itemColor, material.rarity);
            row.CaptionText.text = material.rarity.ToString();
            row.TitleText.text = material.title;
            row.StatText.text = $"x{count} owned";
        }

        // A small colored square beside a name/description — purely informational,
        // since every ability on an equipped weapon (and every passive) is always in
        // the fight, nothing here is toggleable.
        private static void CreateAbilityIconRow(Transform parent, Color iconColor, Sprite icon, string label)
        {
            var go = new GameObject("EffectRow", typeof(RectTransform));
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

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var iconLayout = iconGo.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 64;
            iconLayout.preferredHeight = 64;
            iconLayout.flexibleWidth = 0;
            iconLayout.flexibleHeight = 0;
            var iconImage = iconGo.AddComponent<Image>();
            GearRowFactory.SetIcon(iconImage, icon, iconColor);
            iconImage.raycastTarget = false;

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
            text.color = Color.white;
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
