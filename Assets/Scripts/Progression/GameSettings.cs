using System;

namespace DoomScroller.Progression
{
    // Plain C# so it's testable without a scene, same convention as PlayerProgression/
    // PlayerHealth. A tiny persistent settings bag — just the revive-potion auto-consume
    // flag today, room to grow later without overloading PlayerLoadout/PlayerHealth with
    // an unrelated concern. Session-only, like everything else in this project (no
    // save/load system exists yet).
    public class GameSettings
    {
        public event Action Changed;

        // Defaults to false: a revive potion pauses the fight and asks, unless the
        // player opts into auto-consuming instead (see CombatPlaybackView.Play).
        public bool AutoConsumeRevivePotions { get; private set; }

        // Defaults to false: active abilities auto-cast off cooldown, matching today's
        // behavior. Flipping this on makes them sit ready until tapped instead (see
        // CombatSimulator's manual-ability tracking and CombatPlaybackView's cooldown
        // widgets).
        public bool ManualAbilityCasting { get; private set; }

        public void SetAutoConsumeRevivePotions(bool value)
        {
            if (AutoConsumeRevivePotions == value)
            {
                return;
            }

            AutoConsumeRevivePotions = value;
            Changed?.Invoke();
        }

        public void SetManualAbilityCasting(bool value)
        {
            if (ManualAbilityCasting == value)
            {
                return;
            }

            ManualAbilityCasting = value;
            Changed?.Invoke();
        }
    }
}
