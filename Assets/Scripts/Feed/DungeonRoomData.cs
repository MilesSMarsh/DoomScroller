using System;
using UnityEngine;

namespace DoomScroller.Feed
{
    public enum DungeonRoomType
    {
        Fight,
        Rest,
        Treasure
    }

    // One authored room in EncounterData.dungeonRooms — DungeonGenerator turns this
    // ordered list straight into a run's DungeonStep sequence, no growth-formula scaling
    // involved (each room's own numbers are exactly what plays out). appearChance makes
    // every room optional, not just a single hardcoded "50% rest room" — a run only
    // includes a room if its own independent roll succeeds.
    [Serializable]
    public class DungeonRoomData
    {
        [Tooltip("Optional — blank falls back to an auto label based on type/position " +
                  "(see DungeonGenerator).")]
        public string label;

        public DungeonRoomType type;

        [Tooltip("Independent chance (0-100) this room is actually included the next time " +
                  "this dungeon is run. 100 = always included.")]
        [Range(0f, 100f)]
        public float appearChance = 100f;

        [Tooltip("Fight only.")]
        public int enemyHealth = 80;

        [Tooltip("Fight only.")]
        public int enemyAttackDamage = 10;

        [Tooltip("Fight only.")]
        public float enemyAttackSpeed = 0.8f;

        [Tooltip("Fight only. This room's own creature portrait, shown in " +
                  "CombatPlaybackView during the fight — falls back to a flat color " +
                  "(DungeonGenerator's cycling placeholder palette) when unset. Each Fight " +
                  "room has its own; unlike Combat encounters there's no single shared " +
                  "EncounterData.foregroundArt for a dungeon.")]
        public Sprite enemySprite;

        [Tooltip("Fight only. How this room's enemy takes incoming damage per weapon " +
                  "DamageType — each Fight room has its own, not shared across the whole " +
                  "dungeon. Unlisted types stay at a normal 1×.")]
        public DamageTypeModifier[] damageTypeModifiers;

        [Tooltip("Treasure only — a mid-run bonus pickup. 0 falls back to nothing (unlike " +
                  "the dungeon's own end-of-run gold, a bonus room never derives gold from " +
                  "XP, since it doesn't grant any XP).")]
        public int goldReward;

        [Tooltip("Treasure only.")]
        public LootTable lootTable = new();
    }
}
