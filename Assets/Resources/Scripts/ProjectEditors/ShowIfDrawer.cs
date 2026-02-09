using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(ShowIfAttribute))]
public class ShowIfDrawer : PropertyDrawer
{
    private bool ShouldShow(SerializedProperty property)
    {
        ShowIfAttribute showIf = (ShowIfAttribute)attribute;

        // Correctly find nested fields
        string relativePath = property.propertyPath.Replace(property.name, showIf.conditionFieldName);
        SerializedProperty conditionField = property.serializedObject.FindProperty(relativePath);

        if (conditionField == null)
            return true;

        bool show = false;

        if (!string.IsNullOrEmpty(showIf.compareValue))
        {
            if (conditionField.propertyType == SerializedPropertyType.Enum)
            {
                string currentEnumValue = conditionField.enumNames[conditionField.enumValueIndex];
                string[] compareValues = showIf.compareValue.Split('|');

                foreach (var compareValue in compareValues)
                {
                    if (currentEnumValue.Equals(compareValue.Trim(), System.StringComparison.OrdinalIgnoreCase))
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

        return showIf.invert ? !show : show;
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
