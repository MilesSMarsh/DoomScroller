# Feature (from the GDD): Quest Board daily missions

## GDD section
### Day to day

The player returns for daily missions and long-term power.

- **Daily missions** on the Quest Board (planned) give bonus rewards for goals such as
  clearing a dungeon or defeating a number of enemies.
- Each session builds toward the next region, unlocked by beating that region's solo
  boss.
- Max HP regenerates while the app is closed, so a day's losses are recovered by the
  next session.

## Project conventions
# Unity Conventions

- Namespace scripts `DoomScroller.<System>` matching their folder under
  `Assets/Scripts/` (Combat, Encounters, Feed, Input, Inventory, Network, Progression, Social, UI).
- Keep MonoBehaviours thin. Logic lives in plain C# classes/services so it can be tested
  in EditMode without a scene. Randomness takes an injected `System.Random` so tests
  are deterministic.
- Authored content (encounters, weapons, abilities, materials, recipes) is
  ScriptableObject data under `Assets/Resources/`, loaded by data — never hardcode a
  catalog in code. Add content without new UI code.
- Weapons: authored `WeaponDefinition`s hold a *pool* of possible abilities; generation
  rolls a rarity-sized subset each time. Shops/vendors must generate weapons via
  `WeaponGenerator.GenerateFromDefinition`/`GenerateFromRandomDefinition`, never the
  archetype `WeaponGenerator.Generate`.
- UI is a single Screen Space - Overlay canvas (UGUI). No world-space rendering;
  animation/VFX are UGUI tweens, not ParticleSystem/Animator.
- UI is built from an existing UI kit (its prefabs, sprites and components), never from
  scratch; the chosen kit is recorded in `memory/ui-style.md`. Check its licence and
  `.gitattributes` (LFS) before importing.
- New game visuals (non-UI art) start as greybox/placeholder primitives. No final art before a mechanic
  is proven.
- Every asset and script has a committed `.meta` file. Binary assets go through Git LFS
  — check `.gitattributes` before adding a new binary type.
- Changing a serialized field or save-data shape: keep old saves loading, or call out
  the break explicitly.


## Your file
Write `Assets/Scripts/Encounters/VillageQuestBoardView.cs` (step 3 of 3).
Replace the 'Quests are coming soon' stub. Keep the public static Create(Transform parent) factory and the existing UGUI-built-in-code style (legacy Text, VerticalLayoutGroup). Add Bind(DailyMissionBoard board, PlayerWallet wallet): shows a 'Daily Missions' title, then one row per mission with its description, 'progress/target', and a Claim button that is interactable only when complete and unclaimed; Claim adds the gold to the wallet (PlayerWallet API as in context) and refreshes. Re-renders on board.Changed; unsubscribes in OnDestroy. If Bind is never called, show 'No missions today.'
It is accepted when: [{"absent_text": "Quests are coming soon"}, {"text": "DailyMissionBoard"}]

## Existing code
// ===== Assets/Scripts/Progression/DailyMissions.cs (written earlier in this feature) =====
using System;
using System.Globalization;

namespace DoomScroller.Progression
{
    public enum DailyMissionKind
    {
        DefeatEnemies,
        ClearDungeon
    }

    // One daily goal. Plain C# so it's testable without a scene.
    public class DailyMission
    {
        public DailyMissionKind Kind { get; }
        public string Description { get; }
        public int Target { get; }
        public int Progress { get; internal set; }
        public int GoldReward { get; }
        public bool Claimed { get; internal set; }

        public bool IsComplete => Progress >= Target;

        public DailyMission(DailyMissionKind kind, string description, int target, int goldReward)
        {
            Kind = kind;
            Description = description;
            Target = Math.Max(1, target);
            GoldReward = Math.Max(0, goldReward);
        }
    }

    // Save-friendly snapshot of the board. Lives in SaveData later.
    [Serializable]
    public class DailyMissionState
    {
        public string date;
        public int[] progress;
        public bool[] claimed;
    }

    // Three daily missions, seeded from the UTC calendar date so every device
    // rolls the same set for the same day.
    public class DailyMissionBoard
    {
        public const int MissionCount = 3;
        private const string DateFormat = "yyyy-MM-dd";

        private readonly DailyMission[] _missions = new DailyMission[MissionCount];

        public DateTime Date { get; private set; }
        public int Count => _missions.Length;

        public event Action Changed;

