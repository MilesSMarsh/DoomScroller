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
    // the already-computed result on screen, untouched. Like CombatResolutionView, the
    // fight itself is a separate ephemeral instance — only a loss on this depth costs
    // persistent max HP (PlayerHealth.ApplyDefeatPenalty, applied here since this is the
    // one place that has the fresh numbers to narrate immediately; DungeonView applies
    // the same penalty separately when the player leaves a run early instead).
    public class DungeonStepPageView : MonoBehaviour
    {
        // Fires once, when this page's own combat resolves — DungeonView listens to
        // know whether the frontier can advance.
        public event Action<DungeonStep, bool> Resolved;

        private CombatPlaybackView _playback;
        private Text _depthLabelText;
        private Text _outcomeText;
        private Text _rewardText;
        private Text _advanceHintText;

        private DungeonStep _step;
        private bool _lastResultWasVictory;
        private Coroutine _revealCoroutine;

        public bool IsResolved { get; private set; }
        public bool LastResultWasVictory => IsResolved && _lastResultWasVictory;

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

            // The page root doubles as the revive dialog's screen anchor — a dungeon
            // page already fills the whole screen (see pageLayout above).
            view._playback = CombatPlaybackView.Create(rect, rect);

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
        public void Resolve(DungeonStep step, PlayerLoadout loadout, PlayerProgression progression,
            PlayerHealth health, PlayerStats stats, GameSettings settings)
        {
            _step = step;
            _depthLabelText.text = step.Label;

            var isFight = !step.IsTreasureRoom;
            _playback.gameObject.SetActive(isFight);

            // Always enter at full of whatever CurrentMaxHealth currently is — see
            // CombatResolutionView.Show for the same reasoning.
            var sim = CombatResolver.CreateSimulatorForStep(loadout, stats, settings, step, health.CurrentMaxHealth);

            _outcomeText.text = "";
            _rewardText.text = "";
            _advanceHintText.text = "";

            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
            }

            _revealCoroutine = StartCoroutine(RevealSequence(sim, loadout, progression, health, settings));
        }

        private IEnumerator RevealSequence(CombatSimulator sim, PlayerLoadout loadout,
            PlayerProgression progression, PlayerHealth health, GameSettings settings)
        {
            yield return StartCoroutine(_playback.Play(sim, loadout, settings, null, _step.StepColor));

            var isVictory = _playback.LastOutcomeWasVictory;
            _outcomeText.text = isVictory ? "VICTORY" : "DEFEAT";
            _outcomeText.color = isVictory ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);

            if (isVictory)
            {
                var previousLevel = progression.Level;
                progression.AddXp(_step.XpReward);

                var item = RewardGenerator.GenerateItem(_step.DepthIndex);
                loadout.AddToInventory(item);

                var rewardLine = $"Reward: {_step.XpReward} XP + {item.title} (equip it from Profile)";
                if (_playback.WasCloseCall)
                {
                    rewardLine += "\nClose call — that one nearly had you.";
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
                health.ApplyDefeatPenalty();
                _rewardText.text = $"The dungeon ends here. Your max HP drops to " +
                    $"{health.CurrentMaxHealth}/{health.TrueMaxHealth}, regenerating over time.";
            }

            IsResolved = true;
            _lastResultWasVictory = isVictory;
            _revealCoroutine = null;
            Resolved?.Invoke(_step, isVictory);
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
