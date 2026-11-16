# Feature (from the GDD): Quest Board daily missions

## GDD section
### Day to day

The player returns for daily missions and long-term power.

- **Daily missions** on the Quest Board (planned) give bonus rewards for goals such as
  clearing a dungeon or defeating a number of enemies.
- Each session builds toward the next region, unlocked by beating that region's solo
  boss.
- Max HP regenerates while the app is closed, so a day's losses are recovered by the
  next session.

## Project conventions
# Unity Conventions

- Namespace scripts `DoomScroller.<System>` matching their folder under
  `Assets/Scripts/` (Combat, Encounters, Feed, Input, Inventory, Network, Progression, Social, UI).
- Keep MonoBehaviours thin. Logic lives in plain C# classes/services so it can be tested
  in EditMode without a scene. Randomness takes an injected `System.Random` so tests
  are deterministic.
- Authored content (encounters, weapons, abilities, materials, recipes) is
  ScriptableObject data under `Assets/Resources/`, loaded by data — never hardcode a
  catalog in code. Add content without new UI code.
- Weapons: authored `WeaponDefinition`s hold a *pool* of possible abilities; generation
  rolls a rarity-sized subset each time. Shops/vendors must generate weapons via
  `WeaponGenerator.GenerateFromDefinition`/`GenerateFromRandomDefinition`, never the
  archetype `WeaponGenerator.Generate`.
- UI is a single Screen Space - Overlay canvas (UGUI). No world-space rendering;
  animation/VFX are UGUI tweens, not ParticleSystem/Animator.
- UI is built from an existing UI kit (its prefabs, sprites and components), never from
  scratch; the chosen kit is recorded in `memory/ui-style.md`. Check its licence and
  `.gitattributes` (LFS) before importing.
- New game visuals (non-UI art) start as greybox/placeholder primitives. No final art before a mechanic
  is proven.
- Every asset and script has a committed `.meta` file. Binary assets go through Git LFS
  — check `.gitattributes` before adding a new binary type.
- Changing a serialized field or save-data shape: keep old saves loading, or call out
  the break explicitly.


## Your file
Write `Assets/Scripts/Progression/DailyMissions.cs` (step 1 of 3).
Plain C# (no MonoBehaviour), namespace DoomScroller.Progression. DailyMission (kind: DefeatEnemies / ClearDungeon / WinFightsWithoutLoss-free choice, description, target, progress, gold reward, claimed). DailyMissionBoard: built for a UTC calendar date; rolls exactly 3 missions with a System.Random seeded from the date (same date -> same missions on every device); RecordEnemyDefeated(), RecordDungeonCleared() advance matching missions; Claim(index) returns the gold reward once when complete; RefreshForDate(date) resets progress when the UTC day changes; CaptureState()/LoadState(...) to/from a [Serializable] DailyMissionState (date string, progress[], claimed[]) so it can live in SaveData later. Event Changed fired on any change.
It is accepted when: [{"type": "DailyMissionBoard"}, {"type": "DailyMission"}, {"type": "DailyMissionState"}]

## Existing code


// ===== Assets/Scripts/Progression/PlayerWallet.cs =====
using System;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Plain C# so gold math is testable without a running scene — same shape as
    // PlayerHealth/PlayerProgression. Gold is earned from combat rewards
    // (RewardGenerator.ComputeGoldReward) and spent at the Village (potion refills,
    // the Market).
    public class PlayerWallet
    {
        public int Gold { get; private set; }

        public event Action Changed;

        public PlayerWallet(int startingGold)
        {
            Gold = Mathf.Max(0, startingGold);
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Gold += amount;
            Changed?.Invoke();
        }

        // False (and no state change) if funds are insufficient — callers never need to
        // check Gold themselves first.
        public bool TrySpendGold(int amount)
        {
            if (amount <= 0 || Gold < amount)
            {
                return false;
            }

            Gold -= amount;
            Changed?.Invoke();
            return true;
        }

        // The save/load path. Doesn't fire Changed — called once, before anything
        // subscribes.
        public void LoadState(int gold)
        {
            Gold = gold;
        }
    }
}


// ===== Assets/Scripts/Progression/PlayerProgression.cs =====
using System;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Plain C# so leveling math is testable without a running scene.
    public class PlayerProgression
    {
        private const int BaseXpToNextLevel = 100;
        private const float XpGrowthPerLevel = 0.25f;

        public int Level { get; private set; } = 1;
        public int CurrentXp { get; private set; }
        public int XpToNextLevel { get; private set; } = BaseXpToNextLevel;

        public event Action Changed;
        public event Action<int> LeveledUp;

        public void AddXp(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            CurrentXp += amount;
            while (CurrentXp >= XpToNextLevel)
            {
                CurrentXp -= XpToNextLevel;
                Level++;
                XpToNextLevel = ComputeXpToNextLevel(Level);
                LeveledUp?.Invoke(Level);
            }

            Changed?.Invoke();
        }

        private static int ComputeXpToNextLevel(int level)
        {
            return Mathf.RoundToInt(BaseXpToNextLevel * (1f + (level - 1) * XpGrowthPerLevel));
        }

        // The save/load path — restores Level/CurrentXp directly and recomputes
        // XpToNextLevel via the same formula AddXp uses, so the two can never drift.
        // Doesn't fire Changed — called once, before anything subscribes.
        public void LoadState(int level, int currentXp)
        {
            Level = level;
            CurrentXp = currentXp;
            XpToNextLevel = ComputeXpToNextLevel(level);
        }
    }
}


Return the full file content for `Assets/Scripts/Progression/DailyMissions.cs`.