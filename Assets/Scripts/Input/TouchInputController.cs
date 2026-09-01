using UnityEngine;
using UnityEngine.InputSystem;

namespace DoomScroller.Input
{
    // Thin MonoBehaviour: feeds raw pointer/touch events into TouchGestureService
    // and logs the result. This is the Phase 1 placeholder proving tap/swipe
    // registers correctly; later phases replace the Debug.Log handlers with
    // real feed-scroll / encounter-swipe behavior.
    public class TouchInputController : MonoBehaviour
    {
        [SerializeField] private float swipeMinDistance = 50f;
        [SerializeField] private float tapMaxDuration = 0.3f;

        private TouchGestureService _gestureService;

        private void Awake()
        {
            _gestureService = new TouchGestureService(swipeMinDistance, tapMaxDuration);
        }

        private void OnEnable()
        {
            _gestureService.Tapped += HandleTap;
            _gestureService.Swiped += HandleSwipe;
        }

        private void OnDisable()
        {
            _gestureService.Tapped -= HandleTap;
            _gestureService.Swiped -= HandleSwipe;
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            if (pointer.press.wasPressedThisFrame)
            {
                _gestureService.BeginTouch(pointer.position.ReadValue(), Time.unscaledTime);
            }
            else if (pointer.press.wasReleasedThisFrame)
            {
                _gestureService.EndTouch(pointer.position.ReadValue(), Time.unscaledTime);
            }
        }

        private static void HandleTap(Vector2 position)
        {
            Debug.Log($"[TouchInput] Tap at {position}");
        }

        private static void HandleSwipe(SwipeDirection direction, Vector2 delta)
        {
            Debug.Log($"[TouchInput] Swipe {direction} (delta: {delta})");
        }
    }
}
