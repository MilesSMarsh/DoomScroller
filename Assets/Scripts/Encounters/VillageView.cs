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
    // A horizontally swipeable hub of establishments — reachable from the feed like any
    // other card (EncounterType.Tavern), but it never fights and never locks
    // (EncounterFlowController never wires its own events into
    // FeedController.MarkWon/MarkLost/MarkAbandoned, same as the standalone Tavern this
    // replaces) — the whole point is it's always there to revisit. Paging mirrors
    // DungeonView's horizontal drag/snap mechanism, minus the frontier lock: every
    // establishment is always open, so navigation is free in both directions.
    //
    // Adding a new establishment later is a two-line change: write its own
    // `static XyzView Create(Transform parent)` view class (same shape as every other
    // view in this project), then add one entry to Establishments below.
    //
    // Swiping is only recognized starting on the EstablishmentStrip bar (see
    // SwipeForwarder below) — not this whole screen — specifically so an establishment's
    // own content is free to hold a ScrollRect of its own (e.g. VillageBlacksmithView's
    // recipe lists) without it fighting the page-swipe gesture for the same drag.
    public class VillageView : MonoBehaviour
    {
        private const float SnapSpeed = 12f;
        private const float SnapThreshold = 0.5f;
        private const float HorizontalSwipeThreshold = 80f;

        // The single registration point for the Village's establishments — label plus a
        // static Create(Transform) factory, same pattern the rest of this project's
        // views already use. A stub establishment (QuestBoard/Blacksmith) needs nothing
        // beyond this line; a stateful one also needs a field + a cast below in Show().
        private static readonly (string Label, Func<Transform, MonoBehaviour>)[] Establishments =
        {
            ("Tavern", VillageTavernView.Create),
            ("Inn", VillageInnView.Create),
            ("Market", VillageMarketView.Create),
            ("Alchemist", VillageAlchemistView.Create),
            ("Quest Board", VillageQuestBoardView.Create),
            ("Blacksmith", VillageBlacksmithView.Create),
        };

        public event Action ReturnToFeedRequested;

        private RectTransform _viewport;
        private RectTransform _content;
        private readonly List<LayoutElement> _pageLayouts = new();
        private VillageTavernView _tavernView;
        private VillageInnView _innView;
        private VillageMarketView _marketView;
        private VillageAlchemistView _alchemistView;
        private VillageBlacksmithView _blacksmithView;

        // The persistent "you are here, swipe for these" strip above the pager — see
        // UpdateEstablishmentStrip.
        private Text _prevLabel;
        private Text _currentLabel;
        private Text _nextLabel;

        private int _pageCount;
        private int _currentPage;
        private float _pageWidth;
        private bool _isSnapping;
        private float _targetX;

        private Vector2 _dragStart;

        public static VillageView Create(Transform parent)
        {
            var go = new GameObject("Village", typeof(RectTransform));
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

            var view = go.AddComponent<VillageView>();

            // A persistent bar above the pager — current establishment centered and
            // highlighted, with the establishment on either side named next to a swipe
            // arrow (blank/no-arrow at either end of the list). Its own background color
            // (distinct from the page behind it) so it visually reads as a control
            // surface, not text floating on the page — this is also the *only* place a
            // swipe is recognized (see SwipeForwarder), so it needs to look tappable.
            var stripGo = new GameObject("EstablishmentStrip", typeof(RectTransform));
            stripGo.transform.SetParent(rect, false);
            var stripLayout = stripGo.AddComponent<LayoutElement>();
            SetFixedHeight(stripLayout, 130);
            stripGo.AddComponent<Image>().color = new Color(0.22f, 0.16f, 0.1f, 1f);
            stripGo.AddComponent<SwipeForwarder>().Target = view;

            var stripGroup = stripGo.AddComponent<HorizontalLayoutGroup>();
            stripGroup.padding = new RectOffset(24, 24, 0, 0);
            stripGroup.spacing = 12;
            stripGroup.childAlignment = TextAnchor.MiddleCenter;
            stripGroup.childForceExpandWidth = true;
            stripGroup.childForceExpandHeight = true;
            stripGroup.childControlWidth = true;
            stripGroup.childControlHeight = true;

            view._prevLabel = CreateLabel(stripGo.transform, "PrevLabel", "", 26, FontStyle.Normal);
            view._prevLabel.alignment = TextAnchor.MiddleLeft;
            view._prevLabel.color = new Color(1f, 1f, 1f, 0.4f);

            view._currentLabel = CreateLabel(stripGo.transform, "CurrentLabel", "", 32, FontStyle.Bold);
            view._currentLabel.color = new Color(0.4f, 0.85f, 1f, 1f);

            view._nextLabel = CreateLabel(stripGo.transform, "NextLabel", "", 26, FontStyle.Normal);
            view._nextLabel.alignment = TextAnchor.MiddleRight;
            view._nextLabel.color = new Color(1f, 1f, 1f, 0.4f);

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

            // Force a rebuild so the viewport's real size is available before pages are
            // sized to it — same technique DungeonView/FeedController use.
            Canvas.ForceUpdateCanvases();
            var pageWidth = viewportRect.rect.width;
            view._pageWidth = pageWidth;
            view._pageCount = Establishments.Length;

            foreach (var (label, createEstablishment) in Establishments)
            {
                var pageGo = new GameObject(label.Replace(" ", ""), typeof(RectTransform));
                pageGo.transform.SetParent(contentRect, false);
                var pageLayout = pageGo.AddComponent<LayoutElement>();
                pageLayout.minWidth = pageWidth;
                pageLayout.preferredWidth = pageWidth;
                pageLayout.flexibleWidth = 0;
                pageLayout.minHeight = 0;
                pageLayout.preferredHeight = 0;
                pageLayout.flexibleHeight = 1;
                view._pageLayouts.Add(pageLayout);

                var pageLayoutGroup = pageGo.AddComponent<VerticalLayoutGroup>();
                pageLayoutGroup.padding = new RectOffset(48, 48, 24, 24);
                pageLayoutGroup.spacing = 20;
                // Middle, not Upper: an establishment's own content (Tavern/Inn/Alchemist/
                // QuestBoard especially) is nowhere near tall enough to reach the bottom of
                // the page on most screens — UpperCenter left a large dead gap of bare
                // background below it, reading as "doesn't fill the window." Centering
                // distributes that same gap above and below instead, which is the same fix
                // ResourceEncounterView already uses for its own single-purpose screen.
                pageLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
                pageLayoutGroup.childForceExpandWidth = true;
                pageLayoutGroup.childForceExpandHeight = false;
                pageLayoutGroup.childControlWidth = true;
                pageLayoutGroup.childControlHeight = true;

                // No per-page title label here — the persistent strip above the pager
                // already names the current establishment; a second big title on every
                // page just duplicated it.

                // An establishment is free to wrap its own content in a ScrollAreaFactory
                // area if it needs to (e.g. VillageBlacksmithView's recipe lists) — the
                // page-swipe gesture only ever starts on the EstablishmentStrip bar (see
                // SwipeForwarder), so a ScrollRect here no longer has anything to
                // conflict with.
                var establishment = createEstablishment(pageGo.transform);

                if (establishment is VillageTavernView tavern)
                {
                    view._tavernView = tavern;
                }
                else if (establishment is VillageInnView inn)
                {
                    view._innView = inn;
                }
                else if (establishment is VillageMarketView market)
                {
                    view._marketView = market;
                }
                else if (establishment is VillageAlchemistView alchemist)
                {
                    view._alchemistView = alchemist;
                }
                else if (establishment is VillageBlacksmithView blacksmith)
                {
                    view._blacksmithView = blacksmith;
                }
            }

            CreateButton(rect, "ReturnButton", "Leave", () => view.ReturnToFeedRequested?.Invoke());

            go.SetActive(false);
            return view;
        }

        public void Show(EncounterData data, PlayerLoadout loadout, PlayerHealth health, PlayerWallet wallet,
            PlayerAttributes attributes)
        {
            gameObject.SetActive(true);

            _tavernView.Show(loadout, wallet, attributes, data);
            _innView.Show(health);
            _marketView.Show(loadout, wallet, attributes, data);
            _alchemistView.Show(loadout, wallet, attributes);
            _blacksmithView.Show(loadout, wallet, attributes);

            // Create() ran at EncounterFlowController.Awake() time, when the canvas may
            // not have settled to its real screen size yet — same lesson learned on
            // CombatResolutionView's reward pager. Re-measure and re-apply on every
            // Show() instead of trusting whatever pageWidth Create() computed once.
            Canvas.ForceUpdateCanvases();
            _pageWidth = _viewport.rect.width;
            foreach (var pageLayout in _pageLayouts)
            {
                pageLayout.minWidth = _pageWidth;
                pageLayout.preferredWidth = _pageWidth;
            }

            Canvas.ForceUpdateCanvases();
            _currentPage = 0;
            _isSnapping = false;
            _content.anchoredPosition = Vector2.zero;
            UpdateEstablishmentStrip();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // Called only via SwipeForwarder, which sits on the EstablishmentStrip bar —
        // never directly by the event system, since VillageView itself no longer
        // implements the drag interfaces. Nothing vertical competes for a drag that
        // starts on the strip, so unlike DungeonView's equivalent there's no axis lock
        // to do here — every drag from here is treated as a page gesture directly.
        public void HandleStripBeginDrag(PointerEventData eventData)
        {
            _dragStart = eventData.position;
            _isSnapping = false;
        }

        public void HandleStripDrag(PointerEventData eventData)
        {
            var delta = eventData.position - _dragStart;
            var pos = _content.anchoredPosition;
            pos.x = -_currentPage * _pageWidth + delta.x;
            _content.anchoredPosition = pos;
        }

        public void HandleStripEndDrag(PointerEventData eventData)
        {
            var delta = eventData.position - _dragStart;
            var desired = Mathf.Abs(delta.x) >= HorizontalSwipeThreshold
                ? _currentPage + (delta.x < 0 ? 1 : -1)
                : _currentPage;

            SnapTo(Mathf.Clamp(desired, 0, _pageCount - 1));
        }

        // The only thing in this screen that actually implements the drag interfaces —
        // sits on the EstablishmentStrip GameObject only, so a swipe is recognized
        // exactly there and nowhere else, leaving every establishment's own content free
        // to hold a ScrollRect without a conflicting ancestor drag handler. Just forwards
        // to VillageView, which still owns all the real paging state/logic.
        private class SwipeForwarder : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public VillageView Target;
            public void OnBeginDrag(PointerEventData eventData) => Target.HandleStripBeginDrag(eventData);
            public void OnDrag(PointerEventData eventData) => Target.HandleStripDrag(eventData);
            public void OnEndDrag(PointerEventData eventData) => Target.HandleStripEndDrag(eventData);
        }

        private void SnapTo(int page)
        {
            _currentPage = page;
            _targetX = -page * _pageWidth;
            _isSnapping = true;
            UpdateEstablishmentStrip();
        }

        private void UpdateEstablishmentStrip()
        {
            _prevLabel.text = _currentPage > 0 ? $"◂ {Establishments[_currentPage - 1].Label}" : "";
            _currentLabel.text = Establishments[_currentPage].Label;
            _nextLabel.text = _currentPage < Establishments.Length - 1 ? $"{Establishments[_currentPage + 1].Label} ▸" : "";
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
            layoutElement.preferredHeight = fontSize + 20;

            return text;
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
