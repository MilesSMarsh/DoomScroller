using System;
using System.Collections.Generic;
using System.Linq;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // One establishment in the Village (see VillageView): a Craft tab (armor, weapons,
    // talismans, and crafting materials, split across the four category sub-tabs — the
    // original functionality, unchanged) and an Upgrade tab (Empower/Reroll sub-tabs,
    // each listing every owned weapon). Recipes are generated exactly once per session
    // (RecipeCatalog.RecipesFor, on the first-ever Show()) and never rerolled again —
    // the Blacksmith reads the same at every village, and leaving/re-entering can't be
    // used to reroll its stock. Each recipe names specific material ingredients and is
    // gated behind having discovered them (and crafted any prerequisite recipes) — see
    // PlayerLoadout.IsRecipeUnlocked. The Upgrade tab's weapon lists, unlike recipes,
    // rebuild from scratch on every loadout change instead of refreshing in place, since
    // which weapons are owned (not just their lock state) can change at any time.
    public class VillageBlacksmithView : MonoBehaviour
    {
        private const float RowHeight = 160f;

        private static readonly (string Label, ItemCategory Category)[] Categories =
        {
            ("Armor", ItemCategory.Armor),
            ("Weapons", ItemCategory.Weapon),
            ("Talismans", ItemCategory.Talisman),
            ("Materials", ItemCategory.Material),
        };

        private static readonly string[] UpgradeTabLabels = { "Empower", "Reroll" };

        private static readonly Color SelectedTabColor = new(1f, 1f, 1f, 0.25f);
        private static readonly Color UnselectedTabColor = new(1f, 1f, 1f, 0.08f);

        private PlayerLoadout _loadout;
        private PlayerWallet _wallet;
        private PlayerAttributes _attributes;
        private Text _statusText;

        // Root tabs: Craft (index 0) / Upgrade (index 1).
        private readonly List<GameObject> _rootPanelGos = new();
        private readonly List<Image> _rootTabButtonImages = new();

        // Craft tab's own category sub-tabs (Armor/Weapons/Talismans/Materials).
        private readonly List<GameObject> _tabPanelGos = new();
        private readonly List<Image> _tabButtonImages = new();
        private readonly List<RectTransform> _listContainers = new();

        // Upgrade tab's own sub-tabs (Empower/Reroll).
        private readonly List<GameObject> _upgradeTabPanelGos = new();
        private readonly List<Image> _upgradeTabButtonImages = new();
        private readonly List<RectTransform> _upgradeListContainers = new();

        // Generated exactly once (first Show()) and never rebuilt again — see the class
        // comment. Every row needs refreshing on every Show(), not just its cost label:
        // GoldCostMultiplier can change between visits (Expertise points spent), and a
        // recipe's lock state can too (a material discovered, or a prerequisite crafted,
        // at the Market or elsewhere since the last visit).
        private bool _recipesGenerated;
        private readonly List<(RecipeDefinition Recipe, Text CostText, Image RowBackground, Button RowButton)> _recipeRows = new();

        private static readonly Color LockedRowColor = new(1f, 1f, 1f, 0.02f);
        private static readonly Color UnlockedRowColor = new(1f, 1f, 1f, 0.06f);

        private GameObject _confirmDialogGo;
        private Text _confirmMessageText;
        private GameObject _confirmCraftButtonGo;

        // Set by whichever of OnRecipeTapped/OnEmpowerTapped/OnRerollTapped last opened
        // the (shared) confirm dialog — invoked once by its Craft/Confirm button, so one
        // dialog GameObject tree serves all three actions instead of duplicating it.
        private Action _pendingConfirmAction;

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

            var subtitle = CreateLabel(go.transform, "Subtitle",
                "Craft armor, weapons, talismans, and materials, or upgrade what you already own.", 28, FontStyle.Italic);
            subtitle.color = new Color(1f, 1f, 1f, 0.6f);
            SetFixedHeight(subtitle.GetComponent<LayoutElement>(), 48);

            view._statusText = CreateLabel(go.transform, "Status", "", 24, FontStyle.Italic);
            view._statusText.color = new Color(0.6f, 0.9f, 1f, 0.95f);
            SetFixedHeight(view._statusText.GetComponent<LayoutElement>(), 40);

            // --- Root tabs: Craft | Upgrade ---
            var rootTabRowGo = CreateTabRowContainer(go.transform, "RootTabRow");
            var rootLabels = new[] { "Craft", "Upgrade" };
            for (var i = 0; i < rootLabels.Length; i++)
            {
                var index = i;
                CreateTabButton(rootTabRowGo.transform, $"{rootLabels[i]}Tab", rootLabels[i],
                    () => view.SelectRootTab(index), out var tabImage);
                view._rootTabButtonImages.Add(tabImage);
            }

            var craftPanelGo = CreateSubPanelContainer(go.transform, "CraftPanel");
            view._rootPanelGos.Add(craftPanelGo);

            var upgradePanelGo = CreateSubPanelContainer(go.transform, "UpgradePanel");
            view._rootPanelGos.Add(upgradePanelGo);

            // --- Craft: the original category tab strip + recipe lists, unchanged ---
            var categoryTabRowGo = CreateTabRowContainer(craftPanelGo.transform, "CategoryTabRow");
            for (var i = 0; i < Categories.Length; i++)
            {
                var index = i;
                CreateTabButton(categoryTabRowGo.transform, $"{Categories[i].Label}Tab", Categories[i].Label,
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
                var panelGo = CreateScrollPanel(craftPanelGo.transform, $"{label}Panel", panelHeight, out var scrollContent);
                view._listContainers.Add(scrollContent);
                view._tabPanelGos.Add(panelGo);
            }

            // --- Upgrade: Empower | Reroll sub-tabs, each a scrollable owned-weapon list ---
            var upgradeSubTabRowGo = CreateTabRowContainer(upgradePanelGo.transform, "UpgradeSubTabRow");
            for (var i = 0; i < UpgradeTabLabels.Length; i++)
            {
                var index = i;
                CreateTabButton(upgradeSubTabRowGo.transform, $"{UpgradeTabLabels[i]}Tab", UpgradeTabLabels[i],
                    () => view.SelectUpgradeSubTab(index), out var tabImage);
                view._upgradeTabButtonImages.Add(tabImage);
            }

            foreach (var label in UpgradeTabLabels)
            {
                var panelGo = CreateScrollPanel(upgradePanelGo.transform, $"{label}Panel", panelHeight, out var scrollContent);
                view._upgradeListContainers.Add(scrollContent);
                view._upgradeTabPanelGos.Add(panelGo);
            }

            view._confirmDialogGo = CreateConfirmDialog((RectTransform)go.transform, view);

            view.SelectRootTab(0);
            view.SelectCategoryTab(0);
            view.SelectUpgradeSubTab(0);

            return view;
        }

        // Shared "row of tab buttons" setup — the root tabs, Craft's category tabs, and
        // Upgrade's Empower/Reroll tabs all need the exact same HorizontalLayoutGroup +
        // explicit-height container. All three LayoutElement sizes explicit (not just
        // preferredHeight): this GameObject's own HorizontalLayoutGroup is itself an
        // ILayoutElement tying with this LayoutElement on layoutPriority, and an unset
        // minHeight/flexibleHeight here lets the group's own (much larger) computed
        // value win the tie — same bug/fix as FeedCardView's TopRow and
        // CombatPlaybackView's outer layout.
        private static GameObject CreateTabRowContainer(Transform parent, string name)
        {
            var tabRowGo = new GameObject(name, typeof(RectTransform));
            tabRowGo.transform.SetParent(parent, false);
            SetFixedHeight(tabRowGo.AddComponent<LayoutElement>(), 88);
            var tabRowLayout = tabRowGo.AddComponent<HorizontalLayoutGroup>();
            tabRowLayout.spacing = 10;
            tabRowLayout.childForceExpandWidth = true;
            tabRowLayout.childForceExpandHeight = true;
            tabRowLayout.childControlWidth = true;
            tabRowLayout.childControlHeight = true;
            return tabRowGo;
        }

        // A root-level tab's own content container (Craft/Upgrade) — just a plain
        // VerticalLayoutGroup so its own tab row + panels lay out the same way the
        // Blacksmith's own root already does.
        private static GameObject CreateSubPanelContainer(Transform parent, string name)
        {
            var panelGo = new GameObject(name, typeof(RectTransform));
            panelGo.transform.SetParent(parent, false);
            var panelLayout = panelGo.AddComponent<VerticalLayoutGroup>();
            panelLayout.spacing = 14;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            return panelGo;
        }

        // One fixed-height scrollable panel — shared by Craft's 4 category panels and
        // Upgrade's 2 sub-tab panels.
        private static GameObject CreateScrollPanel(Transform parent, string name, float height, out RectTransform scrollContent)
        {
            var panelGo = new GameObject(name, typeof(RectTransform));
            panelGo.transform.SetParent(parent, false);
            SetFixedHeight(panelGo.AddComponent<LayoutElement>(), height);

            // ScrollAreaFactory's own result relies on its parent being a LayoutGroup
            // that respects childControlWidth/Height (see that file's doc comment) —
            // a bare LayoutElement on panelGo alone isn't enough, so it needs one of
            // its own driving its single ScrollArea child.
            var panelGroup = panelGo.AddComponent<VerticalLayoutGroup>();
            panelGroup.childForceExpandWidth = true;
            panelGroup.childForceExpandHeight = true;
            panelGroup.childControlWidth = true;
            panelGroup.childControlHeight = true;

            scrollContent = ScrollAreaFactory.Create(panelGo.transform, new RectOffset(0, 0, 0, 0), 10);
            return panelGo;
        }

        private void SelectRootTab(int index)
        {
            for (var i = 0; i < _rootPanelGos.Count; i++)
            {
                _rootPanelGos[i].SetActive(i == index);
                _rootTabButtonImages[i].color = i == index ? SelectedTabColor : UnselectedTabColor;
            }
        }

        private void SelectUpgradeSubTab(int index)
        {
            for (var i = 0; i < _upgradeTabPanelGos.Count; i++)
            {
                _upgradeTabPanelGos[i].SetActive(i == index);
                _upgradeTabButtonImages[i].color = i == index ? SelectedTabColor : UnselectedTabColor;
            }
        }

        // Called once per Village visit (see VillageView.Show). Recipes themselves are
        // only ever generated the first time this is called (see _recipesGenerated).
        // Every establishment page is created up front and lives side-by-side in the
        // Village's swipe pager (see VillageView.Show/Create) — none of them get Show()n
        // again on a page swipe — so without the loadout.Changed subscription below, a
        // material bought at the Market wouldn't unlock its recipe here until leaving and
        // re-entering the Village. loadout is one long-lived instance for the whole
        // session, so subscribing exactly once (guarded below, same pattern
        // VillageMarketView already uses for its Sell tab) keeps every row live while
        // browsing the Blacksmith.
        public void Show(PlayerLoadout loadout, PlayerWallet wallet, PlayerAttributes attributes)
        {
            if (_loadout == null)
            {
                loadout.Changed += RefreshAllRecipeRows;
                loadout.Changed += RebuildUpgradePanels;
            }

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
                    foreach (var recipe in RecipeCatalog.RecipesFor(Categories[i].Category))
                    {
                        CreateRecipeRow(_listContainers[i], recipe);
                    }
                }
            }

            RefreshAllRecipeRows();
            RebuildUpgradePanels();
        }

        private void RefreshAllRecipeRows()
        {
            foreach (var row in _recipeRows)
            {
                RefreshRecipeRow(row);
            }
        }

        private void RefreshRecipeRow((RecipeDefinition Recipe, Text CostText, Image RowBackground, Button RowButton) row)
        {
            var unlocked = _loadout.IsRecipeUnlocked(row.Recipe);
            row.CostText.text = unlocked
                ? $"{DiscountedGoldCost(row.Recipe)}g + {DescribeIngredients(row.Recipe)}"
                : DescribeLockHint(row.Recipe);
            row.RowBackground.color = unlocked ? UnlockedRowColor : LockedRowColor;
            row.RowButton.interactable = unlocked;
        }

        // "2 Runed Iron Ore, 1 Runed Wood" — or "no materials" for a gold-only recipe (the
        // Materials tab's own recipes, which craft a raw material for gold alone) or an
        // unconfigured Empower/Reroll tier. Every ingredient in one list shares the same
        // rarity — whatever's being crafted/upgraded at (see PlayerLoadout's
        // material-counting methods) — so it's shown once per line, not per ingredient.
        private static string DescribeIngredients(RecipeIngredient[] ingredients, ItemRarity rarity)
        {
            return ingredients is { Length: > 0 }
                ? string.Join(", ", ingredients.Select(i => $"{i.count} {rarity} {i.material.materialName}"))
                : "no materials";
        }

        private static string DescribeIngredients(RecipeDefinition recipe) =>
            DescribeIngredients(recipe.ingredients, recipe.OutputRarity);

        // What's still missing before a locked row becomes craftable — every
        // undiscovered ingredient and every un-crafted prerequisite, joined together.
        private string DescribeLockHint(RecipeDefinition recipe)
        {
            var parts = new List<string>();

            var missingMaterials = (recipe.ingredients ?? Array.Empty<RecipeIngredient>())
                .Where(i => i.material != null && !_loadout.HasDiscovered(i.material, recipe.OutputRarity))
                .Select(i => $"{recipe.OutputRarity} {i.material.materialName}")
                .ToList();
            if (missingMaterials.Count > 0)
            {
                parts.Add($"discover {string.Join(", ", missingMaterials)}");
            }

            var missingPrereqs = (recipe.prerequisiteRecipes ?? Array.Empty<RecipeDefinition>())
                .Where(p => p != null && !_loadout.HasCrafted(p))
                .Select(p => p.OutputTitle)
                .ToList();
            if (missingPrereqs.Count > 0)
            {
                parts.Add($"craft {string.Join(", ", missingPrereqs)} first");
            }

            return parts.Count > 0 ? $"Requires: {string.Join("; ", parts)}" : "Locked";
        }

        private void SelectCategoryTab(int index)
        {
            for (var i = 0; i < Categories.Length; i++)
            {
                _tabPanelGos[i].SetActive(i == index);
                _tabButtonImages[i].color = i == index ? SelectedTabColor : UnselectedTabColor;
            }
        }

        // Unlike recipe rows (created once, refreshed in place — safe only because the
        // recipe *list* itself never changes), the owned-weapon list can grow/shrink at
        // any time, so both Upgrade sub-tabs rebuild from scratch on every loadout
        // change — same destroy-and-recreate pattern as VillageMarketView.RebuildSellPanel.
        private void RebuildUpgradePanels()
        {
            RebuildWeaponUpgradeList(_upgradeListContainers[0], isEmpower: true);
            RebuildWeaponUpgradeList(_upgradeListContainers[1], isEmpower: false);
        }

        private void RebuildWeaponUpgradeList(Transform container, bool isEmpower)
        {
            for (var i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }

            var weapons = _loadout.GetAllInventory().Where(item => item.category == ItemCategory.Weapon).ToList();
            if (weapons.Count == 0)
            {
                CreateLabel(container, "Empty", "No weapons owned yet.", 24, FontStyle.Italic);
                return;
            }

            foreach (var weapon in weapons)
            {
                if (isEmpower)
                {
                    CreateEmpowerRow(container, weapon);
                }
                else
                {
                    CreateRerollRow(container, weapon);
                }
            }
        }

        private void CreateEmpowerRow(Transform parent, ItemData weapon)
        {
            var canEmpower = WeaponUpgrader.CanEmpower(weapon);
            var costLabel = canEmpower
                ? $"{DiscountedGoldCost(WeaponUpgrader.EmpowerGoldCost(weapon))}g + {DescribeIngredients(WeaponUpgrader.EmpowerIngredients(weapon), weapon.rarity)}  " +
                  $"({weapon.rarity} → {weapon.rarity + 1})"
                : "Already at max rarity";
            CreateWeaponUpgradeRow(parent, weapon, canEmpower, costLabel, () => OnEmpowerTapped(weapon));
        }

        private void CreateRerollRow(Transform parent, ItemData weapon)
        {
            var costLabel = $"{DiscountedGoldCost(WeaponUpgrader.RerollGoldCost(weapon))}g + {DescribeIngredients(WeaponUpgrader.RerollIngredients(), weapon.rarity)}  " +
                             $"(reroll {weapon.rarity} skills)";
            CreateWeaponUpgradeRow(parent, weapon, true, costLabel, () => OnRerollTapped(weapon));
        }

        // Same visual shape as CreateRecipeRow (icon/title/cost, tap-to-confirm), kept
        // separate rather than unified with it since upgrade rows are fully recreated on
        // every rebuild (no need to track/refresh them in place afterward the way recipe
        // rows do).
        // A small 2-line vertical stack (title over a smaller italic stat line) — the
        // same idea as GearRowFactory's own text column, inlined here since these rows
        // aren't built through GearRowFactory (a different overall shape: icon + name +
        // trailing cost, no caption line). Shared by CreateRecipeRow and
        // CreateWeaponUpgradeRow, which differ only in what they pass as `stat`.
        private static void CreateNameColumn(Transform parent, string title, string stat)
        {
            var columnGo = new GameObject("NameColumn", typeof(RectTransform));
            columnGo.transform.SetParent(parent, false);
            var columnLayoutElement = columnGo.AddComponent<LayoutElement>();
            columnLayoutElement.minWidth = 0;
            columnLayoutElement.preferredWidth = 0;
            columnLayoutElement.flexibleWidth = 1;
            columnLayoutElement.flexibleHeight = 0;

            var columnGroup = columnGo.AddComponent<VerticalLayoutGroup>();
            columnGroup.childAlignment = TextAnchor.MiddleLeft;
            columnGroup.childForceExpandWidth = true;
            columnGroup.childForceExpandHeight = false;
            columnGroup.childControlWidth = true;
            columnGroup.childControlHeight = true;

            var nameText = CreateLabel(columnGo.transform, "Name", title, 28, FontStyle.Bold);
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.raycastTarget = false;

            var statText = CreateLabel(columnGo.transform, "Stat", stat, 22, FontStyle.Italic);
            statText.alignment = TextAnchor.MiddleLeft;
            statText.color = new Color(1f, 1f, 1f, 0.7f);
            statText.raycastTarget = false;
        }

        private void CreateWeaponUpgradeRow(Transform parent, ItemData weapon, bool interactable, string costLabel, Action onTapped)
        {
            var rowGo = new GameObject($"Upgrade_{weapon.title}", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            SetFixedHeight(rowGo.AddComponent<LayoutElement>(), RowHeight);
            var rowBackground = rowGo.AddComponent<Image>();
            rowBackground.color = interactable ? UnlockedRowColor : LockedRowColor;
            var button = rowGo.AddComponent<Button>();
            button.interactable = interactable;
            button.onClick.AddListener(() => onTapped());

            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 0, 0);
            rowLayout.spacing = 16;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            var (icon, border) = GearRowFactory.CreateIconFrame(rowGo.transform, 72);
            GearRowFactory.SetIcon(icon, weapon.icon, weapon.itemColor);
            GearRowFactory.SetRarityBorder(border, weapon.rarity);
            icon.raycastTarget = false;

            // Full detail including actual abilities — Empower/Reroll operate on real
            // owned instances, same "nothing to hide" treatment as a shop/inventory row.
            CreateNameColumn(rowGo.transform, weapon.title, weapon.WeaponDetailSummary);

            var costText = CreateLabel(rowGo.transform, "Cost", costLabel, 22, FontStyle.Italic);
            costText.alignment = TextAnchor.MiddleRight;
            costText.color = new Color(1f, 1f, 1f, 0.7f);
            costText.raycastTarget = false;
            var costLayout = costText.GetComponent<LayoutElement>();
            costLayout.minWidth = 260;
            costLayout.preferredWidth = 260;
            costLayout.flexibleWidth = 0;
        }

        private void OnEmpowerTapped(ItemData weapon)
        {
            if (!WeaponUpgrader.CanEmpower(weapon))
            {
                return;
            }

            var goldCost = DiscountedGoldCost(WeaponUpgrader.EmpowerGoldCost(weapon));
            var ingredients = WeaponUpgrader.EmpowerIngredients(weapon);
            var canAfford = _wallet.Gold >= goldCost && _loadout.HasIngredients(ingredients, weapon.rarity);

            _confirmMessageText.text = canAfford
                ? $"Empower {weapon.title} to {weapon.rarity + 1} for {goldCost}g + {DescribeIngredients(ingredients, weapon.rarity)}?"
                : $"Not enough gold or materials to empower {weapon.title}.";
            _confirmCraftButtonGo.SetActive(canAfford);
            _pendingConfirmAction = () =>
            {
                if (_loadout.TryEmpowerWeapon(weapon, _wallet, goldCost, ingredients))
                {
                    _statusText.text = $"Empowered {weapon.title}.";
                }
            };
            _confirmDialogGo.SetActive(true);
        }

        private void OnRerollTapped(ItemData weapon)
        {
            var goldCost = DiscountedGoldCost(WeaponUpgrader.RerollGoldCost(weapon));
            var ingredients = WeaponUpgrader.RerollIngredients();
            var canAfford = _wallet.Gold >= goldCost && _loadout.HasIngredients(ingredients, weapon.rarity);

            _confirmMessageText.text = canAfford
                ? $"Reroll {weapon.title}'s skills for {goldCost}g + {DescribeIngredients(ingredients, weapon.rarity)}?"
                : $"Not enough gold or materials to reroll {weapon.title}.";
            _confirmCraftButtonGo.SetActive(canAfford);
            _pendingConfirmAction = () =>
            {
                if (_loadout.TryRerollWeapon(weapon, _wallet, goldCost, ingredients))
                {
                    _statusText.text = $"Rerolled {weapon.title}.";
                }
            };
            _confirmDialogGo.SetActive(true);
        }

        // A custom row rather than GearRowFactory — icon far left, name taking the
        // remaining space, cost as its own trailing column on the right, per what was
        // asked for (GearRowFactory's stacked caption/title/stat shape doesn't match).
        // The whole row is one Button — tap anywhere on it to open the confirm dialog.
        private void CreateRecipeRow(Transform parent, RecipeDefinition recipe)
        {
            var rowGo = new GameObject($"Recipe_{recipe.OutputTitle}", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            SetFixedHeight(rowGo.AddComponent<LayoutElement>(), RowHeight);
            var rowBackground = rowGo.AddComponent<Image>();
            rowBackground.color = UnlockedRowColor;
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

            var (icon, border) = GearRowFactory.CreateIconFrame(rowGo.transform, 72);
            GearRowFactory.SetIcon(icon, recipe.OutputIcon, recipe.OutputColor);
            GearRowFactory.SetRarityBorder(border, recipe.OutputRarity);
            icon.raycastTarget = false;

            // Damage + speed, deliberately no abilities — nothing's been rolled yet for
            // a not-yet-crafted recipe (see OnRecipeTapped/DescribePossibleAbilities for
            // the tap-to-reveal possible pool instead).
            CreateNameColumn(rowGo.transform, recipe.OutputTitle, recipe.OutputStatSummary);

            var costText = CreateLabel(rowGo.transform, "Cost", "", 22, FontStyle.Italic);
            costText.alignment = TextAnchor.MiddleRight;
            costText.color = new Color(1f, 1f, 1f, 0.7f);
            costText.raycastTarget = false;
            var costLayout = costText.GetComponent<LayoutElement>();
            costLayout.minWidth = 260;
            costLayout.preferredWidth = 260;
            costLayout.flexibleWidth = 0;

            _recipeRows.Add((recipe, costText, rowBackground, button));
        }

        // _attributes.GoldCostMultiplier (Expertise-based) discounts every Blacksmith
        // price — recipes (whose goldCost is self-authored — see RecipeDefinition's own
        // doc comment for why) and Empower/Reroll (whose base cost is
        // weapon.goldValue-derived — see WeaponUpgrader) alike.
        private int DiscountedGoldCost(int baseCost) => Mathf.RoundToInt(baseCost * _attributes.GoldCostMultiplier);

        private int DiscountedGoldCost(RecipeDefinition recipe) => DiscountedGoldCost(recipe.goldCost);

        private void OnRecipeTapped(RecipeDefinition recipe)
        {
            if (!_loadout.IsRecipeUnlocked(recipe))
            {
                return;
            }

            var goldCost = DiscountedGoldCost(recipe);
            var canAfford = _wallet.Gold >= goldCost && _loadout.HasIngredientsFor(recipe);

            _confirmMessageText.text = (canAfford
                ? $"Craft {recipe.OutputTitle} for {goldCost}g + {DescribeIngredients(recipe)}?"
                : $"Not enough gold or materials to craft {recipe.OutputTitle}.") + DescribePossibleAbilities(recipe);
            _confirmCraftButtonGo.SetActive(canAfford);
            _pendingConfirmAction = () =>
            {
                if (_loadout.TryCraftRecipe(recipe, _wallet, goldCost))
                {
                    _statusText.text = $"Crafted {recipe.OutputTitle}.";
                    // Redundant with the loadout.Changed subscription (TryCraftRecipe
                    // already fired it), but keeps this success path self-contained/
                    // obviously correct without relying on event-timing.
                    RefreshAllRecipeRows();
                }
            };
            _confirmDialogGo.SetActive(true);
        }

        // Weapon-kind recipes haven't been rolled yet — abilities/passives are chosen at
        // craft time (WeaponGenerator.GenerateFromDefinition), so the row itself never
        // shows a specific list (see CreateNameColumn's stat argument in
        // CreateRecipeRow). Tapping the row still tells you what's *possible*, clearly
        // framed as such.
        private static string DescribePossibleAbilities(RecipeDefinition recipe)
        {
            if (recipe.outputKind != RecipeOutputKind.Weapon || recipe.outputWeapon == null)
            {
                return "";
            }

            var text = "";
            if (recipe.outputWeapon.possibleAbilities is { Length: > 0 } abilityPool)
            {
                var names = abilityPool.Where(a => a != null).Select(a => a.data.abilityName).ToList();
                if (names.Count > 0)
                {
                    text += $"\nPossible abilities: {string.Join(", ", names)}";
                }
            }

            if (recipe.outputWeapon.possiblePassives is { Length: > 0 } passivePool)
            {
                var names = passivePool.Where(p => p != null)
                    .Select(p => string.IsNullOrEmpty(p.effect.passiveName) ? p.effect.effectType.ToString() : p.effect.passiveName)
                    .ToList();
                if (names.Count > 0)
                {
                    text += $"\nPossible passives: {string.Join(", ", names)}";
                }
            }

            return text;
        }

        // The shared confirm dialog's single button handler — invokes whichever of
        // OnRecipeTapped/OnEmpowerTapped/OnRerollTapped last set _pendingConfirmAction.
        private void OnConfirmButtonTapped()
        {
            _pendingConfirmAction?.Invoke();
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
            boxRect.sizeDelta = new Vector2(800, 400);
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
            SetFixedHeight(view._confirmMessageText.GetComponent<LayoutElement>(), 190);

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
                new Color(0.2f, 0.55f, 0.3f, 0.7f), () => view.OnConfirmButtonTapped());

            overlayGo.SetActive(false);
            return overlayGo;
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
