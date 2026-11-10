using System;
using System.Collections;
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
    // Reveals a fight as an animated beat-by-beat sequence (CombatPlaybackView), then a
    // clear win/lose outcome. The fight is its own ephemeral CombatSimulator instance —
    // it never touches the persistent PlayerHealth; only a defeat (including leaving
    // early) costs anything, via PlayerHealth.ApplyDefeatPenalty.
    //
    // A win pages sideways into a second page (mirroring DungeonView's horizontal
    // swipe-to-page mechanic) holding a RewardChestView — the footer Leave button is
    // hidden the instant a fight is won, so opening that chest is the only way back to
    // the feed. A loss (or leaving mid-fight) has nothing to claim, so the footer button
    // keeps working immediately, exactly as before, and page 2 is unreachable.
    public class CombatResolutionView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float SnapSpeed = 12f;
        private const float SnapThreshold = 0.5f;
        private const float HorizontalSwipeThreshold = 80f;
        private const float AxisLockThreshold = 12f;

        public event Action ReturnToFeedRequested;
        public event Action<EncounterData> EncounterWon;
        public event Action<EncounterData> EncounterLost;

        private CombatPlaybackView _playback;
        private Text _outcomeText;
        private Text _rewardText;
        private Text _hintText;
        private RewardChestView _rewardChest;
        private GameObject _leaveButtonGo;
        private Coroutine _revealCoroutine;
        private EncounterData _data;
        private PlayerHealth _health;

        private RectTransform _viewport;
        private RectTransform _content;
        private LayoutElement _fightPageLayout;
        private LayoutElement _rewardPageLayout;
        private float _pageWidth;
        private int _currentPage;
        // 0 normally; pinned to 1 for a Treasure card (see Show()) so there's nothing
        // to swipe back into — the chest is page 1 for those, full stop.
        private int _minPage;
        private bool _canReachRewardPage;

        private Vector2 _dragStart;
        private bool? _dragIsHorizontal;
        private bool _isSnapping;
        private float _targetX;

        public static CombatResolutionView Create(Transform parent)
        {
            var go = new GameObject("CombatResolution", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = go.AddComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.06f, 1f);

            var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
            // No horizontal padding here — that belongs on ContentArea below, so the
            // Leave button (a direct sibling of it) still spans full width flush to the
            // edges, matching every other button in this project.
            layoutGroup.padding = new RectOffset(0, 0, NavBarController.Height, 0);
            layoutGroup.spacing = 0;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;

            var view = go.AddComponent<CombatResolutionView>();

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

            // Page 0 — outcome/reward-on-loss text sit at the top so they stay visible
            // above the fight itself; a flexible spacer on each side of
            // CombatPlaybackView centers it in the remaining space instead of it
            // sitting right under the text or hugging the very bottom of the screen.
            var contentAreaGo = new GameObject("FightPage", typeof(RectTransform));
            contentAreaGo.transform.SetParent(contentRect, false);
            var contentAreaPageLayout = contentAreaGo.AddComponent<LayoutElement>();
            contentAreaPageLayout.flexibleWidth = 0;
            contentAreaPageLayout.flexibleHeight = 1;
            view._fightPageLayout = contentAreaPageLayout;

            var contentAreaGroup = contentAreaGo.AddComponent<VerticalLayoutGroup>();
            contentAreaGroup.padding = new RectOffset(48, 48, 0, 24);
            contentAreaGroup.spacing = 0;
            contentAreaGroup.childAlignment = TextAnchor.UpperCenter;
            contentAreaGroup.childForceExpandWidth = true;
            contentAreaGroup.childForceExpandHeight = false;
            contentAreaGroup.childControlWidth = true;
            contentAreaGroup.childControlHeight = true;

            view._outcomeText = CreateLabel(contentAreaGo.transform, "Outcome", 64, FontStyle.Bold, 90);
            // Tall enough for a 2-line defeat string (message + HP note).
            view._rewardText = CreateLabel(contentAreaGo.transform, "Reward", 34, FontStyle.Italic, 150);
            view._hintText = CreateLabel(contentAreaGo.transform, "SwipeHint", 30, FontStyle.Bold, 60);
            view._hintText.color = new Color(0.4f, 0.85f, 1f, 0.95f);
            view._hintText.text = "◂ Swipe left for your reward";
            view._hintText.gameObject.SetActive(false);

            CreateSpacer(contentAreaGo.transform);

            // Only shown for actual fights (Enemy type) — Treasure never fights, per
            // CombatResolver.CreateSimulator returning null for it.
            view._playback = CombatPlaybackView.Create(contentAreaGo.transform, rect);

            CreateSpacer(contentAreaGo.transform);

            // Page 1 — the reward chest. Owns its own Return-to-Feed button (see
            // RewardChestView) since the footer one below is hidden on a win.
            var rewardPageGo = new GameObject("RewardPage", typeof(RectTransform));
            rewardPageGo.transform.SetParent(contentRect, false);
            var rewardPageLayout = rewardPageGo.AddComponent<LayoutElement>();
            rewardPageLayout.flexibleWidth = 0;
            rewardPageLayout.flexibleHeight = 1;
            view._rewardPageLayout = rewardPageLayout;

            view._rewardChest = RewardChestView.Create(rewardPageGo.transform);
            view._rewardChest.ReturnRequested += () => view.ReturnToFeedRequested?.Invoke();

            view._leaveButtonGo = CreateButton(rect, "LeaveButton", "Leave", () => view.OnLeaveClicked());

            go.SetActive(false);
            return view;
        }

        public void Show(EncounterData data, PlayerLoadout loadout, PlayerProgression progression, PlayerHealth health,
            PlayerAttributes attributes, GameSettings settings, PlayerWallet wallet)
        {
            gameObject.SetActive(true);
            _data = data;
            _health = health;

            // Ambush is combat-wise identical to Enemy (see EncounterType.Ambush) — both
            // get the full animated beat-by-beat playback, only Treasure/other non-combat
            // types reveal straight to an outcome with no fight to show.
            var isFight = data.type == EncounterType.Enemy || data.type == EncounterType.Ambush;
            _playback.gameObject.SetActive(isFight);

            // Always enter at full of whatever CurrentMaxHealth currently is — the
            // fight is a separate instance from here on; PlayerHealth isn't touched
            // again until it's over (see RevealSequence/OnLeaveClicked).
            var sim = CombatResolver.CreateSimulator(loadout, attributes, settings, data, health.CurrentMaxHealth);

            _outcomeText.text = "";
            _rewardText.text = "";
            _hintText.gameObject.SetActive(false);
            // No voluntary way out of an ambush — it has to actually resolve to a win or
            // a loss, not just an early self-inflicted one (see OnLeaveClicked).
            _leaveButtonGo.SetActive(data.type != EncounterType.Ambush);

            // Reset the pager to the fight page and re-lock the reward page — a fresh
            // Show() reuses this same instance (see EncounterFlowController.Awake). A
            // Treasure card has no fight worth swiping to/from at all, so it starts
            // pinned straight on the reward page instead — see _minPage below.
            var isTreasure = data.type == EncounterType.Treasure;
            _canReachRewardPage = false;
            _minPage = isTreasure ? 1 : 0;
            _currentPage = isTreasure ? 1 : 0;
            _isSnapping = false;
            Canvas.ForceUpdateCanvases();
            _pageWidth = _viewport.rect.width;
            _fightPageLayout.minWidth = _pageWidth;
            _fightPageLayout.preferredWidth = _pageWidth;
            _rewardPageLayout.minWidth = _pageWidth;
            _rewardPageLayout.preferredWidth = _pageWidth;
            Canvas.ForceUpdateCanvases();
            _content.anchoredPosition = new Vector2(isTreasure ? -_pageWidth : 0f, 0f);

            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
            }

            _revealCoroutine = StartCoroutine(RevealSequence(sim, data.baseXpReward, loadout, progression, settings, wallet));
        }

        public void Hide()
        {
            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
                _revealCoroutine = null;
            }

            gameObject.SetActive(false);
        }

        // Leaving before the fight has finished counts as a loss — same max-HP penalty
        // as actually losing, not a free way to walk away from a bad matchup mid-fight.
        // _revealCoroutine is non-null for exactly that window (set in Show, cleared the
        // instant RevealSequence reaches its own Won/Lost below), so its presence alone
        // tells us whether this is an early leave. Once a fight is won this button is
        // hidden (see RevealSequence), so it's otherwise only ever reachable pre-resolution
        // or after a loss — both of which return immediately, nothing to claim.
        private void OnLeaveClicked()
        {
            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
                _revealCoroutine = null;
                _health.ApplyDefeatPenalty();
                EncounterLost?.Invoke(_data);
            }

            ReturnToFeedRequested?.Invoke();
        }

        private IEnumerator RevealSequence(CombatSimulator sim, int xpReward, PlayerLoadout loadout,
            PlayerProgression progression, GameSettings settings, PlayerWallet wallet)
        {
            yield return StartCoroutine(_playback.Play(sim, loadout, settings, _data.foregroundArt, _data.cardColor));

            var isVictory = _playback.LastOutcomeWasVictory;
            _outcomeText.text = isVictory ? "VICTORY" : "DEFEAT";
            _outcomeText.color = isVictory ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);

            if (isVictory)
            {
                var previousLevel = progression.Level;
                progression.AddXp(xpReward);

                // Items now come from the encounter's own authored loot table (all-zero
                // on anything created before loot tables existed, which RollLootTable
                // treats as "exactly one, always Plain" — the exact behavior this
                // replaced).
                var items = RewardGenerator.RollLootTable(_data.lootTable);
                foreach (var rolledItem in items)
                {
                    loadout.AddToInventory(rolledItem);
                }

                // Not granted here — RewardChestView.Show only stores it, and actually
                // adds it to the wallet once the chest is opened.
                var goldReward = RewardGenerator.ResolveGoldReward(_data.goldReward, xpReward);

                // A Treasure card's own chest art; a Combat win keeps the generic chest —
                // _data.foregroundArt there is the creature portrait, not a chest.
                var chestSprite = _data.type == EncounterType.Treasure ? _data.foregroundArt : null;
                _rewardChest.Show(xpReward, goldReward, items, progression.Level > previousLevel, progression.Level,
                    _playback.WasCloseCall, wallet, chestSprite);
                // Already sitting on the reward page for a Treasure card (see Show()) —
                // no swipe needed, so no hint to swipe.
                _hintText.gameObject.SetActive(_data.type != EncounterType.Treasure);
                _canReachRewardPage = true;

                // Opening the chest is now the only way back — see the class comment.
                _leaveButtonGo.SetActive(false);

                EncounterWon?.Invoke(_data);
            }
            else
            {
                _health.ApplyDefeatPenalty();
                _rewardText.text = $"No reward this time — your gear held you back. Your max HP drops to " +
                    $"{_health.CurrentMaxHealth}/{_health.TrueMaxHealth}, regenerating over time. " +
                    "This one's lost for good; gear up and look for the next one.";

                // An Ambush hides this button for the whole fight (see Show()) to block
                // an early voluntary loss — but the fight has now actually resolved, so
                // there needs to be a way back to the feed. Without this, a lost Ambush
                // was a soft-lock: no Leave button, and no reward chest to open either.
                _leaveButtonGo.SetActive(true);

                EncounterLost?.Invoke(_data);
            }

            _revealCoroutine = null;
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

            // Mathf.Max, not a bare 0/1: for a Treasure card _minPage is already 1, and
            // Mathf.Clamp with min > max would silently return min instead of clamping
            // sanely if this ever raced _canReachRewardPage still being false.
            var maxPage = Mathf.Max(_minPage, _canReachRewardPage ? 1 : 0);
            SnapTo(Mathf.Clamp(desired, _minPage, maxPage));
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

        private static void CreateSpacer(Transform parent)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.flexibleHeight = 1;
            layoutElement.minHeight = 0;
            layoutElement.preferredHeight = 0;
        }

        private static Text CreateLabel(Transform parent, string name, int fontSize, FontStyle style, float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;

            return text;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, Action onClick)
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

            return go;
        }
    }
}
