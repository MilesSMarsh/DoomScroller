using System.Collections.Generic;
using DoomScroller.Progression;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Header("Attributes")]
        [Tooltip("How much TrueMaxHealth one Constitution point is worth — recomputed on " +
                  "every PlayerAttributes change, calibrated so today's starting attribute " +
                  "spread reproduces maxHealth above exactly, and only grows from points " +
                  "spent past that.")]
        [FormerlySerializedAs("vigorHealthPerPoint")]
        [SerializeField] private int constitutionHealthPerPoint = 5;
        [SerializeField] private int statPointsPerLevel = 3;

        [Header("Currency")]
        [SerializeField] private int startingGold = 50;

        private PlayerLoadout _loadout;
        private PlayerProgression _progression;
        private PlayerHealth _health;
        private PlayerAttributes _attributes;
        private GameSettings _settings;
        private PlayerWallet _wallet;
        private ProfileView _profileView;
        private EquipSlotDetailView _equipSlotDetailView;

        public PlayerLoadout Loadout => _loadout;
        public PlayerProgression Progression => _progression;
        public PlayerHealth Health => _health;
        public PlayerAttributes Attributes => _attributes;
        public GameSettings Settings => _settings;
        public PlayerWallet Wallet => _wallet;

        private void Awake()
        {
            if (SaveSystem.TryReadFromDisk(out var save))
            {
                LoadFromSave(save);
            }
            else
            {
                StartFresh();

                // Constitution's health-half effect on the health ceiling. Fires
                // immediately once here too, but at the default attribute spread
                // (nothing spent yet) that's a no-op — the starting maxHealth above is
                // already calibrated to match it exactly. Not called on the load path —
                // a save's TrueMaxHealth is authoritative as-is, even if Inspector
                // balance constants have since changed.
                RecomputeTrueMaxHealth();
            }

            // Constitution's effect on future attribute-point spends, either way.
            _attributes.Changed += RecomputeTrueMaxHealth;

            // 3 points per level, however many levels a single XP grant crosses.
            _progression.LeveledUp += _ => _attributes.GrantPoints(statPointsPerLevel);

            var rect = (RectTransform)transform;
            _profileView = ProfileView.Create(rect, _loadout, _progression, _health, _attributes, _settings);
            _profileView.CloseRequested += Close;
            _profileView.SlotTapped += OpenSlotDetail;

            _equipSlotDetailView = EquipSlotDetailView.Create(rect, _loadout);
            _equipSlotDetailView.BackRequested += CloseSlotDetail;

            // Autosave on every change to any persisted sub-system, plus the mobile
            // lifecycle hooks below as a redundant safety net.
            _loadout.Changed += Save;
            _progression.Changed += Save;
            _health.Changed += Save;
            _attributes.Changed += Save;
            _wallet.Changed += Save;
            _settings.Changed += Save;
        }

        private void StartFresh()
        {
            _loadout = new PlayerLoadout(startingInventory, playerSprite);
            _progression = new PlayerProgression();
            _health = new PlayerHealth(maxHealth, regenPerInterval, regenIntervalSeconds);
            _attributes = new PlayerAttributes();
            _settings = new GameSettings();
            _wallet = new PlayerWallet(startingGold);
            EquipStarterWeapon();
        }

        private void LoadFromSave(SaveData save)
        {
            _loadout = new PlayerLoadout(new List<ItemData>(), playerSprite);
            _loadout.RestoreFrom(save);

            _progression = new PlayerProgression();
            _progression.LoadState(save.progressionLevel, save.progressionCurrentXp);

            _health = new PlayerHealth(maxHealth, regenPerInterval, regenIntervalSeconds);
            _health.LoadState(save.healthTrueMax, save.healthCurrentMax, save.healthLastRegenUtcTicks);

            _attributes = new PlayerAttributes();
            _attributes.LoadState(save.attributeValues, save.attributeUnspentPoints);

            _settings = new GameSettings();
            _settings.LoadState(save.autoConsumeRevivePotions, save.manualAbilityCasting);

            _wallet = new PlayerWallet(startingGold);
            _wallet.LoadState(save.gold);
        }

        private void Save()
        {
            var data = new SaveData();
            _loadout.CaptureInto(data);
            data.progressionLevel = _progression.Level;
            data.progressionCurrentXp = _progression.CurrentXp;
            data.healthTrueMax = _health.TrueMaxHealth;
            data.healthCurrentMax = _health.CurrentMaxHealth;
            data.healthLastRegenUtcTicks = _health.LastRegenUtcTicks;
            data.attributeValues = _attributes.CaptureValues();
            data.attributeUnspentPoints = _attributes.UnspentPoints;
            data.gold = _wallet.Gold;
            data.autoConsumeRevivePotions = _settings.AutoConsumeRevivePotions;
            data.manualAbilityCasting = _settings.ManualAbilityCasting;
            SaveSystem.WriteToDisk(data);
        }

        // Mobile-lifecycle safety net — autosave-on-every-Changed above already covers
        // the common case, but a backgrounded or killed app doesn't reliably fire a
        // "final" gameplay event first.
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Save();
            }
        }

        private void OnApplicationQuit()
        {
            Save();
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
            var baseline = maxHealth - PlayerAttributes.BaseValue * constitutionHealthPerPoint;
            _health.SetTrueMaxHealth(baseline + _attributes.Get(PlayerAttribute.Constitution) * constitutionHealthPerPoint);
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

    }
}
