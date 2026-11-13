#if UNITY_EDITOR || DEVELOPMENT_BUILD
using DoomScroller.Feed;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.UI
{
    // Dev-only in-game panel (Editor + development builds): a small "Debug" button on the
    // canvas that opens two buttons and a result line. Self-installs after scene load, so no
    // scene edit is needed and release builds never contain it. Logic is in
    // BorderlineOddsDebugActions; this only builds placeholder UI and calls it.
    public class DebugPanelController : MonoBehaviour
    {
        private FeedController _feed;
        private GameObject _panel;
        private Text _result;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var feed = FindFirstObjectByType<FeedController>();
            var canvas = feed != null ? feed.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                canvas = FindFirstObjectByType<Canvas>();
            }

            if (feed == null || canvas == null)
            {
                return;
            }

            var go = new GameObject("DebugPanel", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<DebugPanelController>().Build(feed);
            rect.SetAsLastSibling();
        }

        private void Build(FeedController feed)
        {
            _feed = feed;
            var root = (RectTransform)transform;

            // Small toggle button, bottom-left, above the feed's bottom edge.
            MakeButton(root, "DebugButton", "Debug", new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(24, 24), new Vector2(180, 90), TogglePanel);

            _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var panelRect = (RectTransform)_panel.transform;
            panelRect.SetParent(root, false);
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(0, 0);
            panelRect.pivot = new Vector2(0, 0);
            panelRect.anchoredPosition = new Vector2(24, 130);
            panelRect.sizeDelta = new Vector2(700, 560);
            _panel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.95f);

            MakeButton(panelRect, "BorderlineButton", "Borderline Odds HP", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(640, 100), OnBorderline);
            MakeButton(panelRect, "PenaltyButton", "Apply Defeat Penalty", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(0, -144), new Vector2(640, 100), OnPenalty);

            _result = MakeLabel(panelRect, "Result", "", 30);
            var resultRect = (RectTransform)_result.transform;
            resultRect.anchorMin = Vector2.zero;
            resultRect.anchorMax = Vector2.one;
            resultRect.offsetMin = new Vector2(24, 24);
            resultRect.offsetMax = new Vector2(-24, -264);
            _result.alignment = TextAnchor.UpperLeft;

            _panel.SetActive(false);
        }

        private void TogglePanel() => _panel.SetActive(!_panel.activeSelf);

        private void OnBorderline()
        {
            var message = BorderlineOddsDebugActions.SetBorderlineHp(_feed, out _);
            _result.text = message;
            Debug.Log("Borderline Odds HP: " + message);
        }

        private void OnPenalty()
        {
            var message = BorderlineOddsDebugActions.ApplyPenalty(_feed);
            _result.text = message;
            Debug.Log("Apply Defeat Penalty: " + message);
        }

        private static Text MakeLabel(Transform parent, string name, string content, int size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        private static void MakeButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(1, 1, 1, 0.25f);
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var text = MakeLabel(rect, "Label", label, 34);
            var labelRect = (RectTransform)text.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }
    }
}
#endif
