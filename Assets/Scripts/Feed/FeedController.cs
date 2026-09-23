using System;
using System.Collections.Generic;
using DoomScroller.Combat;
using DoomScroller.Inventory;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Feed
{
    // Builds a vertically scrolling feed (Scroll View: Viewport/Content) at runtime and
    // populates it by drawing from the hand-authored EncounterData pool (any asset under
    // Assets/Resources/Encounters/ — see EncounterData) as the player scrolls (endless
    // feed). Only a sliding window of cards is ever kept alive: the current card, the
    // previous BehindKeep cards (scrollable back to), and a few drawn ahead so the next
    // swipe never hits an empty edge. Anything further behind is destroyed so the feed
    // doesn't grow unbounded.
    public class FeedController : MonoBehaviour
    {
        private const string PoolResourcesPath = "Encounters";
        private const int BehindKeep = 2;
        private const int AheadBuffer = 2;

        [SerializeField] private ProfileController profileController;

        public event Action<EncounterData> EncounterSelected;

        private readonly List<FeedCardView> _liveCards = new();
        private readonly List<int> _shuffleBag = new();
        private readonly System.Random _rng = new();

        private EncounterData[] _pool;
        private EncounterData _landingCard;
        private FeedCardView _activeCard;

        private RectTransform _viewport;
        private RectTransform _content;
        private FeedPager _pager;
        private float _pageHeight;

        // Absolute, ever-increasing scroll-order indices. Only [_windowStartIndex, _windowEndIndex)
        // currently have live cards attached to Content.
        private int _windowStartIndex;
        private int _windowEndIndex;

        // Which absolute index the player is currently settled on — tracked explicitly
        // (rather than derived from ScrollRect.content.anchoredPosition.y) so
        // OnPageRequested can tell whether the player is sitting on an unresolved Ambush
        // and scan the path to a requested page for one in between. Initialized in
        // BuildFeed once the initial window exists.
        private int _currentAbsoluteIndex;

        private void Start()
        {
            // Deferred to Start(): BuildFeed() needs profileController.Loadout to compute
            // each card's win-chance badge, and Awake() order between components on
            // different GameObjects isn't guaranteed (same hazard as NavBarController).
            EventSystemBootstrapper.EnsureExists();
            LoadPool();
            BuildFeed();

            profileController.Loadout.Changed += RefreshAllChances;
        }

        // Auto-discovery, not a manually-maintained list: drop a new EncounterData asset
        // into Assets/Resources/Encounters/ and it's in the rotation on next play, no
        // registration step. See the workflow notes on EncounterData for authoring one.
        // The one Landing-type asset (if any) is pulled out here rather than left in the
        // rotation — it's meant to appear exactly once, at the very start (see
        // BuildFeed), never redrawn by the shuffle bag.
        private void LoadPool()
        {
            var loaded = Resources.LoadAll<EncounterData>(PoolResourcesPath);
            _landingCard = Array.Find(loaded, e => e.type == EncounterType.Landing);
            // A Resource card with no reward thresholds authored can't grant anything —
            // excluded here rather than left to render a pointless timed window with no
            // possible payoff. Self-healing: authoring at least one threshold brings the
            // card straight back into rotation with no further code change.
            _pool = Array.FindAll(loaded, e => e.type != EncounterType.Landing
                && (e.type != EncounterType.Resource || (e.resourceThresholds != null && e.resourceThresholds.Length > 0)));
            Array.Sort(_pool, (a, b) => string.CompareOrdinal(a.title, b.title));

            if (_pool.Length == 0)
            {
                Debug.LogError($"FeedController: no EncounterData assets found under " +
                    $"Assets/Resources/{PoolResourcesPath}/. Create one via Assets > Create > " +
                    "DoomScroller > Encounter Data and save it in that folder.");
            }
            else if (_pool.Length < BehindKeep + 1 + AheadBuffer)
            {
                Debug.LogWarning($"FeedController: only {_pool.Length} encounter(s) in the pool — " +
                    $"add at least {BehindKeep + 1 + AheadBuffer} to guarantee no repeats within " +
                    "a single scroll window.");
            }
        }

        private void BuildFeed()
        {
            var rect = (RectTransform)transform;

            var scrollRect = gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;

            var viewport = CreateStretchChild(rect, "Viewport");
            _viewport = viewport;
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = CreateStretchChild(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            _content = content;

            // No spacing/padding: each card is sized to exactly one viewport height below,
            // so cards must sit flush against each other to fill the screen with no gaps.
            var layoutGroup = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 0;
            layoutGroup.padding = new RectOffset(0, 0, 0, 0);
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            // Must be true: with this false, the group sizes itself off each card's
            // *current* rect (the untouched 100x100 default) instead of the card's
            // declared LayoutElement.preferredHeight, and never assigns it either.
            layoutGroup.childControlHeight = true;

            var sizeFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = content;

            // Force an immediate layout pass: viewport.rect wouldn't otherwise be
            // guaranteed to reflect the real screen size this early (same-frame Awake).
            Canvas.ForceUpdateCanvases();
            _pageHeight = viewport.rect.height;

            _pager = gameObject.AddComponent<FeedPager>();
            _pager.SetPageRequestHandler(OnPageRequested);

            // The blank landing card, if authored, always sits at absolute index 0 —
            // created unconditionally before any shuffle-bag draws, so real content
            // still starts at index 1 exactly as it always has.
            if (_landingCard != null)
            {
                var landingCardView = FeedCardView.Create(_content, _landingCard, _pageHeight, 1f, 0f, OnCardTapped);
                _liveCards.Add(landingCardView);
                _windowEndIndex++;
            }

            if (_pool.Length > 0)
            {
                // Offset by however many cards (0 or 1) already occupy the window, so
                // this always fills exactly AheadBuffer+1 cards of real content — same
                // as before the landing card existed, just shifted to start at index 1
                // instead of 0 when one's present.
                EnsureFilledThrough(_windowEndIndex + AheadBuffer);
            }

            // Content starts scrolled to local page 0, which is always _windowStartIndex
            // in absolute terms.
            _currentAbsoluteIndex = _windowStartIndex;

            // Content's height is only computed lazily by VerticalLayoutGroup/ContentSizeFitter;
            // force it to resolve now so ScrollRect sees the true scrollable range from frame one.
            Canvas.ForceUpdateCanvases();

            _pager.Configure(_pageHeight, Mathf.Max(_windowEndIndex - _windowStartIndex, 1));
        }

        // Recomputes every card's height whenever the actual on-screen viewport size
        // changes — switching device presets in the Editor Simulator without restarting
        // Play Mode, an aspect ratio the game wasn't tested at, or a real device
        // rotation. _pageHeight was otherwise only ever measured once in Start(), so on
        // a viewport taller than whatever was active then, every card stayed sized to
        // the smaller stale height and the next card peeked out from under the buffer at
        // the bottom of the screen — this keeps "one page" matching the real screen
        // instead of a gap sized to whichever device the feed happened to build under.
        private void OnRectTransformDimensionsChange()
        {
            // Fires for this GameObject's own rect only, but guard anyway: Awake/Start
            // ordering means this can fire before BuildFeed has finished (or ever run).
            if (_pager == null || _viewport == null)
            {
                return;
            }

            var newHeight = _viewport.rect.height;
            if (newHeight <= 0f || Mathf.Approximately(newHeight, _pageHeight))
            {
                return;
            }

            // Content's anchoredPosition.y increases as you swipe up toward later pages
            // (see FeedPager) — recover which page that corresponds to before rescaling,
            // then re-place content at the same logical page under the new height so the
            // resize doesn't yank the player mid-page.
            var currentPage = _pageHeight > 0f ? Mathf.RoundToInt(_content.anchoredPosition.y / _pageHeight) : 0;
            _pageHeight = newHeight;

            foreach (var card in _liveCards)
            {
                var layoutElement = card.GetComponent<LayoutElement>();
                layoutElement.preferredHeight = _pageHeight;
                layoutElement.minHeight = _pageHeight;
            }

            Canvas.ForceUpdateCanvases();
            _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, currentPage * _pageHeight);
            _pager.Configure(_pageHeight, Mathf.Max(_windowEndIndex - _windowStartIndex, 1));
        }

        private void OnCardTapped(FeedCardView card)
        {
            // Cards ignore their own taps once locked, but guard here too since this
            // handler is the single source of truth for entering an encounter.
            if (card.IsLocked)
            {
                return;
            }

            // The blank landing card is genuinely inert — scrolling past it is the only
            // way by design (see BuildFeed/LoadPool).
            if (card.Data.type == EncounterType.Landing)
            {
                return;
            }

            // No energy gate any more — you can always enter a fight, however low your
            // current max HP has dropped (see PlayerHealth). A weakened ceiling is
            // already the consequence.
            _activeCard = card;
            EncounterSelected?.Invoke(card.Data);
        }

        // Locks the currently-entered encounter's card so it greys out and can no longer
        // be entered — on a win ("Completed") or a loss ("Failed"); either way, that card
        // is done. Called by EncounterFlowController on combat resolution (Phase 6 loop).
        // Tracked by card instance, not by EncounterData: the same hand-authored asset can
        // legitimately appear at more than one scroll position over time, so the asset
        // alone can't identify "the specific card the player just played."
        public void MarkWon(EncounterData data)
        {
            _activeCard?.SetLocked(true, "Completed");
        }

        public void MarkLost(EncounterData data)
        {
            _activeCard?.SetLocked(true, "Failed");
        }

        // A dungeon left voluntarily before its treasure room (see DungeonView) still
        // locks the card — otherwise the same run could be re-entered for free, easy
        // rewards from whatever depth was already cleared.
        public void MarkAbandoned(EncounterData data)
        {
            _activeCard?.SetLocked(true, "Abandoned");
        }

        // Invoked by FeedPager with the drag's raw destination page (local to the
        // currently attached window) before it commits to a snap target — whatever this
        // returns is what the pager actually snaps to (see FeedPager.OnEndDrag), which
        // is also how an unresolved Ambush refuses to let the player past it (see
        // ResolveAmbushBlockedDestination). Grows the window ahead and trims it behind
        // for wherever we're actually ending up, then returns the local page to snap to.
        private int OnPageRequested(int desiredLocalPage)
        {
            if (_pool.Length == 0)
            {
                return 0;
            }

            var desiredAbsolute = _windowStartIndex + desiredLocalPage;

            // Cards between here and there must exist before scanning the path for an
            // ambush — backing up only ever revisits already-live cards, so this only
            // actually grows anything when advancing.
            EnsureFilledThrough(Mathf.Max(desiredAbsolute, _currentAbsoluteIndex) + AheadBuffer);

            var finalAbsolute = ResolveAmbushBlockedDestination(desiredAbsolute);

            TrimBehind(finalAbsolute);

            Canvas.ForceUpdateCanvases();
            _pager.Configure(_pageHeight, _windowEndIndex - _windowStartIndex);

            _currentAbsoluteIndex = finalAbsolute;
            return finalAbsolute - _windowStartIndex;
        }

        // An unresolved Ambush (EncounterType.Ambush, not yet locked by MarkWon/MarkLost)
        // refuses to let the player scroll away in either direction: sitting on one
        // blocks any move at all, and scrolling toward one stops exactly there instead of
        // skipping past it (a multi-page flick can't jump clean over one sitting between
        // the current and requested page).
        private int ResolveAmbushBlockedDestination(int desiredAbsolute)
        {
            if (IsUnresolvedAmbush(_currentAbsoluteIndex))
            {
                return _currentAbsoluteIndex;
            }

            var step = desiredAbsolute.CompareTo(_currentAbsoluteIndex);
            for (var i = _currentAbsoluteIndex + step; step != 0 && i != desiredAbsolute + step; i += step)
            {
                if (IsUnresolvedAmbush(i))
                {
                    return i;
                }
            }

            return desiredAbsolute;
        }

        private bool IsUnresolvedAmbush(int absoluteIndex)
        {
            var localIndex = absoluteIndex - _windowStartIndex;
            if (localIndex < 0 || localIndex >= _liveCards.Count)
            {
                return false;
            }

            var card = _liveCards[localIndex];
            return card.Data.type == EncounterType.Ambush && !card.IsLocked;
        }

        private void EnsureFilledThrough(int absoluteIndexInclusive)
        {
            while (_windowEndIndex <= absoluteIndexInclusive)
            {
                var encounter = DrawNext();
                if (encounter.type == EncounterType.WanderingMerchant)
                {
                    // A fresh runtime instance per sighting — WanderingMerchantView keys
                    // its generated stock by this reference, so two different sightings
                    // (even clones of the same static template asset, redrawn later from
                    // the shuffle bag) never accidentally share a "same merchant" stock
                    // cache. This card's Data reference never changes for its lifetime
                    // (see TrimBehind), so leaving and returning to THIS sighting still
                    // round-trips the same instance and its stock survives.
                    encounter = Instantiate(encounter);
                }

                var winChance = CombatResolver.PredictWinChance(profileController.Loadout, profileController.Attributes,
                    profileController.Health, encounter);
                var closeCallChance = CombatResolver.PredictCloseCallChance(profileController.Loadout,
                    profileController.Attributes, profileController.Health, encounter);
                var card = FeedCardView.Create(_content, encounter, _pageHeight, winChance, closeCallChance, OnCardTapped);
                _liveCards.Add(card);
                _windowEndIndex++;
            }
        }

        // Re-predicts every currently-visible card's odds so equip/unequip changes made in
        // Profile (between plays) don't leave stale percentages on cards already in the window.
        private void RefreshAllChances()
        {
            foreach (var card in _liveCards)
            {
                var winChance = CombatResolver.PredictWinChance(profileController.Loadout, profileController.Attributes,
                    profileController.Health, card.Data);
                var closeCallChance = CombatResolver.PredictCloseCallChance(profileController.Loadout,
                    profileController.Attributes, profileController.Health, card.Data);
                card.SetOdds(winChance, closeCallChance);
            }
        }

        // Shuffle-bag draw: every encounter in the pool is served exactly once per full
        // pass before any repeats, so as long as the pool has at least BehindKeep+1+AheadBuffer
        // entries, no two cards in the live window are ever the same encounter.
        private EncounterData DrawNext()
        {
            if (_shuffleBag.Count == 0)
            {
                for (var i = 0; i < _pool.Length; i++)
                {
                    _shuffleBag.Add(i);
                }

                for (var i = _shuffleBag.Count - 1; i > 0; i--)
                {
                    var j = _rng.Next(i + 1);
                    (_shuffleBag[i], _shuffleBag[j]) = (_shuffleBag[j], _shuffleBag[i]);
                }
            }

            var index = _shuffleBag[^1];
            _shuffleBag.RemoveAt(_shuffleBag.Count - 1);
            return _pool[index];
        }

        private void TrimBehind(int currentAbsoluteIndex)
        {
            while (currentAbsoluteIndex - _windowStartIndex > BehindKeep)
            {
                var oldest = _liveCards[0];
                _liveCards.RemoveAt(0);

                // Unparent immediately (rather than relying on deferred Destroy) so the
                // layout group's next reflow no longer counts this card.
                oldest.transform.SetParent(null);
                Destroy(oldest.gameObject);
                _windowStartIndex++;

                // Removing a card from the front shifts every remaining child up by one
                // page height in Content's local space; compensate so nothing visually jumps.
                var pos = _content.anchoredPosition;
                pos.y -= _pageHeight;
                _content.anchoredPosition = pos;
            }
        }

        private static RectTransform CreateStretchChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
