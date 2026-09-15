using System.Collections.Generic;
using System.Linq;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using UnityEditor;
using UnityEngine;

namespace DoomScroller.Progression.Editor
{
    // Design-time tool for authoring equipment: a list of every WeaponDefinition (name,
    // type, damage type, and a per-rarity base-damage/attribute-scaling table) and every
    // Armor/Talisman EquipmentDefinition (name, slot, per-rarity power, effect pool),
    // each fully editable in place, plus Add buttons to create new ones from scratch.
    //
    // Edits go through direct field reads/writes (EditorGUI.BeginChangeCheck +
    // Undo.RecordObject + EditorUtility.SetDirty) rather than SerializedObject/
    // PropertyField for the array sections (statsByRarity, possibleAbilities/
    // possiblePassives, powerByRarity) — PropertyField's default array UI defaults its
    // foldout to *collapsed* and hides everything behind a manual "Size" field, which
    // reads as "can't edit this" with no visible affordance. Every rarity row and pool
    // entry below is always visible instead, with explicit Add/Remove controls. Purely a
    // dev tool — these definition assets aren't read by anything at runtime except
    // WeaponGenerator/EquipmentGenerator.GenerateFromDefinition, called on demand from
    // here to preview a roll.
    public class EquipmentMenuWindow : EditorWindow
    {
        private const string WeaponDefinitionsResourcesPath = "Assets/Resources/WeaponDefinitions";
        private const string EquipmentDefinitionsResourcesPath = "Assets/Resources/EquipmentDefinitions";
        private const string AbilitiesResourcesPath = "Assets/Resources/Abilities";
        private const string RecipeDefinitionsResourcesPath = "Assets/Resources/RecipeDefinitions";
        private const string ItemsResourcesPath = "Assets/Resources/Items";
        private const string WeaponUpgradeConfigAssetPath = "Assets/Resources/WeaponUpgradeConfig.asset";

        private Vector2 _scroll;
        private bool _showAddNew;
        private bool _showAbilityCatalog;

        private string _newName = "";
        private WeaponType _newType;
        private DamageType _newDamageType;
        private bool _newTwoHanded;
        private float _newAttackSpeed = 1f;

        private readonly Dictionary<WeaponDefinition, bool> _expanded = new();
        private readonly Dictionary<WeaponDefinition, ItemRarity> _previewRarity = new();
        private readonly Dictionary<WeaponDefinition, ItemData> _previewResult = new();
        private readonly System.Random _rng = new();

        // --- Armor / Talisman catalog state ---
        private class EquipmentDraft
        {
            public bool ShowAdd;
            public string Name = "";
            public EquipmentSlotType Slot;
            public Sprite Icon;
            public Color Color = Color.gray;
        }

        private readonly Dictionary<ItemCategory, EquipmentDraft> _equipmentDrafts = new()
        {
            [ItemCategory.Armor] = new EquipmentDraft(),
            [ItemCategory.Talisman] = new EquipmentDraft()
        };

        private readonly Dictionary<EquipmentDefinition, bool> _equipExpanded = new();
        private readonly Dictionary<EquipmentDefinition, ItemRarity> _equipPreviewRarity = new();
        private readonly Dictionary<EquipmentDefinition, ItemData> _equipPreviewResult = new();

        // --- Recipe catalog state ---
        private bool _showAddNewRecipe;

        private class RecipeOutputDraft
        {
            public RecipeOutputKind Kind;
            public ItemData Item;
            public WeaponDefinition Weapon;
            public EquipmentDefinition Equipment;
            public ItemRarity Rarity;
            public int GoldCost = 20;
        }

        private readonly RecipeOutputDraft _newRecipeDraft = new();
        private readonly Dictionary<RecipeDefinition, bool> _recipeExpanded = new();

        // --- Base Materials catalog state ---
        private class MaterialDraft
        {
            public bool ShowAdd;
            public string Name = "";
            public Sprite Icon;
            public Color Color = Color.gray;
            public int GoldValue = 5;
        }

        private readonly MaterialDraft _newMaterialDraft = new();
        private readonly Dictionary<ItemData, bool> _materialExpanded = new();

        // --- Weapon Upgrades (Empower/Reroll cost) state ---
        private bool _showWeaponUpgrades;

        // --- Ability / Passive catalog state ---
        private bool _showAddNewAbility;
        private bool _showAddNewPassive;
        private string _newAbilityName = "";
        private string _newPassiveName = "";
        private readonly HashSet<WeaponType> _newAbilityTypes = new();
        private readonly HashSet<WeaponType> _newPassiveTypes = new();

        private readonly Dictionary<AbilityDefinition, bool> _abilityExpanded = new();
        private readonly Dictionary<PassiveDefinition, bool> _passiveExpanded = new();

        // --- Cached asset lists ---
        // AssetDatabase.FindAssets + LoadAssetAtPath + an OrderBy/Where chain used to run
        // fresh in every one of the list-drawing methods below, unconditionally, on every
        // single OnGUI call — and IMGUI's OnGUI re-runs on every Layout pass, every
        // Repaint, and every mouse-move while the window is hovered (easily dozens of
        // times a second). That was the entire cause of this window feeling slow and
        // laggy to update. These five lists are now built once (OnEnable, i.e. on open
        // and after every recompile) and refreshed only when something could actually
        // change them: right after every Create*/Delete action below, or via the manual
        // Refresh button in OnGUI for changes made outside this window (e.g. deleting an
        // asset directly in the Project view).
        private List<WeaponDefinition> _weaponCache = new();
        private List<EquipmentDefinition> _equipmentCache = new();
        private List<RecipeDefinition> _recipeCache = new();
        private List<AbilityDefinition> _abilityCache = new();
        private List<PassiveDefinition> _passiveCache = new();
        private List<ItemData> _materialCache = new();

        private void OnEnable()
        {
            RefreshCaches();
        }

