using System;

namespace DoomScroller.CombatCore
{
    // Plain-data description of one fight. Everything the simulation needs and nothing
    // else, so the device and the server resolve the same input to the same beats.
    // Public fields (JsonUtility-friendly): this crosses the network.
    [Serializable]
    public class FightInput
    {
        public PlayerSideInput player = new PlayerSideInput();
        public EnemySideInput enemy = new EnemySideInput();

        // Seeds the fight's System.Random (unless the caller injects one).
        public int seed;

        // A Revive is carried and armed. Fires automatically once per fight when the
        // player would die, restoring revivePercent of the player's start HP (rounded
        // up, never below 1). JsonUtility can't carry a null object, hence two fields.
        public bool reviveArmed;
        public int revivePercent;
    }

    // Indexes into PlayerSideInput.attributes; must match the game's PlayerAttribute
    // ordinals (a test in the main assembly checks).
    public static class AttributeIndex
    {
        public const int Constitution = 0;
        public const int Agility = 1;
        public const int Spirit = 2;
        public const int Expertise = 3;
    }

    [Serializable]
    public class PlayerSideInput
    {
        public int startHp;

        // Sum of equipped non-weapon gear power. Passive ArmorUp is added by the sim.
        public int defense;

        // Attribute values indexed by AttributeIndex; a missing index reads as 0.
        public int[] attributes = new int[0];

        // Equipped weapons in basic-attack order (left hand, then right hand). Empty = unarmed.
        public WeaponInput[] weapons = new WeaponInput[0];

        // Every ability on an equipped weapon, in the order they are timed.
        public AbilityInput[] abilities = new AbilityInput[0];

        // Passive effects from every equipped item.
        public PassiveInput[] passives = new PassiveInput[0];
    }

    [Serializable]
    public class WeaponInput
    {
        public int power;
        public float attackSpeed;
        public bool isRanged;

        // Ordinal of the game's DamageType; matched against EnemySideInput multipliers.
        public int damageType;
        public ScalingInput[] scaling = new ScalingInput[0];
    }

    [Serializable]
    public class ScalingInput
    {
        public int attribute;
        public float coefficient;
    }

    [Serializable]
    public class AbilityInput
    {
        public string name;

        // Index into PlayerSideInput.weapons — the weapon whose power, scaling and
        // damage type this ability's hit builds on.
        public int weaponIndex;
        public int powerBonus;
        public float cooldownSeconds;
        public EffectInput[] onCastEffects = new EffectInput[0];
    }

    [Serializable]
    public class EffectInput
    {
        public StatusEffectType effectType;
        public float magnitude;
        public float durationSeconds;
        public float tickIntervalSeconds;
    }

    [Serializable]
    public class PassiveInput
    {
        public StatusEffectType effectType;
        public float magnitude;
        public float tickIntervalSeconds;

        // When set, the effective magnitude grows with the player's attribute.
        public bool scalesWithAttribute;
        public int scalingAttribute;
        public float scalingCoefficient;
    }

    [Serializable]
    public class EnemySideInput
    {
        public int maxHp;
        public int attackDamage;
        public float attackSpeed;

        // False for a resource node: no enemy timer is created at all.
        public bool canAttack = true;
        public DamageMultiplierInput[] damageMultipliers = new DamageMultiplierInput[0];
    }

    [Serializable]
    public class DamageMultiplierInput
    {
        public int damageType;
        public float multiplier = 1f;
    }
}
