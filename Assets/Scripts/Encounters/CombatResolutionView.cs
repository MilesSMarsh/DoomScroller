using System;
using System.Collections;
using DoomScroller.Combat;
using DoomScroller.Feed;
using DoomScroller.Inventory;
using DoomScroller.Progression;
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
        private const float SecondsPerLine = 0.7f;

        public event Action ReturnToFeedRequested;

        private Text _sequenceText;
        private Text _outcomeText;
        private Text _rewardText;
        private Coroutine _revealCoroutine;

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
            layoutGroup.padding = new RectOffset(48, 48, NavBarController.Height + 20, 40);
            layoutGroup.spacing = 20;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = false;

            var view = go.AddComponent<CombatResolutionView>();
            view._sequenceText = CreateLabel(rect, "Sequence", 30, FontStyle.Normal, 400);
            view._sequenceText.alignment = TextAnchor.UpperLeft;
            view._outcomeText = CreateLabel(rect, "Outcome", 56, FontStyle.Bold, 80);
            view._rewardText = CreateLabel(rect, "Reward", 30, FontStyle.Italic, 60);

            CreateButton(rect, "ReturnButton", "Return to Feed", () => view.ReturnToFeedRequested?.Invoke());

            go.SetActive(false);
            return view;
        }

        public void Show(EncounterData data, EncounterVariant variant, PlayerLoadout loadout, PlayerProgression progression)
        {
            gameObject.SetActive(true);

            var result = CombatResolver.Resolve(loadout, data, variant);

            _sequenceText.text = "";
            _outcomeText.text = "";
            _rewardText.text = "";

            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
            }

            _revealCoroutine = StartCoroutine(RevealSequence(result, variant, loadout, progression));
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

        private IEnumerator RevealSequence(CombatResult result, EncounterVariant variant, PlayerLoadout loadout,
            PlayerProgression progression)
        {
            foreach (var line in result.SequenceLines)
            {
                _sequenceText.text += line + "\n";
                yield return new WaitForSeconds(SecondsPerLine);
            }

            _outcomeText.text = result.IsVictory ? "VICTORY" : "DEFEAT";
            _outcomeText.color = result.IsVictory ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);

            if (result.IsVictory)
            {
                var previousLevel = progression.Level;
                progression.AddXp(variant.XpReward);

                var item = RewardGenerator.GenerateItem(variant.TierIndex);
                loadout.AddToInventory(item);

                var rewardLine = $"Reward: {variant.XpReward} XP + {item.title} (equip it from Profile)";
                _rewardText.text = progression.Level > previousLevel
                    ? $"{rewardLine}\nLevel up! You're now level {progression.Level}."
                    : rewardLine;
            }
            else
            {
                _rewardText.text = "No reward this time — your gear held you back. Try a different loadout.";
            }

            _revealCoroutine = null;
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
            layoutElement.preferredHeight = 96;

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
            labelText.fontSize = 32;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
        }
    }
}
