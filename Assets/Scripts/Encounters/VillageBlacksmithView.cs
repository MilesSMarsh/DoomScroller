using System;
using System.Collections.Generic;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // One establishment in the Village (see VillageView): craft armor, weapons,
    // talismans, and crafting materials, split across four tabs. Recipes are generated
    // exactly once per session (CraftingGenerator.GenerateRecipes, on the first-ever
    // Show()) and never rerolled again — the Blacksmith reads the same at every village,
    // and leaving/re-entering can't be used to reroll its stock. Materials cost is
    // undifferentiated ("any N materials") until real crafting trees exist.
    public class VillageBlacksmithView : MonoBehaviour
    {
        private const float RowHeight = 130f;

        private static readonly (string Label, ItemCategory Category)[] Categories =
        {
            ("Armor", ItemCategory.Armor),
            ("Weapons", ItemCategory.Weapon),
            ("Talismans", ItemCategory.Talisman),
            ("Materials", ItemCategory.Material),
        };

        private static readonly Color SelectedTabColor = new(1f, 1f, 1f, 0.25f);
        private static readonly Color UnselectedTabColor = new(1f, 1f, 1f, 0.08f);

        private PlayerLoadout _loadout;
        private PlayerWallet _wallet;
        private PlayerAttributes _attributes;
        private Text _statusText;

        private readonly List<GameObject> _tabPanelGos = new();
        private readonly List<Image> _tabButtonImages = new();
        private readonly List<RectTransform> _listContainers = new();

        // Generated exactly once (first Show()) and never rebuilt again — see the class
        // comment. Cost labels still need refreshing on every Show() since
        // GoldCostMultiplier can change between visits (Expertise points spent), so each
        // row's recipe is kept alongside its cost Text to update in place.
        private bool _recipesGenerated;
        private readonly List<(CraftingRecipe Recipe, Text CostText)> _recipeRows = new();

        private GameObject _confirmDialogGo;
        private Text _confirmMessageText;
        private GameObject _confirmCraftButtonGo;
        private CraftingRecipe _pendingRecipe;

        public static VillageBlacksmithView Create(Transform parent)
        {
            var go = new GameObject("Blacksmith", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var view = go.AddComponent<VillageBlacksmithView>();

            var subtitle = CreateLabel(go.transform, "Subtitle", "Craft armor, weapons, talismans, and materials.", 28, FontStyle.Italic);
            subtitle.color = new Color(1f, 1f, 1f, 0.6f);
            SetFixedHeight(subtitle.GetComponent<LayoutElement>(), 48);

            view._statusText = CreateLabel(go.transform, "Status", "", 24, FontStyle.Italic);
            view._statusText.color = new Color(0.6f, 0.9f, 1f, 0.95f);
            SetFixedHeight(view._statusText.GetComponent<LayoutElement>(), 40);

            var tabRowGo = new GameObject("TabRow", typeof(RectTransform));
            tabRowGo.transform.SetParent(go.transform, false);
            // All three explicit (SetFixedHeight), not just preferredHeight: this
            // GameObject's own HorizontalLayoutGroup below is itself an ILayoutElement
            // tying with this LayoutElement on layoutPriority, and an unset
            // minHeight/flexibleHeight here lets the group's own (much larger) computed
            // value win the tie — same bug/fix as FeedCardView's TopRow and
            // CombatPlaybackView's outer layout.
            SetFixedHeight(tabRowGo.AddComponent<LayoutElement>(), 88);
            var tabRowLayout = tabRowGo.AddComponent<HorizontalLayoutGroup>();
            tabRowLayout.spacing = 10;
            tabRowLayout.childForceExpandWidth = true;
            tabRowLayout.childForceExpandHeight = true;
            tabRowLayout.childControlWidth = true;
            tabRowLayout.childControlHeight = true;

            for (var i = 0; i < Categories.Length; i++)
            {
                var index = i;
                CreateTabButton(tabRowGo.transform, $"{Categories[i].Label}Tab", Categories[i].Label,
                    () => view.SelectCategoryTab(index), out var tabImage);
                view._tabButtonImages.Add(tabImage);
            }

            // One scrollable panel per category — safe now that VillageView only ever
            // recognizes a swipe starting on the EstablishmentStrip bar above, not
            // anywhere in an establishment's own content (see VillageView.SwipeForwarder).
            // Bounded to a fixed height sized for the usual recipe count rather than
            // flexibleHeight=1 — that stretched it (and this establishment's whole root)
            // to fill the entire remaining page regardless of how little content there
            // was, leaving a large empty scrollable void under a short list. This still
            // scrolls if a tab's recipe count ever grows past what fits.
            var panelHeight = RowHeight * 4 + 30;
            foreach (var (label, _) in Categories)
            {
                var panelGo = new GameObject($"{label}Panel", typeof(RectTransform));
                panelGo.transform.SetParent(go.transform, false);
                SetFixedHeight(panelGo.AddComponent<LayoutElement>(), panelHeight);

                // ScrollAreaFactory's own result relies on its parent being a LayoutGroup
                // that respects childControlWidth/Height (see that file's doc comment) —
                // a bare LayoutElement on panelGo alone isn't enough, so it needs one of
                // its own driving its single ScrollArea child.
                var panelGroup = panelGo.AddComponent<VerticalLayoutGroup>();
                panelGroup.childForceExpandWidth = true;
                panelGroup.childForceExpandHeight = true;
                panelGroup.childControlWidth = true;
                panelGroup.childControlHeight = true;

                var scrollContent = ScrollAreaFactory.Create(panelGo.transform, new RectOffset(0, 0, 0, 0), 10);
                view._listContainers.Add(scrollContent);
                view._tabPanelGos.Add(panelGo);
            }

            view._confirmDialogGo = CreateConfirmDialog((RectTransform)go.transform, view);

            view.SelectCategoryTab(0);

            return view;
        }

        // Called once per Village visit (see VillageView.Show). Recipes themselves are
        // only ever generated the first time this is called (see _recipesGenerated) —
        // every subsequent call just refreshes each row's cost label, since that can go
        // stale (GoldCostMultiplier) even though the recipe list itself never changes.
        public void Show(PlayerLoadout loadout, PlayerWallet wallet, PlayerAttributes attributes)
        {
            _loadout = loadout;
            _wallet = wallet;
            _attributes = attributes;
            _statusText.text = "";
            _confirmDialogGo.SetActive(false);

            if (!_recipesGenerated)
            {
                _recipesGenerated = true;
                for (var i = 0; i < Categories.Length; i++)
                {
                    foreach (var recipe in CraftingGenerator.GenerateRecipes(Categories[i].Category))
                    {
                        CreateRecipeRow(_listContainers[i], recipe);
                    }
                }
            }

            foreach (var (recipe, costText) in _recipeRows)
            {
                costText.text = $"{DiscountedGoldCost(recipe)}g + {recipe.MaterialsCost} materials";
            }
        }

        private void SelectCategoryTab(int index)
        {
            for (var i = 0; i < Categories.Length; i++)
            {
                _tabPanelGos[i].SetActive(i == index);
                _tabButtonImages[i].color = i == index ? SelectedTabColor : UnselectedTabColor;
            }
        }

        // A custom row rather than GearRowFactory — icon far left, name taking the
        // remaining space, cost as its own trailing column on the right, per what was
        // asked for (GearRowFactory's stacked caption/title/stat shape doesn't match).
        // The whole row is one Button — tap anywhere on it to open the confirm dialog.
        private void CreateRecipeRow(Transform parent, CraftingRecipe recipe)
        {
            var rowGo = new GameObject($"Recipe_{recipe.Item.title}", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            SetFixedHeight(rowGo.AddComponent<LayoutElement>(), RowHeight);
            rowGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
            var button = rowGo.AddComponent<Button>();
            button.onClick.AddListener(() => OnRecipeTapped(recipe));

            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 0, 0);
            rowLayout.spacing = 16;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(rowGo.transform, false);
            var iconLayout = iconGo.AddComponent<LayoutElement>();
            SetFixedLayout(iconLayout, 72, 72);
            var icon = iconGo.AddComponent<Image>();
            icon.color = recipe.Item.itemColor;
            icon.raycastTarget = false;

            var nameText = CreateLabel(rowGo.transform, "Name", recipe.Item.title, 28, FontStyle.Bold);
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.raycastTarget = false;
            var nameLayout = nameText.GetComponent<LayoutElement>();
            nameLayout.minWidth = 0;
            nameLayout.preferredWidth = 0;
            nameLayout.flexibleWidth = 1;

            var costText = CreateLabel(rowGo.transform, "Cost", $"{DiscountedGoldCost(recipe)}g + {recipe.MaterialsCost} materials",
                22, FontStyle.Italic);
            costText.alignment = TextAnchor.MiddleRight;
            costText.color = new Color(1f, 1f, 1f, 0.7f);
            costText.raycastTarget = false;
            var costLayout = costText.GetComponent<LayoutElement>();
            costLayout.minWidth = 260;
            costLayout.preferredWidth = 260;
            costLayout.flexibleWidth = 0;

            _recipeRows.Add((recipe, costText));
        }

        private int DiscountedGoldCost(CraftingRecipe recipe) => Mathf.RoundToInt(recipe.GoldCost * _attributes.GoldCostMultiplier);

        private void OnRecipeTapped(CraftingRecipe recipe)
        {
            _pendingRecipe = recipe;
            var goldCost = DiscountedGoldCost(recipe);
            var canAfford = _wallet.Gold >= goldCost && _loadout.CountMaterials() >= recipe.MaterialsCost;

            _confirmMessageText.text = canAfford
                ? $"Craft {recipe.Item.title} for {goldCost}g + {recipe.MaterialsCost} materials?"
                : $"Not enough gold or materials to craft {recipe.Item.title}.";
            _confirmCraftButtonGo.SetActive(canAfford);
            _confirmDialogGo.SetActive(true);
        }

        private void OnCraftConfirmed()
        {
            if (_loadout.TryCraftItem(_pendingRecipe.Item, _wallet, DiscountedGoldCost(_pendingRecipe), _pendingRecipe.MaterialsCost))
            {
                _statusText.text = $"Crafted {_pendingRecipe.Item.title}.";
            }

            _confirmDialogGo.SetActive(false);
        }

        private static GameObject CreateConfirmDialog(RectTransform parent, VillageBlacksmithView view)
        {
            var overlayGo = new GameObject("ConfirmDialog", typeof(RectTransform));
            var overlayRect = (RectTransform)overlayGo.transform;
            overlayRect.SetParent(parent, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            // This view's own root VerticalLayoutGroup (childControlHeight/Width=true)
            // would otherwise seize this child on activation and collapse it into the
            // layout instead of leaving it as a full-page overlay — same bug/fix as
            // DungeonView's leave-confirm dialog.
            overlayGo.AddComponent<LayoutElement>().ignoreLayout = true;
            overlayGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var boxGo = new GameObject("Box", typeof(RectTransform));
            var boxRect = (RectTransform)boxGo.transform;
            boxRect.SetParent(overlayRect, false);
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(800, 360);
            boxGo.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.16f, 1f);

            var boxLayout = boxGo.AddComponent<VerticalLayoutGroup>();
            boxLayout.padding = new RectOffset(32, 32, 28, 28);
            boxLayout.spacing = 20;
            boxLayout.childAlignment = TextAnchor.MiddleCenter;
            boxLayout.childForceExpandWidth = true;
            boxLayout.childForceExpandHeight = false;
            boxLayout.childControlWidth = true;
            boxLayout.childControlHeight = true;

            view._confirmMessageText = CreateLabel(boxRect, "Message", "", 30, FontStyle.Normal);
            SetFixedHeight(view._confirmMessageText.GetComponent<LayoutElement>(), 150);

            var buttonRowGo = new GameObject("ButtonRow", typeof(RectTransform));
            buttonRowGo.transform.SetParent(boxRect, false);
            SetFixedHeight(buttonRowGo.AddComponent<LayoutElement>(), 90);
            var buttonRowLayout = buttonRowGo.AddComponent<HorizontalLayoutGroup>();
            buttonRowLayout.spacing = 16;
            buttonRowLayout.childForceExpandWidth = false;
            buttonRowLayout.childForceExpandHeight = false;
            buttonRowLayout.childControlWidth = true;
            buttonRowLayout.childControlHeight = true;

            CreateActionButton(buttonRowGo.transform, "CancelButton", "Cancel", 340, 90,
                new Color(1f, 1f, 1f, 0.15f), () => view._confirmDialogGo.SetActive(false));
            view._confirmCraftButtonGo = CreateActionButton(buttonRowGo.transform, "CraftButton", "Craft", 340, 90,
                new Color(0.2f, 0.55f, 0.3f, 0.7f), () => view.OnCraftConfirmed());

            overlayGo.SetActive(false);
            return overlayGo;
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

            go.AddComponent<LayoutElement>();

            return text;
        }

        private static void CreateTabButton(Transform parent, string name, string label, Action onClick, out Image image)
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
            float width, float height, Color backgroundColor, Action onClick)
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
