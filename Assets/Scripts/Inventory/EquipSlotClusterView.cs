using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Inventory
{
    // Two symmetric 4-slot columns, each reaching half the screen: armor (Head/Chest/
    // Legs/Boots) on the left, gear (LeftHand/RightHand/Talisman1/Talisman2) on the
    // right — both stacked the same way, so there's no height mismatch to reconcile
    // between them (unlike the earlier 4-vs-3 layout).
    public class EquipSlotClusterView : MonoBehaviour
    {
        private const float SlotHeight = 130f;
        private const float ColumnSpacing = 12f;
        private const float ClusterHeight = 4 * SlotHeight + 3 * ColumnSpacing; // 556

        private static readonly EquipmentSlotType[] ArmorSlots =
        {
            EquipmentSlotType.Head, EquipmentSlotType.Chest,
            EquipmentSlotType.Legs, EquipmentSlotType.Boots
        };

        private static readonly EquipmentSlotType[] GearSlots =
        {
            EquipmentSlotType.LeftHand, EquipmentSlotType.RightHand,
            EquipmentSlotType.Talisman1, EquipmentSlotType.Talisman2
        };

        private readonly Dictionary<EquipmentSlotType, EquipSlotView> _slots = new();
        private PlayerLoadout _loadout;

        public event Action<EquipmentSlotType> SlotTapped;

        public static EquipSlotClusterView Create(Transform parent, PlayerLoadout loadout)
        {
            var go = new GameObject("EquipCluster", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            var clusterLayoutElement = go.AddComponent<LayoutElement>();
            // All three explicit, not left at -1 ("unspecified"): this GameObject is also
            // its own HorizontalLayoutGroup, and any LayoutElement property left
            // unspecified falls through to that group's own computed value for just that
            // property — verified live via LayoutUtility earlier this project.
            clusterLayoutElement.minHeight = ClusterHeight;
            clusterLayoutElement.preferredHeight = ClusterHeight;
            clusterLayoutElement.flexibleHeight = 0;

            var rowLayout = go.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 24;
            rowLayout.childForceExpandWidth = false;
            // False, same reason documented throughout this codebase: force-expand on a
            // group isn't just "stretch my children," it also makes the group itself
            // self-report flexible=1 to its own parent. Both columns claim an equal share
            // instead via their own explicit flexibleWidth=1.
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            var view = go.AddComponent<EquipSlotClusterView>();
            view._loadout = loadout;

            // flexibleWidth=1 on both (no fixed preferredWidth) splits the row evenly —
            // each column reaches exactly half the available width.
            var armorColumn = CreateColumn(rect, "ArmorColumn");
            foreach (var slot in ArmorSlots)
            {
                view.AddSlot(armorColumn, slot);
            }

            var gearColumn = CreateColumn(rect, "GearColumn");
            foreach (var slot in GearSlots)
            {
                view.AddSlot(gearColumn, slot);
            }

            return view;
        }

        private static Transform CreateColumn(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.minWidth = 0;
            layoutElement.preferredWidth = 0;
            layoutElement.flexibleWidth = 1;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = ColumnSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            return go.transform;
        }

        private void AddSlot(Transform parent, EquipmentSlotType slotType)
        {
            var slotView = EquipSlotView.Create(parent, slotType);
            var layoutElement = slotView.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = SlotHeight;
            layoutElement.preferredHeight = SlotHeight;
            layoutElement.flexibleHeight = 0;
            slotView.Tapped += t => SlotTapped?.Invoke(t);
            _slots[slotType] = slotView;
        }

        public void Refresh()
        {
            foreach (var kvp in _slots)
            {
                kvp.Value.Refresh(_loadout.GetEquipped(kvp.Key), _loadout.GetLockingTwoHandedWeapon(kvp.Key));
            }
        }
    }
}
