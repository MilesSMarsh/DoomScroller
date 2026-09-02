using System;
using System.Collections;
using DoomScroller.Combat;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using DoomScroller.Social;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // One page of a dungeon run: battle row + sequence/outcome/reward text + message
    // board, sized to exactly one viewport width by DungeonView (same shape as
    // CombatResolutionView's layout, minus its own Return button — leaving/advancing is
    // a DungeonView-level gesture, not a per-page control, so pages have no drag
    // handling of their own). Resolves itself once, the first time DungeonView reveals
    // it; a revisit just leaves the already-computed result on screen, untouched.
    public class DungeonStepPageView : MonoBehaviour
    {
        private const float SecondsPerLine = 0.2f;
        private const float PortraitSize = 200f;

        // Fires once, when this page's own combat resolves — DungeonView listens to
        // know whether the frontier can advance.
        public event Action<DungeonStep, CombatResult> Resolved;

        private GameObject _battleRow;
        private Image _playerPortrait;
        private Image _enemyPortrait;
        private Text _depthLabelText;
        private Text _sequenceText;
        private Text _outcomeText;
        private Text _rewardText;
        private Text _advanceHintText;
        private MessageBoardView _messageBoard;

        private DungeonStep _step;
        private CombatResult _result;
        private Coroutine _revealCoroutine;

        public bool IsResolved { get; private set; }
        public bool LastResultWasVictory => IsResolved && _result.IsVictory;

        public static DungeonStepPageView Create(Transform parent, float pageWidth)
        {
            var go = new GameObject("DungeonPage", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var pageLayout = go.AddComponent<LayoutElement>();
            pageLayout.minWidth = pageWidth;
            pageLayout.preferredWidth = pageWidth;
            pageLayout.flexibleWidth = 0;
            pageLayout.minHeight = 0;
            pageLayout.preferredHeight = 0;
            pageLayout.flexibleHeight = 1;

            var background = go.AddComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.06f, 1f);

            var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(48, 48, 24, 24);
            layoutGroup.spacing = 14;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;

            var view = go.AddComponent<DungeonStepPageView>();

            view._depthLabelText = CreateLabel(rect, "DepthLabel", 44, FontStyle.Bold, 68);
            view._battleRow = CreateBattleRow(rect, out view._playerPortrait, out view._enemyPortrait);
            view._sequenceText = CreateLabel(rect, "Sequence", 30, FontStyle.Normal, 240);
            view._sequenceText.alignment = TextAnchor.UpperLeft;
            view._outcomeText = CreateLabel(rect, "Outcome", 54, FontStyle.Bold, 70);
            view._rewardText = CreateLabel(rect, "Reward", 28, FontStyle.Italic, 120);
            view._advanceHintText = CreateLabel(rect, "AdvanceHint", 30, FontStyle.Bold, 60);
            view._advanceHintText.color = new Color(0.4f, 0.85f, 1f, 0.95f);
            view._messageBoard = MessageBoardView.Create(rect);

            return view;
        }

        // Resolves combat for this step and reveals it — DungeonView only ever calls
        // this once per page (guarded by IsResolved on its side).
        public void Resolve(DungeonStep step, EncounterData data, PlayerLoadout loadout,
            PlayerProgression progression, PlayerHealth health)
        {
            _step = step;
            _depthLabelText.text = step.Label;

            var isFight = !step.IsTreasureRoom;
            _battleRow.SetActive(isFight);
            _playerPortrait.sprite = loadout.PlayerSprite;
            _playerPortrait.color = loadout.PlayerSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            if (isFight)
            {
                _enemyPortrait.sprite = null;
                _enemyPortrait.color = step.StepColor;
            }

            var result = CombatResolver.ResolveStep(loadout, data, step);

            _sequenceText.text = "";
            _outcomeText.text = "";
            _rewardText.text = "";
            _advanceHintText.text = "";

            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
            }

            _revealCoroutine = StartCoroutine(RevealSequence(result, data, loadout, progression, health));
        }

        private IEnumerator RevealSequence(CombatResult result, EncounterData data, PlayerLoadout loadout,
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
                progression.AddXp(_step.XpReward);

                var item = RewardGenerator.GenerateItem(_step.DepthIndex);
                loadout.AddToInventory(item);

                var rewardLine = $"Reward: {_step.XpReward} XP + {item.title} (equip it from Profile)";
                if (result.WasCloseCall)
                {
                    rewardLine += $"\nClose call — you took {result.HealthDamage} damage " +
                        $"({health.CurrentHealth}/{health.MaxHealth} HP).";
                }

                _rewardText.text = progression.Level > previousLevel
                    ? $"{rewardLine}\nLevel up! You're now level {progression.Level}."
                    : rewardLine;

                // The only indicator that there's more dungeon ahead — a fresh win with
                // no further step (the treasure room) needs no prompt, since Return to
                // Feed is the only thing left to do.
                _advanceHintText.text = _step.IsTreasureRoom ? "" : "◂ Swipe left to press deeper into the dungeon";
            }
            else
            {
                _rewardText.text = $"You took {result.HealthDamage} damage " +
                    $"({health.CurrentHealth}/{health.MaxHealth} HP). The dungeon ends here.";
            }

            _messageBoard.Populate(data, result.IsVictory);

            IsResolved = true;
            _result = result;
            _revealCoroutine = null;
            Resolved?.Invoke(_step, result);
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
    }
}