        private void RefreshCaches()
        {
            _weaponCache = AssetDatabase.FindAssets("t:WeaponDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(d => d != null)
                .OrderBy(d => d.weaponName)
                .ToList();

            _equipmentCache = AssetDatabase.FindAssets("t:EquipmentDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(d => d != null)
                .OrderBy(d => d.equipmentName)
                .ToList();

            _recipeCache = AssetDatabase.FindAssets("t:RecipeDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<RecipeDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(r => r != null)
                .OrderBy(r => r.OutputTitle)
                .ToList();

            _abilityCache = AssetDatabase.FindAssets("t:AbilityDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(a => a != null)
                .OrderBy(a => a.data.abilityName)
                .ToList();

            _passiveCache = AssetDatabase.FindAssets("t:PassiveDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<PassiveDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(p => p != null)
                .ToList();

            _materialCache = AssetDatabase.FindAssets("t:ItemData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(i => i != null && i.category == ItemCategory.Material)
                .OrderBy(i => i.title)
                .ToList();

            Repaint();
        }

        [MenuItem("Window/DoomScroller/Equipment Menu")]
        public static void Open()
        {
            GetWindow<EquipmentMenuWindow>("Equipment Menu");
        }

        private void OnGUI()
        {
            if (GUILayout.Button("↻ Refresh", GUILayout.Width(90)))
            {
                RefreshCaches();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawAddNewWeapon();
            EditorGUILayout.Space(10);
            DrawWeaponList();
            EditorGUILayout.Space(14);
            DrawEquipmentCategorySection(ItemCategory.Armor, "Armor");
            EditorGUILayout.Space(14);
            DrawEquipmentCategorySection(ItemCategory.Talisman, "Talismans");
            EditorGUILayout.Space(14);
            DrawMaterialSection();
            EditorGUILayout.Space(14);
            DrawRecipeSection();
            EditorGUILayout.Space(14);
            DrawWeaponUpgradeSection();
            EditorGUILayout.Space(14);
            DrawAbilityCatalogSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawAddNewWeapon()
        {
            _showAddNew = EditorGUILayout.Foldout(_showAddNew, "+ Add New Weapon", true, EditorStyles.foldoutHeader);
            if (!_showAddNew)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _newName = EditorGUILayout.TextField("Weapon Name", _newName);
                _newType = (WeaponType)EditorGUILayout.EnumPopup("Weapon Type", _newType);
                _newDamageType = (DamageType)EditorGUILayout.EnumPopup("Damage Type", _newDamageType);
                _newTwoHanded = EditorGUILayout.Toggle("Two-Handed", _newTwoHanded);
                _newAttackSpeed = EditorGUILayout.FloatField("Attack Speed", _newAttackSpeed);

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newName)))
                {
                    if (GUILayout.Button("Create"))
                    {
                        CreateWeaponDefinition();
                    }
                }
            }
        }

        private void CreateWeaponDefinition()
        {
            if (!AssetDatabase.IsValidFolder(WeaponDefinitionsResourcesPath))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "WeaponDefinitions");
            }

            var definition = CreateInstance<WeaponDefinition>();
            definition.weaponName = _newName;
            definition.weaponType = _newType;
            definition.damageType = _newDamageType;
            definition.isTwoHanded = _newTwoHanded;
            definition.attackSpeed = _newAttackSpeed;
            definition.statsByRarity = new[]
            {
                new WeaponRarityStats(), new WeaponRarityStats(), new WeaponRarityStats(), new WeaponRarityStats()
            };
            definition.possibleAbilities = System.Array.Empty<AbilityDefinition>();
            definition.possiblePassives = System.Array.Empty<PassiveDefinition>();

            var safeName = string.Concat(_newName.Where(c => !System.IO.Path.GetInvalidFileNameChars().Contains(c)));
            var path = AssetDatabase.GenerateUniqueAssetPath($"{WeaponDefinitionsResourcesPath}/{safeName}.asset");
            AssetDatabase.CreateAsset(definition, path);
            AssetDatabase.SaveAssets();

            _expanded[definition] = true;
            _newName = "";
            Selection.activeObject = definition;
            RefreshCaches();
        }

        private void DrawWeaponList()
        {
            EditorGUILayout.LabelField("Weapons", EditorStyles.boldLabel);

            if (_weaponCache.Count == 0)
            {
                EditorGUILayout.HelpBox("No weapons yet — use Add New Weapon above.", MessageType.None);
                return;
            }

            foreach (var definition in _weaponCache)
            {
                // Every WeaponDefinition must always carry exactly RarityCount rarity
                // rows — guard here so an asset created before this field existed (or a
                // rarity tier added since, like Mythic) never throws indexing below.
                NormalizeRarityStats(definition);
                DrawWeaponRow(definition);
            }
        }

        private static readonly int RarityCount = System.Enum.GetValues(typeof(ItemRarity)).Length;

        private static void NormalizeRarityStats(WeaponDefinition definition)
        {
            var stats = definition.statsByRarity?.ToList() ?? new List<WeaponRarityStats>();
            var changed = false;
            while (stats.Count < RarityCount)
            {
                stats.Add(new WeaponRarityStats());
                changed = true;
            }

            for (var i = 0; i < stats.Count; i++)
            {
                if (stats[i] == null)
                {
                    stats[i] = new WeaponRarityStats();
                    changed = true;
                }
            }

            if (changed)
            {
                definition.statsByRarity = stats.ToArray();
                EditorUtility.SetDirty(definition);
            }
        }

        private void DrawWeaponRow(WeaponDefinition definition)
        {
            _expanded.TryGetValue(definition, out var expanded);

            var damageSummary = string.Join("  ", System.Enum.GetValues(typeof(ItemRarity))
                .Cast<ItemRarity>()
                .Select(r => $"{r.ToString()[0]}:{definition.statsByRarity[(int)r].baseDamage}"));

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    expanded = EditorGUILayout.Foldout(expanded,
                        $"{definition.weaponName}  ({definition.weaponType}/{definition.damageType})  —  {damageSummary}",
                        true);
                    if (GUILayout.Button("Delete", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Weapon", $"Delete \"{definition.weaponName}\"? This can't be undone.",
                                "Delete", "Cancel"))
                        {
                            _expanded.Remove(definition);
                            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(definition));
                            RefreshCaches();
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                _expanded[definition] = expanded;
                if (!expanded)
                {
                    return;
                }

                DrawWeaponFields(definition);

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Base Damage & Attribute Scaling by Rarity", EditorStyles.boldLabel);
                foreach (ItemRarity rarity in System.Enum.GetValues(typeof(ItemRarity)))
                {
                    DrawRarityStatsRow(definition, rarity);
                }

                EditorGUILayout.Space(6);
                DrawAbilityPool(definition);
                EditorGUILayout.Space(4);
                DrawPassivePool(definition);

                DrawRollPreview(definition);
            }
        }

        private static void DrawWeaponFields(WeaponDefinition definition)
        {
            EditorGUI.BeginChangeCheck();
            var name = EditorGUILayout.TextField("Weapon Name", definition.weaponName);
            var type = (WeaponType)EditorGUILayout.EnumPopup("Weapon Type", definition.weaponType);
            var damageType = (DamageType)EditorGUILayout.EnumPopup("Damage Type", definition.damageType);
            var twoHanded = EditorGUILayout.Toggle("Two-Handed", definition.isTwoHanded);
            var attackSpeed = EditorGUILayout.FloatField("Attack Speed", definition.attackSpeed);
            var color = EditorGUILayout.ColorField("Item Color", definition.itemColor);
            var icon = (Sprite)EditorGUILayout.ObjectField("Icon", definition.icon, typeof(Sprite), false);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(definition, "Edit Weapon");
            definition.weaponName = name;
            definition.weaponType = type;
            definition.damageType = damageType;
            definition.isTwoHanded = twoHanded;
            definition.attackSpeed = attackSpeed;
            definition.itemColor = color;
            definition.icon = icon;
            EditorUtility.SetDirty(definition);
        }

        private static void DrawRarityStatsRow(WeaponDefinition definition, ItemRarity rarity)
        {
            var stats = definition.statsByRarity[(int)rarity];

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(rarity.ToString(), EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                var baseDamage = EditorGUILayout.IntField("Base Damage", stats.baseDamage);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(definition, "Edit Weapon Base Damage");
                    stats.baseDamage = baseDamage;
                    EditorUtility.SetDirty(definition);
                }

                var scaling = stats.attributeScalingEntries?.ToList() ?? new List<AttributeScaling>();
                var scalingChanged = false;

                for (var i = 0; i < scaling.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        var attribute = (PlayerAttribute)EditorGUILayout.EnumPopup(scaling[i].attribute, GUILayout.Width(140));
                        var coefficient = EditorGUILayout.FloatField(scaling[i].coefficient, GUILayout.Width(70));
                        if (EditorGUI.EndChangeCheck())
                        {
                            scaling[i].attribute = attribute;
                            scaling[i].coefficient = coefficient;
                            scalingChanged = true;
                        }

                        if (GUILayout.Button("x", GUILayout.Width(22)))
                        {
                            scaling.RemoveAt(i);
                            scalingChanged = true;
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                if (GUILayout.Button("+ Add Scaling Entry", GUILayout.Width(160)))
                {
                    scaling.Add(new AttributeScaling { attribute = PlayerAttribute.Constitution, coefficient = 0.5f });
                    scalingChanged = true;
                }

                if (scalingChanged)
                {
                    Undo.RecordObject(definition, "Edit Weapon Scaling");
                    stats.attributeScalingEntries = scaling.ToArray();
                    EditorUtility.SetDirty(definition);
                }
            }
        }

        private static void DrawAbilityPool(WeaponDefinition definition)
        {
            EditorGUILayout.LabelField("Ability Pool (rolled from, not guaranteed)", EditorStyles.boldLabel);

            var pool = definition.possibleAbilities?.ToList() ?? new List<AbilityDefinition>();
            for (var i = 0; i < pool.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(pool[i] != null ? pool[i].data.abilityName : "(missing)");
                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    {
                        Undo.RecordObject(definition, "Remove Ability");
                        pool.RemoveAt(i);
                        definition.possibleAbilities = pool.ToArray();
                        EditorUtility.SetDirty(definition);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button("+ Add Ability", GUILayout.Width(160)))
            {
                var menu = new GenericMenu();
                var all = AssetDatabase.FindAssets("t:AbilityDefinition")
                    .Select(guid => AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(a => a != null && !pool.Contains(a))
                    .OrderBy(a => a.data.abilityName)
                    .ToList();

                if (all.Count == 0)
                {
                    menu.AddDisabledItem(new GUIContent("(no more abilities available)"));
                }

                foreach (var ability in all)
                {
                    menu.AddItem(new GUIContent(ability.data.abilityName), false, () =>
                    {
                        Undo.RecordObject(definition, "Add Ability");
                        var updated = definition.possibleAbilities?.ToList() ?? new List<AbilityDefinition>();
                        updated.Add(ability);
                        definition.possibleAbilities = updated.ToArray();
                        EditorUtility.SetDirty(definition);
                    });
                }

                menu.ShowAsContext();
            }
        }

        private static void DrawPassivePool(WeaponDefinition definition)
        {
            EditorGUILayout.LabelField("Passive Pool (rolled from, not guaranteed)", EditorStyles.boldLabel);

            var pool = definition.possiblePassives?.ToList() ?? new List<PassiveDefinition>();
            for (var i = 0; i < pool.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(pool[i] != null ? PassiveLabel(pool[i]) : "(missing)");
                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    {
                        Undo.RecordObject(definition, "Remove Passive");
                        pool.RemoveAt(i);
                        definition.possiblePassives = pool.ToArray();
                        EditorUtility.SetDirty(definition);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button("+ Add Passive", GUILayout.Width(160)))
            {
                var menu = new GenericMenu();
                var all = AssetDatabase.FindAssets("t:PassiveDefinition")
                    .Select(guid => AssetDatabase.LoadAssetAtPath<PassiveDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(p => p != null && !pool.Contains(p))
                    .ToList();

                if (all.Count == 0)
                {
                    menu.AddDisabledItem(new GUIContent("(no more passives available)"));
                }

                foreach (var passive in all)
                {
                    menu.AddItem(new GUIContent(PassiveLabel(passive)), false, () =>
                    {
                        Undo.RecordObject(definition, "Add Passive");
                        var updated = definition.possiblePassives?.ToList() ?? new List<PassiveDefinition>();
                        updated.Add(passive);
                        definition.possiblePassives = updated.ToArray();
                        EditorUtility.SetDirty(definition);
                    });
                }

                menu.ShowAsContext();
            }
        }

        private void DrawRollPreview(WeaponDefinition definition)
        {
            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                _previewRarity.TryGetValue(definition, out var rarity);
                rarity = (ItemRarity)EditorGUILayout.EnumPopup("Preview Rarity", rarity);
                _previewRarity[definition] = rarity;

                if (GUILayout.Button("Roll Preview", GUILayout.Width(110)))
                {
                    _previewResult[definition] = WeaponGenerator.GenerateFromDefinition(definition, rarity, _rng);
                }
            }

            if (_previewResult.TryGetValue(definition, out var result) && result != null)
            {
                var scaling = result.attributeScalingEntries is { Length: > 0 }
                    ? string.Join(", ", result.attributeScalingEntries.Select(e => $"{e.attribute} ×{e.coefficient:0.00}"))
                    : "none";
                var abilities = result.abilities is { Length: > 0 }
                    ? string.Join(", ", result.abilities.Select(a => a.abilityName))
                    : "none";
                var passives = result.passiveEffects is { Length: > 0 }
                    ? string.Join(", ", result.passiveEffects.Select(PassivePreviewLabel))
                    : "none";

                EditorGUILayout.HelpBox(
                    $"Power {result.powerValue}   Scaling: {scaling}\nAbilities: {abilities}\nPassives: {passives}",
                    MessageType.None);
            }
        }

        // --- Armor / Talisman section (shared by both categories, filtered by ItemCategory) ---

        private void DrawEquipmentCategorySection(ItemCategory category, string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            DrawAddNewEquipment(category);
            EditorGUILayout.Space(6);

            var definitions = _equipmentCache.Where(d => d.category == category).ToList();

            if (definitions.Count == 0)
            {
                EditorGUILayout.HelpBox($"No {label.ToLowerInvariant()} yet — use Add New {label} above.", MessageType.None);
                return;
            }

            foreach (var definition in definitions)
            {
                // Guard against an asset created before powerByRarity had exactly
                // RarityCount entries — same normalize-before-index pattern as weapons'
                // statsByRarity.
                NormalizePowerByRarity(definition);
                DrawEquipmentRow(definition);
            }
        }

        private void DrawAddNewEquipment(ItemCategory category)
        {
            var draft = _equipmentDrafts[category];
            draft.ShowAdd = EditorGUILayout.Foldout(draft.ShowAdd, $"+ Add New {category}", true, EditorStyles.foldoutHeader);
            if (!draft.ShowAdd)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                draft.Name = EditorGUILayout.TextField("Name", draft.Name);

                var slots = ItemCategoryInfo.SlotsFor(category);
                var slotIndex = System.Array.IndexOf(slots, draft.Slot);
                if (slotIndex < 0)
                {
                    slotIndex = 0;
                }

                slotIndex = EditorGUILayout.Popup("Slot", slotIndex, slots.Select(ItemCategoryInfo.SlotLabel).ToArray());
                draft.Slot = slots.Length > 0 ? slots[slotIndex] : default;
                draft.Icon = (Sprite)EditorGUILayout.ObjectField("Icon", draft.Icon, typeof(Sprite), false);
                draft.Color = EditorGUILayout.ColorField("Item Color", draft.Color);

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(draft.Name)))
                {
                    if (GUILayout.Button("Create"))
                    {
                        CreateEquipmentDefinition(category, draft);
                    }
                }
            }
        }

        private void CreateEquipmentDefinition(ItemCategory category, EquipmentDraft draft)
        {
            if (!AssetDatabase.IsValidFolder(EquipmentDefinitionsResourcesPath))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "EquipmentDefinitions");
            }

            var definition = CreateInstance<EquipmentDefinition>();
            definition.equipmentName = draft.Name;
            definition.category = category;
            definition.slotType = draft.Slot;
            definition.icon = draft.Icon;
            definition.itemColor = draft.Color;
            definition.powerByRarity = new int[RarityCount];
            definition.possiblePassives = System.Array.Empty<PassiveDefinition>();

            var path = AssetDatabase.GenerateUniqueAssetPath($"{EquipmentDefinitionsResourcesPath}/{SanitizeFileName(draft.Name)}.asset");
            AssetDatabase.CreateAsset(definition, path);
            AssetDatabase.SaveAssets();

            _equipExpanded[definition] = true;
            draft.Name = "";
            draft.Icon = null;
            Selection.activeObject = definition;
            RefreshCaches();
        }

