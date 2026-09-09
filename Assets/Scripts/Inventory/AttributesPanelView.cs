using System;
using System.Collections.Generic;
using DoomScroller.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Inventory
{
    // The Attributes tab inside Profile — mirrors EquipSlotClusterView's role (a
    // runtime-built sub-component the host view creates and refreshes). One row per
    // PlayerAttribute: name, current value, a short effect description, and a "+"
    // button that spends a point immediately (no "-"/undo/respec this pass — matches
    // this project's existing immediate-action UI conventions).
    public class AttributesPanelView : MonoBehaviour
    {
        private const float RowHeight = 130f;

        private static readonly Dictionary<PlayerAttribute, string> Descriptions = new()
        {
            { PlayerAttribute.Constitution, "Increases max health and melee weapon damage." },
            { PlayerAttribute.Agility, "Increases attack speed and ranged weapon damage." },
            { PlayerAttribute.Spirit, "Reduces ability cooldowns and increases magic weapon damage." },
            { PlayerAttribute.Expertise, "Increases resource yields and discounts gold costs." }
        };

        private PlayerAttributes _attributes;
        private Text _unspentPointsText;
        private readonly Dictionary<PlayerAttribute, Text> _valueTexts = new();
        private readonly Dictionary<PlayerAttribute, Button> _addButtons = new();

        public static AttributesPanelView Create(Transform parent, PlayerAttributes attributes)
        {
            var go = new GameObject("AttributesPanel", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var view = go.AddComponent<AttributesPanelView>();
            view._attributes = attributes;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            view._unspentPointsText = CreateLabel(rect, "UnspentPoints", "", 34, FontStyle.Bold);
            view._unspentPointsText.color = new Color(0.6f, 0.9f, 1f);

            foreach (PlayerAttribute attribute in Enum.GetValues(typeof(PlayerAttribute)))
            {
                view.CreateAttributeRow(rect, attribute);
            }

            attributes.Changed += view.Refresh;
            view.Refresh();

            return view;
        }

        private void CreateAttributeRow(Transform parent, PlayerAttribute attribute)
        {
            var rowGo = new GameObject($"Attribute_{attribute}", typeof(RectTransform));
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

            var titleText = CreateLabel(infoGo.transform, "Title", attribute.ToString(), 32, FontStyle.Bold);
            titleText.alignment = TextAnchor.MiddleLeft;
            var descText = CreateLabel(infoGo.transform, "Description", Descriptions[attribute], 22, FontStyle.Normal);
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
            _valueTexts[attribute] = valueText;

            _addButtons[attribute] = CreateAddButton(rowGo.transform, () => _attributes.Allocate(attribute));
        }

        private void Refresh()
        {
            _unspentPointsText.text = $"Unspent Points: {_attributes.UnspentPoints}";

            foreach (var kvp in _valueTexts)
            {
                kvp.Value.text = _attributes.Get(kvp.Key).ToString();
            }

            foreach (var kvp in _addButtons)
            {
                kvp.Value.interactable = _attributes.UnspentPoints > 0;
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
