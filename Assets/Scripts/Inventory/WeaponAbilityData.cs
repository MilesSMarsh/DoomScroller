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

        [Tooltip("Greybox placeholder identity for this ability — same convention as " +
                  "ItemData.itemColor (a plain colored square, no sprite art yet). Shown " +
                  "on its Profile row and on its cooldown-bar widget in the combat sim.")]
        public Color iconColor = new(0.85f, 0.75f, 0.35f);

        [Tooltip("Added on top of the owning weapon's own scaled basic-attack damage " +
                  "(powerValue + stat scaling) when this ability casts — not the whole " +
                  "hit by itself, so a skill always hits harder than a basic swing while " +
                  "scaling identically off whatever stat(s) the weapon scales off. " +
                  "Unconditional if passive, only while toggled on if active. Also still " +
                  "feeds the feed badge's power heuristic (CombatResolver.PredictWinChance).")]
        public int powerBonus = 5;

        [Tooltip("Seconds between casts once this ability is active/equipped. Every " +
                  "ability starts ON cooldown when a fight begins — it can't fire until " +
                  "one full cooldown has elapsed (Intelligence shortens it, same as an " +
                  "auto-cast). In manual mode (GameSettings.ManualAbilityCasting) this is " +
                  "instead how long the button takes to become tappable.")]
        public float cooldownSeconds = 4f;
    }
}
