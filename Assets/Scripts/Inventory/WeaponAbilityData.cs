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

        [Tooltip("Passive abilities always contribute powerBonus. Active abilities only " +
                  "contribute it while toggled on from Profile (capped at " +
                  "PlayerLoadout.MaxActiveAbilities).")]
        public bool isActive = true;

        [Tooltip("Combat power this ability contributes (Phase 5 resolution) — " +
                  "unconditionally if passive, only while toggled on if active.")]
        public int powerBonus = 5;
    }
}
