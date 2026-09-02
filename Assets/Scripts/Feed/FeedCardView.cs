using System;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoomScroller.Feed
{
    // Greybox feed card, built entirely at runtime from EncounterData — no prefab asset yet.
    // Sized to exactly one page's height by FeedController, so each encounter fills the screen.
    // Renders EncounterData.artwork full-bleed behind a bottom caption strip (falls back to
    // a flat EncounterData.cardColor when no artwork is assigned).
    public class FeedCardView : MonoBehaviour, IPointerClickHandler
    {
        // Every hand-authored encounter starts at this tier; harder variants are generated
        // at entry time (Phase 3 sideways swipe), not authored per-asset.
        private const string EntryDifficultyLabel = "Standard";

        // A black strip along each card's own bottom edge, so consecutive cards read as
        // visually separated while scrolling. Drawn inside the card's own allotted page
        // height (not as extra spacing between cards) so it doesn't touch the scroll/snap
        // math in FeedController/FeedPager, which assumes each page is exactly one
        // viewport height apart. Bottom (not top of the next card) so the page's own snap
        // target sits at this buffer's bottom edge — any snap rounding slop on a given
        // aspect ratio only ever reveals more black buffer, never the next card's actual
        // artwork, and stays generously sized (not just cosmetic-thin) for that margin.
        private const float BufferHeight = 48f;

        private Image _background;
        private Text _titleText;
        private Text _badgeText;
        private Text _rewardText;
        private Image _chanceBadge;
        private Text _chanceText;
        private Text _riskText;
        private EncounterData _data;
        private Action<FeedCardView> _onTapped;
        private bool _locked;
        private float _winChance;
        private float _closeCallChance;

        public EncounterData Data => _data;

        // "Locked" covers both outcomes: a win completes the encounter, a loss locks it
        // out too (no infinite retries against the same card) — see SetLocked.
        public bool IsLocked => _locked;

        // Tapped callback receives this card (not just its data): the same EncounterData
        // asset can appear at multiple scroll positions since it's drawn from a reusable
        // hand-authored pool, so only the specific card instance unambiguously identifies
        // "the one the player entered."
        public static FeedCardView Create(Transform parent, EncounterData data, float height, float winChance,
            float closeCallChance, Action<FeedCardView> onTapped = null)
        {
            var go = new GameObject($"Card_{data.title}", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1f);

            var outerLayout = go.AddComponent<LayoutElement>();
            outerLayout.preferredHeight = height;
            outerLayout.minHeight = height;

            var background = go.AddComponent<Image>();

            // Sits behind the caption text, above the artwork, so captions stay legible
            // over busy/bright art without needing per-image color tuning.
            var scrimRect = CreateStretchChild(rect, "Scrim", new Vector2(0, 0), new Vector2(1, 0.4f));
            var scrim = scrimRect.gameObject.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.55f);
            scrimRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
            // Bottom padding clears the BottomBuffer strip (drawn on top, at the very
            // bottom edge) plus the original 20px of breathing room, so the caption text
            // doesn't render partly hidden behind it.
            layoutGroup.padding = new RectOffset(24, 24, 20, 20 + (int)BufferHeight);
            layoutGroup.spacing = 8;
            // Caption sits at the bottom of the card so the artwork reads clearly above it.
            layoutGroup.childAlignment = TextAnchor.LowerLeft;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            // Must be true: with this false, caption rows all fall back to Unity's
            // untouched 100px default instead of their declared height — the caption
            // block still happened to look fine (all three rows shared the same stale
            // default, so gaps stayed consistent by coincidence), but it's not a real
            // guarantee for longer wrapped text. Same fix as every other view this pass.
            layoutGroup.childControlHeight = true;

            var view = go.AddComponent<FeedCardView>();
            view._background = background;
            view._titleText = CreateLabel(rect, "Title", 44, FontStyle.Bold);
            view._badgeText = CreateLabel(rect, "Badge", 30, FontStyle.Italic);
            view._rewardText = CreateLabel(rect, "Reward", 34, FontStyle.Normal);
            view._onTapped = onTapped;

            // Top-right corner, over the artwork (not the bottom caption strip): predicted
            // odds for the entry-level variant. Two numbers stacked — overall success chance
            // (color-graded red (0%) -> green (100%) on the badge itself) on top, and how
            // much of that is a "close call" that still costs health underneath.
            var badgeGo = new GameObject("ChanceBadge", typeof(RectTransform));
            var badgeRect = (RectTransform)badgeGo.transform;
            badgeRect.SetParent(rect, false);
            badgeRect.anchorMin = new Vector2(1, 1);
            badgeRect.anchorMax = new Vector2(1, 1);
            badgeRect.pivot = new Vector2(1, 1);
            // Cards render full-screen behind NavBar (it's a persistent, always-on-top
            // overlay — see NavBarController), so the badge needs to clear its height,
            // not just sit near the card's own top edge, or it renders hidden underneath.
            badgeRect.anchoredPosition = new Vector2(-16, -16 - NavBarController.Height);
            badgeRect.sizeDelta = new Vector2(180, 108);
            badgeGo.AddComponent<LayoutElement>().ignoreLayout = true;

            view._chanceBadge = badgeGo.AddComponent<Image>();

            var chanceTextRect = CreateStretchChild(badgeRect, "Text", new Vector2(0, 0.45f), Vector2.one);
            view._chanceText = chanceTextRect.gameObject.AddComponent<Text>();
            view._chanceText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view._chanceText.fontSize = 40;
            view._chanceText.fontStyle = FontStyle.Bold;
            view._chanceText.alignment = TextAnchor.MiddleCenter;
            view._chanceText.color = Color.white;

            var riskTextRect = CreateStretchChild(badgeRect, "RiskText", Vector2.zero, new Vector2(1, 0.45f));
            view._riskText = riskTextRect.gameObject.AddComponent<Text>();
            view._riskText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view._riskText.fontSize = 28;
            view._riskText.fontStyle = FontStyle.Bold;
            view._riskText.alignment = TextAnchor.MiddleCenter;
            view._riskText.color = new Color(1f, 0.85f, 0.4f);

            // Added last so it renders on top of the artwork/scrim/badge beneath it.
            var bufferRect = CreateStretchChild(rect, "BottomBuffer", Vector2.zero, new Vector2(1, 0));
            bufferRect.pivot = new Vector2(0.5f, 0f);
            bufferRect.sizeDelta = new Vector2(0, BufferHeight);
            bufferRect.gameObject.AddComponent<Image>().color = Color.black;
            bufferRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            view.Bind(data);
            view.SetOdds(winChance, closeCallChance);
            return view;
        }

        public void Bind(EncounterData data)
        {
            _data = data;
            _locked = false;

            _background.sprite = data.artwork;
            _background.color = data.artwork != null ? Color.white : data.cardColor;

            _titleText.text = data.title;
            _badgeText.text = BaseBadgeText(data);
            _rewardText.text = data.rewardSummary;
        }

        // Dungeon cards have no single upfront difficulty (they're a multi-step run —
        // see DungeonView) so they read as "Dungeon Door" instead of "{type} · Standard".
        // Shared by Bind and SetLocked so the two never drift out of sync.
        private static string BaseBadgeText(EncounterData data)
        {
            return data.type == EncounterType.Dungeon ? "Dungeon Door" : $"{data.type} · {EntryDifficultyLabel}";
        }

        // Predicted odds for the entry-level variant against the player's current loadout
        // (see CombatResolver.PredictWinChance/PredictCloseCallChance). Called again by
        // FeedController whenever the loadout changes so already-visible cards don't show
        // stale odds.
        public void SetOdds(float winChance, float closeCallChance)
        {
            _winChance = Mathf.Clamp01(winChance);
            _closeCallChance = Mathf.Clamp01(closeCallChance);

            _chanceText.text = $"{Mathf.RoundToInt(_winChance * 100)}%";
            _riskText.text = _closeCallChance > 0.005f ? $"{Mathf.RoundToInt(_closeCallChance * 100)}% w/ dmg" : "no risk";
            _chanceBadge.color = ChanceGradient(_winChance) * new Color(1f, 1f, 1f, _locked ? 0.4f : 1f);
        }

        // Greys the card out and blocks re-entry. reasonLabel distinguishes a win
        // ("Completed") from a loss ("Failed") in the badge so a locked-out loss doesn't
        // read as if it were won.
        public void SetLocked(bool locked, string reasonLabel = "Completed")
        {
            _locked = locked;

            var dim = locked ? 0.4f : 1f;
            _background.color = (_data.artwork != null ? Color.white : _data.cardColor) * new Color(dim, dim, dim, 1f);

            var textAlpha = locked ? 0.35f : 1f;
            _titleText.color = new Color(1, 1, 1, textAlpha);
            _rewardText.color = new Color(1, 1, 1, textAlpha);
            _badgeText.color = new Color(1, 1, 1, textAlpha);
            _badgeText.text = locked
                ? $"{BaseBadgeText(_data)} · {reasonLabel}"
                : BaseBadgeText(_data);
            _chanceText.color = new Color(1, 1, 1, textAlpha);
            _riskText.color = new Color(1f, 0.85f, 0.4f, textAlpha);
            SetOdds(_winChance, _closeCallChance);
        }

        // Red (0%) -> yellow (50%) -> green (100%): a traffic-light gradient reads more
        // clearly at the midpoint than a straight red-to-green lerp (which passes through
        // a muddy olive around 50%).
        private static Color ChanceGradient(float t)
        {
            var red = new Color(0.75f, 0.2f, 0.18f, 0.9f);
            var yellow = new Color(0.8f, 0.68f, 0.15f, 0.9f);
            var green = new Color(0.2f, 0.68f, 0.3f, 0.9f);
            return t < 0.5f ? Color.Lerp(red, yellow, t / 0.5f) : Color.Lerp(yellow, green, (t - 0.5f) / 0.5f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_locked)
            {
                return;
            }

            _onTapped?.Invoke(this);
        }

        private static Text CreateLabel(Transform parent, string name, int fontSize, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = fontSize + 16;

            return text;
        }

        private static RectTransform CreateStretchChild(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
