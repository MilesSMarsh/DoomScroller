using System;
using System.Collections;
using System.Linq;
using DoomScroller.Combat;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // A Resource-type feed card: tapping the card in the feed both enters and starts a
    // timed damage window (the player's equipped weapon(s) auto-attack this node, which
    // never attacks back — CombatPlaybackView.PlayTimedWindow, backed by
    // CombatResolver.CreateSimulatorForResource) — no separate "tap to start" gesture,
    // since the feed tap already is that. Swipe right for the reward page — same
    // two-page horizontal-swipe shell
    // CombatResolutionView uses (viewport/content, drag-to-swipe, a reward page that
    // only becomes reachable once resolved). Every run is swipeable once the timer ends,
    // win or not — there's no losing state, just "which thresholds did you clear."
    public class ResourceEncounterView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float SnapSpeed = 12f;
        private const float SnapThreshold = 0.5f;
        private const float HorizontalSwipeThreshold = 80f;
        private const float AxisLockThreshold = 12f;

        public event Action ReturnToFeedRequested;
        public event Action<EncounterData> EncounterWon;

        private EncounterData _data;
        private PlayerLoadout _loadout;
        private PlayerAttributes _attributes;
        private GameSettings _settings;

        private Text _titleText;
        private Image _icon;
        private CombatPlaybackView _playback;
        private Text _outcomeText;
        private Text _hintText;
        private GameObject _leaveButtonGo;
        private Coroutine _runCoroutine;

        private RectTransform _rewardLines;
        private GameObject _returnButtonGo;

        private RectTransform _viewport;
        private RectTransform _content;
        private LayoutElement _gatherPageLayout;
        private LayoutElement _rewardPageLayout;
        private float _pageWidth;
        private int _currentPage;
        private bool _canReachRewardPage;

        private Vector2 _dragStart;
        private bool? _dragIsHorizontal;
        private bool _isSnapping;
        private float _targetX;

        public static ResourceEncounterView Create(Transform parent)
        {
            var go = new GameObject("ResourceEncounterView", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = go.AddComponent<Image>();
            background.color = new Color(0.05f, 0.06f, 0.05f, 1f);

            var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(0, 0, NavBarController.Height, 0);
            layoutGroup.spacing = 0;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;

            var view = go.AddComponent<ResourceEncounterView>();

            var pagerAreaGo = new GameObject("PagerArea", typeof(RectTransform));
            pagerAreaGo.transform.SetParent(rect, false);
            var pagerAreaLayout = pagerAreaGo.AddComponent<LayoutElement>();
            pagerAreaLayout.minHeight = 0;
            pagerAreaLayout.preferredHeight = 0;
            pagerAreaLayout.flexibleHeight = 1;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            var viewportRect = (RectTransform)viewportGo.transform;
            viewportRect.SetParent(pagerAreaGo.transform, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();
            view._viewport = viewportRect;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            var contentRect = (RectTransform)contentGo.transform;
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(0, 1);
            contentRect.pivot = new Vector2(0, 0.5f);
            contentRect.sizeDelta = Vector2.zero;
            view._content = contentRect;

            var contentLayout = contentGo.AddComponent<HorizontalLayoutGroup>();
            contentLayout.spacing = 0;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;

            var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Page 0 — the node itself and the timed damage window, which starts the
            // instant Show() runs (entering the card from the feed IS the "start"
            // gesture — no separate confirm step).
            var gatherPageGo = new GameObject("GatherPage", typeof(RectTransform));
            gatherPageGo.transform.SetParent(contentRect, false);
            var gatherPageLayout = gatherPageGo.AddComponent<LayoutElement>();
            gatherPageLayout.flexibleWidth = 0;
            gatherPageLayout.flexibleHeight = 1;
            view._gatherPageLayout = gatherPageLayout;

            var gatherGroup = gatherPageGo.AddComponent<VerticalLayoutGroup>();
            gatherGroup.padding = new RectOffset(48, 48, 24, 24);
            gatherGroup.spacing = 14;
            gatherGroup.childAlignment = TextAnchor.UpperCenter;
            gatherGroup.childForceExpandWidth = true;
            gatherGroup.childForceExpandHeight = false;
            gatherGroup.childControlWidth = true;
            gatherGroup.childControlHeight = true;

            view._titleText = CreateLabel(gatherPageGo.transform, "Title", "", 48, FontStyle.Bold, 68);

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(gatherPageGo.transform, false);
            iconGo.AddComponent<LayoutElement>().preferredHeight = 160;
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.sizeDelta = new Vector2(160, 160);
            view._icon = iconGo.AddComponent<Image>();

            view._outcomeText = CreateLabel(gatherPageGo.transform, "Outcome", "", 40, FontStyle.Bold, 60);
            view._hintText = CreateLabel(gatherPageGo.transform, "SwipeHint", "◂ Swipe left for your reward", 28, FontStyle.Bold, 50);
            view._hintText.color = new Color(0.4f, 0.85f, 1f, 0.95f);
            view._hintText.gameObject.SetActive(false);

            view._playback = CombatPlaybackView.Create(gatherPageGo.transform, rect);
            view._playback.gameObject.SetActive(false);

            // Page 1 — reward summary, one line per threshold cleared.
            var rewardPageGo = new GameObject("RewardPage", typeof(RectTransform));
            rewardPageGo.transform.SetParent(contentRect, false);
            var rewardPageLayout = rewardPageGo.AddComponent<LayoutElement>();
            rewardPageLayout.flexibleWidth = 0;
            rewardPageLayout.flexibleHeight = 1;
            view._rewardPageLayout = rewardPageLayout;

            var rewardGroup = rewardPageGo.AddComponent<VerticalLayoutGroup>();
            rewardGroup.padding = new RectOffset(48, 48, 24, 24);
            rewardGroup.spacing = 14;
            rewardGroup.childAlignment = TextAnchor.UpperCenter;
            rewardGroup.childForceExpandWidth = true;
            rewardGroup.childForceExpandHeight = false;
            rewardGroup.childControlWidth = true;
            rewardGroup.childControlHeight = true;

            CreateLabel(rewardPageGo.transform, "RewardHeader", "Gathered", 44, FontStyle.Bold, 68);

            var linesGo = new GameObject("RewardLines", typeof(RectTransform));
            view._rewardLines = (RectTransform)linesGo.transform;
            view._rewardLines.SetParent(rewardPageGo.transform, false);
            var linesLayout = linesGo.AddComponent<VerticalLayoutGroup>();
            linesLayout.spacing = 8;
            linesLayout.childAlignment = TextAnchor.UpperCenter;
            linesLayout.childForceExpandWidth = true;
            linesLayout.childForceExpandHeight = false;
            linesLayout.childControlWidth = true;
            linesLayout.childControlHeight = true;

            view._returnButtonGo = CreateActionButton(rewardPageGo.transform, "ReturnButton", "Return", 120,
                new Color(1, 1, 1, 0.15f), () => view.ReturnToFeedRequested?.Invoke());

            view._leaveButtonGo = CreateActionButton(rect, "LeaveButton", "Leave", 120,
                new Color(1, 1, 1, 0.15f), () => view.OnLeaveClicked());

            go.SetActive(false);
            return view;
        }

        public void Show(EncounterData data, PlayerLoadout loadout, PlayerAttributes attributes, GameSettings settings)
        {
            gameObject.SetActive(true);
            _data = data;
            _loadout = loadout;
            _attributes = attributes;
            _settings = settings;

            _titleText.text = data.title;
            _icon.sprite = data.foregroundArt;
            _icon.color = data.foregroundArt != null ? Color.white : data.cardColor;

            _outcomeText.text = "";
            _hintText.gameObject.SetActive(false);
            _playback.gameObject.SetActive(true);
            _leaveButtonGo.SetActive(true);
            ClearRewardLines();

            _canReachRewardPage = false;
            _currentPage = 0;
            _isSnapping = false;
            Canvas.ForceUpdateCanvases();
            _pageWidth = _viewport.rect.width;
            _gatherPageLayout.minWidth = _pageWidth;
            _gatherPageLayout.preferredWidth = _pageWidth;
            _rewardPageLayout.minWidth = _pageWidth;
            _rewardPageLayout.preferredWidth = _pageWidth;
            Canvas.ForceUpdateCanvases();
            _content.anchoredPosition = Vector2.zero;

            if (_runCoroutine != null)
            {
                StopCoroutine(_runCoroutine);
            }

            _runCoroutine = StartCoroutine(RunTimedWindow());
        }

        public void Hide()
        {
            if (_runCoroutine != null)
            {
                StopCoroutine(_runCoroutine);
                _runCoroutine = null;
            }

            gameObject.SetActive(false);
        }

        // Nothing is ever at risk here (the node never attacks back), so leaving early
        // has no penalty — unlike CombatResolutionView.OnLeaveClicked, this never fires
        // EncounterWon or applies any cost.
        private void OnLeaveClicked()
        {
            if (_runCoroutine != null)
            {
                StopCoroutine(_runCoroutine);
                _runCoroutine = null;
            }

            ReturnToFeedRequested?.Invoke();
        }

        private IEnumerator RunTimedWindow()
        {
            // playerStartHp is irrelevant here — the player is never at risk
            // (enemyCanAttack: false), so nothing downstream ever reads it back.
            var sim = CombatResolver.CreateSimulatorForResource(_loadout, _attributes, _data, 1);
            yield return StartCoroutine(_playback.PlayTimedWindow(sim, _loadout, _data.foregroundArt,
                _data.cardColor, _data.resourceTimerSeconds));

            var damage = _playback.TotalDamageDealt;
            _outcomeText.text = $"Dealt {damage} damage!";
            _hintText.gameObject.SetActive(true);
            _leaveButtonGo.SetActive(false);
            _canReachRewardPage = true;

            BuildRewardLines(damage);
            EncounterWon?.Invoke(_data);
            _runCoroutine = null;
        }

        // Grants every threshold whose damageRequired is met (they stack — see
        // ResourceThreshold's own doc comment), each scaled by the player's Expertise
        // the same way the old flat yield used to be.
        private void BuildRewardLines(int damage)
        {
            ClearRewardLines();

            var cleared = (_data.resourceThresholds ?? Array.Empty<ResourceThreshold>())
                .Where(t => t.rewardMaterial != null && damage >= t.damageRequired)
                .OrderBy(t => t.damageRequired)
                .ToList();

            if (cleared.Count == 0)
            {
                CreateLabel(_rewardLines, "NoReward", "No resources gathered — try a weapon better suited to this node.",
                    28, FontStyle.Italic, 60);
                return;
            }

            foreach (var threshold in cleared)
            {
                var amount = Mathf.Max(1, Mathf.RoundToInt(threshold.rewardAmount * _attributes.ResourceYieldMultiplier));
                for (var i = 0; i < amount; i++)
                {
                    _loadout.AddToInventory(MaterialGenerator.GenerateFromDefinition(threshold.rewardMaterial, threshold.rewardRarity));
                }

                CreateRewardLine(threshold, amount);
            }
        }

        // Icon (with its rarity border, same as every other gear row) beside the
        // "+N Rarity Material" label, centered as a unit on the reward page.
        private void CreateRewardLine(ResourceThreshold threshold, int amount)
        {
            var lineGo = new GameObject($"Line_{threshold.damageRequired}", typeof(RectTransform));
            lineGo.transform.SetParent(_rewardLines, false);
            var lineLayout = lineGo.AddComponent<HorizontalLayoutGroup>();
            lineLayout.spacing = 16;
            lineLayout.childAlignment = TextAnchor.MiddleCenter;
            lineLayout.childForceExpandWidth = false;
            lineLayout.childForceExpandHeight = false;
            lineLayout.childControlWidth = true;
            lineLayout.childControlHeight = true;

            var (icon, border) = GearRowFactory.CreateIconFrame(lineGo.transform, 64);
            GearRowFactory.SetIcon(icon, threshold.rewardMaterial.icon, threshold.rewardMaterial.itemColor);
            GearRowFactory.SetRarityBorder(border, threshold.rewardRarity);
            icon.raycastTarget = false;

            var label = CreateLabel(lineGo.transform, "Label",
                $"+{amount} {threshold.rewardRarity} {threshold.rewardMaterial.materialName}", 34, FontStyle.Italic, 64);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.alignment = TextAnchor.MiddleLeft;
        }

        private void ClearRewardLines()
        {
            for (var i = _rewardLines.childCount - 1; i >= 0; i--)
            {
                Destroy(_rewardLines.GetChild(i).gameObject);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragStart = eventData.position;
            _dragIsHorizontal = null;
            _isSnapping = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            var delta = eventData.position - _dragStart;

            if (_dragIsHorizontal == null)
            {
                if (Mathf.Abs(delta.x) < AxisLockThreshold && Mathf.Abs(delta.y) < AxisLockThreshold)
                {
                    return;
                }

                _dragIsHorizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            }

            if (_dragIsHorizontal == true)
            {
                var pos = _content.anchoredPosition;
                pos.x = -_currentPage * _pageWidth + delta.x;
                _content.anchoredPosition = pos;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_dragIsHorizontal != true)
            {
                SnapTo(_currentPage);
                return;
            }

            var delta = eventData.position - _dragStart;
            var desired = Mathf.Abs(delta.x) >= HorizontalSwipeThreshold
                ? _currentPage + (delta.x < 0 ? 1 : -1)
                : _currentPage;

            var maxPage = _canReachRewardPage ? 1 : 0;
            SnapTo(Mathf.Clamp(desired, 0, maxPage));
        }

        private void SnapTo(int page)
        {
            _currentPage = page;
            _targetX = -page * _pageWidth;
            _isSnapping = true;
        }

        private void LateUpdate()
        {
            if (!_isSnapping)
            {
                return;
            }

            var pos = _content.anchoredPosition;
            pos.x = Mathf.Lerp(pos.x, _targetX, Time.unscaledDeltaTime * SnapSpeed);

            if (Mathf.Abs(pos.x - _targetX) < SnapThreshold)
            {
                pos.x = _targetX;
                _isSnapping = false;
            }

            _content.anchoredPosition = pos;
        }

        private static Text CreateLabel(Transform parent, string name, string content, int fontSize, FontStyle style,
            float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;

            return text;
        }

        private static GameObject CreateActionButton(Transform parent, string name, string label, float height,
            Color backgroundColor, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = backgroundColor;

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;

            var button = go.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var labelText = labelGo.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 40;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;

            return go;
        }
    }
}
