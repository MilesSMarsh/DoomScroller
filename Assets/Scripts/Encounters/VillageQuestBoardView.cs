using DoomScroller.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // One establishment in the Village (see VillageView): the Quest Board, listing the
    // day's missions from a DailyMissionBoard with a Claim button for each completed one.
    public class VillageQuestBoardView : MonoBehaviour
    {
        private const float RowHeight = 120f;

        private DailyMissionBoard _board;
        private PlayerWallet _wallet;
        private Transform _content;

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
            view._content = go.transform;
            view.Render();

            return view;
        }

        public void Bind(DailyMissionBoard board, PlayerWallet wallet)
        {
            if (_board != null)
            {
                _board.Changed -= Render;
            }

            _board = board;
            _wallet = wallet;

            if (_board != null)
            {
                _board.Changed += Render;
            }

            Render();
        }

        private void OnDestroy()
        {
            if (_board != null)
            {
                _board.Changed -= Render;
            }
        }

        private void Render()
        {
            if (_content == null)
            {
                return;
            }

            for (var i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            if (_board == null)
            {
                var empty = CreateLabel(_content, "Body", "No missions today.", 30, FontStyle.Italic);
                empty.color = new Color(1f, 1f, 1f, 0.6f);
                return;
            }

            CreateLabel(_content, "Title", "Daily Missions", 36, FontStyle.Bold);

            for (var i = 0; i < _board.Count; i++)
            {
                CreateRow(i, _board.GetMission(i));
            }
        }

        private void CreateRow(int index, DailyMission mission)
        {
            var rowGo = new GameObject("Mission_" + index, typeof(RectTransform));
            rowGo.transform.SetParent(_content, false);
            var rowElement = rowGo.AddComponent<LayoutElement>();
            rowElement.minHeight = RowHeight;
            rowElement.preferredHeight = RowHeight;
            rowElement.flexibleHeight = 0;
            rowGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);

            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12;
            rowLayout.padding = new RectOffset(12, 12, 8, 8);
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            var infoGo = new GameObject("Info", typeof(RectTransform));
            infoGo.transform.SetParent(rowGo.transform, false);
            var infoElement = infoGo.AddComponent<LayoutElement>();
            infoElement.minWidth = 0;
            infoElement.preferredWidth = 0;
            infoElement.flexibleWidth = 1;
            var infoLayout = infoGo.AddComponent<VerticalLayoutGroup>();
            infoLayout.childForceExpandWidth = true;
            infoLayout.childForceExpandHeight = false;
            infoLayout.childControlWidth = true;
            infoLayout.childControlHeight = true;

            var description = CreateLabel(infoGo.transform, "Description", mission.Description, 28, FontStyle.Bold);
            description.alignment = TextAnchor.MiddleLeft;
            var progress = CreateLabel(infoGo.transform, "Progress",
                mission.Progress + "/" + mission.Target + "  (" + mission.GoldReward + "g)", 24, FontStyle.Normal);
            progress.alignment = TextAnchor.MiddleLeft;
            progress.color = new Color(1f, 1f, 1f, 0.7f);

            var buttonGo = new GameObject("ClaimButton", typeof(RectTransform));
            buttonGo.transform.SetParent(rowGo.transform, false);
            var buttonElement = buttonGo.AddComponent<LayoutElement>();
            buttonElement.minWidth = 200;
            buttonElement.preferredWidth = 200;
            buttonElement.flexibleWidth = 0;

            var image = buttonGo.AddComponent<Image>();
            image.color = new Color(0.2f, 0.55f, 0.3f, 0.7f);
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = mission.IsComplete && !mission.Claimed;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(buttonGo.transform, false);
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
            labelText.text = mission.Claimed ? "Claimed" : "Claim";
            labelText.raycastTarget = false;

            button.onClick.AddListener(() => OnClaim(index));
        }

        private void OnClaim(int index)
        {
            if (_board == null)
            {
                return;
            }

            // Claim fires board.Changed, which re-renders.
            var gold = _board.Claim(index);
            if (gold > 0 && _wallet != null)
            {
                _wallet.AddGold(gold);
            }
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
