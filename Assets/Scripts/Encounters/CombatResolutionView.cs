using System;
using System.Collections;
using DoomScroller.Combat;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using DoomScroller.Social;
using DoomScroller.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // Reveals the resolved combat as a short sequence, then a clear win/lose outcome.
    // Outcome itself is computed by CombatResolver from equipped stats/abilities, not
    // real-time input here (Phase 5).
    public class CombatResolutionView : MonoBehaviour
    {
        private const float SecondsPerLine = 0.2f;

        public event Action ReturnToFeedRequested;
        public event Action<EncounterData> EncounterWon;
        public event Action<EncounterData> EncounterLost;

        private const float PortraitSize = 220f;

        private GameObject _battleRow;
        private Image _playerPortrait;
        private Image _enemyPortrait;
        private Text _sequenceText;
        private Text _outcomeText;
        private Text _rewardText;
        private Coroutine _revealCoroutine;
        private EncounterData _data;
        private MessageBoardView _messageBoard;
        private RectTransform _scrollContent;

        public static CombatResolutionView Create(Transform parent)
        {
            var go = new GameObject("CombatResolution", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = go.AddComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.06f, 1f);

            var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
            // Left/right/bottom padding moves onto the scroll Content below — only the
            // nav-bar clearance belongs on the root, since that's fixed regardless of
            // scroll position.
            layoutGroup.padding = new RectOffset(0, 0, NavBarController.Height, 0);
            layoutGroup.spacing = 0;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            // Must be true: with this false, sibling positioning (e.g. the message board
            // below Reward, and ReturnButton below that) doesn't track each child's real
            // preferred/content-driven height — verified live via RectTransform dumps,
            // same root cause as the FeedController card-height fix.
            layoutGroup.childControlHeight = true;

            var view = go.AddComponent<CombatResolutionView>();

            // Scrollable body: the battle row + sequence + reward + message board (whose
            // comment list can grow indefinitely) can outgrow a short/narrow screen, so
            // this panel scrolls instead of clipping. Return stays pinned below.
            var content = ScrollAreaFactory.Create(rect, new RectOffset(48, 48, 0, 40), 20);
            view._scrollContent = content;

            // Only shown for actual fights (Enemy type) — Treasure never fights, per
            // CombatResolver.ResolveTreasure's "no guardians, no traps" flavor.
            view._battleRow = CreateBattleRow(content, out view._playerPortrait, out view._enemyPortrait);

            view._sequenceText = CreateLabel(content, "Sequence", 34, FontStyle.Normal, 420);
            view._sequenceText.alignment = TextAnchor.UpperLeft;
            view._outcomeText = CreateLabel(content, "Outcome", 64, FontStyle.Bold, 90);
            // Tall enough for a 3-line reward string (reward + close-call damage note +
            // level-up), not just the visually-tighter single-line case.
            view._rewardText = CreateLabel(content, "Reward", 34, FontStyle.Italic, 150);

            // Phase 7 stub: a minimal comment list + reply field, shown once the outcome
            // is revealed. Full threading/replies/moderation are explicitly out of scope.
            view._messageBoard = MessageBoardView.Create(content);
            view._messageBoard.ContentChanged += () => LayoutRebuilder.ForceRebuildLayoutImmediate(view._scrollContent);

            CreateButton(rect, "ReturnButton", "Return to Feed", () => view.ReturnToFeedRequested?.Invoke());

            go.SetActive(false);
            return view;
        }

        public void Show(EncounterData data, PlayerLoadout loadout, PlayerProgression progression, PlayerHealth health)
        {
            gameObject.SetActive(true);
            _data = data;

            var isFight = data.type == EncounterType.Enemy;
            _battleRow.SetActive(isFight);
            _playerPortrait.sprite = loadout.PlayerSprite;
            _playerPortrait.color = loadout.PlayerSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            if (isFight)
            {
                _enemyPortrait.sprite = data.artwork;
                _enemyPortrait.color = data.artwork != null ? Color.white : data.cardColor;
            }

            var result = CombatResolver.Resolve(loadout, data);

            _sequenceText.text = "";
            _outcomeText.text = "";
            _rewardText.text = "";
            _messageBoard.Hide();

            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
            }

            _revealCoroutine = StartCoroutine(RevealSequence(result, data.baseXpReward, loadout, progression, health));
        }

        public void Hide()
        {
            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
                _revealCoroutine = null;
            }

            gameObject.SetActive(false);
        }

        private IEnumerator RevealSequence(CombatResult result, int xpReward, PlayerLoadout loadout,
            PlayerProgression progression, PlayerHealth health)
        {
            foreach (var line in result.SequenceLines)
            {
                _sequenceText.text += line + "\n";
                yield return new WaitForSeconds(SecondsPerLine);
            }

            _outcomeText.text = result.IsVictory ? "VICTORY" : "DEFEAT";
            _outcomeText.color = result.IsVictory ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);

            health.ApplyDamage(result.HealthDamage);

            if (result.IsVictory)
            {
                var previousLevel = progression.Level;
                progression.AddXp(xpReward);

                // No difficulty choice any more on a plain encounter — always the base
                // (Common) reward tier; Dungeons are the vehicle for better loot now.
                var item = RewardGenerator.GenerateItem(0);
                loadout.AddToInventory(item);

                var rewardLine = $"Reward: {xpReward} XP + {item.title} (equip it from Profile)";
                if (result.WasCloseCall)
                {
                    rewardLine += $"\nClose call — you took {result.HealthDamage} damage " +
                        $"({health.CurrentHealth}/{health.MaxHealth} HP).";
                }

                _rewardText.text = progression.Level > previousLevel
                    ? $"{rewardLine}\nLevel up! You're now level {progression.Level}."
                    : rewardLine;

                EncounterWon?.Invoke(_data);
            }
            else
            {
                _rewardText.text = $"No reward this time — your gear held you back. You took " +
                    $"{result.HealthDamage} damage ({health.CurrentHealth}/{health.MaxHealth} HP). " +
                    "This one's lost for good; gear up and look for the next one.";
                EncounterLost?.Invoke(_data);
            }

            _messageBoard.Populate(_data, result.IsVictory);

            _revealCoroutine = null;
        }

        private static GameObject CreateBattleRow(Transform parent, out Image playerPortrait, out Image enemyPortrait)
        {
            var rowGo = new GameObject("BattleRow", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            rowGo.AddComponent<LayoutElement>().preferredHeight = PortraitSize;

            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 24;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            // Must be true, same reason as MessageBoardView's reply row: without it, this
            // row won't actually write each portrait's fixed preferred size.
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            playerPortrait = CreatePortraitBox(rowGo.transform, "PlayerPortrait");

            var vsGo = new GameObject("VsLabel", typeof(RectTransform));
            vsGo.transform.SetParent(rowGo.transform, false);
            vsGo.AddComponent<LayoutElement>().preferredWidth = 70;
            var vsText = vsGo.AddComponent<Text>();
            vsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            vsText.fontSize = 32;
            vsText.fontStyle = FontStyle.Bold;
            vsText.alignment = TextAnchor.MiddleCenter;
            vsText.color = new Color(1f, 1f, 1f, 0.6f);
            vsText.text = "VS";

            enemyPortrait = CreatePortraitBox(rowGo.transform, "EnemyPortrait");

            return rowGo;
        }

        private static Image CreatePortraitBox(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = PortraitSize;
            layoutElement.preferredHeight = PortraitSize;

            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, 0.12f);

            return image;
        }

        private static Text CreateLabel(Transform parent, string name, int fontSize, FontStyle style, float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;

            return text;
        }

        private static void CreateButton(Transform parent, string name, string label, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
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
            labelText.text = label;
        }
    }
}
