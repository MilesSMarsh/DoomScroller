using UnityEngine;

namespace DoomScroller.UI
{
    // Insets this RectTransform to Screen.safeArea so every UI panel parented under it
    // (Feed, EncounterFlow, ProfileController, NavBar — everything except the full-bleed
    // Background) automatically clears device notches, status bars, punch-hole cameras,
    // and the iOS home-indicator gesture bar, purely via normal stretch anchors. No
    // per-screen or per-device changes needed anywhere else in the UI code.
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaController : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            // Cheap early-out: only recompute on an actual change (device rotation, a
            // resized Game view in the Editor, iOS split-screen/Slide Over resize).
            if (Screen.safeArea != _lastSafeArea || Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            {
                Apply();
            }
        }

        private void Apply()
        {
            var safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;

            var anchorMin = safeArea.position;
            var anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rect.anchorMin = anchorMin;
            _rect.anchorMax = anchorMax;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
