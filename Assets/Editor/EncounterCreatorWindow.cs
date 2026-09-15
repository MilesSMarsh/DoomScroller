using System.Collections.Generic;
using System.Linq;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using UnityEditor;
using UnityEngine;

namespace DoomScroller.Feed.Editor
{
    // Design-time tool for authoring feed encounters: Combat (Enemy + Ambush), Dungeon,
    // Resource, and Treasure — everything except Tavern (a hub, not a lootable card),
    // Wandering Merchant (procedurally-stocked, no authored content to speak of), and
    // Landing (a single fixed intro card), which stay on the plain asset Inspector.
    //
    // Same Add/expand/delete GUI shape as EquipmentMenuWindow — direct field reads/
    // writes via EditorGUI.BeginChangeCheck + Undo.RecordObject + EditorUtility.SetDirty
    // rather than SerializedObject/PropertyField, for the same reason (PropertyField's
    // default array UI hides growable lists behind a collapsed "Size" field).
    public class EncounterCreatorWindow : EditorWindow
    {
        private const string EncountersResourcesPath = "Assets/Resources/Encounters";
        private static readonly string[] RarityLabels = { "Common", "Uncommon", "Rare", "Legendary", "Mythic" };

        private Vector2 _scroll;

        private bool _showAddCombat;
        private bool _showAddDungeon;
        private bool _showAddResource;
        private bool _showAddTreasure;
        private string _newCombatName = "";
        private string _newDungeonName = "";
        private string _newResourceName = "";
        private string _newTreasureName = "";

        private readonly Dictionary<EncounterData, bool> _expanded = new();

        private List<EncounterData> _combatCache = new();
        private List<EncounterData> _dungeonCache = new();
        private List<EncounterData> _resourceCache = new();
        private List<EncounterData> _treasureCache = new();

        [MenuItem("Window/DoomScroller/Encounter Creator")]
        public static void Open()
        {
            GetWindow<EncounterCreatorWindow>("Encounter Creator");
        }

        private void OnEnable()
        {
            RefreshCaches();
        }

