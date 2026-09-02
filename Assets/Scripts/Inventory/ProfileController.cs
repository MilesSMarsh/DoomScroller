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
        [SerializeField] private Sprite playerSprite;

        [Header("Health / Energy Gate")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int regenPerInterval = 10;
        [Tooltip("Real seconds between regen ticks. Defaults are tuned for quick testing — " +
                  "raise this for real energy-system pacing (e.g. 60-300s).")]
        [SerializeField] private float regenIntervalSeconds = 30f;

        private PlayerLoadout _loadout;
        private PlayerProgression _progression;
        private PlayerHealth _health;
        private ProfileView _profileView;
        private EquipSlotDetailView _equipSlotDetailView;

        public PlayerLoadout Loadout => _loadout;
        public PlayerProgression Progression => _progression;
        public PlayerHealth Health => _health;

        private void Awake()
        {
            _loadout = new PlayerLoadout(startingInventory, playerSprite);
            _progression = new PlayerProgression();
            _health = new PlayerHealth(maxHealth, regenPerInterval, regenIntervalSeconds);
            EquipFirstOfEachSlot();

            var rect = (RectTransform)transform;
            _profileView = ProfileView.Create(rect, _loadout, _progression, _health);
            _profileView.CloseRequested += Close;
            _profileView.SlotTapped += OpenSlotDetail;

            _equipSlotDetailView = EquipSlotDetailView.Create(rect, _loadout);
            _equipSlotDetailView.BackRequested += CloseSlotDetail;
        }

        private void Update()
        {
            // Centralized here (the object's owner) rather than in each reader (NavBar,
            // FeedController, ProfileView) so every reader sees the same up-to-date value
            // regardless of which of them happens to run first in a given frame.
            _health.Regenerate();
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

        // A slot's own detail screen fully replaces Profile (not an overlay on top of
        // it) — the earlier overlay approach visually covered and blocked taps on the
        // equip cluster's lower slots.
        private void OpenSlotDetail(EquipmentSlotType slot)
        {
            _profileView.Hide();
            _equipSlotDetailView.Show(slot);
        }

        private void CloseSlotDetail()
        {
            _equipSlotDetailView.Hide();
            _profileView.Show();
        }
    }
}
