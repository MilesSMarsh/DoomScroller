using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace DoomScroller.UI
{
    // Guarantees an EventSystem exists so UI (ScrollRect, buttons) receives pointer/touch
    // input, without needing one hand-authored into every scene.
    public static class EventSystemBootstrapper
    {
        public static void EnsureExists()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
