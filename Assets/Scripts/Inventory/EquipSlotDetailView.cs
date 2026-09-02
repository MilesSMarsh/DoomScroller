using System;
using System.Linq;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Inventory
{
    // Full-screen "pick gear for one slot" panel — replaces the earlier bottom-sheet
    // overlay, which covered and blocked the lower equip slots and had a drag-and-drop
    // ghost-icon cleanup bug. Shows the currently-equipped item at the top (with an
    // Unequip button — a slot can be left empty) and every other item for just this slot
    // below as a tap-to-equip list, each with its own Delete button (confirmed via a
    // dialog before anything is actually removed).
    public class EquipSlotDetailView : MonoBehaviour
    {
        private const float IconSize = 130f;

        public event Action BackRequested;

        private PlayerLoadout _loadout;
        private EquipmentSlotType _slotType;
        private Text _titleText;
        private GearRowFactory.Row _equippedRow;
        private GameObject _unequipButton;
        private RectTransform _listContainer;
        private GameObject _confirmDialogGo;
        private Text _confirmMessageText;
        private Action _pendingConfirmAction;

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
            // Left/right/bottom padding moves onto the scroll Content below — only the
            // nav-bar clearance belongs on the root, since that's fixed regardless of
            // scroll position.
            layoutGroup.padding = new RectOffset(0, 0, NavBarController.Height, 0);
            layoutGroup.spacing = 12;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;

            var view = go.AddComponent<EquipSlotDetailView>();
            view._loadout = loadout;

            // Scrollable body: the "Other Options" list grows without bound as more items
            // land in this slot over time (rewards etc.), so this panel scrolls instead of
            // clipping. Back stays pinned below, outside the scroll area.
            var content = ScrollAreaFactory.Create(rect, new RectOffset(40, 40, 0, 40), 12);

            view._titleText = CreateLabel(content, "Title", "", 60, FontStyle.Bold);

            CreateLabel(content, "EquippedHeader", "Equipped", 30, FontStyle.Italic);

            var equippedRowGo = new GameObject("EquippedRow", typeof(RectTransform));
            equippedRowGo.transform.SetParent(content, false);
            SetFixedSize(equippedRowGo.AddComponent<LayoutElement>(), -1, 172);
            var equippedRowLayout = equippedRowGo.AddComponent<HorizontalLayoutGroup>();
            equippedRowLayout.spacing = 12;
            equippedRowLayout.childForceExpandWidth = false;
            equippedRowLayout.childForceExpandHeight = false;
            equippedRowLayout.childControlWidth = true;
            equippedRowLayout.childControlHeight = true;

            var equippedContentGo = new GameObject("EquippedContent", typeof(RectTransform));
            equippedContentGo.transform.SetParent(equippedRowGo.transform, false);
            equippedContentGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
            var equippedContentLayout = equippedContentGo.AddComponent<LayoutElement>();
            equippedContentLayout.minWidth = 0;
            equippedContentLayout.preferredWidth = 0;
            equippedContentLayout.flexibleWidth = 1;
            equippedContentLayout.minHeight = 172;
            equippedContentLayout.preferredHeight = 172;
            equippedContentLayout.flexibleHeight = 0;
            view._equippedRow = GearRowFactory.Create(equippedContentGo.transform, IconSize, 26, 40, 32);

            view._unequipButton = CreateActionButton(equippedRowGo.transform, "UnequipButton", "Unequip", 200, 172,
                new Color(0.5f, 0.2f, 0.2f, 0.5f), () =>
                {
                    view._loadout.Unequip(view._slotType);
                    view.Refresh();
                });

            CreateLabel(content, "ListHeader", "Other Options", 30, FontStyle.Italic);

            var listGo = new GameObject("List", typeof(RectTransform));
            view._listContainer = (RectTransform)listGo.transform;
            view._listContainer.SetParent(content, false);
            var listLayout = listGo.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 10;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;

            CreateButton(rect, "BackButton", "Back", () => view.BackRequested?.Invoke());

            // Created last so it renders on top of everything else in this screen.
            view._confirmDialogGo = CreateConfirmDialog(rect, view);

            go.SetActive(false);
            return view;
        }

        public void Show(EquipmentSlotType slotType)
        {
            _slotType = slotType;
            gameObject.SetActive(true);
            HideConfirmDialog();
            Refresh();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Refresh()
        {
            _titleText.text = ItemCategoryInfo.SlotLabel(_slotType);

            var lockedBy = _loadout.GetLockingTwoHandedWeapon(_slotType);
            var equipped = _loadout.GetEquipped(_slotType);
            _equippedRow.CaptionText.text = ItemCategoryInfo.SlotLabel(_slotType);

            if (lockedBy != null)
            {
                _equippedRow.Icon.color = new Color(1f, 1f, 1f, 0.06f);
                _equippedRow.TitleText.text = "Locked";
                _equippedRow.StatText.text = $"{lockedBy.title} needs both hands";
                _unequipButton.SetActive(false);
            }
            else if (equipped == null)
            {
                _equippedRow.Icon.color = new Color(1f, 1f, 1f, 0.15f);
                _equippedRow.TitleText.text = "Empty";
                _equippedRow.StatText.text = "pick one below";
                _unequipButton.SetActive(false);
            }
            else
            {
                _equippedRow.Icon.color = equipped.itemColor;
                _equippedRow.TitleText.text = equipped.title;
                _equippedRow.StatText.text = equipped.isTwoHanded
                    ? $"{equipped.StatLabel} {equipped.powerValue} (2H)"
                    : $"{equipped.StatLabel} {equipped.powerValue}";
                _unequipButton.SetActive(true);
            }

            RebuildList(equipped, lockedBy);
        }

        private void RebuildList(ItemData equipped, ItemData lockedBy)
        {
            for (var i = _listContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_listContainer.GetChild(i).gameObject);
            }

            if (lockedBy != null)
            {
                CreateLabel(_listContainer, "LockedMessage",
                    $"Unequip \"{lockedBy.title}\" to free this hand — it's a two-handed weapon.",
                    28, FontStyle.Normal);
                return;
            }

            // Only items for this exact slot — the earlier universal grid showing
            // everything was confusing and made drops easy to misfire.
            var options = _loadout.GetInventory(_slotType).Where(i => i != equipped).ToList();
            if (options.Count == 0)
            {
                CreateLabel(_listContainer, "NoOptions", "No other items for this slot yet", 28, FontStyle.Normal);
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
            SetFixedSize(rowGo.AddComponent<LayoutElement>(), -1, 150);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            // Every item shown here is, by construction, not equipped anywhere (RebuildList
            // already excludes the equipped one) — every option is always safely deletable.
            var equipAreaGo = new GameObject("EquipArea", typeof(RectTransform));
            equipAreaGo.transform.SetParent(rowGo.transform, false);
            equipAreaGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
            var equipAreaLayout = equipAreaGo.AddComponent<LayoutElement>();
            equipAreaLayout.minWidth = 0;
            equipAreaLayout.preferredWidth = 0;
            equipAreaLayout.flexibleWidth = 1;
            equipAreaLayout.minHeight = 150;
            equipAreaLayout.preferredHeight = 150;
            equipAreaLayout.flexibleHeight = 0;
            var equipButton = equipAreaGo.AddComponent<Button>();
            equipButton.onClick.AddListener(() =>
            {
                // Tap to equip, then refresh in place — this item moves to the top,
                // whatever was equipped before reappears in the list below.
                _loadout.Equip(item);
                Refresh();
            });

            var row = GearRowFactory.Create(equipAreaGo.transform, IconSize, 24, 32, 28);
            row.Icon.color = item.itemColor;
            row.CaptionText.text = ItemCategoryInfo.SlotLabel(item.slotType);
            row.TitleText.text = item.title;
            row.StatText.text = item.isTwoHanded
                ? $"{item.StatLabel} {item.powerValue} (2H) — tap to equip"
                : $"{item.StatLabel} {item.powerValue} — tap to equip";

            CreateActionButton(rowGo.transform, "DeleteButton", "Delete", 160, 150,
                new Color(0.5f, 0.15f, 0.15f, 0.6f), () => ShowDeleteConfirm(item));
        }

        private void ShowDeleteConfirm(ItemData item)
        {
            _pendingConfirmAction = () =>
            {
                _loadout.RemoveFromInventory(item);
                Refresh();
            };
            _confirmMessageText.text = $"Delete \"{item.title}\"?\nThis can't be undone.";
            _confirmDialogGo.SetActive(true);
        }

        private void HideConfirmDialog()
        {
            _pendingConfirmAction = null;
            _confirmDialogGo.SetActive(false);
        }

        private static GameObject CreateConfirmDialog(Transform parent, EquipSlotDetailView view)
        {
            var overlayGo = new GameObject("ConfirmDialog", typeof(RectTransform));
            var overlayRect = (RectTransform)overlayGo.transform;
            overlayRect.SetParent(parent, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            // The root panel's VerticalLayoutGroup (childControlHeight/Width=true) would
            // otherwise seize this child on activation and collapse it into the vertical
            // stack instead of leaving it as a full-screen overlay — verified live via
            // RectTransform dump (anchors collapsed to a zero-height point after Show()).
            overlayGo.AddComponent<LayoutElement>().ignoreLayout = true;
            // Opaque enough to read as a modal, and its own raycastTarget blocks taps on
            // whatever's behind it while the dialog is up.
            overlayGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var boxGo = new GameObject("Box", typeof(RectTransform));
            var boxRect = (RectTransform)boxGo.transform;
            boxRect.SetParent(overlayRect, false);
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(760, 340);
            boxGo.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.16f, 1f);

            var boxLayout = boxGo.AddComponent<VerticalLayoutGroup>();
            boxLayout.padding = new RectOffset(32, 32, 28, 28);
            boxLayout.spacing = 20;
            boxLayout.childAlignment = TextAnchor.MiddleCenter;
            boxLayout.childForceExpandWidth = true;
            boxLayout.childForceExpandHeight = false;
            boxLayout.childControlWidth = true;
            boxLayout.childControlHeight = true;

            view._confirmMessageText = CreateLabel(boxRect, "Message", "", 34, FontStyle.Normal);
            SetFixedSize(view._confirmMessageText.GetComponent<LayoutElement>(), -1, 120);

            var buttonRowGo = new GameObject("ButtonRow", typeof(RectTransform));
            buttonRowGo.transform.SetParent(boxRect, false);
            SetFixedSize(buttonRowGo.AddComponent<LayoutElement>(), -1, 90);
            var buttonRowLayout = buttonRowGo.AddComponent<HorizontalLayoutGroup>();
            buttonRowLayout.spacing = 16;
            buttonRowLayout.childForceExpandWidth = false;
            buttonRowLayout.childForceExpandHeight = false;
            buttonRowLayout.childControlWidth = true;
            buttonRowLayout.childControlHeight = true;

            CreateActionButton(buttonRowGo.transform, "CancelButton", "Cancel", 340, 90,
                new Color(1f, 1f, 1f, 0.15f), () => view.HideConfirmDialog());
            CreateActionButton(buttonRowGo.transform, "ConfirmButton", "Delete", 340, 90,
                new Color(0.55f, 0.15f, 0.15f, 0.85f), () =>
                {
                    view._pendingConfirmAction?.Invoke();
                    view.HideConfirmDialog();
                });

            overlayGo.SetActive(false);
            return overlayGo;
        }

        private static void SetFixedSize(LayoutElement layoutElement, float width, float height)
        {
            if (width >= 0)
            {
                layoutElement.minWidth = width;
                layoutElement.preferredWidth = width;
                layoutElement.flexibleWidth = 0;
            }

            if (height >= 0)
            {
                layoutElement.minHeight = height;
                layoutElement.preferredHeight = height;
                layoutElement.flexibleHeight = 0;
            }
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

            SetFixedSize(go.AddComponent<LayoutElement>(), -1, fontSize + 24);

            return text;
        }

        // Small labeled action button (Unequip / Delete / Cancel / Confirm) — a fixed
        // size, colored background, and a centered label.
        private static GameObject CreateActionButton(Transform parent, string name, string label,
            float width, float height, Color backgroundColor, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            SetFixedSize(go.AddComponent<LayoutElement>(), width, height);

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
            labelText.fontSize = 30;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false;

            return go;
        }

        private static void CreateButton(Transform parent, string name, string label, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.15f);

            SetFixedSize(go.AddComponent<LayoutElement>(), -1, 120);

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
