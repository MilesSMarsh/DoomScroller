using System;

namespace DoomScroller.Progression
{
    // Plain C# so it's testable without a scene, same convention as PlayerProgression/
    // PlayerHealth. A tiny persistent settings bag — just the revive-potion auto-consume
    // flag today, room to grow later without overloading PlayerLoadout/PlayerHealth with
    // an unrelated concern. Persisted via SaveSystem (see ProfileController).
    public class GameSettings
    {
        public event Action Changed;

        // Defaults to true (Miles, 2026-11-13) until the per-card Revive toggle exists.
        // When on, a carried Revive potion is armed for each fight and fires
        // automatically inside the simulation (see FightInputBuilder).
        public bool AutoConsumeRevivePotions { get; private set; } = true;

        public void SetAutoConsumeRevivePotions(bool value)
        {
            if (AutoConsumeRevivePotions == value)
            {
                return;
            }

            AutoConsumeRevivePotions = value;
            Changed?.Invoke();
        }

        // The save/load path. Doesn't fire Changed — called once, before anything
        // subscribes.
        public void LoadState(bool autoConsumeRevivePotions)
        {
            AutoConsumeRevivePotions = autoConsumeRevivePotions;
        }
    }
}
