using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(ShowIfAttribute))]
public class ShowIfDrawer : PropertyDrawer
{
    private bool ShouldShow(SerializedProperty property)
    {
        ShowIfAttribute showIf = (ShowIfAttribute)attribute;

        bool show = EvaluateCondition(property, showIf.conditionFieldName, showIf.compareValue, showIf.invert);

        // AND condition: both must pass
        if (show && !string.IsNullOrEmpty(showIf.andConditionFieldName))
            show = EvaluateCondition(property, showIf.andConditionFieldName, showIf.andCompareValue, false);

        return show;
    }

    private bool EvaluateCondition(SerializedProperty property, string fieldName, string compareValue, bool invert)
    {
        string relativePath = property.propertyPath.Replace(property.name, fieldName);
        SerializedProperty conditionField = property.serializedObject.FindProperty(relativePath);

        if (conditionField == null)
            return true;

        bool show = false;

        if (!string.IsNullOrEmpty(compareValue))
        {
            if (conditionField.propertyType == SerializedPropertyType.Enum)
            {
                string current = conditionField.enumNames[conditionField.enumValueIndex];
                foreach (var v in compareValue.Split('|'))
                {
                    if (current.Equals(v.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    {
                        show = true;
                        break;
                    }
                }
            }
        }
        else if (conditionField.propertyType == SerializedPropertyType.Boolean)
        {
            show = conditionField.boolValue;
        }
        else
        {
            show = true;
        }

        return invert ? !show : show;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (ShouldShow(property))
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (ShouldShow(property))
            return EditorGUI.GetPropertyHeight(property, label, true);

        return 0f;
    }
}
