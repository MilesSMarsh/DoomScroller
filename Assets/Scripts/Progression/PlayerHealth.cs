using System;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Plain C# so regen/damage math is testable without a running scene. Doubles as an
    // "energy" gate: once depleted, FeedController blocks entering new encounters until
    // enough real time has passed to regenerate (see FeedController.OnCardTapped).
    public class PlayerHealth
    {
        public int MaxHealth { get; }
        public int CurrentHealth { get; private set; }

        private readonly int _regenPerInterval;
        private readonly float _regenIntervalSeconds;
        private DateTime _lastRegenUtc;

        public event Action Changed;

        public bool IsDepleted => CurrentHealth <= 0;

        // Set true by the encounter views for the duration of a live fight (Phase 8) so
        // regen can't tick up the player's real HP mid-combat while damage is being
        // applied beat-by-beat. The regen clock simply pauses while locked — combat
        // duration neither costs nor grants regen progress.
        public bool CombatLocked { get; set; }

        public PlayerHealth(int maxHealth, int regenPerInterval, float regenIntervalSeconds)
        {
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
            _regenPerInterval = regenPerInterval;
            _regenIntervalSeconds = regenIntervalSeconds;
            _lastRegenUtc = DateTime.UtcNow;
        }

        public void ApplyDamage(int amount)
        {
            if (amount <= 0 || CurrentHealth <= 0)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            Changed?.Invoke();
        }

        // Folds in real-world wall-clock time elapsed since the last tick (not frame time),
        // so regen keeps counting while the game is paused/backgrounded, not just while
        // actively playing. Call before anything that reads CurrentHealth/IsDepleted.
        public void Regenerate()
        {
            if (CombatLocked)
            {
                return;
            }

            if (CurrentHealth >= MaxHealth)
            {
                _lastRegenUtc = DateTime.UtcNow;
                return;
            }

            var elapsedSeconds = (DateTime.UtcNow - _lastRegenUtc).TotalSeconds;
            var ticks = (int)(elapsedSeconds / _regenIntervalSeconds);
            if (ticks <= 0)
            {
                return;
            }

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + ticks * _regenPerInterval);
            _lastRegenUtc += TimeSpan.FromSeconds(ticks * _regenIntervalSeconds);
            Changed?.Invoke();
        }

        // Seconds until the next regen tick (0 once full) — for a "regenerates in X:XX" UI.
        public float SecondsUntilNextTick()
        {
            if (CurrentHealth >= MaxHealth)
            {
                return 0f;
            }

            var elapsedSeconds = (DateTime.UtcNow - _lastRegenUtc).TotalSeconds;
            var remaining = _regenIntervalSeconds - elapsedSeconds % _regenIntervalSeconds;
            return (float)Math.Max(0.0, remaining);
        }
    }
}