        private static void NormalizePowerByRarity(EquipmentDefinition definition)
        {
            var power = definition.powerByRarity?.ToList() ?? new List<int>();
            var changed = power.Count < RarityCount;
            while (power.Count < RarityCount)
            {
                power.Add(0);
            }

            if (changed)
            {
                definition.powerByRarity = power.ToArray();
                EditorUtility.SetDirty(definition);
            }
        }

        private void DrawEquipmentRow(EquipmentDefinition definition)
        {
            _equipExpanded.TryGetValue(definition, out var expanded);

            var powerSummary = string.Join("  ", System.Enum.GetValues(typeof(ItemRarity))
                .Cast<ItemRarity>()
                .Select(r => $"{r.ToString()[0]}:{definition.powerByRarity[(int)r]}"));

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    expanded = EditorGUILayout.Foldout(expanded,
                        $"{definition.equipmentName}  ({ItemCategoryInfo.SlotLabel(definition.slotType)})  —  {powerSummary}",
                        true);
                    if (GUILayout.Button("Delete", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Equipment", $"Delete \"{definition.equipmentName}\"? This can't be undone.",
                                "Delete", "Cancel"))
                        {
                            _equipExpanded.Remove(definition);
                            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(definition));
                            RefreshCaches();
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                _equipExpanded[definition] = expanded;
                if (!expanded)
                {
                    return;
                }

                DrawEquipmentFields(definition);

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Power by Rarity", EditorStyles.boldLabel);
                DrawPowerByRarity(definition);

                EditorGUILayout.Space(6);
                DrawEquipmentEffectPool(definition);

                DrawEquipmentRollPreview(definition);
            }
        }

