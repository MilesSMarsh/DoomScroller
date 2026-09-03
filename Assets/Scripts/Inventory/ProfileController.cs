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

        [Tooltip("The one item a new game starts equipped with. Explicit rather than " +
                  "\"whichever weapon happens to be first in startingInventory\" — that was " +
                  "fragile (adding/reordering inventory content could silently swap in a far " +
                  "stronger weapon) and every hand-authored weapon so far is really mid/late-" +
                  "game power once its abilities are all counted, not a genuine starter.")]
        [SerializeField] private ItemData starterWeapon;

        [Header("Health / Energy Gate")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int regenPerInterval = 10;
        [Tooltip("Real seconds between regen ticks. Defaults are tuned for quick testing — " +
                  "raise this for real energy-system pacing (e.g. 60-300s).")]
        [SerializeField] private float regenIntervalSeconds = 30f;

        [Header("Stats")]
        [Tooltip("How much TrueMaxHealth one Vigor point is worth — recomputed on every " +
                  "PlayerStats change, calibrated so today's starting stat spread " +
                  "reproduces maxHealth above exactly, and only grows from points spent " +
                  "past that.")]
        [SerializeField] private int vigorHealthPerPoint = 5;
        [SerializeField] private int statPointsPerLevel = 3;

        private PlayerLoadout _loadout;
        private PlayerProgression _progression;
        private PlayerHealth _health;
        private PlayerStats _stats;
        private GameSettings _settings;
        private ProfileView _profileView;
        private EquipSlotDetailView _equipSlotDetailView;
        private PotionInventoryView _potionInventoryView;

        public PlayerLoadout Loadout => _loadout;
        public PlayerProgression Progression => _progression;
        public PlayerHealth Health => _health;
        public PlayerStats Stats => _stats;
        public GameSettings Settings => _settings;

        private void Awake()
        {
            _loadout = new PlayerLoadout(startingInventory, playerSprite);
            _progression = new PlayerProgression();
            _health = new PlayerHealth(maxHealth, regenPerInterval, regenIntervalSeconds);
            _stats = new PlayerStats();
            _settings = new GameSettings();
            EquipStarterWeapon();

            // Vigor's effect on the health ceiling. Fires immediately once here too, but
            // at the default stat spread (nothing spent yet) that's a no-op — the
            // starting maxHealth above is already calibrated to match it exactly.
            _stats.Changed += RecomputeTrueMaxHealth;
            RecomputeTrueMaxHealth();

            // 3 points per level, however many levels a single XP grant crosses.
            _progression.LeveledUp += _ => _stats.GrantPoints(statPointsPerLevel);

            var rect = (RectTransform)transform;
            _profileView = ProfileView.Create(rect, _loadout, _progression, _health, _stats, _settings);
            _profileView.CloseRequested += Close;
            _profileView.SlotTapped += OpenSlotDetail;

            _equipSlotDetailView = EquipSlotDetailView.Create(rect, _loadout);
            _equipSlotDetailView.BackRequested += CloseSlotDetail;

            _potionInventoryView = PotionInventoryView.Create(rect, _loadout, _health, _settings);
            _potionInventoryView.CloseRequested += ClosePotions;
        }

        private void Update()
        {
            // Centralized here (the object's owner) rather than in each reader (NavBar,
            // FeedController, ProfileView) so every reader sees the same up-to-date value
            // regardless of which of them happens to run first in a given frame.
            _health.Regenerate();
        }

        private void RecomputeTrueMaxHealth()
        {
            var baseline = maxHealth - PlayerStats.BaseValue * vigorHealthPerPoint;
            _health.SetTrueMaxHealth(baseline + _stats.Get(PlayerStat.Vigor) * vigorHealthPerPoint);
        }

        // Only the one designated starter weapon — armor/talisman slots start empty so a
        // new game actually starts from nothing and gears up purely from fight rewards,
        // instead of a free starting defense stat that outclasses early content before a
        // single fight.
        private void EquipStarterWeapon()
        {
            if (starterWeapon != null)
            {
                _loadout.Equip(starterWeapon);
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

        public void OpenPotions()
        {
            _potionInventoryView.Show();
        }

        private void ClosePotions()
        {
            _potionInventoryView.Hide();
        }
    }
}
