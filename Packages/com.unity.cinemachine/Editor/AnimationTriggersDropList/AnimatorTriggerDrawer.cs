// Place this file in: Assets/Editor/AnimatorTriggerDrawer.cs
// The Editor folder makes sure this code is NEVER included in a build.

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Custom property drawer for any string field tagged with [AnimatorTrigger].
/// Replaces the plain text input with a dropdown showing every Trigger parameter
/// found across ALL AnimatorController assets in the project.
///
/// Usage on any string field:
///     [AnimatorTrigger] public string AnimationTrigger;
/// </summary>
[CustomPropertyDrawer(typeof(AnimatorTriggerAttribute))]
public class AnimatorTriggerDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Only works on string fields
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "AnimatorTrigger requires a string field");
            return;
        }

        List<string> triggers = GetAllProjectTriggers();

        if (triggers.Count == 0)
        {
            // No triggers found -- fall back to plain text field with a warning tint
            Color old = GUI.color;
            GUI.color = new Color(1f, 0.8f, 0.5f);
            EditorGUI.PropertyField(position, property, label);
            GUI.color = old;
            return;
        }

        // Add a blank entry at index 0 so the field can be left empty
        triggers.Insert(0, "-- None --");

        // Find the index of the currently saved value
        string current = property.stringValue;
        int currentIndex = triggers.IndexOf(current);
        if (currentIndex < 0) currentIndex = 0; // default to "-- None --"

        EditorGUI.BeginProperty(position, label, property);

        int chosen = EditorGUI.Popup(position, label.text, currentIndex, triggers.ToArray());

        // Write back -- if "-- None --" is chosen, store an empty string
        property.stringValue = (chosen == 0) ? string.Empty : triggers[chosen];

        EditorGUI.EndProperty();
    }

    // -------------------------------------------------------------------------
    // Scan every AnimatorController in the project and collect Trigger names
    // -------------------------------------------------------------------------

    private static List<string> cachedTriggers;
    private static double lastScanTime = -1;
    private const double ScanCooldownSeconds = 3.0; // re-scan at most every 3 seconds

    private static List<string> GetAllProjectTriggers()
    {
        // Cache results so we are not hammering AssetDatabase every OnGUI frame
        if (cachedTriggers != null && EditorApplication.timeSinceStartup - lastScanTime < ScanCooldownSeconds)
            return new List<string>(cachedTriggers);

        var triggers = new SortedSet<string>(); // sorted = alphabetical dropdown

        string[] guids = AssetDatabase.FindAssets("t:AnimatorController");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimatorController ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ac == null) continue;

            foreach (AnimatorControllerParameter param in ac.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Trigger)
                    triggers.Add(param.name);
            }
        }

        cachedTriggers = new List<string>(triggers);
        lastScanTime = EditorApplication.timeSinceStartup;
        return new List<string>(cachedTriggers);
    }
}