        private static void DrawEquipmentFields(EquipmentDefinition definition)
        {
            EditorGUI.BeginChangeCheck();
            var name = EditorGUILayout.TextField("Name", definition.equipmentName);

            var slots = ItemCategoryInfo.SlotsFor(definition.category);
            var slotIndex = System.Array.IndexOf(slots, definition.slotType);
            if (slotIndex < 0)
            {
                slotIndex = 0;
            }

            var newSlotIndex = EditorGUILayout.Popup("Slot", slotIndex, slots.Select(ItemCategoryInfo.SlotLabel).ToArray());
            var slot = slots.Length > 0 ? slots[newSlotIndex] : definition.slotType;
            var icon = (Sprite)EditorGUILayout.ObjectField("Icon", definition.icon, typeof(Sprite), false);
            var color = EditorGUILayout.ColorField("Item Color", definition.itemColor);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(definition, "Edit Equipment");
            definition.equipmentName = name;
            definition.slotType = slot;
            definition.icon = icon;
            definition.itemColor = color;
            EditorUtility.SetDirty(definition);
        }

        private static void DrawPowerByRarity(EquipmentDefinition definition)
        {
            EditorGUI.BeginChangeCheck();
            var updated = new int[RarityCount];
            foreach (ItemRarity rarity in System.Enum.GetValues(typeof(ItemRarity)))
            {
                updated[(int)rarity] = EditorGUILayout.IntField(rarity.ToString(), definition.powerByRarity[(int)rarity]);
            }

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(definition, "Edit Equipment Power");
            definition.powerByRarity = updated;
            EditorUtility.SetDirty(definition);
        }

        // Same Add/Remove + GenericMenu picker pattern as the weapon ability/passive
        // pools, but unfiltered — armor/talismans aren't gated by allowedWeaponTypes
        // (a weapon-specific concept), so every authored PassiveDefinition is eligible.
        private static void DrawEquipmentEffectPool(EquipmentDefinition definition)
        {
            EditorGUILayout.LabelField("Effect Pool (rolled from, not guaranteed)", EditorStyles.boldLabel);

            var pool = definition.possiblePassives?.ToList() ?? new List<PassiveDefinition>();
            for (var i = 0; i < pool.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(pool[i] != null ? PassiveLabel(pool[i]) : "(missing)");
                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    {
                        Undo.RecordObject(definition, "Remove Effect");
                        pool.RemoveAt(i);
                        definition.possiblePassives = pool.ToArray();
                        EditorUtility.SetDirty(definition);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button("+ Add Effect", GUILayout.Width(160)))
            {
                var menu = new GenericMenu();
                var all = AssetDatabase.FindAssets("t:PassiveDefinition")
                    .Select(guid => AssetDatabase.LoadAssetAtPath<PassiveDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(p => p != null && !pool.Contains(p))
                    .ToList();

                if (all.Count == 0)
                {
                    menu.AddDisabledItem(new GUIContent("(no more effects available)"));
                }

                foreach (var passive in all)
                {
                    menu.AddItem(new GUIContent(PassiveLabel(passive)), false, () =>
                    {
                        Undo.RecordObject(definition, "Add Effect");
                        var updated = definition.possiblePassives?.ToList() ?? new List<PassiveDefinition>();
                        updated.Add(passive);
                        definition.possiblePassives = updated.ToArray();
                        EditorUtility.SetDirty(definition);
                    });
                }

                menu.ShowAsContext();
            }
        }

        private void DrawEquipmentRollPreview(EquipmentDefinition definition)
        {
            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                _equipPreviewRarity.TryGetValue(definition, out var rarity);
                rarity = (ItemRarity)EditorGUILayout.EnumPopup("Preview Rarity", rarity);
                _equipPreviewRarity[definition] = rarity;

                if (GUILayout.Button("Roll Preview", GUILayout.Width(110)))
                {
                    _equipPreviewResult[definition] = EquipmentGenerator.GenerateFromDefinition(definition, rarity, _rng);
                }
            }

            if (_equipPreviewResult.TryGetValue(definition, out var result) && result != null)
            {
                var passives = result.passiveEffects is { Length: > 0 }
                    ? string.Join(", ", result.passiveEffects.Select(PassivePreviewLabel))
                    : "none";
                EditorGUILayout.HelpBox($"Power {result.powerValue}   Effects: {passives}", MessageType.None);
            }
        }

        // --- Recipe section ---

        private void DrawRecipeSection()
        {
            EditorGUILayout.LabelField("Recipes", EditorStyles.boldLabel);

            DrawAddNewRecipe();
            EditorGUILayout.Space(6);

            if (_recipeCache.Count == 0)
            {
                EditorGUILayout.HelpBox("No recipes yet — use Add New Recipe above.", MessageType.None);
                return;
            }

            foreach (var recipe in _recipeCache)
            {
                DrawRecipeRow(recipe, _recipeCache);
            }
        }

        // Shared by the Add-New-Recipe draft form and each existing recipe's editor —
        // draws the output-kind selector and whichever fields are relevant for it, plus
        // goldCost. Returns whether anything changed so the caller can decide how to
        // persist it (direct field writes for a draft with no asset yet, vs.
        // Undo.RecordObject + SetDirty for a live RecipeDefinition).
        private static bool DrawRecipeOutputFields(RecipeOutputKind kind, ItemData item, WeaponDefinition weapon,
            EquipmentDefinition equipment, ItemRarity rarity, int goldCost,
            out RecipeOutputKind newKind, out ItemData newItem, out WeaponDefinition newWeapon,
            out EquipmentDefinition newEquipment, out ItemRarity newRarity, out int newGoldCost)
        {
            EditorGUI.BeginChangeCheck();
            newKind = (RecipeOutputKind)EditorGUILayout.EnumPopup("Output Kind", kind);
            newItem = item;
            newWeapon = weapon;
            newEquipment = equipment;
            newRarity = rarity;

            switch (newKind)
            {
                case RecipeOutputKind.Weapon:
                    newWeapon = (WeaponDefinition)EditorGUILayout.ObjectField("Weapon", weapon, typeof(WeaponDefinition), false);
                    newRarity = (ItemRarity)EditorGUILayout.EnumPopup("Rarity", rarity);
                    break;
                case RecipeOutputKind.Equipment:
                    newEquipment = (EquipmentDefinition)EditorGUILayout.ObjectField("Equipment", equipment, typeof(EquipmentDefinition), false);
                    newRarity = (ItemRarity)EditorGUILayout.EnumPopup("Rarity", rarity);
                    break;
                default:
                    newItem = (ItemData)EditorGUILayout.ObjectField("Output Item", item, typeof(ItemData), false);
                    break;
            }

            newGoldCost = EditorGUILayout.IntField("Gold Cost", goldCost);
            return EditorGUI.EndChangeCheck();
        }

        private void DrawAddNewRecipe()
        {
            _showAddNewRecipe = EditorGUILayout.Foldout(_showAddNewRecipe, "+ Add New Recipe", true, EditorStyles.foldoutHeader);
            if (!_showAddNewRecipe)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var draft = _newRecipeDraft;
                DrawRecipeOutputFields(draft.Kind, draft.Item, draft.Weapon, draft.Equipment, draft.Rarity, draft.GoldCost,
                    out draft.Kind, out draft.Item, out draft.Weapon, out draft.Equipment, out draft.Rarity, out draft.GoldCost);

                var hasValidOutput = draft.Kind switch
                {
                    RecipeOutputKind.Weapon => draft.Weapon != null,
                    RecipeOutputKind.Equipment => draft.Equipment != null,
                    _ => draft.Item != null
                };

                using (new EditorGUI.DisabledScope(!hasValidOutput))
                {
                    if (GUILayout.Button("Create"))
                    {
                        CreateRecipeDefinition(draft);
                    }
                }
            }
        }

