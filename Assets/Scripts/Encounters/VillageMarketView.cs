using DoomScroller.Inventory;
using DoomScroller.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // One establishment in the Village (see VillageView): spend gold on armor,
    // talismans, and crafting materials. Stock rerolls fresh every visit
    // (MarketGenerator.GenerateStock) — no persistent shop inventory yet.
    public class VillageMarketView : MonoBehaviour
    {
        private const float RowHeight = 150f;

        private RectTransform _listContainer;
        private Text _statusText;
        private PlayerLoadout _loadout;
        private PlayerWallet _wallet;

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

            var subtitle = CreateLabel(go.transform, "Subtitle", "Armor, talismans, and crafting materials.", 28, FontStyle.Italic);
            subtitle.color = new Color(1f, 1f, 1f, 0.6f);
            SetFixedHeight(subtitle.GetComponent<LayoutElement>(), 48);

            view._statusText = CreateLabel(go.transform, "Status", "", 24, FontStyle.Italic);
            view._statusText.color = new Color(0.6f, 0.9f, 1f, 0.95f);
            SetFixedHeight(view._statusText.GetComponent<LayoutElement>(), 40);

            var listGo = new GameObject("List", typeof(RectTransform));
            view._listContainer = (RectTransform)listGo.transform;
            view._listContainer.SetParent(go.transform, false);
            var listLayout = listGo.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 10;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;

            return view;
        }

        // Called once per Village visit (see VillageView.Show) — rerolls the stock fresh
        // every time, per the "no persistent shop inventory yet" call on this.
        public void Show(PlayerLoadout loadout, PlayerWallet wallet)
        {
            _loadout = loadout;
            _wallet = wallet;
            _statusText.text = "";

            for (var i = _listContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_listContainer.GetChild(i).gameObject);
            }

            foreach (var entry in MarketGenerator.GenerateStock())
            {
                CreateMarketRow(entry);
            }
        }

        private void CreateMarketRow(MarketEntry entry)
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
            row.Icon.color = entry.Item.itemColor;
            row.CaptionText.text = entry.Item.category.ToString();
            row.TitleText.text = entry.Item.title;
            row.StatText.text = entry.Item.statSummary;

            CreateActionButton(rowGo.transform, "BuyButton", $"Buy {entry.Price}g", 220, RowHeight,
                new Color(0.2f, 0.55f, 0.3f, 0.7f), buyButton =>
                {
                    if (_wallet.TrySpendGold(entry.Price))
                    {
                        _loadout.AddToInventory(entry.Item);
                        buyButton.GetComponentInChildren<Text>().text = "Sold";
                        buyButton.interactable = false;
                        _statusText.text = $"Bought {entry.Item.title} for {entry.Price} gold.";
                    }
                    else
                    {
                        _statusText.text = "Not enough gold.";
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

        private static void CreateActionButton(Transform parent, string name, string label,
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
        }
    }
}
