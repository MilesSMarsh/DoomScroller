using System;
using System.Collections.Generic;
using DoomScroller.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Inventory
{
    // The Stats tab inside Profile — mirrors EquipSlotClusterView's role (a runtime-built
    // sub-component the host view creates and refreshes). One row per PlayerStat: name,
    // current value, a short effect description, and a "+" button that spends a point
    // immediately (no "-"/undo/respec this pass — matches this project's existing
    // immediate-action UI conventions).
    public class StatsPanelView : MonoBehaviour
    {
        private const float RowHeight = 130f;

        private static readonly Dictionary<PlayerStat, string> Descriptions = new()
        {
            { PlayerStat.Vigor, "Increases your max health." },
            { PlayerStat.Endurance, "Increases attack speed." },
            { PlayerStat.Strength, "Increases damage for weapons that scale off Strength." },
            { PlayerStat.Dexterity, "Increases damage for weapons that scale off Dexterity." },
            { PlayerStat.Intelligence, "Reduces weapon ability cooldowns." },
            { PlayerStat.Spirit, "Increases damage for weapons that scale off Spirit." }
        };

        private PlayerStats _stats;
        private Text _unspentPointsText;
        private readonly Dictionary<PlayerStat, Text> _valueTexts = new();
        private readonly Dictionary<PlayerStat, Button> _addButtons = new();

        public static StatsPanelView Create(Transform parent, PlayerStats stats)
        {
            var go = new GameObject("StatsPanel", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var view = go.AddComponent<StatsPanelView>();
            view._stats = stats;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            view._unspentPointsText = CreateLabel(rect, "UnspentPoints", "", 34, FontStyle.Bold);
            view._unspentPointsText.color = new Color(0.6f, 0.9f, 1f);

            foreach (PlayerStat stat in Enum.GetValues(typeof(PlayerStat)))
            {
                view.CreateStatRow(rect, stat);
            }

            stats.Changed += view.Refresh;
            view.Refresh();

            return view;
        }

        private void CreateStatRow(Transform parent, PlayerStat stat)
        {
            var rowGo = new GameObject($"Stat_{stat}", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var rowLayoutElement = rowGo.AddComponent<LayoutElement>();
            rowLayoutElement.preferredHeight = RowHeight;
            rowGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);

            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(20, 20, 10, 10);
            rowLayout.spacing = 16;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            var infoGo = new GameObject("Info", typeof(RectTransform));
            infoGo.transform.SetParent(rowGo.transform, false);
            var infoLayout = infoGo.AddComponent<LayoutElement>();
            infoLayout.minWidth = 0;
            infoLayout.preferredWidth = 0;
            infoLayout.flexibleWidth = 1;

            var infoColumnLayout = infoGo.AddComponent<VerticalLayoutGroup>();
            infoColumnLayout.childAlignment = TextAnchor.MiddleLeft;
            infoColumnLayout.childForceExpandWidth = true;
            infoColumnLayout.childForceExpandHeight = false;
            infoColumnLayout.childControlWidth = true;
            infoColumnLayout.childControlHeight = true;

            var titleText = CreateLabel(infoGo.transform, "Title", stat.ToString(), 32, FontStyle.Bold);
            titleText.alignment = TextAnchor.MiddleLeft;
            var descText = CreateLabel(infoGo.transform, "Description", Descriptions[stat], 22, FontStyle.Normal);
            descText.alignment = TextAnchor.MiddleLeft;
            descText.color = new Color(1f, 1f, 1f, 0.65f);

            var valueGo = new GameObject("Value", typeof(RectTransform));
            valueGo.transform.SetParent(rowGo.transform, false);
            var valueLayout = valueGo.AddComponent<LayoutElement>();
            valueLayout.preferredWidth = 70;
            valueLayout.flexibleWidth = 0;
            var valueText = valueGo.AddComponent<Text>();
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueText.fontSize = 40;
            valueText.fontStyle = FontStyle.Bold;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.color = Color.white;
            _valueTexts[stat] = valueText;

            _addButtons[stat] = CreateAddButton(rowGo.transform, () => _stats.Allocate(stat));
        }

        private void Refresh()
        {
            _unspentPointsText.text = $"Unspent Points: {_stats.UnspentPoints}";

            foreach (var kvp in _valueTexts)
            {
                kvp.Value.text = _stats.Get(kvp.Key).ToString();
            }

            foreach (var kvp in _addButtons)
            {
                kvp.Value.interactable = _stats.UnspentPoints > 0;
            }
        }

        private static Button CreateAddButton(Transform parent, Action onClick)
        {
            var go = new GameObject("AddButton", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 90;
            layoutElement.preferredHeight = 90;
            layoutElement.flexibleWidth = 0;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.25f, 0.6f, 0.3f, 0.85f);

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
            labelText.fontSize = 44;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = "+";
            labelText.raycastTarget = false;

            return button;
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
            layoutElement.preferredHeight = fontSize + 14;

            return text;
        }
    }
}
