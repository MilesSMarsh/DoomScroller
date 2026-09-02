using System;
using System.Collections;
using DoomScroller.Combat;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // One page of a dungeon run: animated battle playback + outcome/reward text, sized to
    // exactly one viewport width by DungeonView (same shape as CombatResolutionView's
    // layout, minus its own Return button — leaving/advancing is a DungeonView-level
    // gesture, not a per-page control, so pages have no drag handling of their own).
    // Resolves itself once, the first time DungeonView reveals it; a revisit just leaves
    // the already-computed result on screen, untouched.
    public class DungeonStepPageView : MonoBehaviour
    {
        // Fires once, when this page's own combat resolves — DungeonView listens to
        // know whether the frontier can advance.
        public event Action<DungeonStep, CombatResult> Resolved;

        private CombatPlaybackView _playback;
        private Text _depthLabelText;
        private Text _outcomeText;
        private Text _rewardText;
        private Text _advanceHintText;

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

            // Outcome/reward sit at the top so they stay visible above the fight itself;
            // a flexible spacer on each side of CombatPlaybackView centers it in the
            // remaining space below the depth label instead of it sitting right under
            // the text or hugging the very bottom of the screen.
            view._depthLabelText = CreateLabel(rect, "DepthLabel", 44, FontStyle.Bold, 68);
            view._outcomeText = CreateLabel(rect, "Outcome", 54, FontStyle.Bold, 70);
            view._rewardText = CreateLabel(rect, "Reward", 28, FontStyle.Italic, 120);
            view._advanceHintText = CreateLabel(rect, "AdvanceHint", 30, FontStyle.Bold, 60);
            view._advanceHintText.color = new Color(0.4f, 0.85f, 1f, 0.95f);

            CreateSpacer(rect);

            view._playback = CombatPlaybackView.Create(rect);

            CreateSpacer(rect);

            return view;
        }

        private static void CreateSpacer(Transform parent)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.flexibleHeight = 1;
            layoutElement.minHeight = 0;
            layoutElement.preferredHeight = 0;
        }

        // Resolves combat for this step and reveals it — DungeonView only ever calls
        // this once per page (guarded by IsResolved on its side).
        public void Resolve(DungeonStep step, PlayerLoadout loadout, PlayerProgression progression, PlayerHealth health)
        {
            _step = step;
            _depthLabelText.text = step.Label;

            var isFight = !step.IsTreasureRoom;
            _playback.gameObject.SetActive(isFight);

            var result = CombatResolver.ResolveStep(loadout, step, health);

            _outcomeText.text = "";
            _rewardText.text = "";
            _advanceHintText.text = "";

            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
            }

            _revealCoroutine = StartCoroutine(RevealSequence(result, loadout, progression, health));
        }

        private IEnumerator RevealSequence(CombatResult result, PlayerLoadout loadout,
            PlayerProgression progression, PlayerHealth health)
        {
            yield return StartCoroutine(_playback.Play(result, loadout, health, _step.EnemyHealth, null, _step.StepColor));

            _outcomeText.text = result.IsVictory ? "VICTORY" : "DEFEAT";
            _outcomeText.color = result.IsVictory ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);

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

            IsResolved = true;
            _result = result;
            _revealCoroutine = null;
            Resolved?.Invoke(_step, result);
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
