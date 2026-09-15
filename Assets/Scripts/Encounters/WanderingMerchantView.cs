using System;
using System.Collections.Generic;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // A feed encounter: a small (3-item), gold-priced shop. Stock is cached per
    // EncounterData reference (_stockByMerchant) — the same pattern
    // VillageMarketView uses for its own per-village cache. The difference is what gets
    // used as the key: FeedController.EnsureFilledThrough gives every *sighting* of a
    // Wandering Merchant its own runtime Instantiate()'d EncounterData, so two
    // different sightings never share a cache entry even when they're clones of the
    // same static template asset — re-entering one sighting shows the same stock,
    // finding a new one rolls fresh stock. Never locked (no EncounterWon/Lost/Abandoned
    // wiring) — same as VillageView — the whole point is you can leave and come back.
    public class WanderingMerchantView : MonoBehaviour
    {
        private const float RowHeight = 186f;
        private const int PanelRowCapacity = 3;

        private class MerchantStock
        {
            public IReadOnlyList<MarketEntry> Entries;
            public readonly HashSet<ItemData> Purchased = new();
        }

        private readonly Dictionary<EncounterData, MerchantStock> _stockByMerchant = new();
        private MerchantStock _currentStock;

        private RectTransform _listContainer;
        private Text _titleText;
        private Text _statusText;

        private PlayerLoadout _loadout;
        private PlayerWallet _wallet;
        private PlayerAttributes _attributes;

        public event Action ReturnToFeedRequested;

        public static WanderingMerchantView Create(Transform parent)
        {
            var go = new GameObject("WanderingMerchant", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = go.AddComponent<Image>();
            background.color = new Color(0.06f, 0.05f, 0.04f, 1f);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, NavBarController.Height + 24, 24);
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var view = go.AddComponent<WanderingMerchantView>();

            view._titleText = CreateLabel(rect, "Title", "", 40, FontStyle.Bold);
            SetFixedHeight(view._titleText.GetComponent<LayoutElement>(), 64);

            var subtitle = CreateLabel(rect, "Subtitle", "A traveling trader — rare goods for gold.", 26, FontStyle.Italic);
            subtitle.color = new Color(1f, 1f, 1f, 0.6f);
            SetFixedHeight(subtitle.GetComponent<LayoutElement>(), 40);

            view._statusText = CreateLabel(rect, "Status", "", 24, FontStyle.Italic);
            view._statusText.color = new Color(0.6f, 0.9f, 1f, 0.95f);
            SetFixedHeight(view._statusText.GetComponent<LayoutElement>(), 40);

            var panelHeight = RowHeight * PanelRowCapacity + 30;
            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(rect, false);
            SetFixedHeight(panelGo.AddComponent<LayoutElement>(), panelHeight);
            var panelGroup = panelGo.AddComponent<VerticalLayoutGroup>();
            panelGroup.childForceExpandWidth = true;
            panelGroup.childForceExpandHeight = true;
            panelGroup.childControlWidth = true;
            panelGroup.childControlHeight = true;
            view._listContainer = ScrollAreaFactory.Create(panelGo.transform, new RectOffset(0, 0, 0, 0), 10);

            CreateReturnButton(rect, () => view.ReturnToFeedRequested?.Invoke());

            go.SetActive(false);
            return view;
        }

        // village-less version of VillageMarketView's Show — a merchant sighting has no
        // village concept, just the EncounterData reference the caller already gave a
        // per-sighting identity to (see the class comment).
        public void Show(EncounterData data, PlayerLoadout loadout, PlayerWallet wallet, PlayerAttributes attributes)
        {
            gameObject.SetActive(true);

            _loadout = loadout;
            _wallet = wallet;
            _attributes = attributes;
            _statusText.text = "";
            _titleText.text = data.title;

            if (!_stockByMerchant.TryGetValue(data, out var stock))
            {
                stock = new MerchantStock { Entries = WanderingMerchantGenerator.GenerateStock() };
                _stockByMerchant[data] = stock;
            }

            _currentStock = stock;
            RebuildPanel();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void RebuildPanel()
        {
            for (var i = _listContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_listContainer.GetChild(i).gameObject);
            }

            foreach (var entry in _currentStock.Entries)
            {
                CreateMerchantRow(entry);
            }
        }

        private void CreateMerchantRow(MarketEntry entry)
        {
            var rowGo = new GameObject($"Row_{entry.Item.title}", typeof(RectTransform));
            rowGo.transform.SetParent(_listContainer, false);
            SetFixedHeight(rowGo.AddComponent<LayoutElement>(), RowHeight);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            var infoGo = new GameObject("Info", typeof(RectTransform));
            infoGo.transform.SetParent(rowGo.transform, false);
            infoGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
            var infoLayout = infoGo.AddComponent<LayoutElement>();
            infoLayout.minWidth = 0;
            infoLayout.preferredWidth = 0;
            infoLayout.flexibleWidth = 1;
            infoLayout.minHeight = RowHeight;
            infoLayout.preferredHeight = RowHeight;
            infoLayout.flexibleHeight = 0;

            var row = GearRowFactory.Create(infoGo.transform, 64, 22, 30, 26);
            GearRowFactory.SetIcon(row, entry.Item.icon, entry.Item.itemColor, entry.Item.rarity);
            GearRowFactory.SetWeaponDetail(row, entry.Item);
            row.CaptionText.text = entry.Item.category.ToString();
            row.TitleText.text = entry.Item.title;
            row.StatText.text = entry.Item.statSummary;

            var alreadyPurchased = _currentStock.Purchased.Contains(entry.Item);
            var price = Mathf.RoundToInt(entry.Price * _attributes.GoldCostMultiplier);
            var buttonGo = CreateActionButton(rowGo.transform, "BuyButton", alreadyPurchased ? "Sold" : $"Buy {price}g",
                220, RowHeight, new Color(0.2f, 0.55f, 0.3f, 0.7f), buyButton =>
                {
                    if (_wallet.TrySpendGold(price))
                    {
                        _loadout.AddToInventory(entry.Item);
                        _currentStock.Purchased.Add(entry.Item);
                        buyButton.GetComponentInChildren<Text>().text = "Sold";
                        buyButton.interactable = false;
                        _statusText.text = $"Bought {entry.Item.title} for {price} gold.";
                    }
                    else
                    {
                        _statusText.text = "Not enough gold.";
                    }
                });
            buttonGo.GetComponent<Button>().interactable = !alreadyPurchased;
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

            go.AddComponent<LayoutElement>();

            return text;
        }

        private static void CreateReturnButton(Transform parent, Action onClick)
        {
            var go = new GameObject("ReturnButton", typeof(RectTransform));
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
            labelText.text = "Return";
            labelText.raycastTarget = false;
        }

        private static GameObject CreateActionButton(Transform parent, string name, string label,
            float width, float height, Color backgroundColor, Action<Button> onClick)
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

            button.onClick.AddListener(() => onClick(button));

            return go;
        }
    }
}