        public DailyMissionBoard(DateTime utcDate)
        {
            Roll(utcDate.Date);
        }

        public DailyMission GetMission(int index)
        {
            if (index < 0 || index >= _missions.Length)
            {
                return null;
            }

            return _missions[index];
        }

        public void RecordEnemyDefeated()
        {
            Advance(DailyMissionKind.DefeatEnemies);
        }

        public void RecordDungeonCleared()
        {
            Advance(DailyMissionKind.ClearDungeon);
        }

        // Returns the gold reward exactly once for a completed mission; 0 otherwise.
        public int Claim(int index)
        {
            DailyMission mission = GetMission(index);
            if (mission == null || mission.Claimed || !mission.IsComplete)
            {
                return 0;
            }

            mission.Claimed = true;
            Changed?.Invoke();
            return mission.GoldReward;
        }

        // Re-rolls and resets progress if the UTC day changed. Returns true if it did.
        public bool RefreshForDate(DateTime utcDate)
        {
            DateTime day = utcDate.Date;
            if (day == Date)
            {
                return false;
            }

            Roll(day);
            Changed?.Invoke();
            return true;
        }

        public DailyMissionState CaptureState()
        {
            var state = new DailyMissionState
            {
                date = Date.ToString(DateFormat, CultureInfo.InvariantCulture),
                progress = new int[_missions.Length],
                claimed = new bool[_missions.Length]
            };

            for (int i = 0; i < _missions.Length; i++)
            {
                state.progress[i] = _missions[i].Progress;
                state.claimed[i] = _missions[i].Claimed;
            }

            return state;
        }

        // Restores progress only if the saved date matches the board's date;
        // a stale or malformed state leaves the fresh board untouched.
        // Doesn't fire Changed — called once, before anything subscribes.
        public void LoadState(DailyMissionState state)
        {
            if (state == null || state.progress == null || state.claimed == null)
            {
                return;
            }

            if (!DateTime.TryParseExact(state.date, DateFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime saved) || saved.Date != Date)
            {
                return;
            }

            for (int i = 0; i < _missions.Length; i++)
            {
                int progress = i < state.progress.Length ? state.progress[i] : 0;
                bool claimed = i < state.claimed.Length && state.claimed[i];
                _missions[i].Progress = Math.Max(0, Math.Min(progress, _missions[i].Target));
                _missions[i].Claimed = claimed && _missions[i].IsComplete;
            }
        }

        private void Advance(DailyMissionKind kind)
        {
            bool changed = false;
            for (int i = 0; i < _missions.Length; i++)
            {
                DailyMission mission = _missions[i];
                if (mission.Kind != kind || mission.Claimed || mission.IsComplete)
                {
                    continue;
                }

                mission.Progress++;
                changed = true;
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        private void Roll(DateTime day)
        {
            Date = day;
            int seed = day.Year * 10000 + day.Month * 100 + day.Day;
            var rng = new Random(seed);

            for (int i = 0; i < _missions.Length; i++)
            {
                DailyMissionKind kind;
                if (i == 0)
                {
                    kind = DailyMissionKind.DefeatEnemies;
                }
                else if (i == 1)
                {
                    kind = DailyMissionKind.ClearDungeon;
                }
                else
                {
                    kind = rng.Next(2) == 0 ? DailyMissionKind.DefeatEnemies : DailyMissionKind.ClearDungeon;
                }

                _missions[i] = CreateMission(kind, rng);
            }
        }

        private static DailyMission CreateMission(DailyMissionKind kind, Random rng)
        {
            if (kind == DailyMissionKind.ClearDungeon)
            {
                int target = rng.Next(1, 3);
                string text = target == 1 ? "Clear a dungeon" : "Clear " + target + " dungeons";
                return new DailyMission(kind, text, target, 75 * target);
            }

            int enemies = 5 * rng.Next(1, 5);
            return new DailyMission(kind, "Defeat " + enemies + " enemies", enemies, 8 * enemies);
        }
    }
}


// ===== Assets/Tests/EditMode/DailyMissionBoardTests.cs (written earlier in this feature) =====
using System;
using DoomScroller.Progression;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    public class DailyMissionBoardTests
    {
        private static readonly DateTime Day = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc);

