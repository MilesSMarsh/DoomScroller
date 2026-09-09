using DoomScroller.Inventory;
using DoomScroller.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // One establishment in the Village (see VillageView): spend gold + crafting
    // materials to raise how many revive potions the player can hold (capacity) and how
    // much HP they revive at (quality). Materials are undifferentiated for this purchase
    // — any N owned, regardless of type; which specific material a recipe wants is a
    // detail for when real crafting (section 3, still a stub) exists.
    public class VillageAlchemistView : MonoBehaviour
    {
        private const int BaseGoldCost = 15;
        private const int GoldCostPerLevel = 10;
        private const int BaseMaterialsCost = 2;
        private const int MaterialsCostPerLevel = 1;

        private PlayerLoadout _loadout;
        private PlayerWallet _wallet;
        private PlayerAttributes _attributes;

        private Text _capacityStatusText;
        private Text _capacityCostText;
        private GameObject _capacityButtonGo;
        private Text _qualityStatusText;
        private Text _qualityCostText;
        private GameObject _qualityButtonGo;

        public static VillageAlchemistView Create(Transform parent)
        {
            var go = new GameObject("Alchemist", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var view = go.AddComponent<VillageAlchemistView>();

            var subtitle = CreateLabel(go.transform, "Subtitle", "Upgrade your revive flasks.", 28, FontStyle.Italic);
            subtitle.color = new Color(1f, 1f, 1f, 0.6f);
            SetFixedHeight(subtitle.GetComponent<LayoutElement>(), 48);

            // Section 1 — flask capacity.
            CreateLabel(go.transform, "CapacityHeader", "Flask Capacity", 34, FontStyle.Bold);
            view._capacityStatusText = CreateLabel(go.transform, "CapacityStatus", "", 26, FontStyle.Normal);
            SetFixedHeight(view._capacityStatusText.GetComponent<LayoutElement>(), 40);
            view._capacityCostText = CreateLabel(go.transform, "CapacityCost", "", 24, FontStyle.Italic);
            view._capacityCostText.color = new Color(0.6f, 0.9f, 1f, 0.95f);
            SetFixedHeight(view._capacityCostText.GetComponent<LayoutElement>(), 36);
            view._capacityButtonGo = CreateButton(go.transform, "CapacityButton", "Upgrade",
                () => view.OnUpgradeCapacityClicked());
            SetFixedHeight(view._capacityButtonGo.GetComponent<LayoutElement>(), 90);

            // Section 2 — flask quality.
            CreateLabel(go.transform, "QualityHeader", "Flask Quality", 34, FontStyle.Bold);
            view._qualityStatusText = CreateLabel(go.transform, "QualityStatus", "", 26, FontStyle.Normal);
            SetFixedHeight(view._qualityStatusText.GetComponent<LayoutElement>(), 40);
            view._qualityCostText = CreateLabel(go.transform, "QualityCost", "", 24, FontStyle.Italic);
            view._qualityCostText.color = new Color(0.6f, 0.9f, 1f, 0.95f);
            SetFixedHeight(view._qualityCostText.GetComponent<LayoutElement>(), 36);
            view._qualityButtonGo = CreateButton(go.transform, "QualityButton", "Upgrade",
                () => view.OnUpgradeQualityClicked());
            SetFixedHeight(view._qualityButtonGo.GetComponent<LayoutElement>(), 90);

            // Section 3 — stub.
            CreateLabel(go.transform, "CraftHeader", "Craft Potions", 34, FontStyle.Bold);
            var craftBody = CreateLabel(go.transform, "CraftBody", "Crafting more potions is coming soon.", 26, FontStyle.Italic);
            craftBody.color = new Color(1f, 1f, 1f, 0.6f);

            return view;
        }

        // Called once per Village visit (see VillageView.Show).
        public void Show(PlayerLoadout loadout, PlayerWallet wallet, PlayerAttributes attributes)
        {
            _loadout = loadout;
            _wallet = wallet;
            _attributes = attributes;
            Refresh();
        }

        private static int GoldCostFor(int level) => BaseGoldCost + level * GoldCostPerLevel;
        private static int MaterialsCostFor(int level) => BaseMaterialsCost + level * MaterialsCostPerLevel;
        private int DiscountedGoldCostFor(int level) => Mathf.RoundToInt(GoldCostFor(level) * _attributes.GoldCostMultiplier);

        private void Refresh()
        {
            var potion = _loadout.GetKnownRevivePotionType();
            var ownedMaterials = _loadout.CountMaterials();

            if (potion == null)
            {
                _capacityStatusText.text = "No revive potions discovered yet.";
                _capacityCostText.text = "";
                _capacityButtonGo.GetComponent<Button>().interactable = false;
                _qualityStatusText.text = "No revive potions discovered yet.";
                _qualityCostText.text = "";
                _qualityButtonGo.GetComponent<Button>().interactable = false;
                return;
            }

            var capacityLevel = _loadout.GetReviveCapacityLevel(potion);
            var capacity = _loadout.GetRefillCapacity(potion);
            var capacityMaxed = capacityLevel >= PlayerLoadout.MaxUpgradeLevel;
            _capacityStatusText.text = $"Holds up to {capacity} revive potion{(capacity == 1 ? "" : "s")}";
            RefreshSection(_capacityCostText, _capacityButtonGo, capacityLevel, capacityMaxed, ownedMaterials);

            var qualityLevel = _loadout.GetReviveQualityLevel(potion);
            var qualityMaxed = qualityLevel >= PlayerLoadout.MaxUpgradeLevel;
            _qualityStatusText.text = $"Revives at {_loadout.GetRevivePotionAmount(potion)} HP";
            RefreshSection(_qualityCostText, _qualityButtonGo, qualityLevel, qualityMaxed, ownedMaterials);
        }

        private void RefreshSection(Text costText, GameObject buttonGo, int level, bool maxed, int ownedMaterials)
        {
            var button = buttonGo.GetComponent<Button>();
            var label = buttonGo.GetComponentInChildren<Text>();

            if (maxed)
            {
                costText.text = "Maxed out.";
                label.text = "Maxed";
                button.interactable = false;
                return;
            }

            var goldCost = DiscountedGoldCostFor(level);
            var materialsCost = MaterialsCostFor(level);
            costText.text = $"Cost: {goldCost}g + {materialsCost} materials (you have {ownedMaterials})";
            label.text = "Upgrade";
            button.interactable = _wallet.Gold >= goldCost && ownedMaterials >= materialsCost;
        }

        private void OnUpgradeCapacityClicked()
        {
            var potion = _loadout.GetKnownRevivePotionType();
            var level = _loadout.GetReviveCapacityLevel(potion);
            _loadout.TryUpgradeReviveCapacity(potion, _wallet, DiscountedGoldCostFor(level), MaterialsCostFor(level));
            Refresh();
        }

        private void OnUpgradeQualityClicked()
        {
            var potion = _loadout.GetKnownRevivePotionType();
            var level = _loadout.GetReviveQualityLevel(potion);
            _loadout.TryUpgradeReviveQuality(potion, _wallet, DiscountedGoldCostFor(level), MaterialsCostFor(level));
            Refresh();
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

        private static GameObject CreateButton(Transform parent, string name, string label, System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.15f);

            go.AddComponent<LayoutElement>();

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
            labelText.fontSize = 30;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false;

            return go;
        }
    }
}
