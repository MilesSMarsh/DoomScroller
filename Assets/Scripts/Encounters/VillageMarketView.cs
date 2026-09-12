using System.Collections.Generic;
using System.Linq;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // One establishment in the Village (see VillageView): a Buy tab (spend gold on
    // armor, talismans, and crafting materials) and a Sell tab (unequipped items and
    // materials for gold). Stock is generated once per distinct village (keyed by the
    // EncounterData reference for that village's feed card, see _stockByVillage) and
    // never rerolled again on re-entry — only a genuinely different village gets its own
    // fresh roll. This is a single long-lived instance reused for every village, same as
    // VillageBlacksmithView.
    public class VillageMarketView : MonoBehaviour
    {
        private const float RowHeight = 150f;
        private const int PanelRowCapacity = 5;

        private static readonly Color SelectedTabColor = new(1f, 1f, 1f, 0.25f);
        private static readonly Color UnselectedTabColor = new(1f, 1f, 1f, 0.08f);

        // Cached per village so re-entering the same one shows the same stock, and which
        // entries were already bought there — without this, a stable-but-unpurchased-
        // state stock would let the same entry be bought over and over on repeat visits.
        private class VillageStock
        {
            public IReadOnlyList<MarketEntry> Entries;
            public readonly HashSet<ItemData> Purchased = new();
        }

        private readonly Dictionary<EncounterData, VillageStock> _stockByVillage = new();
        private VillageStock _currentStock;

        private RectTransform _buyListContainer;
        private RectTransform _sellListContainer;
        private GameObject _buyPanelGo;
        private GameObject _sellPanelGo;
        private Image _buyTabImage;
        private Image _sellTabImage;

        private Text _statusText;
        private PlayerLoadout _loadout;
        private PlayerWallet _wallet;
        private PlayerAttributes _attributes;

        public static VillageMarketView Create(Transform parent)
        {
            var go = new GameObject("Market", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var view = go.AddComponent<VillageMarketView>();

            var subtitle = CreateLabel(go.transform, "Subtitle", "Buy gear and materials, or sell what you don't need.", 28, FontStyle.Italic);
            subtitle.color = new Color(1f, 1f, 1f, 0.6f);
            SetFixedHeight(subtitle.GetComponent<LayoutElement>(), 48);

            view._statusText = CreateLabel(go.transform, "Status", "", 24, FontStyle.Italic);
            view._statusText.color = new Color(0.6f, 0.9f, 1f, 0.95f);
            SetFixedHeight(view._statusText.GetComponent<LayoutElement>(), 40);

            var tabRowGo = new GameObject("TabRow", typeof(RectTransform));
            tabRowGo.transform.SetParent(go.transform, false);
            // All three explicit — an under-specified LayoutElement here loses a
            // tie-break to this GameObject's own HorizontalLayoutGroup, same bug/fix as
            // VillageBlacksmithView's TabRow.
            SetFixedHeight(tabRowGo.AddComponent<LayoutElement>(), 88);
            var tabRowLayout = tabRowGo.AddComponent<HorizontalLayoutGroup>();
            tabRowLayout.spacing = 10;
            tabRowLayout.childForceExpandWidth = true;
            tabRowLayout.childForceExpandHeight = true;
            tabRowLayout.childControlWidth = true;
            tabRowLayout.childControlHeight = true;

            CreateTabButton(tabRowGo.transform, "BuyTab", "Buy", () => view.SelectTab(0), out view._buyTabImage);
            CreateTabButton(tabRowGo.transform, "SellTab", "Sell", () => view.SelectTab(1), out view._sellTabImage);

            var panelHeight = RowHeight * PanelRowCapacity + 30;

            view._buyPanelGo = CreatePanel(go.transform, "BuyPanel", panelHeight, out view._buyListContainer);
            view._sellPanelGo = CreatePanel(go.transform, "SellPanel", panelHeight, out view._sellListContainer);

            view.SelectTab(0);

            return view;
        }

        private static GameObject CreatePanel(Transform parent, string name, float height, out RectTransform listContainer)
        {
            var panelGo = new GameObject(name, typeof(RectTransform));
            panelGo.transform.SetParent(parent, false);
            SetFixedHeight(panelGo.AddComponent<LayoutElement>(), height);

            // ScrollAreaFactory relies on its parent being a LayoutGroup that respects
            // childControlWidth/Height (see that file's doc comment).
            var panelGroup = panelGo.AddComponent<VerticalLayoutGroup>();
            panelGroup.childForceExpandWidth = true;
            panelGroup.childForceExpandHeight = true;
            panelGroup.childControlWidth = true;
            panelGroup.childControlHeight = true;

            listContainer = ScrollAreaFactory.Create(panelGo.transform, new RectOffset(0, 0, 0, 0), 10);
            return panelGo;
        }

        private void SelectTab(int index)
        {
            _buyPanelGo.SetActive(index == 0);
            _sellPanelGo.SetActive(index == 1);
            _buyTabImage.color = index == 0 ? SelectedTabColor : UnselectedTabColor;
            _sellTabImage.color = index == 1 ? SelectedTabColor : UnselectedTabColor;
        }

        // Called once per Village visit (see VillageView.Show). `village` identifies
        // which village this is — its stock is generated once (first time this key is
        // seen) and reused on every later visit, whether that's leaving and coming right
        // back or returning much later.
        public void Show(PlayerLoadout loadout, PlayerWallet wallet, PlayerAttributes attributes, EncounterData village)
        {
            // loadout is one long-lived instance for the whole session, so subscribing
            // exactly once (guarded below, same pattern VillageTavernView already uses
            // for its refill button) keeps the Sell tab in sync with equip/unequip
            // changes made elsewhere (Profile) without needing to leave and re-enter the
            // village to see them — RebuildSellPanel is cheap enough to run on every
            // loadout change, not just on Show().
            if (_loadout == null)
            {
                loadout.Changed += RebuildSellPanel;
            }

            _loadout = loadout;
            _wallet = wallet;
            _attributes = attributes;
            _statusText.text = "";

            if (!_stockByVillage.TryGetValue(village, out var stock))
            {
                stock = new VillageStock { Entries = MarketGenerator.GenerateStock() };
                _stockByVillage[village] = stock;
            }

            _currentStock = stock;

            RebuildBuyPanel();
            RebuildSellPanel();
        }

        private void RebuildBuyPanel()
        {
            for (var i = _buyListContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_buyListContainer.GetChild(i).gameObject);
            }

            foreach (var entry in _currentStock.Entries)
            {
                CreateMarketRow(entry);
            }
        }

        private void RebuildSellPanel()
        {
            for (var i = _sellListContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_sellListContainer.GetChild(i).gameObject);
            }

            // Grouped by item reference (same convention ProfileView.RefreshMaterials
            // already uses) so N owned copies of the same item/material show as one row
            // with a count, rather than N duplicate rows.
            var groups = _loadout.GetSellableItems().GroupBy(i => i).ToList();
            if (groups.Count == 0)
            {
                CreateLabel(_sellListContainer, "NoItems", "Nothing to sell yet", 28, FontStyle.Normal);
                return;
            }

            foreach (var group in groups)
            {
                CreateSellRow(group.Key, group.Count());
            }
        }

        private void CreateMarketRow(MarketEntry entry)
        {
            var rowGo = new GameObject($"Row_{entry.Item.title}", typeof(RectTransform));
            rowGo.transform.SetParent(_buyListContainer, false);
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

        private void CreateSellRow(ItemData item, int count)
        {
            var rowGo = new GameObject($"Sell_{item.title}", typeof(RectTransform));
            rowGo.transform.SetParent(_sellListContainer, false);
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
            GearRowFactory.SetIcon(row, item.icon, item.itemColor, item.rarity);
            row.CaptionText.text = item.category.ToString();
            row.TitleText.text = count > 1 ? $"{item.title} (x{count} owned)" : item.title;
            row.StatText.text = item.statSummary;

            // No explicit RebuildSellPanel() call needed here — SellItem fires
            // loadout.Changed, which the Show()-time subscription already routes there.
            CreateActionButton(rowGo.transform, "SellButton", $"Sell {item.goldValue}g", 220, RowHeight,
                new Color(0.55f, 0.4f, 0.15f, 0.7f), _ =>
                {
                    if (_loadout.SellItem(item, _wallet))
                    {
                        _statusText.text = $"Sold {item.title} for {item.goldValue} gold.";
                    }
                });
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

        private static void CreateTabButton(Transform parent, string name, string label, System.Action onClick, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            image = go.AddComponent<Image>();
            image.color = UnselectedTabColor;

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

        private static GameObject CreateActionButton(Transform parent, string name, string label,
            float width, float height, Color backgroundColor, System.Action<Button> onClick)
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