        private void CreateRecipeDefinition(RecipeOutputDraft draft)
        {
            if (!AssetDatabase.IsValidFolder(RecipeDefinitionsResourcesPath))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "RecipeDefinitions");
            }

            var recipe = CreateInstance<RecipeDefinition>();
            recipe.recipeId = System.Guid.NewGuid().ToString();
            recipe.outputKind = draft.Kind;
            recipe.outputItem = draft.Item;
            recipe.outputWeapon = draft.Weapon;
            recipe.outputEquipment = draft.Equipment;
            recipe.outputRarity = draft.Rarity;
            recipe.goldCost = draft.GoldCost;
            recipe.ingredients = System.Array.Empty<RecipeIngredient>();
            recipe.prerequisiteRecipes = System.Array.Empty<RecipeDefinition>();

            var nameSlug = draft.Kind switch
            {
                RecipeOutputKind.Weapon => draft.Weapon != null ? draft.Weapon.weaponName : "Weapon",
                RecipeOutputKind.Equipment => draft.Equipment != null ? draft.Equipment.equipmentName : "Equipment",
                _ => draft.Item != null ? draft.Item.title : "Item"
            };

            var path = AssetDatabase.GenerateUniqueAssetPath(
                $"{RecipeDefinitionsResourcesPath}/Recipe_{SanitizeFileName(nameSlug)}.asset");
            AssetDatabase.CreateAsset(recipe, path);
            AssetDatabase.SaveAssets();

            _recipeExpanded[recipe] = true;
            draft.Kind = default;
            draft.Item = null;
            draft.Weapon = null;
            draft.Equipment = null;
            draft.Rarity = default;
            draft.GoldCost = 20;
            Selection.activeObject = recipe;
            RefreshCaches();
        }

        private void DrawRecipeRow(RecipeDefinition recipe, List<RecipeDefinition> allRecipes)
        {
            _recipeExpanded.TryGetValue(recipe, out var expanded);

            var ingredientSummary = recipe.ingredients is { Length: > 0 }
                ? string.Join(", ", recipe.ingredients.Select(i => i.material != null ? $"{i.count}x {i.material.title}" : "?"))
                : "no materials";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    expanded = EditorGUILayout.Foldout(expanded, $"{recipe.OutputTitle}  —  {ingredientSummary}", true);
                    if (GUILayout.Button("Delete", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Recipe", $"Delete the recipe for \"{recipe.OutputTitle}\"? This can't be undone.",
                                "Delete", "Cancel"))
                        {
                            _recipeExpanded.Remove(recipe);
                            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(recipe));
                            RefreshCaches();
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                _recipeExpanded[recipe] = expanded;
                if (!expanded)
                {
                    return;
                }

                if (DrawRecipeOutputFields(recipe.outputKind, recipe.outputItem, recipe.outputWeapon, recipe.outputEquipment,
                        recipe.outputRarity, recipe.goldCost,
                        out var outputKind, out var outputItem, out var outputWeapon, out var outputEquipment,
                        out var outputRarity, out var goldCost))
                {
                    Undo.RecordObject(recipe, "Edit Recipe Output");
                    recipe.outputKind = outputKind;
                    recipe.outputItem = outputItem;
                    recipe.outputWeapon = outputWeapon;
                    recipe.outputEquipment = outputEquipment;
                    recipe.outputRarity = outputRarity;
                    recipe.goldCost = goldCost;
                    EditorUtility.SetDirty(recipe);
                }

                EditorGUILayout.Space(4);
                DrawIngredientList(recipe);
                EditorGUILayout.Space(4);
                DrawPrerequisiteList(recipe, allRecipes);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Recipe ID", recipe.recipeId);
            }
        }

        // Add/Remove pattern matching the weapon ability/passive pools, but with a
        // per-entry count IntField (material + how many) instead of a bare reference —
        // the GenericMenu picker is filtered to Material-category items only, since an
        // ingredient that isn't a material would never be discoverable (see
        // PlayerLoadout.HasDiscovered).
        private static void DrawIngredientList(RecipeDefinition recipe)
        {
            EditorGUILayout.LabelField("Ingredients", EditorStyles.boldLabel);
            DrawIngredientPool(recipe, () => recipe.ingredients, updated => recipe.ingredients = updated);
        }

        // Same Add/Remove + GenericMenu pattern as the weapon ability/passive pools,
        // generalized (owner + get/apply delegates, same convention as
        // DrawStatusEffectList) so it serves both a RecipeDefinition's ingredients and
        // WeaponUpgradeConfig's tier/reroll ingredient lists without duplicating this
        // logic three times. get is re-invoked inside the "+ Add" GenericMenu's click
        // callback (not captured as a plain array up front) so it always writes onto
        // whatever the field's current value is by the time the click actually fires,
        // not a stale snapshot from the render frame that queued the menu.
        private static void DrawIngredientPool(UnityEngine.Object owner, System.Func<RecipeIngredient[]> get,
            System.Action<RecipeIngredient[]> apply)
        {
            var list = get()?.ToList() ?? new List<RecipeIngredient>();
            for (var i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var label = list[i].material != null ? list[i].material.title : "(missing)";
                    EditorGUILayout.LabelField(label);

                    EditorGUI.BeginChangeCheck();
                    var count = EditorGUILayout.IntField(list[i].count, GUILayout.Width(50));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(owner, "Edit Ingredient Count");
                        list[i].count = Mathf.Max(1, count);
                        apply(list.ToArray());
                        EditorUtility.SetDirty(owner);
                    }

                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    {
                        Undo.RecordObject(owner, "Remove Ingredient");
                        list.RemoveAt(i);
                        apply(list.ToArray());
                        EditorUtility.SetDirty(owner);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button("+ Add Ingredient", GUILayout.Width(160)))
            {
                var menu = new GenericMenu();
                var all = AssetDatabase.FindAssets("t:ItemData")
                    .Select(guid => AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(item => item != null && item.category == ItemCategory.Material
                                   && list.All(i => i.material != item))
                    .OrderBy(item => item.title)
                    .ToList();

                if (all.Count == 0)
                {
                    menu.AddDisabledItem(new GUIContent("(no more materials available)"));
                }

                foreach (var material in all)
                {
                    menu.AddItem(new GUIContent(material.title), false, () =>
                    {
                        Undo.RecordObject(owner, "Add Ingredient");
                        var updated = get()?.ToList() ?? new List<RecipeIngredient>();
                        updated.Add(new RecipeIngredient { material = material, count = 1 });
                        apply(updated.ToArray());
                        EditorUtility.SetDirty(owner);
                    });
                }

                menu.ShowAsContext();
            }
        }

        // Same Add/Remove + GenericMenu pattern, over other RecipeDefinition assets — the
        // "tree" edges. Excludes itself and anything that already names this recipe as
        // one of *its own* prerequisites (a direct one-hop cycle check; not full
        // transitive cycle detection, which isn't worth the complexity for a first pass).
        private static void DrawPrerequisiteList(RecipeDefinition recipe, List<RecipeDefinition> allRecipes)
        {
            EditorGUILayout.LabelField("Prerequisites (must be crafted first)", EditorStyles.boldLabel);

            var prereqs = recipe.prerequisiteRecipes?.ToList() ?? new List<RecipeDefinition>();
            for (var i = 0; i < prereqs.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var label = prereqs[i] != null && prereqs[i].outputItem != null ? prereqs[i].outputItem.title : "(missing)";
                    EditorGUILayout.LabelField(label);
                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    {
                        Undo.RecordObject(recipe, "Remove Prerequisite");
                        prereqs.RemoveAt(i);
                        recipe.prerequisiteRecipes = prereqs.ToArray();
                        EditorUtility.SetDirty(recipe);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button("+ Add Prerequisite", GUILayout.Width(160)))
            {
                var menu = new GenericMenu();
                var all = allRecipes
                    .Where(r => r != recipe && prereqs.All(p => p != r)
                                && (r.prerequisiteRecipes == null || !r.prerequisiteRecipes.Contains(recipe)))
                    .ToList();

                if (all.Count == 0)
                {
                    menu.AddDisabledItem(new GUIContent("(no eligible recipes)"));
                }

                foreach (var other in all)
                {
                    var label = other.outputItem != null ? other.outputItem.title : "(unnamed)";
                    menu.AddItem(new GUIContent(label), false, () =>
                    {
                        Undo.RecordObject(recipe, "Add Prerequisite");
                        var updated = recipe.prerequisiteRecipes?.ToList() ?? new List<RecipeDefinition>();
                        updated.Add(other);
                        recipe.prerequisiteRecipes = updated.ToArray();
                        EditorUtility.SetDirty(recipe);
                    });
                }

                menu.ShowAsContext();
            }
        }

        // --- Base Materials section ---

        private void DrawMaterialSection()
        {
            EditorGUILayout.LabelField("Base Materials", EditorStyles.boldLabel);

            DrawAddNewMaterial();
            EditorGUILayout.Space(6);

            if (_materialCache.Count == 0)
            {
                EditorGUILayout.HelpBox("No materials yet — use Add New Material above.", MessageType.None);
                return;
            }

            foreach (var material in _materialCache)
            {
                DrawMaterialRow(material);
            }
        }

        private void DrawAddNewMaterial()
        {
            _newMaterialDraft.ShowAdd = EditorGUILayout.Foldout(_newMaterialDraft.ShowAdd, "+ Add New Material", true, EditorStyles.foldoutHeader);
            if (!_newMaterialDraft.ShowAdd)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var draft = _newMaterialDraft;
                draft.Name = EditorGUILayout.TextField("Name", draft.Name);
                draft.Icon = (Sprite)EditorGUILayout.ObjectField("Icon", draft.Icon, typeof(Sprite), false);
                draft.Color = EditorGUILayout.ColorField("Item Color", draft.Color);
                draft.GoldValue = EditorGUILayout.IntField("Gold Value", draft.GoldValue);

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(draft.Name)))
                {
                    if (GUILayout.Button("Create"))
                    {
                        CreateMaterialItem(draft);
                    }
                }
            }
        }

        private void CreateMaterialItem(MaterialDraft draft)
        {
            if (!AssetDatabase.IsValidFolder(ItemsResourcesPath))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Items");
            }

            var material = CreateInstance<ItemData>();
            // Auto-assigned, unlike every material authored before this section existed
            // — closes the "must remember to set this by hand or it can't survive a
            // save/reload as a recipe/upgrade ingredient" gap (see ItemData.itemId's own
            // doc comment).
            material.itemId = System.Guid.NewGuid().ToString();
            material.title = draft.Name;
            material.category = ItemCategory.Material;
            material.icon = draft.Icon;
            material.itemColor = draft.Color;
            material.goldValue = draft.GoldValue;
            material.statSummary = "Crafting material";

            var path = AssetDatabase.GenerateUniqueAssetPath($"{ItemsResourcesPath}/Material_{SanitizeFileName(draft.Name)}.asset");
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            _materialExpanded[material] = true;
            draft.Name = "";
            draft.Icon = null;
            draft.GoldValue = 5;
            Selection.activeObject = material;
            RefreshCaches();
        }

        private void DrawMaterialRow(ItemData material)
        {
            _materialExpanded.TryGetValue(material, out var expanded);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    expanded = EditorGUILayout.Foldout(expanded, $"{material.title}  (sell {material.goldValue}g)", true);
                    if (GUILayout.Button("Delete", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Material", $"Delete \"{material.title}\"? This can't be undone.",
                                "Delete", "Cancel"))
                        {
                            _materialExpanded.Remove(material);
                            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(material));
                            RefreshCaches();
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                _materialExpanded[material] = expanded;
                if (!expanded)
                {
                    return;
                }

                EditorGUI.BeginChangeCheck();
                var name = EditorGUILayout.TextField("Name", material.title);
                var icon = (Sprite)EditorGUILayout.ObjectField("Icon", material.icon, typeof(Sprite), false);
                var color = EditorGUILayout.ColorField("Item Color", material.itemColor);
                var goldValue = EditorGUILayout.IntField("Gold Value", material.goldValue);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(material, "Edit Material");
                    material.title = name;
                    material.icon = icon;
                    material.itemColor = color;
                    material.goldValue = goldValue;
                    EditorUtility.SetDirty(material);
                }

                EditorGUILayout.LabelField("Item ID", material.itemId);
            }
        }

        // --- Weapon Upgrades (Empower/Reroll cost) section ---

        // Every consecutive rarity pair except the top tier (nothing empowers past the
        // max rarity) — generated from the enum itself rather than hardcoded, so adding
        // another rarity tier later doesn't require remembering to update this list too.
        private static readonly (ItemRarity From, ItemRarity To)[] EmpowerTiers = System.Enum.GetValues(typeof(ItemRarity))
            .Cast<ItemRarity>()
            .Where(r => (int)r < RarityCount - 1)
            .Select(r => (r, r + 1))
            .ToArray();

        private void DrawWeaponUpgradeSection()
        {
            _showWeaponUpgrades = EditorGUILayout.Foldout(_showWeaponUpgrades, "Weapon Upgrades (Empower / Reroll)", true, EditorStyles.foldoutHeader);
            if (!_showWeaponUpgrades)
            {
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<WeaponUpgradeConfig>(WeaponUpgradeConfigAssetPath);
            if (config == null)
            {
                EditorGUILayout.HelpBox("No Weapon Upgrade Config yet.", MessageType.None);
                if (GUILayout.Button("Create Weapon Upgrade Config", GUILayout.Width(220)))
                {
                    CreateWeaponUpgradeConfig();
                }

                return;
            }

            NormalizeUpgradeConfig(config);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Empower Materials by Tier", EditorStyles.boldLabel);
                foreach (var (from, to) in EmpowerTiers)
                {
                    EditorGUILayout.LabelField($"{from} → {to}");
                    var tier = config.empowerCostByRarity[(int)from];
                    DrawIngredientPool(config, () => tier.ingredients, updated => tier.ingredients = updated);
                    EditorGUILayout.Space(4);
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Reroll Materials (any rarity)", EditorStyles.boldLabel);
                DrawIngredientPool(config, () => config.rerollIngredients, updated => config.rerollIngredients = updated);
            }
        }

        private void CreateWeaponUpgradeConfig()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            var config = CreateInstance<WeaponUpgradeConfig>();
            config.empowerCostByRarity = new EmpowerTierCost[RarityCount];
            for (var i = 0; i < RarityCount; i++)
            {
                config.empowerCostByRarity[i] = new EmpowerTierCost();
            }

            config.rerollIngredients = System.Array.Empty<RecipeIngredient>();

            AssetDatabase.CreateAsset(config, WeaponUpgradeConfigAssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
        }

        // Guards against a config asset created before empowerCostByRarity had exactly
        // RarityCount entries (or a null entry within it) — same normalize-before-index
        // pattern as weapons' statsByRarity/equipment's powerByRarity.
        private static void NormalizeUpgradeConfig(WeaponUpgradeConfig config)
        {
            var tiers = config.empowerCostByRarity?.ToList() ?? new List<EmpowerTierCost>();
            var changed = tiers.Count < RarityCount;
            while (tiers.Count < RarityCount)
            {
                tiers.Add(new EmpowerTierCost());
            }

            for (var i = 0; i < tiers.Count; i++)
            {
                if (tiers[i] == null)
                {
                    tiers[i] = new EmpowerTierCost();
                    changed = true;
                }
            }

            if (changed)
            {
                config.empowerCostByRarity = tiers.ToArray();
                EditorUtility.SetDirty(config);
            }
        }

        private void DrawAbilityCatalogSection()
        {
            EditorGUILayout.LabelField("Abilities & Passives", EditorStyles.boldLabel);

            DrawAddNewAbility();
            EditorGUILayout.Space(6);
            DrawAddNewPassive();
            EditorGUILayout.Space(10);

            _showAbilityCatalog = EditorGUILayout.Foldout(_showAbilityCatalog, "Ability / Passive List", true, EditorStyles.foldoutHeader);
            if (!_showAbilityCatalog)
            {
                return;
            }

            if (_abilityCache.Count == 0 && _passiveCache.Count == 0)
            {
                EditorGUILayout.HelpBox("No abilities or passives yet — use the Add buttons above.", MessageType.None);
                return;
            }

            foreach (var ability in _abilityCache)
            {
                DrawAbilityRow(ability);
            }

            foreach (var passive in _passiveCache)
            {
                DrawPassiveRow(passive);
            }
        }

        private void DrawAddNewAbility()
        {
            _showAddNewAbility = EditorGUILayout.Foldout(_showAddNewAbility, "+ Add New Ability", true, EditorStyles.foldoutHeader);
            if (!_showAddNewAbility)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _newAbilityName = EditorGUILayout.TextField("Ability Name", _newAbilityName);
                DrawWeaponTypeToggleSet(_newAbilityTypes);

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newAbilityName)))
                {
                    if (GUILayout.Button("Create"))
                    {
                        CreateAbilityDefinition();
                    }
                }
            }
        }

        private void CreateAbilityDefinition()
        {
            if (!AssetDatabase.IsValidFolder(AbilitiesResourcesPath))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Abilities");
            }

            var ability = CreateInstance<AbilityDefinition>();
            ability.data = new WeaponAbilityData
            {
                abilityName = _newAbilityName,
                description = "",
                powerBonus = 5,
                cooldownSeconds = 3f,
                onCastEffects = System.Array.Empty<StatusEffectData>()
            };
            ability.allowedWeaponTypes = _newAbilityTypes.ToArray();

            var path = AssetDatabase.GenerateUniqueAssetPath($"{AbilitiesResourcesPath}/Ability_{SanitizeFileName(_newAbilityName)}.asset");
            AssetDatabase.CreateAsset(ability, path);
            AssetDatabase.SaveAssets();

            _abilityExpanded[ability] = true;
            _showAbilityCatalog = true;
            _newAbilityName = "";
            _newAbilityTypes.Clear();
            Selection.activeObject = ability;
            RefreshCaches();
        }

        private void DrawAddNewPassive()
        {
            _showAddNewPassive = EditorGUILayout.Foldout(_showAddNewPassive, "+ Add New Passive", true, EditorStyles.foldoutHeader);
            if (!_showAddNewPassive)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _newPassiveName = EditorGUILayout.TextField("Passive Name", _newPassiveName);
                EditorGUILayout.HelpBox("Effect type, magnitude, and scaling are filled in below after creation.", MessageType.None);
                DrawWeaponTypeToggleSet(_newPassiveTypes);

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newPassiveName)))
                {
                    if (GUILayout.Button("Create"))
                    {
                        CreatePassiveDefinition();
                    }
                }
            }
        }

        private void CreatePassiveDefinition()
        {
            if (!AssetDatabase.IsValidFolder(AbilitiesResourcesPath))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Abilities");
            }

            var passive = CreateInstance<PassiveDefinition>();
            passive.effect = new StatusEffectData
            {
                passiveName = _newPassiveName,
                description = "",
                effectType = StatusEffectType.EmpowerDamage,
                magnitude = 0.1f,
                durationSeconds = 5f,
                tickIntervalSeconds = 1f
            };
            passive.allowedWeaponTypes = _newPassiveTypes.ToArray();

            var path = AssetDatabase.GenerateUniqueAssetPath($"{AbilitiesResourcesPath}/Passive_{SanitizeFileName(_newPassiveName)}.asset");
            AssetDatabase.CreateAsset(passive, path);
            AssetDatabase.SaveAssets();

            _passiveExpanded[passive] = true;
            _showAbilityCatalog = true;
            _newPassiveName = "";
            _newPassiveTypes.Clear();
            Selection.activeObject = passive;
            RefreshCaches();
        }

        private void DrawAbilityRow(AbilityDefinition ability)
        {
            _abilityExpanded.TryGetValue(ability, out var expanded);
            var types = ability.allowedWeaponTypes ?? System.Array.Empty<WeaponType>();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    expanded = EditorGUILayout.Foldout(expanded,
                        $"⚔ {ability.data.abilityName}  (pwr {ability.data.powerBonus}, cd {ability.data.cooldownSeconds:0.#}s)  [{string.Join("/", types)}]",
                        true);
                    if (GUILayout.Button("Delete", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Ability", $"Delete \"{ability.data.abilityName}\"? This can't be undone.",
                                "Delete", "Cancel"))
                        {
                            _abilityExpanded.Remove(ability);
                            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(ability));
                            RefreshCaches();
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                _abilityExpanded[ability] = expanded;
                if (!expanded)
                {
                    return;
                }

                EditorGUI.BeginChangeCheck();
                var name = EditorGUILayout.TextField("Ability Name", ability.data.abilityName);
                var description = EditorGUILayout.TextField("Description", ability.data.description);
                var iconColor = EditorGUILayout.ColorField("Icon Color", ability.data.iconColor);
                var icon = (Sprite)EditorGUILayout.ObjectField("Icon", ability.data.icon, typeof(Sprite), false);
                var powerBonus = EditorGUILayout.IntField("Power Bonus", ability.data.powerBonus);
                var cooldown = EditorGUILayout.FloatField("Cooldown Seconds", ability.data.cooldownSeconds);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(ability, "Edit Ability");
                    ability.data.abilityName = name;
                    ability.data.description = description;
                    ability.data.iconColor = iconColor;
                    ability.data.icon = icon;
                    ability.data.powerBonus = powerBonus;
                    ability.data.cooldownSeconds = cooldown;
                    EditorUtility.SetDirty(ability);
                }

                DrawWeaponTypeToggles(ability, ability.allowedWeaponTypes, updated => ability.allowedWeaponTypes = updated);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("On-Cast Effects", EditorStyles.boldLabel);
                DrawStatusEffectList(ability, ability.data.onCastEffects, updated => ability.data.onCastEffects = updated);
            }
        }

        // Shared by every place a PassiveDefinition needs a one-line label — its own name
        // when authored, falling back to the raw effect shape for anything created
        // before naming existed.
        private static string PassiveLabel(PassiveDefinition passive)
        {
            if (!string.IsNullOrEmpty(passive.effect.passiveName))
            {
                return $"◆ {passive.effect.passiveName}";
            }

            return passive.effect.scalesWithAttribute
                ? $"◆ {passive.effect.effectType} (scales w/ {passive.effect.scalingAttribute})"
                : $"◆ {passive.effect.effectType} (flat, {passive.effect.magnitude:0.##})";
        }

        // For a rolled StatusEffectData (Roll Preview panels) rather than the catalog
        // asset that produced it — same naming fallback as PassiveLabel above.
        private static string PassivePreviewLabel(StatusEffectData effect)
        {
            return !string.IsNullOrEmpty(effect.passiveName)
                ? $"{effect.passiveName} ({effect.DescribePassive()})"
                : effect.DescribePassive();
        }

        private void DrawPassiveRow(PassiveDefinition passive)
        {
            _passiveExpanded.TryGetValue(passive, out var expanded);
            var types = passive.allowedWeaponTypes ?? System.Array.Empty<WeaponType>();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    expanded = EditorGUILayout.Foldout(expanded, $"{PassiveLabel(passive)}  [{string.Join("/", types)}]", true);
                    if (GUILayout.Button("Delete", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Passive", "Delete this passive? This can't be undone.",
                                "Delete", "Cancel"))
                        {
                            _passiveExpanded.Remove(passive);
                            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(passive));
                            RefreshCaches();
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                _passiveExpanded[passive] = expanded;
                if (!expanded)
                {
                    return;
                }

                EditorGUI.BeginChangeCheck();
                var name = EditorGUILayout.TextField("Passive Name", passive.effect.passiveName);
                var description = EditorGUILayout.TextField("Description", passive.effect.description);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(passive, "Edit Passive");
                    passive.effect.passiveName = name;
                    passive.effect.description = description;
                    EditorUtility.SetDirty(passive);
                }

                DrawStatusEffectFields(passive, passive.effect);
                DrawWeaponTypeToggles(passive, passive.allowedWeaponTypes, updated => passive.allowedWeaponTypes = updated);
            }
        }

        // Editable, always-expanded fields for one StatusEffectData — shared by a
        // PassiveDefinition's single effect and each entry in an AbilityDefinition's
        // onCastEffects list.
        private static void DrawStatusEffectFields(UnityEngine.Object owner, StatusEffectData effect)
        {
            EditorGUI.BeginChangeCheck();
            var effectType = (StatusEffectType)EditorGUILayout.EnumPopup("Effect Type", effect.effectType);
            var magnitude = EditorGUILayout.FloatField("Magnitude", effect.magnitude);
            var duration = EditorGUILayout.FloatField("Duration Seconds", effect.durationSeconds);
            var tickInterval = EditorGUILayout.FloatField("Tick Interval Seconds", effect.tickIntervalSeconds);
            var scales = EditorGUILayout.Toggle("Scales With Attribute", effect.scalesWithAttribute);
            var scalingAttribute = effect.scalingAttribute;
            var scalingCoefficient = effect.scalingCoefficient;
            if (scales)
            {
                scalingAttribute = (PlayerAttribute)EditorGUILayout.EnumPopup("Scaling Attribute", effect.scalingAttribute);
                scalingCoefficient = EditorGUILayout.FloatField("Scaling Coefficient", effect.scalingCoefficient);
            }

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(owner, "Edit Status Effect");
            effect.effectType = effectType;
            effect.magnitude = magnitude;
            effect.durationSeconds = duration;
            effect.tickIntervalSeconds = tickInterval;
            effect.scalesWithAttribute = scales;
            effect.scalingAttribute = scalingAttribute;
            effect.scalingCoefficient = scalingCoefficient;
            EditorUtility.SetDirty(owner);
        }

        // A growable list of StatusEffectData (an ability's onCastEffects) — each entry
        // editable in place via DrawStatusEffectFields, plus explicit Add/Remove controls.
        private static void DrawStatusEffectList(UnityEngine.Object owner, StatusEffectData[] effects,
            System.Action<StatusEffectData[]> apply)
        {
            var list = effects?.ToList() ?? new List<StatusEffectData>();

            for (var i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Effect {i + 1}", EditorStyles.boldLabel);
                        if (GUILayout.Button("Remove", GUILayout.Width(70)))
                        {
                            Undo.RecordObject(owner, "Remove Effect");
                            list.RemoveAt(i);
                            apply(list.ToArray());
                            EditorUtility.SetDirty(owner);
                            GUIUtility.ExitGUI();
                        }
                    }

                    DrawStatusEffectFields(owner, list[i]);
                }
            }

            if (GUILayout.Button("+ Add Effect", GUILayout.Width(140)))
            {
                Undo.RecordObject(owner, "Add Effect");
                list.Add(new StatusEffectData());
                apply(list.ToArray());
                EditorUtility.SetDirty(owner);
            }
        }

        // Editable toggle row for an existing asset's allowedWeaponTypes field.
        private static void DrawWeaponTypeToggles(UnityEngine.Object owner, WeaponType[] current,
            System.Action<WeaponType[]> apply)
        {
            var set = new HashSet<WeaponType>(current ?? System.Array.Empty<WeaponType>());
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Allowed Types", GUILayout.Width(100));
                foreach (WeaponType type in System.Enum.GetValues(typeof(WeaponType)))
                {
                    var has = set.Contains(type);
                    var newHas = EditorGUILayout.ToggleLeft(type.ToString(), has, GUILayout.Width(80));
                    if (newHas == has)
                    {
                        continue;
                    }

                    if (newHas)
                    {
                        set.Add(type);
                    }
                    else
                    {
                        set.Remove(type);
                    }

                    Undo.RecordObject(owner, "Edit Allowed Types");
                    apply(set.ToArray());
                    EditorUtility.SetDirty(owner);
                }
            }
        }

        // Same toggle row, but for a draft HashSet used by the Add New forms before an
        // asset even exists yet (no owner/Undo/dirty needed).
        private static void DrawWeaponTypeToggleSet(HashSet<WeaponType> set)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Allowed Types", GUILayout.Width(100));
                foreach (WeaponType type in System.Enum.GetValues(typeof(WeaponType)))
                {
                    var has = set.Contains(type);
                    var newHas = EditorGUILayout.ToggleLeft(type.ToString(), has, GUILayout.Width(80));
                    if (newHas && !has)
                    {
                        set.Add(type);
                    }
                    else if (!newHas && has)
                    {
                        set.Remove(type);
                    }
                }
            }
        }

        private static string SanitizeFileName(string name)
        {
            return string.Concat(name.Where(c => !System.IO.Path.GetInvalidFileNameChars().Contains(c)));
        }
    }
}
