using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Custom property drawer for any string field tagged with [AnimatorBool].
/// Replaces the plain text input with a dropdown showing every Bool parameter
/// found across ALL AnimatorController assets in the project.
///
/// Usage on any string field:
///     [AnimatorBool] public string AnimationBool;
/// </summary>
[CustomPropertyDrawer(typeof(AnimatorBoolAttribute))]
public class AnimatorBoolDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Only works on string fields
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "AnimatorBool requires a string field");
            return;
        }

        List<string> bools = GetAllProjectBools();

        if (bools.Count == 0)
        {
            // No bools found -- fall back to plain text field with a warning tint
            Color old = GUI.color;
            GUI.color = new Color(1f, 0.8f, 0.5f);
            EditorGUI.PropertyField(position, property, label);
            GUI.color = old;
            return;
        }

        // Add a blank entry at index 0 so the field can be left empty
        bools.Insert(0, "-- None --");

        // Find the index of the currently saved value
        string current = property.stringValue;
        int currentIndex = bools.IndexOf(current);
        if (currentIndex < 0) currentIndex = 0; // default to "-- None --"

        EditorGUI.BeginProperty(position, label, property);
        int chosen = EditorGUI.Popup(position, label.text, currentIndex, bools.ToArray());

        // Write back -- if "-- None --" is chosen, store an empty string
        property.stringValue = (chosen == 0) ? string.Empty : bools[chosen];
        EditorGUI.EndProperty();
    }

    // -------------------------------------------------------------------------
    // Scan every AnimatorController in the project and collect Bool names
    // -------------------------------------------------------------------------
    private static List<string> cachedBools;
    private static double lastScanTime = -1;
    private const double ScanCooldownSeconds = 3.0; // re-scan at most every 3 seconds

    private static List<string> GetAllProjectBools()
    {
        // Cache results so we are not hammering AssetDatabase every OnGUI frame
        if (cachedBools != null && EditorApplication.timeSinceStartup - lastScanTime < ScanCooldownSeconds)
            return new List<string>(cachedBools);

        var bools = new SortedSet<string>(); // sorted = alphabetical dropdown

        string[] guids = AssetDatabase.FindAssets("t:AnimatorController");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimatorController ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ac == null) continue;

            foreach (AnimatorControllerParameter param in ac.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Bool)
                    bools.Add(param.name);
            }
        }

        cachedBools = new List<string>(bools);
        lastScanTime = EditorApplication.timeSinceStartup;
        return new List<string>(cachedBools);
    }
}