        private void RefreshCaches()
        {
            var all = AssetDatabase.FindAssets("t:EncounterData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<EncounterData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(e => e != null)
                .OrderBy(e => e.title)
                .ToList();

            _combatCache = all.Where(e => e.type == EncounterType.Enemy || e.type == EncounterType.Ambush).ToList();
            _dungeonCache = all.Where(e => e.type == EncounterType.Dungeon).ToList();
            _resourceCache = all.Where(e => e.type == EncounterType.Resource).ToList();
            _treasureCache = all.Where(e => e.type == EncounterType.Treasure).ToList();

            Repaint();
        }

        private void OnGUI()
        {
            if (GUILayout.Button("↻ Refresh", GUILayout.Width(90)))
            {
                RefreshCaches();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawCombatSection();
            EditorGUILayout.Space(14);
            DrawDungeonSection();
            EditorGUILayout.Space(14);
            DrawResourceSection();
            EditorGUILayout.Space(14);
            DrawTreasureSection();

            EditorGUILayout.EndScrollView();
        }

        // --- Combat (Enemy + Ambush share every field except the type itself) ---

        private void DrawCombatSection()
        {
            EditorGUILayout.LabelField("Combat", EditorStyles.boldLabel);

            _showAddCombat = EditorGUILayout.Foldout(_showAddCombat, "+ New Combat Encounter", true, EditorStyles.foldoutHeader);
            if (_showAddCombat)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    _newCombatName = EditorGUILayout.TextField("Title", _newCombatName);
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newCombatName)))
                    {
                        if (GUILayout.Button("Create"))
                        {
                            CreateEncounter(_newCombatName, EncounterType.Enemy, withLootTable: true);
                            _newCombatName = "";
                            _showAddCombat = false;
                        }
                    }
                }
            }

            EditorGUILayout.Space(6);
            foreach (var data in _combatCache)
            {
                DrawEncounterRow(data, data.type == EncounterType.Ambush ? "⚠" : "⚔", () =>
                {
                    EditorGUI.BeginChangeCheck();
                    var isAmbush = EditorGUILayout.Toggle(
                        new GUIContent("Unskippable (Ambush)", "Blocks scrolling past it in either direction until it's resolved."),
                        data.type == EncounterType.Ambush);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(data, "Edit Encounter Type");
                        data.type = isAmbush ? EncounterType.Ambush : EncounterType.Enemy;
                        EditorUtility.SetDirty(data);
                        RefreshCaches();
                        GUIUtility.ExitGUI();
                    }

                    EditorGUILayout.Space(4);
                    DrawXpField(data);
                    DrawCombatStatFields(data);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Damage Weaknesses", EditorStyles.boldLabel);
                    DrawDamageTypeModifierList(data);
                    EditorGUILayout.Space(6);
                    DrawLootTable(data);
                });
            }
        }

        // --- Dungeon (shared fields only — keeps its own hardcoded per-depth loot
        // progression, see DungeonGenerator, so no loot table section here) ---

        private void DrawDungeonSection()
        {
            EditorGUILayout.LabelField("Dungeon", EditorStyles.boldLabel);

            _showAddDungeon = EditorGUILayout.Foldout(_showAddDungeon, "+ New Dungeon", true, EditorStyles.foldoutHeader);
            if (_showAddDungeon)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    _newDungeonName = EditorGUILayout.TextField("Title", _newDungeonName);
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newDungeonName)))
                    {
                        if (GUILayout.Button("Create"))
                        {
                            CreateEncounter(_newDungeonName, EncounterType.Dungeon, withLootTable: false);
                            _newDungeonName = "";
                            _showAddDungeon = false;
                        }
                    }
                }
            }

            EditorGUILayout.Space(6);
            foreach (var data in _dungeonCache)
            {
                DrawEncounterRow(data, "🚪", () =>
                {
                    DrawXpField(data);
                    DrawCombatStatFields(data);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Damage Weaknesses", EditorStyles.boldLabel);
                    DrawDamageTypeModifierList(data);
                    EditorGUILayout.HelpBox(
                        "Dungeons keep their own fixed Common → Rare-by-depth progression " +
                        "(see DungeonGenerator) — no loot table here.", MessageType.None);
                });
            }
        }

        // --- Resource (fixed material grant, no rarity roll at all) ---

        private void DrawResourceSection()
        {
            EditorGUILayout.LabelField("Resource", EditorStyles.boldLabel);

            _showAddResource = EditorGUILayout.Foldout(_showAddResource, "+ New Resource Encounter", true, EditorStyles.foldoutHeader);
            if (_showAddResource)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    _newResourceName = EditorGUILayout.TextField("Title", _newResourceName);
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newResourceName)))
                    {
                        if (GUILayout.Button("Create"))
                        {
                            CreateEncounter(_newResourceName, EncounterType.Resource, withLootTable: false);
                            _newResourceName = "";
                            _showAddResource = false;
                        }
                    }
                }
            }

            EditorGUILayout.Space(6);
            foreach (var data in _resourceCache)
            {
                DrawEncounterRow(data, "🌲", () => DrawResourceFields(data));
            }
        }

        // --- Treasure (reward chest, loot table drives the rarity roll) ---

        private void DrawTreasureSection()
        {
            EditorGUILayout.LabelField("Treasure", EditorStyles.boldLabel);

            _showAddTreasure = EditorGUILayout.Foldout(_showAddTreasure, "+ New Treasure Encounter", true, EditorStyles.foldoutHeader);
            if (_showAddTreasure)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    _newTreasureName = EditorGUILayout.TextField("Title", _newTreasureName);
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newTreasureName)))
                    {
                        if (GUILayout.Button("Create"))
                        {
                            CreateEncounter(_newTreasureName, EncounterType.Treasure, withLootTable: true);
                            _newTreasureName = "";
                            _showAddTreasure = false;
                        }
                    }
                }
            }

            EditorGUILayout.Space(6);
            foreach (var data in _treasureCache)
            {
                DrawEncounterRow(data, "💰", () =>
                {
                    DrawXpField(data);
                    DrawLootTable(data);
                });
            }
        }

        // --- Shared row shell: header foldout + Delete + identity fields, then whatever
        // type-specific body the caller provides. ---

        private void DrawEncounterRow(EncounterData data, string icon, System.Action drawBody)
        {
            _expanded.TryGetValue(data, out var expanded);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var header = $"{icon} {(string.IsNullOrEmpty(data.title) ? "(untitled)" : data.title)}";
                    expanded = EditorGUILayout.Foldout(expanded, header, true);
                    if (GUILayout.Button("Delete", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Encounter", $"Delete \"{data.title}\"? This can't be undone.",
                                "Delete", "Cancel"))
                        {
                            _expanded.Remove(data);
                            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(data));
                            RefreshCaches();
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                _expanded[data] = expanded;
                if (!expanded)
                {
                    return;
                }

                DrawIdentityFields(data);
                EditorGUILayout.Space(4);
                drawBody();
            }
        }

        private static void DrawIdentityFields(EncounterData data)
        {
            EditorGUI.BeginChangeCheck();
            var title = EditorGUILayout.TextField("Title", data.title);
            var description = EditorGUILayout.TextField("Description",
                data.description);
            var artwork = (Sprite)EditorGUILayout.ObjectField(
                new GUIContent("Background Art", "Full-bleed feed card art."), data.artwork, typeof(Sprite), false);
            var foregroundArt = (Sprite)EditorGUILayout.ObjectField(
                new GUIContent("Foreground Art", "The creature portrait (Combat/Dungeon) or chest art (Treasure) shown inside the encounter itself."),
                data.foregroundArt, typeof(Sprite), false);
            var cardColor = EditorGUILayout.ColorField("Fallback Color", data.cardColor);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Edit Encounter");
                data.title = title;
                data.description = description;
                data.artwork = artwork;
                data.foregroundArt = foregroundArt;
                data.cardColor = cardColor;
                EditorUtility.SetDirty(data);
            }
        }

        private static void DrawXpField(EncounterData data)
        {
            EditorGUI.BeginChangeCheck();
            var xp = EditorGUILayout.IntField("Base XP Reward", data.baseXpReward);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Edit XP Reward");
                data.baseXpReward = Mathf.Max(0, xp);
                EditorUtility.SetDirty(data);
            }
        }

        private static void DrawCombatStatFields(EncounterData data)
        {
            EditorGUILayout.LabelField("Combat Stats", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var health = EditorGUILayout.IntField("Enemy Health", data.enemyHealth);
            var damage = EditorGUILayout.IntField("Enemy Attack Damage", data.enemyAttackDamage);
            var speed = EditorGUILayout.FloatField("Enemy Attack Speed", data.enemyAttackSpeed);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Edit Combat Stats");
                data.enemyHealth = Mathf.Max(1, health);
                data.enemyAttackDamage = Mathf.Max(0, damage);
                data.enemyAttackSpeed = Mathf.Max(0.05f, speed);
                EditorUtility.SetDirty(data);
            }
        }

        private static void DrawDamageTypeModifierList(EncounterData data)
        {
            var list = data.damageTypeModifiers?.ToList() ?? new List<DamageTypeModifier>();
            for (var i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var type = (DamageType)EditorGUILayout.EnumPopup(list[i].type);
                    var multiplier = EditorGUILayout.FloatField(list[i].multiplier, GUILayout.Width(60));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(data, "Edit Damage Weakness");
                        list[i].type = type;
                        list[i].multiplier = multiplier;
                        data.damageTypeModifiers = list.ToArray();
                        EditorUtility.SetDirty(data);
                    }

                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    {
                        Undo.RecordObject(data, "Remove Damage Weakness");
                        list.RemoveAt(i);
                        data.damageTypeModifiers = list.ToArray();
                        EditorUtility.SetDirty(data);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button("+ Add Weakness", GUILayout.Width(150)))
            {
                Undo.RecordObject(data, "Add Damage Weakness");
                list.Add(new DamageTypeModifier { type = DamageType.Slash, multiplier = 1f });
                data.damageTypeModifiers = list.ToArray();
                EditorUtility.SetDirty(data);
            }
        }

        private static void DrawResourceFields(EncounterData data)
        {
            EditorGUILayout.LabelField("Resource", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var material = (ItemData)EditorGUILayout.ObjectField("Resource Material", data.resourceMaterial, typeof(ItemData), false);
            var yield = EditorGUILayout.IntField("Base Yield", data.baseResourceYield);
            var secondaryMaterial = (ItemData)EditorGUILayout.ObjectField("Secondary Material (optional)",
                data.secondaryResourceMaterial, typeof(ItemData), false);
            var secondaryChance = EditorGUILayout.Slider("Secondary Chance", data.secondaryResourceChance, 0f, 1f);
            var secondaryYield = EditorGUILayout.IntField("Secondary Yield", data.secondaryResourceYield);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Edit Resource Fields");
                data.resourceMaterial = material;
                data.baseResourceYield = Mathf.Max(1, yield);
                data.secondaryResourceMaterial = secondaryMaterial;
                data.secondaryResourceChance = secondaryChance;
                data.secondaryResourceYield = Mathf.Max(1, secondaryYield);
                EditorUtility.SetDirty(data);
            }

            if (material == null)
            {
                EditorGUILayout.HelpBox(
                    "No resource material set — this card is excluded from the feed until one is assigned " +
                    "(see FeedController.LoadPool).", MessageType.Warning);
            }
        }

        // 5 fixed rows (there are always exactly 5 rarities, never a growable list) —
        // doesn't need to sum to 100, RewardGenerator.RollRarity/DescribeLootTable both
        // normalize against whatever the total actually is, but a total that isn't ~100
        // is flagged in red since it's the likely-a-mistake case.
        private static void DrawLootTable(EncounterData data)
        {
            EditorGUILayout.LabelField("Loot Table", EditorStyles.boldLabel);

            var table = data.lootTablePercentByRarity;
            if (table == null || table.Length != 5)
            {
                table = new float[5];
            }

            EditorGUI.BeginChangeCheck();
            for (var i = 0; i < 5; i++)
            {
                table[i] = Mathf.Max(0f, EditorGUILayout.FloatField($"{RarityLabels[i]} %", table[i]));
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Edit Loot Table");
                data.lootTablePercentByRarity = table;
                EditorUtility.SetDirty(data);
            }

            var total = table.Sum();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Total", GUILayout.Width(EditorGUIUtility.labelWidth));
                var totalStyle = new GUIStyle(EditorStyles.label);
                if (total > 0f && Mathf.Abs(total - 100f) > 0.01f)
                {
                    totalStyle.normal.textColor = Color.red;
                }

                EditorGUILayout.LabelField($"{total:0.#}%", totalStyle);
            }

            EditorGUILayout.HelpBox(RewardGenerator.DescribeLootTable(table, data.baseXpReward), MessageType.None);
        }

        private void CreateEncounter(string title, EncounterType type, bool withLootTable)
        {
            if (!AssetDatabase.IsValidFolder(EncountersResourcesPath))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Encounters");
            }

            var data = CreateInstance<EncounterData>();
            data.title = title;
            data.type = type;
            data.cardColor = Color.gray;
            data.baseXpReward = 50;
            data.enemyHealth = 80;
            data.enemyAttackDamage = 10;
            data.enemyAttackSpeed = 0.8f;
            data.baseResourceYield = 5;
            data.secondaryResourceChance = 0.1f;
            data.secondaryResourceYield = 1;
            data.lootTablePercentByRarity = withLootTable ? new[] { 100f, 0f, 0f, 0f, 0f } : new float[5];

            var path = AssetDatabase.GenerateUniqueAssetPath($"{EncountersResourcesPath}/{SanitizeFileName(title)}.asset");
            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();

            _expanded[data] = true;
            Selection.activeObject = data;
            RefreshCaches();
        }

        private static string SanitizeFileName(string name)
        {
            var sanitized = name;
            foreach (var invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalid, '_');
            }

            return sanitized;
        }
    }
}
