using System.Collections.Generic;
using DoomScroller.Network;
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

        [Tooltip("Authored weapon a new game starts equipped with, rolled at Plain via " +
                  "WeaponGenerator.GenerateFromDefinition. Explicit rather than \"whichever " +
                  "weapon happens to be first in startingInventory\" — that was fragile. " +
                  "Existing saves keep their own loadout; this only applies to a fresh game.")]
        [SerializeField] private WeaponDefinition starterWeaponDefinition;

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

        private ProfileSync _sync;
        private bool _applying;

        public bool IsLoaded => _sync != null && _sync.IsLoaded;

        // Gameplay input waits for this; raised once the server profile is applied.
        public event System.Action Loaded;

        // Sign-in, load or save failed. The connection-lost screen (014) listens.
        public event System.Action ConnectionLost;

        private void Awake()
        {
            BuildDefaultState();

            // Constitution's effect on future attribute-point spends.
            _attributes.Changed += RecomputeTrueMaxHealth;

            // 3 points per level, however many levels a single XP grant crosses.
            _progression.LeveledUp += _ => _attributes.GrantPoints(statPointsPerLevel);

            var rect = (RectTransform)transform;
            _profileView = ProfileView.Create(rect, _loadout, _progression, _health, _attributes, _settings);
            _profileView.CloseRequested += Close;
            _profileView.SlotTapped += OpenSlotDetail;

            _equipSlotDetailView = EquipSlotDetailView.Create(rect, _loadout);
            _equipSlotDetailView.BackRequested += CloseSlotDetail;

            // Every change to a persisted sub-system queues a debounced server save, plus
            // the mobile lifecycle hooks below as a redundant flush.
            _loadout.Changed += Save;
            _progression.Changed += Save;
            _health.Changed += Save;
            _attributes.Changed += Save;
            _wallet.Changed += Save;
            _settings.Changed += Save;
            DailyMissionTracker.Board.Changed += Save;
        }

        private async void Start()
        {
            // ServerSession creates the client right after scene load.
            while (ServerSession.Client == null)
            {
                await System.Threading.Tasks.Task.Yield();
            }

            _sync = new ProfileSync(ServerSession.Client, CaptureJson, () => Time.realtimeSinceStartupAsDouble);
            _sync.ConnectionLost += () => ConnectionLost?.Invoke();
            _sync.ServerProfileApplied += OnServerProfileApplied;
            await RunStartupAsync();
        }

        // Public so the connection-lost screen (014) can retry.
        public async System.Threading.Tasks.Task RunStartupAsync()
        {
            _sync.Reset();
            var outcome = await _sync.LoadAsync();
            if (outcome == ProfileLoadOutcome.Failed)
            {
                return; // never start a fresh profile over the server's
            }

            _applying = true;
            try
            {
                ApplyServerTime(_sync.LoadedProfile.ServerTimeUtc);
                if (outcome == ProfileLoadOutcome.New)
                {
                    StartFresh();
                }
                else
                {
                    ApplyProfile(_sync.LoadedProfile.Json);
                }

                NotifyLoaded();
            }
            finally
            {
                _applying = false;
            }

            Loaded?.Invoke();
            if (outcome == ProfileLoadOutcome.New)
            {
                _sync.RequestSave();
                _sync.Flush();
            }
        }

        // Objects are created once so views and the feed keep valid references; loading
        // restores state into them.
        private void BuildDefaultState()
        {
            _loadout = new PlayerLoadout(startingInventory, playerSprite);
            _progression = new PlayerProgression();
            _health = new PlayerHealth(maxHealth, regenPerInterval, regenIntervalSeconds);
            _attributes = new PlayerAttributes();
            _settings = new GameSettings();
            _wallet = new PlayerWallet(startingGold);
        }

        private void StartFresh()
        {
            EquipStarterWeapon();

            // Constitution's health-half effect on the health ceiling. At the default
            // attribute spread this is a no-op. Not called on the load path — a save's
            // TrueMaxHealth is authoritative as-is.
            RecomputeTrueMaxHealth();
        }

        private void ApplyServerTime(string serverTimeUtc)
        {
            if (System.DateTime.TryParse(serverTimeUtc, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var serverUtc))
            {
                _health.SetServerTime(serverUtc);
            }
        }

        private void ApplyProfile(string json)
        {
            var save = JsonUtility.FromJson<SaveData>(json);
            _loadout.RestoreFrom(save);
            _progression.LoadState(save.progressionLevel, save.progressionCurrentXp);
            _health.LoadState(save.healthTrueMax, save.healthCurrentMax, save.healthLastRegenUtcTicks);
            _attributes.LoadState(save.attributeValues, save.attributeUnspentPoints);
            _settings.LoadState(save.autoConsumeRevivePotions);
            _wallet.LoadState(save.gold);
            DailyMissionTracker.Board.LoadState(save.dailyMissions);
        }

        // The profile arrives after the screens are built, so tell them to redraw. Saves are
        // suppressed (_applying), and the attributes listener that recomputes true max HP is
        // detached: a loaded TrueMaxHealth is authoritative as-is.
        private void NotifyLoaded()
        {
            _attributes.Changed -= RecomputeTrueMaxHealth;
            try
            {
                _loadout.NotifyLoaded();
                _progression.NotifyLoaded();
                _health.NotifyLoaded();
                _attributes.NotifyLoaded();
                _settings.NotifyLoaded();
                _wallet.NotifyLoaded();
            }
            finally
            {
                _attributes.Changed += RecomputeTrueMaxHealth;
            }
        }

        private void OnServerProfileApplied(ProfileResponse response, bool isReload)
        {
            _applying = true;
            try
            {
                ApplyServerTime(response.ServerTimeUtc);
                if (isReload)
                {
                    ApplyProfile(response.Json);
                    NotifyLoaded();
                }

                // The server owns current max HP and its regen clock.
                if (!string.IsNullOrEmpty(response.Json))
                {
                    var save = JsonUtility.FromJson<SaveData>(response.Json);
                    _health.ApplyServerState(save.healthCurrentMax, save.healthLastRegenUtcTicks);
                }
            }
            finally
            {
                _applying = false;
            }
        }

        private string CaptureJson()
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
            data.dailyMissions = DailyMissionTracker.Board.CaptureState();
            return JsonUtility.ToJson(data);
        }

        private void Save()
        {
            if (!_applying)
            {
                _sync?.RequestSave();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                _sync?.Flush();
            }
            else
            {
                // Resumed: a save round trip re-anchors regen to server time, since the
                // monotonic clock may not have counted time spent backgrounded.
                _sync?.RequestSave();
                _sync?.Flush();
            }
        }

        private void OnApplicationQuit()
        {
            _sync?.Flush();
        }

        private void Update()
        {
            _sync?.Pump();
            if (!IsLoaded)
            {
                return;
            }

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
            var weapon = StarterWeapon.Create(starterWeaponDefinition, new System.Random());
            if (weapon == null)
            {
                Debug.LogWarning("ProfileController: starterWeaponDefinition is unassigned; new game starts unarmed.");
                return;
            }

            _loadout.AddToInventory(weapon);
            _loadout.Equip(weapon);
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
