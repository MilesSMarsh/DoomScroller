using System;
using System.Collections;
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
        private FeedCardView _activeCard;

        private RectTransform _content;
        private FeedPager _pager;
        private float _pageHeight;

        private RectTransform _toastRect;
        private Text _toastText;
        private Coroutine _toastCoroutine;

        // Absolute, ever-increasing scroll-order indices. Only [_windowStartIndex, _windowEndIndex)
        // currently have live cards attached to Content.
        private int _windowStartIndex;
        private int _windowEndIndex;

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
        private void LoadPool()
        {
            _pool = Resources.LoadAll<EncounterData>(PoolResourcesPath);
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

            CreateToast(rect);

            if (_pool.Length > 0)
            {
                EnsureFilledThrough(AheadBuffer);
            }

            // Content's height is only computed lazily by VerticalLayoutGroup/ContentSizeFitter;
            // force it to resolve now so ScrollRect sees the true scrollable range from frame one.
            Canvas.ForceUpdateCanvases();

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

            // The energy gate: out of health blocks entering anything new until enough
            // real time has passed to regenerate. Regenerate() first so a tap right at
            // the regen boundary isn't blocked on a stale reading.
            var health = profileController.Health;
            health.Regenerate();
            if (health.IsDepleted)
            {
                ShowOutOfEnergyToast();
                return;
            }

            _activeCard = card;
            EncounterSelected?.Invoke(card.Data);
        }

        private void ShowOutOfEnergyToast()
        {
            var seconds = Mathf.CeilToInt(profileController.Health.SecondsUntilNextTick());
            _toastText.text = $"Out of energy — regenerates in {seconds / 60}:{seconds % 60:D2}";
            _toastRect.gameObject.SetActive(true);

            if (_toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
            }

            _toastCoroutine = StartCoroutine(HideToastAfterDelay());
        }

        private IEnumerator HideToastAfterDelay()
        {
            yield return new WaitForSeconds(2.5f);
            _toastRect.gameObject.SetActive(false);
            _toastCoroutine = null;
        }

        private void CreateToast(RectTransform parent)
        {
            var go = new GameObject("Toast", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0, 180);
            rect.sizeDelta = new Vector2(640, 110);
            _toastRect = rect;

            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.85f);

            var textRect = CreateStretchChild(rect, "Text");
            textRect.offsetMin = new Vector2(16, 8);
            textRect.offsetMax = new Vector2(-16, -8);

            _toastText = textRect.gameObject.AddComponent<Text>();
            _toastText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _toastText.fontSize = 32;
            _toastText.fontStyle = FontStyle.Bold;
            _toastText.alignment = TextAnchor.MiddleCenter;
            _toastText.color = Color.white;
            _toastText.horizontalOverflow = HorizontalWrapMode.Wrap;

            go.SetActive(false);
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

        // Invoked by FeedPager with the drag's raw destination page (local to the
        // currently attached window) before it commits to a snap target. Grows the
        // window ahead and trims it behind, then returns the local page to actually
        // snap to (unchanged unless trimming just re-based the window's coordinates).
        private int OnPageRequested(int desiredLocalPage)
        {
            if (_pool.Length == 0)
            {
                return 0;
            }

            var desiredAbsolute = _windowStartIndex + desiredLocalPage;

            EnsureFilledThrough(desiredAbsolute + AheadBuffer);
            TrimBehind(desiredAbsolute);

            Canvas.ForceUpdateCanvases();
            _pager.Configure(_pageHeight, _windowEndIndex - _windowStartIndex);

            return desiredAbsolute - _windowStartIndex;
        }

        private void EnsureFilledThrough(int absoluteIndexInclusive)
        {
            while (_windowEndIndex <= absoluteIndexInclusive)
            {
                var encounter = DrawNext();
                var winChance = CombatResolver.PredictWinChance(profileController.Loadout, encounter);
                var closeCallChance = CombatResolver.PredictCloseCallChance(profileController.Loadout, encounter);
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
                var winChance = CombatResolver.PredictWinChance(profileController.Loadout, card.Data);
                var closeCallChance = CombatResolver.PredictCloseCallChance(profileController.Loadout, card.Data);
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
