using System;
using System.Collections;
using System.Collections.Generic;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // A tap-to-open reward chest: prompt -> shake -> pop open -> reveal XP/item/notes.
    // A content block, not a page/pager of its own — CombatResolutionView places one on
    // its own page-2 (with its own Return-to-Feed button, since that view hides its
    // footer Leave button once a fight is won); DungeonStepPageView embeds one directly
    // into its treasure-room page's existing vertical layout with no button of its own,
    // since DungeonView's persistent footer Return button already covers that (just
    // gated on ChestOpened having fired — see DungeonView._pendingChestClaim).
    public class RewardChestView : MonoBehaviour
    {
        private const float ShakeDuration = 0.4f;
        private const float ShakeAngle = 6f;
        private const int ShakeCycles = 5;
        private const float OpenPunchDuration = 0.18f;

        private static readonly Color ClosedColor = new(0.45f, 0.28f, 0.12f, 1f);
        private static readonly Color OpenColor = new(0.85f, 0.65f, 0.2f, 1f);

        // Fires once the reveal finishes and (if this instance has one) the
        // Return-to-Feed button is visible — tells the caller the claim gate is satisfied.
        public event Action ChestOpened;

        // Only raised when this instance was created with includeReturnButton: true.
        public event Action ReturnRequested;

        private RectTransform _chestRect;
        private Image _chestImage;
        private Sprite _chestSprite;
        private Button _chestButton;
        private Text _promptText;
        private Transform _rewardsContainer;
        private Text _xpText;
        private Text _goldText;
        private Transform _itemRowsContainer;
        private readonly List<GearRowFactory.Row> _itemRows = new();
        private Text _noteText;
        private GameObject _returnButtonGo;

        private bool _isOpen;
        private Coroutine _openCoroutine;
        private PlayerWallet _wallet;
        private int _pendingGoldReward;

        public static RewardChestView Create(Transform parent, bool includeReturnButton = true)
        {
            var go = new GameObject("RewardChest", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            // Stretch to fill whatever parent it's given — without this it sits at
            // Unity's untouched center-anchored 100x100 default (verified live via
            // RectTransform dumps), which is what made every label inside wrap after
            // almost every character and left the Return button effectively unreachable.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20;
            layout.padding = new RectOffset(32, 32, 24, 24);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var view = go.AddComponent<RewardChestView>();

            view._promptText = CreateLabel(rect, "Prompt", "Tap the chest to claim your reward", 30, FontStyle.Italic);
            SetFixedHeight(view._promptText.GetComponent<LayoutElement>(), 70);

            var chestGo = new GameObject("Chest", typeof(RectTransform));
            chestGo.transform.SetParent(rect, false);
            var chestLayoutElement = chestGo.AddComponent<LayoutElement>();
            SetFixedLayout(chestLayoutElement, 220, 160);
            var chestRect = (RectTransform)chestGo.transform;
            view._chestRect = chestRect;

            var chestImage = chestGo.AddComponent<Image>();
            chestImage.color = ClosedColor;
            view._chestImage = chestImage;

            var chestButton = chestGo.AddComponent<Button>();
            chestButton.onClick.AddListener(view.OnChestTapped);
            view._chestButton = chestButton;

            var rewardsGo = new GameObject("Rewards", typeof(RectTransform));
            rewardsGo.transform.SetParent(rect, false);
            var rewardsLayout = rewardsGo.AddComponent<VerticalLayoutGroup>();
            rewardsLayout.spacing = 10;
            rewardsLayout.childAlignment = TextAnchor.MiddleCenter;
            rewardsLayout.childForceExpandWidth = true;
            rewardsLayout.childForceExpandHeight = false;
            rewardsLayout.childControlWidth = true;
            rewardsLayout.childControlHeight = true;
            view._rewardsContainer = rewardsGo.transform;

            view._xpText = CreateLabel(rewardsGo.transform, "XpText", "", 34, FontStyle.Bold);
            SetFixedHeight(view._xpText.GetComponent<LayoutElement>(), 50);

            view._goldText = CreateLabel(rewardsGo.transform, "GoldText", "", 34, FontStyle.Bold);
            view._goldText.color = new Color(1f, 0.85f, 0.4f, 1f);
            SetFixedHeight(view._goldText.GetComponent<LayoutElement>(), 50);

            // Populated fresh per Show() call with one GearRowFactory row per rolled
            // item — a table's default min/maxItemCount of 1 means this is almost always
            // exactly one row, same look as before multi-item loot tables existed.
            var itemRowsGo = new GameObject("ItemRows", typeof(RectTransform));
            itemRowsGo.transform.SetParent(rewardsGo.transform, false);
            var itemRowsLayout = itemRowsGo.AddComponent<VerticalLayoutGroup>();
            itemRowsLayout.spacing = 10;
            itemRowsLayout.childAlignment = TextAnchor.MiddleCenter;
            itemRowsLayout.childForceExpandWidth = true;
            itemRowsLayout.childForceExpandHeight = false;
            itemRowsLayout.childControlWidth = true;
            itemRowsLayout.childControlHeight = true;
            view._itemRowsContainer = itemRowsGo.transform;

            view._noteText = CreateLabel(rewardsGo.transform, "Note", "", 26, FontStyle.Italic);
            view._noteText.color = new Color(0.6f, 0.9f, 1f, 0.95f);
            SetFixedHeight(view._noteText.GetComponent<LayoutElement>(), 70);

            rewardsGo.SetActive(false);

            if (includeReturnButton)
            {
                view._returnButtonGo = CreateButton(rect, "ReturnButton", "Return to Feed",
                    () => view.ReturnRequested?.Invoke());
                view._returnButtonGo.SetActive(false);
            }

            return view;
        }

        // Resets to the unopened state and stores what tapping the chest will reveal.
        // XP/the item are already granted by the time this is called (unchanged,
        // immediate on victory) — gold is not: it's only actually added to wallet once
        // the chest is opened (see ShakeThenOpen), which is the whole point of this
        // parameter existing here instead of being applied by the caller up front.
        public void Show(int xpReward, int goldReward, ItemData[] items, bool leveledUp, int newLevel, bool wasCloseCall,
            PlayerWallet wallet, Sprite chestSprite = null)
        {
            _wallet = wallet;
            _pendingGoldReward = goldReward;

            if (_openCoroutine != null)
            {
                StopCoroutine(_openCoroutine);
                _openCoroutine = null;
            }

            _isOpen = false;
            _chestButton.interactable = true;
            _chestRect.localRotation = Quaternion.identity;
            _chestRect.localScale = Vector3.one;
            // A custom chest sprite stays untinted through the whole open animation —
            // the brown-to-gold Closed/Open lerp below only applies to the flat-color
            // placeholder.
            _chestSprite = chestSprite;
            _chestImage.sprite = chestSprite;
            _chestImage.color = chestSprite != null ? Color.white : ClosedColor;

            _promptText.gameObject.SetActive(true);
            _rewardsContainer.gameObject.SetActive(false);
            if (_returnButtonGo != null)
            {
                _returnButtonGo.SetActive(false);
            }

            _xpText.text = $"+{xpReward} XP";
            _goldText.text = $"+{goldReward} Gold";

            for (var i = _itemRowsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_itemRowsContainer.GetChild(i).gameObject);
            }

            _itemRows.Clear();
            for (var i = 0; i < items.Length; i++)
            {
                var row = CreateItemRow();
                row.CaptionText.text = items.Length > 1 ? $"Item {i + 1}" : "Item";
                row.TitleText.text = items[i].title;
                row.StatText.text = items[i].statSummary;
                GearRowFactory.SetIcon(row, items[i].icon, items[i].itemColor, items[i].rarity);
                GearRowFactory.SetWeaponDetail(row, items[i]);
                _itemRows.Add(row);
            }

            var note = "";
            if (wasCloseCall)
            {
                note += "Close call — that one nearly had you.";
            }

            if (leveledUp)
            {
                note += (note.Length > 0 ? "\n" : "") + $"Level up! You're now level {newLevel}.";
            }

            _noteText.text = note;
            _noteText.gameObject.SetActive(note.Length > 0);
        }

        private GearRowFactory.Row CreateItemRow()
        {
            var row = GearRowFactory.Create(_itemRowsContainer, 64, 22, 30, 24);
            // GearRowFactory's row root (Icon -> IconFrame -> row root, two levels up
            // from Icon) has no LayoutElement of its own (it just stretches to whatever
            // parent gives it) — add one so it gets a sane height here. Tall enough for
            // the weapon detail line (GearRowFactory.SetWeaponDetail) to show without
            // clipping when this reward turns out to be a weapon.
            var rowLayoutElement = row.Icon.transform.parent.parent.gameObject.AddComponent<LayoutElement>();
            SetFixedHeight(rowLayoutElement, 126);
            return row;
        }

        private void OnChestTapped()
        {
            if (_isOpen)
            {
                return;
            }

            _isOpen = true;
            _chestButton.interactable = false;
            _openCoroutine = StartCoroutine(ShakeThenOpen());
        }

        private IEnumerator ShakeThenOpen()
        {
            var elapsed = 0f;
            while (elapsed < ShakeDuration)
            {
                var t = elapsed / ShakeDuration;
                var cycle = Mathf.Sin(t * ShakeCycles * Mathf.PI * 2f);
                _chestRect.localRotation = Quaternion.Euler(0, 0, cycle * ShakeAngle * (1f - t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _chestRect.localRotation = Quaternion.identity;

            elapsed = 0f;
            while (elapsed < OpenPunchDuration)
            {
                var t = elapsed / OpenPunchDuration;
                var scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.25f;
                _chestRect.localScale = new Vector3(scale, scale, 1f);
                _chestImage.color = _chestSprite != null ? Color.white : Color.Lerp(ClosedColor, OpenColor, t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _chestRect.localScale = Vector3.one;
            _chestImage.color = _chestSprite != null ? Color.white : OpenColor;

            _promptText.gameObject.SetActive(false);
            _rewardsContainer.gameObject.SetActive(true);

            if (_returnButtonGo != null)
            {
                _returnButtonGo.SetActive(true);
            }

            // The one moment gold actually changes hands — see Show()'s doc comment.
            _wallet.AddGold(_pendingGoldReward);

            _openCoroutine = null;
            ChestOpened?.Invoke();
        }

        private static void SetFixedLayout(LayoutElement layoutElement, float width, float height)
        {
            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            layoutElement.flexibleWidth = 0;
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleHeight = 0;
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
            text.verticalOverflow = VerticalWrapMode.Overflow;

            go.AddComponent<LayoutElement>();

            return text;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.15f);

            var layoutElement = go.AddComponent<LayoutElement>();
            SetFixedHeight(layoutElement, 110);

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
            labelText.fontSize = 34;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false;

            return go;
        }
    }
}
