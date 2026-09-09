using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Plain C# so allocation math is testable without a running scene, same convention
    // as PlayerProgression/PlayerHealth. Points are spent immediately on tap — no
    // "-"/undo, no staging/confirm step, matching this project's existing immediate-
    // action UI conventions (e.g. tap-to-equip).
    public class PlayerAttributes
    {
        // Public so ProfileController can calibrate PlayerHealth's starting max around
        // it without duplicating the number.
        public const int BaseValue = 5;

        // Expertise's two effects — no change at BaseValue points, scaling linearly from
        // there, mirroring ProfileController.RecomputeTrueMaxHealth's baseline-calibrated
        // shape for Constitution/health.
        private const float ExpertiseYieldBonusPerPoint = 0.05f;
        private const float ExpertiseGoldDiscountPerPoint = 0.02f;
        private const float MinGoldCostMultiplier = 0.5f;

        private readonly Dictionary<PlayerAttribute, int> _values = new();

        public int UnspentPoints { get; private set; }

        public event Action Changed;

        public PlayerAttributes()
        {
            foreach (PlayerAttribute attribute in Enum.GetValues(typeof(PlayerAttribute)))
            {
                _values[attribute] = BaseValue;
            }
        }

        public int Get(PlayerAttribute attribute)
        {
            return _values[attribute];
        }

        public float ResourceYieldMultiplier =>
            1f + (Get(PlayerAttribute.Expertise) - BaseValue) * ExpertiseYieldBonusPerPoint;

        public float GoldCostMultiplier =>
            Mathf.Max(MinGoldCostMultiplier, 1f - (Get(PlayerAttribute.Expertise) - BaseValue) * ExpertiseGoldDiscountPerPoint);

        // Called by ProfileController whenever PlayerProgression.LeveledUp fires.
        public void GrantPoints(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            UnspentPoints += amount;
            Changed?.Invoke();
        }

        public void Allocate(PlayerAttribute attribute)
        {
            if (UnspentPoints <= 0)
            {
                return;
            }

            _values[attribute]++;
            UnspentPoints--;
            Changed?.Invoke();
        }

        // Indexed by PlayerAttribute ordinal — safe since it's a fresh 4-value enum
        // with no legacy ordinal baggage (see PlayerAttribute.cs).
        public int[] CaptureValues()
        {
            var values = new int[Enum.GetValues(typeof(PlayerAttribute)).Length];
            foreach (PlayerAttribute attribute in Enum.GetValues(typeof(PlayerAttribute)))
            {
                values[(int)attribute] = _values[attribute];
            }

            return values;
        }

        // The save/load path. Doesn't fire Changed — called once, before anything
        // subscribes.
        public void LoadState(int[] values, int unspentPoints)
        {
            foreach (PlayerAttribute attribute in Enum.GetValues(typeof(PlayerAttribute)))
            {
                _values[attribute] = values[(int)attribute];
            }

            UnspentPoints = unspentPoints;
        }
    }
}
