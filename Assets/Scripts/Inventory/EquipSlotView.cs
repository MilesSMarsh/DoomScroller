using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoomScroller.Inventory
{
    // One equip-slot cell in the cluster: a square icon (item color, or empty) with the
    // slot name / item title / stat stacked beside it. Tapping opens EquipSlotDetailView
    // for this slot (equipping itself happens there, via tap-to-equip — see that view).
    public class EquipSlotView : MonoBehaviour, IPointerClickHandler
    {
        private const float IconSize = 112f;

        private Image _background;
        private GearRowFactory.Row _row;

        public EquipmentSlotType SlotType { get; private set; }
        public event Action<EquipmentSlotType> Tapped;

        public static EquipSlotView Create(Transform parent, EquipmentSlotType slotType)
        {
            var go = new GameObject($"EquipSlot_{slotType}", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var view = go.AddComponent<EquipSlotView>();
            view.SlotType = slotType;

            view._background = go.AddComponent<Image>();
            view._background.color = EmptyBackgroundColor;

            view._row = GearRowFactory.Create(rect, IconSize, 24, 32, 26);
            view._row.CaptionText.text = ItemCategoryInfo.SlotLabel(slotType);

            view.Refresh(null, null);
            return view;
        }

        private static readonly Color EmptyBackgroundColor = new(1f, 1f, 1f, 0.08f);
        private static readonly Color EmptyIconColor = new(1f, 1f, 1f, 0.15f);
        private static readonly Color LockedBackgroundColor = new(0f, 0f, 0f, 0.35f);
        private static readonly Color LockedIconColor = new(1f, 1f, 1f, 0.06f);

        // lockedBy is the two-handed weapon equipped in the OTHER hand, if any — non-null
        // means this slot can't hold anything right now (see PlayerLoadout.GetLockingTwoHandedWeapon).
        public void Refresh(ItemData equipped, ItemData lockedBy)
        {
            if (lockedBy != null)
            {
                _row.Icon.color = LockedIconColor;
                _row.TitleText.text = "Locked";
                _row.StatText.text = $"{lockedBy.title} (2H)";
                _background.color = LockedBackgroundColor;
            }
            else if (equipped == null)
            {
                _row.Icon.color = EmptyIconColor;
                _row.TitleText.text = "Empty";
                _row.StatText.text = "tap to equip";
                _background.color = EmptyBackgroundColor;
            }
            else
            {
                _row.Icon.color = equipped.itemColor;
                _row.TitleText.text = equipped.title;
                _row.StatText.text = equipped.isTwoHanded
                    ? $"{equipped.StatLabel} {equipped.powerValue} (2H)"
                    : $"{equipped.StatLabel} {equipped.powerValue}";
                _background.color = new Color(equipped.itemColor.r, equipped.itemColor.g, equipped.itemColor.b, 0.25f);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Tapped?.Invoke(SlotType);
        }
    }
}
