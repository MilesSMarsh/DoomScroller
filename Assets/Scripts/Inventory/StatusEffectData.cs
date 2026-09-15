using System;
using DoomScroller.Progression;
using UnityEngine;

namespace DoomScroller.Inventory
{
    // Which stat this effect touches, and which side it touches, is fixed per type —
    // there's no legal combination where e.g. ArmorUp would target the enemy, so a
    // free-form target field would just be a footgun. See CombatSimulator for the math.
    public enum StatusEffectType
    {
        DamageOverTime,   // enemy-only: flat damage per tick
        WeakenDamage,     // enemy-only: % reduction to the enemy's outgoing damage
        SlowAttackSpeed,  // enemy-only: % increase to the enemy's attack interval
        HasteAttackSpeed, // self-only: % decrease to the player's attack interval
        EmpowerDamage,    // self-only: % increase to the player's outgoing damage
        ArmorUp           // self-only: flat defense bonus
    }

    // Authored either on a WeaponAbilityData (applied with durationSeconds when that
    // ability casts) or on a weapon ItemData's passiveEffects (always active for the
    // whole fight while equipped — durationSeconds is ignored in that context).
    [Serializable]
    public class StatusEffectData
    {
        [Tooltip("Only meaningful on a PassiveDefinition's top-level effect (see " +
                  "PassiveDefinition.effect) — blank on an ability's onCastEffects entries, " +
                  "which borrow their owning ability's own name/description instead.")]
        public string passiveName;

        [Tooltip("Same scope as passiveName above — the Equipment Menu only edits this " +
                  "for a PassiveDefinition's top-level effect.")]
        public string description;

        public StatusEffectType effectType;

        [Tooltip("Meaning depends on effectType: DamageOverTime = flat damage per tick; " +
                  "WeakenDamage/SlowAttackSpeed/HasteAttackSpeed/EmpowerDamage = a fraction " +
                  "(0.25 = 25%); ArmorUp = flat defense.")]
        public float magnitude = 0.2f;

        [Tooltip("How long this effect lasts once triggered by a cast. Ignored when this " +
                  "entry lives in a weapon's passiveEffects instead (permanent for the fight).")]
        public float durationSeconds = 5f;

        [Tooltip("DamageOverTime only: seconds between ticks.")]
        public float tickIntervalSeconds = 1f;

        [Tooltip("Only meaningful on a weapon's passiveEffects (see WeaponGenerator/" +
                  "PassiveDefinition) — when set, this passive's effective magnitude grows " +
                  "with the chosen attribute, live in combat, the same way a weapon's own " +
                  "AttributeScaling entries already do (never baked in at roll time).")]
        public bool scalesWithAttribute;
        public PlayerAttribute scalingAttribute;
        public float scalingCoefficient;

        // The magnitude CombatSimulator actually applies — base magnitude plus the live
        // attribute bonus when scalesWithAttribute is set. Never used by Describe/
        // DescribePassive below (their static base-magnitude text is enough for the
        // Profile ability list; a scaled preview isn't in scope here).
        public float EffectiveMagnitude(PlayerAttributes attributes)
        {
            return scalesWithAttribute ? magnitude + attributes.Get(scalingAttribute) * scalingCoefficient : magnitude;
        }

        // Generated straight from the numbers above so Profile UI text can never drift
        // from what the effect actually does in CombatSimulator.
        public string Describe()
        {
            var percent = Mathf.RoundToInt(magnitude * 100f);
            return effectType switch
            {
                StatusEffectType.DamageOverTime =>
                    $"{Mathf.RoundToInt(magnitude)} dmg every {tickIntervalSeconds:0.#}s for {durationSeconds:0.#}s",
                StatusEffectType.WeakenDamage => $"-{percent}% enemy damage for {durationSeconds:0.#}s",
                StatusEffectType.SlowAttackSpeed => $"-{percent}% enemy attack speed for {durationSeconds:0.#}s",
                StatusEffectType.HasteAttackSpeed => $"+{percent}% attack speed for {durationSeconds:0.#}s",
                StatusEffectType.EmpowerDamage => $"+{percent}% damage for {durationSeconds:0.#}s",
                StatusEffectType.ArmorUp => $"+{Mathf.RoundToInt(magnitude)} armor for {durationSeconds:0.#}s",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        // Same wording, without the duration clause — for passiveEffects, where it's permanent.
        public string DescribePassive()
        {
            var percent = Mathf.RoundToInt(magnitude * 100f);
            return effectType switch
            {
                StatusEffectType.DamageOverTime =>
                    $"{Mathf.RoundToInt(magnitude)} dmg every {tickIntervalSeconds:0.#}s (passive)",
                StatusEffectType.WeakenDamage => $"-{percent}% enemy damage (passive)",
                StatusEffectType.SlowAttackSpeed => $"-{percent}% enemy attack speed (passive)",
                StatusEffectType.HasteAttackSpeed => $"+{percent}% attack speed (passive)",
                StatusEffectType.EmpowerDamage => $"+{percent}% damage (passive)",
                StatusEffectType.ArmorUp => $"+{Mathf.RoundToInt(magnitude)} armor (passive)",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
