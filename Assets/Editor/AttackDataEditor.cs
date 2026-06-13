using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

[CustomEditor(typeof(AttackData))]
public class AttackDataEditor : Editor
{
    private SerializedProperty id;
    private SerializedProperty displayName;
    private SerializedProperty description;
    private SerializedProperty element;
    private SerializedProperty effects;
    private SerializedProperty maxPP;
    private SerializedProperty consumeActionPoints;
    private SerializedProperty guaranteedHit;
    private SerializedProperty accuracy;
    private SerializedProperty range;
    private SerializedProperty targetTeam;
    private SerializedProperty targetShape;
    private SerializedProperty rangeTargetShapeSize;
    private SerializedProperty isDirect;
    private SerializedProperty inDirectHitPrecent;
    private SerializedProperty vfxPrefab;
    private SerializedProperty vfxTarget;

    private ReorderableList effectsList;

    private void OnEnable()
    {
        id = serializedObject.FindProperty("id");
        displayName = serializedObject.FindProperty("displayName");
        description = serializedObject.FindProperty("description");
        element = serializedObject.FindProperty("element");
        effects = serializedObject.FindProperty("effects");
        maxPP = serializedObject.FindProperty("maxPP");
        consumeActionPoints = serializedObject.FindProperty("consumeActionPoints");
        guaranteedHit = serializedObject.FindProperty("guaranteedHit");
        accuracy = serializedObject.FindProperty("accuracy");
        range = serializedObject.FindProperty("range");
        targetTeam = serializedObject.FindProperty("targetTeam");
        targetShape = serializedObject.FindProperty("targetShape");
        rangeTargetShapeSize = serializedObject.FindProperty("rangeTargetShapeSize");
        isDirect = serializedObject.FindProperty("isDirect");
        inDirectHitPrecent = serializedObject.FindProperty("inDirectHitPrecent");
        vfxPrefab = serializedObject.FindProperty("vfxPrefab");
        vfxTarget = serializedObject.FindProperty("vfxTarget");

        // Create the ReorderableList
        effectsList = new ReorderableList(serializedObject, effects, true, true, true, true);

        effectsList.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, "Effects");
        };

        effectsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            SerializedProperty elementProp = effects.GetArrayElementAtIndex(index);

            SerializedProperty categoryProp = elementProp.FindPropertyRelative("category");
            SerializedProperty valueProp = elementProp.FindPropertyRelative("value");
            SerializedProperty stageCountProp = elementProp.FindPropertyRelative("stageCount");
            SerializedProperty buffTypeProp = elementProp.FindPropertyRelative("buffType");
            SerializedProperty isDebuffProp = elementProp.FindPropertyRelative("isDebuff");
            SerializedProperty durationProp = elementProp.FindPropertyRelative("duration");
            SerializedProperty chanceProp = elementProp.FindPropertyRelative("chance");
            SerializedProperty isInstantHealProp = elementProp.FindPropertyRelative("isInstantHeal");
            SerializedProperty statusEffectProp = elementProp.FindPropertyRelative("statusEffect");
            SerializedProperty selfInflictedProp = elementProp.FindPropertyRelative("selfInflicted");

            float y = rect.y;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 2f;

            EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), $"Effect {index + 1}", EditorStyles.boldLabel);
            y += lineHeight + spacing;

            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), categoryProp);
            y += lineHeight + spacing;

            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), selfInflictedProp);
            y += lineHeight + spacing;

            if (categoryProp.enumValueIndex == (int)AttackEnum.AttackCategory.damage)
            {
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), valueProp);
                y += lineHeight + spacing;
            }

            // Show buffType, duration, chance only if category is buff/debuff
            if (categoryProp.enumValueIndex == (int)AttackEnum.AttackCategory.buff)
            {
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), isDebuffProp);
                y += lineHeight + spacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), buffTypeProp);
                y += lineHeight + spacing;

                string pickerLabel = isDebuffProp.boolValue ? "Debuff Strength" : "Buff Strength";
                EditorGUI.LabelField(new Rect(rect.x, y, rect.width, lineHeight), pickerLabel, EditorStyles.boldLabel);
                y += lineHeight + spacing;

                float pickerH = lineHeight * 2f;
                DrawStagePicker(new Rect(rect.x, y, rect.width, pickerH), stageCountProp, isDebuffProp.boolValue);
                y += pickerH + spacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), durationProp);
                y += lineHeight + spacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), chanceProp);
                y += lineHeight + spacing;
            }

            // Show duration & isInstantHeal if category is heal
            if (categoryProp.enumValueIndex == (int)AttackEnum.AttackCategory.heal)
            {
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), valueProp);
                y += lineHeight + spacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), durationProp);
                y += lineHeight + spacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), isInstantHealProp);
                y += lineHeight + spacing;
            }

            if (categoryProp.enumValueIndex == (int)AttackEnum.AttackCategory.status) 
            {
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), chanceProp);
                y += lineHeight + spacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), statusEffectProp);
                y += lineHeight + spacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), durationProp);
                y += lineHeight + spacing;
            }
        };

        effectsList.elementHeightCallback = (int index) =>
        {
            SerializedProperty elementProp = effects.GetArrayElementAtIndex(index);
            SerializedProperty categoryProp = elementProp.FindPropertyRelative("category");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 2f;

            float height = lineHeight * 4 + spacing * 4; // category + value + label

            if (categoryProp.enumValueIndex == (int)AttackEnum.AttackCategory.buff)
            {
                // isDebuff + buffType + label + picker(2×) + duration + chance
                height += lineHeight * 7 + spacing * 6;
            }

            if (categoryProp.enumValueIndex == (int)AttackEnum.AttackCategory.heal)
            {
                height += lineHeight * 3 + spacing * 3; // duration + isInstantHeal
            }

            if (categoryProp.enumValueIndex == (int)AttackEnum.AttackCategory.status)
            {
                height += lineHeight * 2 + spacing * 2; // duration + isInstantHeal
            }

            return height;
        };
    }

    private static void DrawStagePicker(Rect rect, SerializedProperty stageCountProp, bool isDebuff)
    {
        // Multipliers match Universal_StatStageConfig stages +1 → +6 (and mirror for debuff)
        float[] buffMults   = { 1.25f, 1.50f, 1.75f, 2.00f, 2.25f, 2.50f };
        float[] debuffMults = { 0.90f, 0.75f, 0.65f, 0.50f, 0.35f, 0.20f };

        Color[] buffColors = {
            new Color(0.60f, 0.90f, 0.60f),
            new Color(0.40f, 0.85f, 0.40f),
            new Color(0.20f, 0.80f, 0.20f),
            new Color(0.05f, 0.75f, 0.05f),
            new Color(0.00f, 0.65f, 0.00f),
            new Color(0.00f, 0.55f, 0.00f),
        };
        Color[] debuffColors = {
            new Color(1.00f, 0.80f, 0.55f),
            new Color(1.00f, 0.65f, 0.40f),
            new Color(1.00f, 0.50f, 0.25f),
            new Color(1.00f, 0.30f, 0.15f),
            new Color(0.95f, 0.15f, 0.05f),
            new Color(0.85f, 0.00f, 0.00f),
        };

        float[] mults  = isDebuff ? debuffMults : buffMults;
        Color[] colors = isDebuff ? debuffColors : buffColors;
        int     current = stageCountProp.intValue;

        float    btnW    = rect.width / 6f;
        GUIStyle style   = new GUIStyle(GUI.skin.button) { fontSize = 10, alignment = TextAnchor.MiddleCenter };

        Color borderColor = isDebuff ? new Color(1f, 0.85f, 0f) : new Color(0f, 1f, 0.4f);

        for (int i = 1; i <= 6; i++)
        {
            Rect btnRect = new Rect(rect.x + (i - 1) * btnW, rect.y, btnW - 2f, rect.height);

            bool selected = (i == current);

            // Bright border rect drawn behind the button for the selected stage
            if (selected)
            {
                EditorGUI.DrawRect(new Rect(btnRect.x - 2, btnRect.y - 2, btnRect.width + 4, btnRect.height + 4), borderColor);
                EditorGUI.DrawRect(new Rect(btnRect.x - 1, btnRect.y - 1, btnRect.width + 2, btnRect.height + 2), Color.black);
            }

            Color c = colors[i - 1];
            if (!selected)
                c = Color.Lerp(c, new Color(0.35f, 0.35f, 0.35f), 0.72f);

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = c;
            style.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;

            string sign  = isDebuff ? "−" : "+";
            string label = $"{sign}{i}\n×{mults[i - 1]:F2}";

            if (GUI.Button(btnRect, label, style))
                stageCountProp.intValue = i;

            GUI.backgroundColor = prev;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(id);
        EditorGUILayout.PropertyField(displayName);
        EditorGUILayout.PropertyField(description);
        EditorGUILayout.PropertyField(element);

        EditorGUILayout.Space();
        effectsList.DoLayoutList();

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(maxPP);
        EditorGUILayout.PropertyField(consumeActionPoints);
        EditorGUILayout.PropertyField(guaranteedHit);
        if (!guaranteedHit.boolValue)
            EditorGUILayout.PropertyField(accuracy);

        EditorGUILayout.PropertyField(range);
        EditorGUILayout.PropertyField(targetTeam);
        EditorGUILayout.PropertyField(targetShape);
        EditorGUILayout.PropertyField(rangeTargetShapeSize);

        EditorGUILayout.PropertyField(isDirect);
        if (!isDirect.boolValue)
            EditorGUILayout.PropertyField(inDirectHitPrecent);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("VFX", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(vfxPrefab);
        EditorGUILayout.PropertyField(vfxTarget);

        serializedObject.ApplyModifiedProperties();
    }
}
