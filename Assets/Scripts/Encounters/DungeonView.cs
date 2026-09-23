using System;
using System.Collections.Generic;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // Multi-step dungeon run: a horizontal sequence of pages (DungeonStepPageView), one
    // per generated DungeonStep, paged with a sideways swipe. Swiping right from the
    // frontier (the furthest step reached, already won) advances into a new page and
    // resolves it; swiping right/left anywhere behind the frontier just moves the camera
    // between already-conquered pages, re-showing their stored results — no re-fighting.
    // A downward swipe (or the always-visible Return button, mid-run) prompts to leave;
    // leaving before the treasure room still locks the feed card, same as clearing or
    // dying does.
    public class DungeonView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float SnapSpeed = 12f;
        private const float SnapThreshold = 0.5f;
        private const float HorizontalSwipeThreshold = 80f;
        private const float LeaveSwipeThreshold = 120f;
        private const float AxisLockThreshold = 12f;

        public event Action ReturnToFeedRequested;
        public event Action<EncounterData> EncounterWon;
        public event Action<EncounterData> EncounterLost;
        public event Action<EncounterData> EncounterAbandoned;

        private RectTransform _viewport;
        private RectTransform _content;
        private readonly List<DungeonStepPageView> _pages = new();
        private GameObject _leaveDialogGo;

        private EncounterData _data;
        private PlayerLoadout _loadout;
        private PlayerProgression _progression;
        private PlayerHealth _health;
        private PlayerAttributes _attributes;
        private GameSettings _settings;
        private PlayerWallet _wallet;
        private IReadOnlyList<DungeonStep> _steps;

        private int _frontierIndex;
        private int _currentPage;
        private float _pageWidth;
        private bool _runOver;
        // The running HP total carried step-to-step within this run — a fight no longer
        // always starts fresh at _health.CurrentMaxHealth; only a fresh run or a rest
        // step resets it (see ResolveAndReveal/OnPageResolved).
        private int _runningPlayerHp;
        // The running gold total across the whole run — never actually granted to the
        // wallet until the treasure room's chest is opened (see
        // DungeonStepPageView.AccumulatedGold); simply discarded on a loss.
        private int _accumulatedGold;
        // True from the instant the treasure-room step wins until its RewardChestView is
        // tapped open — gates the Return button so a run-clearing win can't be swiped
        // past without claiming it. Never set for a mid-run loss (nothing to claim then).
        private bool _pendingChestClaim;

        private Vector2 _dragStart;
        private bool? _dragIsHorizontal;
        private bool _isSnapping;
        private float _targetX;

        public static DungeonView Create(Transform parent)
        {
            var go = new GameObject("DungeonView", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = go.AddComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.06f, 1f);

            var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(0, 0, NavBarController.Height, 0);
            layoutGroup.spacing = 0;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;

            var view = go.AddComponent<DungeonView>();

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

            // Mirrors FeedController's Content exactly (stretched on the cross axis,
            // pinned to one edge on the paging axis, sized along the paging axis by a
            // ContentSizeFitter) — just transposed to horizontal paging instead of
            // vertical.
            var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateButton(rect, "ReturnButton", "Leave", () => view.OnReturnButtonClicked());

            // Created last so it renders on top of everything else in this view.
            view._leaveDialogGo = CreateLeaveDialog(rect, view);

            go.SetActive(false);
            return view;
        }

        public void Show(EncounterData data, PlayerLoadout loadout, PlayerProgression progression, PlayerHealth health,
            PlayerAttributes attributes, GameSettings settings, PlayerWallet wallet)
        {
            _data = data;
            _loadout = loadout;
            _progression = progression;
            _health = health;
            _attributes = attributes;
            _settings = settings;
            _wallet = wallet;

            foreach (var page in _pages)
            {
                Destroy(page.gameObject);
            }

            _pages.Clear();
            gameObject.SetActive(true);
            HideLeaveDialog();

            // Force a rebuild so the viewport's real size is available before pages are
            // sized to it — same technique FeedController uses for _pageHeight.
            Canvas.ForceUpdateCanvases();
            _pageWidth = _viewport.rect.width;

            _steps = DungeonGenerator.Generate(data);
            foreach (var step in _steps)
            {
                var page = DungeonStepPageView.Create(_content, _pageWidth);
                page.RewardClaimed += OnRewardClaimed;
                _pages.Add(page);
            }

            _frontierIndex = 0;
            _currentPage = 0;
            _runOver = false;
            _pendingChestClaim = false;
            // A fresh run always starts at the player's real current ceiling — carried
            // forward (or reset by a rest step) from here on, see OnPageResolved.
            _runningPlayerHp = _health.CurrentMaxHealth;
            _accumulatedGold = 0;
            _content.anchoredPosition = Vector2.zero;
            _isSnapping = false;

            ResolveAndReveal(0);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void ResolveAndReveal(int index)
        {
            var page = _pages[index];
            page.Resolved += OnPageResolved;
            page.Resolve(_steps[index], _loadout, _progression, _health, _attributes, _settings, _wallet, _runningPlayerHp,
                _accumulatedGold);
        }

        // Health's own defeat penalty is applied inside DungeonStepPageView itself (it
        // has the fresh numbers to narrate immediately) — this just handles the run-level
        // consequence of that same loss, or of clearing the treasure room.
        private void OnPageResolved(DungeonStep step, bool wasVictory)
        {
            _pages[step.DepthIndex].Resolved -= OnPageResolved;

            if (!wasVictory)
            {
                _runOver = true;
                EncounterLost?.Invoke(_data);
            }
            else if (step.IsFinalTreasureRoom)
            {
                _runOver = true;
                _pendingChestClaim = true;
                EncounterWon?.Invoke(_data);
            }
            else
            {
                // Run continues — carries forward either the damage taken this step
                // (a normal fight) or a full heal-up (a rest step); either way
                // DungeonStepPageView.EndingPlayerHp already encodes which happened.
                _runningPlayerHp = _pages[step.DepthIndex].EndingPlayerHp;
                _accumulatedGold = _pages[step.DepthIndex].AccumulatedGold;
            }
        }

        private void OnRewardClaimed()
        {
            _pendingChestClaim = false;
        }

        private void OnReturnButtonClicked()
        {
            if (_runOver)
            {
                // A treasure-room win holds the door shut until its chest is opened —
                // see DungeonStepPageView.RewardClaimed. A loss has nothing to claim, so
                // _pendingChestClaim is never true there.
                if (!_pendingChestClaim)
                {
                    ReturnToFeedRequested?.Invoke();
                }
            }
            else
            {
                ShowLeaveDialog();
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
            var delta = eventData.position - _dragStart;

            if (_dragIsHorizontal == false)
            {
                // Swipe down (finger moved down => screen-space Y decreases) prompts to
                // leave; anything else on the vertical axis is ignored.
                SnapTo(_currentPage);
                if (-delta.y > LeaveSwipeThreshold)
                {
                    ShowLeaveDialog();
                }

                return;
            }

            if (_dragIsHorizontal == true)
            {
                var desired = Mathf.Abs(delta.x) >= HorizontalSwipeThreshold
                    ? _currentPage + (delta.x < 0 ? 1 : -1)
                    : _currentPage;
                NavigateTo(desired);
                return;
            }

            // Drag never crossed the axis-lock threshold — treat as a tap, snap in place.
            SnapTo(_currentPage);
        }

        private void NavigateTo(int desiredPage)
        {
            if (desiredPage > _currentPage && _currentPage == _frontierIndex)
            {
                var frontierPage = _pages[_frontierIndex];
                if (frontierPage.IsResolved && frontierPage.LastResultWasVictory && _frontierIndex + 1 < _steps.Count)
                {
                    _frontierIndex++;
                    SnapTo(_frontierIndex);
                    ResolveAndReveal(_frontierIndex);
                    return;
                }

                // Not won yet, lost, or already at the end of the dungeon — nothing to
                // advance into.
                SnapTo(_currentPage);
                return;
            }

            // Browsing within already-reached territory — no re-resolution.
            SnapTo(Mathf.Clamp(desiredPage, 0, _frontierIndex));
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

        private void ShowLeaveDialog()
        {
            _leaveDialogGo.SetActive(true);
        }

        private void HideLeaveDialog()
        {
            _leaveDialogGo.SetActive(false);
        }

        private void ConfirmLeave()
        {
            HideLeaveDialog();
            _runOver = true;
            // The one path DungeonStepPageView can't see or apply itself — walking away
            // is a DungeonView-level gesture, not something any single page knows about.
            _health.ApplyDefeatPenalty();
            EncounterAbandoned?.Invoke(_data);
            ReturnToFeedRequested?.Invoke();
        }

        private static GameObject CreateLeaveDialog(Transform parent, DungeonView view)
        {
            var overlayGo = new GameObject("LeaveDialog", typeof(RectTransform));
            var overlayRect = (RectTransform)overlayGo.transform;
            overlayRect.SetParent(parent, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            // The root panel's own VerticalLayoutGroup (childControlHeight/Width=true)
            // would otherwise seize this child on activation and collapse it into the
            // layout instead of leaving it as a full-screen overlay — same bug and fix
            // as EquipSlotDetailView's delete-confirm dialog.
            overlayGo.AddComponent<LayoutElement>().ignoreLayout = true;
            overlayGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var boxGo = new GameObject("Box", typeof(RectTransform));
            var boxRect = (RectTransform)boxGo.transform;
            boxRect.SetParent(overlayRect, false);
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(760, 340);
            boxGo.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.16f, 1f);

            var boxLayout = boxGo.AddComponent<VerticalLayoutGroup>();
            boxLayout.padding = new RectOffset(32, 32, 28, 28);
            boxLayout.spacing = 20;
            boxLayout.childAlignment = TextAnchor.MiddleCenter;
            boxLayout.childForceExpandWidth = true;
            boxLayout.childForceExpandHeight = false;
            boxLayout.childControlWidth = true;
            boxLayout.childControlHeight = true;

            var message = CreateLabel(boxRect, "Message", "Leave the dungeon?\nProgress so far is kept, but this run ends.", 30, FontStyle.Normal);
            SetFixedHeight(message.GetComponent<LayoutElement>(), 120);

            var buttonRowGo = new GameObject("ButtonRow", typeof(RectTransform));
            buttonRowGo.transform.SetParent(boxRect, false);
            SetFixedHeight(buttonRowGo.AddComponent<LayoutElement>(), 90);
            var buttonRowLayout = buttonRowGo.AddComponent<HorizontalLayoutGroup>();
            buttonRowLayout.spacing = 16;
            buttonRowLayout.childForceExpandWidth = false;
            buttonRowLayout.childForceExpandHeight = false;
            buttonRowLayout.childControlWidth = true;
            buttonRowLayout.childControlHeight = true;

            CreateActionButton(buttonRowGo.transform, "CancelButton", "Cancel", 340, 90,
                new Color(1f, 1f, 1f, 0.15f), () => view.HideLeaveDialog());
            CreateActionButton(buttonRowGo.transform, "LeaveButton", "Leave", 340, 90,
                new Color(0.55f, 0.15f, 0.15f, 0.85f), () => view.ConfirmLeave());

            overlayGo.SetActive(false);
            return overlayGo;
        }

        private static void SetFixedHeight(LayoutElement layoutElement, float height)
        {
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleHeight = 0;
        }

        private static Text CreateLabel(Transform parent, string name, string content, int fontSize, FontStyle style)
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
            layoutElement.preferredHeight = fontSize + 24;

            return text;
        }

        private static void CreateActionButton(Transform parent, string name, string label,
            float width, float height, Color backgroundColor, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            layoutElement.flexibleWidth = 0;
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleHeight = 0;

            var image = go.AddComponent<Image>();
            image.color = backgroundColor;

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
            labelText.fontSize = 26;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false;
        }

        private static void CreateButton(Transform parent, string name, string label, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.15f);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 120;

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
        }
    }
}
