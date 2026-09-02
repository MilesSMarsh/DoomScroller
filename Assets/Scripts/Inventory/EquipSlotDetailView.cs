using System;
using System.Linq;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Inventory
{
    // Full-screen "pick gear for one slot" panel — replaces the earlier bottom-sheet
    // overlay, which covered and blocked the lower equip slots and had a drag-and-drop
    // ghost-icon cleanup bug. Shows the currently-equipped item at the top (the "spot"
    // being filled) and every other item for just this slot below as a tap-to-equip
    // list; tapping one equips it immediately and the screen refreshes in place.
    public class EquipSlotDetailView : MonoBehaviour
    {
        private const float IconSize = 110f;

        public event Action BackRequested;

        private PlayerLoadout _loadout;
        private EquipmentSlotType _slotType;
        private Text _titleText;
        private GearRowFactory.Row _equippedRow;
        private RectTransform _listContainer;

        public static EquipSlotDetailView Create(Transform parent, PlayerLoadout loadout)
        {
            var go = new GameObject("EquipSlotDetail", typeof(RectTransform));
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
            layoutGroup.childControlHeight = true;

            var view = go.AddComponent<EquipSlotDetailView>();
            view._loadout = loadout;

            view._titleText = CreateLabel(rect, "Title", "", 56, FontStyle.Bold);

            CreateLabel(rect, "EquippedHeader", "Equipped", 26, FontStyle.Italic);

            var equippedRowGo = new GameObject("EquippedRow", typeof(RectTransform));
            equippedRowGo.transform.SetParent(rect, false);
            equippedRowGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
            SetFixedHeight(equippedRowGo.AddComponent<LayoutElement>(), 150);
            view._equippedRow = GearRowFactory.Create(equippedRowGo.transform, IconSize, 20, 32, 26);

            CreateLabel(rect, "ListHeader", "Other Options", 26, FontStyle.Italic);

            var listGo = new GameObject("List", typeof(RectTransform));
            view._listContainer = (RectTransform)listGo.transform;
            view._listContainer.SetParent(rect, false);
            var listLayout = listGo.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 10;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;

            CreateButton(rect, "BackButton", "Back", () => view.BackRequested?.Invoke());

            go.SetActive(false);
            return view;
        }

        public void Show(EquipmentSlotType slotType)
        {
            _slotType = slotType;
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Refresh()
        {
            _titleText.text = ItemCategoryInfo.SlotLabel(_slotType);

            var equipped = _loadout.GetEquipped(_slotType);
            _equippedRow.CaptionText.text = ItemCategoryInfo.SlotLabel(_slotType);
            if (equipped == null)
            {
                _equippedRow.Icon.color = new Color(1f, 1f, 1f, 0.15f);
                _equippedRow.TitleText.text = "Empty";
                _equippedRow.StatText.text = "pick one below";
            }
            else
            {
                _equippedRow.Icon.color = equipped.itemColor;
                _equippedRow.TitleText.text = equipped.title;
                _equippedRow.StatText.text = $"{equipped.StatLabel} {equipped.powerValue}";
            }

            RebuildList(equipped);
        }

        private void RebuildList(ItemData equipped)
        {
            for (var i = _listContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_listContainer.GetChild(i).gameObject);
            }

            // Only items for this exact slot — the earlier universal grid showing
            // everything was confusing and made drops easy to misfire.
            var options = _loadout.GetInventory(_slotType).Where(i => i != equipped).ToList();
            if (options.Count == 0)
            {
                CreateLabel(_listContainer, "NoOptions", "No other items for this slot yet", 24, FontStyle.Normal);
                return;
            }

            foreach (var item in options)
            {
                CreateOptionRow(_listContainer, item);
            }
        }

        private void CreateOptionRow(Transform parent, ItemData item)
        {
            var rowGo = new GameObject($"Option_{item.title}", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);

            rowGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
            SetFixedHeight(rowGo.AddComponent<LayoutElement>(), 130);

            var button = rowGo.AddComponent<Button>();
            button.onClick.AddListener(() =>
            {
                // Tap to equip, then refresh in place — this item moves to the top,
                // whatever was equipped before reappears in the list below.
                _loadout.Equip(item);
                Refresh();
            });

            var row = GearRowFactory.Create(rowGo.transform, IconSize, 18, 26, 22);
            row.Icon.color = item.itemColor;
            row.CaptionText.text = ItemCategoryInfo.SlotLabel(item.slotType);
            row.TitleText.text = item.title;
            row.StatText.text = $"{item.StatLabel} {item.powerValue} — tap to equip";
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

        private static void CreateButton(Transform parent, string name, string label, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.15f);

            SetFixedHeight(go.AddComponent<LayoutElement>(), 120);

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
