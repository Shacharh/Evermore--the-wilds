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
    private SerializedProperty targetShape;
    private SerializedProperty rangeTargetShapeSize;
    private SerializedProperty isDirect;
    private SerializedProperty inDirectHitPrecent;

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
        targetShape = serializedObject.FindProperty("targetShape");
        rangeTargetShapeSize = serializedObject.FindProperty("rangeTargetShapeSize");
        isDirect = serializedObject.FindProperty("isDirect");
        inDirectHitPrecent = serializedObject.FindProperty("inDirectHitPrecent");

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
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), valueProp);
                y += lineHeight + spacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), buffTypeProp);
                y += lineHeight + spacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), durationProp);
                y += lineHeight + spacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), chanceProp);
                y += lineHeight + spacing;

                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, lineHeight), isDebuffProp);
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
                height += lineHeight * 5 + spacing * 5; // buffType + duration + chance
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
        EditorGUILayout.PropertyField(targetShape);
        EditorGUILayout.PropertyField(rangeTargetShapeSize);

        EditorGUILayout.PropertyField(isDirect);
        if (!isDirect.boolValue)
            EditorGUILayout.PropertyField(inDirectHitPrecent);

        serializedObject.ApplyModifiedProperties();
    }
}
