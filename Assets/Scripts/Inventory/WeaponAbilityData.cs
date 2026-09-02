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

        [Tooltip("Damage this ability deals when it casts in the combat simulation " +
                  "(Phase 8) — unconditionally if passive, only while toggled on if " +
                  "active. Also still feeds the feed badge's power heuristic " +
                  "(CombatResolver.PredictWinChance).")]
        public int powerBonus = 5;

        [Tooltip("Seconds between casts once this ability is active/equipped (Phase 8). " +
                  "Every active ability starts off-cooldown, so it casts once immediately " +
                  "when a fight begins.")]
        public float cooldownSeconds = 4f;
    }
}