        private static void Complete(DailyMissionBoard board, int index)
        {
            DailyMission mission = board.GetMission(index);
            for (int i = 0; i < mission.Target; i++)
            {
                if (mission.Kind == DailyMissionKind.DefeatEnemies)
                {
                    board.RecordEnemyDefeated();
                }
                else
                {
                    board.RecordDungeonCleared();
                }
            }
        }

        [Test]
        public void SameDate_ProducesIdenticalMissions()
        {
            var a = new DailyMissionBoard(Day);
            var b = new DailyMissionBoard(Day.AddHours(13));

            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a.GetMission(i).Kind, b.GetMission(i).Kind);
                Assert.AreEqual(a.GetMission(i).Description, b.GetMission(i).Description);
                Assert.AreEqual(a.GetMission(i).Target, b.GetMission(i).Target);
                Assert.AreEqual(a.GetMission(i).GoldReward, b.GetMission(i).GoldReward);
            }
        }

        [Test]
        public void DifferentDates_CanProduceDifferentMissions()
        {
            var baseline = new DailyMissionBoard(Day);
            bool anyDifferent = false;

            for (int d = 1; d <= 30 && !anyDifferent; d++)
            {
                var other = new DailyMissionBoard(Day.AddDays(d));
                for (int i = 0; i < other.Count; i++)
                {
                    if (other.GetMission(i).Description != baseline.GetMission(i).Description
                        || other.GetMission(i).Kind != baseline.GetMission(i).Kind)
                    {
                        anyDifferent = true;
                        break;
                    }
                }
            }

            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void Board_HasExactlyThreeMissions()
        {
            var board = new DailyMissionBoard(Day);

            Assert.AreEqual(3, DailyMissionBoard.MissionCount);
            Assert.AreEqual(3, board.Count);
            Assert.IsNotNull(board.GetMission(0));
            Assert.IsNotNull(board.GetMission(2));
            Assert.IsNull(board.GetMission(3));
            Assert.IsNull(board.GetMission(-1));
        }

        [Test]
        public void RecordDungeonCleared_AdvancesOnlyDungeonMissions()
        {
            var board = new DailyMissionBoard(Day);

            board.RecordDungeonCleared();

            for (int i = 0; i < board.Count; i++)
            {
                int expected = board.GetMission(i).Kind == DailyMissionKind.ClearDungeon ? 1 : 0;
                Assert.AreEqual(expected, board.GetMission(i).Progress);
            }
        }

        [Test]
        public void RecordEnemyDefeated_AdvancesOnlyEnemyMissions()
        {
            var board = new DailyMissionBoard(Day);

            board.RecordEnemyDefeated();

            for (int i = 0; i < board.Count; i++)
            {
                int expected = board.GetMission(i).Kind == DailyMissionKind.DefeatEnemies ? 1 : 0;
                Assert.AreEqual(expected, board.GetMission(i).Progress);
            }
        }

        [Test]
        public void Progress_CapsAtTarget()
        {
            var board = new DailyMissionBoard(Day);
            DailyMission mission = board.GetMission(0);

            for (int i = 0; i < mission.Target + 10; i++)
            {
                board.RecordEnemyDefeated();
            }

            Assert.AreEqual(mission.Target, mission.Progress);
            Assert.IsTrue(mission.IsComplete);
        }

        [Test]
        public void Claim_BeforeComplete_ReturnsZero()
        {
            var board = new DailyMissionBoard(Day);

            Assert.AreEqual(0, board.Claim(0));
            Assert.IsFalse(board.GetMission(0).Claimed);
        }

        [Test]
        public void Claim_PaysOnce_WhenComplete()
        {
            var board = new DailyMissionBoard(Day);
            Complete(board, 0);

            int first = board.Claim(0);
            int second = board.Claim(0);

            Assert.AreEqual(board.GetMission(0).GoldReward, first);
            Assert.Greater(first, 0);
            Assert.AreEqual(0, second);
            Assert.IsTrue(board.GetMission(0).Claimed);
        }

        [Test]
        public void Claim_InvalidIndex_ReturnsZero()
        {
            var board = new DailyMissionBoard(Day);

            Assert.AreEqual(0, board.Claim(99));
        }

        [Test]
        public void RefreshForDate_SameDay_DoesNothing()
        {
            var board = new DailyMissionBoard(Day);
            board.RecordEnemyDefeated();

            Assert.IsFalse(board.RefreshForDate(Day.AddHours(5)));
            Assert.AreEqual(1, board.GetMission(0).Progress);
        }

        [Test]
        public void RefreshForDate_NewDay_ResetsProgressAndClaims()
        {
            var board = new DailyMissionBoard(Day);
            Complete(board, 0);
            board.Claim(0);
            int changedCount = 0;
            board.Changed += () => changedCount++;

            bool refreshed = board.RefreshForDate(Day.AddDays(1));

            Assert.IsTrue(refreshed);
            Assert.AreEqual(Day.AddDays(1).Date, board.Date);
            Assert.AreEqual(1, changedCount);
            for (int i = 0; i < board.Count; i++)
            {
                Assert.AreEqual(0, board.GetMission(i).Progress);
                Assert.IsFalse(board.GetMission(i).Claimed);
            }
        }

        [Test]
        public void CaptureAndLoadState_RoundTrips()
        {
            var board = new DailyMissionBoard(Day);
            Complete(board, 0);
            board.Claim(0);
            board.RecordDungeonCleared();

            DailyMissionState state = board.CaptureState();
            var restored = new DailyMissionBoard(Day);
            restored.LoadState(state);

            Assert.AreEqual("2026-03-14", state.date);
            for (int i = 0; i < board.Count; i++)
            {
                Assert.AreEqual(board.GetMission(i).Progress, restored.GetMission(i).Progress);
                Assert.AreEqual(board.GetMission(i).Claimed, restored.GetMission(i).Claimed);
            }
        }

        [Test]
        public void LoadState_StaleDate_IsIgnored()
        {
            var yesterday = new DailyMissionBoard(Day.AddDays(-1));
            Complete(yesterday, 0);
            DailyMissionState state = yesterday.CaptureState();

            var today = new DailyMissionBoard(Day);
            today.LoadState(state);

            for (int i = 0; i < today.Count; i++)
            {
                Assert.AreEqual(0, today.GetMission(i).Progress);
                Assert.IsFalse(today.GetMission(i).Claimed);
            }
        }

        [Test]
        public void LoadState_NullOrMalformed_LeavesBoardUntouched()
        {
            var board = new DailyMissionBoard(Day);
            board.RecordEnemyDefeated();

            board.LoadState(null);
            board.LoadState(new DailyMissionState { date = "2026-03-14" });

            Assert.AreEqual(1, board.GetMission(0).Progress);
        }
    }
}


