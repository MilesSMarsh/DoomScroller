using System;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Plain C# so regen/penalty math is testable without a running scene. A fight no
    // longer touches this object at all — it's a separate, ephemeral instance (see
    // CombatSimulator) that always starts at CurrentMaxHealth. The only thing this
    // object tracks is that ceiling itself: TrueMaxHealth is the real, undamaged max;
    // CurrentMaxHealth is what you actually fight with, temporarily lowered by a
    // defeat and regenerating back up over real time, the same way a depleting
    // "current HP" used to.
    public class PlayerHealth
    {
        // A defeat costs this fraction of TrueMaxHealth off CurrentMaxHealth, down to
        // Floor — never lower, so repeated losses can't lock you out entirely.
        private const float DefeatPenaltyFraction = 0.25f;
        private const float FloorFraction = 0.2f;

        public int TrueMaxHealth { get; private set; }
        public int CurrentMaxHealth { get; private set; }

        // Exposed for SaveSystem — Regenerate() computes elapsed time from this against
        // wall-clock DateTime.UtcNow, so restoring it (not just the two HP fields) means
        // offline time between sessions is correctly credited on the very next
        // Regenerate() call, the same way backgrounding the app already is today.
        public long LastRegenUtcTicks => _lastRegenUtc.Ticks;

        private readonly int _regenPerInterval;
        private readonly float _regenIntervalSeconds;
        private DateTime _lastRegenUtc;
        private readonly Func<DateTime> _deviceUtcNow;
        private readonly Func<double> _monotonicSeconds;
        private bool _anchored;
        private DateTime _anchorServerUtc;
        private double _anchorMonotonic;

        private static readonly System.Diagnostics.Stopwatch DefaultMonotonic = System.Diagnostics.Stopwatch.StartNew();

        // Before the first server sync: device UTC. After: the server time at the anchor
        // plus monotonic seconds since, so device clock changes can't move regen.
        private DateTime Now => _anchored
            ? _anchorServerUtc.AddSeconds(_monotonicSeconds() - _anchorMonotonic)
            : _deviceUtcNow();

        public event Action Changed;

        // Tells listeners (screens, the feed) that state was replaced by a server load.
        // LoadState/RestoreFrom stay silent because they also run before anything subscribes.
        public void NotifyLoaded() => Changed?.Invoke();

        // Never lower than this, however many defeats in a row — rounded, floored at 1
        // so a very low TrueMaxHealth can't compute a floor of 0.
        public int Floor => Mathf.Max(1, Mathf.RoundToInt(TrueMaxHealth * FloorFraction));

        public bool IsAtFloor => CurrentMaxHealth <= Floor;

        public PlayerHealth(int trueMaxHealth, int regenPerInterval, float regenIntervalSeconds, Func<DateTime> deviceUtcNow = null, Func<double> monotonicSeconds = null)
        {
            _monotonicSeconds = monotonicSeconds ?? (() => DefaultMonotonic.Elapsed.TotalSeconds);
            _deviceUtcNow = deviceUtcNow ?? (() => DateTime.UtcNow);
            TrueMaxHealth = trueMaxHealth;
            CurrentMaxHealth = trueMaxHealth;
            _regenPerInterval = regenPerInterval;
            _regenIntervalSeconds = regenIntervalSeconds;
            _lastRegenUtc = Now;
        }

        // Regen runs on server time: anchors server UTC to a monotonic clock here, so
        // changing the device clock afterwards credits nothing. Re-anchored on each sync.
        public void SetServerTime(DateTime serverUtc)
        {
            _anchorServerUtc = serverUtc;
            _anchorMonotonic = _monotonicSeconds();
            _anchored = true;
        }

        // Server-owned values after a load/save: current max and its regen clock. Fires Changed.
        public void ApplyServerState(int currentMax, long lastRegenUtcTicks)
        {
            CurrentMaxHealth = Mathf.Clamp(currentMax, Floor, TrueMaxHealth);
            _lastRegenUtc = new DateTime(lastRegenUtcTicks, DateTimeKind.Utc);
            Changed?.Invoke();
        }

        // The save/load path — restores both HP fields and the regen clock directly.
        // Doesn't fire Changed — called once, before anything subscribes.
        public void LoadState(int trueMax, int currentMax, long lastRegenUtcTicks)
        {
            TrueMaxHealth = trueMax;
            CurrentMaxHealth = currentMax;
            _lastRegenUtc = new DateTime(lastRegenUtcTicks, DateTimeKind.Utc);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Debug only: sets CurrentMaxHealth (clamped to [Floor, TrueMaxHealth]) and fires
        // Changed. Doesn't touch the regen clock.
        public void DebugSetCurrentMaxHealth(int value)
        {
            CurrentMaxHealth = Mathf.Clamp(value, Floor, TrueMaxHealth);
            Changed?.Invoke();
        }
#endif

        // Called once when a fight ends in defeat (including leaving one early).
        public void ApplyDefeatPenalty()
        {
            var penalty = Mathf.RoundToInt(TrueMaxHealth * DefeatPenaltyFraction);
            CurrentMaxHealth = Mathf.Max(Floor, CurrentMaxHealth - penalty);
            Changed?.Invoke();
        }

        // Constitution's effect — ProfileController recomputes the intended true max
        // whenever PlayerAttributes changes and pushes it here. Raising the ceiling raises
        // CurrentMaxHealth by the same amount immediately (you don't have to regen into
        // an attribute point you just spent); lowering it (not currently possible —
        // attributes only ever go up) would shrink CurrentMaxHealth proportionally via
        // the same delta, clamped to the floor.
        public void SetTrueMaxHealth(int newTrueMax)
        {
            var delta = newTrueMax - TrueMaxHealth;
            if (delta == 0)
            {
                return;
            }

            TrueMaxHealth = newTrueMax;
            CurrentMaxHealth = Mathf.Clamp(CurrentMaxHealth + delta, Floor, TrueMaxHealth);
            Changed?.Invoke();
        }

        // The Village Inn's effect — an immediate, free full heal.
        public void FullyHeal()
        {
            if (CurrentMaxHealth >= TrueMaxHealth)
            {
                return;
            }

            CurrentMaxHealth = TrueMaxHealth;
            Changed?.Invoke();
        }

        // A max-health potion's effect — restores CurrentMaxHealth directly, capped at
        // TrueMaxHealth.
        public void RestoreMaxHealth(int amount)
        {
            if (amount <= 0 || CurrentMaxHealth >= TrueMaxHealth)
            {
                return;
            }

            CurrentMaxHealth = Mathf.Min(TrueMaxHealth, CurrentMaxHealth + amount);
            Changed?.Invoke();
        }

        // Folds in real-world wall-clock time elapsed since the last tick (not frame
        // time), so regen keeps counting while the game is paused/backgrounded, not
        // just while actively playing. Call before anything that reads CurrentMaxHealth.
        public void Regenerate()
        {
            if (CurrentMaxHealth >= TrueMaxHealth)
            {
                _lastRegenUtc = Now;
                return;
            }

            var elapsedSeconds = (Now - _lastRegenUtc).TotalSeconds;
            var ticks = (int)(elapsedSeconds / _regenIntervalSeconds);
            if (ticks <= 0)
            {
                return;
            }

            CurrentMaxHealth = Mathf.Min(TrueMaxHealth, CurrentMaxHealth + ticks * _regenPerInterval);
            _lastRegenUtc += TimeSpan.FromSeconds(ticks * _regenIntervalSeconds);
            Changed?.Invoke();
        }

        // Seconds until the next regen tick (0 once at TrueMaxHealth) — one tick only,
        // not the full climb back to TrueMaxHealth. See SecondsUntilFullHealth for the
        // "back to full in X:XX" readout; this exists for callers that specifically
        // want just the next tick.
        public float SecondsUntilNextTick()
        {
            if (CurrentMaxHealth >= TrueMaxHealth)
            {
                return 0f;
            }

            var elapsedSeconds = (Now - _lastRegenUtc).TotalSeconds;
            var remaining = _regenIntervalSeconds - elapsedSeconds % _regenIntervalSeconds;
            return (float)Math.Max(0.0, remaining);
        }

        // Seconds until CurrentMaxHealth actually reaches TrueMaxHealth (0 once there) —
        // the real "back to full in X:XX" readout. SecondsUntilNextTick alone
        // undercounts whenever more than one tick's worth of HP is still missing.
        public float SecondsUntilFullHealth()
        {
            if (CurrentMaxHealth >= TrueMaxHealth)
            {
                return 0f;
            }

            var missing = TrueMaxHealth - CurrentMaxHealth;
            var ticksNeeded = Mathf.CeilToInt(missing / (float)Mathf.Max(1, _regenPerInterval));
            return SecondsUntilNextTick() + (ticksNeeded - 1) * _regenIntervalSeconds;
        }
    }
}
