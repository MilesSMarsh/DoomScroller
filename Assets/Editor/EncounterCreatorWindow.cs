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
                    DrawGoldRewardField(data);
                    DrawCombatStatFields(data);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Damage Weaknesses", EditorStyles.boldLabel);
                    DrawDamageTypeModifierList(data, () => data.damageTypeModifiers, t => data.damageTypeModifiers = t);
                    EditorGUILayout.Space(6);
                    DrawLootTable(data, data.lootTable, data.baseXpReward);
                });
            }
        }

        // --- Dungeon: an authored, ordered room list (DungeonGenerator turns it
        // straight into a run, no growth-formula scaling) plus a guaranteed end-of-run
        // reward, separate from anything any of the rooms themselves grant. ---

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
                            CreateEncounter(_newDungeonName, EncounterType.Dungeon, withLootTable: true);
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
                    EditorGUILayout.LabelField("Guaranteed End-of-Run Reward", EditorStyles.boldLabel);
                    DrawXpField(data);
                    DrawGoldRewardField(data);
                    DrawLootTable(data, data.lootTable, data.baseXpReward);

                    EditorGUILayout.Space(10);
                    DrawDungeonRoomList(data);
                });
            }
        }

        // One block per authored room — Type/Label/Appear Chance always shown, plus
        // whichever fields that type actually uses. Order matters here (unlike every
        // other pool in this window), so Up/Down replaces the usual unordered Add/Remove-
        // only pattern.
        private static void DrawDungeonRoomList(EncounterData data)
        {
            EditorGUILayout.LabelField("Rooms (in run order)", EditorStyles.boldLabel);

            var list = data.dungeonRooms?.ToList() ?? new List<DungeonRoomData>();
            for (var i = 0; i < list.Count; i++)
            {
                var room = list[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var headerLabel = string.IsNullOrEmpty(room.label) ? room.type.ToString() : room.label;
                        EditorGUILayout.LabelField($"{i + 1}. {headerLabel}", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();

                        using (new EditorGUI.DisabledScope(i == 0))
                        {
                            if (GUILayout.Button("▲", GUILayout.Width(28)))
                            {
                                Undo.RecordObject(data, "Move Room Up");
                                (list[i - 1], list[i]) = (list[i], list[i - 1]);
                                data.dungeonRooms = list.ToArray();
                                EditorUtility.SetDirty(data);
                                GUIUtility.ExitGUI();
                            }
                        }

                        using (new EditorGUI.DisabledScope(i == list.Count - 1))
                        {
                            if (GUILayout.Button("▼", GUILayout.Width(28)))
                            {
                                Undo.RecordObject(data, "Move Room Down");
                                (list[i + 1], list[i]) = (list[i], list[i + 1]);
                                data.dungeonRooms = list.ToArray();
                                EditorUtility.SetDirty(data);
                                GUIUtility.ExitGUI();
                            }
                        }

                        if (GUILayout.Button("Remove", GUILayout.Width(70)))
                        {
                            Undo.RecordObject(data, "Remove Room");
                            list.RemoveAt(i);
                            data.dungeonRooms = list.ToArray();
                            EditorUtility.SetDirty(data);
                            GUIUtility.ExitGUI();
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    var roomLabel = EditorGUILayout.TextField("Label", room.label);
                    var type = (DungeonRoomType)EditorGUILayout.EnumPopup("Type", room.type);
                    var chance = EditorGUILayout.Slider(
                        new GUIContent("Appear Chance %", "Independent chance this room is actually included the next time this dungeon is run."),
                        room.appearChance, 0f, 100f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(data, "Edit Room");
                        room.label = roomLabel;
                        room.type = type;
                        room.appearChance = chance;
                        EditorUtility.SetDirty(data);
                    }

                    if (room.type == DungeonRoomType.Fight)
                    {
                        EditorGUI.BeginChangeCheck();
                        var health = EditorGUILayout.IntField("Enemy Health", room.enemyHealth);
                        var damage = EditorGUILayout.IntField("Enemy Attack Damage", room.enemyAttackDamage);
                        var speed = EditorGUILayout.FloatField("Enemy Attack Speed", room.enemyAttackSpeed);
                        var sprite = (Sprite)EditorGUILayout.ObjectField(
                            new GUIContent("Enemy Sprite", "This room's own creature portrait, shown in the fight. Falls back to a placeholder color when unset."),
                            room.enemySprite, typeof(Sprite), false);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(data, "Edit Room Combat Stats");
                            room.enemyHealth = Mathf.Max(1, health);
                            room.enemyAttackDamage = Mathf.Max(0, damage);
                            room.enemyAttackSpeed = Mathf.Max(0.05f, speed);
                            room.enemySprite = sprite;
                            EditorUtility.SetDirty(data);
                        }

                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Damage Weaknesses (this room only)", EditorStyles.boldLabel);
                        DrawDamageTypeModifierList(data, () => room.damageTypeModifiers, t => room.damageTypeModifiers = t);
                    }
                    else if (room.type == DungeonRoomType.Treasure)
                    {
                        EditorGUI.BeginChangeCheck();
                        var gold = EditorGUILayout.IntField(
                            new GUIContent("Gold Reward", "A bonus pickup grants no XP, so 0 here means no gold either — never derived."),
                            room.goldReward);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(data, "Edit Room Gold Reward");
                            room.goldReward = Mathf.Max(0, gold);
                            EditorUtility.SetDirty(data);
                        }

                        DrawLootTable(data, room.lootTable, 0);
                    }
                }
            }

            if (GUILayout.Button("+ Add Room", GUILayout.Width(140)))
            {
                Undo.RecordObject(data, "Add Room");
                list.Add(new DungeonRoomData());
                data.dungeonRooms = list.ToArray();
                EditorUtility.SetDirty(data);
            }
        }

        // --- Resource (timed damage-dealing minigame, threshold rewards) ---

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
                    DrawGoldRewardField(data);
                    DrawLootTable(data, data.lootTable, data.baseXpReward);
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

        private static void DrawGoldRewardField(EncounterData data)
        {
            EditorGUI.BeginChangeCheck();
            var gold = EditorGUILayout.IntField(
                new GUIContent("Gold Reward", "0 = derive from Base XP Reward automatically (RewardGenerator.ComputeGoldReward)."),
                data.goldReward);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Edit Gold Reward");
                data.goldReward = Mathf.Max(0, gold);
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

        // get/apply (same convention as DrawLootTable/DrawIngredientPool) so this serves
        // both EncounterData's own weaknesses (Combat) and a single DungeonRoomData's
        // own weaknesses (a dungeon Fight room) without duplicating this logic.
        private static void DrawDamageTypeModifierList(UnityEngine.Object owner, System.Func<DamageTypeModifier[]> get,
            System.Action<DamageTypeModifier[]> apply)
        {
            var list = get()?.ToList() ?? new List<DamageTypeModifier>();
            for (var i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var type = (DamageType)EditorGUILayout.EnumPopup(list[i].type);
                    var multiplier = EditorGUILayout.FloatField(list[i].multiplier, GUILayout.Width(60));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(owner, "Edit Damage Weakness");
                        list[i].type = type;
                        list[i].multiplier = multiplier;
                        apply(list.ToArray());
                        EditorUtility.SetDirty(owner);
                    }

                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    {
                        Undo.RecordObject(owner, "Remove Damage Weakness");
                        list.RemoveAt(i);
                        apply(list.ToArray());
                        EditorUtility.SetDirty(owner);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button("+ Add Weakness", GUILayout.Width(150)))
            {
                Undo.RecordObject(owner, "Add Damage Weakness");
                list.Add(new DamageTypeModifier { type = DamageType.Slash, multiplier = 1f });
                apply(list.ToArray());
                EditorUtility.SetDirty(owner);
            }
        }

        private static void DrawResourceFields(EncounterData data)
        {
            EditorGUILayout.LabelField("Resource", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var timer = EditorGUILayout.FloatField(
                new GUIContent("Timer (seconds)", "How long the player's equipped weapon(s) auto-attack this " +
                    "node — it never attacks back. Total damage dealt in this window is checked against " +
                    "Reward Thresholds below."),
                data.resourceTimerSeconds);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Edit Resource Timer");
                data.resourceTimerSeconds = Mathf.Max(1f, timer);
                EditorUtility.SetDirty(data);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Weaknesses", EditorStyles.boldLabel);
            DrawDamageTypeModifierList(data, () => data.resourceWeaknesses, w => data.resourceWeaknesses = w);

            EditorGUILayout.Space(4);
            DrawResourceThresholdList(data);

            if (data.resourceThresholds == null || data.resourceThresholds.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No reward thresholds set — this card is excluded from the feed until at least one is " +
                    "added (see FeedController.LoadPool).", MessageType.Warning);
            }
        }

        // Add/Remove-row shape mirroring DrawDungeonRoomList — no reorder buttons, since
        // every threshold whose damageRequired is met grants its own reward regardless
        // of authoring order (they stack, not "highest tier only").
        private static void DrawResourceThresholdList(EncounterData data)
        {
            EditorGUILayout.LabelField("Reward Thresholds", EditorStyles.boldLabel);

            var list = data.resourceThresholds?.ToList() ?? new List<ResourceThreshold>();
            for (var i = 0; i < list.Count; i++)
            {
                var threshold = list[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Threshold {i + 1}", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Remove", GUILayout.Width(70)))
                        {
                            Undo.RecordObject(data, "Remove Threshold");
                            list.RemoveAt(i);
                            data.resourceThresholds = list.ToArray();
                            EditorUtility.SetDirty(data);
                            GUIUtility.ExitGUI();
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    var damageRequired = EditorGUILayout.IntField("Damage Required", threshold.damageRequired);
                    var rewardMaterial = (MaterialDefinition)EditorGUILayout.ObjectField("Reward Material",
                        threshold.rewardMaterial, typeof(MaterialDefinition), false);
                    var rewardRarity = (ItemRarity)EditorGUILayout.EnumPopup("Reward Rarity", threshold.rewardRarity);
                    var rewardAmount = EditorGUILayout.IntField("Reward Amount", threshold.rewardAmount);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(data, "Edit Threshold");
                        threshold.damageRequired = Mathf.Max(0, damageRequired);
                        threshold.rewardMaterial = rewardMaterial;
                        threshold.rewardRarity = rewardRarity;
                        threshold.rewardAmount = Mathf.Max(1, rewardAmount);
                        EditorUtility.SetDirty(data);
                    }
                }
            }

            if (GUILayout.Button("+ Add Threshold", GUILayout.Width(160)))
            {
                Undo.RecordObject(data, "Add Threshold");
                list.Add(new ResourceThreshold());
                data.resourceThresholds = list.ToArray();
                EditorUtility.SetDirty(data);
            }
        }

        // 5 fixed rarity rows (never a growable list — there are always exactly 5),
        // min/max item count, and two specific-template pools (weapons/equipment) —
        // shared by EncounterData's own table (Combat/Treasure/a dungeon's end-of-run
        // reward) and a DungeonRoomData's table (a mid-run bonus Treasure room). A
        // LootTable is always non-null once initialized (CreateEncounter/+Add Room both
        // do), so this mutates its fields in place rather than needing a get/apply pair
        // for the table object itself — same in-place convention as every other nested
        // Serializable field edit in this file.
        private static void DrawLootTable(UnityEngine.Object owner, LootTable table, int xpRewardForPreview)
        {
            EditorGUILayout.LabelField("Loot Table", EditorStyles.boldLabel);

            if (table.percentByRarity == null || table.percentByRarity.Length != 5)
            {
                Undo.RecordObject(owner, "Fix Loot Table");
                table.percentByRarity = new float[5];
                EditorUtility.SetDirty(owner);
            }

            EditorGUI.BeginChangeCheck();
            var percentByRarity = table.percentByRarity;
            for (var i = 0; i < 5; i++)
            {
                percentByRarity[i] = Mathf.Max(0f, EditorGUILayout.FloatField($"{RarityLabels[i]} %", percentByRarity[i]));
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(owner, "Edit Loot Table");
                EditorUtility.SetDirty(owner);
            }

            var total = percentByRarity.Sum();
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

            EditorGUI.BeginChangeCheck();
            var min = EditorGUILayout.IntField(
                new GUIContent("Min Items", "How many separate items one reward grants, at minimum."), table.minItemCount);
            var max = EditorGUILayout.IntField(
                new GUIContent("Max Items", "Must be >= Min Items."), table.maxItemCount);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(owner, "Edit Item Count");
                table.minItemCount = Mathf.Max(0, min);
                table.maxItemCount = Mathf.Max(table.minItemCount, max);
                EditorUtility.SetDirty(owner);
            }

            EditorGUILayout.Space(4);
            DrawDefinitionPool<WeaponDefinition>(owner, "t:WeaponDefinition", "Specific Weapons", "+ Add Weapon",
                () => table.weapons, w => table.weapons = w, w => w.weaponName);
            EditorGUILayout.Space(4);
            DrawDefinitionPool<EquipmentDefinition>(owner, "t:EquipmentDefinition", "Specific Equipment", "+ Add Equipment",
                () => table.equipment, e => table.equipment = e, e => e.equipmentName);
            EditorGUILayout.Space(4);
            DrawDefinitionPool<MaterialDefinition>(owner, "t:MaterialDefinition", "Specific Materials", "+ Add Material",
                () => table.materials, m => table.materials = m, m => m.materialName);

            EditorGUILayout.HelpBox(RewardGenerator.DescribeLootTable(table, xpRewardForPreview), MessageType.None);
        }

        // Generic Add/Remove + GenericMenu-picker pool, same shape as DrawIngredientPool/
        // DrawDamageTypeModifierList — parameterized so it serves a loot table's
        // WeaponDefinition pool, its EquipmentDefinition pool, and its
        // MaterialDefinition pool without duplicating this logic three times. get is
        // re-invoked inside the "+ Add" menu's click callback (not captured up front) so
        // it always writes onto the field's current value.
        private static void DrawDefinitionPool<T>(UnityEngine.Object owner, string assetTypeFilter, string label,
            string addButtonLabel, System.Func<T[]> get, System.Action<T[]> apply, System.Func<T, string> displayName)
            where T : UnityEngine.Object
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            var list = get()?.ToList() ?? new List<T>();
            for (var i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(list[i] != null ? displayName(list[i]) : "(missing)");
                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    {
                        Undo.RecordObject(owner, "Remove " + label);
                        list.RemoveAt(i);
                        apply(list.ToArray());
                        EditorUtility.SetDirty(owner);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button(addButtonLabel, GUILayout.Width(160)))
            {
                var menu = new GenericMenu();
                var all = AssetDatabase.FindAssets(assetTypeFilter)
                    .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(d => d != null && !list.Contains(d))
                    .ToList();

                if (all.Count == 0)
                {
                    menu.AddDisabledItem(new GUIContent("(none available)"));
                }

                foreach (var d in all)
                {
                    menu.AddItem(new GUIContent(displayName(d)), false, () =>
                    {
                        Undo.RecordObject(owner, "Add " + label);
                        var updated = get()?.ToList() ?? new List<T>();
                        updated.Add(d);
                        apply(updated.ToArray());
                        EditorUtility.SetDirty(owner);
                    });
                }

                menu.ShowAsContext();
            }
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
            data.resourceTimerSeconds = 10f;
            data.lootTable = new LootTable { percentByRarity = withLootTable ? new[] { 100f, 0f, 0f, 0f, 0f } : new float[5] };

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
