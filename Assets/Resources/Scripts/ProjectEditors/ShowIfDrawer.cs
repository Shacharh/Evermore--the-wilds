using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(ShowIfAttribute))]
public class ShowIfDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ShowIfAttribute showIf = (ShowIfAttribute)attribute;

        bool invert = false;
        string fieldName = showIf.boolFieldName;

        // Check for ! at the start
        if (fieldName.StartsWith("!"))
        {
            invert = true;
            fieldName = fieldName.Substring(1); // remove the !
        }

        SerializedProperty boolProp = property.serializedObject.FindProperty(fieldName);

        if (boolProp != null && (boolProp.boolValue != invert))
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ShowIfAttribute showIf = (ShowIfAttribute)attribute;

        bool invert = false;
        string fieldName = showIf.boolFieldName;

        if (fieldName.StartsWith("!"))
        {
            invert = true;
            fieldName = fieldName.Substring(1);
        }

        SerializedProperty boolProp = property.serializedObject.FindProperty(fieldName);

        if (boolProp != null && (boolProp.boolValue != invert))
            return EditorGUI.GetPropertyHeight(property, label, true);

        return 0f; // hide the field
    }
}
