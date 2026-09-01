using System;
using UnityEngine;

namespace DoomScroller.Inventory
{
    // Authored on a weapon ItemData. Which abilities are actually active is separate,
    // per-player state held by PlayerLoadout (Phase 4: "visible and manageable").
    [Serializable]
    public class WeaponAbilityData
    {
        public string abilityName;
        public string description;

        [Tooltip("Combat power this ability contributes while active (Phase 5 resolution).")]
        public int powerBonus = 5;
    }
}
