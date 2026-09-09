using System;
using System.Linq;
using DoomScroller.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Inventory
{
    // The Potions sub-tab of Profile's Inventory tab (see ProfileView) — mirrors
    // EquipSlotDetailView's option-row shape (GearRowFactory icon+text, one action
    // button per row), but potions are never equipped to a body slot: MaxHealthRestore
    // potions get a "Drink" button (consumed immediately). Revive potions have no button
    // at all — every one owned is always available, spent automatically (at most one per
    // fight) the moment it would save the player (see PlayerLoadout.GetBestRevivePotion).
    // A settings row at the top controls whether that auto-consumes or pauses to ask
    // (see CombatPlaybackView.Play). A plain embeddable content block, not a screen of
    // its own — its own root GameObject is what ProfileView shows/hides when switching
    // sub-tabs, same shape VillageTavernView/AttributesPanelView already are.
    public class PotionInventoryView : MonoBehaviour
    {
        private const float IconSize = 110f;

        private PlayerLoadout _loadout;
        private PlayerHealth _health;
        private GameSettings _settings;
        private RectTransform _listContainer;
        private Text _settingsToggleText;

        public static PotionInventoryView Create(Transform parent, PlayerLoadout loadout, PlayerHealth health, GameSettings settings)
        {
            var go = new GameObject("PotionInventory", typeof(RectTransform));

            var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 12;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;

            var view = go.AddComponent<PotionInventoryView>();
            view._loadout = loadout;
            view._health = health;
            view._settings = settings;

            var content = (RectTransform)go.transform;
            content.SetParent(parent, false);

            var settingsRowGo = new GameObject("SettingsRow", typeof(RectTransform));
            settingsRowGo.transform.SetParent(content, false);
            SetFixedHeight(settingsRowGo.AddComponent<LayoutElement>(), 100);
            settingsRowGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            var settingsButton = settingsRowGo.AddComponent<Button>();
            settingsButton.onClick.AddListener(() =>
            {
                view._settings.SetAutoConsumeRevivePotions(!view._settings.AutoConsumeRevivePotions);
                view.Refresh();
            });

            var settingsTextGo = new GameObject("Label", typeof(RectTransform));
            settingsTextGo.transform.SetParent(settingsRowGo.transform, false);
            var settingsTextRect = (RectTransform)settingsTextGo.transform;
            settingsTextRect.anchorMin = Vector2.zero;
            settingsTextRect.anchorMax = Vector2.one;
            settingsTextRect.offsetMin = new Vector2(24, 0);
            settingsTextRect.offsetMax = new Vector2(-24, 0);
            view._settingsToggleText = settingsTextGo.AddComponent<Text>();
            view._settingsToggleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view._settingsToggleText.fontSize = 28;
            view._settingsToggleText.fontStyle = FontStyle.Bold;
            view._settingsToggleText.alignment = TextAnchor.MiddleLeft;
            view._settingsToggleText.color = Color.white;
            view._settingsToggleText.horizontalOverflow = HorizontalWrapMode.Wrap;

            CreateLabel(content, "ListHeader", "Owned Potions", 28, FontStyle.Italic);

            var listGo = new GameObject("List", typeof(RectTransform));
            view._listContainer = (RectTransform)listGo.transform;
            view._listContainer.SetParent(content, false);
            var listLayout = listGo.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 10;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;

            loadout.Changed += view.Refresh;
            settings.Changed += view.Refresh;

            view.Refresh();
            return view;
        }

        public void Refresh()
        {
            _settingsToggleText.text = _settings.AutoConsumeRevivePotions
                ? "Auto-use revive potions: ON (tap to ask instead)"
                : "Auto-use revive potions: OFF (tap to auto-use instead)";

            RebuildList();
        }

        private void RebuildList()
        {
            for (var i = _listContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_listContainer.GetChild(i).gameObject);
            }

            // Grouped by item reference (same convention ProfileView.RefreshMaterials
            // already uses) so N owned copies of the same potion show as one stacked row
            // rather than N identical rows.
            var groups = _loadout.GetPotions().GroupBy(p => p).ToList();
            if (groups.Count == 0)
            {
                CreateLabel(_listContainer, "NoPotions", "No potions yet", 28, FontStyle.Normal);
                return;
            }

            foreach (var group in groups)
            {
                CreatePotionRow(group.Key, group.Count());
            }
        }

        private void CreatePotionRow(ItemData potion, int count)
        {
            var rowGo = new GameObject($"Potion_{potion.title}", typeof(RectTransform));
            rowGo.transform.SetParent(_listContainer, false);
            SetFixedHeight(rowGo.AddComponent<LayoutElement>(), 150);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            var infoGo = new GameObject("Info", typeof(RectTransform));
            infoGo.transform.SetParent(rowGo.transform, false);
            infoGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
            var infoLayout = infoGo.AddComponent<LayoutElement>();
            infoLayout.minWidth = 0;
            infoLayout.preferredWidth = 0;
            infoLayout.flexibleWidth = 1;
            infoLayout.minHeight = 150;
            infoLayout.preferredHeight = 150;
            infoLayout.flexibleHeight = 0;

            var row = GearRowFactory.Create(infoGo.transform, IconSize, 24, 32, 28);
            row.Icon.color = potion.itemColor;
            row.CaptionText.text = potion.potionEffect == PotionEffect.MaxHealthRestore ? "Max HP Potion" : "Revive Potion";
            row.TitleText.text = count > 1 ? $"{potion.title} (x{count} owned)" : potion.title;
            row.StatText.text = potion.potionEffect == PotionEffect.MaxHealthRestore
                ? $"Restores {potion.potionAmount} max HP"
                : $"Revives at {potion.potionAmount} HP if you'd die";

            if (potion.potionEffect == PotionEffect.MaxHealthRestore)
            {
                // Removes exactly one unit — every stacked copy is the same ItemData
                // reference, so which "instance" is consumed is meaningless; the row's
                // count (and, once it hits zero, the row itself) updates via the
                // Refresh() this triggers regardless.
                CreateActionButton(rowGo.transform, "DrinkButton", "Drink", 200, 150,
                    new Color(0.2f, 0.55f, 0.3f, 0.7f), () =>
                    {
                        _loadout.ConsumeMaxHealthPotion(potion, _health);
                        Refresh();
                    });
            }
            // Revive potions have no action button — every one owned is always available
            // (see PlayerLoadout.GetBestRevivePotion), spent automatically the moment a
            // fight would otherwise be lost. Nothing to pre-select from here any more.
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

            SetFixedHeight(go.AddComponent<LayoutElement>(), fontSize + 24);

            return text;
        }

        private static void CreateActionButton(Transform parent, string name, string label,
            float width, float height, Color backgroundColor, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            layoutElement.flexibleWidth = 0;
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleHeight = 0;

            var image = go.AddComponent<Image>();
            image.color = backgroundColor;

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
            labelText.fontSize = 28;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false;
        }
    }
}
