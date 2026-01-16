using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(ShowIfAttribute))]
public class ShowIfDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ShowIfAttribute showIf = (ShowIfAttribute)attribute;
        SerializedProperty boolProp = property.serializedObject.FindProperty(showIf.boolFieldName);

        if (boolProp != null && boolProp.boolValue)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ShowIfAttribute showIf = (ShowIfAttribute)attribute;
        SerializedProperty boolProp = property.serializedObject.FindProperty(showIf.boolFieldName);

        if (boolProp != null && boolProp.boolValue)
            return EditorGUI.GetPropertyHeight(property, label, true);
        return 0f; // hide the field
    }
}
