using System;
using DoomScroller.CombatCore;

namespace DoomScroller.Combat
{
    // One beat's effect on both HP bars. PlayerHp/EnemyHp are the values right after the
    // beat lands (a player killed at the Revive beat shows 0 here). When Revived is set
    // the armed Revive fires straight after this beat and PlayerHpAfterRevive is the
    // restored HP; otherwise it equals PlayerHp.
    public readonly struct BeatHp
    {
        public readonly int PlayerHp;
        public readonly int EnemyHp;
        public readonly bool Revived;
        public readonly int PlayerHpAfterRevive;

        public BeatHp(int playerHp, int enemyHp, bool revived, int playerHpAfterRevive)
        {
            PlayerHp = playerHp;
            EnemyHp = enemyHp;
            Revived = revived;
            PlayerHpAfterRevive = playerHpAfterRevive;
        }
    }

    // Pure helpers that turn a finished FightResult into what the playback view shows.
    // No Unity types, so it works the same for a device-resolved or server-resolved fight.
    public static class FightReplay
    {
        // A win that ended below this fraction of the fight's starting HP is a "close call".
        public const float CloseCallHealthFraction = 0.25f;

        // HP a Revive restores: the percent of the fight's starting HP, rounded up, never
        // below 1. Same rule as CombatSimulator (FightResult does not carry the number).
        public static int ReviveHp(int playerStartHp, int revivePercent)
        {
            return Math.Max(1, (Math.Max(0, playerStartHp) * revivePercent + 99) / 100);
        }

        // HP of both sides after each beat, in order. Misses deal no damage.
        public static BeatHp[] BuildHpTimeline(FightResult result, int playerStartHp, int enemyMaxHp, int reviveHp)
        {
            var beats = result.beats ?? new CombatBeat[0];
            var timeline = new BeatHp[beats.Length];
            var playerHp = playerStartHp;
            var enemyHp = enemyMaxHp;

            for (var i = 0; i < beats.Length; i++)
            {
                var beat = beats[i];
                if (!beat.WasMiss)
                {
                    if (beat.Actor == CombatActor.Player)
                    {
                        enemyHp = Math.Max(0, enemyHp - beat.DisplayDamage);
                    }
                    else
                    {
                        playerHp = Math.Max(0, playerHp - beat.DisplayDamage);
                    }
                }

                var revived = result.reviveFired && i == result.reviveBeatIndex;
                var hpAfterBeat = playerHp;
                if (revived)
                {
                    playerHp = reviveHp;
                }

                timeline[i] = new BeatHp(hpAfterBeat, enemyHp, revived, playerHp);
            }

            return timeline;
        }

        public static bool IsCloseCall(FightResult result, int playerStartHp)
        {
            return result.outcome == FightOutcome.Win && playerStartHp > 0 &&
                (float)result.finalPlayerHp / playerStartHp < CloseCallHealthFraction;
        }
    }
}
