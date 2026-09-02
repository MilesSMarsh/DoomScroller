using UnityEditor;
using UnityEngine;

namespace DoomScroller.Feed.Editor
{
    // Custom inspector for EncounterData — the whole point is that making an encounter
    // is "drop in art, fill in a few fields, see it live" without ever pressing Play.
    // See EncounterData's own header comment for where the finished asset needs to live.
    [CustomEditor(typeof(EncounterData))]
    public class EncounterDataEditor : UnityEditor.Editor
    {
        private SerializedProperty _title;
        private SerializedProperty _type;
        private SerializedProperty _artwork;
        private SerializedProperty _cardColor;
        private SerializedProperty _rewardSummary;
        private SerializedProperty _baseXpReward;
        private SerializedProperty _baseChallenge;

        private void OnEnable()
        {
            _title = serializedObject.FindProperty("title");
            _type = serializedObject.FindProperty("type");
            _artwork = serializedObject.FindProperty("artwork");
            _cardColor = serializedObject.FindProperty("cardColor");
            _rewardSummary = serializedObject.FindProperty("rewardSummary");
            _baseXpReward = serializedObject.FindProperty("baseXpReward");
            _baseChallenge = serializedObject.FindProperty("baseChallenge");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPreview();
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_title);
            EditorGUILayout.PropertyField(_type);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Artwork", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_artwork);
            EditorGUILayout.PropertyField(_cardColor,
                new GUIContent("Fallback Color", "Shown when no artwork is assigned."));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Rewards & Difficulty", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_baseXpReward);

            var isTreasure = (EncounterType)_type.enumValueIndex == EncounterType.Treasure;
            using (new EditorGUI.DisabledScope(isTreasure))
            {
                EditorGUILayout.PropertyField(_baseChallenge);
            }
            if (isTreasure)
            {
                EditorGUILayout.HelpBox("Ignored for Treasure — treasure never fights.", MessageType.None);
            }

            EditorGUILayout.PropertyField(_rewardSummary);
            if (GUILayout.Button("Auto-fill reward text from XP"))
            {
                _rewardSummary.stringValue = isTreasure
                    ? "Guaranteed loot: no combat required"
                    : $"{_baseXpReward.intValue} XP + Gear";
            }

            serializedObject.ApplyModifiedProperties();
        }

        // A rough live mock of how this reads as an actual feed card (see FeedCardView):
        // full-bleed art (or the fallback color), a bottom scrim, and the caption stack.
        private void DrawPreview()
        {
            var data = (EncounterData)target;
            var rect = GUILayoutUtility.GetRect(0, 220, GUILayout.ExpandWidth(true));

            if (data.artwork != null)
            {
                GUI.DrawTextureWithTexCoords(rect, data.artwork.texture, GetSpriteUV(data.artwork));
            }
            else
            {
                EditorGUI.DrawRect(rect, data.cardColor);
            }

            var scrimRect = new Rect(rect.x, rect.y + rect.height * 0.6f, rect.width, rect.height * 0.4f);
            EditorGUI.DrawRect(scrimRect, new Color(0f, 0f, 0f, 0.55f));

            var titleStyle = new GUIStyle(EditorStyles.whiteBoldLabel) { fontSize = 14 };
            var badgeStyle = new GUIStyle(EditorStyles.whiteLabel) { fontStyle = FontStyle.Italic };
            var rewardStyle = new GUIStyle(EditorStyles.whiteLabel);

            var titleRect = new Rect(rect.x + 10, scrimRect.y + 8, rect.width - 20, 22);
            var badgeRect = new Rect(rect.x + 10, titleRect.yMax + 2, rect.width - 20, 18);
            var rewardRect = new Rect(rect.x + 10, badgeRect.yMax + 2, rect.width - 20, 18);

            GUI.Label(titleRect, string.IsNullOrEmpty(data.title) ? "(untitled)" : data.title, titleStyle);
            GUI.Label(badgeRect, $"{data.type} · Standard", badgeStyle);
            GUI.Label(rewardRect, string.IsNullOrEmpty(data.rewardSummary) ? "(no reward text)" : data.rewardSummary, rewardStyle);
        }

        private static Rect GetSpriteUV(Sprite sprite)
        {
            var textureRect = sprite.textureRect;
            var texture = sprite.texture;
            return new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);
        }
    }
}