// ===== Assets/Scripts/Encounters/VillageQuestBoardView.cs =====
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // One establishment in the Village (see VillageView) — a stub. Quests themselves are
    // a later iteration; this just holds the establishment's place in the swipe order.
    public class VillageQuestBoardView : MonoBehaviour
    {
        public static VillageQuestBoardView Create(Transform parent)
        {
            var go = new GameObject("QuestBoard", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var view = go.AddComponent<VillageQuestBoardView>();

            var body = CreateLabel(go.transform, "Body", "Quests are coming soon.", 30, FontStyle.Italic);
            body.color = new Color(1f, 1f, 1f, 0.6f);

            return view;
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

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = fontSize + 24;

            return text;
        }
    }
}


// ===== Assets/Scripts/Encounters/VillageMarketView.cs =====
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
        private const float RowHeight = 186f;
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
            GearRowFactory.SetWeaponDetail(row, item);
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


// ===== Assets/Scripts/Progression/PlayerWallet.cs =====
using System;
using UnityEngine;

namespace DoomScroller.Progression
{
    // Plain C# so gold math is testable without a running scene — same shape as
    // PlayerHealth/PlayerProgression. Gold is earned from combat rewards
    // (RewardGenerator.ComputeGoldReward) and spent at the Village (potion refills,
    // the Market).
    public class PlayerWallet
    {
        public int Gold { get; private set; }

        public event Action Changed;

        public PlayerWallet(int startingGold)
        {
            Gold = Mathf.Max(0, startingGold);
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Gold += amount;
            Changed?.Invoke();
        }

        // False (and no state change) if funds are insufficient — callers never need to
        // check Gold themselves first.
        public bool TrySpendGold(int amount)
        {
            if (amount <= 0 || Gold < amount)
            {
                return false;
            }

            Gold -= amount;
            Changed?.Invoke();
            return true;
        }

        // The save/load path. Doesn't fire Changed — called once, before anything
        // subscribes.
        public void LoadState(int gold)
        {
            Gold = gold;
        }
    }
}


Return the full file content for `Assets/Scripts/Encounters/VillageQuestBoardView.cs`.