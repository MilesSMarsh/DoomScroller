using System;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Feed
{
    // One entry in an EncounterData's damageTypeModifiers — how much this enemy's
    // incoming damage is scaled when the hit came from a weapon of the given DamageType.
    // Unlisted types implicitly stay at 1 (normal) — see CombatSimulator.DamageTypeMultiplier.
    [Serializable]
    public class DamageTypeModifier
    {
        public DamageType type;

        [Tooltip("1 = normal, >1 = weak to this damage type, <1 = resistant.")]
        public float multiplier = 1f;
    }
}
