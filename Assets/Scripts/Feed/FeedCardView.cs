using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoomScroller.Feed
{
    // Greybox feed card, built entirely at runtime from EncounterData — no prefab asset yet.
    // Sized to exactly one page's height by FeedController, so each encounter fills the screen.
    // Renders EncounterData.artwork full-bleed behind a bottom caption strip (falls back to
    // a flat EncounterData.cardColor when no artwork is assigned). The whole card (including
    // the action banner/odds strip directly above the caption) is the tap target — only the
    // caption band itself (the text/description) is a safe, non-entering read-only zone
    // (see CaptionArea below).
    public class FeedCardView : MonoBehaviour, IPointerClickHandler
    {
        // Every hand-authored encounter starts at this tier; harder variants are generated
        // at entry time (Phase 3 sideways swipe), not authored per-asset.
        private const string EntryDifficultyLabel = "Standard";

        // How much of the card's own height the caption/scrim region covers, measured up
        // from the bottom — the caption text top-aligns within this band (grows downward
        // from where the scrim starts) rather than bottom-aligning within the whole card.
        private const float CaptionAreaFraction = 0.4f;

        // Height of the caption's top row (action banner + win% badge).
        private const float TopRowHeight = 76f;

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
        private Text _rewardDetailText;
        private Image _chanceBadge;
        private Text _chanceText;
        private Text _riskText;
        private Image _actionBannerImage;
        private Text _actionBannerText;
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
            // over busy/bright art without needing per-image color tuning. Exactly
            // matches CaptionArea's own bounds below.
            var scrimRect = CreateStretchChild(rect, "Scrim", new Vector2(0, 0), new Vector2(1, CaptionAreaFraction));
            var scrim = scrimRect.gameObject.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.55f);
            scrimRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var view = go.AddComponent<FeedCardView>();
            view._background = background;
            view._onTapped = onTapped;

            // A self-contained caption region matching the scrim's bounds, with its own
            // top-aligned layout group — text starts at the top of the translucent band
            // and grows downward, rather than bottom-anchoring within the whole card.
            //
            // It's also a safe read-only zone: a transparent, full-coverage Image plus a
            // no-op click absorber below means a tap anywhere in this band (not just
            // directly on a glyph — including the gaps between lines) never reaches the
            // card's own OnPointerClick, so players can read the description without
            // risking committing into an encounter they're not ready for. Only the
            // artwork above this band still enters.
            var captionAreaGo = new GameObject("CaptionArea", typeof(RectTransform));
            var captionAreaRect = (RectTransform)captionAreaGo.transform;
            captionAreaRect.SetParent(rect, false);
            captionAreaRect.anchorMin = new Vector2(0, 0);
            captionAreaRect.anchorMax = new Vector2(1, CaptionAreaFraction);
            captionAreaRect.offsetMin = Vector2.zero;
            captionAreaRect.offsetMax = Vector2.zero;

            var captionAreaBlocker = captionAreaGo.AddComponent<Image>();
            captionAreaBlocker.color = new Color(0f, 0f, 0f, 0f);
            captionAreaGo.AddComponent<ClickAbsorber>();

            var captionLayout = captionAreaGo.AddComponent<VerticalLayoutGroup>();
            // Bottom padding clears the BottomBuffer strip (drawn on top, at the very
            // bottom edge), so the caption text doesn't render partly hidden behind it.
            captionLayout.padding = new RectOffset(24, 24, 16, 16 + (int)BufferHeight);
            captionLayout.spacing = 10;
            captionLayout.childAlignment = TextAnchor.UpperLeft;
            captionLayout.childForceExpandWidth = true;
            captionLayout.childForceExpandHeight = false;
            captionLayout.childControlWidth = true;
            // Must be true: with this false, caption rows all fall back to Unity's
            // untouched 100px default instead of their declared height — the caption
            // block still happened to look fine (all three rows shared the same stale
            // default, so gaps stayed consistent by coincidence), but it's not a real
            // guarantee for longer wrapped text. Same fix as every other view this pass.
            captionLayout.childControlHeight = true;

            // Full-width strip directly above the caption band — deliberately OUTSIDE
            // CaptionArea's safe zone. It looks like a button (the action banner reads
            // as a call-to-action, the badge as a stat chip), so unlike the caption text
            // below it, tapping it should act like one and commit into the encounter —
            // it has no ClickAbsorber, so a tap here bubbles straight up to the card's
            // own OnPointerClick, same as tapping the artwork.
            var topRowGo = new GameObject("TopRow", typeof(RectTransform));
            var topRowRect = (RectTransform)topRowGo.transform;
            topRowRect.SetParent(rect, false);
            topRowRect.anchorMin = new Vector2(0, CaptionAreaFraction);
            topRowRect.anchorMax = new Vector2(1, CaptionAreaFraction);
            topRowRect.pivot = new Vector2(0.5f, 0f);
            topRowRect.sizeDelta = new Vector2(0, TopRowHeight);
            topRowRect.anchoredPosition = Vector2.zero;
            topRowGo.AddComponent<LayoutElement>().ignoreLayout = true;

            var topRowLayout = topRowGo.AddComponent<HorizontalLayoutGroup>();
            topRowLayout.spacing = 12;
            topRowLayout.childAlignment = TextAnchor.MiddleLeft;
            topRowLayout.childForceExpandWidth = false;
            topRowLayout.childForceExpandHeight = true;
            topRowLayout.childControlWidth = true;
            topRowLayout.childControlHeight = true;

            // Names the action a tap on the artwork above takes (Fight/Collect/Enter).
            // Replaces the old per-card button — the artwork itself is the tap target,
            // this is just the label for it. No separate preview/confirm screen either
            // way (that redundant step was EncounterDetailView, since removed).
            var bannerGo = new GameObject("ActionBanner", typeof(RectTransform));
            bannerGo.transform.SetParent(topRowGo.transform, false);
            var bannerLayout = bannerGo.AddComponent<LayoutElement>();
            bannerLayout.flexibleWidth = 1;
            bannerLayout.minWidth = 0;
            bannerLayout.preferredWidth = 0;

            view._actionBannerImage = bannerGo.AddComponent<Image>();

            var bannerTextRect = CreateStretchChild(bannerGo.transform, "Text", Vector2.zero, Vector2.one);
            view._actionBannerText = bannerTextRect.gameObject.AddComponent<Text>();
            view._actionBannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view._actionBannerText.fontSize = 30;
            view._actionBannerText.fontStyle = FontStyle.Bold;
            view._actionBannerText.alignment = TextAnchor.MiddleCenter;
            view._actionBannerText.color = Color.white;

            // Predicted odds for the entry-level variant. Two numbers stacked — overall
            // success chance (color-graded red (0%) -> green (100%) on the badge itself)
            // on top, and how much of that is a "close call" that still costs health
            // underneath.
            var badgeGo = new GameObject("ChanceBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(topRowGo.transform, false);
            var badgeLayout = badgeGo.AddComponent<LayoutElement>();
            badgeLayout.preferredWidth = 150;
            badgeLayout.flexibleWidth = 0;

            view._chanceBadge = badgeGo.AddComponent<Image>();

            var chanceTextRect = CreateStretchChild(badgeGo.transform, "Text", new Vector2(0, 0.45f), Vector2.one);
            view._chanceText = chanceTextRect.gameObject.AddComponent<Text>();
            view._chanceText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view._chanceText.fontSize = 30;
            view._chanceText.fontStyle = FontStyle.Bold;
            view._chanceText.alignment = TextAnchor.MiddleCenter;
            view._chanceText.color = Color.white;

            var riskTextRect = CreateStretchChild(badgeGo.transform, "RiskText", Vector2.zero, new Vector2(1, 0.45f));
            view._riskText = riskTextRect.gameObject.AddComponent<Text>();
            view._riskText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view._riskText.fontSize = 20;
            view._riskText.fontStyle = FontStyle.Bold;
            view._riskText.alignment = TextAnchor.MiddleCenter;
            view._riskText.color = new Color(1f, 0.85f, 0.4f);

            view._titleText = CreateLabel(captionAreaRect, "Title", 52, FontStyle.Bold);
            view._badgeText = CreateLabel(captionAreaRect, "Badge", 34, FontStyle.Italic);
            view._rewardText = CreateLabel(captionAreaRect, "Reward", 38, FontStyle.Normal);
            view._rewardDetailText = CreateLabel(captionAreaRect, "RewardDetail", 30, FontStyle.Bold);
            view._rewardDetailText.color = new Color(0.75f, 0.95f, 0.6f, 0.9f);

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
            _rewardDetailText.text = ExpectedRewardText(data);

            _actionBannerText.text = ActionLabel(data.type).ToUpperInvariant();
            _actionBannerImage.color = ActionColor(data.type);
        }

        // A concrete, always-accurate expectation on top of the free-text rewardSummary
        // above (whose hand-authored wording doesn't always spell out the XP number —
        // e.g. Treasure's "guaranteed loot" line never states one). Every non-Dungeon
        // win grants the same tier-0 "Common" item (see CombatResolutionView/
        // RewardGenerator.GenerateItem(0)); Dungeon spans Common up to a guaranteed Rare
        // treasure room (see DungeonGenerator); Tavern never grants anything.
        private static string ExpectedRewardText(EncounterData data)
        {
            return data.type switch
            {
                EncounterType.Tavern => "No combat — shop, chat, and upgrade",
                EncounterType.Dungeon => $"+{data.baseXpReward}+ XP · Common → Rare Gear",
                EncounterType.Resource => $"+{data.baseResourceYield}+ {data.resourceMaterial.title}",
                EncounterType.Landing => "",
                _ => $"+{data.baseXpReward} XP · Common Gear"
            };
        }

        // What tapping this card actually does, named for the action rather than the
        // encounter type — a fight, a free pickup, or just walking into a place.
        private static string ActionLabel(EncounterType type)
        {
            return type switch
            {
                EncounterType.Enemy => "Fight",
                EncounterType.Treasure => "Collect",
                EncounterType.Dungeon => "Enter",
                EncounterType.Tavern => "Enter",
                EncounterType.Resource => "Gather",
                EncounterType.Landing => "",
                _ => "Enter"
            };
        }

        private static Color ActionColor(EncounterType type)
        {
            return type switch
            {
                EncounterType.Enemy => new Color(0.7f, 0.25f, 0.2f, 0.9f),
                EncounterType.Treasure => new Color(0.8f, 0.65f, 0.15f, 0.9f),
                EncounterType.Dungeon => new Color(0.4f, 0.3f, 0.65f, 0.9f),
                EncounterType.Tavern => new Color(0.5f, 0.35f, 0.2f, 0.9f),
                EncounterType.Resource => new Color(0.35f, 0.55f, 0.25f, 0.9f),
                EncounterType.Landing => new Color(1f, 1f, 1f, 0.08f),
                _ => new Color(1f, 1f, 1f, 0.15f)
            };
        }

        // Dungeon cards have no single upfront difficulty (they're a multi-step run —
        // see DungeonView) so they read as "Dungeon Door" instead of "{type} · Standard".
        // Tavern is a non-combat social hub, so it gets its own plain label too. Shared
        // by Bind and SetLocked so the two never drift out of sync.
        private static string BaseBadgeText(EncounterData data)
        {
            return data.type switch
            {
                EncounterType.Dungeon => "Dungeon Door",
                EncounterType.Tavern => "The Village",
                EncounterType.Resource => $"{data.resourceMaterial.title} Node",
                EncounterType.Landing => "",
                _ => $"{data.type} · {EntryDifficultyLabel}"
            };
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
            _rewardDetailText.color = new Color(0.75f, 0.95f, 0.6f, locked ? 0.35f : 0.9f);
            _badgeText.color = new Color(1, 1, 1, textAlpha);
            _badgeText.text = locked
                ? $"{BaseBadgeText(_data)} · {reasonLabel}"
                : BaseBadgeText(_data);
            _chanceText.color = new Color(1, 1, 1, textAlpha);
            _riskText.color = new Color(1f, 0.85f, 0.4f, textAlpha);
            SetOdds(_winChance, _closeCallChance);

            _actionBannerImage.color = ActionColor(_data.type) * new Color(1f, 1f, 1f, locked ? 0.4f : 1f);
            _actionBannerText.color = new Color(1, 1, 1, textAlpha);
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

        // Catches a click before it bubbles up to the card's own OnPointerClick above —
        // Unity resolves an unhandled IPointerClickHandler by walking up to the nearest
        // ancestor that has one, so simply having this component (even doing nothing)
        // on CaptionArea is enough to make it a dead end for the click. See CaptionArea's
        // creation comment for why this exists.
        private class ClickAbsorber : MonoBehaviour, IPointerClickHandler
        {
            public void OnPointerClick(PointerEventData eventData)
            {
            }
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
