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
    // Reveals a fight as an animated beat-by-beat sequence (CombatPlaybackView), then a
    // clear win/lose outcome. The fight is its own ephemeral CombatSimulator instance —
    // it never touches the persistent PlayerHealth; only a defeat (including leaving
    // early) costs anything, via PlayerHealth.ApplyDefeatPenalty.
    public class CombatResolutionView : MonoBehaviour
    {
        public event Action ReturnToFeedRequested;
        public event Action<EncounterData> EncounterWon;
        public event Action<EncounterData> EncounterLost;

        private CombatPlaybackView _playback;
        private Text _outcomeText;
        private Text _rewardText;
        private Coroutine _revealCoroutine;
        private EncounterData _data;
        private PlayerHealth _health;

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
            // No horizontal padding here — that belongs on ContentArea below, so the
            // Leave button (a direct sibling of it) still spans full width flush to the
            // edges, matching every other button in this project.
            layoutGroup.padding = new RectOffset(0, 0, NavBarController.Height, 0);
            layoutGroup.spacing = 0;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;

            var view = go.AddComponent<CombatResolutionView>();

            // Outcome/reward sit at the top so they stay visible above the fight itself;
            // a flexible spacer on each side of CombatPlaybackView centers it in the
            // remaining space below the reward text instead of it sitting right under
            // the reward text or hugging the very bottom of the screen.
            var contentAreaGo = new GameObject("ContentArea", typeof(RectTransform));
            contentAreaGo.transform.SetParent(rect, false);
            var contentAreaLayout = contentAreaGo.AddComponent<LayoutElement>();
            contentAreaLayout.flexibleHeight = 1;
            contentAreaLayout.minHeight = 0;
            contentAreaLayout.preferredHeight = 0;

            var contentAreaGroup = contentAreaGo.AddComponent<VerticalLayoutGroup>();
            contentAreaGroup.padding = new RectOffset(48, 48, 0, 24);
            contentAreaGroup.spacing = 0;
            contentAreaGroup.childAlignment = TextAnchor.UpperCenter;
            contentAreaGroup.childForceExpandWidth = true;
            contentAreaGroup.childForceExpandHeight = false;
            contentAreaGroup.childControlWidth = true;
            contentAreaGroup.childControlHeight = true;

            view._outcomeText = CreateLabel(contentAreaGo.transform, "Outcome", 64, FontStyle.Bold, 90);
            // Tall enough for a 3-line reward string (reward + close-call note +
            // level-up), not just the visually-tighter single-line case.
            view._rewardText = CreateLabel(contentAreaGo.transform, "Reward", 34, FontStyle.Italic, 150);

            CreateSpacer(contentAreaGo.transform);

            // Only shown for actual fights (Enemy type) — Treasure never fights, per
            // CombatResolver.CreateSimulator returning null for it.
            view._playback = CombatPlaybackView.Create(contentAreaGo.transform, rect);

            CreateSpacer(contentAreaGo.transform);

            CreateButton(rect, "LeaveButton", "Leave", () => view.OnLeaveClicked());

            go.SetActive(false);
            return view;
        }

        public void Show(EncounterData data, PlayerLoadout loadout, PlayerProgression progression, PlayerHealth health,
            PlayerStats stats, GameSettings settings)
        {
            gameObject.SetActive(true);
            _data = data;
            _health = health;

            var isFight = data.type == EncounterType.Enemy;
            _playback.gameObject.SetActive(isFight);

            // Always enter at full of whatever CurrentMaxHealth currently is — the
            // fight is a separate instance from here on; PlayerHealth isn't touched
            // again until it's over (see RevealSequence/OnLeaveClicked).
            var sim = CombatResolver.CreateSimulator(loadout, stats, settings, data, health.CurrentMaxHealth);

            _outcomeText.text = "";
            _rewardText.text = "";

            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
            }

            _revealCoroutine = StartCoroutine(RevealSequence(sim, data.baseXpReward, loadout, progression, settings));
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

        // Leaving before the fight has finished counts as a loss — same max-HP penalty
        // as actually losing, not a free way to walk away from a bad matchup mid-fight.
        // _revealCoroutine is non-null for exactly that window (set in Show, cleared the
        // instant RevealSequence reaches its own Won/Lost below), so its presence alone
        // tells us whether this is an early leave.
        private void OnLeaveClicked()
        {
            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
                _revealCoroutine = null;
                _health.ApplyDefeatPenalty();
                EncounterLost?.Invoke(_data);
            }

            ReturnToFeedRequested?.Invoke();
        }

        private IEnumerator RevealSequence(CombatSimulator sim, int xpReward, PlayerLoadout loadout,
            PlayerProgression progression, GameSettings settings)
        {
            yield return StartCoroutine(_playback.Play(sim, loadout, settings, _data.artwork, _data.cardColor));

            var isVictory = _playback.LastOutcomeWasVictory;
            _outcomeText.text = isVictory ? "VICTORY" : "DEFEAT";
            _outcomeText.color = isVictory ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);

            if (isVictory)
            {
                var previousLevel = progression.Level;
                progression.AddXp(xpReward);

                // No difficulty choice any more on a plain encounter — always the base
                // (Common) reward tier; Dungeons are the vehicle for better loot now.
                var item = RewardGenerator.GenerateItem(0);
                loadout.AddToInventory(item);

                var rewardLine = $"Reward: {xpReward} XP + {item.title} (equip it from Profile)";
                if (_playback.WasCloseCall)
                {
                    rewardLine += "\nClose call — that one nearly had you.";
                }

                _rewardText.text = progression.Level > previousLevel
                    ? $"{rewardLine}\nLevel up! You're now level {progression.Level}."
                    : rewardLine;

                EncounterWon?.Invoke(_data);
            }
            else
            {
                _health.ApplyDefeatPenalty();
                _rewardText.text = $"No reward this time — your gear held you back. Your max HP drops to " +
                    $"{_health.CurrentMaxHealth}/{_health.TrueMaxHealth}, regenerating over time. " +
                    "This one's lost for good; gear up and look for the next one.";
                EncounterLost?.Invoke(_data);
            }

            _revealCoroutine = null;
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
