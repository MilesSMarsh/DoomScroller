using System.Collections.Generic;
using DoomScroller.Progression;
using UnityEngine;

namespace DoomScroller.Inventory
{
    // Owns the player's persistent session state — loadout and progression — plus the
    // profile screen itself. Opened via NavBarController, which lives directly on the
    // Canvas so it isn't subject to sibling-order races with whatever other screens
    // (Feed, EncounterFlow, ...) happen to build their own hierarchy in Awake().
    public class ProfileController : MonoBehaviour
    {
        [SerializeField] private List<ItemData> startingInventory = new();

        private PlayerLoadout _loadout;
        private PlayerProgression _progression;
        private ProfileView _profileView;

        public PlayerLoadout Loadout => _loadout;
        public PlayerProgression Progression => _progression;

        private void Awake()
        {
            _loadout = new PlayerLoadout(startingInventory);
            _progression = new PlayerProgression();
            EquipFirstOfEachSlot();

            _profileView = ProfileView.Create((RectTransform)transform, _loadout, _progression);
            _profileView.CloseRequested += Close;
        }

        private void EquipFirstOfEachSlot()
        {
            foreach (EquipmentSlotType slot in System.Enum.GetValues(typeof(EquipmentSlotType)))
            {
                var options = _loadout.GetInventory(slot);
                if (options.Count > 0)
                {
                    _loadout.Equip(options[0]);
                }
            }
        }

        public void Open()
        {
            _profileView.Show();
        }

        private void Close()
        {
            _profileView.Hide();
        }
    }
}
